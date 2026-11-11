#!/usr/bin/env python3
"""Curated pose corrections, based on human-readable movement instructions.
Reference illustrations were studied, NOT copied. Output landmarks remain illustrative,
not certified exercise prescriptions.
"""
from pathlib import Path
import copy,json,math,collections
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
data=json.loads(SRC.read_text(encoding='utf8'))
if data['catalogVersion'] >= 6:
 raise SystemExit('A v0.12 pose-migráció már megtörtént; nem módosítunk ismét. Az új ábrákhoz használd a beépített szerkesztőt.')
ex=data['exercises']; byid={e['id']:e for e in ex}
T={}
for e in ex:
 t=e['pose']['templateId']
 if t not in T:T[t]=copy.deepcopy(e['pose']['frames'][0]['joints'])
ST=copy.deepcopy(T['standing-shoulders'])
SE=copy.deepcopy(T['row'])
Q=copy.deepcopy(T['cat-cow'])
SUP=copy.deepcopy(T['bridge'])
SIDE=copy.deepcopy(T['side-leg'])
PL=copy.deepcopy(T.get('plank', T['standing-shoulders']))
SQ=copy.deepcopy(T['squat'])
LUNGE=copy.deepcopy(T['lunge'])
SOURCES={
'bridge':'https://www.cuh.nhs.uk/patient-information/hip-strengthening-exercises/',
'quad':'https://www.harvardhealth.org/exercise-and-fitness/bird-dog-exercise-for-your-core-how-to-do-it-safely',
'row':'https://www.acefitness.org/resources/everyone/exercise-library/48/seated-row/',
'sit':'https://www.nhs.uk/live-well/exercise/strength-exercises/',
'calf':'https://msk-bexley.nhs.uk/conditions/foot-and-ankle-pain/plantar-fasciitis',
'balance':'https://www.cuh.nhs.uk/patient-information/general-stability-exercises1/',
'chair':'https://www.nhs.uk/live-well/exercise/sitting-exercises/',
'clamshell':'https://www.cuh.nhs.uk/patient-information/general-stability-exercises1/',
'leg':'https://www.nhs.uk/live-well/exercise/strength-exercises/',
}
J=list(ST)
def frame(base, **coords):
 out=copy.deepcopy(base)
 for name, xy in coords.items():
  assert name in out,name
  if len(xy)==2:xy=(*xy,out[name]['z'])
  out[name]=dict(zip(('x','y','z'),xy))
 return out

PL=frame(ST,head=(91,37),neck=(80,44),leftShoulder=(70,50),rightShoulder=(72,54),
    leftElbow=(74,69),rightElbow=(76,73),leftWrist=(78,91),rightWrist=(81,92),
    leftHip=(41,58),rightHip=(43,62),leftKnee=(28,77),rightKnee=(32,81),
    leftAnkle=(11,91),rightAnkle=(17,92),leftToe=(7,93),rightToe=(13,95))

def record(id,start,end,ref=None,arrows=None):
 e=byid[id];p=e['pose'];
 p['templateId']='egyedi-'+id
 p['view']='side' if id in SIDE_VIEW else 'front'
 p['reviewStatus']='illustrative'
 p['source']='Mozgásleírás és nyilvános szakmai referencia alapján korrigált sematikus póz; felülvizsgálat szükséges.'
 if ref in SOURCES:p['source']+=' Referencia: '+SOURCES[ref]
 p['frames']=[{'label':'Kiinduló helyzet','joints':start},{'label':'Mozdulat','joints':end}]
 p['arrows']=arrows or []
 corrected.add(id)

corrected=set()
SIDE_VIEW=set('gyermekpoz macska-tehen-mozdulat bird-dog-tartas kar-lab-nyujtas-negykezlab negykezlab-kartavolitas csiponyitas-negykezlab lefele-nezo-kutya-modositva hid-csipoemelessel farizomhid-minibanddel hid-tartasa csipoemeles-labbal-szeken hidpoz-jogablokkal oldalso-alkartamasz-terden alkartamasz-terden oldalfekvo-labemeles kagylonyitas-oldalfekvesben nyitott-konyv-oldalfekvesben kobra-konnyitett szfinxpoz dead-bug-kar-lab-valtassal felules-nelkuli-haspres'.split())

