from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
html=root/'docs/UI_prototipus.html'
js=root/'docs/ui-prototype-v05.js'
s=html.read_text(encoding='utf-8')
s=s.replace('Mozdulj! – UI prototípus','Mozdulj! – v0.6 UI prototípus')
newcat=(root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8')
compact=json.dumps(json.loads(newcat),ensure_ascii=False,separators=(',',':')).replace('</','<\\/')
s=re.sub(r'(<script id="data" type="application/json">).*?(</script>)',lambda m:m.group(1)+compact+m.group(2),s,flags=re.S)
start=s.index('</script><script>')
script=s[start+len('</script><script>'):s.rfind('</script></body></html>')]
if 'function profileEdit()' in script:
    html.write_text(s,encoding='utf-8')
    print('Updated catalog inside existing v0.6 HTML:',len(s))
    raise SystemExit(0)
script=script.replace('const all=JSON.parse(document.querySelector(\'#data\').textContent).exercises;',
'''let activeCatalog=JSON.parse(document.querySelector('#data').textContent);
let all=activeCatalog.exercises;''')
script=script.replace('const img=(e,thumb=false)=>`<img ${thumb?\'class="thumb"\':\'class="illustration"\'} src="data:image/png;base64,${thumb?e.illustration.thumbnailBase64:e.illustration.imageBase64}" alt="Gyakorlatábra"/>`;',
'''const img=(e,thumb=false)=>`<canvas aria-label="Kétfázisú, géppel rajzolt szemléltető póz" class="pose-canvas ${thumb?'thumb':'illustration'}" data-pose="${e.id}" width="${thumb?420:640}" height="${thumb?150:270}"></canvas>`;''')
script=script.replace('<img class="small" src="data:image/png;base64,${e.illustration.thumbnailBase64}">','${img(e,true)}')
# profile should not render controls and save inside read-only panel
idx=script.index('function profile(){')
end=script.index('\nlet pendingSettings=null;',idx)
script=script[:idx]+'''function profile(){return `<h1>Profil</h1><p class="sub">A beállítások itt csak megtekinthetők. A szerkesztést külön, teljes képernyős nézetben lehet elvégezni.</p>${btn('✎ Profil szerkesztése','editprofile')}<div class="card"><h3>Idő és nehézség</h3><p class="mini"><b>Gyakorlatsor célhossza:</b> ${minutes} perc</p><p class="mini"><b>Maximális nehézség:</b> ${['Kezdő','Középhaladó','Haladó'][difficulty-1]}</p><p class="mini"><b>Átvezetés:</b> ${transitionSeconds} másodperc</p></div><div class="card"><h3>Személyre szabás</h3><p class="mini"><b>Fókusztesttáj:</b> ${focus}</p><p class="mini"><b>Elérhető eszközök:</b> ${[...equipment].join(', ')||'Nincs'}</p></div><div class="card"><h3>Gyakorlattár</h3><p class="mini">Helyben tárolt v${activeCatalog.catalogVersion} · ${all.length} gyakorlat, képfájlok nélkül.</p>${btn('↓ Jelenlegi JSON exportálása','exportcatalog','outline')}${btn('↑ JSON importálása','importcatalog','outline')}<input type="file" id="catalogfile" accept=".json,application/json" hidden/></div>`}
let profileDraft=null;
function profileEdit(){const d=profileDraft||{minutes,difficulty,transitionSeconds};return `<div class="modalpage"><h1>Profil szerkesztése</h1><p class="sub">A beállításokat csak a Mentés gombbal alkalmazzuk.</p><div class="card"><h3>Idő és nehézség</h3><div class="field"><label>Gyakorlatsor célhossza</label><select id="minutes">${[5,10,15,20,30].map(n=>`<option value="${n}" ${n===d.minutes?'selected':''}>${n} perc</option>`).join('')}</select></div><div class="field"><label>Legmagasabb megengedett nehézség</label><select id="difficulty">${['Kezdő','Középhaladó','Haladó'].map((a,i)=>`<option value="${i+1}" ${i+1===d.difficulty?'selected':''}>${a}</option>`).join('')}</select></div><div class="field"><label>Átvezetés két feladat között</label><select id="transition">${[5,6,7,8,9,10].map(n=>`<option value="${n}" ${n===d.transitionSeconds?'selected':''}>${n} mp</option>`).join('')}</select></div></div><div class="modal-actions">${btn('Mégse','cancelprofile','outline')}${btn('✓ Beállítások mentése','saveprofile')}</div></div>`}
''' +script[end:]
script=script.replace('const pages={home,preview,replacement,mandatory,detail,player,done,rewards,catalog,profile,settingsDecision}',
                            'const pages={home,preview,replacement,mandatory,detail,player,done,rewards,catalog,profile,profileEdit,settingsDecision}')
script=script.replace("(screen==='player'||screen==='done')?'none':'flex'","(['player','done','profileEdit','settingsDecision'].includes(screen))?'none':'flex'")
script=script.replace(".slice(0,35).map(mini).join('')",".slice(0,35).map(mini).join('');drawPoses()")
# render draws after DOM refreshed
script=script.replace("$('#screen').scrollTop=0;", "$('#screen').scrollTop=0;drawPoses();")
# change profile buttons handlers
old="else if(a==='saveprofile'){const next={minutes:+$('#minutes').value,difficulty:+$('#difficulty').value,transition:+$('#transition').value};const changed=next.minutes!==minutes||next.difficulty!==difficulty||next.transition!==transitionSeconds;pendingSettings=next;if(changed&&started.size<3)go('settingsDecision');else{Object.assign(window,{});minutes=next.minutes;difficulty=next.difficulty;transitionSeconds=next.transition;go('home')}}"
new="""else if(a==='editprofile'){profileDraft={minutes,difficulty,transitionSeconds};go('profileEdit')}
 else if(a==='cancelprofile'){profileDraft=null;go('profile')}
 else if(a==='saveprofile'){const next={minutes:+$('#minutes').value,difficulty:+$('#difficulty').value,transition:+$('#transition').value};const changed=next.minutes!==minutes||next.difficulty!==difficulty||next.transition!==transitionSeconds;pendingSettings=next;if(changed&&started.size<3)go('settingsDecision');else{minutes=next.minutes;difficulty=next.difficulty;transitionSeconds=next.transition;profileDraft=null;go('profile')}}
 else if(a==='exportcatalog'){const url=URL.createObjectURL(new Blob([JSON.stringify(activeCatalog,null,2)],{type:'application/json'}));const el=document.createElement('a');el.href=url;el.download='mozdulj-gyakorlatok-v'+activeCatalog.catalogVersion+'.json';el.click();setTimeout(()=>URL.revokeObjectURL(url),3000)}
 else if(a==='importcatalog'){$('#catalogfile')?.click()}"""
assert old in script
script=script.replace(old,new)
script=script.replace("go('home')}\n else go(a)","profileDraft=null;go('profile')}\n else go(a)")
# render pose function; 2D landmarks with z-driven gradient shading; no image assets
renderer='''
function drawPoses(){document.querySelectorAll('canvas[data-pose]').forEach(c=>{
 const ex=all.find(x=>x.id===c.dataset.pose);if(!ex?.pose)return;
 const ctx=c.getContext('2d');if(!ctx)return;
 const frames=ex.pose.frames.slice(0,3),w=c.width,h=c.height;
 ctx.fillStyle='#f9f6fe';ctx.fillRect(0,0,w,h);
 const gap=9,pw=(w-gap*(frames.length+1))/frames.length,sc=Math.min((pw-14)/104,(h-16)/104);
 frames.forEach((f,i)=>{
  const left=gap+i*(pw+gap),ox=left+(pw-100*sc)/2,oy=(h-100*sc)/2;
  const at=j=>{let p=f.joints[j];return [ox+(p.x+p.z*.20)*sc,oy+(p.y-p.z*.07)*sc]};
  ctx.fillStyle='#f0eafb';ctx.beginPath();ctx.roundRect(left,6,pw,h-12,14);ctx.fill();
  ctx.fillStyle='#d8cde4';ctx.beginPath();ctx.ellipse(ox+50*sc,oy+97*sc,36*sc,2.8*sc,0,0,Math.PI*2);ctx.fill();
  function seg(a,b,r,base,lit){let p=at(a),q=at(b);ctx.lineCap='round';
   ctx.strokeStyle='#5f5165';ctx.lineWidth=r*sc;ctx.beginPath();ctx.moveTo(...p);ctx.lineTo(...q);ctx.stroke();
   ctx.strokeStyle=base;ctx.lineWidth=(r-1.6)*sc;ctx.beginPath();ctx.moveTo(p[0]-.4*sc,p[1]-.4*sc);ctx.lineTo(q[0]-.4*sc,q[1]-.4*sc);ctx.stroke();
   ctx.strokeStyle=lit;ctx.lineWidth=Math.max(1,(r-5)*sc);ctx.beginPath();ctx.moveTo(p[0]-1.1*sc,p[1]-1.1*sc);ctx.lineTo(q[0]-1.1*sc,q[1]-1.1*sc);ctx.stroke()}
  seg('rightHip','rightKnee',12,'#37969b','#79bfc0');seg('rightKnee','rightAnkle',10,'#37969b','#79bfc0');seg('rightAnkle','rightToe',5,'#513b4e','#513b4e');
  seg('rightShoulder','rightElbow',9,'#e4a388','#f7c8aa');seg('rightElbow','rightWrist',7,'#e4a388','#f7c8aa');
  const poly=['leftShoulder','rightShoulder','rightHip','leftHip'].map(at);
  ctx.fillStyle='#794e94';ctx.beginPath();poly.forEach((p,j)=>j?ctx.lineTo(...p):ctx.moveTo(...p));ctx.closePath();ctx.fill();
  ctx.strokeStyle='#ad86bf';ctx.lineWidth=2.8*sc;ctx.beginPath();ctx.moveTo(...poly[0]);ctx.lineTo(...poly[3]);ctx.stroke();
  seg('leftHip','leftKnee',12,'#37969b','#79bfc0');seg('leftKnee','leftAnkle',10,'#37969b','#79bfc0');seg('leftAnkle','leftToe',5,'#513b4e','#513b4e');
  seg('leftShoulder','leftElbow',9,'#e4a388','#f7c8aa');seg('leftElbow','leftWrist',7,'#e4a388','#f7c8aa');
  const n=at('neck'),head=at('head');ctx.strokeStyle='#e5a98c';ctx.lineWidth=7*sc;ctx.beginPath();ctx.moveTo(...n);ctx.lineTo(head[0],head[1]+3*sc);ctx.stroke();
  ctx.fillStyle='#4b3343';ctx.beginPath();ctx.ellipse(head[0],head[1],8*sc,9*sc,0,0,Math.PI*2);ctx.fill();
  ctx.fillStyle='#efb998';ctx.beginPath();ctx.ellipse(head[0],head[1]+1*sc,6*sc,7*sc,0,0,Math.PI*2);ctx.fill();
  ctx.fillStyle='#4b3343';ctx.beginPath();ctx.arc(head[0]+6*sc,head[1]-6*sc,4*sc,0,Math.PI*2);ctx.fill();
  ctx.fillStyle='#302840';ctx.beginPath();ctx.arc(head[0]+2.2*sc,head[1],.85*sc,0,Math.PI*2);ctx.fill();
 });
});}
'''
# add code before initial render() invocation
assert script.endswith('render();\n')
script=script[:-len('render();\n')]+renderer+'\nrender();\n'
# include catalog import listener supports checked properties and renderer drawing
script=script.replace("document.addEventListener('change',e=>{if(e.target.id==='pinphase'){pinFilter=e.target.value;go('mandatory')}});",
'''document.addEventListener('change',async e=>{
 if(e.target.id==='pinphase'){pinFilter=e.target.value;go('mandatory')}
 if(e.target.id==='catalogfile' && e.target.files?.[0]){
  try{const next=JSON.parse(await e.target.files[0].text());
   if(next.schemaVersion!==3||!Array.isArray(next.exercises)||!next.exercises.length||next.exercises.some(x=>!x.pose?.frames?.length))throw Error('Csak v3 pózadatokat tartalmazó JSON importálható.');
   activeCatalog=next;all=next.exercises;
   alert('Katalógus betöltve a böngészős bemutatóba. Az alkalmazásba importálva belső tárhelyre kerül.');go('profile');
  }catch(err){alert('Importálási hiba: '+err.message)}
 }
});''')
s=s[:start+len('</script><script>')]+script+'</script></body></html>'
s=s.replace('min-height:790px','min-height:790px')
s=s.replace('</style>', '''
.pose-canvas{display:block;width:100%;height:auto;max-height:240px;border-radius:12px;background:#f9f6fe}
.pose-canvas.thumb{width:100%;height:94px;object-fit:contain}
.small.pose-canvas{width:100px;height:55px}
.modalpage{min-height:670px;display:flex;flex-direction:column}
.modal-actions{display:flex;gap:8px;position:sticky;bottom:0;background:#fbf9ff;padding:10px 0 16px;margin-top:auto}
.modal-actions>.button{flex:1}
</style>''')
html.write_text(s,encoding='utf-8')
(root/'docs/ui-prototype-v06.js').write_text(script,encoding='utf-8')
print('web preview:',len(s),'JS',len(script))
