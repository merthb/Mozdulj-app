const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'..');
const data=JSON.parse(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'),'utf8'));
const ctx={window:{}};
vm.runInNewContext(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8'),ctx);
const rig=ctx.window.MozduljRig;let frames=0,clampedNew=0,allFrames=0,serious=0,dynamic=0;
const distance=(a,b)=>Math.hypot(...a.map((v,i)=>v-b[i]));
for(const ex of data.exercises){
 const p=ex.pose.rig3D;
 rig.checkPose(p);
 let startJ=null;let anyChanged=false;
 for(const f of p.frames){
  const j=rig.joints(f);allFrames++;
  if(!startJ)startJ=j;
  else if(distance(startJ.hands.left,j.hands.left)>.01 || distance(startJ.hands.right,j.hands.right)>.01 ||
          distance(startJ.feet.left,j.feet.left)>.01 ||distance(startJ.feet.right,j.feet.right)>.01||
          distance(startJ.root,j.root)>.01|| distance(startJ.head,j.head)>.01)
       anyChanged=true;
  for(const side of ['left','right']){
   assert(Math.abs(distance(j.shoulder[side],j.elbow[side])-.31)<.0001,ex.id+' upper arm');
   assert(Math.abs(distance(j.elbow[side],j.hands[side])-.28)<.0001,ex.id+' lower arm');
   assert(Math.abs(distance(j.hip[side],j.knee[side])-.45)<.0001,ex.id+' femur');
   assert(Math.abs(distance(j.knee[side],j.feet[side])-.43)<.0001,ex.id+' shin');
   assert(Math.abs(distance(j.feet[side],j.toes[side])-.18)<.0001,ex.id+' foot');
  }
  if(ex.id.startsWith('user-yoga-')){
    frames++;
    if(j.clamped.length)clampedNew++;
    const overlap=rig.Collision.report(f);
    if(overlap.examples.some(q=>q.depth>.05))serious++;
  }
 }
 if(anyChanged)dynamic++;
}
assert.equal(data.exercises.length,294);
assert.equal(frames,212);
assert.equal(clampedNew,0);
console.log('PASS 3D: '+data.exercises.length+' exercises, '+allFrames+' total frames, '+frames+' candidate frames, '+dynamic+' visibly dynamic rigs.');
console.log('New candidates: '+clampedNew+' unreachable targets, '+serious+' frames with collision depths >5 cm (requiring individual review).');
