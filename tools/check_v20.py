"""Offline source/catalog structure check; native Android build is separate."""
from pathlib import Path
import json, subprocess
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
assert cat['schemaVersion']==3 and cat['catalogVersion']==11
es=cat['exercises']
assert len(es)==158 and len({e['id'] for e in es})==158
assert all(len(e['pose']['rig3D']['frames'])>=1 for e in es)
assert all(e['category'] in cat['categories'] for e in es)
project=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text(encoding='utf-8')
assert '<ApplicationVersion>20</ApplicationVersion>' in project
assert '<ApplicationDisplayVersion>0.20.0</ApplicationDisplayVersion>' in project
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in project
engine=(root/'Mozdulj.Maui/Resources/Raw/rig3d/engine.js').read_text(encoding='utf-8')
editor=(root/'Mozdulj.Maui/Resources/Raw/rig3d/studio.js').read_text(encoding='utf-8')
ui=(root/'Mozdulj.Maui/Resources/Raw/rig3d/index.html').read_text(encoding='utf-8')
for key in ('const Collision3D=', 'function transition(', 'function bodyProbes(', 'function solids(', 'armAngles', 'footPitch', 'bandPath(', 'BAND_ANCHORS'):
    assert key in engine,key
for key in ('guardedEdit(', 'bandAnchorA', 'bandAnchorB', 'fixedAnchorA', 'freePlacement(', 'moveProp'):
    assert key in editor,key
for key in ('armFlex', 'elbowFlex', 'footPitch', 'bandRoute', 'fixedAnchorA','propSelect'):
    assert key in ui,key
for name in ('engine.js','studio.js'):
    subprocess.run(['node','--check',str(root/'Mozdulj.Maui/Resources/Raw/rig3d'/name)],check=True)
for name in ('test_v20_collision.js','test_v19_rig.js','test_v18_pelvis.js','test_v19_catalog3d.js'):
    subprocess.run(['node',str(root/'tools'/name)],check=True)
print('PASS v0.20: 158 gyakorlat, ütközés és kinematika, 3D szerkesztő, 0.20.0 / versionCode 20')
