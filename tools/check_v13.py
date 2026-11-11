#!/usr/bin/env python3
"""Offline structural checks. They do NOT replace a .NET MAUI build or a clinical review."""
from pathlib import Path
import json, math, re, collections, hashlib
ROOT=Path(__file__).resolve().parents[1]
j=json.loads((ROOT/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
assert j['schemaVersion']==3 and j['catalogVersion']==7
ex=j['exercises'];assert len(ex)==132
assert len(set(e['id'] for e in ex))==132
assert len([e for e in ex if e['equipment']])==90
J={'head','neck','leftShoulder','rightShoulder','leftElbow','rightElbow','leftWrist','rightWrist','leftHip','rightHip','leftKnee','rightKnee','leftAnkle','rightAnkle','leftToe','rightToe'}
segments=[('leftShoulder','leftElbow'),('rightShoulder','rightElbow'),('leftElbow','leftWrist'),('rightElbow','rightWrist'),('leftHip','leftKnee'),('rightHip','rightKnee'),('leftKnee','leftAnkle'),('rightKnee','rightAnkle')]
supported={'jógamatrac','szék','gumiszalag','kézisúlyzó','fitball','miniband','SMR-henger','jógablokk','fal','törölköző','lépcső','masszázslabda','jógapánt'}
props=0; templates=collections.Counter()
for e in ex:
 assert e['name'] and e['steps'] and e['regions'] and e['startingPosition']
 assert e['pose']['reviewStatus']=='illustrative'
 p=e['pose'];frames=p['frames'];assert 2<=len(frames)<=6
 templates[p.get('templateId','')]+=1
 for f in frames:
  joints=f['joints'];assert set(joints)==J,(e['id'],'joints')
  for q in joints.values():
   assert all(math.isfinite(q[k]) for k in 'xyz')
   assert 0<=q['x']<=100 and 0<=q['y']<=100 and -20<=q['z']<=20
  for a,b in segments:
   d=math.dist([joints[a][k] for k in 'xyz'],[joints[b][k] for k in 'xyz'])
   assert 3<=d<=85,(e['id'],a,b,d)
 for obj in p.get('props',[]):
  props+=1
  assert obj['kind'] in supported
  assert 0<=obj['frameIndex']<len(frames)
  assert 0<=obj['x']<=100 and 0<=obj['y']<=100 and .5<=obj['scale']<=1.6
  assert -180<=obj['rotation']<=180
  assert all(obj.get(a) in (None,*J) for a in ('anchorA','anchorB'))
 for item in e['equipment']:
  assert any(p0['kind']==item for p0 in p['props']),(e['id'],item)
  assert all(any(p0['kind']==item and p0['frameIndex']==i for p0 in p['props'])
      for i in range(len(frames))),(e['id'],item,'missing frame')
assert props==210,props
assert templates['side-lying']==3 and templates['cross-legged']==3 and templates['chair-seated']>=10,templates
for eid in ('nyitott-konyv-oldalfekvesben','kagylonyitas-oldalfekvesben','oldalfekvo-labemeles'):
 a=next(e for e in ex if e['id']==eid);j0=a['pose']['frames'][0]['joints'];
 assert a['pose']['view']=='side' and abs(j0['rightHip']['x']-j0['leftHip']['x'])<4
 assert abs(j0['rightShoulder']['x']-j0['leftShoulder']['x'])<4
for eid in ('allbehuzas-ulve','lapockazaras-ulve','torzsforditas-ulve'):
 a=next(e for e in ex if e['id']==eid)
 assert a['pose']['templateId']=='cross-legged'
 assert a['pose']['frames'][0]['joints']['leftKnee']['x']<a['pose']['frames'][0]['joints']['leftHip']['x']
for eid in ('terdnyujtas-ulve','ulo-terdemeles'):
 assert next(e for e in ex if e['id']==eid)['pose']['templateId']=='chair-seated'
code=(ROOT/'Mozdulj.Maui/Pages/PoseEditorPage.cs').read_text()
assert all(t in code for t in ['PoseTemplates.Items','PoseEquipmentTypes.Supported','PoseConstraints.TryMove','_pose.Props','LimitProp','Sablon alkalmazása'])
project=(ROOT/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text()
assert '<ApplicationVersion>13</ApplicationVersion>' in project
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in project
assert '<AndroidPackageFormats Condition=' in project
assert (ROOT/'Mozdulj.Maui/Illustration/PoseEquipmentDrawable.cs').exists()
print('PASS: 132 gyakorlat, 90 eszközös, 210 tárgy / fázis, sablonok, JSON, szerkesztők, APK-beállítások.')
