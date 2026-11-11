'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm');
const root=path.join(__dirname,'..');
const ctx={window:{}};vm.runInNewContext(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8'),ctx);
const R=ctx.window.MozduljRig;
const cat=JSON.parse(fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'),'utf8'));
const bones={armUpper:.31,armLower:.28,thigh:.45,shin:.43};
const distance=(a,b)=>Math.hypot(...a.map((v,i)=>v-b[i]));
const groups=['hands','feet','elbow','knee','shoulder','hip'];
let frames=0, dynamic=0, staticPoses=0, visuallySame=[], bad=[], clamped=[];
for(const e of cat.exercises){
 const r=e.pose.rig3D;if(!r)throw Error('No rig: '+e.id);
 R.checkPose(r);
 let joints=[];
 for(const f of r.frames){
  const j=R.joints(f);frames++;joints.push(j);
  for(const side of ['left','right'])for(const [a,b,c,limit] of [['shoulder','elbow','hands',bones.armUpper],['elbow','hands',null,bones.armLower],['hip','knee',null,bones.thigh],['knee','feet',null,bones.shin]]){
    let start=j[a][side],end=j[b][side];const expected=a==='elbow'?bones.armLower:a==='knee'?bones.shin:a==='hip'?bones.thigh:bones.armUpper;
    if(Math.abs(distance(start,end)-expected)>.015)bad.push(`${e.id} ${side} ${a}-${b} length ${distance(start,end)}`);
  }
  for(const side of ['left','right'])for(const part of ['elbow','knee','hands','feet']){
   if(j[part][side].some(v=>!Number.isFinite(v)))bad.push(`${e.id} invalid ${part}`);
  }
  if(j.clamped.length)clamped.push({id:e.id,phase:f.name,parts:j.clamped});
 }
 if(r.frames.length===1)staticPoses++;
 else{
  dynamic++;
  const a=joints[0];
  const distances=[];
  for(let i=1;i<joints.length;i++){
   const b=joints[i];
   let dx=distance(a.head,b.head)+distance(a.root,b.root);
   for(const key of ['headYaw','headPitch','headRoll','shoulderLift'])dx+=Math.abs((r.frames[i][key]||0)-(r.frames[0][key]||0));
   for(const group of groups)for(const side of ['left','right']) dx+=distance(a[group][side],b[group][side]);
   distances.push(dx);
  }
  if(Math.max(...distances)<.035)visuallySame.push(e.id);
 }
}
fs.writeFileSync(path.join(root,'docs','IK_ELERESI_KORLATOK_v16.json'),JSON.stringify({note:'A testcélpontok csak geometriailag ellenőrzöttek; nem gyógytornász által hitelesített pózok.',clamped},null,2));
console.log(JSON.stringify({count:cat.exercises.length,frames,dynamic,staticPoses,visuallySame:visuallySame.slice(0,30),bad:bad.slice(0,30),clamped:clamped.length,clampExamples:clamped.slice(0,12)},null,2));
if(cat.exercises.length!==132||bad.length||visuallySame.length)process.exit(1);
