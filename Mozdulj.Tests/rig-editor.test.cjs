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

test('full mirror swaps sides and reflects bone geometry, props and support locks',()=>{
 const rig=loadRig(),f=rig.baseFrame('lunge'),m=rig.math;
 f.root[0]=.12;f.euler=[.12,.35,.1];f.upperYaw=.2;f.headYaw=.1;f.headRoll=-.15;
 f.armAngles.right={flexion:.4,abduction:.3,elbowFlexion:.7,axialRotation:.3};
 f.legAngles.left={flexion:.6,abduction:.2,kneeFlexion:.8,axialRotation:.2};
 f.equipment.push({type:'band',anchorA:'leftHand',anchorB:'fixed',anchorC:'floorRight',positionB:[.3,.2,.7],route:'left'});
 rig.setPointLock(f,'rightHand',true);
 const a=rig.joints(f),g=rig.mirrorFrame(f),b=rig.joints(g);
 const reflected=p=>[-p[0],p[1],p[2]];
 for(const k of ['root','head','neck','spineUpper'])assert.ok(m.len(m.sub(b[k],reflected(a[k])))<1e-7,k);
 for(const k of ['hip','shoulder','elbow','hands','knee','feet','toes','palms'])
   for(const side of ['left','right'])assert.ok(m.len(m.sub(b[k][side],reflected(a[k][side==='left'?'right':'left'])))<1e-7,k+' '+side);
 const band=g.equipment.at(-1);
 assert.equal(band.anchorA,'rightHand');assert.equal(band.anchorC,'floorLeft');assert.equal(band.route,'right');
 assert.equal(JSON.stringify(band.positionB),JSON.stringify([-.3,.2,.7]));
 assert.equal(JSON.stringify(g.pointLocks.leftHand),JSON.stringify(reflected(f.pointLocks.rightHand)));
 assert.equal(JSON.stringify(rig.mirrorFrame(g)),JSON.stringify(f));
});
test('copy limb transfers angles in body space without changing the source limb',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 f.euler=[.1,.2,.1];
 f.armAngles.left={flexion:.3,abduction:.5,elbowFlexion:.7,axialRotation:.2};
 const a=rig.joints(f),original=JSON.stringify(f.armAngles.left);
 rig.copyLimb(f,'arm','left');
 const b=rig.joints(f);
 assert.equal(JSON.stringify(f.armAngles.left),original);
 assert.equal(f.armAngles.right.axialRotation,-.2);
 assert.ok(m.len(m.sub(a.hands.left,b.hands.left))<1e-9);
});

test('symmetric angle edits preserve the initial offset and mirror rotations',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 f.armAngles={left:{flexion:.2,abduction:.3,elbowFlexion:.4,axialRotation:.2},right:{flexion:.4,abduction:.3,elbowFlexion:.4,axialRotation:-.1}};
 rig.editLimbAngle(f,'arm','left','flexion',.5,true);
 assert.ok(Math.abs(f.armAngles.right.flexion-.7)<1e-8);
 rig.editLimbAngle(f,'arm','left','axialRotation',.5,true);
 assert.ok(Math.abs(f.armAngles.right.axialRotation+.4)<1e-8);
});
test('mirrored target deltas are in body space and do not overwrite the other target',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),before=JSON.parse(JSON.stringify(f));
 f.hands.left[0]-=.05;f.hands.left[1]+=.03;
 rig.mirrorTargetDelta(f,before,'hands','left');
 assert.ok(Math.abs(f.hands.right[0]-before.hands.right[0]-.05)<1e-8);
 assert.ok(Math.abs(f.hands.right[1]-before.hands.right[1]-.03)<1e-8);
});
test('phase direction and expanded phase limit validate, mirror and reorder',()=>{
 const rig=loadRig(),pose=rig.initialPose('stand');
 pose.frames[0].direction='left';assert.equal(rig.mirrorFrame(pose.frames[0]).direction,'right');
 pose.frames[1].direction='clockwise';assert.equal(rig.mirrorFrame(pose.frames[1]).direction,'counterclockwise');
 assert.equal(rig.movePhase(pose.frames,0,1),true);assert.equal(pose.frames[1].direction,'left');
 while(pose.frames.length<16)pose.frames.push(JSON.parse(JSON.stringify(pose.frames[0])));
 assert.doesNotThrow(()=>rig.checkPose(pose));
 pose.frames.push(pose.frames[0]);assert.throws(()=>rig.checkPose(pose));pose.frames.pop();
 pose.frames[0].direction='unknown';assert.throws(()=>rig.checkPose(pose));
});
test('preview is read-only, checks every displayed sample and rejects incompatible equipment changes',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 const original=JSON.stringify(a),result=rig.Collision.previewTransition(a,b);
 assert.equal(result.ok,true);assert.equal(result.samples.length,49);
 assert.equal(JSON.stringify(a),original);
 b.equipment.push({type:'mat'});
 assert.equal(rig.Collision.previewTransition(a,b).issue,'representation');
});

test('rotated chair collision solids follow the rendered backrest',()=>{
 const rig=loadRig(),f=rig.baseFrame('chair');
 f.equipment[0].position=[1,.3,0];f.equipment[0].yaw=Math.PI/2;
 const back=rig.Collision.solids(f).find(x=>x.id.includes('háttámla'));
 assert.ok(Math.abs(back.centre[0]-.67)<1e-8);
 assert.ok(Math.abs(back.centre[2])<1e-8);assert.equal(back.yaw,Math.PI/2);
 assert.equal(rig.mirrorFrame(f).equipment[0].yaw,-Math.PI/2);
});
test('equipment scope matches by type and occurrence, locked changes are atomic',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=rig.baseFrame('stand');
 a.equipment=[{type:'mat'},{type:'block',position:[2,.1,0]}];
 b.equipment=[{type:'block',position:[2,.1,0],locked:true}];
 const frames=[a,b],before=JSON.stringify(frames),locations=rig.matchingProp(frames,0,1);
 assert.equal(JSON.stringify(locations),JSON.stringify([{frame:0,index:1},{frame:1,index:0}]));
 assert.equal(rig.editProps(frames,locations,[2,.1,.1],.3).ok,false);
 assert.equal(JSON.stringify(frames),before);
 delete b.equipment[0].locked;
 const result=rig.editProps(frames,locations,[2,.1,.1],.3);
 assert.equal(result.ok,true);assert.equal(result.frames[1].equipment[0].yaw,.3);
 assert.equal(a.equipment[1].yaw,undefined);
});

test('workspace has unique controls, five accessible tabs and valid angle limits',()=>{
 const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
 const ids=[...html.matchAll(/\bid="([^"]+)"/g)].map(x=>x[1]);
 assert.equal(new Set(ids).size,ids.length);
 for(const id of ['body','phases','props','view','review']){
   assert.ok(html.includes('id="tab-'+id+'"'));
   assert.ok(html.includes('id="pane-'+id+'"'));
 }
 for(const [id,min,max] of [['armFlex',-.52,3.05],['armAbduct',0,2.70],['wristFlex',-1.04,1.04],['forearmRotation',-1.57,1.57]]){
   const m=html.match(new RegExp('id="'+id+'" type="range" min="([^"]+)" max="([^"]+)"'));
   assert.ok(m,id);assert.ok(+m[1]*Math.PI/180>=min,id+' min');assert.ok(+m[2]*Math.PI/180<=max,id+' max');
 }
 assert.ok(html.includes('id="rootHeight" type="number" min="0" max="300"'));
});

test('editor markup preserves panel boundaries and valid nesting',()=>{
 const html=fs.readFileSync(path.join(root,'index.html'),'utf8').replace(/<style>[\s\S]*?<\/style>/g,'');
 const stack=[];
 for(const m of html.matchAll(/<(\/?)([a-z][a-z0-9-]*)([^>]*?)>/gi)){
   const tag=m[2].toLowerCase();
   if(['meta','link','input','br','hr','img'].includes(tag))continue;
   if(m[1])assert.equal(stack.pop(),tag,'Unbalanced closing '+tag);
   else stack.push(tag);
 }
 assert.equal(stack.length,0);
 assert.match(html,/<\/div>\s*<\/div><div id="status"/);
});

test('21 selectable anatomy points have finite world coordinates',()=>{
 const rig=loadRig(),j=rig.joints(rig.baseFrame('stand'));
 assert.equal(rig.BODY_TARGETS.length,21);
 assert.equal(new Set(rig.BODY_TARGETS.map(x=>x.key)).size,21);
 for(const t of rig.BODY_TARGETS){assert.equal(rig.bodyTarget(t.key).kind,t.kind);assert.ok(rig.bodyPoint(j,t.key).every(Number.isFinite));}
});
test('direct bend-plane edit keeps bone lengths and endpoint unchanged',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math,a=rig.joints(f);
 assert.equal(rig.moveBendPoint(f,'elbow','left',[.01,0,.01]),true);
 const b=rig.joints(f);
 assert.ok(m.len(m.sub(a.hands.left,b.hands.left))<1e-8);
 assert.ok(Math.abs(m.len(m.sub(b.shoulder.left,b.elbow.left))-.31)<1e-8);
 assert.ok(Math.abs(m.len(m.sub(b.elbow.left,b.hands.left))-.28)<1e-8);
});
test('support adaptation preserves reachable endpoint and lock coordinates',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 rig.setPointLock(f,'leftHand',true);const locked=JSON.stringify(f.pointLocks.leftHand);
 rig.prepareSupportEdit(f);rig.moveRoot(f,[0,-.02,0]);rig.adaptSupportPoints(f,f.pointLocks);
 assert.equal(JSON.stringify(f.pointLocks.leftHand),locked);
 assert.ok(m.len(m.sub(rig.joints(f).hands.left,f.pointLocks.leftHand))<=.005);
});

