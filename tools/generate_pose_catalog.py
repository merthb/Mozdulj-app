#!/usr/bin/env python3
"""v2 image catalog -> v3 self-contained landmark catalog.
Landmarks are PRELIMINARY motion templates, not clinically reviewed posture data.
A new imported exercise can provide exact pose.frames in the same single JSON file.
"""
import copy, json, pathlib, re, sys
root=pathlib.Path(__file__).resolve().parents[1]
src=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else root/'catalog-v05-image-backup.json'
if not src.exists():
    raise SystemExit('Legacy v2 katalógus nem található. Használat: python tools/generate_pose_catalog.py <régi-katalógus.json>')
dst=root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
a=json.loads(src.read_text(encoding='utf-8'))
J=['head','neck','leftShoulder','rightShoulder','leftElbow','rightElbow','leftWrist','rightWrist','leftHip','rightHip','leftKnee','rightKnee','leftAnkle','rightAnkle','leftToe','rightToe']
# two-dimensional canvas reference positions plus a Z offset for layer ordering and soft perspective
# The reference figure is gender-neutral by data, the renderer applies the clothing palette.
def frame(head,neck,ls,rs,le,re,lw,rw,lh,rh,lk,rk,la,ra,lt,rt):
    return dict(zip(J,[head,neck,ls,rs,le,re,lw,rw,lh,rh,lk,rk,la,ra,lt,rt]))
def C(x,y,z=0): return [round(x,2),round(y,2),round(z,2)]
S=frame(C(50,13),C(50,27),C(40,32,-3),C(60,32,3),C(35,50,-4),C(65,50,4),C(34,70,-4),C(66,70,4),C(44,61,-3),C(56,61,3),C(44,79,-3),C(56,79,3),C(44,96,-3),C(56,96,3),C(42,98,-3),C(60,98,3))
SE=frame(C(68,20),C(63,32),C(56,36,-2),C(63,39,3),C(62,54,-2),C(67,57,3),C(65,70,-2),C(70,71,3),C(49,65,-3),C(56,66,3),C(52,84,-3),C(59,83,3),C(67,96,-3),C(74,96,3),C(75,98,-3),C(82,98,3))
Q=frame(C(83,42),C(73,49),C(64,50,-3),C(67,56,3),C(63,66,-3),C(68,67,3),C(66,91,-3),C(71,92,3),C(28,55,-3),C(34,59,3),C(24,76,-3),C(35,79,3),C(30,91,-3),C(43,93,3),C(19,93,-3),C(32,95,3))
SUP=frame(C(13,77),C(23,76),C(30,75,-2),C(31,80,2),C(41,79,-2),C(44,83,2),C(54,84,-2),C(55,87,2),C(57,75,-3),C(61,79,3),C(77,65,-3),C(79,69,3),C(85,91,-3),C(90,91,3),C(92,95,-3),C(97,95,3))
PL=frame(C(91,37),C(80,44),C(70,50,-2),C(70,56,2),C(74,69,-2),C(76,73,2),C(78,91,-2),C(81,92,2),C(41,58,-3),C(43,62,3),C(28,77,-3),C(32,81,3),C(11,91,-3),C(17,92,3),C(7,93,-3),C(13,95,3))
SIDE=frame(C(16,76),C(25,76),C(33,75,-2),C(34,79,2),C(44,81,-2),C(46,85,2),C(51,85,-2),C(52,88,2),C(57,78,-3),C(59,81,3),C(72,81,-3),C(73,84,3),C(88,88,-3),C(88,91,3),C(94,90,-3),C(95,93,3))
LUNGE=frame(C(55,12),C(55,27),C(47,33,-3),C(63,33,3),C(43,54,-3),C(67,54,3),C(45,70,-3),C(67,70,3),C(50,61,-3),C(60,61,3),C(37,75,-3),C(74,78,3),C(27,96,-3),C(86,96,3),C(23,98,-3),C(91,98,3))
SQUAT=frame(C(50,15),C(50,30),C(40,36,-3),C(60,36,3),C(36,52,-3),C(64,52,3),C(51,50,-3),C(49,50,3),C(43,66,-3),C(57,66,3),C(34,75,-3),C(66,75,3),C(35,96,-3),C(65,96,3),C(32,98,-3),C(70,98,3))
BAL=copy.deepcopy(S)
BAND=copy.deepcopy(SE)
STRETCH=copy.deepcopy(S)
RELAX=copy.deepcopy(SUP)

