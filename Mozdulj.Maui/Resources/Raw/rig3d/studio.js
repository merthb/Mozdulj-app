/* Interactive offline rig editor with a MAUI-compatible base64 bridge. */
(()=>{'use strict';let studio,pose,frameIndex=0,mode='orbit',drag=null,initError=null;
const $=id=>document.getElementById(id),canvas=$('scene');
const clone=o=>JSON.parse(JSON.stringify(o));
const rad=x=>x*Math.PI/180,deg=x=>x*180/Math.PI;
function current(){return pose.frames[frameIndex];}
function msg(s){$('status').textContent=s;}
// All pose-affecting controls share the same swept 3D collision guard.
// Camera operations are intentionally excluded: orbit/pan never move the body.
function guardedEdit(change){
 const before=clone(current());change(current());
 const candidate=clone(current());const result=MozduljRig.Collision.transition(before,candidate);
 pose.frames[frameIndex]=result.frame;
 if(result.limited){sync();msg('Ütközési korlát: a végtag nem haladhat át a padlón, falon vagy eszközön. ('+result.issue+')');}
 else show();
 return result;
}

function updatePhaseControls(){
 if(!pose)return;
 const n=pose.frames.length;
 $('phaseLabel').textContent=pose.frames[frameIndex].name||('Fázis '+(frameIndex+1));
 $('phaseProgress').textContent=(frameIndex+1)+' / '+n;
 $('phasePrev').disabled=frameIndex===0;
 $('phaseNext').disabled=frameIndex>=n-1;
 $('phaseName').value=pose.frames[frameIndex].name||('Fázis '+(frameIndex+1));
 $('addPhase').disabled=n>=6;
 $('removePhase').disabled=n<=1;
 $('phasePrev').setAttribute('aria-disabled',String(frameIndex===0));
 $('phaseNext').setAttribute('aria-disabled',String(frameIndex>=n-1));
}
function show(){if(!studio)return;let f=current();const j=studio.draw(f,{edit:!document.body.classList.contains('view-only')});const collisions=MozduljRig.Collision.report(f);$('canvasStatus').textContent=j.bandBlocked?'A gumiszalag útvonala akadályba ütközik; módosítsd a rögzítést vagy a vezetést.':j.clamped.length?'A célpont elérési korlátra került: '+j.clamped.join(', '):collisions.count?'Régi pózadatban '+collisions.count+' geometriai átfedés; új mozgás nem mélyítheti.':'Rögzített végtaghossz · felület- és eszközütközés-védelem';$('frameNumber').textContent=(frameIndex+1)+' / '+pose.frames.length+' fázis';}

function sync(){let f=current();$('yaw').value=Math.round(deg(f.headYaw||0));$('pitch').value=Math.round(deg(f.headPitch||0));$('torso').value=Math.round(deg(f.euler[0]));$('roll').value=Math.round(deg(f.headRoll||0));$('shoulderLift').value=Math.round((f.shoulderLift||0)*100);$('pelvicTilt').value=Math.round(deg(f.pelvicTilt||0));
 const side=$('legSide').value||'right';const leg=f.legAngles?.[side]||{};
 $('hipFlex').value=Math.round(deg(leg.flexion||0));
 $('hipAbduct').value=Math.round(deg(leg.abduction||0));
 $('kneeFlex').value=Math.round(deg(leg.kneeFlexion||0));
 $('footPitch').value=Math.round(deg(f.footPitch?.[side]||0));
 const arm=f.armAngles?.[$('armSide').value||'right']||{};
 for(const [id,k] of [['armFlex','flexion'],['armAbduct','abduction'],['elbowFlex','elbowFlexion'],['wristFlex','wristFlexion'],['forearmRotation','forearmRotation']])$(id).value=Math.round(deg(arm[k]||0));
 $('preset').value=pose.preset;updatePhaseControls();syncBands();syncProps();$('zoom').value=studio.getCamera().distance;show();}
function syncBands(selected){
 const sel=$('bandSelect'),prior=selected===undefined?sel.value:selected;
 const items=current().equipment||[];
 sel.innerHTML='<option value="new">Új szalag</option>';
 items.forEach((obj,i)=>{if(['band','miniband','strap'].includes(obj.type)){
   const opt=document.createElement('option');opt.value=String(i);opt.textContent='Szalag '+(i+1);sel.appendChild(opt);
 }});
 sel.value=[...sel.options].some(o=>o.value===prior)?prior:'new';
 fillSelectedBand();
}
const POSITIONABLE=['chair','ball','softBall','roller','wall','step','block','balancePad'];
const BASE_POS={chair:[0,.30,-.14],ball:[0,.36,.20],softBall:[0,.36,.20],roller:[0,.18,-.20],wall:[0,1,-.62],step:[0,.13,.48],block:[0,.09,.23],balancePad:[0,.04,.3]};
function syncProps(selected){
 const sel=$('propSelect'),prior=selected===undefined?sel.value:selected;
 sel.innerHTML='<option value="none">Nincs mozgatható eszköz</option>';
 (current().equipment||[]).forEach((prop,i)=>{if(POSITIONABLE.includes(prop.type)){
 const opt=document.createElement('option');opt.value=String(i);opt.textContent=prop.type+' · '+(i+1);sel.appendChild(opt);
 }});
 sel.value=[...sel.options].some(o=>o.value===prior)?prior:(sel.options.length>1?sel.options[1].value:'none');
 fillProp();
}
function fillProp(){const i=$('propSelect').value;
 const prop=i==='none'?null:current().equipment[+i],pos=prop?(prop.position||BASE_POS[prop.type]):[0,.36,.2];
 for(const [idx,key] of ['X','Y','Z'].entries())$('prop'+key).value=pos[idx];
 $('moveProp').disabled=!prop;
}
function freePlacement(type){
 const base=BASE_POS[type];if(!base)return null;
 const offsets=[[0,0,0],[0,0,-.70],[.85,0,0],[-.85,0,0],[0,0,.80],[1.4,0,0],[-1.4,0,0],[0,0,-1.5]];
 for(const offset of offsets){
   const pos=base.map((v,i)=>v+offset[i]);
   const before=clone(current()),after=clone(before);after.equipment.push({type,position:pos});
   if(!MozduljRig.Collision.transition(before,after).limited)return pos;
 }
 return null;
}
function updateFixedAnchorInputs(){
 $('fixedAnchorA').hidden=$('bandAnchorA').value!=='fixed';
 $('fixedAnchorB').hidden=$('bandAnchorB').value!=='fixed';
 $('fixedAnchorC').hidden=$('bandAnchorC').value!=='fixed';
}
function setFixedInputs(prefix,v){const a=v|| (prefix==='A'?[-.8,.35,.45]:[.8,.35,.45]);for(const [idx,k] of ['X','Y','Z'].entries())$('band'+prefix+k).value=a[idx];}
function readFixedInputs(prefix){const v=['X','Y','Z'].map(k=>Number($('band'+prefix+k).value));if(v.some((n,i)=>!Number.isFinite(n)||Math.abs(n)>3||(i===1&&n<0)))throw Error('A rögzített pont koordinátája nem megengedett.');return v;}
function fillSelectedBand(){
 const i=$('bandSelect').value;
 if(i==='new'){ $('bandAnchorA').value='leftHand';$('bandAnchorB').value='rightHand';$('bandRoute').value='auto';$('bandAnchorC').value='none';setFixedInputs('A');setFixedInputs('B');setFixedInputs('C',[0,.35,.45]);updateFixedAnchorInputs();return; }
 const obj=current().equipment[+i];
 if(!obj)return;
 const legacy=obj.anchor||'feet';
 $('bandAnchorA').value=obj.anchorA||({knees:'leftKnee',ankle:'leftAnkle',hands:'leftHand'}[legacy]||'leftAnkle');
 $('bandAnchorB').value=obj.anchorB||({knees:'rightKnee',ankle:'rightAnkle',hands:'rightHand'}[legacy]||'leftHand');
 $('bandRoute').value=obj.route||'auto';$('bandAnchorC').value=obj.anchorC||'none';setFixedInputs('A',obj.position);setFixedInputs('B',obj.positionB);setFixedInputs('C',obj.positionC||[0,.35,.45]);updateFixedAnchorInputs();
}
function switchFrame(i){frameIndex=Math.max(0,Math.min(i,pose.frames.length-1));sync();}
function setPreset(name){pose=MozduljRig.initialPose(name);frameIndex=0;studio.setCamera(pose.camera);sync();msg('Pózsablon betöltve: '+$('preset').selectedOptions[0].textContent);}
function load(p){MozduljRig.checkPose(p);pose=clone(p);frameIndex=0;studio.setCamera(pose.camera||MozduljRig.initialPose().camera);sync();msg('A 3D testhelyzet betöltve.');}
function b64toStr(s){return decodeURIComponent(escape(atob(s)));}function strtoB64(s){return btoa(unescape(encodeURIComponent(s)));}
window.loadRigBase64=s=>{try{load(JSON.parse(b64toStr(s)));return true;}catch(e){msg('Betöltési hiba: '+e.message);return false;}};
window.exportRigBase64=()=>strtoB64(JSON.stringify({...pose,camera:studio.getCamera()}));
window.exportRigJson=()=>JSON.stringify({...pose,camera:studio.getCamera()},null,2);
window.getRigHealth=()=>JSON.stringify({webgl:!!studio,frames:pose?.frames?.length,renderer:'pure-webgl-offline'});
window.setReadOnlyMode=()=>{
  document.body.classList.add('view-only');
  document.querySelector('main').style.gridTemplateColumns='1fr';
  document.querySelector('main > section:nth-child(2)').style.display='none';
  document.querySelector('.header strong').textContent='3D mozgásábra';
  document.querySelector('.header .hint').textContent='A modellen húzva körbeforgathatod a nézetet';
  document.querySelector('.float-l').style.display='none';
  document.querySelector('.float-b').style.display='none';
  mode='orbit'; // drag always rotates the camera in the read-only viewer
  updatePhaseControls();show();
};
function connect(){try{studio=MozduljRig.shaded3D(canvas);setPreset('stand');}catch(e){initError=e;$('canvasStatus').textContent=e.message;msg('WebGL nem áll rendelkezésre: '+e.message);return;}
 $('phasePrev').addEventListener('click',()=>switchFrame(frameIndex-1));
 $('phaseNext').addEventListener('click',()=>switchFrame(frameIndex+1));
 $('addPhase').addEventListener('click',()=>{
   if(!pose||pose.frames.length>=6){msg('Legfeljebb 6 fázis adható hozzá.');return;}
   const next=clone(current());
   next.name='Új fázis '+(pose.frames.length+1);
   pose.frames.splice(frameIndex+1,0,next);
   frameIndex++;
   sync();msg('Új 3D fázis hozzáadva. Módosítsd a testhelyzetet vagy nevezd át.');
 });
 $('removePhase').addEventListener('click',()=>{
   if(!pose||pose.frames.length<=1){msg('Legalább egy fázis szükséges.');return;}
   pose.frames.splice(frameIndex,1);
   frameIndex=Math.min(frameIndex,pose.frames.length-1);
   sync();msg('Az aktuális fázist töröltem.');
 });
 $('phaseName').addEventListener('input',e=>{
   if(!pose)return;
   current().name=e.target.value.slice(0,64);
   $('phaseLabel').textContent=current().name||('Fázis '+(frameIndex+1));
 });

 document.addEventListener('keydown',e=>{
   if(e.altKey||e.ctrlKey||e.metaKey||/^(INPUT|SELECT|TEXTAREA|BUTTON)$/.test(document.activeElement?.tagName||''))return;
   if(e.key==='ArrowLeft'&&frameIndex>0){e.preventDefault();switchFrame(frameIndex-1);}
   if(e.key==='ArrowRight'&&frameIndex<pose.frames.length-1){e.preventDefault();switchFrame(frameIndex+1);}
 });
 $('preset').addEventListener('change',e=>setPreset(e.target.value));
 $('yaw').oninput=e=>guardedEdit(f=>{f.headYaw=rad(+e.target.value);});
 $('pitch').oninput=e=>guardedEdit(f=>{f.headPitch=rad(+e.target.value);});
 $('torso').oninput=e=>guardedEdit(f=>{f.euler[0]=rad(+e.target.value);});
 $('roll').oninput=e=>guardedEdit(f=>{f.headRoll=rad(+e.target.value);});
 $('shoulderLift').oninput=e=>guardedEdit(f=>{f.shoulderLift=+e.target.value/100;});
 $('pelvicTilt').oninput=e=>guardedEdit(f=>{f.pelvicTilt=rad(+e.target.value);});
 $('armSide').onchange=sync;
 function editArm(field,value){const side=$('armSide').value;
   guardedEdit(frame=>{frame.armAngles??={};frame.armAngles[side]??={flexion:0,abduction:0,elbowFlexion:0,wristFlexion:0,forearmRotation:0};frame.armAngles[side][field]=rad(value);});}
 for(const [id,k] of [['armFlex','flexion'],['armAbduct','abduction'],['elbowFlex','elbowFlexion'],['wristFlex','wristFlexion'],['forearmRotation','forearmRotation']])
   $(id).oninput=e=>editArm(k,+e.target.value);
 $('legSide').onchange=sync;
 function editLeg(field,value){const side=$('legSide').value;
 guardedEdit(f=>{f.legAngles??={};f.legAngles[side]??={flexion:0,abduction:0,kneeFlexion:0};f.legAngles[side][field]=rad(value);});}
 $('hipFlex').oninput=e=>editLeg('flexion',+e.target.value);
 $('hipAbduct').oninput=e=>editLeg('abduction',+e.target.value);
 $('kneeFlex').oninput=e=>editLeg('kneeFlexion',+e.target.value);
 $('footPitch').oninput=e=>{const side=$('legSide').value;guardedEdit(f=>{f.footPitch??={};f.footPitch[side]=rad(+e.target.value);});};
 $('zoom').oninput=e=>{studio.getCamera().distance=+e.target.value;show();};
 $('addEquipment').onclick=()=>{
   const type=$('equip').value;
   if(['band','miniband','strap'].includes(type)){const result=guardedEdit(f=>f.equipment.push({type,anchorA:type==='miniband'?'leftKnee':'leftHand',anchorB:type==='miniband'?'rightKnee':'rightHand',route:'auto'}));
     if(!result.limited){syncBands(String(current().equipment.length-1));msg('Szalag beszúrva; lent beállíthatod mindhárom pontját.');}return;}
   if(!current().equipment.some(o=>o.type===type)){
     const pos=POSITIONABLE.includes(type)?freePlacement(type):null;
     if(POSITIONABLE.includes(type)&&!pos){msg('Az eszköznek nem találtam akadálymentes helyet. Módosítsd a pózt vagy válassz másik eszközt.');return;}
     const result=guardedEdit(f=>f.equipment.push(pos?{type,position:pos}:{type}));
     if(!result.limited){syncProps(String(current().equipment.length-1));msg('Eszköz hozzáadva: '+$('equip').selectedOptions[0].textContent);}
   }else msg('Ez az eszköz már szerepel ebben a fázisban.');
 };
 $('clearEquipment').onclick=()=>{current().equipment=[];syncBands('new');syncProps('none');show();msg('Az aktuális fázis eszközeit eltávolítottam.');};
 $('propSelect').onchange=fillProp;
 $('moveProp').onclick=()=>{
   const i=$('propSelect').value;if(i==='none')return;
   const pos=['X','Y','Z'].map(k=>Number($('prop'+k).value));
   if(pos.some((v,n)=>!Number.isFinite(v)||Math.abs(v)>3||(n===1&&v<0))){msg('A pozíció koordinátái nem megfelelőek.');return;}
   // Normalize old tools with implicit default coordinates before sweeping.
   // Otherwise adding a position field would teleport from world origin.
   current().equipment[+i].position??=[...BASE_POS[current().equipment[+i].type]];
   const result=guardedEdit(f=>{f.equipment[+i].position=pos;});
   if(!result.limited){syncProps(i);msg('Eszköz áthelyezve.');}
 };
 $('bandSelect').onchange=fillSelectedBand;
 $('bandAnchorA').onchange=updateFixedAnchorInputs;
 $('bandAnchorB').onchange=updateFixedAnchorInputs;
 $('bandAnchorC').onchange=updateFixedAnchorInputs;
 $('applyBand').onclick=()=>{
   const a=$('bandAnchorA').value,b=$('bandAnchorB').value;
   if(a===b){msg('A szalag két vége nem lehet ugyanazon a ponton.');return;}
   let old=$('bandSelect').value==='new'?null:current().equipment[+$('bandSelect').value];
   let o={type:old?.type||'band',anchorA:a,anchorB:b,route:$('bandRoute').value};
   if($('bandAnchorC').value!=='none')o.anchorC=$('bandAnchorC').value;
   try{if(a==='fixed')o.position=readFixedInputs('A');if(b==='fixed')o.positionB=readFixedInputs('B');if(o.anchorC==='fixed')o.positionC=readFixedInputs('C');}
   catch(e){msg(e.message);return;}
   const i=$('bandSelect').value;
   const result=guardedEdit(f=>{if(i==='new')f.equipment.push(o);else f.equipment[+i]=o;});
   if(result.limited)return;
   syncBands(i==='new'?String(current().equipment.length-1):i);
   msg('Szalagrögzítés mentve: '+$('bandAnchorA').selectedOptions[0].textContent+' – '+$('bandAnchorB').selectedOptions[0].textContent);
 };
 $('deleteBand').onclick=()=>{const i=$('bandSelect').value;
   if(i==='new'){msg('Válassz egy meglévő szalagot a törléshez.');return;}
   current().equipment.splice(+i,1);syncBands('new');show();msg('Szalag eltávolítva.');
 };
 for(const [id,a,e] of [['front',0,.08],['side',Math.PI/2,.12],['threequarter',.66,.33]])$(''+id).onclick=()=>{let c=studio.getCamera();c.azimuth=a;c.elevation=e;show();};
 $('fit').onclick=()=>{let c=studio.getCamera();Object.assign(c,{azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]});sync();};
 for(const m of ['orbit','move','pan'])$(m).onclick=()=>{mode=m;for(const b of ['orbit','move','pan'])$(b).classList.toggle('active',m===b);msg(m==='move'?'Fogj meg egy narancssárga kéz-/lábpontot, és húzd új helyre.':m==='pan'?'Húzással eltolhatod az egész képet, a testarányok változtatása nélkül.':'Húzással elforgathatod a kamerát.');};
 $('export').onclick=()=>{let s=window.exportRigJson();$('json').value=s;try{let blob=new Blob([s],{type:'application/json;charset=utf-8'});let url=URL.createObjectURL(blob);let a=document.createElement('a');a.download='mozdulj-3d-poz.json';a.href=url;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}catch{}msg('3D pózadat mentve.');};
 $('import').onclick=()=>{try{load(JSON.parse($('json').value));}catch(e){msg('Hibás JSON: '+e.message);}};
 function loc(e){let r=canvas.getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top};}
 canvas.addEventListener('pointerdown',e=>{if(!studio)return;canvas.setPointerCapture(e.pointerId);let p=loc(e),sel=mode==='move'?studio.pick(p.x,p.y):null;drag={x:p.x,y:p.y,sel,button:e.button};});
 canvas.addEventListener('pointermove',e=>{if(!drag||!studio)return;let p=loc(e),dx=p.x-drag.x,dy=p.y-drag.y,c=studio.getCamera();if(mode==='move'&&drag.sel)guardedEdit(f=>studio.dragTarget(f,drag.sel,dx,dy,e.shiftKey));else if(mode==='pan'||drag.button===2){let right=[Math.cos(c.azimuth),0,-Math.sin(c.azimuth)];let scale=c.distance/300;c.target=c.target.map((v,i)=>v-right[i]*dx*scale +(i===1?dy*scale:0));}else{c.azimuth-=dx*.012;c.elevation=Math.max(-1.25,Math.min(1.25,c.elevation+dy*.008));}drag.x=p.x;drag.y=p.y;show();});
 ['pointerup','pointercancel','lostpointercapture'].forEach(n=>canvas.addEventListener(n,()=>drag=null));canvas.addEventListener('contextmenu',e=>e.preventDefault());
 canvas.addEventListener('wheel',e=>{e.preventDefault();let c=studio.getCamera();c.distance=Math.max(1.8,Math.min(6.5,c.distance+Math.sign(e.deltaY)*.22));$('zoom').value=c.distance;show();},{passive:false});
 window.addEventListener('resize',show);const observer=new ResizeObserver(()=>show());observer.observe(canvas);
 window.addEventListener('visibilitychange',()=>{if(!document.hidden)show();});
}
connect();
})();
