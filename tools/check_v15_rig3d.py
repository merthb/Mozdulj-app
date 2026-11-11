import json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[1]
path=root/"Mozdulj.Maui/Resources/Raw/exercises.catalog.json"
obj=json.loads(path.read_text(encoding="utf-8"))
ex=obj.get("exercises",[])
missing=[]
for e in ex:
    rig=((e.get("pose") or {}).get("rig3D"))
    if not rig or not rig.get("frames"):
        missing.append(e.get("id"))
print(f"catalogVersion={obj.get('catalogVersion')} exercises={len(ex)} rig3D={len(ex)-len(missing)}")
if missing:
    print('HIANYZO RIG3D:', ', '.join(missing))
    sys.exit(1)
print('Minden gyakorlat rendelkezik Rig3D adattal.')
