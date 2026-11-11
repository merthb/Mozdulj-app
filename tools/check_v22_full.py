"""Offline structural regression suite for combined v0.22.0 source release.
Checks imported exercises, 3D rigs, animation phases, review assets and UI
source changes. Run Node geometric test separately for bone lengths and collisions.
"""
from __future__ import annotations
import json,re,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf8'))
entries=cat['exercises'];ids=[e['id'] for e in entries]
assert cat['schemaVersion']==3 and cat['catalogVersion']>=13
assert len(entries)==294,(len(entries),'expected 158+39+97')
assert len(ids)==len(set(ids))
assert all(e['pose'].get('rig3D') for e in entries)
assert all(e['category'] in cat['categories'] and e['phase'] in ('warmup','main','cooldown') and
           len(e['steps'])>=3 and e['safetyNote'] and len(e['regions'])>0 for e in entries)
added=[e for e in entries if e['id'].startswith('user-yoga-')]
assert len(added)==97,len(added)
assert sum(len(e['pose']['rig3D']['frames']) for e in added)==212
assert all(not e['pose']['frames'] for e in added),'new entries must not duplicate legacy 2D poses'
assert all(e['pose']['rig3D']['reviewStatus']=='illustrative' for e in added)
assert not any('vádlianyújtás' in (e['name']+' '+e['summary']).lower() for e in entries)
assert len(json.loads((root/'docs/joga_jeloltforras/existing_catalog_review.json').read_text(encoding='utf8'))['items'])==26
p=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text(encoding='utf8')
assert ('<ApplicationDisplayVersion>0.22.0</ApplicationDisplayVersion>' in p or '<ApplicationDisplayVersion>0.23.0</ApplicationDisplayVersion>' in p) and ('<ApplicationVersion>22</ApplicationVersion>' in p or '<ApplicationVersion>23</ApplicationVersion>' in p)
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in p
catui=(root/'Mozdulj.Maui/Pages/CatalogPage.cs').read_text(encoding='utf8')
for s in ('_items.Scrolled','filterPanel.Children.Add(quickSources)','_state.ToggleFavoriteAsync','SmallAction','TapGestureRecognizer','overlay.TranslateTo'):
    assert s in catui,s
for s in ('"◉"','"♡"','"✎"'):
    assert s in catui,s
assert 'UI.Button("Megnyitás' not in catui
stat=(root/'Mozdulj.Maui/Pages/StatisticsPage.cs').read_text(encoding='utf8')
assert 'WeeklyGoalTracker.AwardsInMonth' in stat
player=(root/'Mozdulj.Maui/Pages/WorkoutPlayerPage.cs').read_text(encoding='utf8')
assert '?"exercise":"preparation"' in player and 'RemainingSeconds=workout.Kind=="recovery"?0:5' in player
assert '_audio.Countdown(_workout.RemainingSeconds)' in player
rootTab=(root/'Mozdulj.Maui/Pages/RootTabs.cs').read_text(encoding='utf8')
assert 'ToolbarPlacement.Top' in rootTab
for file in ['engine.js','studio.js']:
    subprocess.run(['node','--check',str(root/'Mozdulj.Maui/Resources/Raw/rig3d'/file)],check=True,stdout=subprocess.DEVNULL)
print('PASS v22 catalog:',len(entries),'entries,',len(added),'new yoga, 212 new 3D frames, 26 review candidates')
print('PASS v22 UI source: tabs top, filters scroll, compact actions, preparation countdown, weekly awards')
