/* Mozdulj! offline WebGL articulated-3D prototype. No CDN or third-party runtime.
   Units: metres, Y up, Z forwards. Demo rig; NOT a clinical movement validator. */
(function(global){'use strict';
const V=(x=0,y=0,z=0)=>[x,y,z], add=(a,b)=>a.map((x,i)=>x+b[i]), sub=(a,b)=>a.map((x,i)=>x-b[i]),mul=(a,s)=>a.map(x=>x*s), dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],len=a=>Math.sqrt(dot(a,a)),norm=a=>mul(a,1/Math.max(1e-8,len(a))),clamp=(x,a,b)=>Math.min(b,Math.max(a,x));
const rotX=(p,a)=>[p[0],p[1]*Math.cos(a)-p[2]*Math.sin(a),p[1]*Math.sin(a)+p[2]*Math.cos(a)];
const rotY=(p,a)=>[p[0]*Math.cos(a)+p[2]*Math.sin(a),p[1],-p[0]*Math.sin(a)+p[2]*Math.cos(a)];
const rotZ=(p,a)=>[p[0]*Math.cos(a)-p[1]*Math.sin(a),p[0]*Math.sin(a)+p[1]*Math.cos(a),p[2]];
const rotate=(p,e)=>rotY(rotZ(rotX(p,e[0]),e[2]),e[1]);
function solve2(a,target,upper,lower,pole,minBend=0.04,maxBend=2.62){
  let v=sub(target,a), d=len(v),u=norm(d<.001?[0,0,1]:v); // maxBend is maximum flexion from straight
  const minD=Math.sqrt(Math.max(0,upper*upper+lower*lower-2*upper*lower*Math.cos(Math.PI-maxBend)));
  d=clamp(d, Math.max(Math.abs(upper-lower)+.015,minD),upper+lower-.004);
  const end=add(a,mul(u,d));
  let p=sub(pole,a);p=sub(p,mul(u,dot(p,u)));
  if(len(p)<.0001){p=cross(u,[0,1,0]);if(len(p)<.0001)p=cross(u,[1,0,0]);}
  p=norm(p);
  const along=(upper*upper-lower*lower+d*d)/(2*d),h=Math.sqrt(Math.max(0,upper*upper-along*along));
  return {mid:add(add(a,mul(u,along)),mul(p,h)),end,reachClamped:len(sub(target,end))>.025};
}
const MAX_PHASES=16;
const PROP_POSITIONS={chair:[0,.30,-.14],ball:[0,.36,.20],softBall:[0,.36,.20],roller:[0,.18,-.20],wall:[0,1,-.62],step:[0,.13,.48],block:[0,.09,.23],balancePad:[0,.04,.3]};
function propPosition(prop){return prop.position||PROP_POSITIONS[prop.type];}
function propYaw(prop){return prop.yaw||0;}
function propTransform(prop,local){return add(propPosition(prop),rotY(local,propYaw(prop)));}
// IDs are local to a pose and shared by copies of an object across phases.
// Never assign IDs to legacy objects merely by opening a pose.
function newEquipmentId(frames){
 const used=new Set(frames.flatMap(f=>(f.equipment||[]).map(p=>p.id)));
 let n=1;while(used.has('eq-'+n))n++;return 'eq-'+n;
}
function validEquipmentIds(frames){
 const types=new Map();
 for(const frame of frames){
   const seen=new Set();
   for(const prop of frame.equipment||[]){
     if(prop.id==null)continue;
     if(typeof prop.id!=='string'||!/^[a-zA-Z0-9_-]{1,64}$/.test(prop.id)||seen.has(prop.id)||(types.has(prop.id)&&types.get(prop.id)!==prop.type))return false;
     seen.add(prop.id);types.set(prop.id,prop.type);
   }
 }
 return true;
}
// Explicit identities take precedence; retain the legacy ordinal fallback
// only for ID-less objects, never match them to newly created objects.
function matchingProp(frames,sourceFrame,sourceIndex){
 const prop=frames[sourceFrame].equipment[sourceIndex];
 if(!prop||!PROP_POSITIONS[prop.type])return [];
 if(prop.id!=null)return frames.flatMap((f,frame)=>{
   const index=(f.equipment||[]).findIndex(x=>x.id===prop.id&&x.type===prop.type);
   return index<0?[]:[{frame,index}];
 });
 const ordinal=frames[sourceFrame].equipment.slice(0,sourceIndex).filter(x=>x.type===prop.type&&x.id==null).length;
 return frames.flatMap((f,frame)=>{
   const indexes=(f.equipment||[]).flatMap((x,i)=>x.type===prop.type&&x.id==null?[i]:[]);
   return indexes[ordinal]===undefined?[]:[{frame,index:indexes[ordinal]}];
 });
}
// Atomic multi-phase edit: compute all candidates before accepting any.
function editProps(frames,locations,position,yaw){
 const next=frames.map(cloneFrame);
 for(const {frame,index} of locations){
   const old=next[frame].equipment[index];
   if(old.locked)return {ok:false,frame,issue:'propLocked'};
   const start=cloneFrame(next[frame]);start.equipment[index].position=[...propPosition(old)];start.equipment[index].yaw=propYaw(old);
   const end=cloneFrame(start);end.equipment[index].position=[...position];end.equipment[index].yaw=yaw;
   const result=Collision3D.transition(start,end);
   if(result.limited)return {ok:false,frame,issue:result.issue};
   next[frame]=end;
 }
 return {ok:true,frames:next};
}
const PHASE_DIRECTIONS=['all','left','right','forward','backward','up','down','inward','outward','clockwise','counterclockwise','each_side'];
const bones={armUpper:.31,armLower:.28,thigh:.45,shin:.43};
function baseFrame(preset){
 const b={name:'Kiindulás',root:[0,.94,0],euler:[0,0,0],headYaw:0,headPitch:0,headRoll:0,upperYaw:0,spineBend:0,pelvicTilt:0,footPitch:{left:0,right:0},legAngles:{},armAngles:{},hands:{left:[-.34,.96,.02],right:[.34,.96,.02]},feet:{left:[-.14,.075,0],right:[.14,.075,0]},poles:{elbowLeft:[-.7,.6,.14],elbowRight:[.7,.6,.14],kneeLeft:[-.18,.5,.3],kneeRight:[.18,.5,.3]},equipment:[]};
 const f=JSON.parse(JSON.stringify(b));
 if(preset==='crosslegged'){f.root=[0,.43,0];f.feet={left:[.25,.11,.30],right:[-.25,.11,.30]};f.hands={left:[-.30,.52,.24],right:[.30,.52,.24]};f.poles.kneeLeft=[-.38,.42,.44];f.poles.kneeRight=[.38,.42,.44];f.equipment=[{type:'mat'}];}
 if(preset==='chair'){f.root=[0,.57,0];f.feet={left:[-.14,.07,.45],right:[.14,.07,.45]};f.poles.kneeLeft=[-.14,.56,.50];f.poles.kneeRight=[.14,.56,.50];f.hands={left:[-.29,.53,.15],right:[.29,.53,.15]};f.equipment=[{type:'chair'}];}
 if(preset==='tabletop'||preset==='catcow'||preset==='child'){f.root=[0,.61,.15];f.euler=[Math.PI/2,0,0];f.hands={left:[-.24,.07,.59],right:[.24,.07,.59]};f.feet={left:[-.14,.055,-.57],right:[.14,.055,-.57]};f.poles.kneeLeft=[-.15,.34,-.34];f.poles.kneeRight=[.15,.34,-.34];f.poles.elbowLeft=[-.30,.53,.78];f.poles.elbowRight=[.30,.53,.78];f.headPitch=-.24;f.equipment=[{type:'mat'}];}
 if(preset==='child'){f.root=[0,.31,.44];f.euler=[1.34,0,0];f.hands={left:[-.26,.055,.51],right:[.26,.055,.51]};f.feet={left:[-.16,.055,-.61],right:[.16,.055,-.61]};f.headPitch=-.40;}
 if(preset==='supine'||preset==='bridge'){f.root=[0,.29,.20];f.euler=[-Math.PI/2,0,0];f.hands={left:[-.23,.09,-.55],right:[.23,.09,-.55]};f.feet={left:[-.16,.055,.83],right:[.16,.055,.83]};f.poles.kneeLeft=[-.16,.65,.62];f.poles.kneeRight=[.16,.65,.62];f.equipment=[{type:'mat'}];}
 if(preset==='side'){f.root=[0,.46,.26];f.euler=[-Math.PI/2,0,Math.PI/2];f.hands={left:[0,.08,-.45],right:[.08,.55,-.2]};f.feet={left:[.03,.10,.84],right:[.03,.29,.82]};f.poles.kneeLeft=[-.08,.12,.65];f.poles.kneeRight=[.03,.45,.65];f.headYaw=.10;f.equipment=[{type:'mat'}];}
 if(preset==='rowband'){f.root=[0,.38,.05];f.hands={left:[-.2,.55,.17],right:[.2,.55,.17]};f.feet={left:[-.14,.08,.87],right:[.14,.08,.87]};f.poles.kneeLeft=[-.18,.14,.43];f.poles.kneeRight=[.18,.14,.43];f.equipment=[{type:'mat'},{type:'band',anchor:'feet'}];}
 if(preset==='lunge'){f.root=[0,.71,0];f.feet={left:[-.21,.07,-.57],right:[.21,.07,.56]};f.poles.kneeLeft=[-.2,.44,-.40];f.poles.kneeRight=[.2,.42,.42];f.hands={left:[-.39,.90,-.08],right:[.39,.90,-.08]};f.equipment=[{type:'mat'}];}
 if(preset==='balance'){f.root=[0,.94,0];f.feet.right=[.17,.59,.25];f.poles.kneeRight=[.19,.68,.30];f.hands={left:[-.45,1.02,0],right:[.45,1.02,0]};}
 if(preset==='prone') {f.root=[0,.26,.16];f.euler=[Math.PI/2,0,0];f.hands={left:[-.24,.08,.55],right:[.24,.08,.55]};f.feet={left:[-.16,.07,-.87],right:[.16,.07,-.87]};f.poles.elbowLeft=[-.36,.28,.36];f.poles.elbowRight=[.36,.28,.36];f.poles.kneeLeft=[-.15,.20,-.59];f.poles.kneeRight=[.15,.20,-.59];f.headPitch=-.12;f.equipment=[{type:'mat'}];}
 if(preset==='sideplank'){f.root=[0,.51,.14];f.euler=[-Math.PI/2,0,Math.PI/2];f.hands={left:[-.22,.12,.43],right:[.18,.67,.07]};f.feet={left:[.01,.06,.77],right:[.10,.08,.81]};f.poles.kneeLeft=[-.17,.10,.58];f.poles.kneeRight=[.15,.13,.55];f.equipment=[{type:'mat'}];}
 if(preset==='kneel'){f.root=[0,.70,.03];f.feet={left:[-.18,.07,.55],right:[.18,.07,.55]};f.poles.kneeLeft=[-.16,.14,.25];f.poles.kneeRight=[.16,.14,.25];f.hands={left:[-.31,.73,.12],right:[.31,.73,.12]};f.equipment=[{type:'mat'}];}
 if(preset==='hinge'){f.root=[0,.82,0];f.euler=[-.38,0,0];f.hands={left:[-.30,.77,.40],right:[.30,.77,.40]};f.feet={left:[-.16,.06,.18],right:[.16,.06,.18]};}
 if(preset==='ballseat'){f.root=[0,.67,0];f.hands={left:[-.32,.68,.16],right:[.32,.68,.16]};f.feet={left:[-.17,.07,.44],right:[.17,.07,.44]};f.poles.kneeLeft=[-.18,.43,.41];f.poles.kneeRight=[.18,.43,.41];f.equipment=[{type:'ball',position:[0,.34,0]}];}
 if(preset==='boat'){
   f.root=[0,.44,0];f.euler=[-.16,0,0];
   f.legAngles={left:{flexion:2.05,abduction:.12,kneeFlexion:1.30},
       right:{flexion:2.05,abduction:.12,kneeFlexion:1.30}};
   f.hands={left:[-.34,.40,.35],right:[.34,.40,.35]};
   f.equipment=[{type:'mat'}];
 }
 if(preset==='reclinedbutterfly'){
   const b=baseFrame('supine');Object.assign(f,b);
   f.legAngles={left:{flexion:.9,abduction:.55,kneeFlexion:1.2},
       right:{flexion:.9,abduction:.55,kneeFlexion:1.2}};
 }
 if(preset==='legs-wall'){
   const b=baseFrame('supine');Object.assign(f,b);
   f.legAngles={left:{flexion:1.72,abduction:.03,kneeFlexion:0},
       right:{flexion:1.72,abduction:.03,kneeFlexion:0}};
   f.equipment=[{type:'mat'},{type:'wall'}];
 }
 if(preset==='plank'){f.root=[0,.39,.14];f.euler=[Math.PI/2,0,0];f.hands={left:[-.24,.055,.48],right:[.24,.055,.48]};f.feet={left:[-.16,.055,-.88],right:[.16,.055,-.88]};f.poles.kneeLeft=[-.17,.20,-.66];f.poles.kneeRight=[.17,.20,-.66];f.poles.elbowLeft=[-.25,.35,.30];f.poles.elbowRight=[.25,.35,.30];f.equipment=[{type:'mat'}];}
 if(['tabletop','catcow','child','plank'].includes(preset)){for(const side of ['left','right'])if(f.feet[side][1]<.1){f.feet[side][1]=.12;f.footPitch[side]=-.65;}}
 // Revised creation-only presets. Never apply these adjustments to loaded
 // exercise poses. Use rigid chains for the intended support geometry.
 if(preset==='tabletop'||preset==='catcow'){
   f.root=[0,.55,.15];
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:Math.PI/2,abduction:.32,kneeFlexion:1.69};
     f.footPitch[side]=.95;f.hands[side]=[side==='left'?-.24:.24,.07,.69];
   }
 }
 if(preset==='supine'||preset==='bridge'){
   f.root=[0,.20,.20];
   for(const side of ['left','right'])f.feet[side]=[side==='left'?-.16:.16,.075,.83];
 }
 if(preset==='kneel'){
   f.root=[0,.55,.03];
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:0,abduction:.02,kneeFlexion:1.69};f.footPitch[side]=.95;
   }
 }
 if(preset==='plank'){
   f.root=[0,.33,.14];f.euler=[1.25,0,0];
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:-.15,abduction:.03,kneeFlexion:0};
     f.footPitch[side]=.95;
     f.hands[side]=[side==='left'?-.215:.215,.07,.14+.54*Math.sin(1.25)];
   }
 }
 // Additional creation-only corrections, validated with the same geometry
 // engine as the editor. These are illustrative starting poses.
 if(preset==='chair'){
   f.root=[0,.54,-.03];
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:1.45,abduction:.25,kneeFlexion:1.45};
     f.hands[side]=[side==='left'?-.35:.35,.67,.17];
   }
 }
 if(preset==='prone'){
   f.root=[0,.20,.16];
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:0,abduction:.03,kneeFlexion:.10};
     f.footPitch[side]=.95;f.hands[side]=[side==='left'?-.30:.30,.10,.65];
   }
 }
 if(preset==='rowband'){
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:1.23,abduction:.26,kneeFlexion:.04};f.footPitch[side]=-.1;
   }
 }
 if(preset==='boat'){
   for(const side of ['left','right']){
     f.legAngles[side].abduction=.50;f.hands[side]=[side==='left'?-.49:.49,.66,.35];
   }
 }
 if(preset==='legs-wall'){
   f.root=[0,.20,.20];f.equipment[1].position=[0,1,.40];
   for(const side of ['left','right']){
     f.legAngles[side].flexion=1.47;f.legAngles[side].abduction=.30;
   }
 }
 if(preset==='ballseat'){
   f.root=[0,.82,.15];
   for(const side of ['left','right']){
     f.feet[side]=[side==='left'?-.22:.22,.08,.40];
     f.hands[side]=[side==='left'?-.35:.35,.85,.20];
   }
 }
 if(preset==='balance')f.legAngles.right={flexion:1.2,abduction:.35,kneeFlexion:.6};
 if(preset==='hinge'){
   f.root=[0,.90,0];f.euler=[.45,0,0];
   for(const side of ['left','right']){
     f.feet[side]=[side==='left'?-.16:.16,.085,0];
     f.hands[side]=[side==='left'?-.34:.34,.95,.40];
     f.footPitch[side]=-.22;
   }
 }
 // Third template pass: use actual local articulations for folded and
 // stacked limbs rather than forcing unreachable world targets.
 if(preset==='crosslegged'){
   f.root[1]=.34;
   f.legAngles.left={flexion:1.25,abduction:.65,kneeFlexion:2.15,axialRotation:1.3};
   f.legAngles.right={flexion:1.55,abduction:.65,kneeFlexion:2.15,axialRotation:-1.3};
   f.hands.left=[-.36,.55,.15];f.hands.right=[.36,.55,.15];
 }
 if(preset==='child'){
   f.root=[0,.45,.18];f.euler=[1.8,0,0];f.pelvisPitch=-.35;
   f.lumbarBend=.12;f.thoracicBend=.14;
   for(const side of ['left','right']){
     f.legAngles[side]={flexion:1.95,abduction:.55,kneeFlexion:2.36,axialRotation:side==='left'?.3:-.3};
     f.footPitch[side]=.95;f.hands[side]=[side==='left'?-.32:.32,.08,.97];
   }
 }
 if(preset==='side'){
   f.root[1]=.35;
   for(const side of ['left','right'])f.legAngles[side]={flexion:0,abduction:.12,kneeFlexion:.12};
   f.hands.left=[.35,.10,-.53];f.hands.right=[.32,.47,-.12];
 }
 if(preset==='lunge'){
   f.feet.left=[-.18,.20,-.45];f.feet.right=[.18,.075,.42];
   f.footPitch.left=.65;f.footPitch.right=-.15;
 }
 if(preset==='sideplank'){
   f.root=[0,.35,.14];f.euler=[-1.2,0,1];
   for(const side of ['left','right'])f.legAngles[side]={flexion:0,abduction:0,kneeFlexion:0};
   const j=joints(f);
   f.hands.left=[j.shoulder.left[0],.075,j.shoulder.left[2]];
   f.hands.right=[.26,.70,-.05];
 }
 return f;
}
const PRESET_NAMES=['stand','crosslegged','chair','tabletop','catcow','child','supine','bridge','side','rowband','lunge','balance','boat','reclinedbutterfly','legs-wall','prone','sideplank','kneel','hinge','ballseat','plank'];
function initialPose(preset='stand'){
 let a=baseFrame(preset),b=JSON.parse(JSON.stringify(a));b.name='Mozdulat';
 if(preset==='catcow'){b.lumbarBend=.08;b.thoracicBend=.16;b.headPitch=.30;}
 if(preset==='bridge'){b.root[1]=.49;b.euler[0]=-1.94;}
 if(preset==='rowband'){b.hands.left=[-.33,.61,.02];b.hands.right=[.33,.61,.02];}
 if(preset==='side'){b.hands.right=[.34,.52,-.18];}
 if(preset==='balance'){b.legAngles.right.flexion=1.45;b.legAngles.right.kneeFlexion=.7;}
 if(preset==='lunge'){b.root[1]=.73;}
 if(preset==='child'){b.thoracicBend=.15;b.hands.left=[-.32,.08,.98];b.hands.right=[.32,.08,.98];}
 return {version:1,preset,reviewStatus:'illustrative',frames:[a,b],camera:{azimuth:.58,elevation:.35,distance:3.55,target:[0,.72,0]}};
}
// Review the authored geometry of every template frame; no clinical claim.
function auditPreset(name){
 const pose=initialPose(name);return {name,frames:pose.frames.map(frame=>({overlaps:Collision3D.report(frame),clamped:[...joints(frame).clamped]})),transition:Collision3D.previewTransition(pose.frames[0],pose.frames[1]).ok};
}
// Rotate a direction about a unit axis without changing bone length.
function axisRotate(v,axis,angle){return add(add(mul(v,Math.cos(angle)),mul(cross(axis,v),Math.sin(angle))),mul(axis,dot(axis,v)*(1-Math.cos(angle))));}
// Additive local articulations. Missing fields are exactly neutral: existing
// catalog poses retain their original geometry and serialized representation.
const ARTICULATION_LIMITS={pelvisPitch:30*Math.PI/180,pelvisYaw:35*Math.PI/180,pelvisRoll:25*Math.PI/180,lumbarBend:30*Math.PI/180,lumbarSideBend:25*Math.PI/180,thoracicBend:35*Math.PI/180,thoracicSideBend:25*Math.PI/180,leftShoulderElevation:20*Math.PI/180,leftShoulderProtraction:25*Math.PI/180,leftWristDeviation:25*Math.PI/180,leftAnkleYaw:30*Math.PI/180,leftAnkleRoll:25*Math.PI/180,rightShoulderElevation:20*Math.PI/180,rightShoulderProtraction:25*Math.PI/180,rightWristDeviation:25*Math.PI/180,rightAnkleYaw:30*Math.PI/180,rightAnkleRoll:25*Math.PI/180};
const SIDE_ARTICULATION={arm:['ShoulderElevation','ShoulderProtraction','WristDeviation'],leg:['AnkleYaw','AnkleRoll']};
function sideAngle(frame,side,field){
 const key=side+field,limit=ARTICULATION_LIMITS[key];
 return clamp(frame[key]||0,-limit,limit);
}
function editSideArticulation(frame,side,field,value,symmetric=false){
 const key=side+field,other=swapSide(side)+field,limit=ARTICULATION_LIMITS[key];
 if(limit==null||!Number.isFinite(value))return false;
 const old=frame[key]||0;let delta=clamp(value,-limit,limit)-old;
 if(symmetric){
   const opposite=frame[other]||0;
   delta=clamp(delta,-limit-opposite,limit-opposite);
   frame[other]=opposite+delta;
 }
 frame[key]=old+delta;return true;
}
const HAND_PRESETS={open:0,relaxed:.32,grip:.78,fist:1};
const HAND_KEYS=['leftHandCurl','rightHandCurl'];
const neutralNumericKey=k=>Object.hasOwn(ARTICULATION_LIMITS,k)||HAND_KEYS.includes(k);
function validHandCurl(frame){return HAND_KEYS.every(k=>frame[k]==null||(Number.isFinite(frame[k])&&frame[k]>=0&&frame[k]<=1));}
function handCurl(frame,side){const value=frame[side+'HandCurl'];return value==null?null:clamp(value,0,1);}
function setHandCurl(frame,side,value,symmetric=false){
 if(!['left','right'].includes(side)||(value!==null&&(!Number.isFinite(value)||value<0||value>1)))return false;
 for(const s of symmetric?['left','right']:[side]){
   const key=s+'HandCurl';if(value===null)delete frame[key];else frame[key]=value;
 }
 return true;
}
// Four fingers share one curl control. A mirrored, anatomically local basis
// is constructed from the palm and the rotated thumb rather than world axes.
// Finger segment lengths remain fixed; the grasp position follows curl.
function handGeometry(wrist,forward,thumbDirection,side,curl){
 let transverse=sub(thumbDirection,mul(forward,dot(thumbDirection,forward)));
 if(len(transverse)<1e-6)transverse=cross(forward,Math.abs(forward[1])<.9?[0,1,0]:[0,0,1]);
 transverse=norm(transverse);
 // Existing thumb coordinates use +X for both hands. Use a side-relative
 // axis for the fingers and a reflection-consistent palm normal.
 const sign=side==='left'?-1:1;
 const lateral=mul(transverse,-sign);
 const inward=norm(mul(cross(forward,lateral),sign));
 const center=add(wrist,mul(forward,.048));
 const fingers=[.074,.082,.077,.063].map((length,i)=>{
   const spread=(1.5-i)*.018;
   const base=add(add(wrist,mul(forward,.086)),mul(lateral,spread));
   const segments=[.46,.31,.23],angles=[.95,1.9,2.6].map(a=>a*curl);
   const points=[base];
   for(let k=0;k<3;k++){
     const direction=add(mul(forward,Math.cos(angles[k])),mul(inward,Math.sin(angles[k])));
     points.push(add(points[k],mul(direction,length*segments[k])));
   }
   return points;
 });
 const thumbBase=add(add(wrist,mul(forward,.023)),mul(lateral,.036));
 const thumbMiddle=add(thumbBase,add(mul(lateral,.024*(1-curl*.6)),mul(forward,.027)));
 const thumbTip=add(thumbMiddle,add(mul(forward,.022*(1-curl)),mul(inward,.027*curl)));
 return {center,forward,lateral,inward,fingers,thumb:[thumbBase,thumbMiddle,thumbTip],grip:add(wrist,add(mul(forward,.064*curl),mul(inward,.026*curl))),curl};
}
function gripPoint(j,side){return j.handDetails?.[side]?.grip||j.hands[side];}
function validArticulation(frame){
 return Object.entries(ARTICULATION_LIMITS).every(([key,limit])=>frame[key]==null||(Number.isFinite(frame[key])&&Math.abs(frame[key])<=limit+1e-6));
}
function articulationAxes(frame){
 const angle=key=>clamp(frame[key]||0,-ARTICULATION_LIMITS[key],ARTICULATION_LIMITS[key]);
 const p=[angle('pelvisPitch'),angle('pelvisYaw'),angle('pelvisRoll')];
 const l=[angle('lumbarBend'),0,angle('lumbarSideBend')];
 const t=[angle('thoracicBend'),0,angle('thoracicSideBend')];
 const e=frame.euler;
 const pelvis=v=>rotate(rotate(v,p),e);
 const lower=v=>rotate(rotate(v,l),e);
 const upper=v=>lower(rotate(v,t));
 const legLocal=v=>unrotate(unrotate(v,e),p);
 const armLocal=v=>unrotate(unrotate(unrotate(v,e),l),t);
 return {pelvis,lower,upper,legLocal,armLocal,p,l,t};
}
function joints(frame){
 const root=frame.root,e=frame.euler, world=p=>add(root,rotate(p,e));
 // The pelvis stays anchored while the ribcage can twist and flex independently.
 // Segment lengths remain fixed; curvature is a small local spine offset.
 const bend=clamp(frame.spineBend||0,-.6,.6), twist=clamp(frame.upperYaw||0,-1.3,1.3);
 const spineLocal=(height)=>[0,height-.025*Math.abs(bend)*height,Math.sin(bend)*height*height*.33];
 const axes=articulationAxes(frame);
 const midLocal=spineLocal(.28),mid=add(root,axes.lower(midLocal));
 const upperPoint=v=>axes.l.some(Boolean)||axes.t.some(Boolean)?add(mid,axes.upper(sub(v,midLocal))):world(v);
 const shoulderLocal=side=>{
   const sign=side==='left'?-1:1;
   const elevation=sideAngle(frame,side,'ShoulderElevation'),protraction=sideAngle(frame,side,'ShoulderProtraction');
   // Rotate the simplified clavicle around the chest anchor, rather than
   // stretching the upper arm or translating the entire ribcage.
   const clavicle=rotY(rotZ([sign*.215,0,0],sign*elevation),-sign*protraction);
   return add(spineLocal(.54+(frame.shoulderLift||0)),rotY(clavicle,twist));
 };
 const hip={left:add(root,axes.pelvis([-.13,0,0])),right:add(root,axes.pelvis([.13,0,0]))};
 const shoulder={left:upperPoint(shoulderLocal('left')),right:upperPoint(shoulderLocal('right'))};
 const neck=upperPoint(spineLocal(.70));
 const head=add(neck,axes.upper(rotate([0,.21,.015],[frame.headPitch||0,frame.headYaw||0,frame.headRoll||0])));
 const j={root,hip,shoulder,neck,head,spineMid:mid,spineUpper:upperPoint(spineLocal(.52)),articulatedSpine:axes.l.some(Boolean)||axes.t.some(Boolean),pelvicTilt:clamp(frame.pelvicTilt||0,-.35,.35),hands:{},feet:{},elbow:{},knee:{},toes:{},palms:{},thumbs:{},footAxes:{},handDetails:{},hingePoles:{arm:{},leg:{}},clamped:[]};
 for(const side of ['left','right']){
  let s=side==='left'?-1:1;
  // Anatomical hinge pole is anchored to the current body rotation, not a fixed
  // screen-plane vertex. This avoids reversed bends when the torso is rotated.
  const anatomicElbow=add(shoulder[side],(j.articulatedSpine?axes.upper(unrotate(rotate([s*.11,-.30,-.33],add(e,[0,twist*.5,0])),e)):rotate([s*.11,-.30,-.33],add(e,[0,twist*.5,0]))));
  const anatomicKnee=add(hip[side],axes.pelvis([s*.02,-.17,.58]));
  j.hingePoles.arm[side]=anatomicElbow;j.hingePoles.leg[side]=anatomicKnee;
  function safePole(requested,anchor,anatomic){
    if(!requested)return anatomic;
    const q=norm(sub(requested,anchor)),reference=norm(sub(anatomic,anchor));
    return dot(q,reference)>=.87 ? requested : anatomic;
  }
  let h=solve2(shoulder[side],frame.hands[side],bones.armUpper,bones.armLower,
     safePole(frame.poles['elbow'+(side==='left'?'Left':'Right')],shoulder[side],anatomicElbow),.04,2.60);
  let k=solve2(hip[side],frame.feet[side],bones.thigh,bones.shin,
     safePole(frame.poles['knee'+(side==='left'?'Left':'Right')],hip[side],anatomicKnee),.04,2.43);
  // Contact floor constraint: elbows/knees cannot penetrate the floor plane.
  if(h.mid[1]<.05)h=solve2(shoulder[side],frame.hands[side],bones.armUpper,bones.armLower,
       add(shoulder[side],[s*.1,.65,0]),.04,2.60);
  if(k.mid[1]<.05)k=solve2(hip[side],frame.feet[side],bones.thigh,bones.shin,
       add(hip[side],[s*.1,.65,0]),.04,2.43);
  // Optional forward-kinematic leg controls: move the femur about the hip,
  // then the shin about the knee, with physically fixed segment lengths.
  // This enables >90° hip flexion without an IK pole flipping the knee.
  const leg=frame.legAngles?.[side];
  if(leg){
    const f=clamp(leg.flexion||0,-.3,2.36),a=clamp(leg.abduction||0,-.7,.7);
    const kb=clamp(leg.kneeFlexion||0,0,2.36),out=side==='left'?-1:1;
    const dir=ang=>norm(axes.pelvis([out*Math.sin(a),-Math.cos(ang)*Math.cos(a),Math.sin(ang)*Math.cos(a)]));
    k.mid=add(hip[side],mul(dir(f),bones.thigh));
    if(leg.axialRotation!=null){
      const upper=dir(f),hinge=norm(axes.pelvis([0,-Math.sin(f),-Math.cos(f)]));
      const turned=axisRotate(hinge,upper,clamp(leg.axialRotation,-Math.PI/2,Math.PI/2));
      k.end=add(k.mid,mul(norm(add(mul(upper,Math.cos(kb)),mul(turned,Math.sin(kb)))),bones.shin));
    }else k.end=add(k.mid,mul(dir(f-kb),bones.shin));
    k.reachClamped=false;
  }
  // Joint-angle arm controls. Upper arm and forearm are a two-bone local chain;
  // the elbow flexes only in its anatomical hinge plane, never toward the rear.
  const arm=frame.armAngles?.[side];
  if(arm){
    const flex=clamp(arm.flexion||0,-.52,3.05);
    const abduct=clamp(arm.abduction||0,0,2.70);
    const elbow=clamp(arm.elbowFlexion||0,0,2.62);
    const upper=norm(axes.upper(rotZ(rotX([0,-1,0],-flex),s*abduct)));
    const hinge=norm(axes.upper(rotZ(rotX([0,-1,0],-flex-Math.PI/2),s*abduct)));
    const turned=axisRotate(hinge,upper,clamp(arm.axialRotation||0,-Math.PI/2,Math.PI/2));
    const lower=norm(add(mul(upper,Math.cos(elbow)),mul(turned,Math.sin(elbow))));
    h.mid=add(shoulder[side],mul(upper,bones.armUpper));
    h.end=add(h.mid,mul(lower,bones.armLower));
    h.reachClamped=false;
  }
  j.elbow[side]=h.mid;j.hands[side]=h.end;j.knee[side]=k.mid;j.feet[side]=k.end;
  // The ankle has its OWN mediolateral hinge, unlike the previous solution
  // which projected body-forward onto the shin and suddenly swapped axes
  // during high kicks. This keeps pipa/spicc in the leg's sagittal plane.
  const pitch=clamp(frame.footPitch?.[side]||0,-.65,.95);
  const shinDirection=norm(sub(j.feet[side],j.knee[side]));
  let ankleAxis=axes.pelvis([1,0,0]);
  let neutral=cross(shinDirection,ankleAxis);
  if(len(neutral)<1e-4)neutral=axes.pelvis([0,0,1]);
  neutral=norm(neutral);
  let toeDirection=norm(add(mul(neutral,Math.cos(pitch)),mul(shinDirection,Math.sin(pitch))));
  const ankleYaw=sideAngle(frame,side,'AnkleYaw'),ankleRoll=sideAngle(frame,side,'AnkleRoll');
  if(ankleYaw)toeDirection=norm(axisRotate(toeDirection,shinDirection,-s*ankleYaw));
  j.toes[side]=add(j.feet[side],mul(toeDirection,.18));
  // A noncircular sole needs an orientation as well as a toe endpoint.
  // Rotate the transverse axis with the toe, then tilt around the long axis.
  let transverse=ankleYaw?axisRotate(ankleAxis,shinDirection,-s*ankleYaw):ankleAxis;
  transverse=sub(transverse,mul(toeDirection,dot(transverse,toeDirection)));
  if(len(transverse)<1e-5)transverse=cross(toeDirection,shinDirection);
  if(len(transverse)<1e-5)transverse=cross(toeDirection,Math.abs(toeDirection[1])<.9?[0,1,0]:[0,0,1]);
  transverse=norm(transverse);
  if(ankleRoll)transverse=norm(axisRotate(transverse,toeDirection,s*ankleRoll));
  j.footAxes[side]={right:transverse,up:norm(cross(toeDirection,transverse)),forward:toeDirection};
  // Wrist flexion and forearm pronation/supination change the hand orientation
  // but never move the elbow or stretch either arm segment.
  const forearm=norm(sub(j.hands[side],j.elbow[side]));
  const wrist=clamp(arm?.wristFlexion||0,-1.04,1.04);
  const turn=clamp(arm?.forearmRotation||0,-1.57,1.57);
  let wristAxis=norm(axes.upper([1,0,0]));
  let wristBend=cross(forearm,wristAxis);
  if(len(wristBend)<1e-4)wristBend=axes.upper([0,0,1]);
  wristBend=norm(wristBend);
  let palmDir=norm(add(mul(forearm,Math.cos(wrist)),mul(wristBend,Math.sin(wrist))));
  const deviation=sideAngle(frame,side,'WristDeviation');
  if(deviation){
    let lateral=sub(wristAxis,mul(palmDir,dot(wristAxis,palmDir)));
    if(len(lateral)<1e-5)lateral=cross(palmDir,wristBend);
    palmDir=norm(add(mul(palmDir,Math.cos(deviation)),mul(norm(lateral),s*Math.sin(deviation))));
  }
  j.palms[side]=add(j.hands[side],mul(palmDir,.115));
  let thumbAxis=sub(wristAxis,mul(palmDir,dot(wristAxis,palmDir)));
  if(len(thumbAxis)<1e-4)thumbAxis=cross(palmDir,axes.upper([0,1,0]));
  thumbAxis=norm(thumbAxis);
  const thumbRot=add(mul(thumbAxis,Math.cos(turn)),mul(cross(palmDir,thumbAxis),Math.sin(turn)));
  j.thumbs[side]=add(j.hands[side],mul(norm(thumbRot),.064));
  const curl=handCurl(frame,side);
  if(curl!==null)j.handDetails[side]=handGeometry(j.hands[side],palmDir,norm(thumbRot),side,curl);
  if(h.reachClamped)j.clamped.push(side+'Hand');if(k.reachClamped)j.clamped.push(side+'Foot');
 }
 return j;
}
// Convert rendered joint vectors into local body axes. Only accept a mode
// switch if the existing geometry is representable by the destination mode.
const unrotate=(p,e)=>rotX(rotZ(rotY(p,-e[1]),-e[2]),-e[0]);
const cloneFrame=f=>JSON.parse(JSON.stringify(f));
const modeKeys={arm:'armAngles',leg:'legAngles'};
function limbError(a,b,kind,side){
 const keys=kind==='arm'?['elbow','hands','palms','thumbs']:['knee','feet','toes'];
 return Math.max(...keys.map(k=>len(sub(a[k][side],b[k][side]))));
}
function inferAngles(frame,kind,side,requireAxial=false){
 const j=joints(frame),axes=articulationAxes(frame),s=side==='left'?-1:1;
 const local=kind==='arm'?axes.armLocal:axes.legLocal;
 const upper=norm(local(sub(kind==='arm'?j.elbow[side]:j.knee[side],kind==='arm'?j.shoulder[side]:j.hip[side])));
 const lower=norm(local(sub(kind==='arm'?j.hands[side]:j.feet[side],kind==='arm'?j.elbow[side]:j.knee[side])));
 const options=[];
 if(kind==='leg'){
   const abduction=Math.asin(clamp(s*upper[0],-1,1));
   const flexion=Math.atan2(upper[2],-upper[1]);
   let kneeFlexion=flexion-Math.atan2(lower[2],-lower[1]);
   while(kneeFlexion< -Math.PI)kneeFlexion+=2*Math.PI;
   while(kneeFlexion>Math.PI)kneeFlexion-=2*Math.PI;
   options.push({flexion:clamp(flexion,-.3,2.36),abduction:clamp(abduction,-.7,.7),kneeFlexion:clamp(kneeFlexion,0,2.36)});
 }else{
   const primary=Math.asin(clamp(upper[2],-1,1));
   const elbowFlexion=clamp(Math.acos(clamp(dot(upper,lower),-1,1)),0,2.62);
   for(const flexion of [primary,Math.PI-primary,-Math.PI-primary]){
     const c=Math.cos(flexion);
     if(Math.abs(c)<1e-7){
       // At 90 degrees the upper-arm direction is independent of abduction;
       // infer the hinge plane from the lower arm instead.
       const abduction=Math.atan2(-s*lower[0],lower[1]);
       options.push({flexion:clamp(flexion,-.52,3.05),abduction:clamp(abduction,0,2.70),elbowFlexion,wristFlexion:0,forearmRotation:0});
     }else{
       const abduction=Math.atan2(s*upper[0]/c,-upper[1]/c);
       options.push({flexion:clamp(flexion,-.52,3.05),abduction:clamp(abduction,0,2.70),elbowFlexion,wristFlexion:0,forearmRotation:0});
     }
   }
 }
 // For configurations outside the legacy hinge plane, derive axial rotation
 // from the actual lower bone. Keep legacy candidates as well, so old
 // poses and their flexion semantics remain unchanged when already exact.
 const expanded=[...options];
 for(const a of options){
   const flex=a.flexion,abd=a.abduction;
   const base=kind==='leg'?norm([s*Math.sin(abd),-Math.cos(flex)*Math.cos(abd),Math.sin(flex)*Math.cos(abd)]):
     norm(rotZ(rotX([0,-1,0],-flex),s*abd));
   const hinge=kind==='leg'?norm([0,-Math.sin(flex),-Math.cos(flex)]):
     norm(rotZ(rotX([0,-1,0],-flex-Math.PI/2),s*abd));
   const plane=sub(lower,mul(base,dot(lower,base)));
   const axial=len(plane)<1e-8?0:Math.atan2(dot(cross(hinge,norm(plane)),base),dot(hinge,norm(plane)));
   const bend=Math.acos(clamp(dot(base,lower),-1,1));
   expanded.push({...a,axialRotation:clamp(axial,-Math.PI/2,Math.PI/2),
     [kind==='leg'?'kneeFlexion':'elbowFlexion']:clamp(bend,0,kind==='leg'?2.36:2.62)});
 }
 let best=null;
 for(const angles of expanded){
   if(requireAxial&&angles.axialRotation==null)continue;
   const candidate=cloneFrame(frame),key=modeKeys[kind];
   candidate[key]??={};candidate[key][side]=angles;
   const error=limbError(j,joints(candidate),kind,side);
   if(!best||error<best.error)best={angles,error};
 }
 return best;
}
// A representable conversion does not alter other limbs or the frame.
// Unsupported configurations remain in their original mode, rather than
// being silently snapped into a different anatomical position.
// Reflect across the world sagittal plane (X=0), exchanging anatomical sides.
const reflectX=p=>[-p[0],p[1],p[2]];
const swapSide=s=>s.replace(/left|right|Left|Right/g,k=>({left:'right',right:'left',Left:'Right',Right:'Left'})[k]);
function mirrorControls(a){
 const result=cloneFrame(a);
 for(const key of ['axialRotation','forearmRotation'])if(result[key]!=null)result[key]=-result[key];
 return result;
}
function mirrorFrame(frame){
 const f=cloneFrame(frame);
 if(f.direction==='left'||f.direction==='right')f.direction=swapSide(f.direction);
 else if(f.direction==='clockwise')f.direction='counterclockwise';
 else if(f.direction==='counterclockwise')f.direction='clockwise';
 f.root=reflectX(f.root);f.euler=[f.euler[0],-f.euler[1],-f.euler[2]];
 for(const key of ['headYaw','headRoll','upperYaw','pelvisYaw','pelvisRoll','lumbarSideBend','thoracicSideBend'])if(f[key]!=null)f[key]=-f[key];
 for(const key of ['hands','feet','poles','pointLocks']){
   if(f[key]!=null)f[key]=Object.fromEntries(Object.entries(f[key]).map(([name,pos])=>[swapSide(name),reflectX(pos)]));
 }
 if(f.footPitch!=null)f.footPitch=Object.fromEntries(Object.entries(f.footPitch).map(([side,value])=>[swapSide(side),value]));
 for(const suffix of [...SIDE_ARTICULATION.arm,...SIDE_ARTICULATION.leg]){
   for(const side of ['left','right']){
     const key=side+suffix,opposite=swapSide(side)+suffix;
     if(frame[opposite]!=null)f[key]=frame[opposite];else delete f[key];
   }
 }
 for(const side of ['left','right']){
   const other=swapSide(side),key=side+'HandCurl';if(frame[other+'HandCurl']!=null)f[key]=frame[other+'HandCurl'];else delete f[key];
 }
 for(const key of ['armAngles','legAngles'])if(f[key]!=null)
   f[key]=Object.fromEntries(Object.entries(f[key]).map(([side,a])=>[swapSide(side),mirrorControls(a)]));
 for(const prop of f.equipment||[]){
   for(const key of ['position','positionB','positionC','positionD'])if(prop[key]!=null)prop[key]=reflectX(prop[key]);
   if(prop.yaw!=null)prop.yaw=-prop.yaw;
   for(const key of ['anchor','anchorA','anchorB','anchorC','anchorD','route'])if(typeof prop[key]==='string')prop[key]=swapSide(prop[key]);
 }
 return f;
}
// Copy one side relative to the body, not the camera or world orientation.
// The other limb's existing mode is replaced; its locks remain constraints.
function copyLimb(frame,kind,source){
 const dest=swapSide(source),target=kind==='arm'?'hands':'feet',key=modeKeys[kind];
 const pole=kind==='arm'?'elbow':'knee';
 const localMirror=point=>add(frame.root,rotate(reflectX(unrotate(sub(point,frame.root),frame.euler)),frame.euler));
 frame[target][dest]=localMirror(frame[target][source]);
 const fromPole=pole+(source==='left'?'Left':'Right'),toPole=swapSide(fromPole);
 if(frame.poles?.[fromPole])frame.poles[toPole]=localMirror(frame.poles[fromPole]);
 else if(frame.poles)delete frame.poles[toPole];
 frame[key]??={};
 if(frame[key][source])frame[key][dest]=mirrorControls(frame[key][source]);
 else delete frame[key][dest];
 for(const suffix of SIDE_ARTICULATION[kind]){
   const from=source+suffix,to=dest+suffix;
   if(frame[from]!=null)frame[to]=frame[from];else delete frame[to];
 }
 if(kind==='arm'){if(frame[source+'HandCurl']!=null)frame[dest+'HandCurl']=frame[source+'HandCurl'];else delete frame[dest+'HandCurl'];}
 if(kind==='leg'){
   frame.footPitch??={};
   if(frame.footPitch[source]!=null)frame.footPitch[dest]=frame.footPitch[source];
   else delete frame.footPitch[dest];
 }
 return frame;
}
// A mirror is a discrete construction operation, not an animation through the
// centre of the body. Compare reflected collision identities with the source.
function mirrorIssue(source,result){
 const baseline=new Map();
 for(const [id,depth] of Collision3D.overlaps(source)){
   let mirrored=swapSide(id);
   if(mirrored.startsWith('body:'))mirrored='body:'+mirrored.slice(5).split('/').sort().join('/');
   baseline.set(mirrored,depth);
 }
 return Collision3D.worse(Collision3D.overlaps(result),baseline,1e-6);
}
// Apply mirrored *deltas*, not absolute positions, to preserve the other limb's
// existing pose when symmetric editing is enabled.
function mirrorTargetDelta(frame,before,kind,side){
 const other=swapSide(side),delta=sub(frame[kind][side],before[kind][side]);
 const mirrored=rotate(reflectX(unrotate(delta,before.euler)),before.euler);
 frame[kind][other]=add(before[kind][other],mirrored);
}
const ANGLE_LIMITS={arm:{flexion:[-.52,3.05],abduction:[0,2.70],elbowFlexion:[0,2.62],wristFlexion:[-1.04,1.04],forearmRotation:[-1.57,1.57],axialRotation:[-Math.PI/2,Math.PI/2]},leg:{flexion:[-.3,2.36],abduction:[-.7,.7],kneeFlexion:[0,2.36],axialRotation:[-Math.PI/2,Math.PI/2]}};
function editLimbAngle(frame,kind,side,field,value,symmetric=false){
 const controls=frame[modeKeys[kind]],other=swapSide(side),limits=ANGLE_LIMITS[kind][field];
 const old=controls[side][field]||0;
 let delta=clamp(value,...limits)-old;
 if(symmetric){
   const sign=['axialRotation','forearmRotation'].includes(field)?-1:1;
   const otherOld=controls[other][field]||0;
   const lo=(limits[0]-otherOld)*sign,hi=(limits[1]-otherOld)*sign;
   delta=clamp(delta,Math.min(lo,hi),Math.max(lo,hi));
   controls[other][field]=otherOld+sign*delta;
 }
 controls[side][field]=old+delta;
}
const PHASE_TYPES=['start','movement','hold'];
function phaseTiming(frame){
 return {transition:frame.transitionSeconds??1.2,hold:frame.holdSeconds??(frame.phaseType==='hold'?2:0)};
}
function validPhaseTiming(frame){
 return (frame.phaseType==null||PHASE_TYPES.includes(frame.phaseType))&&
   (frame.transitionSeconds==null||(Number.isFinite(frame.transitionSeconds)&&frame.transitionSeconds>=.1&&frame.transitionSeconds<=30))&&
   (frame.holdSeconds==null||(Number.isFinite(frame.holdSeconds)&&frame.holdSeconds>=0&&frame.holdSeconds<=120));
}
// Time is in milliseconds of the authored sequence, independent of playback speed.
function previewPlan(frames,loop=false,check=Collision3D.previewTransition){
 if(!frames.length||frames.some(f=>!validPhaseTiming(f)))return {ok:false,issue:'timing'};
 const segments=[];let duration=0;
 const append=(segment,seconds)=>{if(seconds<=0)return;const ms=seconds*1000;segments.push({...segment,start:duration,end:duration+ms});duration+=ms;};
 for(let i=0;i<frames.length;i++){
   const timing=phaseTiming(frames[i]);
   append({kind:'hold',from:i,to:i,samples:[cloneFrame(frames[i])]},timing.hold);
   const to=i+1<frames.length?i+1:loop&&frames.length>1?0:null;
   if(to===null)continue;
   const result=check(frames[i],frames[to]);
   if(!result.ok)return {ok:false,issue:result.issue,from:i,to};
   append({kind:'move',from:i,to,samples:result.samples},timing.transition);
 }
 if(duration===0)return {ok:false,issue:'empty'};
 return {ok:true,segments,duration,loop};
}
function previewAt(plan,elapsed){
 const finished=!plan.loop&&elapsed>=plan.duration;
 const time=plan.loop?Math.max(0,elapsed)%plan.duration:clamp(elapsed,0,plan.duration);
 const segment=plan.segments.find(s=>time<s.end)||plan.segments.at(-1);
 const progress=clamp((time-segment.start)/(segment.end-segment.start),0,1);
 const index=Math.min(segment.samples.length-1,Math.floor(progress*(segment.samples.length-1)));
 return {frame:segment.samples[index],from:segment.from,to:segment.to,kind:segment.kind,finished,progress:time/plan.duration};
}
// Lightweight, deterministic thumbnails based on the actual joint geometry.
// Shared bounds across all frames prevent apparent scale jumps.
function phaseThumbnailData(frames){
 const basis=cameraBasis({azimuth:.58,elevation:.20});
 const projected=p=>[dot(p,basis.right),-dot(p,basis.up)];
 const result=frames.map(frame=>{
   const j=joints(frame),lines=[[j.root,j.spineMid],[j.spineMid,j.spineUpper],[j.spineUpper,j.neck],[j.neck,j.head]];
   for(const side of ['left','right']){
     lines.push([j.spineUpper,j.shoulder[side]],[j.shoulder[side],j.elbow[side]],[j.elbow[side],j.hands[side]],[j.hands[side],j.palms[side]],
       [j.root,j.hip[side]],[j.hip[side],j.knee[side]],[j.knee[side],j.feet[side]],[j.feet[side],j.toes[side]]);
   }
   return {lines:lines.map(l=>l.map(projected)),head:projected(j.head)};
 });
 const points=result.flatMap(r=>r.lines.flat());
 if(!points.length)return [];
 const xs=points.map(p=>p[0]),ys=points.map(p=>p[1]);
 const minX=Math.min(...xs)-.16,maxX=Math.max(...xs)+.16,minY=Math.min(...ys)-.18,maxY=Math.max(...ys)+.18;
 const scale=Math.min(84/Math.max(.01,maxX-minX),70/Math.max(.01,maxY-minY));
 const map=p=>[50+(p[0]-(minX+maxX)/2)*scale,43+(p[1]-(minY+maxY)/2)*scale];
 return result.map(r=>({lines:r.lines.map(l=>l.map(map)),head:map(r.head),headRadius:.13*scale}));
}
function movePhase(frames,from,to){
 if(!Number.isInteger(from)||!Number.isInteger(to)||from<0||to<0||from>=frames.length||to>=frames.length)return false;
 if(from===to)return false;
 frames.splice(to,0,frames.splice(from,1)[0]);return true;
}
function copyIssue(source,result){
 const collision=Collision3D.worse(Collision3D.overlaps(result),Collision3D.overlaps(source),1e-6);
 if(collision)return collision;
 const before=lockErrors(joints(source),source.pointLocks),after=lockErrors(joints(result),source.pointLocks);
 for(const [key,d] of after)if(d>Math.max(.005,before.get(key)||0)+1e-7)return {id:'lock:'+key,depth:d};
 return null;
}
function prepareAngleMode(frame,kind,side){
 const key=modeKeys[kind];
 if(frame[key]?.[side])return true;
 const fit=inferAngles(frame,kind,side);
 if(!fit||fit.error>1e-5)return false;
 frame[key]??={};frame[key][side]=fit.angles;
 return true;
}
function prepareTargetMode(frame,kind,side){
 const key=modeKeys[kind];
 if(!frame[key]?.[side])return true;
 const j=joints(frame),candidate=cloneFrame(frame),target=kind==='arm'?'hands':'feet';
 const middle=kind==='arm'?'elbow':'knee',pole=middle+(side==='left'?'Left':'Right');
 candidate[target][side]=[...j[target][side]];
 candidate.poles??={};candidate.poles[pole]=[...j[middle][side]];
 delete candidate[key][side];
 if(limbError(j,joints(candidate),kind,side)>1e-5){
   // The actual elbow/knee vector may lie outside the historical pole cone
   // even when its bend plane has a valid pole inside that cone.
   const anchor=j[kind==='arm'?'shoulder':'hip'][side];
   candidate.poles[pole]=constrainedPole(anchor,j[target][side],j[middle][side],j.hingePoles[kind][side]);
   if(limbError(j,joints(candidate),kind,side)>1e-5)return false;
 }
 frame[target]=candidate[target];frame.poles=candidate.poles;frame[key]=candidate[key];
 return true;
}
// Approximate 3D contact solids: sphere-sampled bone capsules against planes,
// boxes and spheres. This is an editor safety guard, NOT a continuous rigid-body
// simulation. Existing historical poses may have errors; edits cannot worsen
// those errors and cannot introduce new crossings.
// World-space locks record actual rendered joint positions, not raw IK targets.
const LOCK_KEYS=['root','leftHand','rightHand','leftFoot','rightFoot','leftKnee','rightKnee','leftElbow','rightElbow'];
function lockPoint(j,key){
 if(key==='root')return j.root;
 const side=key.startsWith('left')?'left':'right';
 const kind=key.endsWith('Hand')?'hands':key.endsWith('Foot')?'feet':key.endsWith('Knee')?'knee':'elbow';
 return j[kind][side];
}
function setPointLock(frame,key,enabled){
 if(!LOCK_KEYS.includes(key))throw Error('Érvénytelen rögzítési pont.');
 if(enabled){frame.pointLocks??={};frame.pointLocks[key]=[...lockPoint(joints(frame),key)];}
 else if(frame.pointLocks){delete frame.pointLocks[key];if(!Object.keys(frame.pointLocks).length)delete frame.pointLocks;}
}
function lockErrors(j,locks){
 const result=new Map();
 for(const [key,position] of Object.entries(locks||{})){
   if(!LOCK_KEYS.includes(key))continue;
   const d=len(sub(lockPoint(j,key),position));
   if(d>0)result.set(key,d);
 }
 return result;
}
const Collision3D=(()=>{
 const lerp=(a,b,t)=>add(a,mul(sub(b,a),t));
 function bodyProbes(j){
   const out=[];
   const point=(id,p,r)=>out.push({id,p,r});
   const chain=(id,a,b,r)=>{
     for(let i=0;i<=6;i++)point(id+'-'+i,lerp(a,b,i/6),r);
   };
   chain('torso1',j.root,j.spineMid,.135);chain('torso2',j.spineMid,j.spineUpper,.15);
   chain('neck',j.neck,j.head,.07);point('head',j.head,.12);
   for(const side of ['left','right']){
     chain('upperArm'+side,j.shoulder[side],j.elbow[side],.071);
     chain('forearm'+side,j.elbow[side],j.hands[side],.052);
     chain('thigh'+side,j.hip[side],j.knee[side],.095);
     chain('shin'+side,j.knee[side],j.feet[side],.065);
     chain('foot'+side,j.feet[side],j.toes[side],.041);
     point('palm'+side,j.hands[side],.039);
     const detail=j.handDetails?.[side];
     if(detail){
       point('palmDetail'+side,detail.center,.038);
       for(const [i,finger] of [...detail.fingers,detail.thumb].entries())for(let k=1;k<finger.length;k++){
         point('finger'+side+i+'-'+k,finger[k],.010);
         point('fingerMid'+side+i+'-'+k,lerp(finger[k-1],finger[k],.5),.010);
       }
     }
     point('ankle'+side,j.feet[side],.048);
   }
   return out;
 }
 function solids(frame){
   const out=[{id:'padló',type:'floor',height:0}];
   const sizes={wall:[1.24,1.1,.025],step:[.43,.13,.28],block:[.22,.09,.14],roller:[.28,.14,.14],balancePad:[.27,.04,.21]};
   for(const [index,q] of (frame.equipment||[]).entries()){
     const id=q.type+index,centre=propPosition(q),yaw=propYaw(q);
     if(sizes[q.type])out.push({id,type:'box',centre,half:sizes[q.type],yaw});
     if(q.type==='chair'){
       out.push({id:id+'-ülőlap',type:'box',centre,half:[.43,.075,.42],yaw});
       out.push({id:id+'-háttámla',type:'box',centre:propTransform(q,[0,.32,-.33]),half:[.43,.35,.07],yaw});
     }
     if(q.type==='ball'||q.type==='softBall')out.push({id,type:'sphere',centre,radius:.34});
   }
   return out;
 }
 function signedBox(p,c,h){
   const q=p.map((x,i)=>Math.abs(x-c[i])-h[i]);
   return len(q.map(v=>Math.max(v,0)))+Math.min(Math.max(...q),0);
 }
 function penetration(v,o){
   if(o.type==='floor')return Math.max(0,o.height-(v.p[1]-v.r));
   if(o.type==='mat'){
     const p=v.p, c=o.centre;
     if(Math.abs(p[0]-c[0])>o.half[0]+v.r||Math.abs(p[2]-c[2])>o.half[1]+v.r)return 0;
     // A thin contact mat may be compressed but the floor beneath may not.
     return Math.max(0,Math.min(.03,o.height-(p[1]-v.r)));
   }
   if(o.type==='sphere')return Math.max(0,v.r+o.radius-len(sub(v.p,o.centre)));
   if(o.type==='box')return Math.max(0,v.r-signedBox(rotY(sub(v.p,o.centre),-(o.yaw||0)),[0,0,0],o.half));
   return 0;
 }
 // Exact distance between closed line segments, including point segments.
 function segmentDistance(a,b,c,d){
   const u=sub(b,a),v=sub(d,c),w=sub(a,c),uu=dot(u,u),vv=dot(v,v),uv=dot(u,v);
   const uw=dot(u,w),vw=dot(v,w),den=uu*vv-uv*uv;
   const distance=(s,t)=>len(add(w,sub(mul(u,s),mul(v,t))));
   let best=Math.min(distance(0,clamp(vw/Math.max(vv,1e-12),0,1)),
     distance(1,clamp((vw+uv)/Math.max(vv,1e-12),0,1)),
     distance(clamp(-uw/Math.max(uu,1e-12),0,1),0),
     distance(clamp((uv-uw)/Math.max(uu,1e-12),0,1),1));
   if(den>1e-12){
     const s=(uv*vw-vv*uw)/den,t=(uu*vw-uv*uw)/den;
     if(s>=0&&s<=1&&t>=0&&t<=1)best=Math.min(best,distance(s,t));
   }
   return best;
 }
 // Conservative interior capsules leave room for natural soft-tissue contact.
 function bodySegments(j){
   const out=[];
   const seg=(id,a,b,r)=>out.push({id,a,b,r});
   if(j.articulatedSpine){seg('torso',lerp(j.root,j.spineMid,.15),j.spineMid,.125);seg('torso',j.spineMid,j.spineUpper,.125);}
   else seg('torso',lerp(j.root,j.spineMid,.15),j.spineUpper,.125);
   seg('head',j.head,j.head,.105);
   for(const side of ['left','right']){
     seg('upperArm-'+side,lerp(j.shoulder[side],j.elbow[side],.28),j.elbow[side],.062);
     seg('forearm-'+side,j.elbow[side],j.hands[side],.048);
     const detail=j.handDetails?.[side];
     if(detail){
       seg('hand-'+side,j.hands[side],add(j.hands[side],mul(detail.forward,.086)),.035);
       for(const finger of [...detail.fingers,detail.thumb])for(let k=1;k<finger.length;k++)seg('hand-'+side,finger[k-1],finger[k],.010);
     }else seg('hand-'+side,j.hands[side],j.palms[side],.035);
     seg('thigh-'+side,lerp(j.hip[side],j.knee[side],.22),j.knee[side],.081);
     seg('shin-'+side,j.knee[side],j.feet[side],.055);
     seg('foot-'+side,j.feet[side],j.toes[side],.038);
   }
   return out;
 }
 function adjacent(a,b){
   const [x,s]=a.split('-'),[y,t]=b.split('-');
   if(s!==t||!s)return false;
   return ['forearm/upperArm','forearm/hand','shin/thigh','foot/shin'].includes([x,y].sort().join('/'));
 }
 function selfOverlaps(segments){
   const out=new Map();
   for(let i=0;i<segments.length;i++)for(let k=i+1;k<segments.length;k++){
     const a=segments[i],b=segments[k];
     if(a.id===b.id||adjacent(a.id,b.id))continue;
     const r=a.r+b.r-.014;
     if(r<=0||a.a.some((_,n)=>Math.min(a.a[n],a.b[n])-Math.max(b.a[n],b.b[n])>r||
       Math.min(b.a[n],b.b[n])-Math.max(a.a[n],a.b[n])>r))continue;
     const depth=r-segmentDistance(a.a,a.b,b.a,b.b);
     if(depth>.003){const id='body:'+ [a.id,b.id].sort().join('/');out.set(id,Math.max(depth,out.get(id)||0));}
   }
   return out;
 }
 function overlaps(frame,j=joints(frame)){
   const out=selfOverlaps(bodySegments(j));
   const obstacles=solids(frame);
   for(const p of bodyProbes(j))for(const o of obstacles){
     const d=penetration(p,o);
     if(d>.003)out.set(p.id+'@'+o.id,d);
   }
   return out;
 }
 // The baseline is deliberately used: old saved poses are not automatically
 // moved by a new software version. New penetration or deeper penetration is
 // blocked, and every intermediate path position is checked.
 function worse(now,baseline,epsilon=.004){
   for(const [id,depth] of now){
     const margin=id.startsWith('body:')?1e-7:epsilon;
     if(depth>(baseline.get(id)||0)+margin)return {id,depth};
   }
   return null;
 }
 function interpolate(a,b,t){
   if(typeof b==='number')return a===b?b:(typeof a==='number'?a:0)*(1-t)+b*t;
   if(Array.isArray(b))return b.map((x,i)=>i<(Array.isArray(a)?a.length:0)?interpolate(a[i],x,t):x);
   if(b&&typeof b==='object'){
     const out={};
     for(const k of Object.keys(b))out[k]=neutralNumericKey(k)&&b[k]==null&&typeof a?.[k]==='number'&&t<1?a[k]*(1-t):interpolate(a?.[k],b[k],t);
     for(const k of Object.keys(a||{}))if(!(k in b)&&t<1)
       out[k]=neutralNumericKey(k)&&typeof a[k]==='number'?a[k]*(1-t):a[k];
     return out;
   }
   return b;
 }
 function transition(before,after,steps=28,adapt=false){
   const from=joints(before),to=joints(after);
   const baseline=overlaps(before,from);
   const locks=before.pointLocks||{},baseLockErrors=lockErrors(from,locks);
   let travel=0;
   const keys=['root','neck','head','spineUpper'];
   for(const key of keys)travel=Math.max(travel,len(sub(from[key],to[key])));
   for(const key of ['hands','feet','elbow','knee','toes'])for(const side of ['left','right'])travel=Math.max(travel,len(sub(from[key][side],to[key][side])));
   for(const [i,prop] of (before.equipment||[]).entries()){
     const target=after.equipment?.[i];
     if(!target||target.type!==prop.type||!PROP_POSITIONS[prop.type])continue;
     travel=Math.max(travel,len(sub(propPosition(prop),propPosition(target)))+Math.abs(propYaw(prop)-propYaw(target))*1.3);
   }
   steps=Math.min(256,Math.max(steps,Math.ceil(travel/.027)));
   let accepted=before,ratio=0,issue=null;
   // A swept edit is never allowed to jump through solid surfaces even if its
   // final location is beyond them. Cumulative stepping is robust on mobile.
   for(let i=1;i<=steps;i++){
     const f=interpolate(before,after,i/steps);if(adapt)adaptSupportPoints(f,locks,{reference:accepted,source:before,projectRoot:adapt==='root'});const j=joints(f);
     let err=worse(overlaps(f,j),baseline);
     if(!err)for(const [key,depth] of lockErrors(j,locks)){
       if(depth>Math.max(.005,baseLockErrors.get(key)||0)+1e-7){err={id:'lock:'+key,depth};break;}
     }
     if(err){issue=err;break;}
     ratio=i/steps;accepted=f;
   }
   return {frame:accepted,fraction:ratio,limited:ratio<1,issue:issue?.id||null,rootAdjusted:adapt==='root'&&ratio>0&&len(sub(accepted.root,interpolate(before.root,after.root,ratio)))>.001};
 }
 // Preview only interpolates matching rig representations. A change of IK/FK,
 // attachment topology or lock definition needs an explicit intermediate pose.
 function shape(v){
   if(Array.isArray(v))return v.map(shape);
   if(v&&typeof v==='object')return Object.fromEntries(Object.keys(v).sort().map(k=>[k,shape(v[k])]));
   return typeof v==='number'?'number':v;
 }
 // Resolve different representation modes on temporary copies only.
 // Prefer the already authored FK mode; otherwise use exact IK conversion.
 // Even optional fields (notably legacy leg axial rotation) must be checked
 // against the rendered endpoints before normalizing their shape.
 function normalizePreview(a,b){
   const x=cloneFrame(a),y=cloneFrame(b),ja=joints(a),jb=joints(b);
   for(const kind of ['arm','leg'])for(const side of ['left','right']){
     const key=modeKeys[kind],target=kind==='arm'?'hands':'feet',middle=kind==='arm'?'elbow':'knee';
     const pole=middle+(side==='left'?'Left':'Right');
     const lx=x[key]?.[side],ly=y[key]?.[side];
     if(!lx&&!ly)continue;
     const candidates=[];
     // Work one limb at a time; failed conversions never affect the originals.
     for(const mode of ['angle','target']){
       const u=cloneFrame(x),v=cloneFrame(y);
       const convert=mode==='angle'?prepareAngleMode:prepareTargetMode;
       if(!convert(u,kind,side)||!convert(v,kind,side))continue;
       if(mode==='angle'){
         for(const q of [u,v]){
           const control=q[key][side];
           if(kind==='arm')for(const k of ['wristFlexion','forearmRotation','axialRotation'])control[k]??=0;
         }
         if(kind==='leg'&&((u[key][side].axialRotation==null)!==(v[key][side].axialRotation==null))){
           for(const q of [u,v])if(q[key][side].axialRotation==null)q[key][side].axialRotation=0;
         }
         if(JSON.stringify(shape(u[key][side]))!==JSON.stringify(shape(v[key][side])))continue;
       }
       // Poles and unused targets are inert in FK. Fill missing poles without
       // introducing a jump; the geometry comparison below is authoritative.
       for(const [q,j] of [[u,ja],[v,jb]]){
         q.poles??={};q.poles[pole]??=[...j[middle][side]];
         if(mode==='angle')q[target][side]=[...j[target][side]];
       }
       if(limbError(ja,joints(u),kind,side)>1e-5||limbError(jb,joints(v),kind,side)>1e-5)continue;
       candidates.push([u,v]);break;
     }
     if(!candidates.length)return null;
     Object.assign(x,candidates[0][0]);Object.assign(y,candidates[0][1]);
   }
   return previewCompatible(x,y)?[x,y]:null;
 }
 function previewCompatible(a,b){
   for(const key of ['armAngles','legAngles','footPitch','poles','equipment'])
     if(JSON.stringify(shape(a[key]??{}))!==JSON.stringify(shape(b[key]??{})))return false;
   return JSON.stringify(a.pointLocks??{})===JSON.stringify(b.pointLocks??{});
 }
 function previewTransition(a,b){
   const normalized=previewCompatible(a,b)?[a,b]:normalizePreview(a,b);
   if(!normalized)return {ok:false,issue:'representation'};
   const [from,to]=normalized;
   const baseline=overlaps(a);
   for(const [id,depth] of overlaps(b))baseline.set(id,Math.max(depth,baseline.get(id)||0));
   const locks=a.pointLocks||{},start=lockErrors(joints(a),locks),end=lockErrors(joints(b),locks);
   const samples=[];
   // Precompute the exact samples that will be displayed. Never render unchecked
   // intermediate poses. Fixed sampling is approximate, not continuous proof.
   for(let i=0;i<=48;i++){
     const frame=i===0?cloneFrame(a):i===48?cloneFrame(b):interpolate(from,to,i/48);
     if(i>0&&i<48&&Object.keys(locks).length)adaptSupportPoints(frame,locks,{reference:samples.at(-1)});
     const j=joints(frame);
     const err=worse(overlaps(frame,j),baseline);
     if(err)return {ok:false,issue:err.id};
     for(const [key,d] of lockErrors(j,locks))if(d>Math.max(.005,start.get(key)||0,end.get(key)||0)+1e-7)return {ok:false,issue:'lock:'+key};
     samples.push(frame);
   }
   return {ok:true,samples};
 }
 function report(frame){const issues=overlaps(frame);return {count:issues.size,examples:[...issues.entries()].sort((a,b)=>b[1]-a[1]).slice(0,5).map(([name,depth])=>({name,depth:+depth.toFixed(4)}))};}
 return {bodyProbes,bodySegments,selfOverlaps,segmentDistance,solids,overlaps,transition,previewTransition,normalizePreview,interpolate,report,worse};
})();

