"""Offline browser smoke-test for v0.19 leg/foot controls (3D draw stubbed)."""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
for tag in ('<script src="engine.js"></script>','<script src="studio.js"></script>','<link rel="stylesheet" href="studio-v16.css">'):
    assert tag in html;html=html.replace(tag,'')
with sync_playwright() as p:
    b=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=b.new_page(viewport={'width':400,'height':860})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.set_content(html)
    page.add_style_tag(content=(assets/'studio-v16.css').read_text(encoding='utf-8'))
    page.add_script_tag(content=(assets/'engine.js').read_text(encoding='utf-8'))
    page.evaluate('''() => { MozduljRig.shaded3D = () => {
      let c={azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]};
      return {draw:()=>({clamped:[]}),setCamera:v=>{c=v;},getCamera:()=>c,
        pick:()=>null,dragTarget:()=>[],release:()=>{}};}; }''')
    page.add_script_tag(content=(assets/'studio.js').read_text(encoding='utf-8'))
    page.locator('#preset').select_option('supine')
    page.locator('#legSide').select_option('right')
    for key,value in [('hipFlex','120'),('hipAbduct','20'),('kneeFlex','30'),('footPitch','42')]:
        page.locator('#'+key).evaluate('(el,v)=>{el.value=v;el.dispatchEvent(new Event("input",{bubbles:true}));}',value)
    rig=json.loads(page.evaluate('window.exportRigJson()'))
    import math
    f=rig['frames'][0]
    assert math.isclose(f['legAngles']['right']['flexion'],math.radians(120),abs_tol=1e-5)
    assert math.isclose(f['legAngles']['right']['abduction'],math.radians(20),abs_tol=1e-5)
    assert math.isclose(f['legAngles']['right']['kneeFlexion'],math.radians(30),abs_tol=1e-5)
    assert math.isclose(f['footPitch']['right'],math.radians(42),abs_tol=1e-5)
    page.locator('#phaseNext').click()
    assert page.locator('#phaseProgress').inner_text()=='2 / 2'
    assert not errors,errors
    page.screenshot(path=str(root/'docs/RIG3D_LABFEJ_EDITOR_v19.png'),full_page=True)
    print('PASS: lábválasztó, csípőhajlítás, oldalra emelés, térdhajlítás, pipa/spicc, 3D JSON, fázisváltás')
    b.close()
