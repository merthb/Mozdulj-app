"""Lightweight source/package integrity checks; not a substitute for a .NET build."""
from pathlib import Path
import json, re, hashlib
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
assert cat['catalogVersion']==9 and len(cat['exercises'])==132
ids=set()
dynamic=0;static=0
supported={'mat','band','dumbbell','chair','ball','roller','wall','towel','step','block','strap'}
for ex in cat['exercises']:
    assert ex['id'] not in ids, ex['id']; ids.add(ex['id'])
    pose=ex['pose'];assert not any(k in pose for k in ('frames','props','arrows'))
    rig=pose['rig3D'];fs=rig['frames'];assert 1<=len(fs)<=6
    assert rig['reviewStatus']=='illustrative'
    dynamic+=(len(fs)>1)
    static+=(len(fs)==1)
    for f in fs:
        assert len(f['root'])==3 and len(f['euler'])==3
        assert set(f['hands'])=={'left','right'} and set(f['feet'])=={'left','right'}
        assert all(x['type'] in supported for x in f['equipment'])
assert dynamic==105 and static==27, (dynamic,static)
project=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text(encoding='utf-8')
assert '<ApplicationDisplayVersion>0.16.0</ApplicationDisplayVersion>' in project
assert '<ApplicationVersion>16</ApplicationVersion>' in project
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in project
pages='\n'.join(p.read_text(encoding='utf-8') for p in (root/'Mozdulj.Maui/Pages').glob('*.cs'))
assert 'UI.PoseIllustration' not in pages
assert 'PoseEditorPage' not in pages
assert 'class Rig3DEditorPage' in pages
model=(root/'Mozdulj.Core/Rig3D.cs').read_text(encoding='utf-8')
assert model.count('public float HeadRoll { get; set; }')==1
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
for f in ['engine.js','studio.js','studio-v16.css','index.html']:assert (assets/f).exists()
assert '<link rel="stylesheet" href="studio-v16.css">' in (assets/'index.html').read_text(encoding='utf-8')
print(f'PASS: {len(ids)} rig3D models; {dynamic} dynamic, {static} static; no legacy 2D UI; version 16; assets present')