# Explicit poses for core exercises. An exercise-specific 2D joint frame is much
# more faithful than identifying any word and borrowing an unrelated global pose.
child_start=frame(Q,head=(78,77),neck=(69,73),leftShoulder=(61,70),rightShoulder=(64,73),leftHip=(31,75),rightHip=(35,78),leftKnee=(29,86),rightKnee=(34,88),leftAnkle=(22,94),rightAnkle=(26,96),leftToe=(17,95),rightToe=(22,97),leftElbow=(79,75),rightElbow=(83,78),leftWrist=(93,82),rightWrist=(95,85))
record('gyermekpoz',Q,child_start)
bird=frame(Q,leftElbow=(83,47),leftWrist=(97,43),rightKnee=(18,55),rightAnkle=(8,53),rightToe=(3,55),head=(87,45),neck=(74,50))
record('bird-dog-tartas',Q,bird,ref='quad')
record('kar-lab-nyujtas-negykezlab',Q,bird,ref='quad')
record('negykezlab-kartavolitas',Q,frame(Q,leftElbow=(82,47),leftWrist=(96,39)),ref='quad')
record('csiponyitas-negykezlab',Q,frame(Q,rightKnee=(48,42),rightAnkle=(53,62),rightToe=(58,65)),ref='quad')
dog=frame(Q,head=(68,58),neck=(65,53),leftShoulder=(61,51),rightShoulder=(65,55),leftElbow=(75,71),rightElbow=(78,74),leftWrist=(88,92),rightWrist=(91,94),leftHip=(39,22),rightHip=(43,25),leftKnee=(27,56),rightKnee=(31,59),leftAnkle=(17,90),rightAnkle=(21,92),leftToe=(11,94),rightToe=(15,95))
record('lefele-nezo-kutya-modositva',Q,dog)
# Supine bridge: back/shoulders remain on floor, knee above planted feet, hips lift.
bridge_up=frame(SUP,leftHip=(54,50),rightHip=(59,53),leftKnee=(76,59),rightKnee=(79,62),leftAnkle=(85,89),rightAnkle=(89,91))
for id in ('hid-csipoemelessel','farizomhid-minibanddel','hid-tartasa','csipoemeles-labbal-szeken','hidpoz-jogablokkal'):
 record(id,SUP,bridge_up,ref='bridge')

sideplank=frame(SIDE,head=(16,38),neck=(24,49),leftShoulder=(28,52),rightShoulder=(31,55),leftElbow=(25,76),rightElbow=(43,37),leftWrist=(41,91),rightWrist=(54,26),leftHip=(57,65),rightHip=(59,68),leftKnee=(73,91),rightKnee=(76,93),leftAnkle=(86,95),rightAnkle=(88,97),leftToe=(93,95),rightToe=(94,98))
record('oldalso-alkartamasz-terden',SIDE,sideplank)
plank_knees=frame(PL,head=(95,41),neck=(85,47),leftShoulder=(73,50),rightShoulder=(75,54),leftElbow=(74,74),rightElbow=(78,77),leftWrist=(90,76),rightWrist=(93,78),leftHip=(45,52),rightHip=(47,56),leftKnee=(29,88),rightKnee=(32,90),leftAnkle=(16,94),rightAnkle=(19,96),leftToe=(9,96),rightToe=(12,98))
record('alkartamasz-terden',PL,plank_knees)