def patch(base, pairs):
    out=copy.deepcopy(base)
    for joint,xyz in pairs.items():out[joint]=C(*xyz)
    return out

def variants(template):
    if template=='standing-shoulders':
        return S,patch(S,{'leftElbow':(28,31,-3),'rightElbow':(72,31,3),'leftWrist':(23,15,-3),'rightWrist':(77,15,3)})
    if template=='neck-turn':return S,patch(S,{'head':(58,16,2),'neck':(50,27,0)})
    if template=='neck-tilt':return S,patch(S,{'head':(42,19,0),'neck':(50,27,0)})
    if template=='shoulder-shrug':return S,patch(S,{'leftShoulder':(40,26,-3),'rightShoulder':(60,26,3),'leftElbow':(34,45,-4),'rightElbow':(66,45,4)})
    if template=='row':return SE,patch(SE,{'leftElbow':(46,52,-2),'rightElbow':(53,54,3),'leftWrist':(43,57,-2),'rightWrist':(50,59,3)})
    if template=='seated-twist':return SE,patch(SE,{'leftWrist':(74,55,-2),'rightWrist':(52,58,3),'head':(76,22,0)})
    if template=='cat-cow':return Q,patch(Q,{'head':(86,34,0),'neck':(73,43,0),'leftShoulder':(62,46,-3),'rightShoulder':(66,52,3),'leftHip':(28,63,-3),'rightHip':(34,67,3)})
    if template=='thread-needle':return Q,patch(Q,{'head':(55,85,0),'neck':(63,75,0),'leftElbow':(47,73,-3),'leftWrist':(31,79,-3),'rightElbow':(68,65,3),'rightWrist':(70,90,3)})
    if template=='bridge':return SUP,patch(SUP,{'leftHip':(56,49,-3),'rightHip':(62,52,3),'leftKnee':(77,65,-3),'rightKnee':(79,69,3)})
    if template=='supine-knee':return SUP,patch(SUP,{'leftKnee':(66,54,-3),'leftAnkle':(54,65,-3),'leftToe':(52,68,-3)})
    if template=='plank':return PL,patch(PL,{'leftKnee':(27,69,-3),'leftAnkle':(18,88,-3),'leftToe':(12,91,-3)})
    if template=='side-stretch':return S,patch(S,{'head':(56,14,0),'neck':(54,27,0),'leftWrist':(25,55,-3),'rightElbow':(61,21,3),'rightWrist':(46,10,3)})
    if template=='balance':return BAL,patch(BAL,{'rightKnee':(70,75,3),'rightAnkle':(58,66,3),'rightToe':(54,68,3)})
    if template=='lunge':return S,LUNGE
    if template=='squat':return S,SQUAT
    if template=='side-leg':return SIDE,patch(SIDE,{'leftKnee':(75,66,-3),'leftAnkle':(90,63,-3),'leftToe':(97,66,-3)})
    if template=='breathing':return RELAX,patch(RELAX,{'leftWrist':(50,64,-2),'rightWrist':(54,66,2)})
    if template=='arms-open':return SE,patch(SE,{'leftElbow':(35,41,-2),'rightElbow':(83,44,3),'leftWrist':(20,36,-2),'rightWrist':(98,43,3)})
    if template=='leg-raise':return SUP,patch(SUP,{'leftKnee':(72,40,-3),'leftAnkle':(72,17,-3),'leftToe':(74,11,-3)})
    if template=='chin-tuck':return SE,patch(SE,{'head':(63,23,0),'neck':(63,32,0)})
    if template=='shoulder-rotation':return S,patch(S,{'leftElbow':(40,51,-3),'leftWrist':(54,48,-3),'rightElbow':(60,51,3),'rightWrist':(46,49,3)})
    if template=='hip-circle':return S,patch(S,{'leftHip':(39,63,-3),'rightHip':(53,63,3)})
    if template=='hip-back':return S,patch(S,{'rightKnee':(74,77,3),'rightAnkle':(88,90,3),'rightToe':(96,92,3)})
    if template=='hip-side':return S,patch(S,{'rightKnee':(76,71,3),'rightAnkle':(89,78,3),'rightToe':(96,80,3)})
    if template=='knee-extension':return SE,patch(SE,{'rightKnee':(79,74,3),'rightAnkle':(96,74,3),'rightToe':(99,75,3)})
    if template=='calf-raise':return S,patch(S,{'leftAnkle':(44,88,-3),'rightAnkle':(56,88,3),'leftToe':(42,97,-3),'rightToe':(60,97,3)})
    if template=='march':return S,patch(S,{'rightKnee':(64,66,3),'rightAnkle':(56,79,3),'rightToe':(53,83,3)})
    if template=='ankle-mobility':return SE,patch(SE,{'rightAnkle':(78,91,3),'rightToe':(93,94,3)})
    if template=='wrist-mobility':return S,patch(S,{'leftWrist':(46,48,-3),'rightWrist':(55,48,3),'leftElbow':(38,52,-3),'rightElbow':(63,52,3)})
    if template=='forward-fold':return S,patch(S,{'head':(79,71,0),'neck':(70,65,0),'leftShoulder':(60,65,-3),'rightShoulder':(70,68,3),'leftElbow':(70,75,-3),'rightElbow':(77,77,3),'leftWrist':(75,88,-3),'rightWrist':(83,90,3)})
    if template=='hip-kneeling':return Q,LUNGE
    if template=='sphinx':return SUP,patch(SUP,{'head':(86,46,0),'neck':(76,60,0),'leftShoulder':(64,71,-2),'rightShoulder':(67,76,2),'leftElbow':(72,87,-2),'rightElbow':(75,89,2),'leftWrist':(83,92,-2),'rightWrist':(87,93,2)})
    if template=='butterfly':return SE,patch(SE,{'leftKnee':(32,80,-3),'rightKnee':(83,82,3),'leftAnkle':(55,87,-3),'rightAnkle':(56,89,3),'leftToe':(58,89,-3),'rightToe':(62,90,3)})
    if template=='hamstring':return SE,patch(SE,{'leftKnee':(71,78,-3),'rightKnee':(76,82,3),'leftAnkle':(94,85,-3),'rightAnkle':(97,88,3),'head':(79,41,0),'neck':(71,49,0)})
    if template=='roll':return Q,patch(Q,{'leftHip':(25,59,-3),'rightHip':(32,63,3),'head':(74,44,0)})
    return S,patch(S,{'leftWrist':(27,62,-4),'rightWrist':(74,62,4)})

