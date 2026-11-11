"""Lightweight offline source/data check; does not substitute a native .NET build."""
from pathlib import Path
import json, re, subprocess, sys
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
ex=cat['exercises']
assert cat['schemaVersion']==3 and cat['catalogVersion']==11
assert len(ex)==158
ids=[e['id'] for e in ex]; assert len(ids)==len(set(ids))
assert len([x for x in ex if x['id'] in ('achilles-vadli-nyujtott','achilles-soleus-hajlitott')])==2
assert len([x for x in ex if x['id'] in set(ids[132:])])==26
assert all(x['category'] in cat['categories'] and 'jóga' not in x['category'].lower() for x in ex)
for e in ex:
    assert e['phase'] in ('warmup','main','cooldown')
    assert e['regions'] and len(e['steps'])>=3 and e['safetyNote']
    assert e['pose'].get('rig3D') and len(e['pose']['rig3D']['frames'])>=1
    assert e['measure'] in ('time','reps')
csproj=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text(encoding='utf-8')
assert '<ApplicationVersion>19</ApplicationVersion>' in csproj
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in csproj
rootTabs=(root/'Mozdulj.Maui/Pages/RootTabs.cs').read_text(encoding='utf-8')
assert 'Title = "Napló"' in rootTabs and 'Title = "Célok"' in rootTabs
core=(root/'Mozdulj.Core/DataMigration.cs').read_text(encoding='utf-8')
app=(root/'Mozdulj.Maui/Services/AppState.cs').read_text(encoding='utf-8')
for x in ('HMACSHA256.HashData','CryptographicOperations.FixedTimeEquals','BackupIntegrity.Verify'):
    assert x in core
assert 'SecureStorage.Default' in app
assert 'LocalBackupCodec.Import(' in app and 'GetBackupKeyAsync()' in app
engine=(root/'Mozdulj.Maui/Resources/Raw/rig3d/engine.js').read_text(encoding='utf-8')
assert 'footPitch' in engine and 'legAngles' in engine and 'const toeDirection=' in engine
for script in ['test_v19_rig.js','test_v19_catalog3d.js']:
    subprocess.run(['node',str(root/'tools'/script)],check=True)
print('PASS: 158 gyakorlat, 26 új elem, kategóriák, teljes Rig3D lefedettség, új csontlánc, importhitelesítés szerkezete, appverzió')
