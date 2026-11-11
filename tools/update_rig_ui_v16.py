from pathlib import Path
root=Path(__file__).resolve().parents[1]
for d in [root/'Mozdulj.Maui/Resources/Raw/rig3d',root/'docs/rig3d_demo']:
 p=d/'index.html';s=p.read_text(encoding='utf-8')
 s=s.replace('</style></head>', '</style><link rel="stylesheet" href="studio-v16.css"></head>')
 s=s.replace('<div class="buttons"><button class="active" id="frame0">Kiindulás</button><button id="frame1">Mozdulat</button></div>', '<div class="buttons" id="frameButtons" aria-label="Mozgásfázisok"></div>')
 s=s.replace('<div class="line"><label for="torso">', '<div class="line"><label for="torso">')
 # add Head roll and shoulder lift in editor
 s=s.replace('<label for="torso">Törzs előre/hátra dőlése</label>', '<label for="roll">Fej oldalra billentése</label><input id="roll" type="range" min="-45" max="45" value="0"><label for="shoulderLift">Vállak felhúzása</label><input id="shoulderLift" type="range" min="-4" max="8" value="0"><label for="torso">Törzs előre/hátra dőlése</label>')
 s=s.replace('<span class="pill">Offline WebGL</span>', '<span class="pill">3D</span>')
 s=s.replace('A testarányok és a szegmenshosszak rögzítettek.', 'Rögzített testarányok · 3D nézet')
 p.write_text(s,encoding='utf-8')
 css='''/* Mozdulj! – consistent 3D toolbar and editor surfaces, v0.16 */
:root{--purple:#684bbf;--purple-dark:#4d378a;--teal:#4d998e;--surface:#fff;--bg:#f8f6fc;--outline:#e5dff1}
html{color-scheme:light}body{background:var(--bg);color:#29233c;letter-spacing:-.006em}
.shell{border:1px solid var(--outline);border-radius:20px;box-shadow:0 10px 35px rgba(43,31,93,.055)}
.header{background:linear-gradient(120deg,#fff 30%,#f5f0fc);padding:15px 18px}.header strong{font-size:17px;letter-spacing:-.02em}.header .hint{margin-top:3px}.header h1{font-size:18px;letter-spacing:-.02em}
.pill{background:#eee7fb;color:var(--purple-dark);font-size:11px;font-weight:750;padding:7px 12px}
.view-controls{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:9px;padding:14px;background:#fff}
.view-controls button,.panel button,.buttons button{appearance:none;min-height:43px;border-radius:13px;border:1px solid #ded6ef;background:#f7f4fd;color:#54408a;font:600 13px/1.3 system-ui,-apple-system,Segoe UI,sans-serif;padding:9px 11px;box-shadow:none;cursor:pointer;transition:background .12s,border-color .12s,transform .12s}
.view-controls button.active,.panel button.active,.buttons button.active{background:var(--purple)!important;border-color:var(--purple)!important;color:#fff!important}
.view-controls button:active,.panel button:active{transform:scale(.98)}
.view-controls button:focus-visible,.panel button:focus-visible{outline:3px solid #bca5f4;outline-offset:2px}
.view-controls button:disabled,.panel button:disabled{opacity:.45;cursor:not-allowed}
.panel{padding:17px;gap:16px}.line label{font-size:13px;font-weight:700;color:#403358}
.line select{min-height:45px;border:1px solid #ded6ef;background:#faf8fe;color:#36274d;border-radius:13px;padding:10px 12px}
.line input[type=range]{accent-color:var(--purple);cursor:pointer;min-height:24px}
.buttons{display:flex;gap:8px}.buttons button{flex:1;min-width:0}
.panel button.primary,.view-controls button.primary{background:var(--purple)!important;color:#fff!important;border-color:var(--purple)!important}
.note{background:#f4f7f6;border-color:#d7e6e0;color:#486259;border-radius:14px;padding:12px;line-height:1.5}
.status{background:#f5f1fb;color:#634f82;border-radius:10px;padding:8px 11px;line-height:1.35}
.stage{background:radial-gradient(ellipse at 50% 20%,#fff 10%,#f2ecfa 100%)}
.floating{border-radius:12px;border-color:#e7def2;box-shadow:0 2px 15px #2d23420d}
#frameButtons{display:flex;flex-wrap:wrap}#frameButtons button{flex:1 1 100px}
@media(max-width:760px){main{padding:8px}.shell{border-radius:18px}.stage{height:52vh;min-height:310px}.view-controls{grid-template-columns:repeat(2,minmax(0,1fr));padding:12px;gap:8px}.view-controls button{min-height:43px}.panel{padding:14px}.floating{font-size:11px}.float-l{top:9px;left:9px;max-width:60%;font-size:10px}.float-b{bottom:9px;left:9px;right:9px}.header{padding:12px 14px}}
'''
 (d/'studio-v16.css').write_text(css,encoding='utf-8')

 p=d/'studio.js';s=p.read_text(encoding='utf-8')
 s=s.replace('function show(){if(!studio)return;', '''function renderFrameButtons(){
 const container=$('frameButtons');if(!container||!pose)return;
 container.replaceChildren();
 for(let i=0;i<pose.frames.length;i++){
  const btn=document.createElement('button');btn.type='button';
  btn.textContent=pose.frames[i].name||((i+1)+'. fázis');
  btn.classList.toggle('active',frameIndex===i);
  btn.setAttribute('aria-pressed',String(frameIndex===i));
  btn.addEventListener('click',()=>switchFrame(i));container.appendChild(btn);
 }
}
function show(){if(!studio)return;''')
 s=s.replace("for(let i=0;i<2;i++)$('frame'+i).classList.toggle('active',frameIndex===i);", "renderFrameButtons();")
 s=s.replace("$('torso').value=Math.round(deg(f.euler[0]));", "$('torso').value=Math.round(deg(f.euler[0]));$('roll').value=Math.round(deg(f.headRoll||0));$('shoulderLift').value=Math.round((f.shoulderLift||0)*100);")
 s=s.replace('function switchFrame(i){frameIndex=i;sync();}', 'function switchFrame(i){frameIndex=Math.max(0,Math.min(i,pose.frames.length-1));sync();}')
 old='''  const prev=document.createElement('button');prev.textContent='◀ Kiindulás';prev.onclick=()=>switchFrame(0);
  const next=document.createElement('button');next.textContent='Mozdulat ▶';next.onclick=()=>switchFrame(Math.min(1,pose.frames.length-1));
  actions.append(prev,next);show();'''
 new='''  pose.frames.forEach((frame,i)=>{
    const btn=document.createElement('button');btn.type='button';
    btn.textContent=frame.name||((i+1)+'. fázis');
    btn.classList.toggle('active',frameIndex===i);
    btn.onclick=()=>{switchFrame(i);actions.querySelectorAll('.phase-view').forEach((x,j)=>x.classList.toggle('active',i===j));};
    btn.classList.add('phase-view');actions.appendChild(btn);
  });show();'''
 assert old in s, 'phase viewer missing'
 s=s.replace(old,new)
 s=s.replace("$('preset').addEventListener('change',e=>setPreset(e.target.value));for(let i=0;i<2;i++)$('frame'+i).addEventListener('click',()=>switchFrame(i));", "$('preset').addEventListener('change',e=>setPreset(e.target.value));")
 s=s.replace("$('torso').oninput=e=>{current().euler[0]=rad(+e.target.value);show();};", "$('torso').oninput=e=>{current().euler[0]=rad(+e.target.value);show();};$('roll').oninput=e=>{current().headRoll=rad(+e.target.value);show();};$('shoulderLift').oninput=e=>{current().shoulderLift=+e.target.value/100;show();};")
 # camera gestures remain enabled; no other controls in read only.
 s=s.replace("if(!studio)return;let f=current();const j=studio.draw(f,{edit:true});", "if(!studio)return;let f=current();const j=studio.draw(f,{edit:!document.body.classList.contains('view-only')});")
 s=s.replace("window.setReadOnlyMode=()=>{", "window.setReadOnlyMode=()=>{document.body.classList.add('view-only');")
 p.write_text(s,encoding='utf-8')
print('Updated WebGL UI')
