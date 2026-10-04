import {test} from 'node:test';import assert from 'node:assert/strict';import {createApp} from './server.mjs';
test('email gate, OTP attempt limit, single use, profile changes and session revocation',async t=>{
 let db={users:[],sessions:[]},sent=[],time=Date.now();const store={load:async()=>structuredClone(db),save:async x=>{db=structuredClone(x)}};
 const app=createApp({store,sendCode:async(email,code)=>sent.push({email,code}),secret:'test-secret-with-at-least-32-characters',now:()=>time,origins:['https://studio.example']});await new Promise(r=>app.listen(0,'127.0.0.1',r));t.after(()=>new Promise(r=>app.close(r)));const base='http://127.0.0.1:'+app.address().port;
 const request=async(path,body,token,method)=>{const r=await fetch(base+path,{method:method||(body?'POST':'GET'),headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{})});return {status:r.status,...await r.json()}};
 const credentials={username:'Knight',email:'knight@example.com',password:'VeryStrong123!'};
 const registration=await request('/auth/register',credentials);assert.equal(registration.verificationRequired,true);assert.equal(registration.token,undefined);assert.ok(!JSON.stringify(db).includes(credentials.password));assert.ok(!JSON.stringify(db).includes(sent[0].code));
 assert.equal((await request('/auth/login',credentials)).verificationRequired,true);assert.equal((await request('/me')).status,401);
 for(let i=0;i<5;i++)assert.equal((await request('/auth/verify',{email:credentials.email,code:'000000'})).status,400);
 assert.equal((await request('/auth/verify',{email:credentials.email,code:sent[0].code})).status,400);
 time+=61000;await request('/auth/resend',{email:credentials.email});const verified=await request('/auth/verify',sent.at(-1));assert.ok(verified.token);assert.equal((await request('/auth/verify',sent.at(-1))).status,400);assert.equal((await request('/me',null,verified.token)).user.username,'Knight');
 const login=await request('/auth/login',credentials);assert.ok(login.token);
 await request('/me',{...credentials,username:'King',newPassword:'Changed123!!'},verified.token,'PATCH');assert.equal((await request('/me',null,login.token)).status,401);assert.equal((await request('/me',null,verified.token)).user.username,'King');
 const change=await request('/me',{username:'King',email:'king@example.com',password:'Changed123!!'},verified.token,'PATCH');assert.equal(change.verificationRequired,true);assert.equal((await request('/me',null,verified.token)).user.email,'knight@example.com');const next=await request('/auth/verify',sent.at(-1));assert.equal(next.user.email,'king@example.com');assert.equal((await request('/me',null,verified.token)).status,401);
 await request('/auth/logout',{},next.token);assert.equal((await request('/me',null,next.token)).status,401);
});