test('camera pinch changes distance and pans on camera axes, without changing pose',()=>{
 const rig=loadRig(),pose=rig.initialPose(),before=JSON.stringify(pose.frames);
 const c=pose.camera;c.azimuth=0;c.elevation=0;c.distance=4;c.target=[0,1,0];
 const first=rig.gestureMetrics([{x:100,y:100},{x:200,y:100}]);
 const next=rig.gestureMetrics([{x:70,y:110},{x:270,y:110}]);
 rig.pinchCamera(c,first,next,300);
 assert.equal(c.distance,2);assert.ok(c.target[0]<0&&c.target[1]>1);
 assert.equal(JSON.stringify(pose.frames),before);
 rig.pinchCamera(c,{x:0,y:0,distance:1},{x:0,y:0,distance:1000},300);assert.equal(c.distance,1.2);
});
test('orthographic projection is depth-independent in screen x and y',()=>{
 const m=loadRig().ortho(2,1.5);
 assert.ok(Math.abs(m[0]-1/3)<1e-7);assert.equal(m[5],.5);assert.equal(m[11],0);assert.equal(m[15],1);
});

test('diagnostic geometry uses actual body, support and surface probe positions',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),j=rig.joints(f);
 const parts=rig.issueGeometry(f,j,'body:forearm-left/torso');
 assert.equal(parts.length,2);
 assert.equal(rig.issueGeometry(f,j,'lock:leftKnee').length,1);
 const probe=rig.Collision.bodyProbes(j)[0];
 assert.equal(JSON.stringify(rig.issueGeometry(f,j,probe.id+'@padló')[0].a),JSON.stringify(probe.p));
 assert.equal(rig.issueGeometry(f,j,'unknown').length,0);
});

test('support planes use equipment orientation and finite bounds',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 f.equipment=[{type:'wall',position:[0,1,-1],yaw:Math.PI/2},{type:'chair',position:[0,.3,0]}];
 const wall=rig.contactSurface(f,'rightHand','wall',0);
 assert.ok(Math.abs(wall.normal[0]-1)<1e-8);
 assert.ok(Math.abs(wall.origin[0]-.025)<1e-8);
 const seat=rig.contactSurface(f,'root','seat',1);
 assert.equal(seat.origin[1],.375);
 assert.equal(rig.contactSurface(f,'root','seat',0),null);
 const j=rig.joints(f);assert.equal(rig.contactInside(j,'root',seat),true);
 j.root=[2,1,0];assert.equal(rig.contactInside(j,'root',seat),false);
});
test('support fitting failures are atomic and existing locks are respected',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');rig.setPointLock(f,'leftFoot',true);
 const before=JSON.stringify(f);
 assert.equal(rig.fitSupport(f,'floor',['leftFoot','rightFoot'],null,true).issue,'locked');
 assert.equal(rig.fitSupport(f,'wall',['rightHand'],0).issue,'surface');
 assert.equal(JSON.stringify(f),before);
});
test('already aligned hand can be fitted and locked without moving the source',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 const j=rig.joints(f),envelope=rig.contactEnvelope(j,'rightHand');
 // Place a wall so its front face is tangent to the hand envelope plus gap.
 const z=Math.min(...envelope.map(x=>x.p[2]-x.r))-.027;
 f.equipment=[{type:'wall',position:[j.hands.right[0],j.hands.right[1],z],yaw:0}];
 const before=JSON.stringify(f),result=rig.fitSupport(f,'wall',['rightHand'],0,true);
 assert.equal(result.ok,true,JSON.stringify(result));
 assert.equal(JSON.stringify(f),before);
 const final=rig.joints(result.frame),surface=rig.contactSurface(result.frame,'rightHand','wall',0);
 assert.ok(Math.abs(rig.contactDistance(final,'rightHand',surface)-.002)<=.005);
 assert.ok(result.frame.pointLocks.rightHand);
});

test('authored hold and outgoing transition times produce a checked continuous loop',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 a.name='A';a.holdSeconds=.5;a.transitionSeconds=2;
 b.name='B';b.phaseType='hold';b.transitionSeconds=1;
 const before=JSON.stringify([a,b]),calls=[];
 const checked=(x,y)=>{calls.push(x.name+y.name);return {ok:true,samples:[x,y]};};
 const plan=rig.previewPlan([a,b],true,checked);
 assert.equal(plan.ok,true);assert.equal(plan.duration,5500);
 assert.equal(JSON.stringify(calls),JSON.stringify(['AB','BA']));
 assert.equal(rig.previewAt(plan,100).kind,'hold');
 assert.equal(rig.previewAt(plan,700).kind,'move');
 assert.equal(rig.previewAt(plan,3000).frame.name,'B');
 assert.equal(rig.previewAt(plan,5500).frame.name,'A');
 assert.equal(JSON.stringify([a,b]),before);
 const noLoop=rig.previewPlan([a,b],false,checked);
 assert.equal(noLoop.duration,4500);assert.equal(rig.previewAt(noLoop,4500).finished,true);
 const blocked=rig.previewPlan([a,b],true,(x,y)=>x===b?{ok:false,issue:'blocked'}:checked(x,y));
 assert.equal(blocked.ok,false);assert.equal(blocked.from,1);assert.equal(blocked.to,0);
});
test('old phase timings stay implicit, invalid timing fails validation, thumbnail coordinates are finite',()=>{
 const rig=loadRig(),pose=rig.initialPose('stand');
 assert.equal(rig.phaseTiming(pose.frames[0]).transition,1.2);
 assert.equal(rig.phaseTiming(pose.frames[0]).hold,0);
 assert.equal(pose.frames[0].transitionSeconds,undefined);
 const previews=rig.phaseThumbnailData(pose.frames);
 assert.equal(previews.length,pose.frames.length);
 assert.ok(previews.every(p=>p.lines.flat(2).every(Number.isFinite)&&p.headRadius>0));
 pose.frames[0].holdSeconds=-1;assert.throws(()=>rig.checkPose(pose));
 pose.frames[0].holdSeconds=0;pose.frames[0].transitionSeconds=0;assert.throws(()=>rig.checkPose(pose));
 pose.frames[0].transitionSeconds=1;pose.frames[0].phaseType='hold';assert.doesNotThrow(()=>rig.checkPose(pose));
});

test('equipment identities survive different frame orders and isolate new objects from legacy ones',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=rig.baseFrame('stand');
 a.equipment=[{type:'block',id:'eq-1'},{type:'block',id:'eq-2'},{type:'block'}];
 b.equipment=[{type:'block'},{type:'block',id:'eq-2'},{type:'block',id:'eq-1'}];
 const frames=[a,b];
 assert.equal(rig.newEquipmentId(frames),'eq-3');
 assert.equal(JSON.stringify(rig.matchingProp(frames,0,0)),JSON.stringify([{frame:0,index:0},{frame:1,index:2}]));
 assert.equal(JSON.stringify(rig.matchingProp(frames,0,2)),JSON.stringify([{frame:0,index:2},{frame:1,index:0}]));
 assert.equal(rig.validEquipmentIds(frames),true);
 b.equipment[0].id='eq-2';assert.equal(rig.validEquipmentIds(frames),false);
 b.equipment[0].id='bad id';assert.equal(rig.validEquipmentIds(frames),false);
});
test('explicit three-point band handles detach at the rendered position and move independently',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 f.equipment=[{type:'band',id:'eq-1',anchorA:'leftHand',anchorB:'rightHand',anchorC:'fixed',positionC:[0,.1,.4]}];
 let handles=rig.equipmentHandles(f);
 assert.equal(handles.length,3);assert.equal(handles[0].movable,false);assert.equal(handles[2].movable,true);
 const first=JSON.stringify(handles[0].position),other=JSON.stringify(handles[1].position);
 assert.equal(rig.moveBandPoint(f,0,'A',[.1,0,0]),false);
 assert.equal(rig.detachBandPoint(f,0,'A'),true);
 assert.equal(JSON.stringify(f.equipment[0].position),first);
 assert.equal(rig.moveBandPoint(f,0,'A',[.1,0,0]),true);
 handles=rig.equipmentHandles(f);
 assert.equal(handles[0].movable,true);assert.notEqual(JSON.stringify(handles[0].position),first);
 assert.equal(JSON.stringify(handles[1].position),other);
 assert.equal(JSON.stringify(f.equipment[0].positionC),JSON.stringify([0,.1,.4]));
});

test('four-point band follows A-C-D-B, retains legacy routes and mirrors each anchor',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),j=rig.joints(f);
 const band={type:'band',anchorA:'leftHand',anchorB:'rightHand',anchorC:'leftToe',anchorD:'rightToe'};
 f.equipment=[band];
 const seg=rig.bandSegments(band,f,j);
 assert.equal(seg.length,3);
 assert.equal(JSON.stringify(seg),JSON.stringify([[j.hands.left,j.toes.left],[j.toes.left,j.toes.right],[j.toes.right,j.hands.right]]));
 assert.equal(rig.equipmentHandles(f).length,4);
 const mirror=rig.mirrorFrame(f).equipment[0];assert.equal(mirror.anchorC,'rightToe');assert.equal(mirror.anchorD,'leftToe');
 const old={...band};delete old.anchorD;assert.equal(rig.bandSegments(old,f,j).length,2);
 delete old.anchorC;assert.equal(rig.bandSegments(old,f,j).length,1);
 assert.equal(rig.detachBandPoint(f,0,'D'),true);
 const previous=JSON.stringify(f.equipment[0].positionD);
 assert.equal(rig.moveBandPoint(f,0,'D',[.12,0,0]),true);
 assert.notEqual(JSON.stringify(f.equipment[0].positionD),previous);
 const reflected=rig.mirrorFrame(f).equipment[0].positionD;
 assert.equal(reflected[0],-f.equipment[0].positionD[0]);
 const pose=rig.initialPose('stand');pose.frames[0]=f;
 assert.doesNotThrow(()=>rig.checkPose(pose));
 delete band.anchorC;assert.throws(()=>rig.checkPose(pose));
});

