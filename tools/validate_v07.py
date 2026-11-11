#!/usr/bin/env python3
"""Offline structural validations; does NOT compile C# or prove exercise biomechanics."""
from pathlib import Path
import json, re, subprocess
import jsonschema
root=Path(__file__).resolve().parents[1]
base=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text())
schema=json.loads((root/'schemas/mozdulj-library-v1.schema.json').read_text())
sample=json.loads((root/'examples/sajat-gyakorlat-minta.json').read_text())
assert base['schemaVersion']==3
assert len(base['exercises'])==132
assert all(len(e['pose']['frames'])>=2 and all(len(f['joints'])==16 for f in e['pose']['frames']) for e in base['exercises'])
assert len({e['id'] for e in base['exercises']})==132
jsonschema.Draft202012Validator.check_schema(schema)
jsonschema.Draft202012Validator(schema).validate(sample)
invalid=json.loads(json.dumps(sample))
invalid['userExercises'][0]['exercise']['regions']=[]
assert any(jsonschema.Draft202012Validator(schema).iter_errors(invalid))
invalid=json.loads(json.dumps(sample))
invalid['userExercises'][0]['exercise']['repTargets']=[]
assert any(jsonschema.Draft202012Validator(schema).iter_errors(invalid))
html=(root/'docs/UI_v07_interaktiv.html').read_text()
assert 'Mozdulj!' in html and '__EMBEDDED_CATALOG__' not in html
assert len(json.loads(re.search(r'<script id="seed" type="application/json">(.*?)</script>',html,re.S).group(1))['exercises'])==132
result=subprocess.run(['node','--check',str(root/'tools/ui-v07.js')],capture_output=True,text=True)
assert result.returncode==0,result.stderr
for file in ['Mozdulj.Core/ExerciseLibrary.cs','Mozdulj.Maui/Pages/ExerciseEditorPage.cs',
             'Mozdulj.Maui/Pages/PoseEditorPage.cs','docs/FUNKCIONALIS_SPECIFIKACIO_v07.md',
             'docs/KEPERNYOTERV_v07.md','docs/JSON_SEMA_v4.md']:
    assert (root/file).is_file(),file
print('PASS 132 beépített gyakorlat / 16 pont, könyvtár JSON Schema / aktív minimum, embedded HTML + Node syntax, v0.7 fájlok')
