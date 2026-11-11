"""Automated DOM/interaction checks for the new 3D viewer. WebGL renderer is stubbed.
This is not a physical Android or GPU test.
"""
import json
from pathlib import Path
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
for tag in ('<script src="engine.js"></script>','<script src="studio.js"></script>','<link rel="stylesheet" href="studio-v16.css">'):
    assert tag in html
    html=html.replace(tag,'')
exercises=json.loads((root/'Mozdulj.Maui/Resources/Raw/exercises.catalog.json').read_text(encoding='utf-8'))['exercises']
def get_rig(n):
    matches=[e for e in exercises if len(e['pose']['rig3D']['frames'])==n]
    assert matches, 'Missing '+str(n)+'-phase exercise'
    return matches[0]['pose']['rig3D']
with sync_playwright() as p:
    b=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    page=b.new_page(viewport={'width':410,'height':800},device_scale_factor=1)
    errors=[]
    page.on('pageerror',lambda exc:errors.append(str(exc)))
    page.set_content(html)
    page.add_style_tag(content=(assets/'studio-v16.css').read_text(encoding='utf-8'))
    page.add_script_tag(content=(assets/'engine.js').read_text(encoding='utf-8'))
    page.evaluate('''() => { MozduljRig.shaded3D = () => {
      let c={azimuth:.58,elevation:.34,distance:3.55,target:[0,.72,0]};
      return {draw:()=>({clamped:[]}),setCamera:v=>{c=v;},getCamera:()=>c,
        pick:()=>null,dragTarget:()=>[],release:()=>{}};}; }''')
    page.add_script_tag(content=(assets/'studio.js').read_text(encoding='utf-8'))
    assert page.get_by_role('button',name='Kamera kezelése').count()==1
    assert page.get_by_role('button',name='Forgatás').count()==0
    assert page.locator('#orbit').is_visible()
    for count in (1,2,3):
        rig=get_rig(count)
        ok=page.evaluate('(r) => window.loadRigBase64(btoa(unescape(encodeURIComponent(JSON.stringify(r)))))',rig)
        assert ok is True
        assert page.locator('#phaseProgress').inner_text()==f'1 / {count}'
        assert page.locator('#phaseLabel').inner_text()==rig['frames'][0]['name']
        assert page.locator('#phasePrev').is_disabled()
        assert page.locator('#phaseNext').is_disabled() == (count==1)
        for i in range(1,count):
            page.locator('#phaseNext').click()
            assert page.locator('#phaseProgress').inner_text()==f'{i+1} / {count}'
            assert page.locator('#phaseLabel').inner_text()==rig['frames'][i]['name']
        assert page.locator('#phaseNext').is_disabled()
        if count>1:
            page.locator('#phasePrev').click()
            assert page.locator('#phaseProgress').inner_text()==f'{count-1} / {count}'
    # Switch to real viewer mode and ensure no editor controls / no redundant mode switch.
    page.evaluate('window.setReadOnlyMode()')
    assert not page.locator('.edit-tools').is_visible()
    assert not page.locator('main > section:nth-child(2)').is_visible()
    assert page.locator('#phasePrev').is_visible()
    assert page.locator('#phaseNext').is_visible()
    assert page.locator('.view-controls button').count()==4
    assert page.locator('.phase-controls button').count()==2
    # Dragging inside the canvas still rotates the camera without choosing any rotation tool.
    angle0=page.evaluate('window.exportRigJson()')
    pos=page.locator('#scene').bounding_box(); assert pos
    x=pos['x']+pos['width']*.50;y=pos['y']+pos['height']*.45
    page.mouse.move(x,y);page.mouse.down();page.mouse.move(x+66,y+15,steps=6);page.mouse.up()
    # compare camera via serialized export, returned from engine stub
    a0=json.loads(angle0)['camera']['azimuth']
    a1=json.loads(page.evaluate('window.exportRigJson()'))['camera']['azimuth']
    assert abs(a0-a1)>0.05,(a0,a1)
    page.locator('#phaseNext').click()
    assert page.locator('#phaseProgress').inner_text()=='3 / 3'
    assert page.locator('#phaseNext').is_disabled()
    assert not errors, errors
    page.screenshot(path=str(root/'docs/RIG3D_NEZET_v17_ELLENORZES.png'),full_page=True)
    print('PASS: editor/viewer, 1-2-3 phase nav, disabled bounds, camera drag rotation, no redundant viewer button, responsive CSS')
    b.close()
