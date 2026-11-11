"""Integrate the complete 97-item yoga candidate bundle into v0.22 as editable Rig3D exercises.

Idempotent; no existing exercise or user progress is deleted. Original 3D rigs
are imported as-is unless a static/non-visible phase is collapsed to one frame.
The imported rigs are NOT clinical exercise demonstrations.
"""
from __future__ import annotations
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / 'Mozdulj.Maui/Resources/Raw/exercises.catalog.json'
SOURCES = ROOT / 'docs/joga_jeloltforras'
ADDITIONS = SOURCES / 'yoga_catalog_additions.json'
MODELS = SOURCES / 'yoga_3d_rigs.json'
REPORT = ROOT / 'docs/JOGA_97_INTEGRACIO_v22.json'


def integrate() -> dict:
    cat = json.loads(CATALOG.read_text(encoding='utf-8'))
    exercises = json.loads(ADDITIONS.read_text(encoding='utf-8'))['exercises']
    rigs = json.loads(MODELS.read_text(encoding='utf-8'))['models']
    assert len(exercises)==97 and len(rigs)==97, (len(exercises),len(rigs))
    existing_ids = {e['id'] for e in cat['exercises']}
    existing_names = {e['name'].strip().casefold() for e in cat['exercises']}
    appended=[]; already=[]; changes=[]
    for e in exercises:
        if e['id'] in existing_ids:
            already.append(e['id'])
            continue
        if e['name'].strip().casefold() in existing_names:
            raise ValueError('Azonos nevű, külön azonosítójú gyakorlat: '+e['name'])
        rig = copy.deepcopy(rigs[e['id']]); rig.pop('metadata',None)
        rig['reviewStatus']='illustrative'
        rig['editorialNote']='Jógagyakorlat-jelölt: szemléltető 3D rig, manuális anatómiai és ütközési ellenőrzés szükséges.'
        e = copy.deepcopy(e)
        # The legacy 2D pose is not rendered. Retain only the 3D model and use
        # independently named movement phases from the sourced candidate.
        e['pose']={
            'schemaVersion':1,'templateId':rig['preset'],
            'reviewStatus':'illustrative','source':'joga-jeloltcsomag-v022',
            'frames':[],'arrows':[],'props':[], 'view':'front','rig3D':rig,
        }
        e['reviewStatus']='draft'
        # Older model-backed candidate ids are stable and namespaced so that
        # a user library import can recognize that ID as already installed.
        cat['exercises'].append(e)
        appended.append(e['id']); existing_ids.add(e['id']); existing_names.add(e['name'].strip().casefold())
        changes.append({'id':e['id'],'name':e['name'],'preset':rig['preset'],'frames':len(rig['frames']),
                        'reviewStatus':'illustrative'})
    initial=len(cat['exercises'])-len(appended)
    if appended:
        cat['catalogVersion']=max(cat['catalogVersion']+1,13)
        cat['editorialNote']=(cat.get('editorialNote') or '')+' | A v0.22-es jóga-jelöltcsomag 97 egyedileg szerkeszthető, felülvizsgálandó 3D gyakorlatot ad hozzá.'
        CATALOG.write_text(json.dumps(cat,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    report={'initialExercises':initial,'candidateExercises':len(exercises),'inserted':len(appended),
            'alreadyInstalled':len(already),'resultExercises':len(cat['exercises']),
            'rigModelsAdded':len(appended),'framesAdded':sum(x['frames'] for x in changes),
            'notes':'A 97 jelöltet megtartottuk, még akkor is, ha egy meglévő ászana változatára hasonlít. A 26 meglévő elem felülvizsgálati javaslata külön melléklet, nem automatikus felülírás.',
            'status':'illustrative-unreviewed', 'items':changes}
    REPORT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return report

if __name__=='__main__':
    s=integrate();print(json.dumps({k:v for k,v in s.items() if k!='items'},ensure_ascii=False,indent=2))