test('neutral added articulation keeps legacy joint geometry and new fields preserve chain lengths',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand');f.euler=[.17,.2,-.1];f.upperYaw=.3;f.spineBend=.18;
 f.legAngles={left:{flexion:.5,abduction:.2,kneeFlexion:.6,axialRotation:.12}};
 f.armAngles={right:{flexion:.5,abduction:.4,elbowFlexion:.6,wristFlexion:0,forearmRotation:0}};
 const before=rig.joints(f),neutral=JSON.parse(JSON.stringify(f));
 for(const k of Object.keys(rig.ARTICULATION_LIMITS))neutral[k]=0;
 assert.equal(JSON.stringify(rig.joints(neutral)),JSON.stringify(before));
 const next=JSON.parse(JSON.stringify(f));next.lumbarBend=.22;next.lumbarSideBend=.16;next.thoracicBend=-.18;next.thoracicSideBend=.12;next.pelvisPitch=.2;next.pelvisYaw=.25;next.pelvisRoll=.14;
 const after=rig.joints(next),distance=(a,b)=>m.len(m.sub(a,b));
 for(const [a,b] of [['root','spineMid'],['spineMid','spineUpper'],['spineMid','neck'],['neck','head']])
   assert.ok(Math.abs(distance(before[a],before[b])-distance(after[a],after[b]))<1e-8,a+' '+b);
 for(const side of ['left','right']){
   for(const [a,b,length] of [['hip','knee',.45],['knee','feet',.43],['shoulder','elbow',.31],['elbow','hands',.28]])
     assert.ok(Math.abs(distance(after[a][side],after[b][side])-length)<1e-8,a+' '+side);
 }
 assert.ok(distance(after.hip.left,before.hip.left)>.01);assert.ok(distance(after.shoulder.right,before.shoulder.right)>.01);
});
test('pelvis and spine articulations are independent and mirror correctly',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),before=rig.joints(f);
 f.pelvisYaw=.3;f.pelvisRoll=.2;const after=rig.joints(f);
 assert.equal(JSON.stringify(after.shoulder),JSON.stringify(before.shoulder));
 assert.ok(m.len(m.sub(after.hip.left,before.hip.left))>.01);
 f.lumbarSideBend=.1;f.thoracicSideBend=.2;f.lumbarBend=.25;f.pelvisPitch=.1;
 const mirror=rig.mirrorFrame(f);assert.equal(mirror.pelvisYaw,-.3);assert.equal(mirror.pelvisRoll,-.2);assert.equal(mirror.lumbarSideBend,-.1);assert.equal(mirror.thoracicSideBend,-.2);assert.equal(mirror.lumbarBend,.25);assert.equal(mirror.pelvisPitch,.1);
 const pose=rig.initialPose('stand');pose.frames[0]=f;assert.doesNotThrow(()=>rig.checkPose(pose));f.lumbarBend=2;assert.throws(()=>rig.checkPose(pose));
});

test('optional articulation interpolates continuously to and from the legacy neutral pose',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 a.lumbarBend=.4;a.pelvisYaw=.2;a.thoracicSideBend=-.2;
 const forward=rig.Collision.interpolate(a,b,.5),reverse=rig.Collision.interpolate(b,a,.5);
 for(const k of ['lumbarBend','pelvisYaw','thoracicSideBend']){
   assert.ok(Math.abs(forward[k]-a[k]/2)<1e-9);
   assert.equal(forward[k],reverse[k]);
   assert.equal(rig.Collision.interpolate(a,b,1)[k],undefined);
 }
 b.lumbarBend=null;assert.equal(rig.Collision.interpolate(a,b,.5).lumbarBend,.2);
});
test('local articulation axes and inverse axes are consistent',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand');f.euler=[.3,-.2,.1];f.pelvisPitch=.2;f.pelvisYaw=.3;f.pelvisRoll=.1;f.lumbarBend=-.2;f.thoracicSideBend=.3;
 const axes=rig.articulationAxes(f),v=[.2,.4,-.5];
 assert.ok(m.len(m.sub(axes.legLocal(axes.pelvis(v)),v))<1e-8);
 assert.ok(m.len(m.sub(axes.armLocal(axes.upper(v)),v))<1e-8);
});

test('independent shoulder controls preserve clavicle and arm lengths and opposite side',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),before=rig.joints(f);
 f.leftShoulderElevation=.2;f.leftShoulderProtraction=.25;
 const after=rig.joints(f),d=(a,b)=>m.len(m.sub(a,b));
 assert.ok(d(after.shoulder.left,before.shoulder.left)>.03);
 assert.equal(JSON.stringify(after.shoulder.right),JSON.stringify(before.shoulder.right));
 assert.ok(Math.abs(d(after.shoulder.left,after.elbow.left)-.31)<1e-8);
 assert.ok(Math.abs(d(after.elbow.left,after.hands.left)-.28)<1e-8);
 assert.equal(JSON.stringify(after.root),JSON.stringify(before.root));
});
test('wrist deviation and ankle turning work in target mode without moving limb endpoints',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),before=rig.joints(f);
 f.leftWristDeviation=.25;f.rightAnkleYaw=.3;f.rightAnkleRoll=.25;
 const after=rig.joints(f),d=(a,b)=>m.len(m.sub(a,b));
 assert.equal(JSON.stringify(before.hands),JSON.stringify(after.hands));
 assert.equal(JSON.stringify(before.feet),JSON.stringify(after.feet));
 assert.ok(d(before.palms.left,after.palms.left)>.01);
 assert.ok(d(before.toes.right,after.toes.right)>.01);
 assert.ok(Math.abs(d(after.feet.right,after.toes.right)-.18)<1e-8);
 assert.ok(Math.abs(d(after.hands.left,after.palms.left)-.115)<1e-8);
 const axes=after.footAxes.right;assert.ok(Math.abs(m.len(axes.up)-1)<1e-8);
 const rollOnly=JSON.parse(JSON.stringify(f));rollOnly.rightAnkleRoll=0;
 const noRoll=rig.joints(rollOnly);
 assert.ok(d(noRoll.footAxes.right.up,axes.up)>.05);
 assert.equal(JSON.stringify(noRoll.toes.right),JSON.stringify(after.toes.right));
});
test('side controls mirror, copy, limit symmetric edits and interpolate to legacy values',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 f.leftShoulderElevation=.2;f.leftWristDeviation=.1;f.rightAnkleRoll=.2;
 const mirrored=rig.mirrorFrame(f);
 assert.equal(mirrored.rightShoulderElevation,.2);assert.equal(mirrored.rightWristDeviation,.1);assert.equal(mirrored.leftAnkleRoll,.2);
 assert.equal(mirrored.leftShoulderElevation,undefined);
 rig.copyLimb(f,'arm','left');assert.equal(f.rightShoulderElevation,.2);assert.equal(f.rightWristDeviation,.1);
 rig.editSideArticulation(f,'left','ShoulderElevation',1,true);
 assert.ok(Math.abs(f.leftShoulderElevation-rig.ARTICULATION_LIMITS.leftShoulderElevation)<1e-8);
 assert.equal(f.leftShoulderElevation,f.rightShoulderElevation);
 const neutral=rig.baseFrame('stand');
 assert.equal(rig.Collision.interpolate(f,neutral,.5).rightAnkleRoll,.1);
 const pose=rig.initialPose('stand');pose.frames[0]=f;assert.doesNotThrow(()=>rig.checkPose(pose));
 f.leftWristDeviation=2;assert.throws(()=>rig.checkPose(pose));
});

test('hand curl preserves the arm chain and finger lengths, moves equipment grasp independently',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),before=rig.joints(f);
 assert.equal(rig.handCurl(f,'left'),null);assert.equal(before.handDetails.left,undefined);
 rig.setHandCurl(f,'left',0);const open=rig.joints(f);
 rig.setHandCurl(f,'left',rig.HAND_PRESETS.grip);const grip=rig.joints(f);
 const d=(a,b)=>m.len(m.sub(a,b));
 for(const key of ['hands','elbow','shoulder','palms','thumbs'])assert.equal(JSON.stringify(before[key]),JSON.stringify(grip[key]));
 assert.equal(grip.handDetails.right,undefined);
 for(let i=0;i<4;i++)for(let k=1;k<4;k++){
   const a=open.handDetails.left.fingers[i],b=grip.handDetails.left.fingers[i];
   assert.ok(Math.abs(d(a[k],a[k-1])-d(b[k],b[k-1]))<1e-8);
 }
 assert.ok(d(rig.gripPoint(grip,'left'),before.hands.left)>.03);
 const prop={type:'band',anchorA:'leftHand',anchorB:'rightHand'};
 assert.equal(JSON.stringify(rig.bandSegments(prop,f,grip)[0][0]),JSON.stringify(rig.gripPoint(grip,'left')));
 assert.equal(JSON.stringify(rig.bandSegments(prop,f,grip)[0][1]),JSON.stringify(before.hands.right));
 assert.ok(rig.Collision.bodyProbes(grip).some(p=>p.id.startsWith('fingerleft')));
});
test('hand pose copies, mirrors, validates and interpolates without changing legacy saves',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),legacy=JSON.parse(JSON.stringify(f));
 rig.setHandCurl(f,'left',.8);
 assert.equal(rig.mirrorFrame(f).rightHandCurl,.8);
 rig.copyLimb(f,'arm','left');assert.equal(f.rightHandCurl,.8);
 assert.equal(rig.Collision.interpolate(f,legacy,.5).leftHandCurl,.4);
 rig.setHandCurl(f,'right',null,true);
 assert.equal(f.leftHandCurl,undefined);assert.equal(f.rightHandCurl,undefined);
 const pose=rig.initialPose('stand');pose.frames[0]=f;
 f.leftHandCurl=1.1;assert.throws(()=>rig.checkPose(pose));f.leftHandCurl=0;assert.doesNotThrow(()=>rig.checkPose(pose));
});

