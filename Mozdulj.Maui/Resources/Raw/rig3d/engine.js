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
 return f;
}
function initialPose(preset='stand'){
 let a=baseFrame(preset),b=JSON.parse(JSON.stringify(a));b.name='Mozdulat';
 if(preset==='catcow'){b.euler[0]+=.18;b.headPitch=.30;}
 if(preset==='bridge'){b.root[1]=.49;b.feet.left=[-.16,.055,.87];b.feet.right=[.16,.055,.87];}
 if(preset==='rowband'){b.hands.left=[-.33,.61,.02];b.hands.right=[.33,.61,.02];}
 if(preset==='side'){b.hands.right=[.02,.83,-.28];}
 if(preset==='balance'){b.feet.right=[.18,.77,.26];}
 if(preset==='lunge'){b.root[1]=.84;}
 if(preset==='child'){b.root[1]=.30;b.hands.left=[-.28,.055,.55];b.hands.right=[.28,.055,.55];}
 return {version:1,preset,reviewStatus:'illustrative',frames:[a,b],camera:{azimuth:.58,elevation:.35,distance:3.55,target:[0,.72,0]}};
}
// Rotate a direction about a unit axis without changing bone length.
function axisRotate(v,axis,angle){return add(add(mul(v,Math.cos(angle)),mul(cross(axis,v),Math.sin(angle))),mul(axis,dot(axis,v)*(1-Math.cos(angle))));}
function joints(frame){
 const root=frame.root,e=frame.euler, world=p=>add(root,rotate(p,e));
 // The pelvis stays anchored while the ribcage can twist and flex independently.
 // Segment lengths remain fixed; curvature is a small local spine offset.
 const bend=clamp(frame.spineBend||0,-.6,.6), twist=clamp(frame.upperYaw||0,-1.3,1.3);
 const spineLocal=(height)=>[0,height-.025*Math.abs(bend)*height,Math.sin(bend)*height*height*.33];
 const shoulderLocal=(side)=>add(spineLocal(.54+(frame.shoulderLift||0)),rotY([side==='left'?-.215:.215,0,0],twist));
 const hip={left:world([-.13,0,0]),right:world([.13,0,0])};
 const shoulder={left:world(shoulderLocal('left')),right:world(shoulderLocal('right'))};
 const neck=world(spineLocal(.70));
 const head=add(neck,rotate(rotate([0,.21,.015],[frame.headPitch||0,frame.headYaw||0,frame.headRoll||0]),e));
 const j={root,hip,shoulder,neck,head,spineMid:world(spineLocal(.28)),spineUpper:world(spineLocal(.52)),pelvicTilt:clamp(frame.pelvicTilt||0,-.35,.35),hands:{},feet:{},elbow:{},knee:{},toes:{},palms:{},thumbs:{},clamped:[]};
 for(const side of ['left','right']){
  let s=side==='left'?-1:1;
  // Anatomical hinge pole is anchored to the current body rotation, not a fixed
  // screen-plane vertex. This avoids reversed bends when the torso is rotated.
  const anatomicElbow=add(shoulder[side],rotate([s*.11,-.30,-.33],add(e,[0,twist*.5,0])));
  const anatomicKnee=add(hip[side],rotate([s*.02,-.17,.58],e));
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
    const dir=ang=>norm(rotate([out*Math.sin(a),-Math.cos(ang)*Math.cos(a),Math.sin(ang)*Math.cos(a)],e));
    k.mid=add(hip[side],mul(dir(f),bones.thigh));
    if(leg.axialRotation!=null){
      const upper=dir(f),hinge=norm(rotate([0,-Math.sin(f),-Math.cos(f)],e));
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
    const upper=norm(rotate(rotZ(rotX([0,-1,0],-flex),s*abduct),e));
    const hinge=norm(rotate(rotZ(rotX([0,-1,0],-flex-Math.PI/2),s*abduct),e));
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
  let ankleAxis=rotate([1,0,0],e);
  let neutral=cross(shinDirection,ankleAxis);
  if(len(neutral)<1e-4)neutral=rotate([0,0,1],e);
  neutral=norm(neutral);
  const toeDirection=norm(add(mul(neutral,Math.cos(pitch)),mul(shinDirection,Math.sin(pitch))));
  j.toes[side]=add(j.feet[side],mul(toeDirection,.18));
  // Wrist flexion and forearm pronation/supination change the hand orientation
  // but never move the elbow or stretch either arm segment.
  const forearm=norm(sub(j.hands[side],j.elbow[side]));
  const wrist=clamp(arm?.wristFlexion||0,-1.04,1.04);
  const turn=clamp(arm?.forearmRotation||0,-1.57,1.57);
  let wristAxis=norm(rotate([1,0,0],e));
  let wristBend=cross(forearm,wristAxis);
  if(len(wristBend)<1e-4)wristBend=rotate([0,0,1],e);
  wristBend=norm(wristBend);
  const palmDir=norm(add(mul(forearm,Math.cos(wrist)),mul(wristBend,Math.sin(wrist))));
  j.palms[side]=add(j.hands[side],mul(palmDir,.115));
  let thumbAxis=sub(wristAxis,mul(palmDir,dot(wristAxis,palmDir)));
  if(len(thumbAxis)<1e-4)thumbAxis=cross(palmDir,rotate([0,1,0],e));
  thumbAxis=norm(thumbAxis);
  const thumbRot=add(mul(thumbAxis,Math.cos(turn)),mul(cross(palmDir,thumbAxis),Math.sin(turn)));
  j.thumbs[side]=add(j.hands[side],mul(norm(thumbRot),.064));
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
 const j=joints(frame),e=frame.euler,s=side==='left'?-1:1;
 const upper=norm(unrotate(sub(kind==='arm'?j.elbow[side]:j.knee[side],kind==='arm'?j.shoulder[side]:j.hip[side]),e));
 const lower=norm(unrotate(sub(kind==='arm'?j.hands[side]:j.feet[side],kind==='arm'?j.elbow[side]:j.knee[side]),e));
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
 f.root=reflectX(f.root);f.euler=[f.euler[0],-f.euler[1],-f.euler[2]];
 for(const key of ['headYaw','headRoll','upperYaw'])if(f[key]!=null)f[key]=-f[key];
 for(const key of ['hands','feet','poles','pointLocks']){
   if(f[key]!=null)f[key]=Object.fromEntries(Object.entries(f[key]).map(([name,pos])=>[swapSide(name),reflectX(pos)]));
 }
 if(f.footPitch!=null)f.footPitch=Object.fromEntries(Object.entries(f.footPitch).map(([side,value])=>[swapSide(side),value]));
 for(const key of ['armAngles','legAngles'])if(f[key]!=null)
   f[key]=Object.fromEntries(Object.entries(f[key]).map(([side,a])=>[swapSide(side),mirrorControls(a)]));
 for(const prop of f.equipment||[]){
   for(const key of ['position','positionB','positionC'])if(prop[key]!=null)prop[key]=reflectX(prop[key]);
   for(const key of ['anchor','anchorA','anchorB','anchorC','route'])if(typeof prop[key]==='string')prop[key]=swapSide(prop[key]);
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
 if(limbError(j,joints(candidate),kind,side)>1e-5)return false;
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
     point('ankle'+side,j.feet[side],.048);
   }
   return out;
 }
 function solids(frame){
   // Contact is permitted at surfaces: the solid volumes, not decorative
   // shadows or the full area behind a finite wall, are used for collisions.
   const out=[{id:'padló',type:'floor',height:0}];
   for(const [index,q] of (frame.equipment||[]).entries()){
     const id=q.type+index;
     // The mat is a thin compliant floor covering, not an obstacle: the
     // underlying floor collision plane already prevents passing through it.
     if(q.type==='wall')out.push({id,type:'box',centre:q.position||[0,1,-.62],half:[1.24,1.1,.025]});
     if(q.type==='chair'){
       out.push({id:id+'-ülőlap',type:'box',centre:q.position||[0,.30,-.14],half:[.43,.075,.42]});
       out.push({id:id+'-háttámla',type:'box',centre:add(q.position||[0,.30,-.14],[0,.32,-.33]),half:[.43,.35,.07]});
     }
     if(q.type==='step')out.push({id,type:'box',centre:q.position||[0,.13,.48],half:[.43,.13,.28]});
     if(q.type==='block')out.push({id,type:'box',centre:q.position||[0,.09,.23],half:[.22,.09,.14]});
     if(q.type==='ball'||q.type==='softBall')out.push({id,type:'sphere',centre:q.position||[0,.36,.20],radius:.34});
     if(q.type==='roller')out.push({id,type:'box',centre:q.position||[0,.18,-.2],half:[.28,.14,.14]});
     if(q.type==='balancePad')out.push({id,type:'box',centre:q.position||[0,.04,.3],half:[.27,.04,.21]});
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
   if(o.type==='box')return Math.max(0,v.r-signedBox(v.p,o.centre,o.half));
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
   seg('torso',lerp(j.root,j.spineMid,.15),j.spineUpper,.125);
   seg('head',j.head,j.head,.105);
   for(const side of ['left','right']){
     seg('upperArm-'+side,lerp(j.shoulder[side],j.elbow[side],.28),j.elbow[side],.062);
     seg('forearm-'+side,j.elbow[side],j.hands[side],.048);
     seg('hand-'+side,j.hands[side],j.palms[side],.035);
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
     if(adjacent(a.id,b.id))continue;
     const r=a.r+b.r-.014;
     if(r<=0||a.a.some((_,n)=>Math.min(a.a[n],a.b[n])-Math.max(b.a[n],b.b[n])>r||
       Math.min(b.a[n],b.b[n])-Math.max(a.a[n],a.b[n])>r))continue;
     const depth=r-segmentDistance(a.a,a.b,b.a,b.b);
     if(depth>.003)out.set('body:'+ [a.id,b.id].sort().join('/'),depth);
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
   if(typeof b==='number')return (typeof a==='number'?a:0)*(1-t)+b*t;
   if(Array.isArray(b))return b.map((x,i)=>i<(Array.isArray(a)?a.length:0)?interpolate(a[i],x,t):x);
   if(b&&typeof b==='object'){
     const out={};
     for(const k of Object.keys(b))out[k]=interpolate(a?.[k],b[k],t);
     for(const k of Object.keys(a||{}))if(!(k in b)&&t<1)out[k]=a[k];
     return out;
   }
   return b;
 }
 function transition(before,after,steps=28){
   const from=joints(before),to=joints(after);
   const baseline=overlaps(before,from);
   const locks=before.pointLocks||{},baseLockErrors=lockErrors(from,locks);
   let travel=0;
   const keys=['root','neck','head','spineUpper'];
   for(const key of keys)travel=Math.max(travel,len(sub(from[key],to[key])));
   for(const key of ['hands','feet','elbow','knee','toes'])for(const side of ['left','right'])travel=Math.max(travel,len(sub(from[key][side],to[key][side])));
   steps=Math.min(256,Math.max(steps,Math.ceil(travel/.027)));
   let accepted=before,ratio=0,issue=null;
   // A swept edit is never allowed to jump through solid surfaces even if its
   // final location is beyond them. Cumulative stepping is robust on mobile.
   for(let i=1;i<=steps;i++){
     const f=interpolate(before,after,i/steps),j=joints(f);
     let err=worse(overlaps(f,j),baseline);
     if(!err)for(const [key,depth] of lockErrors(j,locks)){
       if(depth>Math.max(.005,baseLockErrors.get(key)||0)+1e-7){err={id:'lock:'+key,depth};break;}
     }
     if(err){issue=err;break;}
     ratio=i/steps;accepted=f;
   }
   return {frame:accepted,fraction:ratio,limited:ratio<1,issue:issue?.id||null};
 }
 function report(frame){const issues=overlaps(frame);return {count:issues.size,examples:[...issues.entries()].sort((a,b)=>b[1]-a[1]).slice(0,5).map(([name,depth])=>({name,depth:+depth.toFixed(4)}))};}
 return {bodyProbes,bodySegments,selfOverlaps,segmentDistance,solids,overlaps,transition,report,worse};
})();

function mul4(a,b){let c=new Float32Array(16);for(let i=0;i<4;i++)for(let k=0;k<4;k++)for(let j=0;j<4;j++)c[j*4+i]+=a[k*4+i]*b[j*4+k];return c;}
function perspective(fov,aspect,n,f){let t=1/Math.tan(fov/2),a=new Float32Array(16);a[0]=t/aspect;a[5]=t;a[10]=-(f+n)/(f-n);a[11]=-1;a[14]=-2*f*n/(f-n);return a;}
function lookAt(eye,at){let z=norm(sub(eye,at)),x=norm(cross([0,1,0],z)),y=cross(z,x);return new Float32Array([x[0],y[0],z[0],0,x[1],y[1],z[1],0,x[2],y[2],z[2],0,-dot(x,eye),-dot(y,eye),-dot(z,eye),1]);}
function model(centre,xAxis,yAxis,zAxis){return new Float32Array([xAxis[0],xAxis[1],xAxis[2],0,yAxis[0],yAxis[1],yAxis[2],0,zAxis[0],zAxis[1],zAxis[2],0,...centre,1]);}
function segmentM(a,b,rX,rZ){let y=sub(b,a),l=len(y);if(l<1e-5)return null;y=norm(y);let tmp=Math.abs(dot(y,[0,0,1]))>.96?[1,0,0]:[0,0,1];let x=norm(cross(y,tmp)),z=norm(cross(x,y));return model(mul(add(a,b),.5),mul(x,rX),mul(y,l*.55),mul(z,rZ));}
function sphereM(pos,size){return model(pos,[size[0],0,0],[0,size[1],0],[0,0,size[2]]);}
const BAND_ANCHORS=['leftHand','rightHand','leftAnkle','rightAnkle','leftKnee','rightKnee','leftToe','rightToe','floorLeft','floorRight','fixed'];
// Move the root in world coordinates. IK endpoints retain their positions;
// FK limbs follow the root. The swept collision guard owns final acceptance.
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
 function setup(){const w=Math.max(300,canvas.clientWidth),h=Math.max(240,canvas.clientHeight),dpr=Math.min(window.devicePixelRatio||1,2);if(canvas.width!==Math.floor(w*dpr)||canvas.height!==Math.floor(h*dpr)){canvas.width=Math.floor(w*dpr);canvas.height=Math.floor(h*dpr);gl.viewport(0,0,canvas.width,canvas.height);}dims={w,h};let c=camera;let el=clamp(c.elevation,-1.35,1.35);c.elevation=el;let eye=add(c.target,[c.distance*Math.sin(c.azimuth)*Math.cos(el),c.distance*Math.sin(el),c.distance*Math.cos(c.azimuth)*Math.cos(el)]);viewproj=mul4(perspective(Math.PI/4,w/h,.04,90),lookAt(eye,c.target));gl.uniformMatrix4fv(uVP,false,viewproj);return eye;}
 function drawM(M,color,opacity=1){if(!M)return;gl.uniformMatrix4fv(uM,false,M);gl.uniform3fv(uC,new Float32Array(palette[color]||color));gl.uniform1f(uO,opacity);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0);}
 const ell=(centre,s,c,o=1)=>drawM(sphereM(centre,s),c,o),limb=(a,b,th,c)=>drawM(segmentM(a,b,th,th),c);
 function poly(points,th,color){for(let i=1;i<points.length;i++)limb(points[i-1],points[i],th,color);}
 function bezier(a,b,c,d,t){let u=1-t;return add(add(mul(a,u*u*u),mul(b,3*u*u*t)),add(mul(c,3*u*t*t),mul(d,t*t*t)));}
 // Stable attachment names shared with the editor and the JSON validator.
 function bandAnchorPoint(anchor,frame,j,prop,which){
   const fixed=which==='A' ? prop.position : which==='C' ? prop.positionC : (prop.positionB||prop.position);
   const look={leftHand:j.hands.left,rightHand:j.hands.right,
     leftAnkle:j.feet.left,rightAnkle:j.feet.right,leftKnee:j.knee.left,rightKnee:j.knee.right,
     leftToe:j.toes.left,rightToe:j.toes.right,
     floorLeft:[j.feet.left[0],.035,j.feet.left[2]],floorRight:[j.feet.right[0],.035,j.feet.right[2]],
     fixed:fixed||[0,.25,.85]};
   if(!Object.hasOwn(look,anchor))throw Error('Ismeretlen szalagrögzítés: '+anchor);
   return [...look[anchor]];
 }
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
           const q=p.map((x,i)=>Math.abs(x-solid.centre[i])-solid.half[i]);
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
 function bandSegments(prop,frame,j){
   const chosen=prop.anchorA&&prop.anchorB;
   if(chosen){const a=bandAnchorPoint(prop.anchorA,frame,j,prop,'A'),b=bandAnchorPoint(prop.anchorB,frame,j,prop,'B');
     if(prop.anchorC){const c=bandAnchorPoint(prop.anchorC,frame,j,prop,'C');return [[a,c],[c,b]];}
     return [[a,b]];}
   // Backward-compatibility with the 158 bundled movement records.
   switch(prop.anchor){
     case 'hands':return [[j.hands.left,j.hands.right]];
     case 'knees':return [[j.knee.left,j.knee.right]];
     case 'ankle':return [[j.feet.left,j.feet.right]];
     case 'fixed':return [[[ -.82,.35,.58],j.hands.left],[[.82,.35,.58],j.hands.right]];
     default:return [[j.feet.left,j.hands.left],[j.feet.right,j.hands.right]];
   }
 }
 function renderEquipment(frame,j){for(const prop of frame.equipment||[]){let type=prop.type;
  if(type==='wall'){drawM(model(prop.position||[0,1,-.62],[1.24,0,0],[0,1.1,0],[0,0,.025]),'floor');}
  if(type==='mat'){drawM(model([0,.008,.12],[.52,0,0],[0,.022,0],[0,0,1.42]),'mat');}
  if(type==='chair'){let centre=prop.position||[0,.30,-.14],shift=sub(centre,[0,.30,-.14]);ell(centre,[.43,.08,.42],'equip');drawM(segmentM(add([0,.26,-.47],shift),add([0,.95,-.47],shift),.06,.07),'equip');for(let x of [-.35,.35])for(let z of [-.40,.12])limb(add([x,.27,z],shift),add([x,.03,z],shift),.035,'equip');}
  if(type==='step'){ell(prop.position||[0,.13,.48],[.43,.13,.28],'equip');}
  if(type==='block'){ell(prop.position||[0,.09,.23],[.22,.09,.14],'equip');}
  if(type==='ball'||type==='softBall'){ell(prop.position||[0,.36,.20],[.34,.34,.34],'equip');}
  if(type==='roller'){let pos=prop.position||[0,.18,-.2];ell(pos,[.28,.14,.14],'equip');}
  if(type==='balancePad'){ell(prop.position||[0,.04,.3],[.27,.04,.21],'equip');}
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
  if(type==='dumbbell'){for(let s of ['left','right']){let p=j.hands[s];limb(add(p,[-.10,0,0]),add(p,[.10,0,0]),.025,'metal');ell(add(p,[-.12,0,0]),[.035,.06,.06],'metal');ell(add(p,[.12,0,0]),[.035,.06,.06],'metal');}}
 }}
 function draw(frame,{edit=true,selected=null}={}){let eye=setup();gl.clearColor(.985,.978,1,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);gl.useProgram(program);gl.uniformMatrix4fv(uVP,false,viewproj);
 const j=joints(frame);lastJoints=j;
 // ground follows world coordinates. No automatic nonuniform body shrinking.
 ell([0,-.10,0],[1.60,.085,1.80],'floor');
 // Props drawn before character; the rubber band is routed away from the trunk.
 renderEquipment(frame,j);
 const rp=j.root,axis=(p)=>rotate(p,frame.euler),neck=j.neck;
 // torso and pelvis; geometry stays constant length through every pose.
 let torsoUpper=j.spineUpper;limb(add(rp,axis([0,.025,0])),j.spineMid,.19,'shirt');limb(j.spineMid,torsoUpper,.19,'shirt');
 // Local pelvic articulation: only the pelvis tilts. The ribcage, shoulders,
 // elbow/hands and neck keep the same world-space transform. Do not translate
 // or rotate the whole body for a small posterior pelvic tilt.
 const tilt=j.pelvicTilt;
 const pelvisRotation=[frame.euler[0]+tilt,frame.euler[1],frame.euler[2]];
 const pelvisCentre=add(rp,axis([0,.10,0]));
 drawM(model(pelvisCentre,rotate([.23,0,0],pelvisRotation),
     rotate([0,.13,0],pelvisRotation),rotate([0,0,.19],pelvisRotation)),'leggings');
 // Subtle waistband cue helps distinguish the tilt from the neutral frame.
 const beltCentre=add(pelvisCentre,rotate([0,.10,0],pelvisRotation));
 drawM(model(beltCentre,rotate([.20,0,0],pelvisRotation),
     rotate([0,.023,0],pelvisRotation),rotate([0,0,.14],pelvisRotation)),'shirt');
 // upper chest to neck, neckline
 limb(torsoUpper,neck,.105,'skin');
 for(const side of ['left','right']){
  const shoulder=j.shoulder[side],hip=j.hip[side],elbow=j.elbow[side],hand=j.hands[side],knee=j.knee[side],foot=j.feet[side];
  ell(shoulder,[.12,.115,.12],'shirt');limb(shoulder,elbow,.092,'skin');ell(elbow,[.075,.075,.075],'skin');limb(elbow,hand,.065,'skin');ell(hand,[.06,.055,.055],'skin');limb(hand,j.palms[side],.046,'skin');limb(hand,j.thumbs[side],.018,'skin');
  ell(hip,[.11,.11,.11],'leggings');limb(hip,knee,.13,'leggings');ell(knee,[.109,.108,.105],'leggings');limb(knee,foot,.092,'leggings');
  limb(foot,j.toes[side],.067,'shoes');ell(j.toes[side],[.065,.050,.070],'shoes');
 }
 // Move the head with the neck and look direction; face is on head's local +Z axis.
 const head=j.head,hyaw=frame.headYaw||0,hpitch=frame.headPitch||0,hroll=frame.headRoll||0;
 const headEuler=add(frame.euler,[hpitch,hyaw,hroll]);
 const faceForward=rotate([0,0,1],headEuler);let headUp=rotate([0,1,0],headEuler),headRight=norm(cross(headUp,faceForward));if(len(headRight)<.01)headRight=[1,0,0];headUp=norm(cross(faceForward,headRight));
 ell(head,[.132,.171,.126],'skin');ell(add(head,add(mul(headUp,.135),mul(faceForward,-.036))),[.137,.068,.117],'hair');ell(add(head,add(mul(headUp,.15),mul(faceForward,-.12))),[.065,.068,.062],'hair');
 for(let s of [-1,1])ell(add(head,add(add(mul(headRight,.052*s),mul(headUp,.016)),mul(faceForward,.117))),[.012,.016,.014],'hair');
 ell(add(head,add(mul(faceForward,.129),mul(headUp,-.035))),[.022,.018,.025],'skin');
 if(edit){
   const handle=(pos,key)=>{const r=key===selected? .068:.045;ell(pos,[r,r,r],key===selected?[.15,.8,.48]:'handle');};
   handle(j.root,'root');
   for(let s of ['left','right']){handle(j.hands[s],s+'Hand');handle(j.feet[s],s+'Foot');}
   for(const key of Object.keys(frame.pointLocks||{})){
     if(LOCK_KEYS.includes(key))ell(lockPoint(j,key),[.055,.055,.055],[.19,.49,.92]);
   }
 }
 return j;}
 function project(p){let q=[p[0],p[1],p[2],1],r=[];for(let i=0;i<4;i++)r.push(viewproj[i]*q[0]+viewproj[4+i]*q[1]+viewproj[8+i]*q[2]+viewproj[12+i]*q[3]);if(r[3]<=.001)return null;return [(r[0]/r[3]*.5+.5)*dims.w,(-r[1]/r[3]*.5+.5)*dims.h];}
 function pick(x,y){let best=null,dist=32;const root=project(lastJoints?.root||[999,999,999]);if(root){const d=Math.hypot(x-root[0],y-root[1]);if(d<dist){dist=d;best={kind:'root'};}}for(let side of ['left','right'])for(let kind of ['hands','feet']){let screen=project(lastJoints?.[kind]?.[side]||[999,999,999]);if(!screen)continue;let d=Math.hypot(x-screen[0],y-screen[1]);if(d<dist){dist=d;best={kind,side};}}return best;}
 function dragTarget(frame,sel,dx,dy,depth=false,axis='free'){const c=camera,right=[Math.cos(c.azimuth),0,-Math.sin(c.azimuth)],up=[-Math.sin(c.azimuth)*Math.sin(c.elevation),Math.cos(c.elevation),-Math.cos(c.azimuth)*Math.sin(c.elevation)],forward=[Math.sin(c.azimuth),0,Math.cos(c.azimuth)];let size=c.distance/Math.max(280,dims.h)*1.55;let delta=depth||axis==='depth'?mul(forward,-dy*size):add(mul(right,dx*size),mul(up,-dy*size));
 if(axis==='x')delta=[dx*size,0,0];else if(axis==='y')delta=[0,-dy*size,0];else if(axis==='z')delta=[0,0,-dy*size];
 if(sel.kind==='root'){moveRoot(frame,delta);return joints(frame).clamped;}
 let key=sel.kind==='hands'?'hands':'feet';let origin=frame[key][sel.side];
 // The studio prepares an equivalent target-mode frame before interpolation.
 if((key==='feet'&&frame.legAngles?.[sel.side])||(key==='hands'&&frame.armAngles?.[sel.side]))return null;
 origin=frame[key][sel.side];
 let point=add(origin,delta);
 if(key==='feet')point[1]=Math.max(.05,point[1]);
 frame[key][sel.side]=point;return joints(frame).clamped;}
 return {draw,setCamera(c){camera=c;},getCamera:()=>camera,pick,dragTarget,project,setup,getJoints:()=>lastJoints,release(){gl.getExtension('WEBGL_lose_context')?.loseContext();}};
}
function checkPose(p){if(!p||p.version!==1||!Array.isArray(p.frames)||p.frames.length<1||p.frames.length>6)throw Error('Nem támogatott 3D pózformátum.');for(let f of p.frames){for(let k of ['root','euler'])if(!Array.isArray(f[k])||f[k].length!==3||f[k].some(x=>!Number.isFinite(x)))throw Error('Hibás testhelyzet: '+k);for(const k of ['headYaw','headPitch','headRoll','shoulderLift','pelvicTilt'])if(f[k]!==undefined&&(!Number.isFinite(f[k])||Math.abs(f[k])>2))throw Error('Hibás ízületi vezérlő: '+k);if(Math.abs(f.pelvicTilt||0)>.35)throw Error('Túl nagy medencebillentés.');
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
 if(['band','miniband','strap'].includes(prop.type)){
   if((prop.anchorA||prop.anchorB)&&(!BAND_ANCHORS.includes(prop.anchorA)||!BAND_ANCHORS.includes(prop.anchorB)))throw Error('Hibás gumiszalag-rögzítés.');
   if(prop.anchorC&&!BAND_ANCHORS.includes(prop.anchorC))throw Error('Hibás harmadik szalagpont.');
   if(prop.positionC&&(!Array.isArray(prop.positionC)||prop.positionC.length!==3||prop.positionC.some(x=>!Number.isFinite(x)||Math.abs(x)>10)))throw Error('Hibás harmadik szalagkoordináta.');
   if(prop.route&&!['auto','direct','front','back','left','right'].includes(prop.route))throw Error('Hibás szalagvezetés.');
 }
}for(let kind of ['hands','feet'])for(let side of ['left','right'])if(!Array.isArray(f[kind]?.[side])||f[kind][side].length!==3||f[kind][side].some(x=>!Number.isFinite(x)))throw Error('Hiányzó vagy hibás végpont: '+kind+'/'+side);}}
global.MozduljRig={initialPose,baseFrame,joints,solve2,shaded3D,moveRoot,checkPose,mirrorFrame,mirrorIssue,copyLimb,copyIssue,inferAngles,prepareAngleMode,prepareTargetMode,limbError,LOCK_KEYS,lockPoint,setPointLock,lockErrors,BAND_ANCHORS,Collision:Collision3D,math:{V,add,sub,mul,dot,cross,len,norm,rotate}};
})(window);