# Lying exercises: previous generic arms-up, upright or knee-to-chest frames were incorrect.
side_raised=frame(SIDE,leftKnee=(72,62),leftAnkle=(88,51),leftToe=(96,53),rightKnee=(73,85),rightAnkle=(89,88))
record('oldalfekvo-labemeles',SIDE,side_raised,ref='leg')
clam_start=frame(SIDE,leftKnee=(73,85),rightKnee=(75,88),leftAnkle=(82,92),rightAnkle=(85,94))
clam_open=frame(clam_start,leftKnee=(72,66),leftAnkle=(83,93))
record('kagylonyitas-oldalfekvesben',clam_start,clam_open,ref='clamshell')
openbook=frame(SIDE,leftElbow=(36,58),leftWrist=(39,37),head=(15,73))
record('nyitott-konyv-oldalfekvesben',SIDE,openbook)
near_knee=frame(SUP,leftKnee=(62,49),leftAnkle=(49,63),leftToe=(47,65))
record('terdhuzas-mellkashoz-hanyatt',SUP,near_knee)
hipstretch=frame(SUP,leftKnee=(67,54),leftAnkle=(74,63),leftToe=(78,66),rightKnee=(79,67),rightAnkle=(88,92))
record('farizomnyujtas-hanyatt',SUP,hipstretch)
record('fel-galambpoz-helyett-hanyatt-nyujtas',SUP,hipstretch)
record('felules-nelkuli-haspres',SUP,frame(SUP,head=(21,62),neck=(30,67),leftShoulder=(37,68),rightShoulder=(39,73),leftElbow=(48,72),rightElbow=(50,75)))
record('gerinccsavaras-hanyatt',SUP,frame(SUP,leftKnee=(57,84),rightKnee=(61,88),leftAnkle=(77,94),rightAnkle=(81,96)))
record('dead-bug-kar-lab-valtassal',SUP,frame(SUP,leftElbow=(29,54),leftWrist=(28,35),rightElbow=(49,81),leftKnee=(76,40),leftAnkle=(78,24),leftToe=(80,18),rightKnee=(83,63),rightAnkle=(91,87)))
record('sarokcsusztatas-hanyatt',SUP,frame(SUP,leftKnee=(78,82),leftAnkle=(91,90),leftToe=(97,90)))
record('medencebillentes-hanyatt',SUP,frame(SUP,leftHip=(57,72),rightHip=(61,75),leftKnee=(77,66),rightKnee=(79,69)))
record('hengeres-farizom-lazitas',SE,frame(SE,leftHip=(43,70),rightHip=(50,72),leftElbow=(35,75),leftWrist=(31,89)))
record('karok-es-labak-valtott-nyujtozasa',SUP,frame(SUP,leftElbow=(19,59),leftWrist=(10,50),leftKnee=(77,79),leftAnkle=(91,91)))
record('piheno-poz-hanyatt',SUP,frame(SUP,leftElbow=(43,80),leftWrist=(55,87)))
record('haton-fekvo-teljes-lazitas',SUP,frame(SUP,leftElbow=(43,79),leftWrist=(59,87)))

# Seated movements: standing side bends and bridges must not illustrate seated actions.
seated_lift=frame(SE,rightKnee=(70,62),rightAnkle=(77,80),rightToe=(83,84))
record('ulo-terdemeles',SE,seated_lift,ref='chair')
record('fitballon-ulo-torzsstabilitas',SE,frame(SE,head=(64,20),neck=(61,33),leftShoulder=(54,37),rightShoulder=(62,39)))
seated_side=frame(SE,leftElbow=(49,30),leftWrist=(43,15),rightElbow=(72,69),rightWrist=(79,77),head=(61,18),neck=(58,32))
record('oldalso-torzsnyujtas-ulve',SE,seated_side)
record('legzogyakorlat-ulve',SE,frame(SE,leftElbow=(56,58),leftWrist=(53,67),rightElbow=(64,58),rightWrist=(62,66)))
record('torzslazitas-fitballon',SE,frame(SE,head=(79,51),neck=(70,50),leftShoulder=(65,49),rightShoulder=(69,52),leftElbow=(74,66),rightElbow=(79,68),leftWrist=(82,77),rightWrist=(86,80)))
seated_fold=frame(SE,head=(86,64),neck=(77,57),leftShoulder=(69,55),rightShoulder=(71,59),leftElbow=(79,67),rightElbow=(82,69),leftWrist=(91,81),rightWrist=(94,84))
for id in ('hati-szakasz-nyujtasa-szeken','labdan-finom-torzsdontes','ulo-elorehajlas-jogapanttal','kimelo-elorehajlas-szeken'):
 record(id,SE,seated_fold,ref='chair')
