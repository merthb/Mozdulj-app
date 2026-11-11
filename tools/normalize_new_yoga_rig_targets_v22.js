/* Fix unreachable IK targets in the 97 imported 3D rigs to the exact reachable
 * endpoint produced by the rig solver. This does NOT claim anatomical accuracy.
 * The correction is deliberately limited to hand/foot controls without FK angles.
 */
const fs=require('fs'),vm=require('vm'),path=require('path');
const root=path.resolve(__dirname,'..');
const catalogPath=path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json');
const engineSrc=fs.readFileSync(path.join(root,'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8');
const context={window:{}};vm.runInNewContext(engineSrc,context);
const {joints,checkPose,Collision}=context.window.MozduljRig;
const data=JSON.parse(fs.readFileSync(catalogPath,'utf8'));
let resolved=0,framesChanged=0,remaining=0;const changed=[];const overlapping=[];
for(const e of data.exercises){
 if(!e.id.startsWith('user-yoga-'))continue;
 const rig=e.pose.rig3D;checkPose(rig);
 for(let i=0;i<rig.frames.length;i++){
  const frame=rig.frames[i];const original=joints(frame);let updates=[];
  for(const side of ['left','right']){
   for(const [kind,control] of [['hands','armAngles'],['feet','legAngles']]){
    const key=side+(kind==='hands'?'Hand':'Foot');
    if(original.clamped.includes(key) && !frame[control]?.[side]){
     frame[kind][side]=original[kind][side].map(x=>Math.round(x*1000000)/1000000);
     updates.push(key);resolved++;
    }
   }
  }
  if(updates.length){framesChanged++;changed.push({exercise:e.id,frame:i,corrected:updates});}
  const final=joints(frame); if(final.clamped.length)remaining++;
  const issues=Collision.report(frame);
  const serious=(issues.examples||[]).filter(item=>item.depth>.05);
  if(serious.length)overlapping.push({exercise:e.id,frame:i,overlap:serious.slice(0,8)});
 }
}
fs.writeFileSync(catalogPath,JSON.stringify(data,null,2)+'\n','utf8');
const report={fixedTargets:resolved,affectedFrames:framesChanged,residualClampedFrames:remaining,
 seriousCollisionFrames:overlapping.length,corrected:changed,possibleOverlaps:overlapping,
 disclaimer:'These are geometric corrections. Actual anatomy, contact pose and exercise quality are not clinically verified.'};
fs.writeFileSync(path.join(root,'docs/JOGA_3D_GEOMETRIA_AUDIT_v22.json'),JSON.stringify(report,null,2),'utf8');
console.log(JSON.stringify({fixedTargets:resolved,affectedFrames:framesChanged,residualClampedFrames:remaining,seriousCollisionFrames:overlapping.length},null,2));