function mul4(a,b){let c=new Float32Array(16);for(let i=0;i<4;i++)for(let k=0;k<4;k++)for(let j=0;j<4;j++)c[j*4+i]+=a[k*4+i]*b[j*4+k];return c;}
const VIEW_FOV=Math.PI/4;
function ortho(halfHeight,aspect,n=.04,f=90){
 const a=new Float32Array(16);a[0]=1/(halfHeight*aspect);a[5]=1/halfHeight;a[10]=-2/(f-n);a[14]=-(f+n)/(f-n);a[15]=1;return a;
}
const CAMERA_VIEWS={front:[0,0],side:[Math.PI/2,0],leftView:[-Math.PI/2,0],backView:[Math.PI,0],topView:[0,Math.PI/2-.001],bottomView:[0,-Math.PI/2+.001],threequarter:[.66,.33]};
function setCameraView(camera,name){
 const angles=CAMERA_VIEWS[name];if(!angles)return false;
 [camera.azimuth,camera.elevation]=angles;return true;
}
function cameraViewName(camera){
 const direction=cameraBasis(camera).forward;
 return Object.keys(CAMERA_VIEWS).find(name=>dot(direction,cameraBasis({azimuth:CAMERA_VIEWS[name][0],elevation:CAMERA_VIEWS[name][1]}).forward)>.9998)||null;
}
// Ratio refers to the model viewport, not to the inspector. A smaller
// landscape/portrait viewport has a higher minimum to keep the model visible.
function workspaceShare(value,vertical){return clamp(Number.isFinite(value)?value:vertical?56:60,vertical?50:38,vertical?75:72);}
function skeletonSegments(j){
 const segments=[[j.root,j.spineMid],[j.spineMid,j.spineUpper],[j.spineUpper,j.neck],[j.neck,j.head]];
 for(const side of ['left','right']){
   segments.push([j.spineUpper,j.shoulder[side]],[j.shoulder[side],j.elbow[side]],[j.elbow[side],j.hands[side]],[j.root,j.hip[side]],[j.hip[side],j.knee[side]],[j.knee[side],j.feet[side]],[j.feet[side],j.toes[side]]);
   const detail=j.handDetails?.[side];
   if(detail){
     segments.push([j.hands[side],detail.center]);
     for(const finger of [...detail.fingers,detail.thumb]){
       segments.push([detail.center,finger[0]]);
       for(let k=1;k<finger.length;k++)segments.push([finger[k-1],finger[k]]);
     }
   }else segments.push([j.hands[side],j.palms[side]],[j.hands[side],j.thumbs[side]]);
 }
 return segments;
}
function cameraBasis(c){
 const a=c.azimuth,e=c.elevation;
 return {right:[Math.cos(a),0,-Math.sin(a)],up:[-Math.sin(a)*Math.sin(e),Math.cos(e),-Math.cos(a)*Math.sin(e)],forward:[Math.sin(a)*Math.cos(e),Math.sin(e),Math.cos(a)*Math.cos(e)]};
}
function pixelScale(c,height){return 2*c.distance*Math.tan(VIEW_FOV/2)/Math.max(1,height);}
function panCamera(c,dx,dy,height){
 const {right,up}=cameraBasis(c),s=pixelScale(c,height);
 c.target=add(c.target,add(mul(right,-dx*s),mul(up,dy*s))).map(v=>clamp(v,-10,10));return c;
}
function gestureMetrics(points){
 const [a,b]=points;return {x:(a.x+b.x)/2,y:(a.y+b.y)/2,distance:Math.max(1,Math.hypot(a.x-b.x,a.y-b.y))};
}
function pinchCamera(c,previous,next,height){
 c.distance=clamp(c.distance*previous.distance/next.distance,1.2,10);
 return panCamera(c,next.x-previous.x,next.y-previous.y,height);
}
// Convert collision identifiers to body-space geometry for visual feedback.
function issueGeometry(frame,j,id){
 if(!id)return [];
 if(id.startsWith('lock:')){
   const key=id.slice(5);
   return LOCK_KEYS.includes(key)?[{a:lockPoint(j,key),b:lockPoint(j,key),r:.065}]:[];
 }
 if(id.startsWith('body:')){
   const parts=id.slice(5).split('/');
   return Collision3D.bodySegments(j).filter(s=>parts.includes(s.id));
 }
 const probe=Collision3D.bodyProbes(j).find(p=>p.id===id.split('@')[0]);
 return probe?[{a:probe.p,b:probe.p,r:Math.max(.045,probe.r)}]:[];
}
function perspective(fov,aspect,n,f){let t=1/Math.tan(fov/2),a=new Float32Array(16);a[0]=t/aspect;a[5]=t;a[10]=-(f+n)/(f-n);a[11]=-1;a[14]=-2*f*n/(f-n);return a;}
function lookAt(eye,at){let z=norm(sub(eye,at)),x=norm(cross([0,1,0],z)),y=cross(z,x);return new Float32Array([x[0],y[0],z[0],0,x[1],y[1],z[1],0,x[2],y[2],z[2],0,-dot(x,eye),-dot(y,eye),-dot(z,eye),1]);}
function model(centre,xAxis,yAxis,zAxis){return new Float32Array([xAxis[0],xAxis[1],xAxis[2],0,yAxis[0],yAxis[1],yAxis[2],0,zAxis[0],zAxis[1],zAxis[2],0,...centre,1]);}
function segmentM(a,b,rX,rZ){let y=sub(b,a),l=len(y);if(l<1e-5)return null;y=norm(y);let tmp=Math.abs(dot(y,[0,0,1]))>.96?[1,0,0]:[0,0,1];let x=norm(cross(y,tmp)),z=norm(cross(x,y));return model(mul(add(a,b),.5),mul(x,rX),mul(y,l*.55),mul(z,rZ));}
function sphereM(pos,size){return model(pos,[size[0],0,0],[0,size[1],0],[0,0,size[2]]);}
const BAND_ANCHORS=['leftHand','rightHand','leftAnkle','rightAnkle','leftKnee','rightKnee','leftToe','rightToe','floorLeft','floorRight','fixed'];
// Move the root in world coordinates. IK endpoints retain their positions;
// FK limbs follow the root. The swept collision guard owns final acceptance.
const BODY_TARGETS=[
 {key:'root',kind:'root'},{key:'head',kind:'articulate',secondary:true},{key:'neck',kind:'articulate',secondary:true},{key:'chest',kind:'articulate',secondary:true},{key:'waist',kind:'articulate',secondary:true},
 ...['left','right'].flatMap(side=>[{key:side+'Shoulder',kind:'articulate',secondary:true,side},{key:side+'Hip',kind:'articulate',secondary:true,side},{key:side+'Elbow',kind:'elbow',side},{key:side+'Hand',kind:'hands',side},{key:side+'Knee',kind:'knee',side},{key:side+'Foot',kind:'feet',side},{key:side+'Toe',kind:'articulate',secondary:true,side}])];