record('szekrol-felallas',SE,ST,ref='sit')
record('bicepsz-konnyu-sullyal',ST,frame(ST,leftElbow=(36,48),rightElbow=(64,48),leftWrist=(37,32),rightWrist=(63,32)))
record('konyokhajlitas-ulve',SE,frame(SE,leftElbow=(54,53),rightElbow=(62,55),leftWrist=(52,39),rightWrist=(62,40)),ref='chair')

# Standing: avoid substituting the same arm-raise frame for unrelated balance/hip work.
tandem=frame(ST,leftKnee=(43,80),rightKnee=(58,78),leftAnkle=(36,96),rightAnkle=(68,94),leftToe=(32,98),rightToe=(75,96))
record('tandem-allas',ST,tandem,ref='balance')
record('hegytartas',ST,frame(ST,leftElbow=(32,46),rightElbow=(68,46),leftWrist=(28,60),rightWrist=(72,60)))
record('fapoz-tamasz-mellett',ST,frame(ST,rightKnee=(67,78),rightAnkle=(50,69),rightToe=(45,69)),ref='balance')
record('sulypontathelyezes-allva',ST,frame(ST,leftHip=(42,62),rightHip=(54,61),leftKnee=(43,79),rightKnee=(59,79)))
record('farizomfeszites-allva',ST,frame(ST,leftHip=(43,60),rightHip=(55,60)))
record('oldalemeles-kis-sullyal',ST,frame(ST,leftElbow=(27,34),rightElbow=(73,34),leftWrist=(15,32),rightWrist=(85,32)))
record('eloreemeles-kis-sullyal',ST,frame(ST,leftElbow=(37,36),rightElbow=(63,36),leftWrist=(39,27),rightWrist=(61,27)))
record('karok-atolelese-mellkas-elott',ST,frame(ST,leftElbow=(59,44),rightElbow=(41,45),leftWrist=(68,44),rightWrist=(32,44)))
record('teljes-testes-nyujtozas-allva',ST,frame(ST,leftElbow=(35,21),rightElbow=(65,21),leftWrist=(31,8),rightWrist=(69,8)))
record('karok-emelese-magas-tartasba',ST,frame(ST,leftElbow=(35,20),rightElbow=(65,20),leftWrist=(39,7),rightWrist=(61,7)))
record('karcsusztatas-falon',ST,frame(ST,leftElbow=(34,25),rightElbow=(66,25),leftWrist=(35,9),rightWrist=(65,9)))
record('mellkasnyitas-allva',ST,frame(ST,leftElbow=(23,34),rightElbow=(77,34),leftWrist=(13,41),rightWrist=(87,41)))
record('torolkozos-mellkasnyitas',ST,frame(ST,leftElbow=(27,35),rightElbow=(73,35),leftWrist=(15,35),rightWrist=(85,35)))
record('gumiszalag-szethuzas-mellmagassagban',ST,frame(ST,leftElbow=(25,43),rightElbow=(75,43),leftWrist=(14,43),rightWrist=(86,43)),ref='row')
record('vallkorzes-elore-hatra',ST,frame(ST,leftShoulder=(39,29),rightShoulder=(61,29),leftElbow=(34,47),rightElbow=(66,47)))
record('karkorzes-kicsiben',ST,frame(ST,leftElbow=(25,31),rightElbow=(75,31),leftWrist=(13,32),rightWrist=(87,32)))
record('oldalazo-lepesek-minibanddel',ST,frame(ST,rightHip=(60,61),rightKnee=(69,79),rightAnkle=(79,96),rightToe=(83,98)),ref='leg')
record('hatralepes-minibanddel',ST,frame(ST,rightHip=(59,60),rightKnee=(73,80),rightAnkle=(84,94),rightToe=(91,97)))
record('tricepsznyujtas-gumiszalaggal',ST,frame(ST,rightElbow=(63,16),rightWrist=(63,7),leftElbow=(37,52),leftWrist=(42,46)))
record('gumiszalagos-torzsellenallas',ST,frame(ST,leftElbow=(45,43),rightElbow=(59,43),leftWrist=(51,44),rightWrist=(57,44)))
record('bokamunka-gumiszalaggal',SE,frame(SE,rightAnkle=(80,86),rightToe=(94,84)),ref='chair')
record('teljes-testes-nyujtozas-allva',ST,frame(ST,leftElbow=(35,20),rightElbow=(65,20),leftWrist=(35,8),rightWrist=(65,8)))
record('fel-napudvozlet-modositva',ST,frame(ST,head=(76,59),neck=(68,55),leftShoulder=(62,54),rightShoulder=(67,57),leftElbow=(74,68),rightElbow=(77,71),leftWrist=(82,83),rightWrist=(86,85)))

