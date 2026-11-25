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
test('preview is read-only, checks every displayed sample and rejects representation changes',()=>{
 const rig=loadRig(),a=rig.baseFrame('stand'),b=JSON.parse(JSON.stringify(a));
 const original=JSON.stringify(a),result=rig.Collision.previewTransition(a,b);
 assert.equal(result.ok,true);assert.equal(result.samples.length,49);
 assert.equal(JSON.stringify(a),original);
 b.armAngles.left={flexion:.3,abduction:.2,elbowFlexion:.4};
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

test('19 selectable anatomy points have finite world coordinates',()=>{
 const rig=loadRig(),j=rig.joints(rig.baseFrame('stand'));
 assert.equal(rig.BODY_TARGETS.length,19);
 assert.equal(new Set(rig.BODY_TARGETS.map(x=>x.key)).size,19);
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
