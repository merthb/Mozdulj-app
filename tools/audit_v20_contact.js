const fs=require('fs'),vm=require('vm'),path=require('path');
let s=fs.readFileSync(path.join(__dirname,'../Mozdulj.Maui/Resources/Raw/rig3d/engine.js'),'utf8');let ctx={window:{}};vm.createContext(ctx);vm.runInContext(s,ctx);const C=ctx.window.MozduljRig.Collision;
const catalog=JSON.parse(fs.readFileSync(path.join(__dirname,'../Mozdulj.Maui/Resources/Raw/exercises.catalog.json'),'utf8'));
let frames=0,issues=0,noIssues=0,max=0,types={},list=[];
for(const e of catalog.exercises){for(const [index,f] of e.pose.rig3D.frames.entries()){frames++;
 const r=C.report(f);if(r.count){issues++;max=Math.max(max,r.count);list.push({id:e.id,name:e.name,frame:index,overlaps:r.count,examples:r.examples});}
 else noIssues++;
 for(const x of r.examples)types[x.name.split('@')[1]]=(types[x.name.split('@')[1]]||0)+1;
}}
fs.writeFileSync(path.join(__dirname,'../docs/RIG3D_UTKOZES_AUDIT_v20.json'),JSON.stringify({exercises:catalog.exercises.length,frames,issueFrames:issues,cleanFrames:noIssues,maxIssueCount:max,surfaces:types,details:list},null,2));
console.log('count',catalog.exercises.length,'frames',frames,'with base collisions',issues,'clear',noIssues,'max overlap samples',max,'surfaces',types);
console.log(list.slice(0,12).map(x=>`${x.name}: ${x.overlaps} ${x.examples[0]?.name}`).join('\n'));
