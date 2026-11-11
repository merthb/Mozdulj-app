#!/usr/bin/env python3
"""v0.26 source/catalog integrity smoke checks; run: python tools/test_v26_upgrade.py."""
import json, pathlib, re, csv
ROOT=pathlib.Path(__file__).resolve().parents[1]
read=lambda p:(ROOT/p).read_text(encoding='utf-8-sig')
catalog=json.loads(read('Mozdulj.Maui/Resources/Raw/exercises.catalog.json'))
ex=catalog['exercises']; ids={e['id'] for e in ex}
assert len(ex)==294 and len(ids)==294, 'practice library changed unexpectedly'
assert catalog['catalogVersion']>=16
phase_counts={n:sum(len(e['pose']['rig3D']['frames'])==n for e in ex) for n in (1,2,3)}
assert phase_counts=={1:21,2:0,3:273},phase_counts
assert all(len(e['pose']['rig3D']['frames'])>=3 for e in ex if e['measure']=='reps'), 'repetition exercise without 3 phases'
for e in ex:
    frames=e['pose']['rig3D']['frames']
    assert all('name' in f and 'root' in f and 'hands' in f and 'feet' in f for f in frames)
    if len(frames)==3:assert len({f['name'] for f in frames})==3,(e['id'],'phase names')
pattern=re.compile(r'\b(jobbra|balra|jobb|bal)\b')
side=lambda s:('jobb' in pattern.findall(s.lower()) or 'jobbra' in pattern.findall(s.lower()),
               'bal' in pattern.findall(s.lower()) or 'balra' in pattern.findall(s.lower()))
key=lambda s:pattern.sub('{oldal}',s.lower())
pairs={}
for e in ex:
    right,left=side(e['name'])
    if not right!=left:continue
    matches=[other for other in ex if other['id']!=e['id'] and other['phase']==e['phase']
             and key(other['name'])==key(e['name']) and (side(other['name'])[0]!=side(other['name'])[1])]
    assert len(matches)==1,(e['name'],len(matches))
    assert side(matches[0]['name'])==(left,right)
    pairs[e['id']]=matches[0]['id']
assert len(pairs)==38,len(pairs)
assert 'AddHours(-2)' in read('Mozdulj.Core/WorkoutDay.cs')
assert 'FilePicker.Default.PickAsync' in read('Mozdulj.Maui/Pages/ProfileEditPage.cs')
assert 'fromCatalog:true' in read('Mozdulj.Maui/Pages/CatalogPage.cs')
assert 'SidePairing.Balanced' in read('Mozdulj.Core/WorkoutPlanEditing.cs')
assert 'DropGestureRecognizer' in read('Mozdulj.Maui/Pages/WorkoutPreviewPage.cs')
assert 'newCounterpart' in read('Mozdulj.Core/WorkoutCustomization.cs')
assert len(list(csv.DictReader((ROOT/'docs/3D_FELULVIZSGALAT_v026.csv').open(encoding='utf-8-sig'),delimiter=';')))==15
print('PASS v0.26: 294 IDs; 273 3-phase / 21 static rigs; 38 symmetric side variants;',
      '2AM boundary, native audio, catalog editing, drag and drop, side pairing checks')
