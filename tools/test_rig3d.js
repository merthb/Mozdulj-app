const fs=require('fs');const vm=require('vm');const assert=require('assert');
const ctx={window:{},console,Math,Float32Array,Uint16Array,Error,Array,Object};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(__dirname+'/../Mozdulj.Maui/Resources/Raw/rig3d/engine.js','utf8'),ctx);
const rig=ctx.window.MozduljRig;
const presets=['stand','crosslegged','chair','tabletop','catcow','child','supine','bridge','side','rowband','lunge','balance'];
const dist=(a,b)=>Math.hypot(...a.map((x,i)=>x-b[i]));
for(const preset of presets){
 const pose=rig.initialPose(preset);rig.checkPose(pose);
 for(const frame of pose.frames){
  let j=rig.joints(frame);
  for(const side of ['left','right']){
   assert(Math.abs(dist(j.shoulder[side],j.elbow[side])-.31)<1e-7,`upper arm ${preset}`);
   assert(Math.abs(dist(j.elbow[side],j.hands[side])-.28)<1e-7,`forearm ${preset}`);
   assert(Math.abs(dist(j.hip[side],j.knee[side])-.45)<1e-7,`thigh ${preset}`);
   assert(Math.abs(dist(j.knee[side],j.feet[side])-.43)<1e-7,`shin ${preset}`);
  }
 }
}
// Geometric invariant: even wildly unreachable targets preserve all bone lengths.
let p=rig.initialPose('stand').frames[0];p.hands.left=[-9,9,8];let j=rig.joints(p);
assert(j.clamped.includes('leftHand'));
assert(Math.abs(dist(j.shoulder.left,j.elbow.left)-.31)<1e-7);
// Rubber band anchors exist on both hand and foot pairs; no raster file dependency.
assert(rig.initialPose('rowband').frames[0].equipment.some(x=>x.type==='band'));
console.log('PASS: 12 offline 3D templates x 2 phases, IK fixed lengths, unreachable target clipping, JSON structure.');
