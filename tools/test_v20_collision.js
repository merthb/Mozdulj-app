const fs=require('fs'),vm=require('vm'),assert=require('assert');
const src=fs.readFileSync(__dirname+'/../Mozdulj.Maui/Resources/Raw/rig3d/engine.js','utf8');
const ctx={window:{},console};vm.createContext(ctx);vm.runInContext(src,ctx);
const {initialPose,joints,Collision}=ctx.window.MozduljRig;
const clone=v=>JSON.parse(JSON.stringify(v));
let checks=0;
function ok(v,msg){assert(v,msg);checks++;}
const base=initialPose('stand').frames[0];
const baseRep=Collision.report(base);console.log('standing baseline penetration count',baseRep.count,baseRep.examples);
// 1. Floor: a foot cannot be dragged through the ground.
{
 let after=clone(base);after.root[1]=-.65;
 let r=Collision.transition(base,after);
 console.log('floor',r.fraction,r.issue);
 ok(r.limited,'floor movement should be limited');
 ok(r.fraction<1,'floor transition cannot finish below floor');
}
// 2. A wall is a finite solid; sweeping across it must be blocked.
{
 let before=clone(base);before.equipment=[{type:'wall',position:[0,1,-.35]}];before.hands.right=[.34,1.06,.19];
 const after=clone(before);after.hands.right=[.34,1.06,-.93];
 let r=Collision.transition(before,after);
 console.log('wall',r.fraction,r.issue,Collision.report(before).count);
 ok(r.limited,'sweep across wall must stop');
 ok(r.issue?.includes('wall'),'wall collision should name wall');
}
// 3. A sphere cannot be passed through by an arm even if the end is clear.
{
 let before=clone(base);before.equipment=[{type:'ball',position:[.34,.92,-.18]}];before.hands.right=[.35,1.00,.35];
 let after=clone(before);after.hands.right=[.35,1.0,-.72];
 const r=Collision.transition(before,after);
 console.log('ball',r.fraction,r.issue,Collision.report(before).count);
 ok(r.limited,'sphere obstacle must limit arm movement');
}
// 4. High hip flexion must preserve segment lengths, including both sides.
{
 let f=clone(base);f.legAngles={left:{flexion:Math.PI*.68,abduction:0,kneeFlexion:.2},right:{flexion:Math.PI*.68,abduction:0,kneeFlexion:.2}};
 const j=joints(f);
 for(const side of ['left','right']){
  const L=(a,b)=>Math.hypot(...a.map((v,i)=>v-b[i]));
  ok(Math.abs(L(j.hip[side],j.knee[side])-.45)<.001,side+' femur length');
  ok(Math.abs(L(j.knee[side],j.feet[side])-.43)<.001,side+' shin length');
  ok(Math.abs(L(j.feet[side],j.toes[side])-.18)<.001,side+' foot length');
 }
}
// 5. Arm controls preserve both segments across a high shoulder raise.
{
 let f=clone(base);f.armAngles={right:{flexion:Math.PI*.8,abduction:.3,elbowFlexion:1.5,wristFlexion:.4,forearmRotation:.6}};
 const j=joints(f);const L=(a,b)=>Math.hypot(...a.map((v,i)=>v-b[i]));
 ok(Math.abs(L(j.shoulder.right,j.elbow.right)-.31)<.001,'upper arm length');
 ok(Math.abs(L(j.elbow.right,j.hands.right)-.28)<.001,'forearm length');
 ok(Math.abs(L(j.hands.right,j.palms.right)-.115)<.001,'palm length');
}
// 6. Pressing downward with foot while a wall exists must still stop at floor.
{
 let before=clone(base);before.equipment=[{type:'wall'}];
 let after=clone(before);after.root[1]=-.35;
 const r=Collision.transition(before,after);
 ok(r.limited,'floor with wall');
}
// 7. Zero edit has no extra invalidity despite existing old preset overlaps.
{
 const f=initialPose('chair').frames[0];
 const r=Collision.transition(f,clone(f));
 ok(!r.limited,'unchanged chair pose should not be rejected');
}
console.log('PASS',checks,'collision and kinematic assertions');
