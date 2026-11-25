/* Interactive offline rig editor with a MAUI-compatible base64 bridge. */
(()=>{'use strict';let studio,pose,frameIndex=0,mode='orbit',drag=null,initError=null;
const $=id=>document.getElementById(id),canvas=$('scene');
const clone=o=>JSON.parse(JSON.stringify(o));
const rad=x=>x*Math.PI/180,deg=x=>x*180/Math.PI;
const history=new MozduljPoseHistory();
let selectedTarget=null,selectedEquipment=null,gesture=null,controlGesture=null;
let workoutGuide=null, workoutGuideRequest=0;
 let animation=null, timelineSignature='', draftBaseline=null, pendingPreset=null,activeIssue=null;
let resetFrames=[],directionHints=[],directionSignature='';
const pointers=new Map();let pinch=null, multiGesture=false;
const snapshot=()=>{const data=clone(pose);delete data.camera;return {pose:data,frameIndex,resetFrames:clone(resetFrames)};};
function syncHistory(){ $('undoPose').disabled=!history.canUndo; $('redoPose').disabled=!history.canRedo; }
function closeGesture(){if(pose&&history.pending!==null)history.finish(snapshot());gesture=null;controlGesture=null;syncHistory();if(pose&&$('tab-phases').getAttribute('aria-selected')==='true')syncTimeline();}
function beginGesture(owner){if(gesture!==owner){closeGesture();gesture=owner;history.begin(snapshot());}}
function tracked(change){
 stopPreview();activeIssue=null;
 const before=snapshot();
 change();
 history.record(before,snapshot());
 syncHistory();
}
function restoreHistory(state){
 if(!state)return;selectedEquipment=null;activeIssue=null;
 const camera=studio.getCamera();
 pose=clone(state.pose);pose.camera=camera;
 resetFrames=clone(state.resetFrames||pose.frames);
 frameIndex=Math.min(state.frameIndex,pose.frames.length-1);
 sync();syncHistory();
}
function undoPose(){stopPreview();closeGesture();restoreHistory(history.undo(snapshot()));}
function redoPose(){stopPreview();closeGesture();restoreHistory(history.redo(snapshot()));}
window.resetRigHistory=()=>{history.clear();gesture=null;controlGesture=null;syncHistory();return true;};
const targetLabels={root:'Medence',leftHand:'Bal kéz',rightHand:'Jobb kéz',leftFoot:'Bal lábfej',rightFoot:'Jobb lábfej',leftKnee:'Bal térd',rightKnee:'Jobb térd',leftElbow:'Bal könyök',rightElbow:'Jobb könyök',head:'Fej',neck:'Nyak',chest:'Mellkas',waist:'Derék',leftShoulder:'Bal váll',rightShoulder:'Jobb váll',leftScapula:'Bal lapocka',rightScapula:'Jobb lapocka',leftHip:'Bal csípő',rightHip:'Jobb csípő',leftToe:'Bal lábujjrész',rightToe:'Jobb lábujjrész'};
function targetKey(sel){return sel?.key||null;}
function bodyFocusFor(key){
 if(key==='root'||key?.endsWith('Hip'))return 'root';
 if(!key)return 'all';
 if(['head','neck'].includes(key))return 'head';
 if(['chest','waist'].includes(key))return 'trunk';
 return (key.startsWith('left')?'left':'right')+(/Hand|Elbow|Shoulder|Scapula/.test(key)?'Arm':'Leg');
}
function selectTab(name){
 const alreadyActive=$('tab-'+name).getAttribute('aria-selected')==='true';
 for(const tab of document.querySelectorAll('.workspace-tabs [role=tab]')){
   const active=tab.id==='tab-'+name;
   tab.setAttribute('aria-selected',String(active));tab.tabIndex=active?0:-1;
   $(tab.getAttribute('aria-controls')).hidden=!active;
 }
 if(!alreadyActive)$('workspaceScroll').scrollTop=0;
 if(name==='phases'&&pose)syncTimeline();
}
function focusBody(value,open=true){
 $('bodyFocus').value=value;
 const group=value==='all'?'all':value.endsWith('Arm')?'arm':value.endsWith('Leg')?'leg':'central';
 for(const detail of document.querySelectorAll('[data-body-group]')){
   detail.hidden=group!=='all'&&detail.dataset.bodyGroup!==group;
   if(group!=='all'&&!detail.hidden)detail.open=true;
 }
 const centralFields={root:['rootHeight','pelvicTilt','pelvisPitch','pelvisYaw','pelvisRoll'],head:['yaw','pitch','roll','cervicalPitch','cervicalYaw','cervicalRoll'],trunk:['shoulderLift','torso','upperYaw','spineBend','lumbarBend','lumbarSideBend','thoracicBend','thoracicSideBend','lumbarYaw','thoracicYaw']};
 for(const field of document.querySelectorAll('[data-central-field]'))
   field.hidden=group==='central'&&!centralFields[value]?.includes(field.dataset.centralField);
 if(group==='all')for(const field of document.querySelectorAll('[data-central-field]'))field.hidden=false;
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
function explicitTarget(key){return MozduljRig.bodyTarget(key);}
function setMode(m){
 mode=m;
 for(const b of ['orbit','move','pan','editEquipment'])$(b).classList.toggle('active',m===b);
 if(m==='move'&&!document.body.classList.contains('view-only'))selectTab('body');
 if(m==='editEquipment'&&!document.body.classList.contains('view-only'))selectTab('props');
 show();
}
function current(){return pose.frames[frameIndex];}
function msg(s){$('status').textContent=window.rigText(s);}
// All pose-affecting controls share the same swept 3D collision guard.
// Camera operations are intentionally excluded: orbit/pan never move the body.
function guardedEdit(change,prepare=null,adapt=false){
 stopPreview();
 const original=snapshot();
 const before=clone(current());
 if(prepare&&!prepare(before)){
   activeIssue=null;
   sync();msg('A jelenlegi póz ebben a vezérlési módban nem adható meg pontosan. A póz változatlan maradt.');
   return {limited:true,issue:'mode',fraction:0};
 }
 if(adapt)MozduljRig.prepareSupportEdit(before);
 const candidate=clone(before);change(candidate);
 const result=MozduljRig.Collision.transition(before,candidate,28,adapt);
 activeIssue=result.limited?result.issue:null;
 if(result.fraction>0)pose.frames[frameIndex]=result.frame;
 history.record(original,snapshot());syncHistory();
 if(result.limited){
   sync();
   const issue=result.issue||'';
   if(issue.startsWith('lock:'))msg(window.rigText('Rögzített testpont: ')+window.rigText(targetLabels[issue.slice(5)]||'Eszközkapcsolat')+' — '+window.rigText('Oldd fel a rögzítést a további mozgatáshoz.'));
   else if(issue.startsWith('link:'))msg(window.rigText('A testpontkapcsolat nem tartható. Mozgasd a vezetőt, vagy oldd fel a kapcsolatot.'));
   else if(issue.startsWith('bend:'))msg(window.rigText('Hirtelen hajlításirány-váltás: ')+window.rigText(targetLabels[issue.slice(5)]||issue.slice(5))+' — '+window.rigText('Módosítsd fokozatosan a hajlítási síkot.'));
   else if(issue.startsWith('body:'))msg(window.rigText('Testrészek ütközése: ')+issue.slice(5).split('/').map(bodyPartLabel).join(' – '));
   else msg(window.rigText('Ütközési korlát: a végtag nem haladhat át a padlón, falon vagy eszközön. (')+issue+')');
 }
 else {syncJointValues();show();if(result.rootAdjusted)msg('A medence a rögzített támaszpontokhoz igazodott.');}
 return result;
}

function bodyPartLabel(id){
 const [part,side]=id.split('-');
 const parts={torso:'Törzs',head:'Fej',upperArm:'Felkar',forearm:'Alkar',hand:'Kéz',thigh:'Comb',shin:'Lábszár',foot:'Lábfej'};
 const label=window.rigText(parts[part]||part);
 return side?window.rigText(side==='left'?'Bal':'Jobb')+' '+label:label;
}
function lockOption(select,value,label){
 const option=document.createElement('option');option.value=value;option.textContent=label;select.appendChild(option);
}
function syncLocks(){
 const f=current();
 for(const input of document.querySelectorAll('[data-lock-point]')){
   const key=input.dataset.lockPoint,binding=f.equipmentPointLocks?.[key];
   input.disabled=!!f.bodyPointLinks?.[key];
   input.replaceChildren();
   lockOption(input,'free','Szabad');
   lockOption(input,'world','Térben rögzített ●');
   for(const [i,prop] of (f.equipment||[]).entries()){
     if(POSITIONABLE.includes(prop.type))lockOption(input,'equipment:'+i,'✚ '+equipmentLabel(prop,i));
   }
   const index=binding?(f.equipment||[]).findIndex(p=>p.id===binding.equipmentId&&p.type===binding.equipmentType):-1;
   input.value=binding&&index>=0?'equipment:'+index:f.pointLocks?.[key]?'world':'free';
 }
 $('bodyLinkStatus').textContent=Object.entries(f.bodyPointLinks||{}).map(([key,b])=>window.rigText(targetLabels[b.leader])+' → '+window.rigText(targetLabels[key])).join(' · ')||window.rigText('Nincs testpontkapcsolat.');
 syncContactSurfaces();
}
// Only an explicit binding action gives a legacy object an identity, on a
// candidate copy. Failed actions and opening old poses do not mutate data.
function identifySupport(frame,index){
 const prop=frame.equipment?.[index];
 if(!prop||!POSITIONABLE.includes(prop.type))return false;
 prop.id??=MozduljRig.newEquipmentId(pose.frames);
 return true;
}
function changePointLock(input){
 closeGesture();stopPreview();
 const candidate=clone(current()),key=input.dataset.lockPoint,value=input.value;
 if(value==='free'||value==='world')MozduljRig.setPointLock(candidate,key,value==='world');
 else {
   const index=Number(value.slice('equipment:'.length));
   if(!value.startsWith('equipment:')||!Number.isInteger(index)||!identifySupport(candidate,index)||!MozduljRig.setEquipmentPointLock(candidate,key,index)){
     syncLocks();msg('A testpont nem rögzíthető a kiválasztott eszközhöz.');return;
   }
 }
 tracked(()=>{pose.frames[frameIndex]=candidate;});sync();
}
function syncContactSurfaces(){
 const type=$('contactAction').value;
 const surface=$('contactSurface'),prior=surface.value;
 surface.replaceChildren();
 const needs=type==='wall'?'wall':type==='seat'?'chair':null;
 $('contactSurfaceRow').hidden=!needs;
 const relative=$('contactLockMode').querySelector('option[value="equipment"]');
 relative.disabled=!needs;
 if(!needs&&$('contactLockMode').value==='equipment')$('contactLockMode').value='world';
 $('contactSideRow').hidden=type==='seat';
 $('contactAlign').disabled=type==='seat';
 if(needs){
   for(const [i,prop] of (current().equipment||[]).entries()){
     if(prop.type!==needs)continue;
     const option=document.createElement('option');option.value=String(i);
     option.textContent=window.rigText(needs==='wall'?'Fal':'Szék')+' '+(i+1);surface.appendChild(option);
   }
   if(!surface.options.length){
     const option=document.createElement('option');option.value='';option.textContent=window.rigText('Nincs megfelelő eszköz a fázisban');surface.appendChild(option);
   }
   if([...surface.options].some(o=>o.value===prior))surface.value=prior;
 }
 $('fitContact').disabled=!!needs&&surface.value==='';
}
function inspectContacts(){
 const contacts=MozduljRig.Collision.naturalContacts(MozduljRig.Collision.bodySegments(MozduljRig.joints(current())));
 $('naturalContactStatus').textContent=contacts.length?contacts.map(c=>c.name.slice(5).split('/').map(bodyPartLabel).join(' – ')).join(' · '):window.rigText('Nincs közeli, áthatolásmentes testkontaktus.');
}
function fitContact(){
 stopPreview();closeGesture();
 const action=$('contactAction').value,type=action==='feet'||action==='hands'?'floor':action;
 const sides=$('contactSide').value==='both'?['left','right']:[$('contactSide').value];
 const keys=type==='seat'?['root']:sides.map(side=>side+(action==='feet'?'Foot':'Hand'));
 const index=$('contactSurface').value===''?null:Number($('contactSurface').value);
 const lockMode=$('contactLockMode').value,relative=lockMode==='equipment';
 const candidate=clone(current());
 if(relative&&!identifySupport(candidate,index)){msg('A testpont nem rögzíthető a kiválasztott eszközhöz.');return;}
 const result=MozduljRig.fitSupport(candidate,type,keys,index,lockMode!=='free',$('contactAlign').checked&&type!=='seat',relative);
 const status=$('contactStatus');
 if(!result.ok){
   const explanations={alignment:'A tenyér vagy talp nem igazítható a felülethez a jelenlegi ízületi korlátok mellett.',target:'Nem megfelelő testpont.',locked:'A testpont már rögzítve van. Előbb oldd fel a rögzítést.',mode:'A jelenlegi végtagállás nem alakítható át ugrásmentesen.',orientation:'Ehhez közel függőleges törzsű kiinduló póz szükséges.',surface:'Nincs megfelelő támaszfelület.',reach:'A támaszfelület nem érhető el ebben a testhelyzetben.',outside:'A testpont a támaszfelület szélén kívülre kerülne.'};
   const id=result.issue;
   activeIssue=id.startsWith('body:')||id.startsWith('lock:')||id.includes('@')?id:null;
   status.textContent=window.rigText('Az illesztés nem sikerült. A póz változatlan maradt.')+' '+window.rigText(explanations[id]||'Ütközés vagy rögzítés akadályozza a mozdulatot.')+(result.key?' '+window.rigText(targetLabels[result.key]):'');
   show();return;
 }
 tracked(()=>{pose.frames[frameIndex]=result.frame;});sync();
 status.textContent=window.rigText(relative?'Illesztés kész, a testpontok az eszközhöz rögzítve.':lockMode!=='free'?'Illesztés kész, a testpontok rögzítve.':'Illesztés kész.');
}
function discreteEdit(build,validate,insert=false){
 activeIssue=null;
 stopPreview();
 closeGesture();
 const before=clone(current()),next=build(clone(before));
 const problem=validate(before,next);
 if(problem){msg('A tükrözés vagy másolás ütközést okozna, illetve megsértene egy rögzítést. A póz változatlan maradt.');return;}
 if(insert&&pose.frames.length>=MozduljRig.MAX_PHASES){msg('Legfeljebb 16 fázis adható hozzá.');return;}
 tracked(()=>{
   if(insert){next.name='Új fázis '+(pose.frames.length+1);pose.frames.splice(frameIndex+1,0,next);resetFrames.splice(frameIndex+1,0,clone(next));frameIndex++;}
   else pose.frames[frameIndex]=next;
 });
 sync();msg('A pózmódosítás elkészült.');
}
const phaseTypeLabels={start:'Kiindulás',movement:'Mozdulat',hold:'Kitartás'};
function syncTimeline(){
 const timeline=$('phaseTimeline');
 const signature=JSON.stringify(pose.frames);
 if(signature!==timelineSignature){
   timelineSignature=signature;
   const thumbnails=MozduljRig.phaseThumbnailData(pose.frames);
   timeline.replaceChildren();
   function svgNode(tag,attrs){const el=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const [k,v] of Object.entries(attrs))el.setAttribute(k,String(v));return el;}
   pose.frames.forEach((frame,i)=>{
     const button=document.createElement('button');button.type='button';button.className='phase-tile';button.dataset.index=String(i);
     const data=thumbnails[i],svg=svgNode('svg',{viewBox:'0 0 100 86','aria-hidden':'true'});
     for(const [a,b] of data.lines)svg.appendChild(svgNode('line',{x1:a[0],y1:a[1],x2:b[0],y2:b[1],stroke:'currentColor','stroke-width':3,'stroke-linecap':'round'}));
     svg.appendChild(svgNode('circle',{cx:data.head[0],cy:data.head[1],r:data.headRadius,fill:'currentColor'}));
     const name=document.createElement('span');name.className='phase-tile-name';name.textContent=(i+1)+'. '+window.rigText(frame.name||'Fázis');
     const meta=document.createElement('span');meta.className='phase-tile-meta';
     const t=MozduljRig.phaseTiming(frame);
     meta.textContent=window.rigText(phaseTypeLabels[frame.phaseType]||'Automatikus')+' · '+t.hold+' s';
     button.append(svg,name,meta);button.onclick=()=>switchFrame(i);timeline.appendChild(button);
   });
 }
 for(const tile of timeline.children){const active=Number(tile.dataset.index)===frameIndex;tile.setAttribute('aria-current',active?'step':'false');}
 // Only scroll the strip, not the whole inspector or persistent viewport.
 const active=timeline.children[frameIndex];
 if(active&&timeline.clientWidth){if(active.offsetLeft<timeline.scrollLeft)timeline.scrollLeft=active.offsetLeft;else if(active.offsetLeft+active.offsetWidth>timeline.scrollLeft+timeline.clientWidth)timeline.scrollLeft=active.offsetLeft+active.offsetWidth-timeline.clientWidth;}
}
function syncPhaseComparison(){
 const n=pose.frames.length,select=$('phaseCopySource'),prior=select.value;
 select.replaceChildren();
 for(const [i,f] of pose.frames.entries()){
   if(i===frameIndex)continue;
   const opt=document.createElement('option');opt.value=String(i);
   opt.textContent=(i+1)+'. '+window.rigText(f.name||'Fázis');select.appendChild(opt);
 }
 if([...select.options].some(o=>o.value===prior))select.value=prior;
 else if(select.options.length)select.value=String(frameIndex>0?frameIndex-1:frameIndex+1);
 select.disabled=n<2;$('copyPhaseBodyPart').disabled=n<2;
 $('phaseOverlay').disabled=n<2;
}
function applyPhaseBodyCopy(){
 const i=Number($('phaseCopySource').value),group=$('phaseCopyGroup').value;
 if(!Number.isInteger(i)||i===frameIndex||!pose.frames[i])return;
 closeGesture();stopPreview();
 const result=MozduljRig.copyPhaseBodyPart(current(),pose.frames[i],group);
 if(!result.ok){
   activeIssue=result.issue.startsWith('reach:')?null:result.issue;
   show();msg('A testrész másolása ütközés, rögzítés vagy elérési korlát miatt nem lehetséges. A póz változatlan maradt.');return;
 }
 tracked(()=>{pose.frames[frameIndex]=result.frame;});sync();msg('A kiválasztott testrész átmásolva az aktuális fázisba.');
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
 $('phaseType').value=current().phaseType||'';
 $('transitionSeconds').value=current().transitionSeconds??'';
 $('holdSeconds').value=current().holdSeconds??'';
 syncTimeline();
 const list=$('phaseList');list.replaceChildren();
 pose.frames.forEach((frame,i)=>{
   const opt=document.createElement('option');opt.value=String(i);
   opt.textContent=(i+1)+'. '+window.rigText(frame.name||'Fázis')+(frame.direction?' · '+window.rigText(directionNames[frame.direction]):'');
   list.appendChild(opt);
 });
 list.value=String(frameIndex);
 syncPhaseComparison();
 $('phasePrev').setAttribute('aria-disabled',String(frameIndex===0));
 $('phaseNext').setAttribute('aria-disabled',String(frameIndex>=n-1));
}
function drawEquipmentLabels(frame,j){
 const overlay=$('equipmentLabels');
 overlay.replaceChildren();
 if(mode!=='editEquipment'||animation||document.body.classList.contains('view-only'))return;
 for(const h of MozduljRig.equipmentHandles(frame,j)){
   const point=studio.project(h.position);if(!point)continue;
   const label=document.createElement('span');
   label.className='equipment-handle-label'+(h.key===selectedEquipment?.key?' selected':'')+(!h.movable?' locked':'');
   label.textContent=h.which?(h.index+1)+'·'+h.which:String(h.index+1);
   label.style.left=point[0]+'px';label.style.top=point[1]+'px';overlay.appendChild(label);
 }
}
function show(){if(!studio)return;$('naturalContactStatus').textContent='';let f=workoutGuide?.display||animation?.display||current();const j=studio.draw(f,{edit:!animation&&!document.body.classList.contains('view-only'),selected:mode==='move'?selectedTarget:null,showAll:mode==='move'&&$('showAllTargets').checked,issue:activeIssue,dark:document.documentElement.classList.contains('dark'),equipmentMode:mode==='editEquipment',equipmentSelection:selectedEquipment,displayMode:$('modelDisplay').value,highContrast:document.documentElement.classList.contains('high-contrast'),axisSpace:$('axisSpace').value,moveAxis:$('moveAxis').value,directionHints:currentDirectionHints(),referenceFrames:!animation&&!document.body.classList.contains('view-only')?MozduljRig.phaseReferences(pose.frames,frameIndex,$('phaseOverlay').value):[]});syncViewCube();syncPointCoordinates(false,animation?null:j);drawEquipmentLabels(f,j);$('activeTarget').textContent=selectedTarget?window.rigText(targetLabels[selectedTarget]):window.rigText('Nincs kiválasztott testpont');$('targetHelp').textContent=selectedTarget&&MozduljRig.bodyTarget(selectedTarget)?.kind==='articulate'?window.rigText('A húzás a kapcsolódó ízületi szögeket módosítja; a pont nem mozgatható tetszőlegesen.'):selectedTarget&&['elbow','knee'].includes(MozduljRig.bodyTarget(selectedTarget)?.kind)?window.rigText('A húzás a hajlítási síkot módosítja, a végtag hossza nem változik.'):'';const collisions=MozduljRig.Collision.report(f);$('canvasStatus').textContent=j.bandBlocked?'A gumiszalag útvonala akadályba ütközik; módosítsd a rögzítést vagy a vezetést.':j.clamped.length?window.rigText('A célpont elérési korlátra került: ')+j.clamped.join(', '):collisions.count?window.rigText('Régi pózadatban ')+collisions.count+window.rigText(' geometriai átfedés; új mozgás nem mélyítheti.'):'Rögzített végtaghossz · test-, felület- és eszközütközés-védelem';$('frameNumber').textContent=(frameIndex+1)+' / '+pose.frames.length+window.rigText(' fázis');}

function syncJointValues(){let f=current();$('yaw').value=Math.round(deg(f.headYaw||0));$('pitch').value=Math.round(deg(f.headPitch||0));$('torso').value=Math.round(deg(f.euler[0]));$('roll').value=Math.round(deg(f.headRoll||0));$('shoulderLift').value=Math.round((f.shoulderLift||0)*100);$('pelvicTilt').value=Math.round(deg(f.pelvicTilt||0));$('rootHeight').value=Number((f.root[1]*100).toFixed(1));$('upperYaw').value=Math.round(deg(f.upperYaw||0));$('spineBend').value=Math.round(deg(f.spineBend||0));
 for(const id of Object.keys(MozduljRig.ARTICULATION_LIMITS))if($(id))$(id).value=Math.round(deg(f[id]||0));
 const side=$('legSide').value||'right';const leg=f.legAngles?.[side]||MozduljRig.inferAngles(f,'leg',side)?.angles||{};
 $('hipFlex').value=Math.round(deg(leg.flexion||0));
 $('hipAbduct').value=Math.round(deg(leg.abduction||0));
 $('kneeFlex').value=Math.round(deg(leg.kneeFlexion||0));$('hipRotation').value=Math.round(deg(leg.axialRotation||0));
 $('footPitch').value=Math.round(deg(f.footPitch?.[side]||0));
 const armSide=$('armSide').value||'right';const arm=f.armAngles?.[armSide]||MozduljRig.inferAngles(f,'arm',armSide)?.angles||{};
 const curl=MozduljRig.handCurl(f,armSide);
 $('handCurl').disabled=curl===null;$('handCurl').value=Math.round((curl??0)*100);
 $('handShape').value=curl===null?'legacy':Object.keys(MozduljRig.HAND_PRESETS).find(k=>Math.abs(MozduljRig.HAND_PRESETS[k]-curl)<.00001)||'custom';
 for(const [kind,selected] of [['arm',armSide],['leg',side]])
   for(const suffix of MozduljRig.SIDE_ARTICULATION[kind])$('side'+suffix).value=Math.round(deg(f[selected+suffix]||0));
 for(const [id,k] of [['armFlex','flexion'],['armAbduct','abduction'],['elbowFlex','elbowFlexion'],['wristFlex','wristFlexion'],['forearmRotation','forearmRotation'],['armRotation','axialRotation']])$(id).value=Math.round(deg(arm[k]||0));
 syncPrecision();
}
function sync(){syncJointValues();$('preset').value=pose.preset;updatePhaseControls();syncBands();syncProps();syncLocks();syncEquipmentSelection();$('zoom').value=studio.getCamera().distance;show();}
function syncBands(selected){
 const sel=$('bandSelect'),prior=selected===undefined?sel.value:selected;
 const items=current().equipment||[];
 sel.innerHTML='<option value="new">Új szalag</option>';
 items.forEach((obj,i)=>{if(['band','miniband','strap'].includes(obj.type)){
   const opt=document.createElement('option');opt.value=String(i);opt.textContent=equipmentLabel(obj,i);sel.appendChild(opt);
 }});
 sel.value=[...sel.options].some(o=>o.value===prior)?prior:'new';
 fillSelectedBand();
}
const BASE_POS=MozduljRig.PROP_POSITIONS,POSITIONABLE=Object.keys(BASE_POS);
function equipmentLabel(prop,i){const name=[...$('equip').options].find(o=>o.value===prop.type)?.textContent||prop.type;return name+' · '+(i+1);}
function syncProps(selected){
 const sel=$('propSelect'),prior=selected===undefined?sel.value:selected;
 sel.innerHTML='<option value="none">Nincs mozgatható eszköz</option>';
 (current().equipment||[]).forEach((prop,i)=>{if(POSITIONABLE.includes(prop.type)){
 const opt=document.createElement('option');opt.value=String(i);opt.textContent=equipmentLabel(prop,i)+(prop.locked?' 🔒':'');sel.appendChild(opt);
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
 $('equipmentIdentity').textContent=prop?.id?window.rigText('Eszközazonosító: ')+prop.id:prop?window.rigText('Régi eszköz – típus és sorrend szerinti párosítás') :'';
}
function syncEquipmentSelection(){
 const handles=MozduljRig.equipmentHandles(current());
 const select=$('equipmentPoint');select.replaceChildren();
 const empty=document.createElement('option');empty.value='';empty.textContent='Automatikus kiválasztás';select.appendChild(empty);
 for(const h of handles){const option=document.createElement('option');option.value=h.key;option.textContent=equipmentLabel(current().equipment[h.index],h.index)+(h.which?' · '+h.which:'')+(h.movable?'':' 🔒');select.appendChild(option);}
 selectedEquipment=handles.find(h=>h.key===selectedEquipment?.key)||null;
 select.value=selectedEquipment?.key||'';
 $('detachBandPoint').disabled=!(selectedEquipment?.kind==='band'&&!selectedEquipment.movable);
 $('equipmentPointStatus').textContent=selectedEquipment?(selectedEquipment.movable?window.rigText('Húzd a kijelölt eszközpontot.'):window.rigText(selectedEquipment.kind==='band'?'A szalagpont a testhez kötött. A szabad mozgatáshoz válaszd le.':'A kijelölt eszköz rögzített.')):'';
}
function chooseEquipment(key){
 selectedEquipment=MozduljRig.equipmentHandles(current()).find(h=>h.key===key)||null;
 if(selectedEquipment?.kind==='prop')syncProps(String(selectedEquipment.index));
 if(selectedEquipment?.kind==='band')syncBands(String(selectedEquipment.index));
 syncEquipmentSelection();show();
}
function freePlacement(type){
 const base=BASE_POS[type];if(!base)return null;
 const offsets=[[0,0,0],[0,0,-.70],[.85,0,0],[-.85,0,0],[0,0,.80],[1.4,0,0],[-1.4,0,0],[0,0,-1.5],[2.2,0,0],[-2.2,0,0],[0,0,2.2]];
 for(const offset of offsets){
   const pos=base.map((v,i)=>v+offset[i]);
   if(current().equipment.some(o=>POSITIONABLE.includes(o.type)&&MozduljRig.math.len(MozduljRig.math.sub(MozduljRig.propPosition(o),pos))<.35))continue;
   const before=clone(current()),after=clone(before);after.equipment.push({type,position:pos});
   if(!MozduljRig.Collision.transition(before,after).limited)return pos;
 }
 return null;
}
function updateFixedAnchorInputs(){
 $('fixedAnchorA').hidden=$('bandAnchorA').value!=='fixed';
 $('fixedAnchorB').hidden=$('bandAnchorB').value!=='fixed';
 $('fixedAnchorC').hidden=$('bandAnchorC').value!=='fixed';
 const hasC=$('bandAnchorC').value!=='none';
 $('bandAnchorD').disabled=!hasC;
 if(!hasC)$('bandAnchorD').value='none';
 $('fixedAnchorD').hidden=!hasC||$('bandAnchorD').value!=='fixed';
}
function setFixedInputs(prefix,v){const a=v|| (prefix==='A'?[-.8,.35,.45]:[.8,.35,.45]);for(const [idx,k] of ['X','Y','Z'].entries())$('band'+prefix+k).value=a[idx];}
function readFixedInputs(prefix){const v=['X','Y','Z'].map(k=>Number($('band'+prefix+k).value));if(v.some((n,i)=>!Number.isFinite(n)||Math.abs(n)>3||(i===1&&n<0)))throw Error('A rögzített pont koordinátája nem megengedett.');return v;}
function fillSelectedBand(){
 const i=$('bandSelect').value;
 if(i==='new'){ $('bandAnchorA').value='leftHand';$('bandAnchorB').value='rightHand';$('bandRoute').value='auto';$('bandAnchorC').value='none';$('bandAnchorD').value='none';setFixedInputs('D',[.25,.35,.45]);setFixedInputs('A');setFixedInputs('B');setFixedInputs('C',[0,.35,.45]);updateFixedAnchorInputs();return; }
 const obj=current().equipment[+i];
 if(!obj)return;
 const legacy=obj.anchor||'feet';
 $('bandAnchorA').value=obj.anchorA||({knees:'leftKnee',ankle:'leftAnkle',hands:'leftHand'}[legacy]||'leftAnkle');
 $('bandAnchorB').value=obj.anchorB||({knees:'rightKnee',ankle:'rightAnkle',hands:'rightHand'}[legacy]||'leftHand');
 $('bandRoute').value=obj.route||'auto';$('bandAnchorC').value=obj.anchorC||'none';$('bandAnchorD').value=obj.anchorD||'none';setFixedInputs('D',obj.positionD||[.25,.35,.45]);setFixedInputs('A',obj.position);setFixedInputs('B',obj.positionB);setFixedInputs('C',obj.positionC||[0,.35,.45]);updateFixedAnchorInputs();
}
function switchFrame(i){selectedEquipment=null;$('contactStatus').textContent='';activeIssue=null;stopPreview();closeGesture();frameIndex=Math.max(0,Math.min(i,pose.frames.length-1));sync();}
function showPresetAudit(name){
 const audit=MozduljRig.auditPreset(name),issues=audit.frames.reduce((sum,f)=>sum+f.overlaps.count+f.clamped.length,0);
 $('presetAudit').textContent=window.rigText('Sablon geometriai ellenőrzése: ')+issues+' '+window.rigText('eltérés')+' · '+window.rigText(audit.transition?'Átmenet ellenőrizve':'Átmenet javítandó')+'. '+window.rigText('Nem szakmai hitelesítés.');
}
function setPreset(name){stopWorkoutGuide();selectedEquipment=null;activeIssue=null;pendingPreset=null;$('presetConfirm').hidden=true;stopPreview();const apply=()=>{const camera=pose?studio.getCamera():null;pose=MozduljRig.initialPose(name);resetFrames=clone(pose.frames);frameIndex=0;studio.setCamera(camera||pose.camera);sync();};if(pose){closeGesture();tracked(apply);}else apply();msg(window.rigText('Pózsablon betöltve: ')+$('preset').selectedOptions[0].textContent);showPresetAudit(name);}
function load(p,track=false){stopWorkoutGuide();$('presetAudit').textContent='';selectedEquipment=null;activeIssue=null;pendingPreset=null;$('presetConfirm').hidden=true;stopPreview();MozduljRig.checkPose(p);const apply=()=>{const camera=track?studio.getCamera():null;pose=clone(p);resetFrames=clone(pose.frames);frameIndex=0;studio.setCamera(camera||pose.camera||MozduljRig.initialPose().camera);sync();};closeGesture();if(track)tracked(apply);else{apply();window.resetRigHistory();}msg('A 3D testhelyzet betöltve.');}
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
// Dedicated compact guide: no editing or independent playback controls.
function stopWorkoutGuide(){
 if(workoutGuideRequest)cancelAnimationFrame(workoutGuideRequest);
 workoutGuideRequest=0;workoutGuide=null;
}
window.setWorkoutGuideMode=()=>{
 stopPreview();stopWorkoutGuide();
 document.body.classList.add('view-only','workout-guide');setMode('orbit');return true;
};
function workoutGuideTick(now){
 workoutGuideRequest=0;
 const g=workoutGuide;if(!g)return;
 const extra=g.playing?Math.min(1000,Math.max(0,now-g.received)):0;
 const elapsed=Math.min(g.total*1000,g.elapsed*1000+extra);
 const next=MozduljRig.workoutGuideAt(g.plan,elapsed);
 if(g.display!==next){g.display=next;if(next)show();}
 if(g.playing&&extra<1000)workoutGuideRequest=requestAnimationFrame(workoutGuideTick);
}
window.setWorkoutGuideBase64=payload=>{
 if(!studio||!pose||!document.body.classList.contains('workout-guide'))return false;
 try{
   const timing=JSON.parse(b64toStr(payload));
   if(!timing||!MozduljRig.PHASE_DIRECTIONS.includes(timing.direction)||!Number.isFinite(timing.elapsedSeconds)||!Number.isFinite(timing.totalSeconds)||timing.totalSeconds<=0)return false;
   if(workoutGuideRequest)cancelAnimationFrame(workoutGuideRequest);
   const repetitions=timing.repetitions===true;
   const plan=workoutGuide?.direction===timing.direction&&workoutGuide.repetitions===repetitions?workoutGuide.plan:MozduljRig.workoutGuidePlan(pose.frames,timing.direction,repetitions);
   workoutGuide={direction:timing.direction,repetitions,elapsed:timing.elapsedSeconds,total:timing.totalSeconds,playing:timing.playing===true,received:performance.now(),plan,display:null};
   canvas.style.visibility=MozduljRig.workoutGuideAt(plan,0)?'visible':'hidden';
   const status=$('workoutGuideStatus');
   status.textContent=window.rigText(plan.kind==='unassigned'?'Ehhez az irányhoz nincs hozzárendelt 3D mozgás.':plan.kind==='blocked'?'Az animáció nem lejátszható biztonságosan; csak állókép jelenik meg.':'Szemléltető 3D útmutató');
   workoutGuideTick(performance.now());return true;
 }catch(e){stopWorkoutGuide();canvas.style.visibility='hidden';return false;}
};
window.clearWorkoutGuide=()=>{stopWorkoutGuide();canvas.style.visibility='hidden';return true;};
window.getRigHealth=()=>JSON.stringify({webgl:!!studio,frames:pose?.frames?.length,renderer:'pure-webgl-offline'});
window.setReadOnlyMode=()=>{
  if(!studio||!pose)return false;
  document.body.classList.add('view-only');
  document.querySelector('main').style.gridTemplateColumns='1fr';
  document.querySelector('.workspace-shell').style.display='none';
  $('workspaceDivider').hidden=true;
  $('modelDisplay').value='solid';
  // Retain camera and animation controls in the viewer, without editor tabs.
  document.querySelector('.viewport-shell').append(document.querySelector('.view-controls'),document.querySelector('.preview-tools'));
  // Keep projection selection visible in the standalone viewer as well.
  document.querySelector('.view-controls').append($('projection').closest('.line'));
  document.querySelector('.header strong').textContent='3D mozgásábra';
  document.querySelector('.header .hint').textContent='A modellen húzva körbeforgathatod a nézetet';
  document.querySelector('.float-l').style.display='none';
  document.querySelector('.float-b').style.display='none';
  setMode('orbit'); // drag always rotates the camera in the read-only viewer
  updatePhaseControls();show();return true;
};
const directionNames={all:'Irány nélkül',left:'Bal oldal',right:'Jobb oldal',forward:'Előre',backward:'Hátra',up:'Felfelé',down:'Lefelé',inward:'Befelé',outward:'Kifelé',clockwise:'Óramutató járásával megegyezően',counterclockwise:'Óramutató járásával ellentétesen',each_side:'Oldalanként'};
function playbackButtons(){
 const paused=animation?.paused;
 $('playPreview').textContent=animation?(paused?'▶ Folytatás':'Ⅱ Szünet'):'▶ Előnézet';
 $('quickPreview').textContent=animation&&!paused?'Ⅱ':'▶';
 $('stopPreview').disabled=!animation;
 $('previewSpeed').disabled=!!animation;$('previewLoop').disabled=!!animation;
}
function stopPreview(){
 if(!animation)return;
 cancelAnimationFrame(animation.request);animation=null;
 playbackButtons();$('previewStatus').textContent='';$('previewProgress').value=0;
 if(studio&&pose)show();
}
function playbackTick(now){
 const a=animation;if(!a||a.paused)return;
 if(a.last!==null)a.elapsed+=(now-a.last)*a.speed;
 a.last=now;
 const state=MozduljRig.previewAt(a.plan,a.elapsed);
 if(a.display!==state.frame){a.display=state.frame;show();}
 $('previewProgress').value=state.progress;
 const label=state.kind==='hold'?window.rigText('Kitartás')+' · '+(state.from+1): (state.from+1)+' → '+(state.to+1);
 if(label!==a.label){a.label=label;$('previewStatus').textContent=window.rigText('Ellenőrzött animációs előnézet')+' · '+label;}
 if(state.finished){
   // Leave the final phase visible; navigation is not a saved pose edit.
   stopPreview();switchFrame(pose.frames.length-1);return;
 }
 a.request=requestAnimationFrame(playbackTick);
}
function playPreview(){
 if(animation){
   if(animation.paused){animation.paused=false;animation.last=null;animation.request=requestAnimationFrame(playbackTick);}
   else {animation.paused=true;cancelAnimationFrame(animation.request);animation.last=null;}
   playbackButtons();return;
 }
 closeGesture();
 const plan=MozduljRig.previewPlan(pose.frames,$('previewLoop').checked);
 if(!plan.ok){
   const detail=plan.issue?.startsWith('bend:')?window.rigText('Hirtelen hajlításirány-váltás: ')+window.rigText(targetLabels[plan.issue.slice(5)]||plan.issue.slice(5)):plan.issue==='representation'?window.rigText('Eltérő vezérlés vagy rögzítés'):plan.issue==='empty'?window.rigText('Nincs lejátszható időtartam.'):plan.issue;
   const message=window.rigText('Nem lejátszható átmenet: ')+(plan.from!=null?(plan.from+1)+' → '+(plan.to+1)+' · ':'')+detail;
   $('previewStatus').textContent=message;msg(message);
   if(!document.body.classList.contains('view-only'))selectTab('phases');
   return;
 }
 animation={plan,elapsed:0,last:null,paused:false,speed:Number($('previewSpeed').value||1),display:plan.segments[0].samples[0],request:0,label:null};
 playbackButtons();show();animation.request=requestAnimationFrame(playbackTick);
}
// Reports are based on the same geometry as the editor, including band routing.
function reviewPose(){
 stopPreview();closeGesture();
 const findings=[];
 for(const [i,frame] of pose.frames.entries()){
   const j=MozduljRig.joints(frame);
   for(const [id,depth] of MozduljRig.Collision.overlaps(frame))findings.push({phase:i,kind:'collision',id,depth});
   for(const [id,depth] of MozduljRig.lockErrors(j,MozduljRig.effectiveLocks(frame)))if(depth>.005) findings.push({phase:i,kind:'lock',id,depth});
   for(const [key,depth] of MozduljRig.bodyLinkErrors(j,frame))if(depth>.005)findings.push({phase:i,kind:'link',id:'link:'+key,depth});
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
   const labels={link:'Testpontkapcsolat eltérése',collision:'Geometriai átfedés',lock:'Rögzített testpont eltérése',reach:'Elérési korlát',band:'Akadályos szalagútvonal',transition:'Nem lejátszható átmenet'};
   const bendTarget=item.id.startsWith('bend:')||item.id.startsWith('link:')?item.id.slice(5):null;
   const id=bendTarget?window.rigText(item.id.startsWith('link:')?'Testpontkapcsolat eltérése':'Hirtelen hajlításirány-váltás: ')+' '+window.rigText(targetLabels[bendTarget]||bendTarget):item.id.startsWith('body:')?item.id.slice(5).split('/').map(bodyPartLabel).join(' – '):targetLabels[item.id]?window.rigText(targetLabels[item.id]):item.id;
   button.textContent=(item.phase+1)+'. '+window.rigText('Fázis')+' · '+window.rigText(labels[item.kind])+' · '+id;
   button.onclick=()=>{
     switchFrame(item.phase);activeIssue=bendTarget?item.id:item.kind==='lock'?'lock:'+item.id:item.kind==='collision'?item.id:null;
     const key=bendTarget|| (targetLabels[item.id]?item.id:item.kind==='collision'&&item.id.startsWith('body:')?item.id.slice(5).split('/').map(id=>{const [part,side]=id.split('-');return side&&(part==='hand'||part==='forearm'?side+'Hand':part==='foot'||part==='shin'?side+'Foot':null);}).find(Boolean):null);
     if(key){if([...$('targetSelect').options].some(o=>o.value===key))$('targetSelect').value=key;else $('targetSelect').value='auto';selectedTarget=key;focusBody(bodyFocusFor(key));setMode('move');}else selectTab('body');show();
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
   entry.disabled=$(id).disabled;
   if(document.activeElement!==entry)entry.value=$(id).value;
 }
}
function setupPrecision(){
 for(const slider of document.querySelectorAll('input[type=range]:not(#zoom)')){
   const label=document.querySelector('label[for="'+slider.id+'"]');
   if(!label)continue;
   const row=document.createElement('div');row.className='precision-row';
   const number=document.createElement('input');number.type='number';number.min=slider.min;number.max=slider.max;number.step=slider.step||'1';number.value=slider.value;
   const unit=slider.id==='shoulderLift'?'cm':slider.id==='handCurl'?'%':'°';
   label.id='label-'+slider.id;
   number.setAttribute('aria-labelledby',label.id);
   const unitText=document.createElement('span');unitText.className='precision-unit';unitText.textContent=unit;
   unitText.setAttribute('aria-hidden','true');
   function apply(value){
     if(!Number.isFinite(value)){number.value=slider.value;return;}
     slider.value=String(Math.max(+slider.min,Math.min(+slider.max,value)));
     slider.dispatchEvent(new Event('input',{bubbles:true}));
     number.value=slider.value;
   }
   number.addEventListener('change',()=>apply(number.valueAsNumber));
   const button=(text,delta)=>{
     const b=document.createElement('button');b.type='button';b.textContent=text;
     b.setAttribute('aria-label',delta>0?'Növelés a kiválasztott lépésközzel':'Csökkentés a kiválasztott lépésközzel');
     b.onclick=()=>{if(slider.disabled)return;closeGesture();apply(+slider.value+delta*Number($('precisionStep').value));};return b;
   };
   const entry=document.createElement('div');entry.className='precision-entry';entry.append(number,unitText);
   row.append(button('−',-1),entry,button('+',1));
   const field=document.createElement('div');field.className='precision-field';
   if(label.closest('[data-body-group="central"]'))field.dataset.centralField=slider.id;
   label.before(field);field.append(label,slider,row);
   slider.classList.add('precision-slider');precisionControls.set(slider.id,number);
 }
 const rootHeight=$('rootHeight'),rootLabel=document.querySelector('label[for="rootHeight"]');
 const rootField=document.createElement('div');rootField.className='precision-field';rootField.dataset.centralField='rootHeight';
 rootLabel.before(rootField);rootField.append(rootLabel,rootHeight);
 syncPrecision();
}
function syncViewCube(){
 if(!studio)return;
 const name=MozduljRig.cameraViewName(studio.getCamera());
 for(const button of document.querySelectorAll('[data-camera-view]'))button.setAttribute('aria-pressed',String(button.dataset.cameraView===name));
}
function directionKey(){
 const c=studio.getCamera(),space=$('axisSpace').value;
 return JSON.stringify([current(),selectedTarget,space,$('symmetricEdit').checked,$('coupledGirdle').checked,space==='camera'?[c.azimuth,c.elevation]:null]);
}
function currentDirectionHints(){
 if(!directionHints.length||mode!=='move'||animation||directionSignature!==directionKey()){
   directionHints=[];$('directionStatus').textContent='';return [];
 }
 return directionHints;
}
function checkDirections(){
 if(!pose||!selectedTarget||animation||mode!=='move')return;
 directionHints=MozduljRig.probeTargetDirections(current(),MozduljRig.bodyTarget(selectedTarget),studio.getCamera(),$('axisSpace').value,$('symmetricEdit').checked,$('coupledGirdle').checked);
 directionSignature=directionKey();
 const label=h=>(h.sign>0?'+':'−')+h.axis;
 const allowed=directionHints.filter(h=>h.allowed).map(label).join(', '),blocked=directionHints.filter(h=>!h.allowed).map(label).join(', ');
 $('directionStatus').textContent=window.rigText('1 cm-es próba – elérhető: ')+(allowed||'—')+' · '+window.rigText('Nem igazolt: ')+(blocked||'—');
 show();
}
function syncPointCoordinates(force=false,rendered=null){
 if(!pose)return;
 const key=selectedTarget,sel=key&&MozduljRig.bodyTarget(key);
 const point=sel&&MozduljRig.bodyPoint(rendered||MozduljRig.joints(current()),key);
 $('pointCoordinates').hidden=!point;
 const movable=!!sel&&sel.kind!=='inspect'&&!animation;
 for(const [i,id] of ['pointX','pointY','pointZ'].entries()){
   const input=$(id);input.disabled=!movable;
   if(force||document.activeElement!==input)input.value=point?Number((point[i]*100).toFixed(1)):'';
 }
 $('applyPointCoordinates').disabled=!movable;
 $('resetBodyPart').disabled=$('bodyFocus').value==='all'||!resetFrames[frameIndex]||!!animation;
}
function applyPointCoordinates(){
 const sel=selectedTarget&&MozduljRig.bodyTarget(selectedTarget);
 if(!sel||sel.kind==='inspect')return;
 const desired=['pointX','pointY','pointZ'].map(id=>$(id).valueAsNumber/100);
 if(desired.some(v=>!Number.isFinite(v)||Math.abs(v)>3)){msg('Érvénytelen testpont-koordináta.');return;}
 closeGesture();
 const opts=MozduljRig.targetEditOptions(sel,$('symmetricEdit').checked,$('adaptRootPath').checked,$('coupledGirdle').checked),m=MozduljRig.math;
 const result=guardedEdit(f=>opts.move(f,m.sub(desired,MozduljRig.bodyPoint(MozduljRig.joints(f),sel.key))),opts.prepare,opts.adapt);
 if(result.limited)checkDirections();
 syncPointCoordinates(true);
 if(!result.limited&&m.len(m.sub(desired,MozduljRig.bodyPoint(MozduljRig.joints(current()),sel.key)))>.005)msg('A kért koordinátát az anatómiai korlátok miatt nem lehet pontosan elérni. A tényleges helyzet látható.');
}
function restoreBodyPart(){
 const group=$('bodyFocus').value,reference=resetFrames[frameIndex],candidate=MozduljRig.resetBodyPart(current(),reference,group);
 if(!candidate)return;
 closeGesture();stopPreview();
 // Like mirror/copy, this is a discrete construction action, not a drag.
 // Reject any new overlap or deeper violation of an existing lock.
 const issue=MozduljRig.copyIssue(current(),candidate);
 if(issue){activeIssue=issue.id;show();msg('A testrész visszaállítása a jelenlegi rögzítések vagy ütközések miatt nem lehetséges.');return;}
 tracked(()=>{pose.frames[frameIndex]=candidate;});sync();msg('A kiválasztott testrész kiinduló beállításai visszaállítva.');
}
function setupWorkspaceDivider(){
 const main=document.querySelector('main'),handle=$('workspaceDivider'),mq=window.matchMedia('(max-width:760px)');
 const shares={vertical:56,horizontal:60};let pointer=null;
 const apply=value=>{
   const vertical=mq.matches,key=vertical?'vertical':'horizontal';
   shares[key]=MozduljRig.workspaceShare(value,vertical);
   main.style.setProperty('--model-share',shares[key]+'fr');main.style.setProperty('--inspector-share',(100-shares[key])+'fr');
   handle.setAttribute('aria-orientation',vertical?'horizontal':'vertical');
   handle.setAttribute('aria-valuemin',vertical?'50':'38');handle.setAttribute('aria-valuemax',vertical?'75':'72');
   handle.setAttribute('aria-valuenow',String(Math.round(shares[key])));
   handle.setAttribute('aria-valuetext',Math.round(shares[key])+'%');
 };
 const reset=()=>apply(shares[mq.matches?'vertical':'horizontal']);reset();
 if(mq.addEventListener)mq.addEventListener('change',reset);else mq.addListener(reset);
 handle.addEventListener('pointerdown',event=>{if(pointer!==null||event.button!==0)return;pointer=event.pointerId;handle.setPointerCapture(pointer);event.preventDefault();});
 handle.addEventListener('pointermove',event=>{
   if(pointer!==event.pointerId)return;
   const box=main.getBoundingClientRect(),vertical=mq.matches;
   const extent=vertical?box.height:box.width,offset=vertical?event.clientY-box.top:event.clientX-box.left;
   apply(offset/Math.max(1,extent)*100);
 });
 for(const name of ['pointerup','pointercancel','lostpointercapture'])handle.addEventListener(name,event=>{if(event.pointerId===pointer)pointer=null;});
 handle.addEventListener('keydown',event=>{
   const vertical=mq.matches,key=vertical?'vertical':'horizontal',increase=vertical?'ArrowDown':'ArrowRight',decrease=vertical?'ArrowUp':'ArrowLeft';
   if(![increase,decrease,'Home','End'].includes(event.key))return;
   event.preventDefault();apply(event.key==='Home'?0:event.key==='End'?100:shares[key]+(event.key===increase?3:-3));
 });
 handle.addEventListener('dblclick',()=>apply(mq.matches?56:60));
}
function connect(){try{studio=MozduljRig.shaded3D(canvas);setPreset('stand');}catch(e){initError=e;$('canvasStatus').textContent=e.message;msg(window.rigText('WebGL nem áll rendelkezésre: ')+e.message);return;}
 setupPrecision();setupWorkspaceDivider();
 $('modelDisplay').onchange=show;
 $('axisSpace').onchange=show;$('moveAxis').onchange=show;
 $('checkDirections').onclick=checkDirections;
 $('applyPointCoordinates').onclick=applyPointCoordinates;
 $('resetBodyPart').onclick=restoreBodyPart;
 $('toggleViewCube').onclick=()=>{const open=$('viewCube').hidden;$('viewCube').hidden=!open;$('toggleViewCube').setAttribute('aria-expanded',String(open));};
 for(const button of document.querySelectorAll('[data-camera-view]'))button.onclick=()=>{MozduljRig.setCameraView(studio.getCamera(),button.dataset.cameraView);show();};
 $('viewCube').onkeydown=event=>{if(event.key==='Escape'){$('viewCube').hidden=true;$('toggleViewCube').setAttribute('aria-expanded','false');$('toggleViewCube').focus();}};
 for(const tab of document.querySelectorAll('.workspace-tabs [role=tab]')){
   tab.onclick=()=>selectTab(tab.id.slice(4));
   tab.onkeydown=e=>{
     const tabs=[...document.querySelectorAll('.workspace-tabs [role=tab]')],i=tabs.indexOf(tab);
     let n=e.key==='ArrowRight'?(i+1)%tabs.length:e.key==='ArrowLeft'?(i+tabs.length-1)%tabs.length:e.key==='Home'?0:e.key==='End'?tabs.length-1:-1;
     if(n>=0){e.preventDefault();tabs[n].click();tabs[n].focus();}
   };
 }
 $('bodyFocus').onchange=e=>{
   const value=e.target.value;
   // The inspector and drag selection must not refer to different limbs.
   const targets={root:'root',leftArm:'leftHand',rightArm:'rightHand',leftLeg:'leftFoot',rightLeg:'rightFoot'};
   const key=targets[value]||'auto';
   $('targetSelect').value=key;selectedTarget=key==='auto'?null:key;
   focusBody(value,false);show();
 };
 $('quickPreview').onclick=playPreview;
 $('showAllTargets').onchange=show;
 $('contactAction').onchange=()=>{syncContactSurfaces();$('contactStatus').textContent='';};
 $('fitContact').onclick=fitContact;
 $('inspectContacts').onclick=inspectContacts;
 $('overlapTargets').onchange=e=>{if(!e.target.value)return;$('targetSelect').value=e.target.value;chooseTarget(e.target.value);$('overlapTargets').hidden=true;};
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
 for(const input of document.querySelectorAll('[data-lock-point]'))input.onchange=()=>changePointLock(input);
 $('clearPointLocks').onclick=()=>{closeGesture();tracked(()=>{delete current().pointLocks;delete current().equipmentPointLocks;delete current().bodyPointLinks;});sync();};
 $('linkFollower').value='rightHand';
 $('addBodyLink').onclick=()=>{stopPreview();closeGesture();const candidate=clone(current());if(!MozduljRig.setBodyLink(candidate,$('linkFollower').value,$('linkLeader').value)){msg(window.rigText('A testpontkapcsolat nem hozható létre. Ellenőrizd a rögzítéseket és a vezérlési módot.'));return;}tracked(()=>{pose.frames[frameIndex]=candidate;});sync();};
 $('removeBodyLink').onclick=()=>{stopPreview();closeGesture();tracked(()=>MozduljRig.removeBodyLink(current(),$('linkFollower').value));sync();};
 $('rootHeight').onchange=e=>{
   const value=e.target.valueAsNumber/100;
   if(!Number.isFinite(value)||value<0||value>3){sync();msg('Érvénytelen medencemagasság.');return;}
   guardedEdit(f=>MozduljRig.moveRoot(f,[0,value-f.root[1],0]),null,$('adaptRootPath').checked?'root':true);sync();
 };
 $('playPreview').onclick=playPreview;
 $('stopPreview').onclick=stopPreview;
 $('previewSpeed').onchange=stopPreview;
 $('phaseType').onchange=e=>{stopPreview();closeGesture();tracked(()=>{if(e.target.value)current().phaseType=e.target.value;else delete current().phaseType;});sync();};
 for(const id of ['transitionSeconds','holdSeconds'])$(id).onchange=e=>{
   stopPreview();closeGesture();const input=e.target,value=input.value===''?null:input.valueAsNumber;
   const candidate=clone(current());if(value===null)delete candidate[id];else candidate[id]=value;
   if(!MozduljRig.validPhaseTiming(candidate)){sync();msg('Érvénytelen fázisidőzítés.');return;}
   tracked(()=>{if(value===null)delete current()[id];else current()[id]=value;});sync();
 };
 $('phaseList').onchange=e=>switchFrame(+e.target.value);
 $('phaseDirection').onchange=e=>{
   stopPreview();closeGesture();tracked(()=>{if(e.target.value)current().direction=e.target.value;else delete current().direction;});sync();
 };
 function reorder(delta){
   stopPreview();closeGesture();tracked(()=>{if(MozduljRig.movePhase(pose.frames,frameIndex,frameIndex+delta)){MozduljRig.movePhase(resetFrames,frameIndex,frameIndex+delta);frameIndex+=delta;}});sync();
 }
 $('phaseUp').onclick=()=>reorder(-1);$('phaseDown').onclick=()=>reorder(1);
 $('copyPhase').onclick=()=>discreteEdit(f=>f,()=>null,true);
 $('phaseOverlay').onchange=show;
 $('copyPhaseBodyPart').onclick=applyPhaseBodyCopy;
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
   closeGesture();tracked(()=>{pose.frames.splice(frameIndex+1,0,next);resetFrames.splice(frameIndex+1,0,clone(next));frameIndex++;});
   sync();msg('Új 3D fázis hozzáadva. Módosítsd a testhelyzetet vagy nevezd át.');
 });
 $('removePhase').addEventListener('click',()=>{
   stopPreview();
   if(!pose||pose.frames.length<=1){msg('Legalább egy fázis szükséges.');return;}
   closeGesture();tracked(()=>{pose.frames.splice(frameIndex,1);resetFrames.splice(frameIndex,1);frameIndex=Math.min(frameIndex,pose.frames.length-1);});
   sync();msg('Az aktuális fázist töröltem.');
 });
 $('phaseName').addEventListener('input',e=>{
   if(!pose)return;
   stopPreview();beginGesture($('phaseName'));current().name=e.target.value.slice(0,64);
   $('phaseLabel').textContent=current().name||('Fázis '+(frameIndex+1));syncTimeline();
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
 $('torso').oninput=e=>guardedEdit(f=>{f.euler[0]=rad(+e.target.value);},null,true);
 $('roll').oninput=e=>guardedEdit(f=>{f.headRoll=rad(+e.target.value);});
 $('shoulderLift').oninput=e=>guardedEdit(f=>{f.shoulderLift=+e.target.value/100;},null,true);
 $('pelvicTilt').oninput=e=>guardedEdit(f=>{f.pelvicTilt=rad(+e.target.value);},null,true);
 $('upperYaw').oninput=e=>guardedEdit(f=>{f.upperYaw=rad(+e.target.value);},null,true);
 $('spineBend').oninput=e=>guardedEdit(f=>{f.spineBend=rad(+e.target.value);},null,true);
 for(const id of Object.keys(MozduljRig.ARTICULATION_LIMITS))if($(id))$(id).oninput=e=>guardedEdit(f=>{f[id]=rad(+e.target.value);},null,true);
 for(const [kind,selector] of [['arm','armSide'],['leg','legSide']])
   for(const suffix of MozduljRig.SIDE_ARTICULATION[kind])$('side'+suffix).oninput=e=>{
     const selected=$(selector).value,value=rad(+e.target.value),symmetric=$('symmetricEdit').checked;
     guardedEdit(f=>MozduljRig.editSideArticulation(f,selected,suffix,value,symmetric,$('coupledGirdle').checked),null,/^(Shoulder|Scapula)/.test(suffix));
   };
 $('handShape').onchange=e=>{
   const selected=$('armSide').value,symmetric=$('symmetricEdit').checked;
   const value=e.target.value;
   if(value==='custom')return;
   closeGesture();
   const curl=value==='legacy'?null:MozduljRig.HAND_PRESETS[value];
   guardedEdit(f=>MozduljRig.setHandCurl(f,selected,curl,symmetric));syncJointValues();
 };
 $('handCurl').oninput=e=>{
   const selected=$('armSide').value,symmetric=$('symmetricEdit').checked;
   if(MozduljRig.handCurl(current(),selected)===null)return;
   guardedEdit(f=>MozduljRig.setHandCurl(f,selected,Number(e.target.value)/100,symmetric));
 };
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
   if(['band','miniband','strap'].includes(type)){const id=MozduljRig.newEquipmentId(pose.frames);const result=guardedEdit(f=>f.equipment.push({type,id,anchorA:type==='miniband'?'leftKnee':'leftHand',anchorB:type==='miniband'?'rightKnee':'rightHand',route:'auto'}));
     if(!result.limited){syncBands(String(current().equipment.length-1));syncEquipmentSelection();msg('Szalag beszúrva; lent beállíthatod a rögzítési pontjait.');}return;}
   if(POSITIONABLE.includes(type)||!current().equipment.some(o=>o.type===type)){
     const pos=POSITIONABLE.includes(type)?freePlacement(type):null;
     if(POSITIONABLE.includes(type)&&!pos){msg('Az eszköznek nem találtam akadálymentes helyet. Módosítsd a pózt vagy válassz másik eszközt.');return;}
     const id=MozduljRig.newEquipmentId(pose.frames);
     const result=guardedEdit(f=>f.equipment.push(pos?{type,id,position:pos}:{type,id}));
     if(!result.limited){syncProps(String(current().equipment.length-1));syncLocks();syncEquipmentSelection();msg(window.rigText('Eszköz hozzáadva: ')+$('equip').selectedOptions[0].textContent);}
   }else msg('Ez az eszköz már szerepel ebben a fázisban.');
 };
 $('clearEquipment').onclick=()=>{selectedEquipment=null;stopPreview();closeGesture();tracked(()=>{const bound=new Set(Object.values(current().equipmentPointLocks||{}).map(b=>b.equipmentId));current().equipment=current().equipment.filter(p=>p.locked||bound.has(p.id));});syncBands('new');syncProps('none');syncLocks();syncEquipmentSelection();show();msg('Az aktuális fázis eszközeit eltávolítottam.');};
 $('propSelect').onchange=()=>{fillProp();chooseEquipment('prop:'+$('propSelect').value);};
 function propLocations(){
   const i=Number($('propSelect').value);
   return $('propScope').value==='all'?MozduljRig.matchingProp(pose.frames,frameIndex,i):[{frame:frameIndex,index:i}];
 }
 $('propLocked').onchange=()=>{
   stopPreview();closeGesture();const i=Number($('propSelect').value);
   if(!Number.isInteger(i))return;
   tracked(()=>{const prop=current().equipment[i];if($('propLocked').checked)prop.locked=true;else delete prop.locked;});syncProps(String(i));syncEquipmentSelection();show();
 };
 $('deleteProp').onclick=()=>{
   stopPreview();closeGesture();const locations=propLocations();
   if(locations.some(({frame,index})=>{const f=pose.frames[frame],prop=f.equipment[index];return Object.values(f.equipmentPointLocks||{}).some(b=>b.equipmentId===prop.id);})){msg('Előbb oldd fel az eszközhöz kötött testpontokat.');return;}
    if(locations.some(({frame,index})=>pose.frames[frame].equipment[index].locked)){msg('Előbb oldd fel az eszköz rögzítését.');return;}
   tracked(()=>{for(const {frame,index} of locations)pose.frames[frame].equipment.splice(index,1);});selectedEquipment=null;
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
 $('bandSelect').onchange=()=>{fillSelectedBand();const i=$('bandSelect').value;if(i==='new'){selectedEquipment=null;syncEquipmentSelection();show();}else chooseEquipment('band:'+i+':A');};
 $('bandAnchorA').onchange=updateFixedAnchorInputs;
 $('bandAnchorB').onchange=updateFixedAnchorInputs;
 $('bandAnchorC').onchange=updateFixedAnchorInputs;
 $('bandAnchorD').onchange=updateFixedAnchorInputs;
 $('applyBand').onclick=()=>{
   const a=$('bandAnchorA').value,b=$('bandAnchorB').value;
   if(a===b&&a!=='fixed'){msg('A szalag két vége nem lehet ugyanazon a ponton.');return;}
   let old=$('bandSelect').value==='new'?null:current().equipment[+$('bandSelect').value];
   let o={type:old?.type||'band',anchorA:a,anchorB:b,route:$('bandRoute').value};
   if(old?.id!=null)o.id=old.id;else if(!old)o.id=MozduljRig.newEquipmentId(pose.frames);
   if($('bandAnchorC').value!=='none')o.anchorC=$('bandAnchorC').value;
   if(o.anchorC&&$('bandAnchorD').value!=='none')o.anchorD=$('bandAnchorD').value;
   try{if(a==='fixed')o.position=readFixedInputs('A');if(b==='fixed')o.positionB=readFixedInputs('B');if(o.anchorC==='fixed')o.positionC=readFixedInputs('C');if(o.anchorD==='fixed')o.positionD=readFixedInputs('D');}
   catch(e){msg(e.message);return;}
   if(a==='fixed'&&b==='fixed'&&MozduljRig.math.len(MozduljRig.math.sub(o.position,o.positionB))<.001){msg('A szalag két vége nem lehet ugyanazon a ponton.');return;}
   const i=$('bandSelect').value,index=i==='new'?current().equipment.length:+i;
   const candidate=clone(current()),wasBlocked=i!=='new'&&studio.bandIssues(current()).includes(index);
   if(i==='new')candidate.equipment.push(o);else candidate.equipment[index]=o;
   if(studio.bandIssues(candidate).includes(index)&&!wasBlocked){msg('A szalag új útvonala akadályba ütközne.');return;}
   closeGesture();tracked(()=>{pose.frames[frameIndex]=candidate;});
   syncBands(i==='new'?String(current().equipment.length-1):i);syncEquipmentSelection();show();
   msg(window.rigText('Szalagrögzítés mentve: ')+$('bandAnchorA').selectedOptions[0].textContent+' – '+$('bandAnchorB').selectedOptions[0].textContent);
 };
 $('deleteBand').onclick=()=>{stopPreview();const i=$('bandSelect').value;
   if(i==='new'){msg('Válassz egy meglévő szalagot a törléshez.');return;}
   closeGesture();tracked(()=>current().equipment.splice(+i,1));selectedEquipment=null;sync();show();msg('Szalag eltávolítva.');
 };
 for(const id of Object.keys(MozduljRig.CAMERA_VIEWS))$(id).onclick=()=>{MozduljRig.setCameraView(studio.getCamera(),id);show();};
 $('projection').onchange=e=>{studio.setProjection(e.target.value);show();};
 $('focusTarget').onclick=()=>{const key=selectedTarget||'root',point=MozduljRig.bodyPoint(MozduljRig.joints(current()),key);if(point){studio.getCamera().target=[...point];show();}};
 $('fit').onclick=()=>{let c=studio.getCamera();Object.assign(c,{azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]});sync();};
 for(const m of ['orbit','move','pan','editEquipment'])$(m).onclick=()=>{closeGesture();setMode(m);msg(m==='editEquipment'?'Válassz eszközpontot, majd húzd a kívánt helyre.':m==='move'?'Válassz testpontot, majd húzd a kívánt helyre.':m==='pan'?'Húzással eltolhatod az egész képet, a testarányok változtatása nélkül.':'Húzással elforgathatod a kamerát.');};
 $('equipmentPoint').onchange=e=>chooseEquipment(e.target.value);
 $('detachBandPoint').onclick=()=>{
   const h=selectedEquipment;if(!h||h.kind!=='band'||h.movable)return;
   closeGesture();
   tracked(()=>MozduljRig.detachBandPoint(current(),h.index,h.which));
   sync();msg('A szalagpont leválasztva, a helyzete változatlan.');
 };
 $('export').onclick=()=>{let s=window.exportRigJson();$('json').value=s;try{let blob=new Blob([s],{type:'application/json;charset=utf-8'});let url=URL.createObjectURL(blob);let a=document.createElement('a');a.download='mozdulj-3d-poz.json';a.href=url;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}catch{}msg('3D pózadat mentve.');};
 $('import').onclick=()=>{try{load(JSON.parse($('json').value),true);}catch(e){msg(window.rigText('Hibás JSON: ')+e.message);}};
 function loc(e){let r=canvas.getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top};}
 canvas.addEventListener('pointerdown',e=>{
   if(!studio||pointers.has(e.pointerId)||pointers.size>=2)return;
   if(e.pointerType==='mouse'&&e.button!==0&&e.button!==2)return;
   canvas.setPointerCapture(e.pointerId);let p=loc(e);
   pointers.set(e.pointerId,p);
   if(pointers.size===2){
     if(gesture===canvas)closeGesture();
     drag=null;multiGesture=true;pinch=MozduljRig.gestureMetrics([...pointers.values()]);return;
   }
   if(multiGesture)return;
   if(mode==='move'||mode==='editEquipment')stopPreview();
   let sel=null;
   if(mode==='move'){
     const explicit=explicitTarget($('targetSelect').value),hits=explicit?[explicit]:studio.pickAll(p.x,p.y,$('showAllTargets').checked);
     const chooser=$('overlapTargets');chooser.replaceChildren();chooser.hidden=hits.length<2;
     if(hits.length>1){
       const blank=document.createElement('option');blank.value='';blank.textContent='Átfedő pontok – válassz';chooser.appendChild(blank);
       for(const t of hits){const option=document.createElement('option');option.value=t.key;option.textContent=window.rigText(targetLabels[t.key]);chooser.appendChild(option);}
       selectTab('body');
     }else sel=hits[0]||null;
     selectedTarget=targetKey(sel);
     if(sel){focusBody(bodyFocusFor(selectedTarget));if(sel.kind!=='inspect')beginGesture(canvas);else sel=null;}
     show();
   }
   if(mode==='editEquipment'&&e.button!==2){
     const hits=studio.pickEquipment(current(),p.x,p.y);
     const selected=hits.find(h=>h.key===selectedEquipment?.key);
     if(hits.length>1&&!selected){msg('Több eszközpont fedi egymást. Válassz az eszközpontok listájából.');selectTab('props');}
     else{
       const hit=selected||hits[0];
       if(hit){chooseEquipment(hit.key);if(hit.movable){sel=hit;beginGesture(canvas);}else msg($('equipmentPointStatus').textContent);}
     }
   }
   drag={id:e.pointerId,x:p.x,y:p.y,sel,button:e.button};
 });
 canvas.addEventListener('pointermove',e=>{
   if(!studio||!pointers.has(e.pointerId))return;
   const p=loc(e);pointers.set(e.pointerId,p);
   if(pointers.size===2){
     const next=MozduljRig.gestureMetrics([...pointers.values()]);
     if(pinch)MozduljRig.pinchCamera(studio.getCamera(),pinch,next,canvas.clientHeight);
     pinch=next;$('zoom').value=studio.getCamera().distance;show();return;
   }
   if(multiGesture||!drag||drag.id!==e.pointerId)return;
   let dx=p.x-drag.x,dy=p.y-drag.y,c=studio.getCamera();if(mode==='move'){
   if(drag.sel){
     const sel=drag.sel,opts=MozduljRig.targetEditOptions(sel,$('symmetricEdit').checked,$('adaptRootPath').checked,$('coupledGirdle').checked);
     const result=guardedEdit(f=>opts.move(f,MozduljRig.pointDragDelta(f,sel,c,canvas.clientHeight,dx,dy,e.shiftKey?'depth':$('moveAxis').value,$('axisSpace').value)),opts.prepare,opts.adapt);
     drag.blocked=result.limited;
   }
 }else if(mode==='editEquipment'&&drag.button!==2){
   const h=drag.sel;if(h){
     const delta=MozduljRig.equipmentDragDelta(c,canvas.clientHeight,dx,dy,e.shiftKey?'depth':$('equipmentAxis').value);
     if(h.kind==='prop'){
       const prop=current().equipment[h.index];
       if(prop&&!prop.locked){
         const pos=MozduljRig.math.add(MozduljRig.propPosition(prop),delta).map((v,i)=>Math.max(i===1?0:-3,Math.min(3,v)));
         const result=MozduljRig.editProps(pose.frames,propLocations(),pos,MozduljRig.propYaw(prop));
         if(result.ok){tracked(()=>{pose.frames=result.frames;});fillProp();}
         else {activeIssue=result.issue;msg(window.rigText('Az eszköz módosítása nem lehetséges ebben a fázisban: ')+(result.frame+1)+' · '+result.issue);}
       }
     }else{
       const candidate=clone(current()),wasBlocked=studio.bandIssues(current()).includes(h.index);
       if(MozduljRig.moveBandPoint(candidate,h.index,h.which,delta)){
         const blocked=studio.bandIssues(candidate).includes(h.index);
         if(blocked&&!wasBlocked)msg('A szalag új útvonala akadályba ütközne.');
         else{tracked(()=>{pose.frames[frameIndex]=candidate;});fillSelectedBand();}
       }
     }
   }
 }else if(mode==='pan'||drag.button===2){MozduljRig.panCamera(c,dx,dy,canvas.clientHeight);}else{c.azimuth-=dx*.012;c.elevation=Math.max(-1.25,Math.min(1.25,c.elevation+dy*.008));}drag.x=p.x;drag.y=p.y;show();});
 ['pointerup','pointercancel','lostpointercapture'].forEach(n=>canvas.addEventListener(n,e=>{
   if(!pointers.delete(e.pointerId))return;
   if(drag?.id===e.pointerId){const blocked=drag.blocked;drag=null;if(gesture===canvas)closeGesture();sync();if(blocked&&mode==='move')checkDirections();}
   if(pointers.size<2)pinch=null;
   if(!pointers.size){multiGesture=false;drag=null;}
 }));
 canvas.addEventListener('dblclick',()=>{$('focusTarget').click();});canvas.addEventListener('contextmenu',e=>e.preventDefault());
 canvas.addEventListener('wheel',e=>{e.preventDefault();let c=studio.getCamera();c.distance=Math.max(1.2,Math.min(10,c.distance+Math.sign(e.deltaY)*.22));$('zoom').value=c.distance;show();},{passive:false});
 let resizeRequest=null;const requestResize=()=>{if(resizeRequest!==null)return;resizeRequest=requestAnimationFrame(()=>{resizeRequest=null;show();});};
 window.addEventListener('resize',requestResize);const observer=new ResizeObserver(requestResize);observer.observe(canvas);
 document.addEventListener('visibilitychange',()=>{if(document.hidden){stopPreview();closeGesture();drag=null;pointers.clear();pinch=null;multiGesture=false;}else show();});
 syncHistory();
}
connect();
})();