# Upper and lower extension-specific corrections.
record('vadli-nyujtasa-falnal',ST,frame(ST,leftKnee=(30,77),leftAnkle=(20,96),leftToe=(15,98),rightKnee=(67,76),rightAnkle=(72,96),rightToe=(78,98)),ref='calf')
record('csipohajlito-nyujtas-terdelve',LUNGE,frame(LUNGE,leftKnee=(36,89),leftAnkle=(29,96),rightKnee=(70,75)))
record('allo-csipooldalemeles',ST,frame(ST,rightKnee=(75,68),rightAnkle=(89,77),rightToe=(96,80)),ref='leg')
record('gumiszalagos-csipooldalemeles',ST,frame(ST,rightKnee=(75,68),rightAnkle=(89,77),rightToe=(96,80)),ref='leg')
record('sulypontathelyezes-allva',ST,frame(ST,leftHip=(38,62),rightHip=(50,62),leftKnee=(36,79),rightKnee=(55,79)))
record('torzsdontes-csipobol',ST,frame(ST,head=(78,54),neck=(67,52),leftShoulder=(61,50),rightShoulder=(65,54),leftElbow=(70,66),rightElbow=(74,67),leftWrist=(77,77),rightWrist=(82,80)))
record('labdas-medencekorzes',SE,frame(SE,leftHip=(44,67),rightHip=(52,68)))
record('vallovi-lazitas-torolkozovel',SE,frame(SE,leftElbow=(50,51),rightElbow=(75,51),leftWrist=(47,40),rightWrist=(84,41)))
record('hengeres-farizom-lazitas',SE,frame(SE,leftElbow=(34,72),leftWrist=(26,87),rightElbow=(77,71),rightWrist=(84,86)))