test('middle-joint support spheres constrain the root without moving the recorded locks',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('tabletop');
 for(const key of ['leftKnee','rightKnee','leftHand','rightHand'])rig.setPointLock(f,key,true);
 const next=JSON.parse(JSON.stringify(f)),locks=JSON.stringify(f.pointLocks);
 rig.moveRoot(next,[0,.02,.025]);const requested=[...next.root];
 const residual=x=>rig.supportChainConstraints(x,x.pointLocks).reduce((sum,c)=>{
   const d=m.len(m.sub(c.anchor,c.point));return sum+(d-Math.max(c.min,Math.min(c.max,d)))**2;
 },0);
 const before=residual(next);
 rig.projectSupportRoot(next,next.pointLocks,f);
 assert.ok(residual(next)<=before+1e-12);
 assert.ok(m.len(m.sub(next.root,requested))<=m.len(m.sub(requested,f.root))+.001001);
 assert.equal(JSON.stringify(next.pointLocks),locks);
 const locked=JSON.parse(JSON.stringify(f));rig.setPointLock(locked,'root',true);
 const candidate=JSON.parse(JSON.stringify(locked));rig.moveRoot(candidate,[0,.02,0]);
 const raw=JSON.stringify(candidate.root);rig.projectSupportRoot(candidate,candidate.pointLocks,locked);
 assert.equal(JSON.stringify(candidate.root),raw);
});
test('middle-only support fitting preserves rigid segment lengths and leaves unrelated limbs alone',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),before=rig.joints(f);
 rig.setPointLock(f,'leftKnee',true);
 const next=JSON.parse(JSON.stringify(f));rig.moveRoot(next,[.005,0,.006]);
 rig.adaptSupportPoints(next,next.pointLocks,{reference:f,source:f,projectRoot:true});
 const after=rig.joints(next),d=(a,b)=>m.len(m.sub(a,b));
 assert.ok(d(after.knee.left,next.pointLocks.leftKnee)<=.005);
 assert.ok(Math.abs(d(after.hip.left,after.knee.left)-.45)<1e-8);
 assert.ok(Math.abs(d(after.knee.left,after.feet.left)-.43)<1e-8);
 assert.equal(JSON.stringify(next.hands),JSON.stringify(f.hands));
 assert.equal(JSON.stringify(next.feet.right),JSON.stringify(f.feet.right));
});
test('swept multi-support edits never accept newly violated locks and keep root correction bounded',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('tabletop');
 for(const key of ['leftKnee','rightKnee','leftHand','rightHand'])rig.setPointLock(f,key,true);
 const next=JSON.parse(JSON.stringify(f));rig.moveRoot(next,[0,.012,.015]);
 const result=rig.Collision.transition(f,next,28,'root'),j=rig.joints(result.frame);
 for(const [,error] of rig.lockErrors(j,f.pointLocks))assert.ok(error<=.0050001);
 assert.equal(JSON.stringify(result.frame.pointLocks),JSON.stringify(f.pointLocks));
 assert.ok(m.len(m.sub(result.frame.root,f.root))<=2*m.len(m.sub(next.root,f.root))+.001001);
 // An unpinned pose must not be silently adjusted.
 const free=rig.baseFrame('stand'),raw=JSON.parse(JSON.stringify(free));rig.moveRoot(raw,[.01,0,0]);
 const requested=JSON.stringify(raw.root);rig.adaptSupportPoints(raw,{}, {source:free,projectRoot:true});
 assert.equal(JSON.stringify(raw.root),requested);
});

test('mixed IK/FK animation normalizes representable limbs without changing authored poses',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 assert.equal(rig.prepareAngleMode(b,'arm','left'),true);
 const before=JSON.stringify([a,b]),result=rig.Collision.previewTransition(a,b);
 assert.equal(result.ok,true,JSON.stringify(result.issue));
 assert.equal(result.samples.length,49);
 assert.equal(JSON.stringify([a,b]),before);
 for(const sample of result.samples)assert.ok(rig.limbError(rig.joints(a),rig.joints(sample),'arm','left')<1e-5);
 assert.equal(JSON.stringify(result.samples[0]),JSON.stringify(a));
 assert.equal(JSON.stringify(result.samples[48]),JSON.stringify(b));
});
test('revised support presets are finite, reachable and improve geometric baseline',()=>{
 const rig=loadRig(),m=rig.math;
 for(const name of ['tabletop','supine','kneel','plank']){
   const f=rig.baseFrame(name),j=rig.joints(f);
   assert.equal(j.clamped.length,0,name);
   assert.equal(rig.Collision.report(f).count,0,name);
   for(const side of ['left','right']){
     assert.ok(Math.abs(m.len(m.sub(j.hip[side],j.knee[side]))-.45)<1e-8);
     assert.ok(Math.abs(m.len(m.sub(j.knee[side],j.feet[side]))-.43)<1e-8);
   }
 }
 for(const name of ['tabletop','kneel'])for(const y of Object.values(rig.joints(rig.baseFrame(name)).knee))assert.ok(y[1]>=.09&&y[1]<.13);
 assert.ok(rig.PRESET_NAMES.length>=21);
 for(const name of rig.PRESET_NAMES){
   const p=rig.initialPose(name);assert.doesNotThrow(()=>rig.checkPose(p));
   const audit=rig.auditPreset(name);assert.equal(audit.frames.length,2);
 }
});

test('constant numeric coordinates are not perturbed by interpolation',()=>{
 const rig=loadRig(),f=rig.baseFrame('tabletop');
 for(const key of ['leftKnee','rightKnee','leftHand','rightHand'])rig.setPointLock(f,key,true);
 const b=JSON.parse(JSON.stringify(f));rig.moveRoot(b,[0,.012,.015]);
 const expected=JSON.stringify(f.pointLocks);
 for(let i=0;i<=48;i++)assert.equal(JSON.stringify(rig.Collision.interpolate(f,b,i/48).pointLocks),expected);
});
test('second preset pass has no flagged geometry or unreachable targets in either phase',()=>{
 const rig=loadRig();
 for(const name of ['catcow','chair','prone','rowband','boat','legs-wall']){
   const audit=rig.auditPreset(name);
   for(const frame of audit.frames){assert.equal(frame.overlaps.count,0,name);assert.equal(frame.clamped.length,0,name);}
   assert.equal(audit.transition,true,name);
 }
});

test('ball seat, hip hinge and single-leg balance presets pass both frame and transition audits',()=>{
 const rig=loadRig();
 for(const name of ['ballseat','hinge','balance']){
   const a=rig.auditPreset(name);
   assert.ok(a.frames.every(f=>f.overlaps.count===0&&f.clamped.length===0),name);
   assert.equal(a.transition,true,name);
 }
 const balance=rig.initialPose('balance'),a=rig.joints(balance.frames[0]),b=rig.joints(balance.frames[1]);
 assert.ok(b.feet.right[1]>a.feet.right[1]+.1);
 assert.equal(JSON.stringify(a.feet.left),JSON.stringify(b.feet.left));
});

test('folded and stacked templates preserve intended geometry without unreachable limb targets',()=>{
 const rig=loadRig(),m=rig.math;
 for(const name of ['crosslegged','child','side','lunge','sideplank']){
   const a=rig.auditPreset(name);
   assert.ok(a.frames.every(f=>f.clamped.length===0),name);
   for(const f of rig.initialPose(name).frames){
     const j=rig.joints(f);
     for(const side of ['left','right']){
       assert.ok(Math.abs(m.len(m.sub(j.hip[side],j.knee[side]))-.45)<1e-8);
       assert.ok(Math.abs(m.len(m.sub(j.knee[side],j.feet[side]))-.43)<1e-8);
     }
   }
 }
 const crossed=rig.joints(rig.baseFrame('crosslegged'));
 assert.ok(crossed.feet.left[0]>0&&crossed.feet.right[0]<0);
 assert.ok(crossed.feet.right[1]>crossed.feet.left[1]+.08);
 const child=rig.joints(rig.baseFrame('child'));
 assert.ok(child.head[1]<.25);
 assert.ok(child.knee.left[1]<.15);
 assert.ok(child.feet.left[2]<child.knee.left[2]-.2);
 const plank=rig.joints(rig.baseFrame('sideplank'));
 assert.ok(plank.hands.left[1]<.09&&plank.feet.left[1]<.09);
});

test('all 21 built-in templates pass both phases and checked transition geometry',()=>{
 const rig=loadRig();
 assert.equal(rig.PRESET_NAMES.length,21);
 for(const name of rig.PRESET_NAMES){
   const a=rig.auditPreset(name);
   for(const frame of a.frames){
     assert.equal(frame.overlaps.count,0,name+' overlap');
     assert.equal(frame.clamped.length,0,name+' reach');
   }
   assert.equal(a.transition,true,name+' transition');
 }
});

