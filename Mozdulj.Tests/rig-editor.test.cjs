const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const root=path.resolve(__dirname,'../Mozdulj.Maui/Resources/Raw/rig3d');
const History=require(path.join(root,'history.js'));
const state=(height,frameIndex=0)=>({pose:{frames:[{root:[0,height,0]}]},frameIndex});
test('one drag is one undo, redo restores final pose',()=>{
 const h=new History();h.begin(state(1));
 for(const y of [.9,.8,.7])h.record(state(y+.1),state(y));
 h.finish(state(.7));assert.equal(h.past.length,1);
 assert.deepEqual(h.undo(state(.7)),state(1));assert.deepEqual(h.redo(state(1)),state(.7));
});
test('no-op keeps redo and new edit clears it; frame navigation alone is not an edit',()=>{
 const h=new History();h.record(state(1),state(.8));
 h.undo(state(.8));assert.equal(h.canRedo,true);
 h.record(state(1,0),state(1,1));assert.equal(h.canRedo,true);
 h.record(state(1),state(.6));assert.equal(h.canRedo,false);
});
test('deep snapshots preserve complete pose metadata and bounded history',()=>{
 const h=new History(2);const a=state(1);a.pose.frames[0].equipment=[{type:'band',anchorA:'leftHand'}];
 const b=structuredClone(a);b.pose.frames[0].equipment[0].anchorA='rightHand';
 h.record(a,b);a.pose.frames[0].equipment[0].anchorA='fixed';
 assert.equal(h.undo(b).pose.frames[0].equipment[0].anchorA,'leftHand');
 h.clear();for(let i=0;i<4;i++)h.record(state(i),state(i+1));assert.equal(h.past.length,2);
});
test('all packaged JS parses',()=>{
 for(const file of ['history.js','engine.js','studio.js','localization.js'])new vm.Script(fs.readFileSync(path.join(root,file),'utf8'),{filename:file});
});
test('pelvis movement preserves IK targets and clamps world bounds',()=>{
 const context={};context.window=context;vm.createContext(context);
 vm.runInContext(fs.readFileSync(path.join(root,'engine.js'),'utf8'),context);
 const rig=context.MozduljRig;const f=rig.baseFrame('crosslegged');
 const before=JSON.stringify(f.hands),feet=JSON.stringify(f.feet),y=f.root[1];
 rig.moveRoot(f,[0,-.05,0]);assert.ok(Math.abs(f.root[1]-(y-.05))<1e-8);
 assert.equal(JSON.stringify(f.hands),before);assert.equal(JSON.stringify(f.feet),feet);
 rig.moveRoot(f,[0,-100,0]);assert.equal(f.root[1],0);
});

function loadRig(){
 const context={};context.window=context;vm.createContext(context);
 vm.runInContext(fs.readFileSync(path.join(root,'engine.js'),'utf8'),context);
 return context.MozduljRig;
}
test('capsules detect crossing segments but allow surface contact and adjacent bones',()=>{
 const c=loadRig().Collision;
 const a={id:'forearm-left',a:[-1,0,0],b:[1,0,0],r:.05};
 const b={id:'thigh-right',a:[0,-1,0],b:[0,1,0],r:.05};
 assert.equal(c.segmentDistance(a.a,a.b,b.a,b.b),0);
 assert.equal(c.selfOverlaps([a,b]).size,1);
 const contact={...b,a:[0,-1,.1],b:[0,1,.1]};
 assert.equal(c.selfOverlaps([a,contact]).size,0);
 assert.equal(c.selfOverlaps([a,{...b,id:'upperArm-left'}]).size,0);
 assert.equal(c.segmentDistance([0,0,0],[0,0,0],[1,0,0],[1,0,0]),1);
});
test('old self-overlap may improve but cannot deepen or accumulate',()=>{
 const c=loadRig().Collision;
 const baseline=new Map([['body:forearm-left/torso',.04]]);
 assert.equal(c.worse(new Map([['body:forearm-left/torso',.03]]),baseline),null);
 assert.ok(c.worse(new Map([['body:forearm-left/torso',.041]]),baseline));
 assert.ok(c.worse(new Map([['body:head/hand-left',.01]]),baseline));
});
test('world locks capture rendered coordinates and prevent root drift',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 rig.setPointLock(f,'root',true);
 const a=JSON.stringify(f.root);
 const next=JSON.parse(JSON.stringify(f));rig.moveRoot(next,[0,.15,0]);
 const result=rig.Collision.transition(f,next);
 assert.equal(result.limited,true);
 assert.ok(Math.abs(result.frame.root[1]-f.root[1])<=.0050001);
 assert.equal(JSON.stringify(result.frame.pointLocks.root),a);
 rig.setPointLock(f,'root',false);
 assert.equal(f.pointLocks,undefined);
});
test('knee and hand locks refer to real joint positions and survive JSON',()=>{
 const rig=loadRig(),f=rig.baseFrame('tabletop');
 rig.setPointLock(f,'leftKnee',true);rig.setPointLock(f,'rightHand',true);
 const j=rig.joints(f);
 assert.equal(JSON.stringify(f.pointLocks.leftKnee),JSON.stringify(j.knee.left));
 assert.equal(JSON.stringify(f.pointLocks.rightHand),JSON.stringify(j.hands.right));
 const copy=JSON.parse(JSON.stringify(f));
 assert.equal(rig.lockErrors(rig.joints(copy),copy.pointLocks).size,0);
 const pose=rig.initialPose('stand');pose.frames[0].pointLocks={bad:[0,0,0]};
 assert.throws(()=>rig.checkPose(pose));
});

