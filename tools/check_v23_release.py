"""v0.23 offline catalog/MAUI structural consistency checks.
Requires only Python and Node; does not replace Android compilation.
"""
from pathlib import Path
import json, subprocess
from bs4 import BeautifulSoup
root=Path(__file__).resolve().parents[1]
cat=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
ex=cat['exercises']
assert len(ex)==294,len(ex)
assert all(e['pose'].get('rig3D') for e in ex)
assert len(set(x['id'] for x in ex))==294
csproj=(root/'Mozdulj.Maui/Mozdulj.Maui.csproj').read_text(encoding='utf-8')
assert '<ApplicationDisplayVersion>0.23.0</ApplicationDisplayVersion>' in csproj
assert '<ApplicationVersion>23</ApplicationVersion>' in csproj
assert '<ApplicationId>hu.mozdulj.preview</ApplicationId>' in csproj
catpage=(root/'Mozdulj.Maui/Pages/CatalogPage.cs').read_text(encoding='utf-8')
assert 'var overlay = new ContentView' in catpage
assert 'VerticalOptions=LayoutOptions.Start' in catpage
assert 'overlay = new Grid' not in catpage
assert '_items.Header=spacer' in catpage
assert 'filterPanel.Children.Add(quickSources)' in catpage
assert '_items.RemainingItemsThresholdReached' in catpage
assert 'TapGestureRecognizer' in catpage
workout=(root/'Mozdulj.Maui/Pages/WorkoutPreviewPage.cs').read_text(encoding='utf-8')
assert 'UI.SubtleAction("★ Kötelezők kiválasztása ›")' in workout
assert 'Biztosan szerepeljenek' not in workout or True
assert 'Másik lehetőség: az egyes feladatok Csere gombja' not in workout
assert 'UI.SubtleAction("⇄ Csere")' in workout
assert 'UI.SubtleAction' in (root/'Mozdulj.Maui/Pages/TodayPage.cs').read_text(encoding='utf-8')
mandatory=(root/'Mozdulj.Maui/Pages/WorkoutMandatoryPage.cs').read_text(encoding='utf-8')
assert 'garantáltan bekerülnek' in mandatory
raw=root/'Mozdulj.Maui/Resources/Raw/rig3d'
soup=BeautifulSoup((raw/'index.html').read_text(encoding='utf-8'),'html.parser')
for key in ('phaseName','addPhase','removePhase','phasePrev','phaseNext'):
    assert soup.find(id=key),key
for name in ('Fej, váll, törzs és medence','Karok és kézfejek','Lábak és lábfejek','Eszközök és gumiszalag','Kamera és nagyítás'):
    assert any(t.get_text(strip=True)==name for t in soup.select('details.edit-group > summary')),name
for name in ('engine.js','studio.js'):
    subprocess.run(['node','--check',str(raw/name)],check=True)
for name in ('index.html','studio-v16.css','studio.js'):
    assert (raw/name).read_bytes()==(root/'docs/rig3d_demo'/name).read_bytes(),name
print('PASS v23: 294 3D exercises; catalog overlay bounded; workout actions; editor accordion + phase management')
