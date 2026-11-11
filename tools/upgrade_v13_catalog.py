#!/usr/bin/env python3
"""V13 catalog migration for built-ins (offline pose and equipment geometry).
The output is illustrative—not a clinical verification or automatic anatomy solver.
"""
from pathlib import Path
import re, json, copy, math
root=Path(__file__).resolve().parents[1]
path=root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
data=json.loads(path.read_text(encoding='utf8'))
src=(root/'Mozdulj.Core/PoseTemplates.cs').read_text(encoding='utf8')
names=['head','neck','leftShoulder','rightShoulder','leftElbow','rightElbow','leftWrist','rightWrist','leftHip','rightHip','leftKnee','rightKnee','leftAnkle','rightAnkle','leftToe','rightToe']
patterns={}
for key,raw in re.findall(r'"([\w-]+)"\s*=>\s*M\(\[([^\]]+)\]\)',src):
    vals=list(map(float,re.findall(r'-?\d+(?:\.\d+)?',raw)))
    assert len(vals)==48,(key,len(vals))
    patterns[key]={n:dict(x=vals[i*3],y=vals[i*3+1],z=vals[i*3+2]) for i,n in enumerate(names)}
assert len(patterns)>=10,patterns.keys()

def equipment_props(kind, frame, i):
    js=frame['joints']; a={'kind':kind,'frameIndex':i,'x':50,'y':75,'rotation':0,'scale':1}
    if kind=='kézisúlyzó':
        return [dict(a,x=js[k]['x'],y=js[k]['y'],scale=.75,anchorA=k) for k in ('leftWrist','rightWrist')]
    if kind in ('gumiszalag','jógapánt','törölköző'):
        return [dict(a,x=50,y=68,anchorA='leftWrist',anchorB='rightWrist')]
    if kind=='miniband':
        return [dict(a,x=50,y=70,scale=.8,anchorA='leftKnee',anchorB='rightKnee')]
    position={'jógamatrac':(50,92,1.15),'szék':(50,76,1),'fitball':(50,77,1),
              'SMR-henger':(50,85,.9),'jógablokk':(48,83,.7),
              'fal':(87,50,1),'lépcső':(60,87,1),'masszázslabda':(52,83,.65)}
    x,y,scale=position.get(kind,(50,75,1))
    return [dict(a,x=x,y=y,scale=scale)]

# Cross-legged only when a floor-seated base makes biomechanical sense.
base={
 'allbehuzas-ulve':'cross-legged',
 'lapockazaras-ulve':'cross-legged',
 'torzsforditas-ulve':'cross-legged',
 'pillangoules':'butterfly',
 'gumiszalagos-evezes-ulve':'long-sitting',
 'nyitott-konyv-oldalfekvesben':'side-lying',
 'kagylonyitas-oldalfekvesben':'side-lying',
 'oldalfekvo-labemeles':'side-lying',
}
chair={'terdnyujtas-ulve','combfeszites-ulve','ulo-terdemeles','bokakorzes-ulve','bokapumpa-ulve',
       'hati-szakasz-nyujtasa-szeken','oldalso-torzsnyujtas-ulve','ulo-jogacsavaras-szeken',
       'legzogyakorlat-ulve','kimelo-elorehajlas-szeken','fitballon-ulo-torzsstabilitas',
       'fitballos-terdnyujtas-ulve','konyokhajlitas-ulve'}
for id in chair:
    if id not in ('fitballon-ulo-torzsstabilitas','fitballos-terdnyujtas-ulve'):
        base[id]='chair-seated'
# Fitball sitting needs an upright trunk but a ball, not a chair; same upright geometry.
base.update({'fitballon-ulo-torzsstabilitas':'chair-seated','fitballos-terdnyujtas-ulve':'chair-seated'})

