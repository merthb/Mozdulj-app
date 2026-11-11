/* Offline catalog migration: movement-family hints and individually positioned 3D
 * targets. This is an illustration draft, not clinically validated biomechanics. */
'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm');
const root=path.join(__dirname,'..');
const enginePath=path.join(root,'Mozdulj.Maui/Resources/Raw/rig3d/engine.js');
const catalogPath=path.join(root,'Mozdulj.Maui/Resources/Raw/exercises.catalog.json');
const context={window:{}};vm.runInNewContext(fs.readFileSync(enginePath,'utf8'),context,{filename:'engine.js'});
const Rig=context.window.MozduljRig;
const catalog=JSON.parse(fs.readFileSync(catalogPath,'utf8'));
const groups={
  chin_tuck:'allbehuzas-ulve',
  head_turn:'fejforditas-jobbra-balra',
  head_tilt:'oldalso-nyakdontes',
  shoulder_circle:'vallkorzes-elore-hatra',
  shoulder_shrug:'vallfelhuzas-es-leengedes',
  scapular:'lapockazaras-ulve gumiszalagos-lapockazaras hati-lapockamozgatas-falnal',
  arm_circle:'karkorzes-kicsiben',
  arm_overhead:'karok-emelese-magas-tartasba karcsusztatas-falon fel-napudvozlet-modositva karok-es-labak-valtott-nyujtozasa teljes-testes-nyujtozas-allva',
  neck_relax:'nyak-es-vallov-lazitasa-legzessel',
  shoulder_external:'vallkulso-rotacio-szalaggal',
  arm_side:'oldalemeles-kis-sullyal',
  arm_forward:'eloreemeles-kis-sullyal',
  towel_chest:'vallovi-lazitas-torolkozovel torolkozos-mellkasnyitas',
  catcow:'macska-tehen-mozdulat',
  pelvic_tilt:'medencebillentes-hanyatt',
  torso_twist:'torzsforditas-ulve falnal-hati-forditas ulo-jogacsavaras-szeken',
  open_book:'nyitott-konyv-oldalfekvesben',
  chest_open:'mellkasnyitas-allva',
  bird_dog:'kar-lab-nyujtas-negykezlab bird-dog-tartas',
  child:'gyermekpoz terdelo-piheno-jogablokkal',
  seated_back_stretch:'hati-szakasz-nyujtasa-szeken kimelo-elorehajlas-szeken',
  thoracic_roller:'smr-hengeres-hati-mobilizalas',
  seated_side_bend:'oldalso-torzsnyujtas-ulve',
  knee_to_chest:'terdhuzas-mellkashoz-hanyatt',
  row:'gumiszalagos-evezes-ulve egykezes-evezes-tamaszkodva',
  breath:'hasi-legzes-hanyatt legzogyakorlat-ulve haton-fekvo-teljes-lazitas piheno-poz-hanyatt',
  core_brace:'hasfal-finom-aktivalasa',
  bridge:'hid-csipoemelessel farizomhid-minibanddel hid-tartasa csipoemeles-labbal-szeken hidpoz-jogablokkal',
  dead_bug:'dead-bug-kar-lab-valtassal',
  forearm_plank:'alkartamasz-terden',
  side_plank:'oldalso-alkartamasz-terden',
  quadruped_arm:'negykezlab-kartavolitas',
  seated_knee_raise:'ulo-terdemeles',
  stability_ball:'fitballon-ulo-torzsstabilitas labdas-medencekorzes',
  seated_knee_extend:'fitballos-terdnyujtas-ulve terdnyujtas-ulve combfeszites-ulve',
  anti_rotation:'gumiszalagos-torzsellenallas',
  heel_slide:'sarokcsusztatas-hanyatt',
  abdominal_curl:'felules-nelkuli-haspres',
  hip_hinge:'torzsdontes-csipobol labdan-finom-torzsdontes torzslazitas-fitballon',
  hip_circle:'csipokorzes-allva',
  hip_flexor_stretch:'csipohajlito-nyujtas-terdelve',
  glute_stretch:'farizomnyujtas-hanyatt fel-galambpoz-helyett-hanyatt-nyujtas',
  clam:'kagylonyitas-oldalfekvesben',
  side_leg_raise:'oldalfekvo-labemeles allo-csipooldalemeles gumiszalagos-csipooldalemeles',
  quadruped_hip:'csiponyitas-negykezlab',
  glute_standing:'farizomfeszites-allva',
  step_side:'oldalazo-lepesek-minibanddel oldalra-lepes lassu-oldalra-setalas',
  step_back:'hatralepes-minibanddel hatralepes-kicsiben',
  hip_extend:'allo-csipohatrafeszites',
  heel_raise:'sarokemeles-allva',
  toe_raise:'labujjemeles-allva',
  squat:'felguggolas-szekkel falnal-csusztatott-felguggolas',
  sit_stand:'szekrol-felallas',
  marching:'lepes-helyben konnyu-jaras-helyben',
  mini_lunge:'mini-kitores-tamaszkodva',
  step_up:'lepcsore-fellepes oldalso-fellepes',
  band_knee:'gumiszalagos-terdnyujtas',
  hamstring_stretch:'combhajlito-nyujtas-torolkozovel',
  ankle_circle:'bokakorzes-ulve',
  ankle_pump:'bokapumpa-ulve',
  toe_spread:'labujjak-szetterpesztese',
  towel_scrunch:'talpi-torolkozohuzas',
  calf_stretch:'vadli-nyujtasa-falnal',
  tandem:'tandem-allas',
  balance:'egy-labon-allas-tamasz-mellett fapoz-tamasz-mellett',
  weight_shift:'sulypontathelyezes-allva',
  heel_to_toe:'sarok-labujj-gordules',
  band_ankle:'bokamunka-gumiszalaggal oldaliranyu-bokaerosites-szalaggal',
  massage_ball:'puha-labda-gorditese-talp-alatt',
  wrist_circle:'csuklokorzes',
  fingers:'ujjak-nyitasa-zarasa',
  wrist_stretch:'csuklofeszito-nyujtas csuklohajlito-nyujtas',
  elbow_flex:'konyokhajlitas-ulve bicepsz-konnyu-sullyal',
  triceps:'tricepsznyujtas-gumiszalaggal',
  wrist_curl:'csuklohajlitas-konnyu-sullyal csuklofeszites-konnyu-sullyal',
  towel_grip:'torolkozo-enyhe-szoritasa',
  pull_apart:'gumiszalag-szethuzas-mellmagassagban',
  embrace:'karok-atolelese-mellkas-elott',
  mountain:'hegytartas',
  downward_dog:'lefele-nezo-kutya-modositva',
  cobra:'kobra-konnyitett',
  sphinx:'szfinxpoz',
  warrior1:'harcos-i-rovid-tartassal',
  warrior2:'harcos-ii-modositva',
  triangle:'haromszogpoz-konnyitett',
  butterfly:'pillangoules',
  seated_forward:'ulo-elorehajlas-jogapanttal',
  kneel_side:'terdelo-oldalso-nyujtozas',
  supine_twist:'gerinccsavaras-hanyatt',
  crescent:'oldalra-nyujtozo-holdsarlo',
  shoulders_hips:'vall-es-csipo-atmozgato-sor',
  bent_rest:'hengeres-farizom-lazitas',
  wall_breath:'mellkasnyito-legzes-falnal'
};
const familyMap={}; for(const [family,ids] of Object.entries(groups))for(const id of ids.split(/\s+/)){if(familyMap[id])throw Error('Double family '+id);familyMap[id]=family;}
const presetOverrides={
 'lapockazaras-ulve':'chair','torzsforditas-ulve':'chair','oldalso-torzsnyujtas-ulve':'chair',
 'egykezes-evezes-tamaszkodva':'hinge','labdan-finom-torzsdontes':'ballseat',
 'fitballon-ulo-torzsstabilitas':'ballseat','labdas-medencekorzes':'ballseat','torzslazitas-fitballon':'ballseat',
 'oldalso-alkartamasz-terden':'sideplank','kobra-konnyitett':'prone','szfinxpoz':'prone',
 'csipohajlito-nyujtas-terdelve':'kneel','terdelo-oldalso-nyujtozas':'kneel','terdelo-piheno-jogablokkal':'child',
 'hengeres-farizom-lazitas':'crosslegged','ulo-elorehajlas-jogapanttal':'crosslegged',
 'alkartamasz-terden':'plank','lefele-nezo-kutya-modositva':'tabletop',
 'felguggolas-szekkel':'stand','szekrol-felallas':'chair','mini-kitores-tamaszkodva':'lunge',
 'fapoz-tamasz-mellett':'balance', 'egy-labon-allas-tamasz-mellett':'balance',
 'allo-csipooldalemeles':'stand', 'gumiszalagos-csipooldalemeles':'stand',
 'gumiszalagos-evezes-ulve':'rowband','gumiszalagos-lapockazaras':'rowband',
 'vallkulso-rotacio-szalaggal':'stand','tricepsznyujtas-gumiszalaggal':'stand',
 'bokamunka-gumiszalaggal':'chair','oldaliranyu-bokaerosites-szalaggal':'chair',
 'gumiszalagos-terdnyujtas':'chair','gumiszalag-szethuzas-mellmagassagban':'stand',
 'konyokhajlitas-ulve':'chair', 'bokakorzes-ulve':'chair','bokapumpa-ulve':'chair'
};
const staticFamilies=new Set(['mountain','tandem','balance','breath','core_brace','stability_ball','glute_standing','towel_grip','fingers','toe_spread','wrist_circle']);
function vec(frame,key,side,p){frame[key][side]=p.slice();}
function both(frame,key,left,right){frame[key].left=left.slice();frame[key].right=right.slice();}
const mirror=(p)=>[-p[0],p[1],p[2]];
function setHands(f,L,R=mirror(L)){both(f,'hands',L,R);}
function setFeet(f,L,R=mirror(L)){both(f,'feet',L,R);}
function setElbowPoles(f,offset=0){f.poles.elbowLeft=[-.33,f.root[1]+.15,-.27+offset];f.poles.elbowRight=[.33,f.root[1]+.15,-.27+offset];}
function applyMovement(rig,fam,id){
 const a=rig.frames[0],b=rig.frames[1];b.name='Mozdulat';
 switch(fam){
 case 'chin_tuck':b.headPitch=.21;break;
 case 'head_turn':b.headYaw=.52;{const c=JSON.parse(JSON.stringify(a));c.name='Balra fordítás';c.headYaw=-.52;rig.frames.push(c);}break;
 case 'head_tilt':b.headRoll=.30;{const c=JSON.parse(JSON.stringify(a));c.name='Másik oldal';c.headRoll=-.30;rig.frames.push(c);}break;
 case 'shoulder_circle':b.shoulderLift=.075;setHands(b,[-.36,1.11,.27],[.36,1.11,.27]);{const c=JSON.parse(JSON.stringify(a));c.name='Hátra és le';setHands(c,[-.39,.92,-.18],[.39,.92,-.18]);rig.frames.push(c);}break;
 case 'shoulder_shrug':b.shoulderLift=.075;setHands(b,[-.33,1.01,.03]);break;
 case 'scapular':b.shoulderLift=-.01;setHands(b,[-.38,b.root[1]+.42,-.18]);break;
 case 'arm_circle':case 'arm_side':setHands(b,[-.73,1.46,.06]);break;
 case 'arm_overhead':setHands(b,[-.29,1.99,.05]);break;
 case 'neck_relax':b.headPitch=.14;b.headRoll=.16;b.shoulderLift=-.03;break;
 case 'shoulder_external':setHands(a,[-.28,a.root[1]+.3,.18]);setHands(b,[-.52,b.root[1]+.32,.19]);break;
 case 'arm_forward':setHands(b,[-.28,1.50,.44]);break;
 case 'towel_chest':case 'chest_open':setHands(b,[-.57,b.root[1]+.43,-.20]);b.euler[0]=.12;break;
 case 'catcow':b.spineBend=.42;b.headPitch=.22;break;
 case 'pelvic_tilt':b.euler[0]=a.euler[0]+.16;b.root[1]+= .045;break;
 case 'torso_twist':b.upperYaw=.46;break;
 case 'open_book':b.upperYaw=.50;setHands(b,[-.40,.78,-.18],[.38,.77,-.22]);break;
 case 'bird_dog':setHands(b,[-.26,.09,-.85],[.25,.08,-.54]);vec(b,'feet','right',[.18,.19,.91]);break;
 case 'child':b.euler[0]=a.euler[0]-.12;setHands(b,[-.28,.055,-.58]);break;
 case 'seated_back_stretch':b.euler[0]=-0.25;setHands(b,[-.32,b.root[1]+.20,.38]);break;
 case 'thoracic_roller':b.euler[0]=-1.40;b.headPitch=.24;break;
 case 'seated_side_bend':b.euler[2]=.23;setHands(b,[-.27,b.root[1]+.43,.07],[.32,b.root[1]+.85,.03]);break;
 case 'knee_to_chest':vec(b,'feet','right',[.12,.43,.27]);break;
 case 'row':setHands(b,[-.27,b.root[1]+.42,.08],[.28,b.root[1]+.42,.08]);break;
 case 'bridge':b.root[1]=Math.max(a.root[1]+.20,.50);break;
 case 'dead_bug':setHands(b,[-.27,.85,-.29],[.32,.11,-.40]);vec(b,'feet','right',[.22,.50,.36]);break;
 case 'forearm_plank':b.root[1]+= .11;break;
 case 'side_plank':b.root[1]+= .14;break;
 case 'quadruped_arm':setHands(b,[-.24,.075,-.80],[.24,.075,-.59]);break;
 case 'seated_knee_raise':vec(b,'feet','right',[.16,.41,.36]);break;
 case 'seated_knee_extend':vec(b,'feet','right',[.16,.40,.85]);break;
 case 'anti_rotation':b.euler[1]=.10;setHands(b,[-.30,b.root[1]+.53,.42],[.30,b.root[1]+.53,.42]);break;
 case 'heel_slide':vec(b,'feet','right',[.15,.055,.46]);break;
 case 'abdominal_curl':b.headPitch=.30;b.spineBend=.30;break;
 case 'hip_hinge':b.euler[0]=a.euler[0]-.29;b.root[1]-=.06;break;
 case 'hip_circle':b.euler[2]=.17;b.root[0]=.06;break;
 case 'hip_flexor_stretch':b.root[1]-=.04;vec(b,'feet','right',[.19,.08,-.37]);break;
 case 'glute_stretch':vec(b,'feet','right',[.18,.49,.30]);break;
 case 'clam':b.poles.kneeRight=[.20,.62,.50];break;
 case 'side_leg_raise':if(rig.preset==='side')vec(b,'feet','right',[.18,.57,.81]);else vec(b,'feet','right',[.65,.24,.08]);break;
 case 'quadruped_hip':vec(b,'feet','right',[.34,.47,.30]);break;
 case 'step_side':vec(b,'feet','right',[.51,.08,.04]);break;
 case 'step_back':vec(b,'feet','right',[.20,.07,-.50]);break;
 case 'hip_extend':vec(b,'feet','right',[.20,.24,-.48]);break;
 case 'heel_raise':b.root[1]+= .07;vec(b,'feet','left',[-.14,.14,0]);vec(b,'feet','right',[.14,.14,0]);break;
 case 'toe_raise':b.root[1]+= .02;vec(b,'feet','left',[-.14,.12,.02]);vec(b,'feet','right',[.14,.12,.02]);break;
 case 'squat':b.root[1]-=.18;setHands(b,[-.26,b.root[1]+.55,.37]);break;
 case 'sit_stand':b.root[1]=.94;setHands(b,[-.33,.95,.03]);break;
 case 'marching':vec(b,'feet','right',[.14,.44,.35]);break;
 case 'mini_lunge':b.root[1]=.58;break;
 case 'step_up':vec(b,'feet','right',[.19,.30,.36]);b.root[1]+= .05;break;
 case 'band_knee':vec(b,'feet','right',[.15,.36,.75]);break;
 case 'hamstring_stretch':vec(b,'feet','right',[.16,.79,.35]);break;
 case 'ankle_circle':case 'ankle_pump':case 'band_ankle':case 'wrist_circle':case 'wrist_curl':case 'wrist_stretch':case 'fingers':case 'toe_spread':case 'towel_scrunch':case 'massage_ball':
   rig.frames=[a];a.name='Alaphelyzet';rig.requiresFineJoints=true;break;
 case 'calf_stretch':vec(b,'feet','right',[.15,.06,-.37]);b.euler[0]=-.12;break;
 case 'weight_shift':b.root[0]=.09;b.euler[2]=-.08;break;
 case 'heel_to_toe':vec(b,'feet','right',[.15,.15,.28]);break;
 case 'elbow_flex':setHands(b,[-.28,b.root[1]+.46,.35]);break;
 case 'triceps':setHands(b,[-.28,b.root[1]+.89,.12]);break;
 case 'pull_apart':setHands(a,[-.29,1.27,.35]);setHands(b,[-.67,1.30,.26]);break;
 case 'embrace':setHands(b,[.17,b.root[1]+.48,.29],[-.17,b.root[1]+.48,.30]);break;
 case 'downward_dog':b.root[1]=.94;b.euler[0]=a.euler[0]+.19;break;
 case 'cobra':b.euler[0]=1.26;b.headPitch=-.13;b.root[1]=.34;break;
 case 'sphinx':b.root[1]=.37;b.euler[0]=1.34;break;
 case 'warrior1':b.root[1]+= .08;setHands(b,[-.27,b.root[1]+.94,.09]);break;
 case 'warrior2':b.root[1]+=.06;setHands(b,[-.76,b.root[1]+.51,.02]);break;
 case 'triangle':b.euler[2]=-.40;setHands(b,[-.58,b.root[1]+.08,.06],[.42,b.root[1]+.95,.05]);break;
 case 'butterfly':rig.frames=[a];a.name='Tartás';break;
 case 'seated_forward':b.euler[0]=-.37;setHands(b,[-.21,b.root[1]+.02,.59]);break;
 case 'kneel_side':b.euler[2]=-.31;setHands(b,[-.26,b.root[1]+.38,.02],[.30,b.root[1]+.98,.03]);break;
 case 'supine_twist':vec(b,'feet','right',[-.43,.17,.37]);b.euler[1]=.17;break;
 case 'crescent':b.euler[2]=.30;setHands(b,[-.29,b.root[1]+.95,.07],[.29,b.root[1]+.95,.07]);break;
 case 'shoulders_hips':b.shoulderLift=.035;b.euler[2]=.12;break;
 case 'wall_breath':b.euler[0]=.04;setHands(b,[-.52,1.43,.24]);break;
 case 'bent_rest':b.euler[0]=-.12;break;
 case 'breath':case 'core_brace':case 'stability_ball':case 'glute_standing':case 'towel_grip':case 'mountain':case 'tandem':case 'balance':
    rig.frames=[a];a.name='Tartás';break;
 default: throw Error('No apply movement '+fam+' '+id);
 }
 // Record the movement semantics as a provenance hint, not a professional approval.
 rig.movementId=fam;
 rig.reviewStatus='illustrative';
 rig.editorialNote=rig.requiresFineJoints?'A mozdulat finom kéz-/lábujjízületeket igényel: csak kiinduló testhelyzet látható.':'Egyénileg ellenőrizendő, szemléltető 3D mozdulat.';
}
const equipmentAliases={
 'jógamatrac':'mat','szék':'chair','gumiszalag':'band','miniband':'band','kézisúlyzó':'dumbbell',
 'fal':'wall','fitball':'ball','SMR-henger':'roller',
 'lépcső':'step','jógablokk':'block','jógapánt':'strap','törölköző':'towel','masszázslabda':'ball'
};
const fixedProps=new Set(['mat','chair','ball','roller','wall','block','step']);
const equipmentAudit=new Set();
const coverage=[];
for(const exercise of catalog.exercises){
 const family=familyMap[exercise.id];
 if(!family)throw Error('Missing movement mapping '+exercise.id);
 const old=exercise.pose.rig3D;
 const preset=presetOverrides[exercise.id]||old.preset;
 const rig=Rig.initialPose(preset);
 for(const f of rig.frames){f.equipment=[];}
 for(const f of rig.frames){
  for(const eq of exercise.equipment){const type=equipmentAliases[eq];if(!type)throw Error('Unknown equipment '+eq+' '+exercise.id);equipmentAudit.add(type);
   if(!f.equipment.some(it=>it.type===type)){
    let anchor='world';
    if(type==='dumbbell')anchor='hands';
    if(type==='band')anchor=eq==='miniband'||['bridge','side_leg_raise','step_side','step_back'].includes(family)?'knees':
      ['pull_apart','shoulder_external','scapular'].includes(family)?'hands':
      ['band_ankle'].includes(family)?'ankle':
      ['anti_rotation','triceps','band_knee'].includes(family)?'fixed':'feet';
    f.equipment.push({type,anchor});
   }
  }
  // Some mandatory props are inherent to preset even if not explicitly named.
  if(preset==='ballseat' && !f.equipment.some(it=>it.type==='ball'))f.equipment.push({type:'ball',position:[0,.34,0]});
 }
 applyMovement(rig,family,exercise.id);
 const different=rig.frames.length>1&&rig.frames.some(f=>JSON.stringify(f)!==JSON.stringify(rig.frames[0]));
 if(rig.frames.length>1&&!different)throw Error('Visually unchanged frames '+exercise.id);
 exercise.pose.rig3D=rig;
 coverage.push({id:exercise.id,name:exercise.name,preset,movement:family,frames:rig.frames.length,equipment:exercise.equipment.join(', '),review:'illusztratív; szakmai ellenőrzés szükséges'});
}
// Explicit whole-catalog technical coverage, but do not claim clinical correctness.
catalog.catalogVersion=9;
catalog.editorialNote='A WebGL Rig3D mozgáscsoportok és gyakorlatonkénti 3D célpontok szemléltetők; nyilvános kiadás előtt szakmai ellenőrzést igényelnek.';
fs.writeFileSync(catalogPath,JSON.stringify(catalog,null,2)+'\n','utf8');
const doc=['# v0.16 – 3D mozgásfázisok szakmai ellenőrzési jegyzéke','','Minden alábbi 3D pozíció **szemléltető**, nem szakmailag hitelesített gyakorlat.','', '| Gyakorlat | Mozgáscsoport | 3D sablon | Fázisok | Eszközök |','|---|---|---|---:|---|'];
for(const c of coverage)doc.push(`| ${c.name.replace(/\|/g,'/')} | ${c.movement} | ${c.preset} | ${c.frames} | ${c.equipment||'–'} |`);
fs.writeFileSync(path.join(root,'docs','RIG3D_MOZDULAT_AUDIT_v16.md'),doc.join('\n')+'\n');
fs.writeFileSync(path.join(root,'docs','rig3d_movement_coverage_v16.json'),JSON.stringify(coverage,null,2)+'\n');
console.log('converted',coverage.length,'dynamic',coverage.filter(c=>c.frames>1).length,'static',coverage.filter(c=>c.frames===1).length,'families',new Set(coverage.map(c=>c.movement)).size,'equipment',Array.from(equipmentAudit));
