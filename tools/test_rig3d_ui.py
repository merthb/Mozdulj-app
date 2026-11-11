"""UI functionality only. This Chromium environment disables real WebGL;
we use a no-op context to test event wiring, not visual rendering."""
from pathlib import Path
from playwright.sync_api import sync_playwright
p=Path(__file__).parents[1]/'Mozdulj.Maui/Resources/Raw/rig3d'
h=(p/'index.html').read_text()
mock='''<script>
HTMLCanvasElement.prototype.getContext=function(kind){
 if(kind!=='webgl')return null;
 const gl={};for(const c of ['VERTEX_SHADER','FRAGMENT_SHADER','ARRAY_BUFFER','ELEMENT_ARRAY_BUFFER','STATIC_DRAW','FLOAT','UNSIGNED_SHORT','TRIANGLES','DEPTH_TEST','CULL_FACE','SRC_ALPHA','ONE_MINUS_SRC_ALPHA','COLOR_BUFFER_BIT','DEPTH_BUFFER_BIT'])gl[c]=1;
 for(const m of ['shaderSource','compileShader','attachShader','linkProgram','useProgram','bindBuffer','bufferData','enableVertexAttribArray','vertexAttribPointer','enable','blendFunc','viewport','uniformMatrix4fv','uniform3fv','uniform1f','drawElements','clearColor','clear'])gl[m]=()=>{};
 for(const m of ['createShader','createProgram','createBuffer','getUniformLocation'])gl[m]=()=>({});
 gl.getShaderParameter=()=>true;gl.getProgramParameter=()=>true;gl.getAttribLocation=()=>0;gl.getExtension=()=>null;
 return gl;
};</script>'''
h=h.replace('<script src="engine.js"></script>',mock+'<script>'+(p/'engine.js').read_text()+'</script>')
h=h.replace('<script src="studio.js"></script>','<script>'+(p/'studio.js').read_text()+'</script>')
with sync_playwright() as pw:
 b=pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
 page=b.new_page(viewport={'width':900,'height':750})
 errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.set_content(h);page.wait_for_timeout(250)
 assert 'true' in page.evaluate('window.getRigHealth()')
 page.select_option('#preset','side')
 assert 'side' in page.locator('#status').inner_text().lower() or 'oldalfekvés' in page.locator('#status').inner_text().lower()
 page.click('#frame1')
 page.locator('#yaw').fill('25')
 page.select_option('#equip','band');page.click('#addEquipment')
 a=page.evaluate('JSON.parse(window.exportRigJson())')
 assert a['preset']=='side' and a['frames'][1]['equipment'][-1]['type']=='band'
 assert abs(a['frames'][1]['headYaw']-.4363)<.01
 b64=page.evaluate('window.exportRigBase64()')
 assert page.evaluate('(a)=>window.loadRigBase64(a)',b64)
 page.click('#front')
 page.click('#move')
 assert page.locator('#move').get_attribute('class')=='active'
 assert not errors, errors
 print('PASS: browser UI events, preset selection, phase, head yaw, equipment, base64 import/export, camera, JS errors=0 (GPU rendering NOT tested).')
 b.close()