function bodyTarget(key){return BODY_TARGETS.find(t=>t.key===key)||null;}
function bodyPoint(j,key){
 const central={root:'root',head:'head',neck:'neck',chest:'spineUpper',waist:'spineMid'};
 if(central[key])return j[central[key]];
 const side=key.startsWith('left')?'left':'right',name={Shoulder:'shoulder',Hip:'hip',Elbow:'elbow',Hand:'hands',Knee:'knee',Foot:'feet',Toe:'toes'}[key.slice(side.length)];
 return name?j[name][side]:null;
}
// Keep the endpoint and bone lengths; change the bend plane within the
// existing anatomical pole restrictions.
function moveBendPoint(frame,kind,side,delta){
 const j=joints(frame),arm=kind==='elbow',anchor=arm?j.shoulder[side]:j.hip[side];
 const end=arm?j.hands[side]:j.feet[side],mid=j[kind][side],axis=norm(sub(end,anchor));
 const perpendicular=v=>sub(v,mul(axis,dot(v,axis)));
 const desired=perpendicular(sub(add(mid,delta),anchor));
 if(len(desired)<1e-6)return false;
 const pole=kind+(side==='left'?'Left':'Right'),along=dot(sub(mid,anchor),axis);
 frame.poles[pole]=add(anchor,add(mul(axis,along),mul(norm(desired),len(perpendicular(sub(mid,anchor))))));
 return true;
}
function mirrorBendDelta(frame,before,kind,side){
 const other=swapSide(side),key=kind+(side==='left'?'Left':'Right'),dest=kind+(other==='left'?'Left':'Right');
 if(!frame.poles[key]||!before.poles[key]||!before.poles[dest])return;
 const delta=sub(frame.poles[key],before.poles[key]);
 frame.poles[dest]=add(before.poles[dest],rotate(reflectX(unrotate(delta,before.euler)),before.euler));
}
// Prepare equivalent IK representations only for locked support endpoints.
// Support constraints use the existing rigid two-bone chains. A locked
// middle joint is a sphere constraint on its proximal joint; an endpoint
// lock is a reach-shell constraint. No stored lock is moved by this solver.
const SUPPORT_TOLERANCE=.005;
const SUPPORT_CHAINS=[
 {kind:'arm',target:'hands',middle:'elbow',anchor:'shoulder',endKey:'Hand',midKey:'Elbow',upper:bones.armUpper,lower:bones.armLower,maxBend:2.60},
 {kind:'leg',target:'feet',middle:'knee',anchor:'hip',endKey:'Foot',midKey:'Knee',upper:bones.thigh,lower:bones.shin,maxBend:2.43}
];
function supportChainConstraints(frame,locks){
 const j=joints(frame),constraints=[];
 for(const side of ['left','right'])for(const chain of SUPPORT_CHAINS){
   const anchor=j[chain.anchor][side],mid=locks?.[side+chain.midKey],end=locks?.[side+chain.endKey];
   if(mid)constraints.push({anchor,point:mid,min:chain.upper,max:chain.upper,key:side+chain.midKey});
   if(end){
     const min=Math.max(Math.abs(chain.upper-chain.lower)+.015,Math.sqrt(Math.max(0,chain.upper**2+chain.lower**2+2*chain.upper*chain.lower*Math.cos(chain.maxBend))));
     // The target-mode solver has a small straight-limb margin; preserve
     // the full length for an existing FK limb, where straight is valid.
     const max=chain.upper+chain.lower-(frame[modeKeys[chain.kind]]?.[side]?0:.004);
     constraints.push({anchor,point:end,min,max,key:side+chain.endKey});
   }
 }
 return constraints;
}
function projectSupportRoot(frame,locks,source,maxCorrection=.08){
 if(locks?.root||!source||!Object.keys(locks||{}).length)return frame;
 const desired=[...frame.root],travel=len(sub(desired,source.root));
 if(travel<1e-8)return frame;
 const limit=Math.min(maxCorrection,travel+.001),constraints=supportChainConstraints(frame,locks);
 let correction=[0,0,0];
 const residual=delta=>constraints.reduce((sum,c)=>{
   const d=len(sub(add(c.anchor,delta),c.point)),error=d-clamp(d,c.min,c.max);
   return sum+error*error;
 },0);
 let best=[...correction],score=residual(best);
 // Alternating projections find a nearby point satisfying the intersection
 // of the support spheres/shells. Bound the correction to the requested drag.
 for(let pass=0;pass<16&&score>1e-8;pass++){
   for(const c of constraints){
     const v=sub(add(c.anchor,correction),c.point),d=len(v);
     if(d<1e-8)continue;
     const error=clamp(d,c.min,c.max)-d;
     if(Math.abs(error)<.0001)continue;
     correction=add(correction,mul(v,error/d));
     if(len(correction)>limit)correction=mul(norm(correction),limit);
     correction=sub(add(desired,correction).map((v,i)=>clamp(v,i===1?0:-3,3)),desired);
   }
   const next=residual(correction);
   if(next<score){score=next;best=[...correction];}
 }
 frame.root=add(desired,best);return frame;
}
function prepareSupportEdit(frame){
 for(const side of ['left','right'])for(const chain of SUPPORT_CHAINS){
   const point=frame.pointLocks?.[side+chain.endKey],mid=frame.pointLocks?.[side+chain.midKey];
   if(!point&&!mid)continue;
   // Keep FK if conversion cannot preserve the original rendered geometry.
   if(!prepareTargetMode(frame,chain.kind,side))continue;
   if(!point)continue;
   const original=cloneFrame(frame),j=joints(frame);
   frame[chain.target][side]=[...point];
   if(limbError(j,joints(frame),chain.kind,side)>1e-5)frame[chain.target]=original[chain.target];
 }
}
// Find a pole in the requested bend plane that still satisfies the existing
// anatomical cone. If no such pole exists, the swept guard rejects the move.
function constrainedPole(anchor,end,mid,anatomic){
 const axis=norm(sub(end,anchor)),v=sub(mid,anchor);
 const plane=sub(v,mul(axis,dot(v,axis)));
 if(len(plane)<1e-7)return anatomic;
 const direction=norm(plane),ref=norm(sub(anatomic,anchor));
 const axial=dot(ref,axis),lateral=Math.max(0,dot(ref,direction));
 const q=add(mul(axis,axial),mul(direction,Math.max(1e-4,lateral)));
 if(len(q)<1e-7||dot(norm(q),ref)<.87)return mid;
 return add(anchor,mul(norm(q),.5));
}
function adaptSupportPoints(frame,locks,options={}){
 if(!Object.keys(locks||{}).length)return frame;
 if(options.projectRoot)projectSupportRoot(frame,locks,options.source);
 const reference=joints(options.reference||frame);
 for(const side of ['left','right'])for(const c of SUPPORT_CHAINS){
   const endpoint=locks?.[side+c.endKey],mid=locks?.[side+c.midKey];
   if(!endpoint&&!mid)continue;
   if(frame[modeKeys[c.kind]]?.[side])continue; // incompatible FK remains protected
   const pole=c.middle+(side==='left'?'Left':'Right');
   if(endpoint)frame[c.target][side]=[...endpoint];
   if(!mid)continue;
   const j=joints(frame);
   if(len(sub(j[c.middle][side],mid))<=.001)continue;
   const anchor=j[c.anchor][side],upper=sub(mid,anchor);
   // The proximal joint must lie on the fixed-length sphere. Don't invent
   // an unreachable bend or stretch a segment to satisfy a lock.
   if(Math.abs(len(upper)-c.upper)>SUPPORT_TOLERANCE)continue;
   if(!endpoint){
     // Preserve the previous accepted lower-bone direction as closely as
     // possible; keep the bend within the same limits used by solve2.
     const u=norm(upper),old=norm(sub(reference[c.target][side],reference[c.middle][side]));
     let perpendicular=sub(old,mul(u,dot(old,u)));
     if(len(perpendicular)<1e-7)perpendicular=cross(u,Math.abs(u[1])<.9?[0,1,0]:[1,0,0]);
     const minBend=Math.acos(clamp(((c.upper+c.lower-.004)**2-c.upper**2-c.lower**2)/(2*c.upper*c.lower),-1,1));
     const bend=clamp(Math.acos(clamp(dot(u,old),-1,1)),minBend+.0001,c.maxBend-.0001);
     frame[c.target][side]=add(mid,mul(add(mul(u,Math.cos(bend)),mul(norm(perpendicular),Math.sin(bend))),c.lower));
   }
   frame.poles??={};
   frame.poles[pole]=constrainedPole(anchor,frame[c.target][side],mid,j.hingePoles[c.kind][side]);
 }
 return frame;
}
// Contact fitting operates on the actual rendered endpoint chain and the
// same conservative collision volumes as the editor. It is not a balance or
// clinical posture solver. All changes are transactional.
const CONTACT_GAP=.002,CONTACT_TOLERANCE=.005;
function contactEnvelope(j,key){
 if(key==='root')return [{p:j.root,r:.135}];
 const side=key.startsWith('left')?'left':'right';
 if(key.endsWith('Foot'))return [{p:j.feet[side],r:.067},{p:j.toes[side],r:.067}];
 if(key.endsWith('Hand')){
   const detail=j.handDetails?.[side];
   if(detail)return [{p:j.hands[side],r:.055},{p:detail.center,r:.038},...[...detail.fingers,detail.thumb].flatMap(f=>f.map(p=>({p,r:.010})))];
   return [{p:j.hands[side],r:.055},{p:j.palms[side],r:.046},{p:j.thumbs[side],r:.018}];
 }
 return [];
}
function contactDistance(j,key,surface){
 return Math.min(...contactEnvelope(j,key).map(({p,r})=>dot(sub(p,surface.origin),surface.normal)-r));
}
function contactSurface(frame,key,type,index){
 const j=joints(frame);
 if(type==='floor')return {origin:[0,0,0],normal:[0,1,0]};
 const prop=frame.equipment?.[index];
 if(type==='wall'&&prop?.type==='wall'){
   const centre=propPosition(prop),yaw=propYaw(prop),axis=rotY([0,0,1],yaw);
   const sign=dot(sub(lockPoint(j,key),centre),axis)>=0?1:-1;
   return {origin:add(centre,mul(axis,sign*.025)),normal:mul(axis,sign),centre,yaw,half:[1.24,1.1,.025],type};
 }
 if(type==='seat'&&prop?.type==='chair'){
   const centre=propPosition(prop);return {origin:add(centre,[0,.075,0]),normal:[0,1,0],centre,yaw:propYaw(prop),half:[.43,.075,.42],type};
 }
 return null;
}
function contactInside(j,key,surface){
 if(!surface.type)return true;
 // Ensure the contact proxy lies on the finite support face, not the plane
 // extended beyond a wall or chair. Other body collisions are swept separately.
 const near=contactEnvelope(j,key).reduce((a,b)=>dot(sub(a.p,surface.origin),surface.normal)-a.r<=dot(sub(b.p,surface.origin),surface.normal)-b.r?a:b);
 const q=rotY(sub(near.p,surface.centre),-surface.yaw);
 return Math.abs(q[0])+near.r<=surface.half[0]+CONTACT_TOLERANCE &&
   (surface.type==='wall'?Math.abs(q[1])+near.r<=surface.half[1]+CONTACT_TOLERANCE:Math.abs(q[2])+near.r<=surface.half[2]+CONTACT_TOLERANCE);
}
function fitSupport(frame,type,keys,index=null,lock=false){
 const fail=(issue,key=null)=>({ok:false,issue,key});
 const allowed=type==='floor'?['leftHand','rightHand','leftFoot','rightFoot']:type==='wall'?['leftHand','rightHand']:type==='seat'?['root']:[];
 if(!keys?.length||keys.some(k=>!allowed.includes(k))||new Set(keys).size!==keys.length)return fail('target');
 const before=cloneFrame(frame);
 // Conversion must preserve the original rendered pose before starting.
 for(const key of keys){
   if(before.pointLocks?.[key])return fail('locked',key);
   if(key==='root')continue;
   const side=key.startsWith('left')?'left':'right',kind=key.endsWith('Hand')?'arm':'leg';
   if(!prepareTargetMode(before,kind,side))return fail('mode',key);
 }
 if(type==='seat'){
   // A horizontal or upside-down torso must not be treated as a seated pose.
   if(dot(rotate([0,1,0],before.euler),[0,1,0])<.7)return fail('orientation','root');
   prepareSupportEdit(before);
 }
 const candidate=cloneFrame(before);
 const surfaces=new Map(keys.map(key=>[key,contactSurface(before,key,type,index)]));
 if([...surfaces.values()].some(s=>!s))return fail('surface');
 for(const key of keys){
   const surface=surfaces.get(key);
   if(key==='root'){
     const local=rotY(sub(candidate.root,surface.centre),-surface.yaw);
     // The front half of the seat provides room away from the backrest.
     const x=clamp(local[0],-.29,.29),z=clamp(local[2],-.08,.27);
     const target=add(surface.centre,rotY([x,.075+.135+CONTACT_GAP,z],surface.yaw));
     moveRoot(candidate,sub(target,candidate.root));adaptSupportPoints(candidate,before.pointLocks);
     continue;
   }
   const side=key.startsWith('left')?'left':'right',kind=key.endsWith('Hand')?'hands':'feet';
   // Start from the actual endpoint, not a possibly unreachable raw target.
   candidate[kind][side]=[...joints(candidate)[kind][side]];
   for(let i=0;i<24;i++){
     const j=joints(candidate),distance=contactDistance(j,key,surface),error=CONTACT_GAP-distance;
     if(Math.abs(error)<=.001)break;
     candidate[kind][side]=add(candidate[kind][side],mul(surface.normal,clamp(error,-.12,.12)));
   }
 }
 const final=joints(candidate);
 for(const key of keys){
   const surface=surfaces.get(key);
   if(Math.abs(contactDistance(final,key,surface)-CONTACT_GAP)>CONTACT_TOLERANCE)return fail('reach',key);
   if(!contactInside(final,key,surface))return fail('outside',key);
 }
 const checked=Collision3D.transition(before,candidate,28,type==='seat');
 if(checked.limited)return fail(checked.issue||'collision');
 const accepted=checked.frame,rendered=joints(accepted);
 for(const key of keys){
   const surface=surfaces.get(key);
   if(Math.abs(contactDistance(rendered,key,surface)-CONTACT_GAP)>CONTACT_TOLERANCE||!contactInside(rendered,key,surface))return fail('reach',key);
 }
 if(lock)for(const key of keys)setPointLock(accepted,key,true);
 return {ok:true,frame:accepted};
}
 function bandAnchorPoint(anchor,frame,j,prop,which){
   const fixed=which==='A' ? prop.position : which==='C' ? prop.positionC : which==='D' ? prop.positionD : (prop.positionB||prop.position);
   const look={leftHand:gripPoint(j,'left'),rightHand:gripPoint(j,'right'),
     leftAnkle:j.feet.left,rightAnkle:j.feet.right,leftKnee:j.knee.left,rightKnee:j.knee.right,
     leftToe:j.toes.left,rightToe:j.toes.right,
     floorLeft:[j.feet.left[0],.035,j.feet.left[2]],floorRight:[j.feet.right[0],.035,j.feet.right[2]],
     fixed:fixed||[0,.25,.85]};
   if(!Object.hasOwn(look,anchor))throw Error('Ismeretlen szalagrögzítés: '+anchor);
   return [...look[anchor]];
 }

 function bandSegments(prop,frame,j){
   const chosen=prop.anchorA&&prop.anchorB;
   if(chosen){const a=bandAnchorPoint(prop.anchorA,frame,j,prop,'A'),b=bandAnchorPoint(prop.anchorB,frame,j,prop,'B');
     if(prop.anchorC){const c=bandAnchorPoint(prop.anchorC,frame,j,prop,'C');if(prop.anchorD){const d=bandAnchorPoint(prop.anchorD,frame,j,prop,'D');return [[a,c],[c,d],[d,b]];}return [[a,c],[c,b]];}
     return [[a,b]];}
   // Backward-compatibility with the 158 bundled movement records.
   switch(prop.anchor){
     case 'hands':return [[gripPoint(j,'left'),gripPoint(j,'right')]];
     case 'knees':return [[j.knee.left,j.knee.right]];
     case 'ankle':return [[j.feet.left,j.feet.right]];
     case 'fixed':return [[[ -.82,.35,.58],gripPoint(j,'left')],[[.82,.35,.58],gripPoint(j,'right')]];
     default:return [[j.feet.left,gripPoint(j,'left')],[j.feet.right,gripPoint(j,'right')]];
   }
 }

