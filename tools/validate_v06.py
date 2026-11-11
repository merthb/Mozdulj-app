#!/usr/bin/env python3
import json,pathlib,collections,math,re
root=pathlib.Path(__file__).resolve().parents[1]
p=root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
b=p.read_bytes();cat=json.loads(b)
assert cat['schemaVersion']==3 and cat['catalogVersion']==4
assert len(cat['exercises'])==132
assert len(b)<1_000_000
assert b'imageBase64' not in b and b'thumbnailBase64' not in b
assert len({e['id'] for e in cat['exercises']})==132
req=['head','neck','leftShoulder','rightShoulder','leftElbow','rightElbow','leftWrist','rightWrist','leftHip','rightHip','leftKnee','rightKnee','leftAnkle','rightAnkle','leftToe','rightToe']
pairs=[('leftShoulder','leftElbow'),('rightShoulder','rightElbow'),('leftElbow','leftWrist'),('rightElbow','rightWrist'),('leftHip','leftKnee'),('rightHip','rightKnee'),('leftKnee','leftAnkle'),('rightKnee','rightAnkle')]
for e in cat['exercises']:
 assert e['category'] not in ('jóga','Jóga és teljes test')
 assert e['phase'] in ('warmup','main','cooldown')
 assert len(e['pose']['frames'])>=2
 for f in e['pose']['frames']:
  assert set(f['joints'])==set(req)
  for v in f['joints'].values():
   assert 0<=v['x']<=100 and 0<=v['y']<=100 and -20<=v['z']<=20
  for x,y in pairs:
   d=math.dist([f['joints'][x][i] for i in ['x','y','z']],[f['joints'][y][i] for i in ['x','y','z']]);assert 3<=d<=85
js=(root/'docs/ui-prototype-v06.js').read_text()
assert all(x in js for x in ['function profileEdit()',"a==='exportcatalog'","a==='importcatalog'",'function drawPoses()'])
assert 'profileDraft' in js
p=(root/'Mozdulj.Maui/Pages/ProfileEditPage.cs').read_text()
assert 'PopModalAsync' in p and 'Beállítások mentése' in p
p=(root/'Mozdulj.Maui/Pages/ProfilePage.cs').read_text()
assert 'ExportCatalogBytesAsync' in p and 'PushModalAsync' in p
p=(root/'Mozdulj.Maui/Platforms/Android/MainActivity.cs').read_text()
assert 'Intent.ActionCreateDocument' in p and 'OpenOutputStream' in p
print('PASS 132 exercises, self-contained v3, no images, 2 frames, 16 landmarks')
print('PASS phase separation + profile + Android import/export source wiring')
print('Template coverage:',len(set(e['pose']['templateId'] for e in cat['exercises'])),'templates')
