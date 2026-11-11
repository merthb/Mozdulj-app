from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
script=(root/'docs/ui-prototype-v06.js').read_text(encoding='utf-8')
pose_function=script[script.index('function drawPoses(){'):script.index('\nrender();',script.index('function drawPoses(){'))]
body='''<!doctype html><html lang="hu"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Mozdulj! – Generált gyakorlattár v0.6</title><style>
:root{font-family:system-ui,-apple-system,'Segoe UI',sans-serif;color:#2d2545;background:#f8f5fc}
*{box-sizing:border-box}body{margin:0}header{padding:38px 5vw;background:linear-gradient(115deg,#f1e8fd,#e9f7f5)}h1{font-size:32px;margin:4px 0}p{line-height:1.5}main{padding:28px 5vw 70px}.filters{display:flex;flex-wrap:wrap;gap:9px;margin:17px 0 26px}
input,select{font:inherit;padding:12px;border-radius:13px;background:white;border:1px solid #ded2eb;min-width:170px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(290px,1fr));gap:16px}.card{background:white;border:1px solid #e5daed;border-radius:19px;padding:14px;box-shadow:0 5px 20px #2517370b}.card h2{font-size:18px;margin:9px 0}.meta{color:#6b5c7c;font-size:13px}.tag{display:inline-block;background:#e9e0f9;border-radius:14px;padding:4px 9px;font-size:12px}.pose-canvas{width:100%;border-radius:13px;background:#f9f6fe}.steps{font-size:13px;line-height:1.6}details{margin-top:8px}.tiny{font-size:12px;color:#8a7c99}.notice{background:#fff1e2;padding:11px 14px;border-radius:12px;max-width:990px}
</style></head><body><header><div class="tiny">MOZDULJ! · OFFLINE GYAKORLATTÁR</div><h1>Generált testhelyzet-ábrák</h1><p>132 gyakorlathoz strukturált testpontok. Minden ábra helyben, azonos stílusban készül; külön képfájl nem kell az importhoz.</p><p class="notice"><b>Prototípus:</b> A pózadatok automatikus mozgássablonok, nem szakmailag validáltak. A testhelyzetek eltérhetnek a tényleges gyakorlattól. Kövesd a szöveges utasításokat, a képeket egyelőre ne tekintsd oktatási referenciának.</p></header><main><div class="filters"><input id="search" placeholder="Keresés…"/><select id="cat"><option value="">Minden mozgástípus</option></select><select id="phase"><option value="">Minden edzésfázis</option><option value="warmup">Bemelegítés</option><option value="main">Fő rész</option><option value="cooldown">Levezetés</option></select><select id="region"><option value="">Minden testtáj</option></select></div><p id="count"></p><section class="grid" id="grid"></section></main><script id="data" type="application/json">'''+json.dumps(cat,ensure_ascii=False,separators=(',',':')).replace('</','<\\/')+'''</script><script>
let activeCatalog=JSON.parse(document.querySelector('#data').textContent);let all=activeCatalog.exercises;
'''+pose_function+'''
const regionNames=[...new Set(all.flatMap(e=>e.regions))].sort(),categories=[...new Set(all.map(e=>e.category))].sort();
for(const x of categories)document.querySelector('#cat').add(new Option(x,x));
for(const x of regionNames)document.querySelector('#region').add(new Option(x,x));
const escape=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function render(){const q=document.querySelector('#search').value.toLocaleLowerCase('hu'),cat=document.querySelector('#cat').value,phase=document.querySelector('#phase').value,region=document.querySelector('#region').value;
const filtered=all.filter(e=>(!q||(e.name+' '+e.regions.join(' ')).toLocaleLowerCase('hu').includes(q))&&(!cat||e.category===cat)&&(!phase||e.phase===phase)&&(!region||e.regions.includes(region)));
document.querySelector('#count').textContent=filtered.length+' / '+all.length+' gyakorlat';
document.querySelector('#grid').innerHTML=filtered.map(e=>`<article class="card"><canvas class="pose-canvas" data-pose="${escape(e.id)}" width="600" height="255"></canvas><h2>${escape(e.name)}</h2><span class="tag">${escape(e.category)}</span> <span class="tag">${escape(e.phase)}</span><p class="meta">${escape(e.regions.join(', '))} · ${escape(e.equipment.join(', ')||'Eszköz nélkül')}</p><details><summary>Leírás és lépések</summary><p class="steps">${escape(e.startingPosition)}</p><ol class="steps">${e.steps.map(x=>`<li>${escape(x)}</li>`).join('')}</ol><p class="tiny">${escape(e.safetyNote)}</p></details><p class="tiny">Pózsablon: ${escape(e.pose.templateId)} · ${escape(e.pose.reviewStatus)}</p></article>`).join('');drawPoses()}
for(const id of ['search','cat','phase','region'])document.getElementById(id).addEventListener(id==='search'?'input':'change',render);
render();
</script></body></html>'''
p=root/'docs/katalogus_elonezet.html';p.write_text(body,encoding='utf-8');print('Wrote',p,p.stat().st_size)