for e in data['exercises']:
    pose=e['pose']; pose.setdefault('arrows',[]);pose.setdefault('view','front');pose['props']=[]
    id=e['id']
    if id in base:
        typ=base[id]
        pose.update(templateId=typ,view='side' if typ in ('side-lying','long-sitting') else 'front',
                    source='v13-illustrative-template')
        frames=pose['frames']
        assert len(frames)>=2
        frames[0]['joints']=copy.deepcopy(patterns[typ]);frames[1]['joints']=copy.deepcopy(patterns[typ])
        if typ=='cross-legged':
            e['startingPosition']='Ülj kényelmes törökülésbe, a törzsed maradjon magas, a vállad laza.'
        elif typ=='butterfly':
            e['startingPosition']='Ülj a talajon, érintsd össze a talpakat, a térdek oldalra nyílnak. Ne erőltesd a csípőt.'
        elif typ=='long-sitting':
            e['startingPosition']='Ülj a talajon nyújtott lábbal, a hátad legyen kényelmesen hosszú; a gumiszalagot stabilan vezesd a talpak köré.'
        elif typ=='chair-seated' and 'fitball' not in id:
            e['startingPosition']='Ülj stabil, nem guruló székre, mindkét talpad támaszkodjon a talajon.'
        elif typ=='chair-seated' and 'fitball' in id:
            e['startingPosition']='Ülj stabil fitballra, talpaid csípőszélességben támaszkodjanak a talajon.'
        if typ=='side-lying':
            e['startingPosition']='Feküdj valóban az oldaladra: a vállak és a csípők egymás fölött maradjanak, a fejedet támaszd alá.'
            if id=='kagylonyitas-oldalfekvesben':
                js=frames[1]['joints'];js['rightKnee']={'x':75,'y':50,'z':8};js['rightHip']={'x':58,'y':57,'z':4}
                # heel and lower leg remain supported
            elif id=='oldalfekvo-labemeles':
                for f in frames:
                    js=f['joints'];js['leftKnee']={'x':75,'y':68,'z':-4};js['leftAnkle']={'x':89,'y':75,'z':-4};js['leftToe']={'x':97,'y':75,'z':-4}
                    js['rightKnee']={'x':76,'y':64,'z':4};js['rightAnkle']={'x':90,'y':70,'z':4};js['rightToe']={'x':98,'y':70,'z':4}
                js=frames[1]['joints'];js['rightKnee']={'x':76,'y':47,'z':6};js['rightAnkle']={'x':90,'y':36,'z':6};js['rightToe']={'x':98,'y':32,'z':6}
            elif id=='nyitott-konyv-oldalfekvesben':
                f0=frames[0]['joints'];f1=frames[1]['joints'];
                f0['rightElbow']={'x':49,'y':59,'z':4};f0['rightWrist']={'x':63,'y':60,'z':4}
                f1['rightElbow']={'x':40,'y':38,'z':11};f1['rightWrist']={'x':37,'y':23,'z':14}
        if typ=='cross-legged' and id=='torzsforditas-ulve':
            j=frames[1]['joints'];j['leftShoulder']['x']+=2;j['rightShoulder']['x']-=2
            j['leftWrist']['x']+=4;j['rightWrist']['x']-=4
        if typ=='cross-legged' and id=='allbehuzas-ulve':
            frames[1]['joints']['head']['z']=-2
        if typ=='butterfly':
            # subtle relaxation, legs remain in comfortable butterfly position
            frames[1]['joints']['leftKnee']['y']+=2
            frames[1]['joints']['rightKnee']['y']+=2
        if typ=='chair-seated' and id=='terdnyujtas-ulve':
            j=frames[1]['joints'];j['rightAnkle']={'x':86,'y':72,'z':3};j['rightToe']={'x':94,'y':72,'z':3}
    # All advertised equipment is visible, and each frame has its own props.
    for i,f in enumerate(pose['frames']):
        for equip in e.get('equipment',[]):
            pose['props'].extend(equipment_props(equip,f,i))
    # All poses remain illustrative pending a clinician's review.
    pose['reviewStatus']='illustrative'

data['catalogVersion']=7
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('v13',len(data['exercises']),'individual-templates',len(base),
      'equipment-exercises',sum(bool(e['equipment']) for e in data['exercises']),
      'props',sum(len(e['pose']['props']) for e in data['exercises']))
