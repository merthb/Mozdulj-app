const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');
const root=path.resolve(__dirname,'..'), data=JSON.parse(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'),'utf8'));
const ctx={window:{}};vm.runInNewContext(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8'),ctx);
const engine=ctx.window.MozduljRig;
let frames=0, legControls=0;
for(const exercise of data.exercises){
  const rig=exercise.pose.rig3D;assert(rig,exercise.id+' missing 3D rig');
  engine.checkPose(rig);
  for(const f of rig.frames){
    const j=engine.joints(f); frames++;
    for(const side of ['left','right']){
      for(const pos of [j.knee[side],j.elbow[side],j.feet[side],j.hands[side],j.toes[side]])
        assert(pos.every(Number.isFinite),exercise.id+' '+side+' produced NaN');
      const length=(a,b)=>Math.hypot(...a.map((x,i)=>x-b[i]));
      assert(Math.abs(length(j.hip[side],j.knee[side])-.45)<.0001,exercise.id+' femur length');
      assert(Math.abs(length(j.knee[side],j.feet[side])-.43)<.0001,exercise.id+' tibia length');
      assert(Math.abs(length(j.feet[side],j.toes[side])-.18)<.0001,exercise.id+' foot length');
      if(f.legAngles?.[side])legControls++;
    }
  }
}
assert(data.exercises.length===158);
assert(data.catalogVersion===11);
for(const id of ['achilles-vadli-nyujtott','achilles-soleus-hajlitott']){
  const e=data.exercises.find(x=>x.id===id);assert(e && e.regions.includes('vádli') && e.phase==='cooldown' && e.category==='nyújtás');
}
assert(!data.categories.some(x=>x.toLowerCase().includes('jóga')));
console.log(`PASS: ${data.exercises.length} gyakorlat / ${frames} fázis, stabil szegmenshosszak, ${legControls} szögvezérelt lábfázis, 2 Achilles-környéki nyújtás`);