const BAND_TYPES=['band','miniband','strap'];
const bandPositionKey=which=>({A:'position',B:'positionB',C:'positionC',D:'positionD'})[which];
function equipmentHandles(frame,j=joints(frame)){
 const out=[];
 for(const [index,prop] of (frame.equipment||[]).entries()){
   if(PROP_POSITIONS[prop.type])out.push({key:'prop:'+index,index,kind:'prop',position:propPosition(prop),movable:!prop.locked});
   if(BAND_TYPES.includes(prop.type)&&prop.anchorA&&prop.anchorB)
     for(const which of ['A','B','C','D']){
       const anchor=prop['anchor'+which];if(!anchor)continue;
       out.push({key:'band:'+index+':'+which,index,kind:'band',which,position:bandAnchorPoint(anchor,frame,j,prop,which),movable:anchor==='fixed'});
     }
 }
 return out;
}
// Explicit detachment preserves the currently rendered endpoint, not a default.
function detachBandPoint(frame,index,which){
 const prop=frame.equipment?.[index],key=bandPositionKey(which);
 if(!prop||!BAND_TYPES.includes(prop.type)||!key||!prop['anchor'+which])return false;
 const position=bandAnchorPoint(prop['anchor'+which],frame,joints(frame),prop,which);
 prop['anchor'+which]='fixed';prop[key]=position;return true;
}
function moveBandPoint(frame,index,which,delta){
 const prop=frame.equipment?.[index],key=bandPositionKey(which);
 if(!prop||!BAND_TYPES.includes(prop.type)||!key||prop['anchor'+which]!=='fixed')return false;
 const position=bandAnchorPoint('fixed',frame,joints(frame),prop,which);
 prop[key]=add(position,delta).map((v,i)=>clamp(v,i===1?0:-3,3));
 return true;
}
function equipmentDragDelta(camera,height,dx,dy,axis='free'){
 const {right,up,forward}=cameraBasis(camera),size=pixelScale(camera,height);
 if(axis==='x')return [dx*size,0,0];
 if(axis==='y')return [0,-dy*size,0];
 if(axis==='z')return [0,0,-dy*size];
 if(axis==='depth')return mul(forward,-dy*size);
 return add(mul(right,dx*size),mul(up,-dy*size));
}
function targetAxes(frame,sel,camera,space='world'){
 if(space==='camera'){const {right,up,forward}=cameraBasis(camera);return [right,up,forward];}
 const unit=[[1,0,0],[0,1,0],[0,0,1]];
 if(space!=='body')return unit;
 const axes=articulationAxes(frame);
 const transform=(['root','feet','knee'].includes(sel?.kind)||/Hip|Toe/.test(sel?.key||''))?axes.pelvis:axes.upper;
 return unit.map(transform);
}
function pointDragDelta(frame,sel,camera,height,dx,dy,axis='free',space='world'){
 const {right,up,forward}=cameraBasis(camera),size=pixelScale(camera,height);
 if(axis==='depth')return mul(forward,-dy*size);
 if(axis==='free')return add(mul(right,dx*size),mul(up,-dy*size));
 const vector=targetAxes(frame,sel,camera,space)[['x','y','z'].indexOf(axis)];
 if(!vector)return [0,0,0];
 const horizontal=dot(vector,right),vertical=dot(vector,up),screen=Math.hypot(horizontal,vertical);
 // A nearly edge-on axis uses vertical movement for depth instead of
 // amplifying tiny projected motion into an enormous world-space delta.
 const amount=screen>.15?(dx*horizontal-dy*vertical)/screen:-dy;
 return mul(vector,amount*size);
}
// Approximate inverse articulation with bounded existing controls only.
function articulationControls(sel){
 const k=sel.key,s=sel.side;
 const spec=(key,lo,hi,side=null)=>({key,lo,hi,side});
 const limited=key=>spec(key,-ARTICULATION_LIMITS[key],ARTICULATION_LIMITS[key]);
 if(k==='head')return [spec('headPitch',-Math.PI/2,Math.PI/2),spec('headRoll',-Math.PI/3,Math.PI/3),spec('headYaw',-Math.PI/2,Math.PI/2)];
 if(k==='waist')return ['lumbarBend','lumbarSideBend'].map(limited);
 if(k==='neck'||k==='chest')return ['thoracicBend','thoracicSideBend','lumbarBend','lumbarSideBend'].map(limited);
 if(k.endsWith('Shoulder'))return ['ShoulderElevation','ShoulderProtraction'].map(suffix=>({...limited(s+suffix),suffix}));
 if(k.endsWith('Hip'))return ['pelvisPitch','pelvisYaw','pelvisRoll'].map(limited);
 if(k.endsWith('Toe'))return [spec('footPitch',-.65,.95,s),{...limited(s+'AnkleYaw'),suffix:'AnkleYaw'}];
 return [];
}
function fitArticulatedPoint(frame,sel,delta,symmetric=false){
 const controls=articulationControls(sel),start=bodyPoint(joints(frame),sel.key),goal=add(start,delta);
 if(!controls.length||len(delta)<1e-8)return false;
 const working=cloneFrame(frame);
 const get=(f,c)=>c.side?(f[c.key]?.[c.side]||0):(f[c.key]||0);
 const set=(f,c,value)=>{
   value=clamp(value,c.lo,c.hi);
   if(c.suffix){editSideArticulation(f,sel.side,c.suffix,value,symmetric);return;}
   if(c.side){
     f[c.key]??={};const previous=get(f,c);
     if(symmetric){const other=swapSide(c.side),old=f[c.key][other]||0;value=previous+clamp(value-previous,c.lo-old,c.hi-old);f[c.key][other]=old+value-previous;}
     f[c.key][c.side]=value;
   }else f[c.key]=value;
 };
 let best=len(delta);
 for(let pass=0;pass<4;pass++){
   let improved=false;
   for(const c of controls){
     const current=get(working,c),probe=cloneFrame(working),step=current+.005<=c.hi?.005:-.005;
     set(probe,c,current+step);
     const actualStep=get(probe,c)-current;
     if(Math.abs(actualStep)<1e-8)continue;
     const point=bodyPoint(joints(working),sel.key),derivative=mul(sub(bodyPoint(joints(probe),sel.key),point),1/actualStep);
     const amount=clamp(dot(sub(goal,point),derivative)/(dot(derivative,derivative)+.0001),-.25,.25);
     if(Math.abs(amount)<1e-6)continue;
     for(const scale of [1,.5,.25]){
       const trial=cloneFrame(working);set(trial,c,current+amount*scale);
       const error=len(sub(goal,bodyPoint(joints(trial),sel.key)));
       if(error<best-1e-7){Object.assign(working,trial);best=error;improved=true;break;}
     }
   }
   if(!improved||best<.0005)break;
 }
 if(best>=len(delta)-1e-7)return false;
 Object.assign(frame,working);return true;
}
function targetEditOptions(sel,symmetric=false,adaptRoot=false){
 const central=sel.kind==='root',articulated=sel.kind==='articulate';
 const mirror=!central&&!articulated&&symmetric;
 const kind=['hands','elbow'].includes(sel.kind)?'arm':'leg',other=sel.side?swapSide(sel.side):null;
 return {prepare:f=>central||articulated||(prepareTargetMode(f,kind,sel.side)&&(!mirror||prepareTargetMode(f,kind,other))),
   adapt:central?(adaptRoot?'root':true):articulated&&!sel.key.endsWith('Toe')&&sel.key!=='head',
   move:(f,delta)=>{
     const before=cloneFrame(f);
     if(!moveTargetBy(f,sel,delta,symmetric))return false;
     if(mirror){if(['elbow','knee'].includes(sel.kind))mirrorBendDelta(f,before,sel.kind,sel.side);else mirrorTargetDelta(f,before,sel.kind,sel.side);}
     return true;
   }};
}
function probeTargetDirections(frame,sel,camera,space='world',symmetric=false){
 if(!sel||sel.kind==='inspect')return [];
 const opts=targetEditOptions(sel,symmetric,false),base=cloneFrame(frame);
 if(!opts.prepare(base))return [];
 if(opts.adapt)prepareSupportEdit(base);
 const point=bodyPoint(joints(base),sel.key),axes=targetAxes(base,sel,camera,space);
 return axes.flatMap((axis,i)=>[-1,1].map(sign=>{
   const direction=mul(axis,sign),candidate=cloneFrame(base);
   const changed=opts.move(candidate,mul(direction,.01));
   const result=changed?Collision3D.transition(base,candidate,6,opts.adapt):null;
   const progress=result?dot(sub(bodyPoint(joints(result.frame),sel.key),point),direction):0;
   return {axis:['X','Y','Z'][i],sign,direction,allowed:!!result&&!result.limited&&progress>=.001,progress};
 }));
}
function moveTargetBy(frame,sel,delta,symmetric=false){
 if(!sel||sel.kind==='inspect'||!Array.isArray(delta)||delta.length!==3||delta.some(v=>!Number.isFinite(v)))return false;
 if(sel.kind==='root'){moveRoot(frame,delta);return true;}
 if(sel.kind==='articulate')return fitArticulatedPoint(frame,sel,delta,symmetric);
 const kind=['hands','elbow'].includes(sel.kind)?'arm':'leg';
 if(frame[modeKeys[kind]]?.[sel.side])return false;
 if(sel.kind==='elbow'||sel.kind==='knee')return moveBendPoint(frame,sel.kind,sel.side,delta);
 const key=sel.kind==='hands'?'hands':'feet',origin=frame[key][sel.side];
 frame[key][sel.side]=add(origin,delta).map((v,i)=>clamp(v,key==='feet'&&i===1?.05:-3,3));
 return true;
}
function resetBodyPart(frame,reference,group){
 if(!reference||!['root','head','trunk','leftArm','rightArm','leftLeg','rightLeg'].includes(group))return null;
 const out=cloneFrame(frame);
 const copy=(to,from,key)=>{if(from?.[key]!=null)to[key]=cloneFrame(from[key]);else delete to[key];};
 const fields={root:['root','pelvicTilt','pelvisPitch','pelvisYaw','pelvisRoll'],head:['headYaw','headPitch','headRoll'],trunk:['shoulderLift','upperYaw','spineBend','lumbarBend','lumbarSideBend','thoracicBend','thoracicSideBend']};
 if(fields[group]){
   for(const key of fields[group])copy(out,reference,key);
   if(group==='trunk')out.euler=[...reference.euler];
 }else{
   const side=group.startsWith('left')?'left':'right',kind=group.endsWith('Arm')?'arm':'leg',target=kind==='arm'?'hands':'feet',mode=modeKeys[kind],pole=(kind==='arm'?'elbow':'knee')+(side==='left'?'Left':'Right');
   // Restore the original limb in its current body-local reference frame:
   // an unrelated translation or rotation of the body is not undone.
   for(const suffix of SIDE_ARTICULATION[kind])copy(out,reference,side+suffix);
   const a=articulationAxes(reference),b=articulationAxes(out);
   const original=joints(reference),current=joints(out);
   const anchor=kind==='arm'?'shoulder':'hip';
   const local=kind==='arm'?a.armLocal:a.legLocal,world=kind==='arm'?b.upper:b.pelvis;
   const relocate=point=>add(current[anchor][side],world(local(sub(point,original[anchor][side]))));
   out[target][side]=relocate(reference[target][side]);
   out.poles??={};
   if(reference.poles?.[pole])out.poles[pole]=relocate(reference.poles[pole]);else delete out.poles[pole];
   out[mode]??={};copy(out[mode],reference[mode],side);
   if(kind==='arm')copy(out,reference,side+'HandCurl');
   else {out.footPitch??={};copy(out.footPitch,reference.footPitch,side);}
 }
 return out;
}
function moveRoot(frame,delta){
 frame.root=frame.root.map((v,i)=>clamp(v+delta[i],i===1?0:-3,3));
 return frame;
}
function shaded3D(canvas){
 let gl=canvas.getContext('webgl',{alpha:true,antialias:true,preserveDrawingBuffer:true});if(!gl)throw Error('A készülék nem támogatja a WebGL megjelenítést.');
 const vert=`attribute vec3 aPos;attribute vec3 aN;uniform mat4 uM;uniform mat4 uVP;varying vec3 vN;varying vec3 vW;void main(){vec4 wp=uM*vec4(aPos,1.);vW=wp.xyz;vN=normalize(mat3(uM)*aN);gl_Position=uVP*wp;}`;
 const frag=`precision mediump float;varying vec3 vN;varying vec3 vW;uniform vec3 uColor;uniform float uOpacity;void main(){vec3 l=normalize(vec3(-.45,.85,.65));float diff=max(0.,dot(normalize(vN),l));float level=.42+.58*diff;vec3 c=uColor*level+vec3(.09,.06,.11)*pow(max(0.,diff),9.);gl_FragColor=vec4(c,uOpacity);}`;
 function shader(t,s){let o=gl.createShader(t);gl.shaderSource(o,s);gl.compileShader(o);if(!gl.getShaderParameter(o,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(o));return o;}
 let program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,vert));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,frag));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
 let verts=[],norms=[],indices=[],rings=13,segs=20;
 for(let i=0;i<=rings;i++){let t=i*Math.PI/rings;for(let j=0;j<=segs;j++){let p=j*2*Math.PI/segs,n=[Math.sin(t)*Math.cos(p),Math.cos(t),Math.sin(t)*Math.sin(p)];verts.push(...n);norms.push(...n);}}
 for(let i=0;i<rings;i++)for(let j=0;j<segs;j++){let n=i*(segs+1)+j;indices.push(n,n+1,n+segs+1,n+1,n+segs+2,n+segs+1);}
 function buf(kind,data){let b=gl.createBuffer();gl.bindBuffer(kind,b);gl.bufferData(kind,data,gl.STATIC_DRAW);return b;}
 let vb=buf(gl.ARRAY_BUFFER,new Float32Array(verts)),nb=buf(gl.ARRAY_BUFFER,new Float32Array(norms)),ib=buf(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(indices));
 let posLoc=gl.getAttribLocation(program,'aPos'),nLoc=gl.getAttribLocation(program,'aN');gl.bindBuffer(gl.ARRAY_BUFFER,vb);gl.enableVertexAttribArray(posLoc);gl.vertexAttribPointer(posLoc,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,nb);gl.enableVertexAttribArray(nLoc);gl.vertexAttribPointer(nLoc,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);
 let uM=gl.getUniformLocation(program,'uM'),uVP=gl.getUniformLocation(program,'uVP'),uC=gl.getUniformLocation(program,'uColor'),uO=gl.getUniformLocation(program,'uOpacity');
 gl.enable(gl.DEPTH_TEST);gl.enable(gl.CULL_FACE);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);
 let palette={skin:[.92,.61,.45],shirt:[.45,.24,.62],leggings:[.19,.57,.60],hair:[.23,.13,.17],shoes:[.24,.18,.31],mat:[.74,.65,.85],background:[.97,.95,.99],band:[.60,.29,.76],metal:[.54,.50,.62],equip:[.52,.57,.74],floor:[.83,.77,.90],handle:[.95,.55,.2]};
 let camera={azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]},viewproj, dims={w:0,h:0},lastJoints;
 let projectionMode='perspective';
 function setup(){const w=Math.max(1,canvas.clientWidth),h=Math.max(1,canvas.clientHeight),dpr=Math.min(window.devicePixelRatio||1,2);if(canvas.width!==Math.floor(w*dpr)||canvas.height!==Math.floor(h*dpr)){canvas.width=Math.floor(w*dpr);canvas.height=Math.floor(h*dpr);gl.viewport(0,0,canvas.width,canvas.height);}dims={w,h};let c=camera;let el=clamp(c.elevation,-Math.PI/2+.001,Math.PI/2-.001);c.elevation=el;let eye=add(c.target,[c.distance*Math.sin(c.azimuth)*Math.cos(el),c.distance*Math.sin(el),c.distance*Math.cos(c.azimuth)*Math.cos(el)]);viewproj=mul4(projectionMode==='ortho'?ortho(c.distance*Math.tan(VIEW_FOV/2),w/h):perspective(VIEW_FOV,w/h,.04,90),lookAt(eye,c.target));gl.uniformMatrix4fv(uVP,false,viewproj);return eye;}
 let characterOpacity=1;
 function drawM(M,color,opacity=1){if(!M)return;gl.uniformMatrix4fv(uM,false,M);gl.uniform3fv(uC,new Float32Array(palette[color]||color));gl.uniform1f(uO,opacity*characterOpacity);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);}
 const ell=(centre,s,c,o=1)=>drawM(sphereM(centre,s),c,o),limb=(a,b,th,c)=>drawM(segmentM(a,b,th,th),c);
 function poly(points,th,color){for(let i=1;i<points.length;i++)limb(points[i-1],points[i],th,color);}
 function bezier(a,b,c,d,t){let u=1-t;return add(add(mul(a,u*u*u),mul(b,3*u*u*t)),add(mul(c,3*u*t*t),mul(d,t*t*t)));}
 // Stable attachment names shared with the editor and the JSON validator.
 function distanceToSegment(p,a,b){const d=sub(b,a),l=dot(d,d),t=clamp(dot(sub(p,a),d)/Math.max(l,1e-8),0,1);return len(sub(p,add(a,mul(d,t))));}
 function bandPath(a,b,j,route='auto',frame={euler:[0,0,0]}){
   // Avoid a crude forced arc. If a straight strap clears the ribcage, keep it
   // straight; otherwise route its control points around a trunk capsule.
   const trunkA=j.root,trunkB=j.neck,radius=.24;
   const straight=[];for(let n=0;n<=24;n++)straight.push(add(a,mul(sub(b,a),n/24)));
   const rigid=Collision3D.solids(frame).filter(o=>o.type==='box'||o.type==='sphere');
   // Band attachments are allowed to touch hands/feet; the middle of the
   // strap must stay outside the torso, props and support furniture.
   const clearance=pts=>{
     let value=Infinity;
     for(const p of pts.slice(3,-3)){
       value=Math.min(value,distanceToSegment(p,trunkA,trunkB)-radius);
       for(const solid of rigid){
         if(solid.type==='sphere')value=Math.min(value,len(sub(p,solid.centre))-solid.radius-.008);
         if(solid.type==='box'){
           const q=rotY(sub(p,solid.centre),-(solid.yaw||0)).map((x,i)=>Math.abs(x)-solid.half[i]);
           value=Math.min(value,len(q.map(x=>Math.max(0,x)))+Math.min(Math.max(...q),0)-.008);
         }
       }
     }
     return value;
   };
   const avoid=clearance(straight)<.008;
   if(!avoid)return straight;
   const e=frame.euler||[0,0,0];
   const forward=norm(rotate([0,0,1],e)),right=norm(rotate([1,0,0],e));
   const dirs={front:forward,back:mul(forward,-1),left:mul(right,-1),right:right};
   const choices=route==='auto'?Object.keys(dirs):[dirs[route]?route:'front'];
   let best=null,score=-Infinity;
   for(const choice of choices)for(const lift of [.40,.53,.68,.84]){
     const vector=mul(dirs[choice],lift),c1=add(a,vector),c2=add(b,vector);
     const path=[];for(let n=0;n<=30;n++)path.push(bezier(a,c1,c2,b,n/30));
     const nearest=clearance(path);
     const s=nearest-lift*.025;
     if(s>score){score=s;best=path;}
     if(nearest>=.012)return path;
   }
   // Never draw an intersecting fallback through the body. The editor will
   // signal that the anchor/route must be changed instead.
   return score>=.005 ? best : null;
 }
 function bandIssues(frame){
   const j=joints(frame),issues=[];
   for(const [index,prop] of (frame.equipment||[]).entries()){
     if(!['band','miniband','strap'].includes(prop.type))continue;
     const strands=prop.type==='miniband'&&!prop.anchorA?[[j.knee.left,j.knee.right]]:bandSegments(prop,frame,j);
     if(strands.some(([a,b])=>!bandPath(a,b,j,prop.route||'auto',frame)))issues.push(index);
   }
   return issues;
 }
 function renderEquipment(frame,j){for(const prop of frame.equipment||[]){let type=prop.type;
  if(PROP_POSITIONS[type]){
    const centre=propPosition(prop),yaw=propYaw(prop);
    const shape=(pos,size,color='equip')=>drawM(model(pos,rotY([size[0],0,0],yaw),[0,size[1],0],rotY([0,0,size[2]],yaw)),color);
    const world=p=>propTransform(prop,p);
    if(type==='wall')shape(centre,[1.24,1.1,.025],'floor');
    if(type==='chair'){
      shape(centre,[.43,.08,.42]);
      drawM(segmentM(world([0,-.04,-.33]),world([0,.65,-.33]),.06,.07),'equip');
      for(let x of [-.35,.35])for(let z of [-.26,.26])limb(world([x,-.03,z]),world([x,-.27,z]),.035,'equip');
    }
    const sizes={step:[.43,.13,.28],block:[.22,.09,.14],ball:[.34,.34,.34],softBall:[.34,.34,.34],roller:[.28,.14,.14],balancePad:[.27,.04,.21]};
    if(sizes[type])shape(centre,sizes[type]);
  }
  if(type==='mat'){drawM(model([0,.008,.12],[.52,0,0],[0,.022,0],[0,0,1.42]),'mat');}
  if(type==='band'||type==='miniband'||type==='strap'){
    const strands=type==='miniband'&&!prop.anchorA ? [[j.knee.left,j.knee.right]] : bandSegments(prop,frame,j);
    for(const [a,b] of strands){
      const path=bandPath(a,b,j,prop.route||'auto',frame);
      if(!path){j.bandBlocked=(j.bandBlocked||0)+1;continue;}
      poly(path,type==='miniband'?.019:.013,'band');
      ell(a,[.021,.021,.021],'band');ell(b,[.021,.021,.021],'band');
    }
  }
  if(type==='towel'){
    const a=j.hands.left,b=j.hands.right,mid=mul(add(a,b),.5);
    const f=rotate([0,0,.24],frame.euler);
    poly([a,add(mid,f),b],type==='towel'?.027:.012,type==='towel'?'mat':'band');
  }
  if(type==='dumbbell'){for(let s of ['left','right']){
    const p=gripPoint(j,s),axis=j.handDetails[s]?.lateral||[1,0,0];
    limb(add(p,mul(axis,-.10)),add(p,mul(axis,.10)),.025,'metal');
    for(const sign of [-1,1])ell(add(p,mul(axis,sign*.12)),[.035,.06,.06],'metal');
  }}
 }}
 function draw(frame,{edit=true,selected=null,showAll=false,issue=null,dark=false,equipmentMode=false,equipmentSelection=null,displayMode='solid',highContrast=false,axisSpace='world',moveAxis='free',directionHints=[]}={}){let eye=setup();gl.clearColor(...(dark?[.115,.10,.16,1]:[.985,.978,1,1]));gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);gl.useProgram(program);gl.uniformMatrix4fv(uVP,false,viewproj);
 const j=joints(frame);lastJoints=j;
 // ground follows world coordinates. No automatic nonuniform body shrinking.
 ell([0,-.10,0],[1.60,.085,1.80],dark?[.26,.23,.34]:'floor');
 // Props drawn before character; the rubber band is routed away from the trunk.
 renderEquipment(frame,j);
 const ghost=displayMode==='ghost',skeleton=displayMode==='skeleton';
 if(!skeleton){
 if(ghost){characterOpacity=.20;gl.enable(gl.BLEND);gl.depthMask(false);}
 try{
 const rp=j.root,axis=(p)=>rotate(p,frame.euler),neck=j.neck,axes=articulationAxes(frame);
 // torso and pelvis; geometry stays constant length through every pose.
 let torsoUpper=j.spineUpper;limb(add(rp,axis([0,.025,0])),j.spineMid,.19,'shirt');limb(j.spineMid,torsoUpper,.19,'shirt');
 // Local pelvic articulation: only the pelvis tilts. The ribcage, shoulders,
 // elbow/hands and neck keep the same world-space transform. Do not translate
 // or rotate the whole body for a small posterior pelvic tilt.
 const tilt=j.pelvicTilt;
 const pelvisRotation=[frame.euler[0]+tilt,frame.euler[1],frame.euler[2]];
 const articulated=axes.p.some(Boolean);
 const pelvicAxis=v=>articulated?axes.pelvis(rotX(v,tilt)):rotate(v,pelvisRotation);
 const pelvisCentre=add(rp,articulated?axes.pelvis([0,.10,0]):axis([0,.10,0]));
 drawM(model(pelvisCentre,pelvicAxis([.23,0,0]),pelvicAxis([0,.13,0]),pelvicAxis([0,0,.19])),'leggings');
 const beltCentre=add(pelvisCentre,pelvicAxis([0,.10,0]));
 drawM(model(beltCentre,pelvicAxis([.20,0,0]),pelvicAxis([0,.023,0]),pelvicAxis([0,0,.14])),'shirt');
 // upper chest to neck, neckline
 limb(torsoUpper,neck,.105,'skin');
 for(const side of ['left','right']){
  const shoulder=j.shoulder[side],hip=j.hip[side],elbow=j.elbow[side],hand=j.hands[side],knee=j.knee[side],foot=j.feet[side];
  ell(shoulder,[.12,.115,.12],'shirt');limb(shoulder,elbow,.092,'skin');ell(elbow,[.075,.075,.075],'skin');limb(elbow,hand,.065,'skin');ell(hand,[.06,.055,.055],'skin');
  const detail=j.handDetails[side];
  if(detail){
    drawM(model(detail.center,mul(detail.lateral,.043),mul(detail.inward,.023),mul(detail.forward,.052)),'skin');
    for(const finger of [...detail.fingers,detail.thumb])poly(finger,.010,'skin');
  }else {limb(hand,j.palms[side],.046,'skin');limb(hand,j.thumbs[side],.018,'skin');}
  ell(hip,[.11,.11,.11],'leggings');limb(hip,knee,.13,'leggings');ell(knee,[.109,.108,.105],'leggings');limb(knee,foot,.092,'leggings');
  if(sideAngle(frame,side,'AnkleYaw')||sideAngle(frame,side,'AnkleRoll')){
    const {right,up,forward}=j.footAxes[side];
    const centre=mul(add(foot,j.toes[side]),.5);
    drawM(model(centre,mul(right,.067),mul(up,.047),mul(forward,.145)),'shoes');
    drawM(model(add(centre,mul(up,-.038)),mul(right,.058),mul(up,.013),mul(forward,.13)),'leggings');
  }else {limb(foot,j.toes[side],.067,'shoes');ell(j.toes[side],[.065,.050,.070],'shoes');}
 }
 // Move the head with the neck and look direction; face is on head's local +Z axis.
 const head=j.head,hyaw=frame.headYaw||0,hpitch=frame.headPitch||0,hroll=frame.headRoll||0;
 const headEuler=add(frame.euler,[hpitch,hyaw,hroll]);
 const faceAxis=v=>j.articulatedSpine?axes.upper(unrotate(rotate(v,headEuler),frame.euler)):rotate(v,headEuler);
 const faceForward=faceAxis([0,0,1]);let headUp=faceAxis([0,1,0]),headRight=norm(cross(headUp,faceForward));if(len(headRight)<.01)headRight=[1,0,0];headUp=norm(cross(faceForward,headRight));
 ell(head,[.132,.171,.126],'skin');ell(add(head,add(mul(headUp,.135),mul(faceForward,-.036))),[.137,.068,.117],'hair');ell(add(head,add(mul(headUp,.15),mul(faceForward,-.12))),[.065,.068,.062],'hair');
 for(let s of [-1,1])ell(add(head,add(add(mul(headRight,.052*s),mul(headUp,.016)),mul(faceForward,.117))),[.012,.016,.014],'hair');
 ell(add(head,add(mul(faceForward,.129),mul(headUp,-.035))),[.022,.018,.025],'skin');
 }finally{if(ghost){characterOpacity=1;gl.depthMask(true);gl.disable(gl.BLEND);}}
 }
 if(ghost||skeleton){
   const bone=dark?(highContrast?[1,1,1]:[.87,.84,.98]):(highContrast?[.05,.04,.10]:[.23,.16,.38]);
   gl.disable(gl.DEPTH_TEST);
   try{
     for(const [a,b] of skeletonSegments(j))limb(a,b,.016,bone);
     for(const target of BODY_TARGETS){const pos=bodyPoint(j,target.key);ell(pos,[.025,.025,.025],bone);}
   }finally{gl.enable(gl.DEPTH_TEST);}
 }
 if(edit){
   const handle=(pos,key)=>{const r=key===selected? .068:.045;ell(pos,[r,r,r],key===selected?[.15,.8,.48]:'handle');};
   if(ghost||skeleton)gl.disable(gl.DEPTH_TEST);
   try{
     if(!equipmentMode)for(const target of BODY_TARGETS){if(target.secondary&&!showAll&&target.key!==selected)continue;handle(bodyPoint(j,target.key),target.key);}
     for(const key of Object.keys(frame.pointLocks||{})){
       if(LOCK_KEYS.includes(key))ell(lockPoint(j,key),[.055,.055,.055],[.19,.49,.92]);
     }
   }finally{if(ghost||skeleton)gl.enable(gl.DEPTH_TEST);}
 }
 if(edit&&!equipmentMode&&selected){
   const target=bodyTarget(selected),origin=bodyPoint(j,selected);
   if(target&&origin){
     const axes=targetAxes(frame,target,camera,axisSpace),colors=[[.92,.18,.22],[.12,.65,.30],[.12,.42,.95]];
     gl.disable(gl.DEPTH_TEST);
     try{for(const [i,vector] of axes.entries()){
       const end=add(origin,mul(vector,.19));const active=moveAxis==='free'||moveAxis===['x','y','z'][i];
       limb(origin,end,active?.013:.007,colors[i]);ell(end,[.022,.022,.022],colors[i]);
     }}finally{gl.enable(gl.DEPTH_TEST);}
   }
 }
 if(edit&&!equipmentMode&&selected&&directionHints.length){
   const origin=bodyPoint(j,selected);
   gl.disable(gl.DEPTH_TEST);
   try{for(const hint of directionHints){
     const end=add(origin,mul(hint.direction,.27)),color=hint.allowed?[.10,.72,.36]:[.95,.18,.22];
     if(hint.allowed){limb(add(origin,mul(hint.direction,.20)),end,.012,color);ell(end,[.028,.028,.028],color);}
     else{const side=norm(cross(hint.direction,Math.abs(hint.direction[1])<.85?[0,1,0]:[1,0,0]));limb(add(end,mul(side,-.035)),add(end,mul(side,.035)),.012,color);}
   }}finally{gl.enable(gl.DEPTH_TEST);}
 }
 if(edit&&equipmentMode){
   gl.disable(gl.DEPTH_TEST);
   try{
     for(const h of equipmentHandles(frame,j)){
       const r=h.key===equipmentSelection?.key? .075:.052;
       const color=h.key===equipmentSelection?.key?[.15,.8,.48]:h.movable?[.95,.55,.2]:[.19,.49,.92];
       ell(h.position,[r,r,r],color);
     }
   }finally{gl.enable(gl.DEPTH_TEST);}
 }
 if(edit&&issue){
   // Draw crosses facing the camera, including on otherwise occluded limbs.
   // A diagnostic is not new model geometry and never enters saved data.
   const {right,up}=cameraBasis(camera);
   gl.disable(gl.DEPTH_TEST);
   try{
     for(const part of issueGeometry(frame,j,issue)){
       const r=Math.min(.10,Math.max(.035,part.r*.6)),colour=[1,.25,.25];
       if(len(sub(part.a,part.b))>.001)limb(part.a,part.b,r*.30,colour);
       const points=len(sub(part.a,part.b))>.001?[part.a,part.b]:[part.a];
       for(const point of points){
         const d1=mul(add(right,up),r),d2=mul(sub(right,up),r);
         limb(sub(point,d1),add(point,d1),.012,colour);
         limb(sub(point,d2),add(point,d2),.012,colour);
       }
     }
   }finally{gl.enable(gl.DEPTH_TEST);}
 }
 return j;}
 function project(p){let q=[p[0],p[1],p[2],1],r=[];for(let i=0;i<4;i++)r.push(viewproj[i]*q[0]+viewproj[4+i]*q[1]+viewproj[8+i]*q[2]+viewproj[12+i]*q[3]);if(r[3]<=.001)return null;return [(r[0]/r[3]*.5+.5)*dims.w,(-r[1]/r[3]*.5+.5)*dims.h];}
 function pickAll(x,y,showAll=false){
 if(!lastJoints)return [];
 return BODY_TARGETS.filter(t=>showAll||!t.secondary).map(t=>{
   const screen=project(bodyPoint(lastJoints,t.key));
   return {target:t,distance:screen?Math.hypot(x-screen[0],y-screen[1]):Infinity};
 }).filter(x=>x.distance<=28).sort((a,b)=>a.distance-b.distance).map(x=>x.target);
 }
 function pickEquipment(frame,x,y){return equipmentHandles(frame,lastJoints||joints(frame)).map(h=>{const s=project(h.position);return {h,d:s?Math.hypot(x-s[0],y-s[1]):Infinity};}).filter(v=>v.d<=28).sort((a,b)=>a.d-b.d).map(v=>v.h);}
 function pick(x,y,showAll=false){return pickAll(x,y,showAll)[0]||null;}
 function dragTarget(frame,sel,dx,dy,depth=false,axis='free',space='world'){
 const delta=pointDragDelta(frame,sel,camera,dims.h,dx,dy,depth?'depth':axis,space);
 return moveTargetBy(frame,sel,delta)?joints(frame).clamped:null;
 }
 return {draw,bandIssues,setCamera(c){camera=c;},getCamera:()=>camera,setProjection(value){projectionMode=value==='ortho'?'ortho':'perspective';},pick,pickAll,pickEquipment,dragTarget,project,setup,getJoints:()=>lastJoints,release(){gl.getExtension('WEBGL_lose_context')?.loseContext();}};
}
function checkPose(p){if(!p||p.version!==1||!Array.isArray(p.frames)||p.frames.length<1||p.frames.length>MAX_PHASES)throw Error('Nem támogatott 3D pózformátum.');if(!validEquipmentIds(p.frames))throw Error('Érvénytelen eszközazonosító.');for(let f of p.frames){if(!validHandCurl(f))throw Error('Érvénytelen kéztartás.');if(!validArticulation(f))throw Error('Érvénytelen helyi gerinc- vagy medenceforgatás.');if(!validPhaseTiming(f))throw Error('Érvénytelen fázisidőzítés.');if(f.direction!=null&&!PHASE_DIRECTIONS.includes(f.direction))throw Error('Érvénytelen fázisirány.');for(let k of ['root','euler'])if(!Array.isArray(f[k])||f[k].length!==3||f[k].some(x=>!Number.isFinite(x)))throw Error('Hibás testhelyzet: '+k);for(const k of ['headYaw','headPitch','headRoll','shoulderLift','pelvicTilt'])if(f[k]!==undefined&&(!Number.isFinite(f[k])||Math.abs(f[k])>2))throw Error('Hibás ízületi vezérlő: '+k);if(Math.abs(f.pelvicTilt||0)>.35)throw Error('Túl nagy medencebillentés.');
if(f.pointLocks!=null){
 if(typeof f.pointLocks!=='object'||Array.isArray(f.pointLocks))throw Error('Érvénytelen rögzítési pont.');
 for(const [key,pos] of Object.entries(f.pointLocks))if(!LOCK_KEYS.includes(key)||!Array.isArray(pos)||pos.length!==3||pos.some(x=>!Number.isFinite(x)||Math.abs(x)>10))throw Error('Érvénytelen rögzítési pont.');
}
for(const [side,leg] of Object.entries(f.legAngles||{})){
 if(!['left','right'].includes(side)|| !leg ||
 !Number.isFinite(leg.flexion)||!Number.isFinite(leg.abduction)||!Number.isFinite(leg.kneeFlexion)||
 leg.flexion<-.3||leg.flexion>2.36||Math.abs(leg.abduction)>.7||leg.kneeFlexion<0||leg.kneeFlexion>2.36)
 throw Error('Érvénytelen csípő- és térdmozgás: '+side);
}
for(const controls of [f.legAngles||{},f.armAngles||{}])
 for(const a of Object.values(controls))
  if(a.axialRotation!=null&&(!Number.isFinite(a.axialRotation)||Math.abs(a.axialRotation)>1.570797))throw Error('Érvénytelen végtagrotáció.');
for(const [side,pitch] of Object.entries(f.footPitch||{}))
 if(!['left','right'].includes(side)||!Number.isFinite(pitch)||pitch<-.65||pitch>.95)
 throw Error('Érvénytelen lábfejállás: '+side);
const armRanges={flexion:[-.52,3.05],abduction:[0,2.70],elbowFlexion:[0,2.62],wristFlexion:[-1.04,1.04],forearmRotation:[-1.57,1.57]};
for(const [side,a] of Object.entries(f.armAngles||{})){
 if(!['left','right'].includes(side)||!a)throw Error('Hibás kar: '+side);
 for(const [k,[lo,hi]] of Object.entries(armRanges))if(a[k]!==undefined&&(!Number.isFinite(a[k])||a[k]<lo-.00001||a[k]>hi+.00001))throw Error('A kar nem megengedett szögben áll: '+k);
}
for(const prop of f.equipment||[]){
 if(prop.yaw!=null&&(!PROP_POSITIONS[prop.type]||!Number.isFinite(prop.yaw)||Math.abs(prop.yaw)>Math.PI+1e-6))throw Error('Érvénytelen eszközforgatás.');
 if(prop.locked!=null&&(typeof prop.locked!=='boolean'||!PROP_POSITIONS[prop.type]))throw Error('Érvénytelen eszközrögzítés.');
 if(['band','miniband','strap'].includes(prop.type)){
   if((prop.anchorA||prop.anchorB)&&(!BAND_ANCHORS.includes(prop.anchorA)||!BAND_ANCHORS.includes(prop.anchorB)))throw Error('Hibás gumiszalag-rögzítés.');
   if(prop.anchorC&&!BAND_ANCHORS.includes(prop.anchorC))throw Error('Hibás harmadik szalagpont.');
   if(prop.positionC&&(!Array.isArray(prop.positionC)||prop.positionC.length!==3||prop.positionC.some(x=>!Number.isFinite(x)||Math.abs(x)>10)))throw Error('Hibás harmadik szalagkoordináta.');
   if(prop.anchorD!=null&&(!BAND_ANCHORS.includes(prop.anchorD)||!BAND_ANCHORS.includes(prop.anchorA)||!BAND_ANCHORS.includes(prop.anchorB)||!BAND_ANCHORS.includes(prop.anchorC)))throw Error('Hibás negyedik szalagpont.');
   if(prop.positionD!=null&&(!Array.isArray(prop.positionD)||prop.positionD.length!==3||prop.positionD.some(x=>!Number.isFinite(x)||Math.abs(x)>10)))throw Error('Hibás negyedik szalagkoordináta.');
   if(prop.anchorD==='fixed'&&prop.positionD==null)throw Error('Hibás negyedik szalagkoordináta.');
   if(prop.route&&!['auto','direct','front','back','left','right'].includes(prop.route))throw Error('Hibás szalagvezetés.');
 }
}for(let kind of ['hands','feet'])for(let side of ['left','right'])if(!Array.isArray(f[kind]?.[side])||f[kind][side].length!==3||f[kind][side].some(x=>!Number.isFinite(x)))throw Error('Hiányzó vagy hibás végpont: '+kind+'/'+side);}}
global.MozduljRig={initialPose,baseFrame,auditPreset,PRESET_NAMES,joints,solve2,shaded3D,moveRoot,checkPose,HAND_PRESETS,handCurl,setHandCurl,validHandCurl,gripPoint,articulationAxes,validArticulation,ARTICULATION_LIMITS,SIDE_ARTICULATION,sideAngle,editSideArticulation,phaseTiming,validPhaseTiming,previewPlan,previewAt,phaseThumbnailData,PHASE_TYPES,contactEnvelope,contactDistance,contactSurface,contactInside,fitSupport,gestureMetrics,pinchCamera,panCamera,cameraBasis,CAMERA_VIEWS,setCameraView,cameraViewName,workspaceShare,skeletonSegments,targetAxes,pointDragDelta,moveTargetBy,targetEditOptions,probeTargetDirections,resetBodyPart,pixelScale,ortho,issueGeometry,BODY_TARGETS,bodyTarget,bodyPoint,moveBendPoint,mirrorBendDelta,prepareSupportEdit,adaptSupportPoints,projectSupportRoot,supportChainConstraints,mirrorFrame,mirrorIssue,copyLimb,copyIssue,mirrorTargetDelta,editLimbAngle,movePhase,PROP_POSITIONS,propPosition,propYaw,matchingProp,editProps,newEquipmentId,validEquipmentIds,bandSegments,equipmentHandles,detachBandPoint,moveBandPoint,equipmentDragDelta,MAX_PHASES,PHASE_DIRECTIONS,inferAngles,prepareAngleMode,prepareTargetMode,limbError,LOCK_KEYS,lockPoint,setPointLock,lockErrors,BAND_ANCHORS,Collision:Collision3D,math:{V,add,sub,mul,dot,cross,len,norm,rotate}};
})(window);
