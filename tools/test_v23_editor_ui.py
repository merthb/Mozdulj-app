"""Browser UI regression. Uses real engine validation and collision, stubbed WebGL draw.
Does not claim visual WebGL hardware validation.
"""
from pathlib import Path
import json
from bs4 import BeautifulSoup
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
for tag in ('<script src="engine.js"></script>','<script src="studio.js"></script>','<link rel="stylesheet" href="studio-v16.css">'):
    assert tag in html; html=html.replace(tag,'')
parsed=BeautifulSoup(html,'html.parser')
groups=[x.find('summary').get_text(strip=True) for x in parsed.select('details.edit-group')]
assert ['Fej, váll, törzs és medence','Karok és kézfejek','Lábak és lábfejek','Eszközök és gumiszalag','Merev eszköz elhelyezése','Gumiszalag rögzítése és vezetése','Kamera és nagyítás']==groups, groups
with sync_playwright() as pw:
    browser=pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=browser.new_page(viewport={'width':390,'height':840})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.set_content(html)
    page.add_style_tag(content=(assets/'studio-v16.css').read_text(encoding='utf-8'))
    page.add_script_tag(content=(assets/'engine.js').read_text(encoding='utf-8'))
    page.evaluate('''() => { MozduljRig.shaded3D=()=>{
       let camera={azimuth:.58,elevation:.35,distance:3.55,target:[0,.72,0]};
       return {draw:()=>({clamped:[],bandBlocked:false}),setCamera:c=>camera=c,
          getCamera:()=>camera,pick:()=>null,dragTarget:()=>[],release:()=>{}};
    }; }''')
    page.add_script_tag(content=(assets/'studio.js').read_text(encoding='utf-8'))
    assert not errors,errors
    assert page.locator('#phaseProgress').inner_text()=='1 / 2'
    assert page.locator('#addPhase').is_enabled()
    # All top-level groups start folded, exposing only the editor essentials.
    for title in ['Fej, váll, törzs és medence','Karok és kézfejek','Lábak és lábfejek','Eszközök és gumiszalag','Kamera és nagyítás']:
        el=page.locator('details.edit-group').filter(has=page.locator('summary',has_text=title)).first
        assert not el.evaluate('(n)=>n.open'),title
    page.locator('#addPhase').click()
    assert page.locator('#phaseProgress').inner_text()=='2 / 3'
    assert page.locator('#phaseLabel').inner_text()=='Új fázis 3'
    page.locator('#phaseName').fill('Kitartás')
    assert page.locator('#phaseLabel').inner_text()=='Kitartás'
    data=json.loads(page.evaluate('window.exportRigJson()'))
    assert len(data['frames'])==3 and data['frames'][1]['name']=='Kitartás'
    # Current frame cloned its predecessor's rig, but any update is independent.
    page.locator('details.edit-group > summary',has_text='Fej, váll, törzs és medence').click()
    page.locator('#yaw').evaluate('(el)=>{el.value="20";el.dispatchEvent(new Event("input",{bubbles:true}));}')
    data=json.loads(page.evaluate('window.exportRigJson()'))
    assert data['frames'][0]['headYaw']==0
    assert data['frames'][1]['headYaw']!=0
    page.locator('#phasePrev').click()
    assert page.locator('#phaseProgress').inner_text()=='1 / 3'
    assert page.locator('#phaseName').input_value()=='Kiindulás'
    page.locator('#phaseNext').click()
    page.locator('#removePhase').click()
    assert page.locator('#phaseProgress').inner_text()=='2 / 2'
    assert len(json.loads(page.evaluate('window.exportRigJson()'))['frames'])==2
    # Limit is enforced by JS and the model validator's 6-phase limit.
    for _ in range(4): page.locator('#addPhase').click()
    assert len(json.loads(page.evaluate('window.exportRigJson()'))['frames'])==6
    assert page.locator('#addPhase').is_disabled()
    assert not errors,errors
    # Save/import round trip, including phase names.
    archive=page.evaluate('window.exportRigBase64()')
    assert page.evaluate('(s)=>window.loadRigBase64(s)',archive)
    assert len(json.loads(page.evaluate('window.exportRigJson()'))['frames'])==6
    assert not errors,errors
    # Read-only viewer keeps phase switching, hides editor.
    page.evaluate('window.setReadOnlyMode()')
    assert not page.locator('#addPhase').is_visible()
    assert page.locator('#phasePrev').is_visible()
    page.screenshot(path=str(root/'docs/RIG3D_EDITOR_v23_UI.png'),full_page=True)
    browser.close()
print('PASS v23 WebView editor: accordions, phase add/remove/rename, 6-frame cap, JSON roundtrip, viewer')
