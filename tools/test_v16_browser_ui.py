import json
from pathlib import Path
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
for tag in ['<script src="engine.js"></script>','<script src="studio.js"></script>','<link rel="stylesheet" href="studio-v16.css">']:
    html=html.replace(tag,'')
ex=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))['exercises']
def rig(id):
    return next(e['pose']['rig3D'] for e in ex if e['id']==id)
with sync_playwright() as p:
    b=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=b.new_page(viewport={'width':1100,'height':1000})
    errors=[];page.on('pageerror',lambda exc:errors.append(str(exc)))
    page.set_content(html)
    page.add_style_tag(content=(assets/'studio-v16.css').read_text(encoding='utf-8'))
    page.add_script_tag(content=(assets/'engine.js').read_text(encoding='utf-8'))
    # DOM test without graphical WebGL (not available in this restricted runtime).
    page.evaluate('''() => { MozduljRig.shaded3D = () => {
        let camera={azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]};
        return {draw:()=>({clamped:[]}),setCamera:c=>{camera=c;},getCamera:()=>camera,
           pick:()=>null,dragTarget:()=>[],release:()=>{}}; }; }''')
    page.add_script_tag(content=(assets/'studio.js').read_text(encoding='utf-8'))
    assert json.loads(page.evaluate('window.getRigHealth()'))['webgl'] is True
    # Three-frame neck rotation, switch to left and ensure selected phase updates.
    page.evaluate('(r)=>window.loadRigBase64(btoa(unescape(encodeURIComponent(JSON.stringify(r)))))',rig('fejforditas-jobbra-balra'))
    assert page.locator('#frameButtons button').count()==3
    page.get_by_role('button',name='Balra fordítás').click()
    assert '3 / 3' in page.locator('#frameNumber').inner_text()
    # One-frame holds never expose an inert second button.
    page.evaluate('(r)=>window.loadRigBase64(btoa(unescape(encodeURIComponent(JSON.stringify(r)))))',rig('hegytartas'))
    assert page.locator('#frameButtons button').count()==1
    assert '1 / 1' in page.locator('#frameNumber').inner_text()
    # Two different exercise phases in viewer mode; no editor visible.
    page.evaluate('(r)=>window.loadRigBase64(btoa(unescape(encodeURIComponent(JSON.stringify(r)))))',rig('konyokhajlitas-ulve'))
    page.evaluate('window.setReadOnlyMode()')
    assert page.locator('.view-controls .phase-view').count()==2
    assert not page.locator('main > section:nth-child(2)').is_visible()
    page.locator('.view-controls .phase-view').nth(1).click()
    assert '2 / 2' in page.locator('#frameNumber').inner_text()
    page.screenshot(path=str(root/'docs/RIG3D_UI_v16_elozet.png'),full_page=True)
    assert not errors, errors
    print('PASS: dynamic phase controls 3-frame, static 1-frame, viewer 2-frame, modern toolbar; no JS page errors')
    b.close()
