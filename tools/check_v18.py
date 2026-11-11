from pathlib import Path
import json, re, subprocess
root=Path(__file__).resolve().parents[1]
catalog=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
assert len(catalog['exercises'])==132
assert catalog['catalogVersion']==10
assert all(e['pose'].get('rig3D') for e in catalog['exercises'])
exercise=next(e for e in catalog['exercises'] if e['id']=='medencebillentes-hanyatt')
rig=exercise['pose']['rig3D']; f0,f1=rig['frames']
for k in ('root','euler','hands','feet','poles','headYaw','headPitch','headRoll','spineBend','upperYaw'):
    assert f0[k]==f1[k], k
assert f0['pelvicTilt']==0 and .1<f1['pelvicTilt']<=.35
src=(root/'Mozdulj.Maui/Services/AppState.cs').read_text(encoding='utf-8')
assert 'if(Data.OnboardingCompleted)' in src
assert 'private void RequireSetup()' in src
assert src.count('RequireSetup();')>=2
assert src.index('RequireSetup();') < src.index('MonthlyChallengePlanner.EnsureMonth(Data,Catalog,today)')
assert src.index('Data.OnboardingCompleted = true;') < src.index('await SaveAsync();',src.index('Data.OnboardingCompleted = true;'))
assert 'new OnboardingPage(_state)' in (root/'Mozdulj.Maui/Pages/StartupPage.cs').read_text(encoding='utf-8')
assert (root/'Mozdulj.Maui/Pages/OnboardingPage.cs').is_file()
migr=(root/'Mozdulj.Core/DataMigration.cs').read_text(encoding='utf-8')
assert 'CurrentVersion = 5' in migr and 'data.SchemaVersion < 5' in migr and 'data.OnboardingCompleted = true' in migr
for name in ('engine.js','studio.js'):
    subprocess.run(['node','--check',str(root/'Mozdulj.Maui/Resources/Raw/rig3d'/name)],check=True)
subprocess.run(['node',str(root/'tools/test_v18_pelvis.js')],check=True)
subprocess.run(['node',str(root/'tools/test_rig3d.js')],check=True)
print('PASS: katalogus 132/132, medencebillentés, első indítás védelme, migrációs szerkezet, WebGL JS szintaxis és IK tesztek')