# Additional exercise-family corrections, their geometry is still indicative.
record('haromszogpoz-konnyitett',ST,frame(ST,head=(74,39),neck=(65,45),leftShoulder=(57,48),rightShoulder=(62,52),leftElbow=(41,40),rightElbow=(76,48),leftWrist=(27,30),rightWrist=(92,51),leftHip=(45,62),rightHip=(57,62),leftKnee=(30,79),rightKnee=(72,79),leftAnkle=(21,96),rightAnkle=(83,96),leftToe=(16,98),rightToe=(90,98)))
record('terdelo-oldalso-nyujtozas',Q,frame(Q,head=(51,22),neck=(51,34),leftShoulder=(43,39),rightShoulder=(60,39),leftElbow=(34,25),rightElbow=(71,48),leftWrist=(25,10),rightWrist=(79,62),leftHip=(44,62),rightHip=(57,63),leftKnee=(39,83),rightKnee=(61,84),leftAnkle=(37,94),rightAnkle=(63,95),leftToe=(32,96),rightToe=(67,97)))
record('oldalra-nyujtozo-holdsarlo',ST,frame(ST,head=(58,16),neck=(55,27),leftShoulder=(43,33),rightShoulder=(63,33),leftElbow=(35,20),rightElbow=(70,24),leftWrist=(26,9),rightWrist=(80,12)))
record('hasi-legzes-hanyatt',SUP,frame(SUP,leftElbow=(42,76),rightElbow=(46,81),leftWrist=(52,68),rightWrist=(60,71)))
record('nyak-es-vallov-lazitasa-legzessel',SE,frame(SE,leftShoulder=(55,38),rightShoulder=(63,39),leftElbow=(60,55),rightElbow=(66,56)))
record('terdelo-piheno-jogablokkal',Q,child_start)
record('mellkasnyito-legzes-falnal',ST,frame(ST,leftElbow=(28,36),rightElbow=(72,36),leftWrist=(14,39),rightWrist=(86,39)))
record('szfinxpoz',SUP,frame(SUP,head=(90,37),neck=(79,52),leftShoulder=(68,63),rightShoulder=(72,66),leftElbow=(76,81),rightElbow=(79,83),leftWrist=(87,93),rightWrist=(90,95)))
record('kobra-konnyitett',SUP,frame(SUP,head=(91,33),neck=(80,47),leftShoulder=(70,61),rightShoulder=(73,65),leftElbow=(76,80),rightElbow=(79,82),leftWrist=(87,93),rightWrist=(89,95)))

# Restrained deduplication of exact initial sentence in data as well as in the UI.
removed=0
for e in ex:
 if len(e.get('steps',[]))>1 and e['steps'][0].strip().rstrip('.')==e['startingPosition'].strip().rstrip('.'):
  e['steps'].pop(0);removed+=1

# Do not erase edits performed by a user in their own JSON. This is only the bundled baseline.
data['catalogVersion']=6
# Stop exposing internal editorial phrasing in the catalog metadata itself.
data['editorialNote']='A rajzok a mozdulatok megértését segítik. Kövesd a gyakorlat leírását.'
SRC.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
report=ROOT/'docs/ABRA_FELULVIZSGALAT_v12.md'
lines=['# Alapgyakorlatok ábraellenőrzése – v0.12','',
'Az eredeti állomány 132 gyakorlata 32 általános pózsablont használt; több esetben a sablon és a leírt feladat nem illett össze.',
'Az alábbi javítások **egyedi, testpontszintű geometriai korrekciók**, nem külső képek másolatai. A teljes anatómiai hitelesítés **nem történt meg**.',
'',f'- Egyedileg javított, eltérő mozdulatot rajzoló gyakorlatok: **{len(corrected)}**.',
 f'- Még eredeti sablont használó, további szakmai és vizuális ellenőrzést igénylő gyakorlatok: **{132-len(corrected)}**.',
 f'- Eltávolított ismétlődő „kiinduló helyzet” mondatok: **{removed}**.',
 '', '## Felhasznált nyilvános mozgásreferenciák (nem másolt képek)','']
for r,url in SOURCES.items():lines.append(f'- {r}: {url}')
lines+=['','## Gyakorlatonkénti állapot','','| Gyakorlat | Ábra | Korábbi sablon |','|---|---|---|']
for e in ex:
 lines.append(f'| {e["name"]} | {"Egyedileg javított, szemléltető" if e["id"] in corrected else "Eredeti sablon, ellenőrzendő"} | {e["pose"]["templateId"] if e["id"] not in corrected else "→ egyedi póz"} |')
lines+=['','## Értelmezés','',
'A „javított” jelölés **nem** jelent orvosi vagy gyógytornászi jóváhagyást. A 16 testpontból álló, 2.5D rendszer nem képes minden esetben a gerinc ívét, finom kéz- és lábmozdulatokat vagy segédeszközöket pontosan ábrázolni.',
'A tartalmilag bizonytalan mozdulatokat és a gépies pózokat kötelező szakemberrel ellenőriztetni nyilvános kiadás előtt.']
report.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('corrected',len(corrected),'redundant steps',removed,'remaining',len(ex)-len(corrected))
