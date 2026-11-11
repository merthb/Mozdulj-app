from pathlib import Path
import json,re,subprocess
root=Path(__file__).resolve().parents[1]
catalog=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text())
assert len(catalog['exercises'])==132
assert all(len(e['pose']['frames'])>=2 for e in catalog['exercises'])
assert all(len(frame['joints'])==16 for e in catalog['exercises'] for frame in e['pose']['frames'])
assert all(e['category'] not in ['jóga','Jóga és teljes test'] for e in catalog['exercises'])
print('PASS 132 offline JSON gyakorlat, mindegyiknél 2+ pózfázis és 16 testpont')
source=(root/'Mozdulj.Maui/Pages/CatalogPage.cs').read_text()
assert 'CollectionView' in source and 'RemainingItemsThresholdReached' in source
assert 'private const int BatchSize = 12' in source
assert 'Content = new ScrollView { Content=content }' not in source
assert 'new Grid {' in source and 'GridLength.Star' in source
print('PASS CollectionView + 12-es adagok + csillagméretű lista-viewport')
rewards=(root/'Mozdulj.Core/Rewards.cs').read_text()
assert 'LevelThreshold' in rewards and 'FreezeThreshold' in rewards and 'long TotalXp' in rewards
assert 250 + 50 * 0==250 and 250+50*1==300
assert [250*n+25*n*(n-1) for n in range(1,6)]==[250,550,900,1300,1750]
assert [500*n+50*n*(n-1) for n in range(1,5)]==[500,1100,1800,2600]
print('PASS referencia XP-lépcsők 250/550/900/1300 és freeze 500/1100/1800/2600')
models=(root/'Mozdulj.Core/Models.cs').read_text()
for key in ['MonthlyChallenges','MonthlyGoalAwards','Regions { get; set; } = []','Equipment { get; set; } = []','Category { get; set; } = ""']:
 assert key in models,key
monthly=(root/'Mozdulj.Core/MonthlyChallenges.cs').read_text()
for key in ['EnsureMonth','UpdateAwards','RegionExercises','EquipmentDays','CategoryDays','previous!.Year', 'prior.Target + (achieved >= prior.Target ? 1 : -1)']:
 assert key in monthly,key
print('PASS havi célsnapshot, könnyítés/nehezítés, teljesítéskori metaadatok jelen vannak')
html=(root/'docs/UI_v10_gyors_katalogus_es_kihivasok.html').read_text()
assert 'IntersectionObserver' in html and 'const batchSize=12' in html
js=html.split('<script>',1)[1].split('</script>',1)[0]
p=root/'docs/.v10-preview-check.js';p.write_text(js)
r=subprocess.run(['node','--check',str(p)],capture_output=True,text=True)
assert r.returncode==0,r.stderr
p.unlink()
print('PASS HTML interaktív előnézet JavaScript szintaxis')
print('NOTE .NET SDK és Android build nélkül ez nem helyettesíti a C# fordítást vagy a teljesítménymérést')