test('viewport resizing is bounded and camera view faces are consistent',()=>{
 const rig=loadRig();
 assert.equal(rig.workspaceShare(0,true),50);assert.equal(rig.workspaceShare(100,true),75);
 assert.equal(rig.workspaceShare(0,false),38);assert.equal(rig.workspaceShare(100,false),72);
 assert.equal(rig.workspaceShare(NaN,true),56);
 const pose=rig.initialPose(),before=JSON.stringify(pose.frames);
 for(const view of Object.keys(rig.CAMERA_VIEWS)){
   assert.equal(rig.setCameraView(pose.camera,view),true);
   assert.equal(rig.cameraViewName(pose.camera),view);
 }
 assert.equal(JSON.stringify(pose.frames),before);
});
test('skeleton uses the actual articulated joints and preserves pose data',()=>{
 const rig=loadRig(),f=rig.baseFrame('crosslegged');f.leftHandCurl=.7;
 const before=JSON.stringify(f),j=rig.joints(f),segments=rig.skeletonSegments(j);
 assert.ok(segments.some(([a,b])=>a===j.hip.left&&b===j.knee.left));
 assert.ok(segments.some(([a,b])=>a===j.knee.left&&b===j.feet.left));
 assert.ok(segments.some(([a,b])=>a===j.handDetails.left.fingers[0][0]&&b===j.handDetails.left.fingers[0][1]));
 assert.ok(segments.flat(2).every(Number.isFinite));
 assert.equal(JSON.stringify(f),before);
});

test('movement axes follow world, body and camera coordinates without stretching',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),c=rig.initialPose().camera;
 f.euler=[0,Math.PI/2,0];const sel=rig.bodyTarget('root');
 const world=rig.targetAxes(f,sel,c,'world'),body=rig.targetAxes(f,sel,c,'body'),camera=rig.targetAxes(f,sel,c,'camera');
 assert.ok(m.len(m.sub(body[0],world[0]))>1);
 for(const axes of [world,body,camera])for(const axis of axes)assert.ok(Math.abs(m.len(axis)-1)<1e-8);
 const delta=rig.pointDragDelta(f,sel,c,400,15,-7,'x','body');
 assert.ok(m.len(m.cross(delta,body[0]))<1e-8);
 const original=[...f.root];assert.equal(rig.moveTargetBy(f,sel,[.02,0,0]),true);
 assert.ok(Math.abs(f.root[0]-original[0]-.02)<1e-8);
});
test('body-part reset preserves unrelated settings, locks and equipment',()=>{
 const rig=loadRig(),baseline=rig.baseFrame('stand'),f=JSON.parse(JSON.stringify(baseline));
 f.headYaw=.3;f.leftHandCurl=.8;f.rightHandCurl=.4;f.leftShoulderElevation=.1;
 f.root[0]=.1;f.hands.left[0]-=.07;f.equipment.push({type:'band',anchorA:'leftHand',anchorB:'rightHand'});
 rig.setPointLock(f,'rightHand',true);
 const reset=rig.resetBodyPart(f,baseline,'leftArm');
 assert.equal(reset.leftHandCurl,undefined);assert.equal(reset.leftShoulderElevation,undefined);
 assert.equal(reset.rightHandCurl,.4);assert.equal(reset.headYaw,.3);
 for(const key of ['root','equipment','pointLocks','feet'])assert.equal(JSON.stringify(reset[key]),JSON.stringify(f[key]));
 assert.equal(JSON.stringify(reset.hands.right),JSON.stringify(f.hands.right));
 assert.ok(Math.abs(reset.hands.left[0]-baseline.hands.left[0]-.1)<1e-8);
 assert.equal(f.leftHandCurl,.8);
 assert.equal(rig.resetBodyPart(f,baseline,'all'),null);
});

test('secondary targets use bounded articulation and preserve segment lengths',()=>{
 const rig=loadRig(),m=rig.math;
 for(const key of ['head','neck','chest','waist','leftShoulder','rightHip','leftToe']){
   const f=rig.baseFrame('stand'),sel=rig.bodyTarget(key),start=rig.bodyPoint(rig.joints(f),key),delta=[.015,0,.02];
   assert.equal(sel.kind,'articulate');assert.equal(rig.moveTargetBy(f,sel,delta),true,key);
   const j=rig.joints(f);assert.ok(m.len(m.sub(m.add(start,delta),rig.bodyPoint(j,key)))<m.len(delta),key);
   for(const side of ['left','right'])for(const [a,b,l] of [['hip','knee',.45],['knee','feet',.43],['shoulder','elbow',.31],['elbow','hands',.28]])assert.ok(Math.abs(m.len(m.sub(j[a][side],j[b][side]))-l)<1e-8);
   const pose=rig.initialPose();pose.frames[0]=f;assert.doesNotThrow(()=>rig.checkPose(pose));
 }
});
test('direction probes do not mutate the frame and respect locks',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),c=rig.initialPose().camera;
 rig.setPointLock(f,'root',true);const before=JSON.stringify(f),hints=rig.probeTargetDirections(f,rig.bodyTarget('root'),c,'world');
 assert.equal(hints.length,6);assert.ok(hints.every(h=>!h.allowed));assert.equal(JSON.stringify(f),before);
 const free=rig.baseFrame('stand'),raw=JSON.stringify(free);
 assert.ok(rig.probeTargetDirections(free,rig.bodyTarget('leftHand'),c,'world').some(h=>h.allowed));assert.equal(JSON.stringify(free),raw);
});

test('natural contact reporting distinguishes near contact and penetration',()=>{
 const rig=loadRig(),a={id:'hand-left',a:[0,0,0],b:[0,.1,0],r:.04},b={id:'thigh-right',a:[.107,0,0],b:[.107,.1,0],r:.08};
 assert.equal(rig.Collision.naturalContacts([a,b]).length,1);
 assert.equal(rig.Collision.selfOverlaps([a,b]).size,0);
 b.a[0]=b.b[0]=.06;
 assert.equal(rig.Collision.naturalContacts([a,b]).length,0);
 assert.equal(rig.Collision.selfOverlaps([a,b]).size,1);
});
test('optional wrist orientation works in target mode and preserves wrist position',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math,before=rig.joints(f);
 f.leftWristPitch=.5;f.leftPalmTurn=.7;
 const after=rig.joints(f);assert.equal(JSON.stringify(after.hands),JSON.stringify(before.hands));
 assert.ok(m.len(m.sub(after.palmNormals.left,before.palmNormals.left))>.1);
 assert.ok(Math.abs(m.len(after.palmNormals.left)-1)<1e-8);
 assert.equal(rig.mirrorFrame(f).rightPalmTurn,.7);
 const pose=rig.initialPose();pose.frames[0]=f;assert.doesNotThrow(()=>rig.checkPose(pose));
 f.leftWristPitch=2;assert.throws(()=>rig.checkPose(pose));
});
test('alignment requests preserve the source on success or failure',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),before=JSON.stringify(f);
 const result=rig.fitSupport(f,'floor',['leftFoot','rightFoot'],null,true,true);
 assert.equal(JSON.stringify(f),before);
 if(result.ok){for(const key of ['leftFoot','rightFoot']){
   const j=rig.joints(result.frame),surface=rig.contactSurface(result.frame,key,'floor');
   assert.ok(rig.supportAlignment(j,key,surface)>=Math.cos(Math.PI/12));assert.ok(result.frame.pointLocks[key]);
 }}else assert.ok(result.issue);
});

test('standing soles and tabletop palms align atomically with the floor',()=>{
 const rig=loadRig();
 for(const [name,keys] of [['stand',['leftFoot','rightFoot']],['tabletop',['leftHand','rightHand']]]){
   const f=rig.baseFrame(name),before=JSON.stringify(f);
   const result=rig.fitSupport(f,'floor',keys,null,true,true);
   assert.equal(result.ok,true,name+' '+result.issue);
   assert.equal(JSON.stringify(f),before);
   const j=rig.joints(result.frame);
   for(const key of keys){
     const surface=rig.contactSurface(result.frame,key,'floor');
     assert.ok(rig.supportAlignment(j,key,surface)>=Math.cos(Math.PI/12),key);
     assert.ok(Math.abs(rig.contactDistance(j,key,surface)-.002)<=.005,key);
     assert.ok(result.frame.pointLocks[key]);
   }
   assert.equal(rig.Collision.report(result.frame).count,0,name);
 }
});


test('equipment-relative support coordinates follow stable identity, translation and yaw',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand');
 f.equipment=[{type:'wall',id:'eq-1',position:[.2,1,-.6],yaw:.35},{type:'wall',id:'eq-2'}];
 const before=JSON.stringify(f),binding=rig.captureEquipmentSupport(f,'rightHand',0);
 const near=(a,b)=>assert.ok(m.len(m.sub(a,b))<1e-8);
 assert.ok(binding);near(rig.resolveEquipmentSupport(f,binding),rig.joints(f).hands.right);
 assert.equal(JSON.stringify(f),before);
 f.equipment.reverse();f.equipment[1].position=[.4,1.1,-.5];f.equipment[1].yaw=.35+Math.PI/2;
 near(rig.resolveEquipmentSupport(f,binding),m.add(f.equipment[1].position,m.rotate(binding.local,[0,f.equipment[1].yaw,0])));
 const p=rig.resolveEquipmentSupport(f,binding),mirror=rig.mirrorFrame(f);
 near(rig.resolveEquipmentSupport(mirror,rig.mirrorEquipmentSupport(binding)),[-p[0],p[1],p[2]]);
 f.equipment.push({type:'wall',id:'eq-1'});assert.equal(rig.resolveEquipmentSupport(f,binding),null);
 f.equipment.pop();f.equipment.pop();assert.equal(rig.resolveEquipmentSupport(f,binding),null);
 assert.equal(rig.captureEquipmentSupport(rig.baseFrame('chair'),'root',0),null);
 assert.equal(rig.resolveEquipmentSupport(f,{...binding,local:[NaN,0,0]}),null);
});


