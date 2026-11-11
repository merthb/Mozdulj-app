#!/usr/bin/env python3
from pathlib import Path
import json,html
root=Path(__file__).resolve().parents[1]
a=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf8'))
rows=[{'id':e['id'],'name':e['name'],'category':e['category'],'regions':e['regions'],'template':e['pose']['templateId'],
       'frames':e['pose']['frames'],'state':'Egyedileg javított' if e['pose']['templateId'].startswith('egyedi-') else 'További ellenőrzésre vár'} for e in a['exercises']]
page='''<!DOCTYPE html><html lang="hu"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Mozdulj! – 132 ábra ellenőrzése</title><style>
:root{font-family:system-ui,sans-serif;background:#f8f6fc;color:#24203e}
body{margin:0 auto;max-width:1160px;padding:20px} h1{color:#5a3c9a}
.controls{position:sticky;top:0;background:#f8f6fcee;padding:14px 0;z-index:2;display:flex;flex-wrap:wrap;gap:8px}
input,select{border:1px solid #c9b9df;border-radius:9px;padding:10px;font:inherit;min-width:160px}
#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(265px,1fr));gap:12px}
article{background:white;border:1px solid #e8e1f0;border-radius:14px;padding:12px;box-shadow:0 3px 9px #29153508}
h3{font-size:15px;margin:0 0 5px}.meta{font-size:12px;color:#777}.tag{font-size:11px;color:#523c85;background:#eee6fc;padding:4px 6px;border-radius:6px;display:inline-block;margin:4px 0}
svg{width:100%;height:auto;background:#f7f3fd;border-radius:9px}
.notice{background:#eee9fa;border-radius:10px;padding:10px;font-size:13px;line-height:1.5}
</style></head><body><h1>Mozdulj! · Ábraellenőrzés</h1>
<p class="notice">A testpontokból generált képek szemléltető ábrák, nem szakember által jóváhagyott mozgásminták. A cél az egyértelműen hibás mozdulatok kiszűrése; a szakmai ellenőrzés még szükséges.</p>
<div class="controls"><input id="q" placeholder="Gyakorlat keresése" aria-label="Gyakorlat keresése"><select id="state"><option value="all">Összes állapot</option><option value="fixed">Egyedileg javított</option><option value="unchecked">Ellenőrzendő</option></select><span id="count"></span></div><section id="grid"></section><p><button id="more" style="padding:12px;border:0;border-radius:10px;background:#6546ae;color:white;cursor:pointer">További ábrák betöltése</button></p>
<script>const ITEMS=__DATA__;
const links=[['head','neck'],['neck','leftShoulder'],['neck','rightShoulder'],['leftShoulder','leftElbow'],['leftElbow','leftWrist'],['rightShoulder','rightElbow'],['rightElbow','rightWrist'],['leftHip','leftKnee'],['leftKnee','leftAnkle'],['leftAnkle','leftToe'],['rightHip','rightKnee'],['rightKnee','rightAnkle'],['rightAnkle','rightToe']];
function fig(f,offset){const j=f.joints, x=n=>j[n].x*1.27+offset,y=n=>j[n].y*1.27+4;
let s=`<rect x="${offset}" y="1" width="130" height="136" rx="10" fill="#f1eafb"/>`;
for(let [a,b] of links){const isLeg=a.includes('Hip')||a.includes('Knee')||a.includes('Ankle');s+=`<line x1="${x(a)}" y1="${y(a)}" x2="${x(b)}" y2="${y(b)}" stroke="${isLeg?'#369c9c':'#d4a48d'}" stroke-width="${isLeg?8:6}" stroke-linecap="round"/>`}
for(let [a,b] of [['leftShoulder','leftHip'],['rightShoulder','rightHip']])s+=`<line x1="${x(a)}" y1="${y(a)}" x2="${x(b)}" y2="${y(b)}" stroke="#7e519e" stroke-width="10" stroke-linecap="round"/>`;
s+=`<line x1="${x('leftShoulder')}" y1="${y('leftShoulder')}" x2="${x('rightShoulder')}" y2="${y('rightShoulder')}" stroke="#7e519e" stroke-width="9"/>`;
s+=`<circle cx="${x('head')}" cy="${y('head')}" r="8" fill="#d9a186" stroke="#523443" stroke-width="2"/>`;
return s}
function draw(e){const fixed=e.template.startsWith('egyedi-');const svg=`<svg viewBox="0 0 270 140" xmlns="http://www.w3.org/2000/svg">${fig(e.frames[0],3)}${fig(e.frames[1],137)}</svg>`;
const card=document.createElement('article');let h=document.createElement('h3');h.textContent=e.name;card.append(h);card.insertAdjacentHTML('beforeend',svg);const p=document.createElement('div');p.className='meta';p.textContent=e.category+' · '+e.regions.join(', ');card.append(p);const t=document.createElement('span');t.className='tag';t.textContent=e.state;card.append(t);return card;}
let shown=24;
function render(){const q=document.getElementById('q').value.toLocaleLowerCase('hu');const state=document.getElementById('state').value;let visible=ITEMS.filter(e=>(e.name+' '+e.regions.join(' ')).toLocaleLowerCase('hu').includes(q)&& (state==='all'||(state==='fixed')===e.template.startsWith('egyedi-')));document.getElementById('count').textContent=visible.length+' / '+ITEMS.length;document.getElementById('grid').replaceChildren(...visible.slice(0,shown).map(draw));document.getElementById('more').hidden=shown>=visible.length;}
for(const id of ['q','state'])document.getElementById(id).addEventListener('input',()=>{shown=24;render()});document.getElementById('more').addEventListener('click',()=>{shown+=24;render()});render();</script></body></html>'''
page=page.replace('__DATA__',json.dumps(rows,ensure_ascii=False,separators=(',',':')).replace('</script>','<\\/script>'))
(root/'docs/ABRA_AUDIT_v12.html').write_text(page,encoding='utf8')
print(len(page),len(rows))
