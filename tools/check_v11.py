from pathlib import Path
import json,re,subprocess,hashlib
from jsonschema import Draft202012Validator
p=Path(__file__).resolve().parents[1]
cat=json.loads((p/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text())
assert cat['schemaVersion']==3 and cat['catalogVersion']==5
assert len(cat['exercises'])==132
assert len({e['id'] for e in cat['exercises']})==132
assert all('yogaInspired' not in e and 'jógaeredetű' not in e.get('tags',[]) for e in cat['exercises'])
assert all(e.get('pose',{}).get('frames') and len(e['pose']['frames'])>=2 for e in cat['exercises'])
print('PASS: 132 gyakorlat és változatlan pózadat, jógaeredet flag nélkül')
for file in (p/'schemas').glob('*.json'):
 schema=json.loads(file.read_text());Draft202012Validator.check_schema(schema)
 print('PASS schema',file.name)
schema=json.loads((p/'schemas/mozdulj-library-v2.schema.json').read_text())
example=json.loads((p/'examples/alapgyakorlat-korrektura-v11.json').read_text())
Draft202012Validator(schema).validate(example)
print('PASS: v2 korrektúra export az új sémával kompatibilis')
# v2 schema must not allow changing protected metadata in an override.
props=set(schema['$defs']['builtInContentOverride']['properties'])
for name in ['category','name','regions','phase','measure','equipment','difficulty','targetSeconds','repTargets']:
 assert name not in props,name
print('PASS: korlátozott alapgyakorlat-korrektúra séma')
cs=(p/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text()
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in cs
assert '<ApplicationVersion>11</ApplicationVersion>' in cs
assert '<ApplicationDisplayVersion>0.11.0</ApplicationDisplayVersion>' in cs
source='\n'.join(x.read_text() for folder in ['Mozdulj.Core','Mozdulj.Maui']
     for x in (p/folder).rglob('*.cs'))
assert 'YogaInspired' not in source and 'PreferYogaInspired' not in source
for ident in ['BuiltInContentEditorPage','SaveBuiltInContentAsync','ResetBuiltInContentAsync',
              'ExportUserBackupAsync','RestoreUserBackupAsync','LocalDataMigrations.Upgrade',
              'FileSystem.Current.AppDataDirectory','BuiltInContentOverrides']:
 assert ident in source,ident
print('PASS: változatlan csomagnév, emelkedő versionCode, v11 C# vezérlők')
h=(p/'docs/UI_v11_interaktiv.html').read_text()
assert h.count('yogaInspired')==0
scripts=re.findall(r'<script(?: [^>]*)?>(.*?)</script>',h,flags=re.S)
js=scripts[-1]
tmp=p/'tools/_temp_v11_preview_check.js'
tmp.write_text(js)
result=subprocess.run(['node','--check',str(tmp)],capture_output=True,text=True)
tmp.unlink()
assert result.returncode==0,result.stderr
print('PASS: offline UI-előnézet JavaScript-szintaxis')
print('OK: v0.11 statikus és JSON séma-ellenőrzés')
