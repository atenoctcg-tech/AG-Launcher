const $=s=>document.querySelector(s),$$=s=>[...document.querySelectorAll(s)];
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const safe=u=>{if(typeof u!=='string'||!u.trim())return '';try{const x=new URL(u,location.href);return x.protocol==='https:'||x.origin===location.origin?x.href:''}catch{return ''}};
const raw='https://raw.githubusercontent.com/atenoctcg-tech/AG-Launcher/main/launcher-manifest.json';
const liveManifestUrl=()=>raw+(raw.includes('?')?'&':'?')+'_ag='+Date.now();
let catalog,category='All',versions=new Map();
const nicknameKey='ag-local-nickname',avatarKey='ag-local-avatar';
async function json(url,options={}){const r=await fetch(url,{cache:'no-store',...options,signal:AbortSignal.timeout(15000)});const text=await r.text();let d={};try{d=text?JSON.parse(text):{}}catch{}if(!r.ok)throw Error(d.error||d.message||`Request failed (${r.status})`);return d}
function modal(html){$('#modalContent').innerHTML=html;if(!$('#modal').open)$('#modal').showModal()}
function toast(message){$('#toast').textContent=message;$('#toast').hidden=false;clearTimeout(toast.t);toast.t=setTimeout(()=>$('#toast').hidden=true,4200)}
const image=(url,alt='')=>`<img src="${esc(safe(url)||'media/castle-survival.png')}" alt="${esc(alt)}" loading="lazy">`;
const heroMedia=g=>{const u=safe(g?.animatedBannerUrl||'');if(u&&/\.mp4(?:$|[?#])/i.test(u))return `<video class="hero-media" src="${esc(u)}" poster="${esc(safe(g.bannerUrl)||'media/castle-survival.png')}" autoplay muted loop playsinline preload="metadata"></video>`;if(u&&/\.gif(?:$|[?#])/i.test(u))return image(u,g.name);return image(g?.bannerUrl,g?.name)};
const download=()=>safe(catalog?.launcher?.packageUrl)||'https://github.com/atenoctcg-tech/AG-Launcher/releases/download/v0.4.5/AGLauncher-v0.4.5-win-x64.zip';

function applyDownloadLinks(){
 const url=download();
 $('.download,[data-launcher-download]').forEach(a=>{
  a.href=url;
  a.removeAttribute('target');
  a.setAttribute('download','AGLauncher-Windows-x64.zip');
 });
}

function downloadLauncher(e){
 e.preventDefault();
 const url=download();
 if(!url){toast('Launcher download is temporarily unavailable.');return}
 const a=document.createElement('a');
 a.href=url;
 a.download='AGLauncher-Windows-x64.zip';
 a.rel='noopener';
 a.style.display='none';
 document.body.appendChild(a);
 a.click();
 a.remove();
 toast('AG Launcher download started.');
}
function nickname(){return (localStorage.getItem(nicknameKey)||'').trim()}
function refreshNickname(){
 const n=nickname(),avatar=localStorage.getItem(avatarKey)||'';
 $('#profileName').textContent=n||'Guest';
 const hint=$('#profileButton small');if(hint)hint.textContent=(n||avatar)?'Edit profile ↗':'Set profile ↗';
 const img=$('#profileAvatar'),initials=$('#profileInitials');
 if(avatar){img.src=avatar;img.hidden=false;initials.hidden=true}else{img.removeAttribute('src');img.hidden=true;initials.hidden=false}
}
async function avatarData(file){
 if(!file)return '';
 if(file.size>6*1024*1024)throw Error('Profile photo must be smaller than 6 MB.');
 if(!/^image\/(png|jpeg|webp)$/i.test(file.type))throw Error('Use PNG, JPG or WEBP.');
 const url=URL.createObjectURL(file);
 try{
  const img=new Image();await new Promise((ok,bad)=>{img.onload=ok;img.onerror=bad;img.src=url});
  const size=256,canvas=document.createElement('canvas');canvas.width=size;canvas.height=size;
  const ctx=canvas.getContext('2d'),scale=Math.max(size/img.width,size/img.height),w=img.width*scale,h=img.height*scale;
  ctx.drawImage(img,(size-w)/2,(size-h)/2,w,h);
  return canvas.toDataURL('image/jpeg',.86);
 }finally{URL.revokeObjectURL(url)}
}
function openNickname(){
 const current=nickname(),currentAvatar=localStorage.getItem(avatarKey)||'';
 modal(`<p class="eyebrow">LOCAL PROFILE</p><h2>Your player profile.</h2><p>No account, email or login is required. Nickname and photo stay only in this browser.</p><form id="nicknameForm"><div class="web-profile-preview"><span id="modalInitials">AG</span><img id="modalAvatar" alt="" ${currentAvatar?'src="'+esc(currentAvatar)+'"':'hidden'}></div><label class="field">Nickname<input name="nickname" maxlength="24" minlength="2" value="${esc(current)}" placeholder="Your nickname"></label><label class="field">Profile photo<input name="photo" type="file" accept="image/png,image/jpeg,image/webp"></label><p class="form-error" id="nicknameError"></p><div class="auth-actions"><button type="button" class="secondary" id="clearAvatar">Remove photo</button><button class="primary">Save profile</button></div></form>`);
 let removeAvatar=false;
 const photo=$('#nicknameForm [name=photo]');
 photo.onchange=async()=>{try{const data=await avatarData(photo.files[0]);if(data){$('#modalAvatar').src=data;$('#modalAvatar').hidden=false;$('#modalInitials').hidden=true}}catch(x){$('#nicknameError').textContent=x.message}};
 $('#clearAvatar').onclick=()=>{removeAvatar=true;photo.value='';$('#modalAvatar').removeAttribute('src');$('#modalAvatar').hidden=true;$('#modalInitials').hidden=false};
 $('#nicknameForm').onsubmit=async e=>{e.preventDefault();const n=String(new FormData(e.target).get('nickname')||'').trim();if(n&&n.length<2){$('#nicknameError').textContent='Nickname must contain at least 2 characters.';return}try{let data='';if(photo.files[0])data=await avatarData(photo.files[0]);localStorage.setItem(nicknameKey,n);if(removeAvatar)localStorage.removeItem(avatarKey);else if(data)localStorage.setItem(avatarKey,data);refreshNickname();$('#modal').close();toast('Profile saved.')}catch(x){$('#nicknameError').textContent=x.message}}
}
function render(){
 const q=$('#search').value.toLowerCase();const games=(catalog.games||[]).filter(g=>g.visible!==false).sort((a,b)=>Number(b.featured)-Number(a.featured)||(a.sortOrder||0)-(b.sortOrder||0));
 const news=(catalog.news||[]).filter(n=>n.visible!==false).sort((a,b)=>Number(b.pinned)-Number(a.pinned)||String(b.date).localeCompare(String(a.date)));
 const g=games[0];$('#heroGrid').innerHTML=g?`<article class="hero-card">${heroMedia(g)}<div class="hero-copy"><span class="pill">FEATURED · ${esc(g.pricing||'FREE').toUpperCase()}</span><h2>${esc(g.name)}</h2><p>${esc(g.description)}</p><a class="primary" data-launcher-download href="${esc(download())}">Download launcher <span>↓</span></a></div></article><div class="hero-side">${news.slice(0,2).map(n=>`<article class="mini-news">${image(n.imageUrl)}<div><small>STUDIO NEWS · ${esc(n.date)}</small><h3>${esc(n.title)}</h3><a href="#" data-news="${esc(n.id)}">Read story ↗</a></div></article>`).join('')}</div>`:'<div class="empty">New worlds are on their way.</div>';
 $('#gamesGrid').innerHTML=games.filter(g=>(g.name+' '+g.description).toLowerCase().includes(q)).map(g=>`<article class="game-card"><div class="game-media">${image(g.bannerUrl,g.name)}<span class="pill">${esc(g.status)}</span></div><div class="game-info"><div class="game-top"><h3>${esc(g.name)}</h3><small>${esc(versions.get(g.id)||'')}</small></div><p>${esc(g.description)}</p><div class="game-bottom"><span>${esc(g.pricing||'FREE').toUpperCase()}</span><a class="secondary" data-launcher-download href="${esc(download())}">Download launcher ↓</a></div></div></article>`).join('')||'<p class="empty">No matching games.</p>';
 $('#libraryGrid').innerHTML=games.filter(g=>g.name.toLowerCase().includes(q)).map(g=>`<a class="library-card" data-launcher-download href="${esc(download())}">${image(g.libraryImageUrl||g.bannerUrl,g.name)}<h3>${esc(g.name)}</h3><span class="subtle">${esc(versions.get(g.id)||g.status)} · ${esc(g.pricing||'Free')}</span></a>`).join('')||'<p class="empty">No matching games.</p>';
 $('#newsGrid').innerHTML=news.filter(n=>(n.title+' '+n.summary).toLowerCase().includes(q)).map(n=>`<article class="news-card">${image(n.imageUrl)}<div><small>${esc(n.date)} · ${n.pinned?'FEATURED':'NEWS'}</small><h3>${esc(n.title)}</h3><p>${esc(n.summary)}</p><a href="#" data-news="${esc(n.id)}">Read story ↗</a></div></article>`).join('');
 $('#workshopGrid').innerHTML=(catalog.workshop||[]).filter(w=>w.visible&&(category==='All'||w.category===category)&&(w.name+' '+w.description).toLowerCase().includes(q)).map(w=>`<article class="game-card"><div class="game-media">${image(w.imageUrl,w.name)}<span class="pill">${esc(w.category)}</span></div><div class="game-info"><h3>${esc(w.name)}</h3><p>${esc(w.description)}</p>${w.pricing==='free'&&safe(w.downloadUrl)?`<a class="secondary" href="${esc(safe(w.downloadUrl))}" target="_blank" rel="noopener">Download free ↗</a>`:'<span class="subtle">Coming soon</span>'}</div></article>`).join('')||'<div class="empty"><span>◇</span><h2>A space for your imagination.</h2><p>Mods, 3D models and tools will appear here when the studio publishes them.</p></div>';
 $('#notificationCount').textContent=news.length;
}
function route(){const page=['home','library','workshop'].includes(location.hash.slice(1))?location.hash.slice(1):'home';$$('.page').forEach(e=>e.hidden=e.id!==page);$$('[data-page]').forEach(e=>e.classList.toggle('active',e.dataset.page===page))}
$('#modal .close-modal').onclick=()=>$('#modal').close();$('#modal').onclick=e=>{if(e.target===$('#modal'))$('#modal').close()};$('#profileButton').onclick=openNickname;$('#search').oninput=render;window.onhashchange=route;
$$('[data-category]').forEach(b=>b.onclick=()=>{category=b.dataset.category;$$('[data-category]').forEach(x=>x.classList.toggle('selected',x===b));render()});
document.addEventListener('click',e=>{const dl=e.target.closest('.download,[data-launcher-download]');if(dl){downloadLauncher({preventDefault:()=>e.preventDefault(),currentTarget:dl});return}const n=e.target.closest('[data-news]');if(n){e.preventDefault();const item=(catalog.news||[]).find(x=>x.id===n.dataset.news);if(item)modal(`${image(item.imageUrl)}<p class="eyebrow">${esc(item.date)}</p><h2>${esc(item.title)}</h2><p>${esc(item.summary)}</p>${safe(item.linkUrl)?`<a class="secondary" href="${esc(safe(item.linkUrl))}" target="_blank" rel="noopener">Full story ↗</a>`:''}`)}});
document.addEventListener('keydown',e=>{if(e.key==='/'&&!/INPUT|TEXTAREA/.test(document.activeElement.tagName)){e.preventDefault();$('#search').focus()}});
$('#notifications').onclick=()=>modal('<h2>Studio updates</h2>'+(catalog.news||[]).filter(n=>n.visible!==false).map(n=>`<p><b>${esc(n.title)}</b><br>${esc(n.date)} · ${esc(n.summary)}</p>`).join(''));
function applyTheme(){
 for(const [key,variable] of Object.entries({background:'bg',panel:'panel',card:'card',accent:'accent',text:'text',mutedText:'muted'}))
  if(/^#[0-9a-f]{6}$/i.test(catalog?.theme?.[key]))
   document.documentElement.style.setProperty('--'+variable,catalog.theme[key]);
 document.body.classList.toggle('motion-off',catalog?.theme?.motion===false);
}

function applySocials(){
 $$('[data-social]').forEach(a=>{
  const key=a.dataset.social;
  const url=safe(catalog?.socials?.[key]||catalog?.socials?.[key==='youtube'?'youTube':key]);
  if(url){a.href=url;a.target='_blank';a.rel='noopener'}
  else a.onclick=e=>{e.preventDefault();toast('The studio has not added this community link yet.')}
 });
}

function paintCatalog(){
 applyTheme();
 applySocials();
 const heading=document.querySelector('#home h1');
 if(heading)heading.textContent=catalog?.presentation?.defaultHeroTitle||'Your next adventure.';
 refreshNickname();
 render();
 applyDownloadLinks();
 route();
}

async function load(){
 let localError=null;
 try{
  // Same-origin catalog is the reliable website source and is published together with launcher-manifest.json.
  catalog=await json('catalog.json?_ag='+Date.now());
 }catch(err){
  localError=err;
  try{catalog=await json(liveManifestUrl())}
  catch(liveErr){throw Error('Catalog could not be loaded: '+(localError?.message||liveErr.message))}
 }

 paintCatalog();
 $('#status').hidden=true;

 // Refresh from the live launcher manifest when available, but never blank the website if it fails.
 try{
  const live=await json(liveManifestUrl());
  if(live&&JSON.stringify(live)!==JSON.stringify(catalog)){
   catalog=live;
   paintCatalog();
  }
 }catch{}

 await Promise.allSettled((catalog.games||[]).filter(g=>g.visible!==false).map(async g=>{
  let gm=g;
  if(g.manifestUrl)gm=await json(g.manifestUrl+(g.manifestUrl.includes('?')?'&':'?')+'_ag='+Date.now());
  if(gm.releaseRepo){
   const r=await json('https://api.github.com/repos/'+gm.releaseRepo+'/releases/latest');
   if((r.assets||[]).some(a=>/\.zip$/i.test(a.name)&&!/source/i.test(a.name)))versions.set(g.id,r.tag_name);
  }else if(gm.version)versions.set(g.id,'v'+gm.version);
 }));
 render();
 applyDownloadLinks();
}

async function syncLiveCatalog(){
 try{
  const next=await json(liveManifestUrl());
  if(JSON.stringify(next)!==JSON.stringify(catalog)){
   catalog=next;
   paintCatalog();
   toast('Website content refreshed.');
  }
 }catch{}
}
load().then(()=>setInterval(syncLiveCatalog,30000)).catch(err=>{$('#status').hidden=true;console.error('AG website catalog error',err)});