test('angle inference recovers representable limb geometry and does not mutate frame',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 f.armAngles.right={flexion:.4,abduction:.3,elbowFlexion:.7,wristFlexion:0,forearmRotation:0};
 f.legAngles.left={flexion:.5,abduction:.15,kneeFlexion:.4};
 const before=JSON.stringify(f),j=rig.joints(f);
 for(const [kind,side,key] of [['arm','right','armAngles'],['leg','left','legAngles']]){
   const fit=rig.inferAngles(f,kind,side);
   assert.ok(fit.error<1e-5,kind+' fit: '+fit.error);
   const copy=JSON.parse(before);copy[key][side]=fit.angles;
   assert.ok(rig.limbError(j,rig.joints(copy),kind,side)<1e-5);
 }
 assert.equal(JSON.stringify(f),before);
});
test('unrepresentable mode changes are atomic; accepted changes preserve joints',()=>{
 const rig=loadRig();
 for(const preset of ['stand','crosslegged','tabletop','lunge','supine']){
   const f=rig.baseFrame(preset);
   for(const kind of ['arm','leg'])for(const side of ['left','right']){
     const copy=JSON.parse(JSON.stringify(f)),before=JSON.stringify(copy),j=rig.joints(copy);
     const ok=rig.prepareAngleMode(copy,kind,side);
     if(ok)assert.ok(rig.limbError(j,rig.joints(copy),kind,side)<=1e-5);
     else assert.equal(JSON.stringify(copy),before);
   }
 }
 const f=rig.baseFrame('stand');f.armAngles.right={flexion:.4,abduction:.3,elbowFlexion:.7,wristFlexion:.6,forearmRotation:.7};
 const before=JSON.stringify(f);
 assert.equal(rig.prepareTargetMode(f,'arm','right'),false);
 assert.equal(JSON.stringify(f),before);
});
test('chest rotation and spine bend are independent of pelvis and retain limb lengths',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),a=rig.joints(f);
 f.upperYaw=.4;f.spineBend=.25;
 const b=rig.joints(f),m=rig.math;
 assert.equal(JSON.stringify(a.hip),JSON.stringify(b.hip));
 assert.ok(m.len(m.sub(a.shoulder.left,b.shoulder.left))>.01);
 for(const side of ['left','right']){
   assert.ok(Math.abs(m.len(m.sub(b.shoulder[side],b.elbow[side]))-.31)<1e-6);
   assert.ok(Math.abs(m.len(m.sub(b.elbow[side],b.hands[side]))-.28)<1e-6);
 }
});

test('axial rotations preserve bone lengths, move the distal joint and are inferable',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 f.legAngles.left={flexion:.6,abduction:.2,kneeFlexion:.8,axialRotation:0};
 f.armAngles.right={flexion:.4,abduction:.3,elbowFlexion:.7,axialRotation:0};
 const a=rig.joints(f);
 f.legAngles.left.axialRotation=.55;f.armAngles.right.axialRotation=-.45;
 const b=rig.joints(f);
 assert.ok(m.len(m.sub(a.feet.left,b.feet.left))>.03);
 assert.ok(m.len(m.sub(a.hands.right,b.hands.right))>.03);
 assert.ok(m.len(m.sub(a.knee.left,b.knee.left))<1e-8);
 assert.ok(m.len(m.sub(a.elbow.right,b.elbow.right))<1e-8);
 assert.ok(Math.abs(m.len(m.sub(b.knee.left,b.feet.left))-.43)<1e-8);
 assert.ok(Math.abs(m.len(m.sub(b.elbow.right,b.hands.right))-.28)<1e-8);
 for(const [kind,side] of [['leg','left'],['arm','right']])assert.ok(rig.inferAngles(f,kind,side,true).error<1e-5);
 const pose=rig.initialPose('stand');pose.frames[0].legAngles.left={...f.legAngles.left,axialRotation:2};
 assert.throws(()=>rig.checkPose(pose));
});