test('relative support follows a swept equipment move and rejects unreachable movement atomically',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),j=rig.joints(f);
 const z=Math.min(...rig.contactEnvelope(j,'rightHand').map(x=>x.p[2]-x.r))-.027;
 f.equipment=[{type:'wall',id:'eq-1',position:[j.hands.right[0],j.hands.right[1],z],yaw:0}];
 const fit=rig.fitSupport(f,'wall',['rightHand'],0,true,false,true);
 assert.equal(fit.ok,true,fit.issue);
 const base=fit.frame,before=JSON.stringify(base),p=base.equipment[0].position;
 assert.equal(base.pointLocks?.rightHand,undefined);
 assert.equal(base.equipmentPointLocks.rightHand.equipmentId,'eq-1');
 const moved=rig.editProps([base],[{frame:0,index:0}],[p[0]+.015,p[1],p[2]],0);
 assert.equal(moved.ok,true,moved.issue);
 const g=moved.frames[0],locks=rig.effectiveLocks(g);
 assert.ok(m.len(m.sub(rig.joints(g).hands.right,locks.rightHand))<=.005);
 assert.equal(JSON.stringify(base),before);
 assert.equal(rig.editProps([base],[{frame:0,index:0}],[p[0]+2,p[1],p[2]],0).ok,false);
 assert.equal(JSON.stringify(base),before);
 assert.equal(rig.Collision.previewTransition(base,g).ok,true);
 const pose=rig.initialPose('stand');pose.frames=[g];rig.checkPose(pose);
 assert.equal(rig.validEquipmentPointLocks(rig.mirrorFrame(g)),true);
 const orphan=JSON.parse(JSON.stringify(g));orphan.equipment=[];
 assert.equal(rig.validEquipmentPointLocks(orphan),false);
 pose.frames=[orphan];assert.throws(()=>rig.checkPose(pose));
 const conflict=JSON.parse(JSON.stringify(g));conflict.pointLocks={rightHand:[0,0,0]};
 assert.equal(rig.validEquipmentPointLocks(conflict),false);
 rig.setPointLock(g,'rightHand',false);assert.equal(g.equipmentPointLocks,undefined);
});


test('support mode selectors cover all lock keys and preserve relative identity across list reordering',()=>{
 const rig=loadRig(),html=fs.readFileSync(path.join(root,'index.html'),'utf8'),source=fs.readFileSync(path.join(root,'studio.js'),'utf8');
 const keys=[...html.matchAll(/<select data-lock-point="([^"]+)"/g)].map(x=>x[1]);
 assert.equal(JSON.stringify(keys),JSON.stringify(rig.LOCK_KEYS));
 assert.ok(html.includes('id="contactLockMode"'));assert.ok(!source.includes("$('contactLock')"));
 const mk=key=>({dataset:{lockPoint:key},options:[],value:'',appendChild(o){this.options.push(o)},replaceChildren(){this.options=[]}});
 const selects=keys.map(mk),pose={frames:[rig.baseFrame('stand')]},frameIndex=0,current=()=>pose.frames[0];
 current().equipment=[{type:'wall'},{type:'chair',id:'eq-1'}];
 const bodyLinkStatus={textContent:''};
 const environment={$:id=>{assert.equal(id,'bodyLinkStatus');return bodyLinkStatus;},window:{rigText:s=>s},targetLabels:Object.fromEntries(rig.LINK_KEYS.map(key=>[key,key])),MozduljRig:rig,document:{createElement:()=>({}),querySelectorAll:()=>selects},POSITIONABLE:Object.keys(rig.PROP_POSITIONS),current,pose,frameIndex,
 clone:o=>JSON.parse(JSON.stringify(o)),equipmentLabel:(p,i)=>p.type+' '+(i+1),syncContactSurfaces:()=>{},closeGesture:()=>{},stopPreview:()=>{},tracked:f=>f(),sync:()=>{},msg:()=>{}};
 const code=source.slice(source.indexOf('function lockOption('),source.indexOf('function syncContactSurfaces('));
 const fns=new Function(...Object.keys(environment),code+';return {syncLocks,changePointLock};')(...Object.values(environment));
 fns.syncLocks();assert.equal(bodyLinkStatus.textContent,'Nincs testpontkapcsolat.');assert.equal(selects[0].options.length,4);assert.equal(selects[0].value,'free');
 const hand=selects.find(s=>s.dataset.lockPoint==='rightHand');
 hand.value='equipment:0';fns.changePointLock(hand);
 assert.equal(current().equipment[0].id,'eq-2');assert.equal(current().equipmentPointLocks.rightHand.equipmentId,'eq-2');
 fns.syncLocks();assert.equal(hand.value,'equipment:0');
 current().equipment.reverse();fns.syncLocks();assert.equal(hand.value,'equipment:1');
 hand.value='world';fns.changePointLock(hand);assert.ok(current().pointLocks.rightHand);assert.equal(current().equipmentPointLocks,undefined);
 hand.value='free';fns.changePointLock(hand);assert.equal(current().pointLocks,undefined);
 const localization=fs.readFileSync(path.join(root,'localization.js'),'utf8');
 for(const key of ['Szabad','Térben rögzített ●','Eszközhöz rögzített ✚','Rögzítés sikeres illesztés után'])
   assert.ok(localization.includes(JSON.stringify(key)+':'));
});


test('creation templates place palms, soles, chair seat and wall supports within contact tolerance',()=>{
 const rig=loadRig();
 const cases=[
   ['stand','floor',['leftFoot','rightFoot'],null,true],
   ['tabletop','floor',['leftHand','rightHand'],null,true],
   ['catcow','floor',['leftHand','rightHand'],null,true],
   ['plank','floor',['leftHand','rightHand'],null,true],
   ['chair','seat',['root'],0,false],
   ['chair','floor',['leftFoot','rightFoot'],null,true],
   ['ballseat','floor',['leftFoot','rightFoot'],null,true],
   ['legs-wall','wall',['leftFoot','rightFoot'],1,false]
 ];
 for(const [name,type,keys,index,align] of cases){
   const frame=rig.baseFrame(name),j=rig.joints(frame);
   for(const key of keys){
     const surface=rig.contactSurface(frame,key,type,index);
     assert.ok(Math.abs(rig.contactDistance(j,key,surface)-.002)<=.005,name+' '+key);
     assert.ok(rig.contactInside(j,key,surface),name+' '+key+' outside');
     if(align)assert.ok(rig.supportAlignment(j,key,surface)>=Math.cos(Math.PI/12),name+' '+key+' orientation');
   }
 }
 const f=rig.baseFrame('chair');f.equipment[0].id='chair-test';
 const before=JSON.stringify(f),fit=rig.fitSupport(f,'seat',['root'],0,true,false,true);
 assert.equal(fit.ok,true,fit.issue);assert.equal(JSON.stringify(f),before);
 assert.equal(fit.frame.equipmentPointLocks.root.equipmentId,'chair-test');
 const crossed=rig.joints(rig.baseFrame('crosslegged'));
 assert.ok(crossed.root[1]<=.28&&crossed.feet.left[1]>=.067);
});


test('phase references are bounded, non-wrapping and do not change pose data',()=>{
 const rig=loadRig(),frames=rig.initialPose('stand').frames;
 frames.push(JSON.parse(JSON.stringify(frames[1])));
 const before=JSON.stringify(frames);
 assert.equal(rig.phaseReferences(frames,0,'previous').length,0);
 assert.equal(rig.phaseReferences(frames,2,'next').length,0);
 assert.equal(rig.phaseReferences(frames,1,'off').length,0);
 const refs=rig.phaseReferences(frames,1,'both');
 assert.equal(refs.length,2);assert.equal(refs[0].frame,frames[0]);assert.equal(refs[1].frame,frames[2]);
 assert.equal(JSON.stringify(frames),before);
 const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
 for(const id of ['phaseOverlay','phaseCopySource','phaseCopyGroup','copyPhaseBodyPart'])assert.ok(html.includes('id="'+id+'"'));
});
test('phase body copy is transactional, preserves metadata and rejects locked changes',()=>{
 const rig=loadRig(),frame=rig.baseFrame('stand'),source=JSON.parse(JSON.stringify(frame));
 frame.name='Destination';frame.holdSeconds=5;source.name='Source';source.holdSeconds=2;source.headYaw=.2;
 const before=JSON.stringify(frame),original=JSON.stringify(source);
 const copy=rig.copyPhaseBodyPart(frame,source,'head');assert.equal(copy.ok,true);
 assert.equal(copy.frame.headYaw,.2);assert.equal(copy.frame.name,'Destination');assert.equal(copy.frame.holdSeconds,5);
 assert.equal(JSON.stringify(copy.frame.equipment),JSON.stringify(frame.equipment));
 assert.equal(JSON.stringify(frame),before);assert.equal(JSON.stringify(source),original);
 rig.setPointLock(frame,'root',true);source.root[1]-=.1;
 const locked=JSON.stringify(frame);
 assert.equal(rig.copyPhaseBodyPart(frame,source,'root').ok,false);
 assert.equal(JSON.stringify(frame),locked);
 assert.equal(rig.copyPhaseBodyPart(frame,source,'unknown').ok,false);
 const a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));b.root[0]=.2;b.hands.left[0]+=.2;b.poles.elbowLeft[0]+=.2;
 const limb=rig.copyPhaseBodyPart(a,b,'leftArm');assert.equal(limb.ok,true,limb.issue);
 assert.ok(rig.math.len(rig.math.sub(rig.joints(limb.frame).hands.left,rig.joints(a).hands.left))<1e-7);
 assert.equal(JSON.stringify(limb.frame.hands.right),JSON.stringify(a.hands.right));
});


