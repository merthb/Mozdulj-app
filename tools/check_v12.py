#!/usr/bin/env python3
"""Offline structural/regression checks. No SDK dependency; not a native MAUI build."""
import json,re,math,sys,collections, pathlib
root=pathlib.Path(__file__).resolve().parents[1]
path=root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
a=json.loads(path.read_text(encoding='utf8'))
assert a['catalogVersion']==6 and len(a['exercises'])==132
joints={'head','neck','leftShoulder','rightShoulder','leftElbow','rightElbow','leftWrist','rightWrist','leftHip','rightHip','leftKnee','rightKnee','leftAnkle','rightAnkle','leftToe','rightToe'}
segments=[('leftShoulder','leftElbow'),('rightShoulder','rightElbow'),('leftElbow','leftWrist'),('rightElbow','rightWrist'),('leftHip','leftKnee'),('rightHip','rightKnee'),('leftKnee','leftAnkle'),('rightKnee','rightAnkle')]
for e in a['exercises']:
 assert e['steps'] and e['startingPosition'] and e['name']
 assert e['steps'][0].strip().rstrip('.') != e['startingPosition'].strip().rstrip('.'),e['name']
 p=e['pose'];assert len(p['frames'])>=2
 for f in p['frames']:
  j=f['joints'];assert set(j)==joints,e['name']
  for pos in j.values():
   assert 0<=pos['x']<=100 and 0<=pos['y']<=100 and -20<=pos['z']<=20,e['name']
  for p1,p2 in segments:
   dist=math.sqrt(sum((j[p1][v]-j[p2][v])**2 for v in 'xyz'))
   assert 3<=dist<=85,(e['name'],p1,p2,dist)
 assert p['reviewStatus']=='illustrative' # no false claim of expert sign-off
assert sum(e['pose']['templateId'].startswith('egyedi-') for e in a['exercises'])==78
project=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text()
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in project
assert '<ApplicationVersion>12</ApplicationVersion>' in project
assert '<AndroidPackageFormats Condition=' in project and '>apk<' in project
catalog=(root/'Mozdulj.Maui/Pages/CatalogPage.cs').read_text()
assert 'Alap' in catalog and 'Saját' in catalog and 'További szűrők' in catalog
statistics=(root/'Mozdulj.Maui/Pages/StatisticsPage.cs').read_text()
assert all(x in statistics for x in ('CompletedExercises','CompletedWorkouts','ActiveSeconds','Xp'))
assert 'DeduplicateStartingPosition' in (root/'Mozdulj.Maui/Pages/ExerciseDetailPage.cs').read_text()
assert 'XP-t nem kell elkölteni' not in (root/'Mozdulj.Maui/Pages/ChallengesPage.cs').read_text()
print('OK: 132 gyakorlat, 78 javított póz, 16 testpont/fázis, nincsenek duplikált kezdeti lépések; Android release és UI kódszerkezet.')
