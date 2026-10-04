import http from 'node:http';
import {randomBytes,randomInt,scryptSync,timingSafeEqual,createHash,createHmac} from 'node:crypto';
import {pathToFileURL} from 'node:url';
const digest=s=>createHash('sha256').update(s).digest('hex');
const publicUser=u=>({username:u.username,email:u.email,verified:u.verified});
const fail=(message,status=400)=>{const e=Error(message);e.status=status;throw e};
const hashPassword=p=>{const salt=randomBytes(16).toString('hex');return salt+':'+scryptSync(p,salt,64).toString('hex')};
const checkPassword=(p,stored)=>{try{const [salt,hash]=stored.split(':');return timingSafeEqual(scryptSync(p,salt,64),Buffer.from(hash,'hex'))}catch{return false}};
const validPassword=p=>{if(typeof p!=='string'||p.length<10||p.length>256)fail('Password must contain 10–256 characters.')};
const emailOf=v=>{const e=String(v||'').trim().toLowerCase();if(e.length>254||!/^\S+@[^\s@]+\.[^\s@]+$/.test(e))fail('Enter a valid email address.');return e};
const usernameOf=v=>{const n=String(v||'').trim();if(!/^[\p{L}\p{N}_ .-]{3,32}$/u.test(n))fail('Username must contain 3–32 letters, numbers, spaces, dots, dashes or underscores.');return n};
export function createApp({store,sendCode,secret,origins=[],now=()=>Date.now()}){
 if(!secret||secret.length<32)throw Error('OTP_SECRET must contain at least 32 characters.');
 let queue=Promise.resolve();const limits=new Map();
 const otpHash=(email,code)=>createHmac('sha256',secret).update(email+':'+code).digest('hex');
 async function issue(db,user,email=user.email){if(user.otp&&now()-user.otp.sent<60000)fail('Wait one minute before requesting another code.',429);const code=String(randomInt(100000,1000000));user.otp={hash:otpHash(email,code),email,expires:now()+600000,sent:now(),attempts:0};await store.save(db);await sendCode(email,code)}
 function session(db,user){const token=randomBytes(32).toString('base64url');db.sessions=db.sessions.filter(s=>s.expires>now());db.sessions.push({hash:digest(token),userId:user.id,expires:now()+30*86400000});return {token,user:publicUser(user)}}
 return http.createServer(async(req,res)=>{
  const origin=req.headers.origin;res.setHeader('Cache-Control','no-store');res.setHeader('X-Content-Type-Options','nosniff');
  if(origin){if(!origins.includes(origin)){res.writeHead(403);res.end();return}res.setHeader('Access-Control-Allow-Origin',origin);res.setHeader('Vary','Origin')}
  if(req.method==='OPTIONS'){res.setHeader('Access-Control-Allow-Methods','GET,POST,PATCH,OPTIONS');res.setHeader('Access-Control-Allow-Headers','Authorization,Content-Type');res.writeHead(204);res.end();return}
  if(req.url==='/health'){res.end('{"ok":true}');return}
  try{
   let raw='';for await(const chunk of req){raw+=chunk;if(raw.length>8192)fail('Request too large.',413)}const body=raw?JSON.parse(raw):{};
   // Key by socket, never trust arbitrary forwarded headers. Email limits survive restarts in the private account file.
   const key=req.socket.remoteAddress,limit=limits.get(key)||{start:now(),count:0};if(now()-limit.start>60000){limit.start=now();limit.count=0}if(++limit.count>60)fail('Too many requests. Try again shortly.',429);limits.set(key,limit);if(limits.size>10000)for(const [k,v] of limits)if(now()-v.start>60000)limits.delete(k);
   const work=async()=>{
    const db=await store.load();db.users??=[];db.sessions??=[];let response;
    const path=req.url,method=req.method;
    if(path==='/auth/register'&&method==='POST'){
     const email=emailOf(body.email),username=usernameOf(body.username);validPassword(body.password);
     const existing=db.users.find(u=>u.email===email||u.pendingEmail===email);if(existing)fail('Account exists. Sign in or resend your verification code.');
     if(db.users.some(u=>u.username.toLowerCase()===username.toLowerCase()))fail('This username is already taken.');
     const user={id:randomBytes(16).toString('hex'),email,username,password:hashPassword(body.password),verified:false};db.users.push(user);await issue(db,user);response={verificationRequired:true};
    }else if(path==='/auth/login'&&method==='POST'){
     const email=emailOf(body.email),u=db.users.find(u=>u.email===email);validPassword(body.password);
     if(u?.lockUntil>now())fail('Too many sign-in attempts. Try again in 15 minutes.',429);
     if(!u||!checkPassword(body.password,u.password)){if(u){u.failures=(u.failures||0)+1;if(u.failures>=5){u.lockUntil=now()+900000;u.failures=0}await store.save(db)}fail('Email or password is incorrect.',401)}
     u.failures=0;u.lockUntil=0;
     if(!u.verified){await store.save(db);return {verificationRequired:true}}
     response=session(db,u);await store.save(db);
    }else if(path==='/auth/resend'&&method==='POST'){
     const email=emailOf(body.email),u=db.users.find(u=>u.email===email&&!u.verified||u.pendingEmail===email);
     if(u)await issue(db,u,email);response={ok:true};
    }else if(path==='/auth/verify'&&method==='POST'){
     const email=emailOf(body.email),u=db.users.find(u=>u.otp?.email===email);const code=String(body.code||'');
     if(!u?.otp||u.otp.expires<now()||u.otp.attempts>=5)fail('Code expired. Request a new one.');
     u.otp.attempts++;
     if(!/^\d{6}$/.test(code)||!timingSafeEqual(Buffer.from(u.otp.hash,'hex'),Buffer.from(otpHash(email,code),'hex'))){await store.save(db);fail('Incorrect verification code.')}
     if(db.users.some(x=>x.id!==u.id&&(x.email===email||x.pendingEmail===email)))fail('Email is already in use.');
     u.email=email;u.verified=true;delete u.otp;delete u.pendingEmail;db.sessions=db.sessions.filter(s=>s.userId!==u.id);response=session(db,u);await store.save(db);
    }else{
     const token=String(req.headers.authorization||'').replace(/^Bearer /,'');const s=db.sessions.find(s=>s.hash===digest(token)&&s.expires>now());const u=s&&db.users.find(u=>u.id===s.userId&&u.verified);if(!u)fail('Sign in to continue.',401);
     if(path==='/me'&&method==='GET')response={user:publicUser(u)};
     else if(path==='/auth/logout'&&method==='POST'){db.sessions=db.sessions.filter(x=>x!==s);await store.save(db);response={ok:true}}
     else if(path==='/me'&&method==='PATCH'){
      validPassword(body.password);if(!checkPassword(body.password,u.password))fail('Current password is incorrect.',401);
      const username=usernameOf(body.username),email=emailOf(body.email);if(db.users.some(x=>x.id!==u.id&&(x.username.toLowerCase()===username.toLowerCase()||x.email===email||x.pendingEmail===email)))fail('Username or email is already in use.');
      u.username=username;if(body.newPassword){validPassword(body.newPassword);u.password=hashPassword(body.newPassword);db.sessions=db.sessions.filter(x=>x.userId!==u.id||x===s)}
      if(email!==u.email){u.pendingEmail=email;await issue(db,u,email);response={verificationRequired:true}}else{await store.save(db);response={user:publicUser(u)}}
     }else fail('Not found.',404);
    }
    return response;
   };
   const pending=queue.then(work);queue=pending.catch(()=>{});const result=await pending;res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify(result));
  }catch(e){res.writeHead(e.status||500,{'Content-Type':'application/json'});res.end(JSON.stringify({error:e.status?e.message:'Account service unavailable. Please try again.'}))}
 });
}
export class GitHubStore{
 constructor(repo,token){if(!/^[\w.-]+\/[\w.-]+$/.test(repo||''))throw Error('ACCOUNT_REPOSITORY must be owner/private-repo.');this.base='https://api.github.com/repos/'+repo;this.token=token;this.sha=null}
 async request(path,body){const r=await fetch(this.base+path,{method:body?'PUT':'GET',headers:{Authorization:'Bearer '+this.token,Accept:'application/vnd.github+json','User-Agent':'AG-Account-Server','Content-Type':'application/json'},...(body?{body:JSON.stringify(body)}:{}),signal:AbortSignal.timeout(15000)});return {status:r.status,data:await r.json()}}
 async load(){const meta=await this.request('');if(meta.status!==200||meta.data.private!==true)throw Error('Accounts must be stored in a private GitHub repository.');const r=await this.request('/contents/accounts.json');if(r.status===404){this.sha=null;return {users:[],sessions:[]}}if(r.status!==200)throw Error('Account storage unavailable');this.sha=r.data.sha;return JSON.parse(Buffer.from(r.data.content,'base64').toString('utf8'))}
 async save(db){const r=await this.request('/contents/accounts.json',{message:'Update private account records',content:Buffer.from(JSON.stringify(db)).toString('base64'),...(this.sha?{sha:this.sha}:{})});if(r.status!==200&&r.status!==201)throw Error('Account write failed; retry request');this.sha=r.data.content.sha}
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const env=process.env;for(const key of ['ACCOUNT_REPOSITORY','ACCOUNT_GITHUB_TOKEN','OTP_SECRET','RESEND_API_KEY','MAIL_FROM','ALLOWED_ORIGINS'])if(!env[key])throw Error('Missing configuration: '+key);
 const store=new GitHubStore(env.ACCOUNT_REPOSITORY,env.ACCOUNT_GITHUB_TOKEN);
 const sendCode=async(email,code)=>{const r=await fetch('https://api.resend.com/emails',{method:'POST',headers:{Authorization:'Bearer '+env.RESEND_API_KEY,'Content-Type':'application/json'},body:JSON.stringify({from:env.MAIL_FROM,to:[email],subject:'Your AG Launcher verification code',text:`Your code is ${code}. It expires in 10 minutes. If you did not request this, ignore this email.`}),signal:AbortSignal.timeout(15000)});if(!r.ok)throw Error('Email delivery failed')};
 await store.load();createApp({store,sendCode,secret:env.OTP_SECRET,origins:env.ALLOWED_ORIGINS.split(',').map(s=>s.trim())}).listen(Number(env.PORT||8080),'0.0.0.0');
}