test('timed workout guides select authored sides, sustain holds and never infer missing sides',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),left=JSON.parse(JSON.stringify(a)),right=JSON.parse(JSON.stringify(a));
 left.direction='left';left.phaseType='hold';left.headYaw=.2;
 right.direction='right';right.phaseType='hold';right.headYaw=-.2;
 const frames=[a,left,right],before=JSON.stringify(frames);
 for(const [code,yaw] of [['left',.2],['right',-.2]]){
   const plan=rig.workoutGuidePlan(frames,code);assert.equal(plan.kind,'hold');
   assert.equal(rig.workoutGuideAt(plan,0).headYaw,0);
   assert.equal(rig.workoutGuideAt(plan,30000).headYaw,yaw);
 }
 const missing=rig.workoutGuidePlan([a,left],'right');
 assert.equal(missing.kind,'unassigned');assert.equal(missing.frame,a);
 assert.equal(rig.workoutGuidePlan([left],'right').frame,null);
 assert.equal(rig.workoutGuidePlan([left,right],'all').kind,'unassigned');
 const loop=rig.workoutGuidePlan([a,JSON.parse(JSON.stringify(a))],'all');
 assert.equal(loop.kind,'loop');assert.ok(rig.workoutGuideAt(loop,10000));
 assert.equal(JSON.stringify(frames),before);
});
test('workout guide refuses unchecked transitions and provides a still fallback',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 b.equipment=[{type:'wall',id:'eq-1'}];b.direction='left';
 const guide=rig.workoutGuidePlan([a,b],'left');
 assert.equal(guide.kind,'blocked');assert.equal(guide.frame,b);
 const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
 assert.ok(html.includes('id="workoutGuideStatus"'));
});

test('repetition guide loops authored holds instead of stopping at the first hold',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 b.direction='left';b.phaseType='hold';b.headYaw=.2;
 const before=JSON.stringify([a,b]);
 const timed=rig.workoutGuidePlan([a,b],'left');
 const reps=rig.workoutGuidePlan([a,b],'left',true);
 assert.equal(timed.kind,'hold');assert.equal(reps.kind,'loop');
 assert.equal(rig.workoutGuideAt(reps,reps.plan.duration).headYaw,0);
 assert.equal(rig.workoutGuideAt(timed,30000).headYaw,.2);
 assert.equal(rig.workoutGuidePlan([a,b],'right',true).kind,'unassigned');
 assert.equal(JSON.stringify([a,b]),before);
});


test('bend plane guard identifies abrupt elbow and knee reversal, not unchanged joints',()=>{
 const rig=loadRig(),m=rig.math,j=rig.joints(rig.baseFrame('stand'));
 assert.equal(rig.bendFlip(j,j),null);
 for(const [a,b,c,s,key] of [['shoulder','elbow','hands','left','leftElbow'],['hip','knee','feet','right','rightKnee']]){
   const next=JSON.parse(JSON.stringify(j)),axis=m.norm(m.sub(j[c][s],j[a][s])),v=m.sub(j[b][s],j[a][s]);
   const radial=m.sub(v,m.mul(axis,m.dot(v,axis)));
   assert.ok(m.len(radial)>.025,key);
   next[b][s]=m.sub(j[b][s],m.mul(radial,2));
   assert.equal(rig.bendFlip(j,next),key);
   assert.ok(rig.issueGeometry(rig.baseFrame('stand'),j,'bend:'+key).length);
 }
});
test('supine IK floor correction preserves continuous bend in checked preview and swept edit',()=>{
 const rig=loadRig(),a=rig.baseFrame('supine'),b=JSON.parse(JSON.stringify(a));
 a.hands.left[1]+=.195;b.hands.left[1]+=.200;
 assert.equal(rig.bendFlip(rig.joints(a),rig.joints(b)),null);
 const before=JSON.stringify(a),after=JSON.stringify(b);
 assert.equal(rig.Collision.previewTransition(a,b).ok,true);
 assert.equal(rig.Collision.transition(a,b).limited,false);
 assert.equal(JSON.stringify(a),before);assert.equal(JSON.stringify(b),after);
});

test('floor hinge projection preserves bones, endpoint and reflection symmetry',()=>{
 const rig=loadRig(),m=rig.math,dist=(a,b)=>m.len(m.sub(a,b)),mirror=p=>[-p[0],p[1],p[2]];
 for(const target of [[0,.12,.44],[.44,.12,0]]){
  const a=[0,.12,0],pole=[0,-.5,0],s=rig.solve2(a,target,.31,.28,pole);
  const fixed=rig.fitHingeFloor(a,s,'left');
  assert.ok(fixed.mid[1]>=.075-1e-8);
  assert.ok(Math.abs(dist(a,fixed.mid)-.31)<1e-8);
  assert.ok(Math.abs(dist(fixed.mid,fixed.end)-.28)<1e-8);
  assert.deepEqual(fixed.end,s.end);
  const reflected=rig.fitHingeFloor(mirror(a),{...s,mid:mirror(s.mid),end:mirror(s.end)},'right');
  assert.ok(dist(reflected.mid,mirror(fixed.mid))<1e-8);
  assert.equal(rig.fitHingeFloor(a,fixed,'left'),fixed);
 }
});
test('impossible floor correction preserves lengths without pretending the floor is reachable',()=>{
 const rig=loadRig(),m=rig.math,a=[0,-.5,0],s=rig.solve2(a,[0,-.5,.44],.31,.28,[0,-1,0]);
 const fixed=rig.fitHingeFloor(a,s,'left');
 assert.ok(fixed.mid[1]<.075);
 assert.ok(Math.abs(m.len(m.sub(a,fixed.mid))-.31)<1e-8);
 assert.ok(Math.abs(m.len(m.sub(fixed.mid,fixed.end))-.28)<1e-8);
 assert.deepEqual(fixed.end,s.end);
});

test('body endpoint links follow a leader without stretching and survive mirroring',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 assert.ok(rig.setBodyLink(f,'rightHand','leftHand'));
 const j=rig.joints(f),b=JSON.parse(JSON.stringify(f));b.hands.left[2]+=.025;
 const moved=rig.Collision.transition(f,b);
 assert.equal(moved.limited,false);
 const end=rig.joints(moved.frame);
 assert.ok((rig.bodyLinkErrors(end,moved.frame).get('rightHand')||0)<.005);
 assert.ok(m.len(m.sub(end.hands.right,j.hands.right))>.01);
 assert.ok(Math.abs(m.len(m.sub(end.elbow.right,end.hands.right))-.28)<1e-8);
 const mirror=rig.mirrorFrame(moved.frame);
 assert.ok(rig.validBodyLinks(mirror));assert.equal(mirror.bodyPointLinks.leftHand.leader,'rightHand');
 assert.ok((rig.bodyLinkErrors(rig.joints(mirror),mirror).get('leftHand')||0)<.005);
 assert.ok(rig.Collision.previewTransition(f,moved.frame).ok);
 assert.ok(rig.removeBodyLink(mirror,'leftHand'));assert.equal(mirror.bodyPointLinks,undefined);
});
test('body links reject cycles, conflicting locks and independent follower edits',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 rig.setPointLock(f,'rightHand',true);assert.equal(rig.setBodyLink(f,'rightHand','leftHand'),false);
 rig.setPointLock(f,'rightHand',false);assert.ok(rig.setBodyLink(f,'rightHand','leftHand'));
 const before=JSON.stringify(f);
 assert.equal(rig.setBodyLink(f,'leftHand','rightHand'),false);
 assert.equal(rig.setPointLock(f,'rightHand',true),false);
 assert.equal(JSON.stringify(f),before);
 const b=JSON.parse(before);b.hands.right[2]+=.1;
 assert.ok(rig.Collision.transition(f,b).limited);
 const pose=rig.initialPose('stand');pose.frames[0]=b;b.bodyPointLinks.leftHand={leader:'rightHand',offset:[0,0,0]};
 assert.throws(()=>rig.checkPose(pose));
});

test('independent neck articulation moves head and neck without disturbing shoulders or hips',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math,j=rig.joints(f);
 const next={...f,cervicalPitch:.17,cervicalYaw:.2,cervicalRoll:.12},q=rig.joints(next);
 for(const key of ['root','hip','shoulder','elbow','hands','feet'])assert.deepEqual(q[key],j[key]);
 assert.ok(m.len(m.sub(q.neck,j.neck))>.005);
 assert.ok(m.len(m.sub(q.head,j.head))>.02);
 assert.ok(Math.abs(m.len(m.sub(q.head,q.neck))-m.len(m.sub(j.head,j.neck)))<1e-8);
 const mirror=rig.mirrorFrame(next);assert.equal(mirror.cervicalYaw,-.2);assert.equal(mirror.cervicalRoll,-.12);assert.equal(mirror.cervicalPitch,.17);
 const restored=rig.resetBodyPart(next,f,'head');assert.equal(restored.cervicalPitch,undefined);
 assert.equal(rig.validArticulation({...f,cervicalYaw:2}),false);
});
test('segmental spine twists preserve hip and clavicle distances and mirror direction',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math,j=rig.joints(f);
 const next={...f,lumbarYaw:.1,thoracicYaw:.3},q=rig.joints(next);
 assert.deepEqual(q.hip,j.hip);
 assert.ok(m.len(m.sub(q.shoulder.left,j.shoulder.left))>.02);
 assert.ok(Math.abs(m.len(m.sub(q.shoulder.left,q.shoulder.right))-m.len(m.sub(j.shoulder.left,j.shoulder.right)))<1e-8);
 const mirror=rig.mirrorFrame(next);assert.equal(mirror.lumbarYaw,-.1);assert.equal(mirror.thoracicYaw,-.3);
 const restored=rig.resetBodyPart(next,f,'trunk');assert.equal(restored.thoracicYaw,undefined);
 const half=rig.Collision.interpolate(next,f,.5);assert.equal(half.thoracicYaw,.15);
 assert.equal(rig.validArticulation({...f,lumbarYaw:.5}),false);
});