def choose(e):
    s=(e['name']+' '+e['startingPosition']+' '+' '.join(e.get('tags',[]))).lower()
    contains=lambda *args:any(x in s for x in args)
    if contains('állbehúz'):return 'chin-tuck'
    if contains('vállkülső rotáció'):return 'shoulder-rotation'
    if contains('csípőkörzés','medencekörzés'):return 'hip-circle'
    if contains('hátrafeszítés','csípőhátra'):return 'hip-back'
    if contains('oldalemelés') and ('csípő' in e['regions'] or 'láb' in e['regions']):return 'hip-side'
    if contains('térdnyújtás','combfeszítés ülve'):return 'knee-extension'
    if contains('sarokemelés','lábujjemelés','sarok-lábujj'):return 'calf-raise'
    if contains('lépés helyben','járás helyben','térdfelhúzás állva','térdhúzás váltva','felváltva térd','fellépés','hátralépés','oldalra lépés','sétálás'):return 'march'
    if contains('bokakörzés','bokapumpa','lábujjak','talpi törölköző','puha labda','lábfej'):return 'ankle-mobility'
    if contains('csukló','ujjak','bicepsz','könyökhajlít','törölköző enyhe szorítás'):return 'wrist-mobility'
    if contains('előrehajlás','háti szakasz nyújtása','törzsdöntés'):return 'forward-fold'
    if contains('csípőhajlító nyújtás térdelve'):return 'hip-kneeling'
    if contains('szfinx','kobra'):return 'sphinx'
    if contains('pillangóülés'):return 'butterfly'
    if contains('combhajlító nyújtás','jógapánttal','vádli nyújtása'):return 'hamstring'
    if contains('macska','tehén'):return 'cat-cow'
    if contains('négykézláb','négykézlá', 'bird-dog','madár','tűbefűzés'):return 'thread-needle' if contains('tűbefűz','fonal','rotáció') else 'cat-cow'
    if contains('híd','csípőemelés','medenceemelés'):return 'bridge'
    if contains('plank','fekvőtámasz'):return 'plank'
    if contains('nyakfordít','fejfordít'):return 'neck-turn'
    if contains('nyakdönt','oldalra döntés'):return 'neck-tilt'
    if contains('vállfelhúz'):return 'shoulder-shrug'
    if contains('guggolás','székről felállás','leülés'):return 'squat'
    if contains('kitörés','harcos','lépés előre','lépés hátra'):return 'lunge'
    if contains('evez','lapockazárás'):return 'row'
    if contains('törzsfordít','törzscsavar','csavarás'):return 'seated-twist'
    if contains('oldalsó','oldalra nyújt','oldalsó törzs'):return 'side-stretch' if e['category']=='nyújtás' else 'side-leg'
    if contains('egyensúly','egy lábon','egylábas','flamingó','állás egy lábon'):return 'balance'
    if contains('térdhúzás hanyatt','hanyatt térd','térd mellkashoz'):return 'supine-knee'
    if contains('lábemel','nyújtott láb'):return 'leg-raise'
    if contains('légzés','belégzés','kilégzés','relax','feszültség'):return 'breathing'
    if contains('hengerez','roller','masszíroz'):return 'roll'
    if contains('gumiszalag','miniband','kötél'):return 'row' if e['category']=='erősítés' else 'arms-open'
    if contains('mellkasnyit','karcsúsztat','karok emelése','karkörzés','vállkörzés'):return 'arms-open'
    if contains('nyújt','fokozatos megnyúl'):return 'side-stretch' if 'törzs' in e['regions'] else 'standing-shoulders'
    if contains('ülve','szék','ülés'):return 'seated-twist' if 'törzs' in e['regions'] or 'hát' in e['regions'] else 'row'
    if contains('hanyatt','háton','fekvésben'):return 'supine-knee'
    if contains('oldalfekvés'):return 'side-leg'
    if contains('emel','kar','váll'):return 'standing-shoulders'
    if e['category'] in ('relaxáció','légzőgyakorlat'):return 'breathing'
    if e['category']=='egyensúly':return 'balance'
    if e['category']=='erősítés':return 'squat' if any(x in e['regions'] for x in ('comb','láb','térd')) else 'plank'
    if e['category']=='nyújtás':return 'side-stretch'
    return 'standing-shoulders'

for e in a['exercises']:
    template=choose(e);start,end=variants(template)
    e.pop('illustration',None)
    e['pose']={'schemaVersion':1,'templateId':template,'reviewStatus':'illustrative',
               'source':'automatikus pózsablon; nem szakmailag validált',
               'frames':[{'label':'Kiinduló helyzet','joints':{k:{'x':v[0],'y':v[1],'z':v[2]} for k,v in start.items()}},
                         {'label':'Mozdulat szemléltetése','joints':{k:{'x':v[0],'y':v[1],'z':v[2]} for k,v in end.items()}}]}
a['schemaVersion']=3
a['catalogVersion']=4
a['editorialNote']='A pózok eljárással rajzolt, 2.5D árnyalt szemléltető sablonok, NEM anatómiailag hitelesített illusztrációk. Klinikai ellenőrzés szükséges.'
dst.write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
print('catalog saved:',dst,'bytes',dst.stat().st_size, 'exercises',len(a['exercises']))
from collections import Counter
print(Counter(e['pose']['templateId'] for e in a['exercises']))
