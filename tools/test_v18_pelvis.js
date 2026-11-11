const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const path = require('path');
const root = path.join(__dirname, '..');
const ctx = {window:{},console,Math,Float32Array,Uint16Array,Error,Array,Object};
vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(root, 'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8'),ctx);
const engine = ctx.window.MozduljRig;
const catalog = JSON.parse(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'),'utf8'));
const exercise = catalog.exercises.find(e=>e.id==='medencebillentes-hanyatt');
assert(exercise,'A javítandó alapgyakorlat hiányzik');
const pose = exercise.pose.rig3D;
engine.checkPose(pose);
assert.strictEqual(pose.frames.length,2);
const [a,b] = pose.frames;
assert(b.pelvicTilt > .1 && b.pelvicTilt <= .35,'Látható, de kismértékű medencebillentés szükséges');
assert.strictEqual(a.pelvicTilt,0);
for (const key of ['root','euler']) assert.deepStrictEqual(a[key],b[key],key+' nem változhat');
for (const key of ['hands','feet'])
 for(const side of ['left','right']) assert.deepStrictEqual(a[key][side],b[key][side],key+'/'+side+' nem mozoghat');
const ja=engine.joints(a), jb=engine.joints(b);
const diff=(x,y)=>Math.hypot(...x.map((v,i)=>v-y[i]));
for (const part of ['shoulder','elbow','hands','hip','knee','feet']) {
 for (const side of ['left','right']) {
  assert(diff(ja[part][side],jb[part][side]) < 1e-8,
    `Nem kívánt mozgás ${part}/${side}: ${diff(ja[part][side],jb[part][side])}`);
 }
}
assert.strictEqual(ja.clamped.length,0,'Kiinduló fázisnál nem lehet elérhetetlen célpont');
assert.strictEqual(jb.clamped.length,0,'Mozdulat fázisnál nem lehet elérhetetlen célpont');
const invalid=JSON.parse(JSON.stringify(pose));
invalid.frames[1].pelvicTilt=0.9;
assert.throws(()=>engine.checkPose(invalid),'Túlzott medenceforgatás validációja');
console.log('PASS: medencebillentés 2 fázis, medence-rotáció, változatlan váll/könyök/kéz/csípő/térd/láb, nincs IK-korlátátlépés');