test('scapular articulation moves its own shoulder while preserving bones and opposite side',()=>{
 const rig=loadRig(),m=rig.math,f=rig.baseFrame('stand'),j=rig.joints(f);
 const next={...f,leftScapulaElevation:.12,leftScapulaProtraction:.2,leftScapulaRotation:.3},q=rig.joints(next);
 const d=(a,b)=>m.len(m.sub(a,b));
 assert.ok(d(q.scapula.left.inferior,j.scapula.left.inferior)>.01);
 assert.ok(d(q.shoulder.left,j.shoulder.left)>.01);
 for(const key of ['root','hip','head','neck'])assert.deepEqual(q[key],j[key]);
 assert.deepEqual(q.scapula.right,j.scapula.right);assert.deepEqual(q.shoulder.right,j.shoulder.right);
 assert.ok(Math.abs(d(q.shoulder.left,q.elbow.left)-.31)<1e-8);
 assert.ok(Math.abs(d(q.elbow.left,q.hands.left)-.28)<1e-8);
 for(const [a,b] of [['superior','medial'],['medial','inferior'],['inferior','superior']])assert.ok(Math.abs(d(q.scapula.left[a],q.scapula.left[b])-d(j.scapula.left[a],j.scapula.left[b]))<1e-8);
 const mirror=rig.mirrorFrame(next),r=rig.joints(mirror);
 assert.equal(mirror.rightScapulaRotation,.3);
 assert.ok(d(r.scapula.right.inferior,[-q.scapula.left.inferior[0],q.scapula.left.inferior[1],q.scapula.left.inferior[2]])<1e-8);
 assert.equal(rig.resetBodyPart(next,f,'leftArm').leftScapulaElevation,undefined);
 assert.equal(rig.Collision.interpolate(next,f,.5).leftScapulaProtraction,.1);
 assert.equal(rig.validArticulation({...f,leftScapulaRotation:2}),false);
 assert.deepEqual(rig.bodyPoint(q,'leftScapula'),q.scapula.left.inferior);
});
test('scapula control supports symmetry and direct body-point editing',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 assert.ok(rig.editSideArticulation(f,'left','ScapulaProtraction',.1,true));
 assert.equal(f.rightScapulaProtraction,.1);
 const sel=rig.bodyTarget('leftScapula');assert.ok(sel);
 assert.ok(rig.moveTargetBy(f,sel,[0,.01,0]));
 assert.ok(rig.validArticulation(f));
});

test('coordinated girdle editing gently recruits scapula and thoracic spine without moving pelvis',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),before=rig.joints(f),m=rig.math;
 assert.ok(rig.editSideArticulation(f,'left','ShoulderProtraction',.2,false,true));
 assert.ok(f.leftScapulaProtraction>0);assert.equal(f.rightScapulaProtraction,undefined);
 assert.ok(f.thoracicBend>0);
 const after=rig.joints(f);
 assert.deepEqual(after.hip,before.hip);assert.deepEqual(after.root,before.root);
 assert.ok(Math.abs(m.len(m.sub(after.shoulder.left,after.elbow.left))-.31)<1e-8);
 assert.ok(rig.editSideArticulation(f,'left','ShoulderProtraction',0,false,true));
 assert.ok(Math.abs(f.thoracicBend)<1e-8);assert.ok(Math.abs(f.leftScapulaProtraction)<1e-8);
 const separate=rig.baseFrame('stand');
 rig.editSideArticulation(separate,'left','ScapulaProtraction',.2,false,false);
 assert.equal(separate.thoracicBend,undefined);
 const symmetric=rig.baseFrame('stand');
 rig.editSideArticulation(symmetric,'left','ScapulaRotation',.2,true,true);
 assert.equal(symmetric.rightScapulaRotation,.2);assert.ok(symmetric.thoracicBend<0);
 assert.ok(rig.validArticulation(symmetric));
});

test('toe articulation preserves ankle, forefoot and toe segment lengths and mirror symmetry',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),j=rig.joints(f),m=rig.math,d=(a,b)=>m.len(m.sub(a,b));
 const next={...f,leftToeBend:.4},q=rig.joints(next);
 assert.deepEqual(q.feet,j.feet);assert.deepEqual(q.knee,j.knee);
 assert.deepEqual(q.toeJoints,j.toeJoints);assert.deepEqual(q.toes.right,j.toes.right);
 assert.ok(q.toeArticulated.left);assert.ok(d(q.toes.left,j.toes.left)>.01);
 assert.ok(Math.abs(d(q.feet.left,q.toeJoints.left)-.12)<1e-8);
 assert.ok(Math.abs(d(q.toeJoints.left,q.toes.left)-.06)<1e-8);
 assert.ok(m.dot(m.sub(q.toes.left,j.toes.left),q.footAxes.left.up)>0);
 const mirrored=rig.mirrorFrame(next),r=rig.joints(mirrored);
 assert.equal(mirrored.rightToeBend,.4);assert.ok(d(r.toes.right,[-q.toes.left[0],q.toes.left[1],q.toes.left[2]])<1e-8);
 assert.equal(rig.Collision.interpolate(next,f,.5).leftToeBend,.2);
 assert.equal(rig.resetBodyPart(next,f,'leftLeg').leftToeBend,undefined);
 assert.equal(rig.validArticulation({...f,leftToeBend:1}),false);
 assert.ok(rig.Collision.bodyProbes(q).some(p=>p.id.startsWith('toeleft')));
 assert.equal(rig.contactEnvelope(q,'leftFoot').length,3);
 assert.equal(rig.contactEnvelope(j,'leftFoot').length,2);
 assert.ok(rig.skeletonSegments(q).some(([a,b])=>a===q.toeJoints.left&&b===q.toes.left));
});
test('toe edits participate in swept collision checks and can be symmetric',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(f));
 assert.ok(rig.editSideArticulation(b,'left','ToeBend',.3,true));
 assert.equal(b.rightToeBend,.3);
 const r=rig.Collision.transition(f,b);
 assert.equal(r.limited,false);
 assert.ok(rig.Collision.previewTransition(f,r.frame).ok);
 const m=rig.math;assert.ok(m.len(m.sub(rig.joints(r.frame).toes.left,rig.joints(f).toes.left))>.01);
});

test('surface links capture grip placement and follow rotation without stretching',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand'),m=rig.math;
 rig.setHandCurl(f,'left',.78);rig.setHandCurl(f,'right',.78);
 assert.ok(rig.setBodyLink(f,'rightHand','leftHand','surface'));
 assert.ok((rig.bodyLinkErrors(rig.joints(f),f).get('rightHand')||0)<1e-8);
 const b=JSON.parse(JSON.stringify(f));b.leftWristPitch=.025;
 const r=rig.Collision.transition(f,b);
 assert.equal(r.limited,false);
 assert.ok((rig.bodyLinkErrors(rig.joints(r.frame),r.frame).get('rightHand')||0)<.005);
 assert.ok(Math.abs(m.len(m.sub(rig.joints(r.frame).hands.right,rig.joints(r.frame).elbow.right))-.28)<1e-8);
 const mirror=rig.mirrorFrame(r.frame);
 assert.ok((rig.bodyLinkErrors(rig.joints(mirror),mirror).get('leftHand')||0)<.005);
});
test('scapular rhythm is optional and reversible with explicit stored angles',()=>{
 const rig=loadRig(),f=rig.baseFrame('stand');
 f.armAngles.left={flexion:0,abduction:0,elbowFlexion:.1};
 rig.editLimbAngle(f,'arm','left','flexion',1.4,false,true);
 assert.ok(f.leftScapulaRotation>.1);assert.equal(f.rightScapulaRotation,undefined);
 rig.editLimbAngle(f,'arm','left','flexion',0,false,true);
 assert.ok(Math.abs(f.leftScapulaRotation)<1e-8);
 rig.editLimbAngle(f,'arm','left','flexion',1.4,false,false);
 assert.ok(Math.abs(f.leftScapulaRotation)<1e-8);
});

test('body map exposes every selectable point and every rotation ring maps to an existing control',()=>{
 const rig=loadRig(),html=fs.readFileSync(path.join(root,'index.html'),'utf8'),source=fs.readFileSync(path.join(root,'studio.js'),'utf8');
 const keys=[...html.matchAll(/data-body-map="([^"]+)"/g)].map(m=>m[1]).sort();
 assert.deepEqual(keys,rig.BODY_TARGETS.map(p=>p.key).sort());
 const chunk=source.slice(source.indexOf('const ringFields='),source.indexOf('let ringDrag='));
 const {ringControls,ringSamples,nearestRingTangent}=new Function('MozduljRig',chunk+';return {ringControls,ringSamples,nearestRingTangent};')(rig);
 for(const key of keys){
   const controls=ringControls(key);assert.ok(controls.length,key);
   for(const [id,axis] of controls){assert.ok(html.includes('id="'+id+'"'),id);assert.ok(axis>=0&&axis<=2);}
 }
 const points=ringSamples([0,0,0],[[1,0,0],[0,1,0],[0,0,1]],2,.2);
 assert.equal(points.length,49);
 for(const point of points)assert.ok(Math.abs(rig.math.len(point)-.2)<1e-8);
 const p=points.map(v=>[v[0]*100,v[1]*100]);
 const tangent=nearestRingTangent(p,20,0);assert.ok(tangent&&tangent.d<.01);
 assert.ok(tangent.pxPerRadian>19&&tangent.pxPerRadian<21);
});
