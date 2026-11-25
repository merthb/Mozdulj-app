/* Interactive offline rig editor with a MAUI-compatible base64 bridge. */
(()=>{'use strict';let studio,pose,frameIndex=0,mode='orbit',drag=null,initError=null;
const $=id=>document.getElementById(id),canvas=$('scene');
const clone=o=>JSON.parse(JSON.stringify(o));
const rad=x=>x*Math.PI/180,deg=x=>x*180/Math.PI;
const history=new MozduljPoseHistory();
let selectedTarget=null,gesture=null,controlGesture=null;
let animation=null, draftBaseline=null, pendingPreset=null;
const snapshot=()=>{const data=clone(pose);delete data.camera;return {pose:data,frameIndex};};
function syncHistory(){ $('undoPose').disabled=!history.canUndo; $('redoPose').disabled=!history.canRedo; }
function closeGesture(){if(pose&&history.pending!==null)history.finish(snapshot());gesture=null;controlGesture=null;syncHistory();}
function beginGesture(owner){if(gesture!==owner){closeGesture();gesture=owner;history.begin(snapshot());}}
function tracked(change){
 const before=snapshot();
 change();
 history.record(before,snapshot());
 syncHistory();
}
function restoreHistory(state){
 if(!state)return;
 const camera=studio.getCamera();
 pose=clone(state.pose);pose.camera=camera;
 frameIndex=Math.min(state.frameIndex,pose.frames.length-1);
 sync();syncHistory();
}
function undoPose(){stopPreview();closeGesture();restoreHistory(history.undo(snapshot()));}
function redoPose(){stopPreview();closeGesture();restoreHistory(history.redo(snapshot()));}
window.resetRigHistory=()=>{history.clear();gesture=null;controlGesture=null;syncHistory();return true;};
const targetLabels={root:'Medence',leftHand:'Bal kéz',rightHand:'Jobb kéz',leftFoot:'Bal lábfej',rightFoot:'Jobb lábfej',leftKnee:'Bal térd',rightKnee:'Jobb térd',leftElbow:'Bal könyök',rightElbow:'Jobb könyök'};
function targetKey(sel){return sel?.kind==='root'?'root':sel?sel.side+(sel.kind==='hands'?'Hand':'Foot'):null;}
function bodyFocusFor(key){
 if(key==='root')return 'root';
 if(!key)return 'all';
 return (key.startsWith('left')?'left':'right')+(/Hand|Elbow/.test(key)?'Arm':'Leg');
}
function selectTab(name){
 for(const tab of document.querySelectorAll('.workspace-tabs [role=tab]')){
   const active=tab.id==='tab-'+name;
   tab.setAttribute('aria-selected',String(active));tab.tabIndex=active?0:-1;
   $(tab.getAttribute('aria-controls')).hidden=!active;
 }
 $('workspaceScroll').scrollTop=0;
}
function focusBody(value,open=true){
 $('bodyFocus').value=value;
 const group=value==='all'?'all':value.endsWith('Arm')?'arm':value.endsWith('Leg')?'leg':'central';
 for(const detail of document.querySelectorAll('[data-body-group]')){
   detail.hidden=group!=='all'&&detail.dataset.bodyGroup!==group;
   if(group!=='all'&&!detail.hidden)detail.open=true;
 }
 if(group==='arm')$('armSide').value=value.startsWith('left')?'left':'right';
 if(group==='leg')$('legSide').value=value.startsWith('left')?'left':'right';
 if(open)selectTab('body');
 if(pose)syncJointValues();
}
function chooseTarget(key){
 selectedTarget=key&&key!=='auto'?key:null;
 if(selectedTarget){focusBody(bodyFocusFor(selectedTarget));setMode('move');}
 else show();
}
function explicitTarget(key){
 if(key==='root')return {kind:'root'};
 if(!key||key==='auto')return null;
 return {side:key.startsWith('left')?'left':'right',kind:key.endsWith('Hand')?'hands':'feet'};
}
function setMode(m){
 mode=m;
 for(const b of ['orbit','move','pan'])$(b).classList.toggle('active',m===b);
 if(m==='move'&&!document.body.classList.contains('view-only'))selectTab('body');
 show();
}
function current(){return pose.frames[frameIndex];}
function msg(s){$('status').textContent=window.rigText(s);}
// All pose-affecting controls share the same swept 3D collision guard.
// Camera operations are intentionally excluded: orbit/pan never move the body.
function guardedEdit(change,prepare=null){
 stopPreview();
 const original=snapshot();
 const before=clone(current());
 if(prepare&&!prepare(before)){
   sync();msg('A jelenlegi póz ebben a vezérlési módban nem adható meg pontosan. A póz változatlan maradt.');
   return {limited:true,issue:'mode',fraction:0};
 }
 const candidate=clone(before);change(candidate);
 const result=MozduljRig.Collision.transition(before,candidate);
 if(result.fraction>0)pose.frames[frameIndex]=result.frame;
 history.record(original,snapshot());syncHistory();
 if(result.limited){
   sync();
   const issue=result.issue||'';
   if(issue.startsWith('lock:'))msg(window.rigText('Rögzített testpont: ')+window.rigText(targetLabels[issue.slice(5)])+' — '+window.rigText('Oldd fel a rögzítést a további mozgatáshoz.'));
   else if(issue.startsWith('body:'))msg(window.rigText('Testrészek ütközése: ')+issue.slice(5).split('/').map(bodyPartLabel).join(' – '));
   else msg(window.rigText('Ütközési korlát: a végtag nem haladhat át a padlón, falon vagy eszközön. (')+issue+')');
 }
 else {syncJointValues();show();}
 return result;
}

function bodyPartLabel(id){
 const [part,side]=id.split('-');
 const parts={torso:'Törzs',head:'Fej',upperArm:'Felkar',forearm:'Alkar',hand:'Kéz',thigh:'Comb',shin:'Lábszár',foot:'Lábfej'};
 const label=window.rigText(parts[part]||part);
 return side?window.rigText(side==='left'?'Bal':'Jobb')+' '+label:label;
}
function syncLocks(){
 const active=current().pointLocks||{};
 for(const input of document.querySelectorAll('[data-lock-point]'))input.checked=Object.hasOwn(active,input.dataset.lockPoint);
}
function discreteEdit(build,validate,insert=false){
 stopPreview();
 closeGesture();
 const before=clone(current()),next=build(clone(before));
 const problem=validate(before,next);
 if(problem){msg('A tükrözés vagy másolás ütközést okozna, illetve megsértene egy rögzítést. A póz változatlan maradt.');return;}
 if(insert&&pose.frames.length>=MozduljRig.MAX_PHASES){msg('Legfeljebb 16 fázis adható hozzá.');return;}
 tracked(()=>{
   if(insert){next.name='Új fázis '+(pose.frames.length+1);pose.frames.splice(frameIndex+1,0,next);frameIndex++;}
   else pose.frames[frameIndex]=next;
 });
 sync();msg('A pózmódosítás elkészült.');
}
function updatePhaseControls(){
 if(!pose)return;
 const n=pose.frames.length;
 $('phaseLabel').textContent=pose.frames[frameIndex].name||('Fázis '+(frameIndex+1));
 $('phaseProgress').textContent=(frameIndex+1)+' / '+n;
 $('phasePrev').disabled=frameIndex===0;
 $('phaseNext').disabled=frameIndex>=n-1;
 $('phaseName').value=pose.frames[frameIndex].name||('Fázis '+(frameIndex+1));
 $('addPhase').disabled=n>=MozduljRig.MAX_PHASES;$('mirrorCopy').disabled=n>=MozduljRig.MAX_PHASES;
 $('removePhase').disabled=n<=1;
 $('phaseUp').disabled=frameIndex===0;$('phaseDown').disabled=frameIndex===n-1;
 $('copyPhase').disabled=n>=MozduljRig.MAX_PHASES;
 $('phaseDirection').value=current().direction||'';
 const list=$('phaseList');list.replaceChildren();
 pose.frames.forEach((frame,i)=>{
   const opt=document.createElement('option');opt.value=String(i);
   opt.textContent=(i+1)+'. '+window.rigText(frame.name||'Fázis')+(frame.direction?' · '+window.rigText(directionNames[frame.direction]):'');
   list.appendChild(opt);
 });
 list.value=String(frameIndex);
 $('phasePrev').setAttribute('aria-disabled',String(frameIndex===0));
 $('phaseNext').setAttribute('aria-disabled',String(frameIndex>=n-1));
}
function show(){if(!studio)return;let f=animation?.display||current();const j=studio.draw(f,{edit:!animation&&!document.body.classList.contains('view-only'),selected:mode==='move'?selectedTarget:null});$('activeTarget').textContent=selectedTarget?window.rigText(targetLabels[selectedTarget]):window.rigText('Nincs kiválasztott testpont');const collisions=MozduljRig.Collision.report(f);$('canvasStatus').textContent=j.bandBlocked?'A gumiszalag útvonala akadályba ütközik; módosítsd a rögzítést vagy a vezetést.':j.clamped.length?window.rigText('A célpont elérési korlátra került: ')+j.clamped.join(', '):collisions.count?window.rigText('Régi pózadatban ')+collisions.count+window.rigText(' geometriai átfedés; új mozgás nem mélyítheti.'):'Rögzített végtaghossz · test-, felület- és eszközütközés-védelem';$('frameNumber').textContent=(frameIndex+1)+' / '+pose.frames.length+window.rigText(' fázis');}

function syncJointValues(){let f=current();$('yaw').value=Math.round(deg(f.headYaw||0));$('pitch').value=Math.round(deg(f.headPitch||0));$('torso').value=Math.round(deg(f.euler[0]));$('roll').value=Math.round(deg(f.headRoll||0));$('shoulderLift').value=Math.round((f.shoulderLift||0)*100);$('pelvicTilt').value=Math.round(deg(f.pelvicTilt||0));$('rootHeight').value=Number((f.root[1]*100).toFixed(1));$('upperYaw').value=Math.round(deg(f.upperYaw||0));$('spineBend').value=Math.round(deg(f.spineBend||0));
 const side=$('legSide').value||'right';const leg=f.legAngles?.[side]||MozduljRig.inferAngles(f,'leg',side)?.angles||{};
 $('hipFlex').value=Math.round(deg(leg.flexion||0));
 $('hipAbduct').value=Math.round(deg(leg.abduction||0));
 $('kneeFlex').value=Math.round(deg(leg.kneeFlexion||0));$('hipRotation').value=Math.round(deg(leg.axialRotation||0));
 $('footPitch').value=Math.round(deg(f.footPitch?.[side]||0));
 const armSide=$('armSide').value||'right';const arm=f.armAngles?.[armSide]||MozduljRig.inferAngles(f,'arm',armSide)?.angles||{};
 for(const [id,k] of [['armFlex','flexion'],['armAbduct','abduction'],['elbowFlex','elbowFlexion'],['wristFlex','wristFlexion'],['forearmRotation','forearmRotation'],['armRotation','axialRotation']])$(id).value=Math.round(deg(arm[k]||0));
 syncPrecision();
}
function sync(){syncJointValues();$('preset').value=pose.preset;updatePhaseControls();syncBands();syncProps();syncLocks();$('zoom').value=studio.getCamera().distance;show();}
function syncBands(selected){
 const sel=$('bandSelect'),prior=selected===undefined?sel.value:selected;
 const items=current().equipment||[];
 sel.innerHTML='<option value="new">Új szalag</option>';
 items.forEach((obj,i)=>{if(['band','miniband','strap'].includes(obj.type)){
   const opt=document.createElement('option');opt.value=String(i);opt.textContent=window.rigText('Szalag ')+(i+1);sel.appendChild(opt);
 }});
 sel.value=[...sel.options].some(o=>o.value===prior)?prior:'new';
 fillSelectedBand();
}
const BASE_POS=MozduljRig.PROP_POSITIONS,POSITIONABLE=Object.keys(BASE_POS);
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
 $('propYaw').value=prop?Math.round(deg(prop.yaw||0)):0;
 $('propLocked').checked=!!prop?.locked;$('propLocked').disabled=!prop;
 $('moveProp').disabled=!prop||!!prop.locked;$('deleteProp').disabled=!prop||!!prop.locked;
 $('propScope').disabled=!prop;
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
function switchFrame(i){stopPreview();closeGesture();frameIndex=Math.max(0,Math.min(i,pose.frames.length-1));sync();}
function setPreset(name){pendingPreset=null;$('presetConfirm').hidden=true;stopPreview();const apply=()=>{const camera=pose?studio.getCamera():null;pose=MozduljRig.initialPose(name);frameIndex=0;studio.setCamera(camera||pose.camera);sync();};if(pose){closeGesture();tracked(apply);}else apply();msg(window.rigText('Pózsablon betöltve: ')+$('preset').selectedOptions[0].textContent);}
function load(p,track=false){pendingPreset=null;$('presetConfirm').hidden=true;stopPreview();MozduljRig.checkPose(p);const apply=()=>{const camera=track?studio.getCamera():null;pose=clone(p);frameIndex=0;studio.setCamera(camera||pose.camera||MozduljRig.initialPose().camera);sync();};closeGesture();if(track)tracked(apply);else{apply();window.resetRigHistory();}msg('A 3D testhelyzet betöltve.');}
function b64toStr(s){return decodeURIComponent(escape(atob(s)));}function strtoB64(s){return btoa(unescape(encodeURIComponent(s)));}
window.loadRigBase64=s=>{try{load(JSON.parse(b64toStr(s)));return true;}catch(e){msg(window.rigText('Betöltési hiba: ')+e.message);return false;}};
window.exportRigBase64=()=>strtoB64(JSON.stringify({...pose,camera:studio.getCamera()}));
window.exportRigJson=()=>JSON.stringify({...pose,camera:studio.getCamera()},null,2);
// Camera-only navigation is not an unsaved pose edit.
window.markRigDraftBaseline=()=>{draftBaseline=JSON.stringify(snapshot().pose);return true;};
window.captureRigDraftBase64=()=>strtoB64(JSON.stringify({
 dirty:JSON.stringify(snapshot().pose)!==draftBaseline,
 payload:window.exportRigBase64(),frameIndex
}));
window.restoreRigDraftBase64=(payload,index)=>{
 try{
   load(JSON.parse(b64toStr(payload)));
   switchFrame(index);return true;
 }catch(e){msg(window.rigText('Betöltési hiba: ')+e.message);return false;}
};
window.loadRigPreset=name=>{
 if(![...$('preset').options].some(o=>o.value===name))return false;
 setPreset(name);window.resetRigHistory();return true;
};
window.getRigHealth=()=>JSON.stringify({webgl:!!studio,frames:pose?.frames?.length,renderer:'pure-webgl-offline'});
window.setReadOnlyMode=()=>{
  document.body.classList.add('view-only');
  document.querySelector('main').style.gridTemplateColumns='1fr';
  document.querySelector('main > section:nth-child(2)').style.display='none';
  // Retain camera and animation controls in the viewer, without editor tabs.
  document.querySelector('.viewport-shell').append(document.querySelector('.view-controls'),document.querySelector('.preview-tools'));
  document.querySelector('.header strong').textContent='3D mozgásábra';
  document.querySelector('.header .hint').textContent='A modellen húzva körbeforgathatod a nézetet';
  document.querySelector('.float-l').style.display='none';
  document.querySelector('.float-b').style.display='none';
  setMode('orbit'); // drag always rotates the camera in the read-only viewer
  updatePhaseControls();show();
};
const directionNames={all:'Irány nélkül',left:'Bal oldal',right:'Jobb oldal',forward:'Előre',backward:'Hátra',up:'Felfelé',down:'Lefelé',inward:'Befelé',outward:'Kifelé',clockwise:'Óramutató járásával megegyezően',counterclockwise:'Óramutató járásával ellentétesen',each_side:'Oldalanként'};
function stopPreview(){
 if(!animation)return;
 cancelAnimationFrame(animation.request);animation=null;
 $('playPreview').textContent='▶ Előnézet';$('quickPreview').textContent='▶';$('previewStatus').textContent='';
 if(studio&&pose)show();
}
function playPreview(){
 if(animation){stopPreview();return;}
 closeGesture();
 if(pose.frames.length<2){msg('Az előnézethez legalább két fázis szükséges.');return;}
 const transitions=[];
 for(let i=0;i<pose.frames.length-1;i++){
   const result=MozduljRig.Collision.previewTransition(pose.frames[i],pose.frames[i+1]);
   if(!result.ok){
     $('previewStatus').textContent=window.rigText('Nem lejátszható átmenet: ')+(i+1)+' → '+(i+2)+' · '+(result.issue==='representation'?window.rigText('Eltérő vezérlés vagy rögzítés'):result.issue);
     return;
   }
   transitions.push(result.samples);
 }
 animation={transitions,start:null,display:transitions[0][0],request:0};
 $('playPreview').textContent='■ Leállítás';$('quickPreview').textContent='■';
 $('previewStatus').textContent=window.rigText('Ellenőrzött animációs előnézet');
 function tick(now){
   if(!animation)return;
   const a=animation;if(a.start===null)a.start=now;
   const interval=1200/Number($('previewSpeed').value||1),duration=interval*transitions.length;
   let elapsed=now-a.start;
   if(elapsed>=duration){
     if(!$('previewLoop').checked){stopPreview();return;}
     // Repeat the sequence from its start; the return is not an interpolated phase.
     a.start=now;elapsed=0;
   }
   const index=Math.min(transitions.length-1,Math.floor(elapsed/interval));
   const sample=Math.min(48,Math.floor((elapsed-index*interval)/interval*48));
   a.display=transitions[index][sample];show();
   $('previewStatus').textContent=window.rigText('Ellenőrzött animációs előnézet')+' · '+(index+1)+' → '+(index+2);
   a.request=requestAnimationFrame(tick);
 }
 animation.request=requestAnimationFrame(tick);
}
// Reports are based on the same geometry as the editor, including band routing.
function reviewPose(){
 stopPreview();closeGesture();
 const findings=[];
 for(const [i,frame] of pose.frames.entries()){
   const j=MozduljRig.joints(frame);
   for(const [id,depth] of MozduljRig.Collision.overlaps(frame))findings.push({phase:i,kind:'collision',id,depth});
   for(const [id,depth] of MozduljRig.lockErrors(j,frame.pointLocks))if(depth>.005) findings.push({phase:i,kind:'lock',id,depth});
   for(const id of j.clamped)findings.push({phase:i,kind:'reach',id});
   for(const index of studio.bandIssues(frame))findings.push({phase:i,kind:'band',id:String(index+1)});
   if(i+1<pose.frames.length){
     const transition=MozduljRig.Collision.previewTransition(frame,pose.frames[i+1]);
     if(!transition.ok)findings.push({phase:i,kind:'transition',id:transition.issue});
   }
 }
 const panel=$('reviewResults');panel.replaceChildren();
 $('reviewSummary').textContent=findings.length?window.rigText('Ellenőrizendő eltérések: ')+findings.length:window.rigText('A geometriai ellenőrzés nem talált eltérést.');
 // Limit the number of rendered rows, but report every finding in the count.
 for(const item of findings.slice(0,100)){
   const button=document.createElement('button');button.type='button';
   const labels={collision:'Geometriai átfedés',lock:'Rögzített testpont eltérése',reach:'Elérési korlát',band:'Akadályos szalagútvonal',transition:'Nem lejátszható átmenet'};
   const id=item.id.startsWith('body:')?item.id.slice(5).split('/').map(bodyPartLabel).join(' – '):targetLabels[item.id]?window.rigText(targetLabels[item.id]):item.id;
   button.textContent=(item.phase+1)+'. '+window.rigText('Fázis')+' · '+window.rigText(labels[item.kind])+' · '+id;
   button.onclick=()=>{
     switchFrame(item.phase);
     const key=targetLabels[item.id]?item.id:item.kind==='collision'&&item.id.startsWith('body:')?item.id.slice(5).split('/').map(id=>{const [part,side]=id.split('-');return side&&(part==='hand'||part==='forearm'?side+'Hand':part==='foot'||part==='shin'?side+'Foot':null);}).find(Boolean):null;
     if(key){if([...$('targetSelect').options].some(o=>o.value===key))$('targetSelect').value=key;else $('targetSelect').value='auto';selectedTarget=key;focusBody(bodyFocusFor(key));setMode('move');}else selectTab('body');
   };
   panel.appendChild(button);
 }
 $('reviewPanel').open=true;selectTab('review');
 return {count:findings.length,findings};
}
window.reviewRigBase64=()=>strtoB64(JSON.stringify(reviewPose()));
const precisionControls=new Map();
function syncPrecision(){
 for(const [id,entry] of precisionControls){
   if(document.activeElement!==entry)entry.value=$(id).value;
 }
}
function setupPrecision(){
 for(const slider of document.querySelectorAll('input[type=range]:not(#zoom)')){
   const label=document.querySelector('label[for="'+slider.id+'"]');
   if(!label)continue;
   const row=document.createElement('div');row.className='precision-row';
   const number=document.createElement('input');number.type='number';number.min=slider.min;number.max=slider.max;number.step=slider.step||'1';number.value=slider.value;
   const unit=slider.id==='shoulderLift'?'cm':'°';
   number.setAttribute('aria-label',label.textContent+' ('+unit+')');
   function apply(value){
     if(!Number.isFinite(value)){number.value=slider.value;return;}
     slider.value=String(Math.max(+slider.min,Math.min(+slider.max,value)));
     slider.dispatchEvent(new Event('input',{bubbles:true}));
     number.value=slider.value;
   }
   number.addEventListener('change',()=>apply(number.valueAsNumber));
   const button=(text,delta)=>{
     const b=document.createElement('button');b.type='button';b.textContent=text;
     b.setAttribute('aria-label',label.textContent+' '+(delta>0?'+1':'−1')+' '+unit);
     b.onclick=()=>{closeGesture();apply(+slider.value+delta);};return b;
   };
   row.append(button('−',-1),number,button('+',1));
   slider.after(row);slider.classList.add('precision-slider');precisionControls.set(slider.id,number);
 }
 syncPrecision();
}
function connect(){try{studio=MozduljRig.shaded3D(canvas);setPreset('stand');}catch(e){initError=e;$('canvasStatus').textContent=e.message;msg(window.rigText('WebGL nem áll rendelkezésre: ')+e.message);return;}
 setupPrecision();
 for(const tab of document.querySelectorAll('.workspace-tabs [role=tab]')){
   tab.onclick=()=>selectTab(tab.id.slice(4));
   tab.onkeydown=e=>{
     const tabs=[...document.querySelectorAll('.workspace-tabs [role=tab]')],i=tabs.indexOf(tab);
     let n=e.key==='ArrowRight'?(i+1)%tabs.length:e.key==='ArrowLeft'?(i+tabs.length-1)%tabs.length:e.key==='Home'?0:e.key==='End'?tabs.length-1:-1;
     if(n>=0){e.preventDefault();tabs[n].click();tabs[n].focus();}
   };
 }
 $('bodyFocus').onchange=e=>focusBody(e.target.value,false);
 $('quickPreview').onclick=playPreview;
 $('runReview').onclick=()=>reviewPose();
 $('undoPose').onclick=undoPose;
 $('redoPose').onclick=redoPose;
 $('targetSelect').onchange=e=>{chooseTarget(e.target.value);};
 // One slider gesture or one phase-name edit is one history entry.
 for(const el of document.querySelectorAll('input[type=range]:not(#zoom),#phaseName')){
   el.addEventListener('pointerdown',e=>{beginGesture(el);controlGesture=e.pointerId;});
   el.addEventListener('focus',()=>{if(el.id==='phaseName')beginGesture(el);});
   el.addEventListener('blur',()=>{if(gesture===el)closeGesture();});
   el.addEventListener('change',()=>{if(gesture===el)closeGesture();});
 }
 const endControl=e=>{if(controlGesture===e.pointerId&&gesture!==canvas)closeGesture();};
 window.addEventListener('pointerup',endControl);
 window.addEventListener('pointercancel',endControl);
 for(const input of document.querySelectorAll('[data-lock-point]'))input.onchange=()=>{
   closeGesture();tracked(()=>MozduljRig.setPointLock(current(),input.dataset.lockPoint,input.checked));sync();
 };
 $('clearPointLocks').onclick=()=>{closeGesture();tracked(()=>{delete current().pointLocks;});sync();};
 $('rootHeight').onchange=e=>{
   const value=e.target.valueAsNumber/100;
   if(!Number.isFinite(value)||value<0||value>3){sync();msg('Érvénytelen medencemagasság.');return;}
   guardedEdit(f=>MozduljRig.moveRoot(f,[0,value-f.root[1],0]));sync();
 };
 $('playPreview').onclick=playPreview;
 $('previewSpeed').onchange=stopPreview;
 $('phaseList').onchange=e=>switchFrame(+e.target.value);
 $('phaseDirection').onchange=e=>{
   stopPreview();closeGesture();tracked(()=>{if(e.target.value)current().direction=e.target.value;else delete current().direction;});sync();
 };
 function reorder(delta){
   stopPreview();closeGesture();tracked(()=>{if(MozduljRig.movePhase(pose.frames,frameIndex,frameIndex+delta))frameIndex+=delta;});sync();
 }
 $('phaseUp').onclick=()=>reorder(-1);$('phaseDown').onclick=()=>reorder(1);
 $('copyPhase').onclick=()=>discreteEdit(f=>f,()=>null,true);
 $('mirrorPose').onclick=()=>discreteEdit(MozduljRig.mirrorFrame,MozduljRig.mirrorIssue);
 $('mirrorCopy').onclick=()=>discreteEdit(MozduljRig.mirrorFrame,MozduljRig.mirrorIssue,true);
 $('copyArm').onclick=()=>discreteEdit(f=>MozduljRig.copyLimb(f,'arm',$('armSide').value),MozduljRig.copyIssue);
 $('copyLeg').onclick=()=>discreteEdit(f=>MozduljRig.copyLimb(f,'leg',$('legSide').value),MozduljRig.copyIssue);
 $('phasePrev').addEventListener('click',()=>switchFrame(frameIndex-1));
 $('phaseNext').addEventListener('click',()=>switchFrame(frameIndex+1));
 $('addPhase').addEventListener('click',()=>{
   stopPreview();
   if(!pose||pose.frames.length>=MozduljRig.MAX_PHASES){msg('Legfeljebb 16 fázis adható hozzá.');return;}
   const next=clone(current());
   next.name='Új fázis '+(pose.frames.length+1);
   closeGesture();tracked(()=>{pose.frames.splice(frameIndex+1,0,next);frameIndex++;});
   sync();msg('Új 3D fázis hozzáadva. Módosítsd a testhelyzetet vagy nevezd át.');
 });
 $('removePhase').addEventListener('click',()=>{
   stopPreview();
   if(!pose||pose.frames.length<=1){msg('Legalább egy fázis szükséges.');return;}
   closeGesture();tracked(()=>{pose.frames.splice(frameIndex,1);frameIndex=Math.min(frameIndex,pose.frames.length-1);});
   sync();msg('Az aktuális fázist töröltem.');
 });
 $('phaseName').addEventListener('input',e=>{
   if(!pose)return;
   stopPreview();beginGesture($('phaseName'));current().name=e.target.value.slice(0,64);
   $('phaseLabel').textContent=current().name||('Fázis '+(frameIndex+1));
 });

 document.addEventListener('keydown',e=>{
   if(document.body.classList.contains('view-only'))return;
   const editing=/^(INPUT|SELECT|TEXTAREA)$/.test(document.activeElement?.tagName||'');
   if(!editing&&(e.ctrlKey||e.metaKey)&&!e.altKey&&['z','y'].includes(e.key.toLowerCase())){
     e.preventDefault();if(e.key.toLowerCase()==='y'||e.shiftKey)redoPose();else undoPose();return;
   }
   if(e.altKey||e.ctrlKey||e.metaKey||editing||document.activeElement?.tagName==='BUTTON')return;
   if(e.key==='ArrowLeft'&&frameIndex>0){e.preventDefault();switchFrame(frameIndex-1);}
   if(e.key==='ArrowRight'&&frameIndex<pose.frames.length-1){e.preventDefault();switchFrame(frameIndex+1);}
 });
 $('preset').addEventListener('change',e=>{
   if(e.target.value===pose.preset)return;
   pendingPreset=e.target.value;e.target.value=pose.preset;
   $('presetConfirm').hidden=false;
 });
 $('confirmPreset').onclick=()=>{
   const name=pendingPreset;pendingPreset=null;$('presetConfirm').hidden=true;
   if(name)setPreset(name);
 };
 $('cancelPreset').onclick=()=>{pendingPreset=null;$('presetConfirm').hidden=true;};
 $('yaw').oninput=e=>guardedEdit(f=>{f.headYaw=rad(+e.target.value);});
 $('pitch').oninput=e=>guardedEdit(f=>{f.headPitch=rad(+e.target.value);});
 $('torso').oninput=e=>guardedEdit(f=>{f.euler[0]=rad(+e.target.value);});
 $('roll').oninput=e=>guardedEdit(f=>{f.headRoll=rad(+e.target.value);});
 $('shoulderLift').oninput=e=>guardedEdit(f=>{f.shoulderLift=+e.target.value/100;});
 $('pelvicTilt').oninput=e=>guardedEdit(f=>{f.pelvicTilt=rad(+e.target.value);});
 $('upperYaw').oninput=e=>guardedEdit(f=>{f.upperYaw=rad(+e.target.value);});
 $('spineBend').oninput=e=>guardedEdit(f=>{f.spineBend=rad(+e.target.value);});
 $('armSide').onchange=()=>focusBody($('armSide').value+'Arm',false);
 function editArm(field,value){const side=$('armSide').value;
   const symmetric=$('symmetricEdit').checked,other=side==='left'?'right':'left';
   guardedEdit(frame=>MozduljRig.editLimbAngle(frame,'arm',side,field,rad(value),symmetric),f=>MozduljRig.prepareAngleMode(f,'arm',side)&&(!symmetric||MozduljRig.prepareAngleMode(f,'arm',other)));}
 for(const [id,k] of [['armFlex','flexion'],['armAbduct','abduction'],['elbowFlex','elbowFlexion'],['wristFlex','wristFlexion'],['forearmRotation','forearmRotation'],['armRotation','axialRotation']])
   $(id).oninput=e=>editArm(k,+e.target.value);
 $('legSide').onchange=()=>focusBody($('legSide').value+'Leg',false);
 function editLeg(field,value){const side=$('legSide').value;
 const symmetric=$('symmetricEdit').checked,other=side==='left'?'right':'left';
 function prepare(f,side){
   if(!MozduljRig.prepareAngleMode(f,'leg',side))return false;
   if(field!=='axialRotation'||f.legAngles[side].axialRotation!=null)return true;
   const j=MozduljRig.joints(f),copy=clone(f),fit=MozduljRig.inferAngles(f,'leg',side,true);
   if(!fit||fit.error>1e-5)return false;
   copy.legAngles[side]=fit.angles;
   if(MozduljRig.limbError(j,MozduljRig.joints(copy),'leg',side)>1e-5)return false;
   f.legAngles[side]=fit.angles;return true;
 }
 guardedEdit(f=>MozduljRig.editLimbAngle(f,'leg',side,field,rad(value),symmetric),f=>prepare(f,side)&&(!symmetric||prepare(f,other)));
 }
 $('hipFlex').oninput=e=>editLeg('flexion',+e.target.value);
 $('hipAbduct').oninput=e=>editLeg('abduction',+e.target.value);
 $('kneeFlex').oninput=e=>editLeg('kneeFlexion',+e.target.value);
 $('hipRotation').oninput=e=>editLeg('axialRotation',+e.target.value);
 $('footPitch').oninput=e=>{const side=$('legSide').value,other=side==='left'?'right':'left';guardedEdit(f=>{f.footPitch??={};const value=rad(+e.target.value),old=f.footPitch[side]||0;let delta=value-old;if($('symmetricEdit').checked){const opposite=f.footPitch[other]||0;delta=Math.max(-.65-opposite,Math.min(.95-opposite,delta));f.footPitch[other]=opposite+delta;}f.footPitch[side]=old+delta;});};
 $('zoom').oninput=e=>{studio.getCamera().distance=+e.target.value;show();};
 $('addEquipment').onclick=()=>{
   const type=$('equip').value;
   if(['band','miniband','strap'].includes(type)){const result=guardedEdit(f=>f.equipment.push({type,anchorA:type==='miniband'?'leftKnee':'leftHand',anchorB:type==='miniband'?'rightKnee':'rightHand',route:'auto'}));
     if(!result.limited){syncBands(String(current().equipment.length-1));msg('Szalag beszúrva; lent beállíthatod mindhárom pontját.');}return;}
   if(!current().equipment.some(o=>o.type===type)){
     const pos=POSITIONABLE.includes(type)?freePlacement(type):null;
     if(POSITIONABLE.includes(type)&&!pos){msg('Az eszköznek nem találtam akadálymentes helyet. Módosítsd a pózt vagy válassz másik eszközt.');return;}
     const result=guardedEdit(f=>f.equipment.push(pos?{type,position:pos}:{type}));
     if(!result.limited){syncProps(String(current().equipment.length-1));msg(window.rigText('Eszköz hozzáadva: ')+$('equip').selectedOptions[0].textContent);}
   }else msg('Ez az eszköz már szerepel ebben a fázisban.');
 };
 $('clearEquipment').onclick=()=>{stopPreview();closeGesture();tracked(()=>{current().equipment=current().equipment.filter(p=>p.locked);});syncBands('new');syncProps('none');show();msg('Az aktuális fázis eszközeit eltávolítottam.');};
 $('propSelect').onchange=fillProp;
 function propLocations(){
   const i=Number($('propSelect').value);
   return $('propScope').value==='all'?MozduljRig.matchingProp(pose.frames,frameIndex,i):[{frame:frameIndex,index:i}];
 }
 $('propLocked').onchange=()=>{
   stopPreview();closeGesture();const i=Number($('propSelect').value);
   if(!Number.isInteger(i))return;
   tracked(()=>{const prop=current().equipment[i];if($('propLocked').checked)prop.locked=true;else delete prop.locked;});syncProps(String(i));show();
 };
 $('deleteProp').onclick=()=>{
   stopPreview();closeGesture();const locations=propLocations();
   if(locations.some(({frame,index})=>pose.frames[frame].equipment[index].locked)){msg('Előbb oldd fel az eszköz rögzítését.');return;}
   tracked(()=>{for(const {frame,index} of locations)pose.frames[frame].equipment.splice(index,1);});
   sync();msg('Kijelölt eszköz eltávolítva.');
 };
 $('moveProp').onclick=()=>{
   if($('propSelect').value==='none')return;
   const pos=['X','Y','Z'].map(k=>$('prop'+k).valueAsNumber),yaw=rad($('propYaw').valueAsNumber);
   if(pos.some((v,n)=>!Number.isFinite(v)||Math.abs(v)>3||(n===1&&v<0))||!Number.isFinite(yaw)||Math.abs(yaw)>Math.PI){msg('A pozíció koordinátái nem megfelelőek.');return;}
   stopPreview();closeGesture();const result=MozduljRig.editProps(pose.frames,propLocations(),pos,yaw);
   if(!result.ok){msg(window.rigText('Az eszköz módosítása nem lehetséges ebben a fázisban: ')+(result.frame+1)+' · '+(result.issue==='propLocked'?window.rigText('Rögzített eszköz'):result.issue));return;}
   tracked(()=>{pose.frames=result.frames;});sync();msg('Eszköz áthelyezve.');
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
   msg(window.rigText('Szalagrögzítés mentve: ')+$('bandAnchorA').selectedOptions[0].textContent+' – '+$('bandAnchorB').selectedOptions[0].textContent);
 };
 $('deleteBand').onclick=()=>{stopPreview();const i=$('bandSelect').value;
   if(i==='new'){msg('Válassz egy meglévő szalagot a törléshez.');return;}
   closeGesture();tracked(()=>current().equipment.splice(+i,1));syncBands('new');show();msg('Szalag eltávolítva.');
 };
 for(const [id,a,e] of [['front',0,.08],['side',Math.PI/2,.12],['threequarter',.66,.33]])$(''+id).onclick=()=>{let c=studio.getCamera();c.azimuth=a;c.elevation=e;show();};
 $('fit').onclick=()=>{let c=studio.getCamera();Object.assign(c,{azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]});sync();};
 for(const m of ['orbit','move','pan'])$(m).onclick=()=>{closeGesture();setMode(m);msg(m==='move'?'Válassz testpontot, majd húzd a kívánt helyre.':m==='pan'?'Húzással eltolhatod az egész képet, a testarányok változtatása nélkül.':'Húzással elforgathatod a kamerát.');};
 $('export').onclick=()=>{let s=window.exportRigJson();$('json').value=s;try{let blob=new Blob([s],{type:'application/json;charset=utf-8'});let url=URL.createObjectURL(blob);let a=document.createElement('a');a.download='mozdulj-3d-poz.json';a.href=url;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}catch{}msg('3D pózadat mentve.');};
 $('import').onclick=()=>{try{load(JSON.parse($('json').value),true);}catch(e){msg(window.rigText('Hibás JSON: ')+e.message);}};
 function loc(e){let r=canvas.getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top};}
 canvas.addEventListener('pointerdown',e=>{
   if(!studio||drag||!e.isPrimary)return;
   if(mode==='move')stopPreview();
   canvas.setPointerCapture(e.pointerId);let p=loc(e);
   let sel=mode==='move'?(explicitTarget($('targetSelect').value)||studio.pick(p.x,p.y)):null;
   if(mode==='move'){selectedTarget=targetKey(sel);if(sel){focusBody(bodyFocusFor(selectedTarget));beginGesture(canvas);}show();}
   drag={id:e.pointerId,x:p.x,y:p.y,sel,button:e.button};
 });
 canvas.addEventListener('pointermove',e=>{if(!drag||!studio||drag.id!==e.pointerId)return;let p=loc(e),dx=p.x-drag.x,dy=p.y-drag.y,c=studio.getCamera();if(mode==='move'){if(drag.sel){const sel=drag.sel,symmetric=$('symmetricEdit').checked&&sel.kind!=='root',kind=sel.kind==='hands'?'arm':'leg',other=sel.side==='left'?'right':'left';guardedEdit(f=>{const before=clone(f);studio.dragTarget(f,sel,dx,dy,e.shiftKey,$('moveAxis').value);if(symmetric)MozduljRig.mirrorTargetDelta(f,before,sel.kind,sel.side);},f=>sel.kind==='root'||(MozduljRig.prepareTargetMode(f,kind,sel.side)&&(!symmetric||MozduljRig.prepareTargetMode(f,kind,other))));}}else if(mode==='pan'||drag.button===2){let right=[Math.cos(c.azimuth),0,-Math.sin(c.azimuth)];let scale=c.distance/300;c.target=c.target.map((v,i)=>v-right[i]*dx*scale +(i===1?dy*scale:0));}else{c.azimuth-=dx*.012;c.elevation=Math.max(-1.25,Math.min(1.25,c.elevation+dy*.008));}drag.x=p.x;drag.y=p.y;show();});
 ['pointerup','pointercancel','lostpointercapture'].forEach(n=>canvas.addEventListener(n,e=>{if(drag?.id!==e.pointerId)return;drag=null;if(gesture===canvas)closeGesture();sync();}));canvas.addEventListener('contextmenu',e=>e.preventDefault());
 canvas.addEventListener('wheel',e=>{e.preventDefault();let c=studio.getCamera();c.distance=Math.max(1.8,Math.min(6.5,c.distance+Math.sign(e.deltaY)*.22));$('zoom').value=c.distance;show();},{passive:false});
 window.addEventListener('resize',show);const observer=new ResizeObserver(()=>show());observer.observe(canvas);
 document.addEventListener('visibilitychange',()=>{if(document.hidden){stopPreview();closeGesture();drag=null;}else show();});
 syncHistory();
}
connect();
})();
