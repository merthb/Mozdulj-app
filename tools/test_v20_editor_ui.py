"""Offline 3D editor UI smoke test. Stub renderer so GPU availability is not required."""
from pathlib import Path
import json, math
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parents[1]
assets=root/'Mozdulj.Maui/Resources/Raw/rig3d'
html=(assets/'index.html').read_text(encoding='utf-8')
for tag in ('<script src="engine.js"></script>','<script src="studio.js"></script>','<link rel="stylesheet" href="studio-v16.css">'):
    assert tag in html;html=html.replace(tag,'')
with sync_playwright() as p:
 b=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
 page=b.new_page(viewport={'width':390,'height':860})
 errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.set_content(html)
 page.add_style_tag(content=(assets/'studio-v16.css').read_text(encoding='utf-8'))
 page.add_script_tag(content=(assets/'engine.js').read_text(encoding='utf-8'))
 page.evaluate('''() => { MozduljRig.shaded3D=()=>{
   let c={azimuth:.58,elevation:.35,distance:3.55,target:[0,.72,0]};
   return {draw:()=>({clamped:[]}),setCamera:v=>{c=v;},getCamera:()=>c,
    pick:()=>null,dragTarget:()=>[],release:()=>{}};}; }''')
 page.add_script_tag(content=(assets/'studio.js').read_text(encoding='utf-8'))
 assert not errors,errors
 # New v0.23 editor folds controls into accordion sections.
 page.locator('details.edit-group > summary',has_text='Karok és kézfejek').click()
 page.locator('details.edit-group > summary',has_text='Eszközök és gumiszalag').click()
 page.locator('#preset').select_option('stand')
 page.locator('#armSide').select_option('right')
 for key,val in [('armFlex','100'),('armAbduct','30'),('elbowFlex','90'),('wristFlex','20'),('forearmRotation','45')]:
    page.locator('#'+key).evaluate('(el,v)=>{el.value=v;el.dispatchEvent(new Event("input",{bubbles:true}));}',val)
 rig=json.loads(page.evaluate('window.exportRigJson()'))
 arm=rig['frames'][0]['armAngles']['right']
 assert math.isclose(arm['flexion'],math.radians(100),abs_tol=.06),arm
 assert math.isclose(arm['abduction'],math.radians(30),abs_tol=.06),arm
 assert math.isclose(arm['elbowFlexion'],math.radians(90),abs_tol=.06),arm
 assert math.isclose(arm['wristFlexion'],math.radians(20),abs_tol=.06),arm
 assert math.isclose(arm['forearmRotation'],math.radians(45),abs_tol=.06),arm
 # Exercise-band endpoints and a selected path are persisted in the JSON.
 page.locator('#addEquipment').click() if page.locator('#equip').input_value()=='band' else None
 page.locator('#equip').select_option('band')
 page.locator('#addEquipment').click()
 page.locator('#bandAnchorA').select_option('leftHand')
 page.locator('#bandAnchorB').select_option('rightAnkle')
 page.locator('#bandRoute').select_option('front')
 page.locator('#applyBand').click()
 rig=json.loads(page.evaluate('window.exportRigJson()'))
 bands=[x for x in rig['frames'][0]['equipment'] if x['type']=='band']
 assert bands and bands[0]['anchorA']=='leftHand' and bands[0]['anchorB']=='rightAnkle',bands
 assert bands[0]['route']=='front',bands
 assert not errors,errors
 # Positionable equipment is automatically placed in a non-penetrating location.
 page.locator('#equip').select_option('ball')
 page.locator('#addEquipment').click()
 rig2=json.loads(page.evaluate('window.exportRigJson()'))
 balls=[x for x in rig2['frames'][0]['equipment'] if x['type']=='ball']
 assert len(balls)==1 and len(balls[0]['position'])==3,balls
 page.locator('summary',has_text='Merev eszköz elhelyezése').click()
 page.locator('#propSelect').select_option(str(next(i for i,e in enumerate(rig2['frames'][0]['equipment']) if e['type']=='ball')))
 for k,v in [('X','0'),('Y','.94'),('Z','0')]:
  page.locator('#prop'+k).fill(v)
 page.locator('#moveProp').click()
 rig3=json.loads(page.evaluate('window.exportRigJson()'))
 ball3=next(x for x in rig3['frames'][0]['equipment'] if x['type']=='ball')
 assert ball3['position']!=[0,.94,0], 'moving ball through torso must be stopped'
 assert 'Ütközési korlát' in page.locator('#status').inner_text()
 # A second band may use a fixed world-space anchor and a joint point.
 page.locator('#bandSelect').select_option('new')
 page.locator('#bandAnchorA').select_option('fixed')
 page.locator('#bandAnchorB').select_option('rightHand')
 for k,v in [('X','-1.3'),('Y','.5'),('Z','0')]:page.locator('#bandA'+k).fill(v)
 page.locator('#applyBand').click()
 newRig=json.loads(page.evaluate('window.exportRigJson()'))
 assert any(x.get('anchorA')=='fixed' and x.get('position')==[-1.3,.5,0] for x in newRig['frames'][0]['equipment']), 'fixed endpoint persisted'
 page.locator('#phaseNext').click()
 assert page.locator('#phaseProgress').inner_text()=='2 / 2'
 assert not errors,errors
 page.screenshot(path=str(root/'docs/RIG3D_EDITOR_v20_ELOZET.png'),full_page=True)
 b.close()
 print('PASS UI: kar, csukló, szalagvégek, ütköző eszköz elutasítása, fázisváltás')
