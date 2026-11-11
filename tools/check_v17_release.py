"""Project/static validation. Does not compile a native APK or test GPU rendering."""
import json,hashlib,xml.etree.ElementTree as ET
from pathlib import Path
r=Path(__file__).resolve().parents[1]
app=r/'Mozdulj.Maui'
proj=ET.parse(app/'Mozdulj.Maui.csproj').getroot()
ns={'android':'http://schemas.android.com/apk/res/android'}
def prop(name):
 x=proj.find('.//'+name)
 return x.text if x is not None else None
assert prop('ApplicationId')=='hu.mozdulj.preview'
assert prop('ApplicationDisplayVersion')=='0.17.0'
assert prop('ApplicationVersion')=='17'
maui_icon=proj.find('.//MauiIcon')
assert maui_icon is not None
assert maui_icon.attrib['Include'].endswith('AppIcon\\appicon.svg')
assert maui_icon.attrib['ForegroundFile'].endswith('AppIcon\\appiconfg.svg')
for f in ('AppIcon/appicon.svg','AppIcon/appiconfg.svg','Splash/splash.svg'):
 ET.parse(app/'Resources'/f)
fg=(app/'Resources/AppIcon/appiconfg.svg').read_text(encoding='utf-8')
splash=(app/'Resources/Splash/splash.svg').read_text(encoding='utf-8')
assert 'M64 105C31 88 24 56 40 37' in splash and 'M64 105C31 88 24 56 40 37' in fg
manifest=ET.parse(app/'Platforms/Android/AndroidManifest.xml').getroot()
a=manifest.find('application')
assert a.attrib['{http://schemas.android.com/apk/res/android}icon']=='@mipmap/appicon'
assert a.attrib['{http://schemas.android.com/apk/res/android}roundIcon']=='@mipmap/appicon_round'
cat=json.loads((app/'Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))
assert len(cat['exercises'])==132
assets=app/'Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
js=(assets/'studio.js').read_text(encoding='utf-8')
css=(assets/'studio-v16.css').read_text(encoding='utf-8')
assert 'id="phasePrev"' in html and 'id="phaseNext"' in html
assert 'id="orbit" class="active">Kamera kezelése' in html
assert '<button id="orbit" class="active">Forgatás' not in html
assert 'class="phase-controls"' in html
assert "for(const m of ['orbit','move','pan'])" in js
assert "window.setReadOnlyMode" in js
assert 'view-only .edit-tools{display:none}' in css
assert (r/'docs/RIG3D_NEZET_v17_interaktiv.html').exists()
print('PASS: MAUI v0.17 adaptive icon, manifest, stable app ID, 132 exercises, phase navigation and viewer mode')
