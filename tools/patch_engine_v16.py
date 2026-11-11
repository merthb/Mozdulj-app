from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'Mozdulj.Maui/Resources/Raw/rig3d/engine.js'
s=p.read_text(encoding='utf-8')
# Prevent mirrored elbow by using posterior/anatomic pole; do not trust a previous wrong pole.
s=s.replace("const anatomicElbow=add(shoulder[side],rotate([s*.48,-.26,.20],e));", "const anatomicElbow=add(shoulder[side],rotate([s*.10,-.31,-.33],e));")
s=s.replace("const anatomicKnee=add(hip[side],rotate([s*.10,-.12,.53],e));", "const anatomicKnee=add(hip[side],rotate([s*.02,-.17,.58],e));")
s=s.replace("return dot(q,reference)>=.5 ? requested : anatomic;", "return dot(q,reference)>=.87 ? requested : anatomic;")
s=s.replace("const b={name:'Kiindulás',root:[0,.94,0],euler:[0,0,0],headYaw:0,headPitch:0,hands:", "const b={name:'Kiindulás',root:[0,.94,0],euler:[0,0,0],headYaw:0,headPitch:0,headRoll:0,shoulderLift:0,hands:")
s=s.replace("if(preset==='balance'){f.root=[0,.94,0];", "if(preset==='balance'){f.root=[0,.94,0];")
anchor=" return f;\n}\nfunction initialPose"
insert=""" if(preset==='prone') {f.root=[0,.26,.16];f.euler=[Math.PI/2,0,0];f.hands={left:[-.24,.08,.55],right:[.24,.08,.55]};f.feet={left:[-.16,.07,-.87],right:[.16,.07,-.87]};f.poles.elbowLeft=[-.36,.28,.36];f.poles.elbowRight=[.36,.28,.36];f.poles.kneeLeft=[-.15,.20,-.59];f.poles.kneeRight=[.15,.20,-.59];f.headPitch=-.12;f.equipment=[{type:'mat'}];}
 if(preset==='sideplank'){f.root=[0,.51,.14];f.euler=[-Math.PI/2,0,Math.PI/2];f.hands={left:[-.22,.12,.43],right:[.18,.67,.07]};f.feet={left:[.01,.06,.77],right:[.10,.08,.81]};f.poles.kneeLeft=[-.17,.10,.58];f.poles.kneeRight=[.15,.13,.55];f.equipment=[{type:'mat'}];}
 if(preset==='kneel'){f.root=[0,.70,.03];f.feet={left:[-.18,.07,.55],right:[.18,.07,.55]};f.poles.kneeLeft=[-.16,.14,.25];f.poles.kneeRight=[.16,.14,.25];f.hands={left:[-.31,.73,.12],right:[.31,.73,.12]};f.equipment=[{type:'mat'}];}
 if(preset==='hinge'){f.root=[0,.82,0];f.euler=[-.38,0,0];f.hands={left:[-.30,.77,.40],right:[.30,.77,.40]};f.feet={left:[-.16,.06,.18],right:[.16,.06,.18]};}
 if(preset==='ballseat'){f.root=[0,.67,0];f.hands={left:[-.32,.68,.16],right:[.32,.68,.16]};f.feet={left:[-.17,.07,.44],right:[.17,.07,.44]};f.poles.kneeLeft=[-.18,.43,.41];f.poles.kneeRight=[.18,.43,.41];f.equipment=[{type:'ball',position:[0,.34,0]}];}
 if(preset==='plank'){f.root=[0,.39,.14];f.euler=[-Math.PI/2,0,0];f.hands={left:[-.24,.055,-.48],right:[.24,.055,-.48]};f.feet={left:[-.16,.055,.88],right:[.16,.055,.88]};f.poles.kneeLeft=[-.17,.20,.66];f.poles.kneeRight=[.17,.20,.66];f.poles.elbowLeft=[-.25,.35,-.30];f.poles.elbowRight=[.25,.35,-.30];f.equipment=[{type:'mat'}];}
 return f;
}
function initialPose"""
assert anchor in s
s=s.replace(anchor,insert)
# shoulders rise after shrug and head roll around neck
s=s.replace("const shoulder={left:world([-.215,.54,0]),right:world([.215,.54,0])};", "const lift=clamp(frame.shoulderLift||0,-.04,.085);const shoulder={left:world([-.215,.54+lift,0]),right:world([.215,.54+lift,0])};")
s=s.replace("const head=j.head,hyaw=frame.headYaw||0,hpitch=frame.headPitch||0;", "const head=j.head,hyaw=frame.headYaw||0,hpitch=frame.headPitch||0,hroll=frame.headRoll||0;")
s=s.replace("const faceForward=rotate(rotate([0,0,1],[hpitch,hyaw,0]),frame.euler);let headUp=rotate([0,1,0],frame.euler)", "const faceForward=rotate(rotate([0,0,1],[hpitch,hyaw,hroll]),frame.euler);let headUp=rotate(rotate([0,1,0],[hpitch,hyaw,hroll]),frame.euler)")
# geometric elbow policy with explicit posterior, avoid pole flip:
s=s.replace("let h=solve2(shoulder[side],frame.hands[side],bones.armUpper,bones.armLower,", "let h=solve2(shoulder[side],frame.hands[side],bones.armUpper,bones.armLower,")
# Prevent incorrect legs for side-lying by not incorrectly forcing each foot to a floor y.
# add wall as equipment
s=s.replace("if(type==='mat'){drawM", "if(type==='wall'){drawM(model([0,1,-.62],[1.24,0,0],[0,1.1,0],[0,0,.025]),'floor');}\n  if(type==='mat'){drawM")
# strengthen pose validation
s=s.replace("function checkPose(p){if(!p||p.version!==1", "function checkPose(p){if(!p||p.version!==1")
s=s.replace("for(let kind of ['hands','feet'])for(let side of ['left','right'])", "for(const k of ['headYaw','headPitch','headRoll','shoulderLift'])if(f[k]!==undefined&&(!Number.isFinite(f[k])||Math.abs(f[k])>2))throw Error('Hibás ízületi vezérlő: '+k);for(let kind of ['hands','feet'])for(let side of ['left','right'])")
p.write_text(s,encoding='utf-8')
# replicate in demo
(root/'docs/rig3d_demo/engine.js').write_text(s,encoding='utf-8')
print('Updated 3D engine')
