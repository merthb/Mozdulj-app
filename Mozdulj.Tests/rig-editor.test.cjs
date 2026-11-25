const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const root=path.resolve(__dirname,'../Mozdulj.Maui/Resources/Raw/rig3d');
const History=require(path.join(root,'history.js'));
const state=(height,frameIndex=0)=>({pose:{frames:[{root:[0,height,0]}]},frameIndex});
test('one drag is one undo, redo restores final pose',()=>{
 const h=new History();h.begin(state(1));
 for(const y of [.9,.8,.7])h.record(state(y+.1),state(y));
 h.finish(state(.7));assert.equal(h.past.length,1);
 assert.deepEqual(h.undo(state(.7)),state(1));assert.deepEqual(h.redo(state(1)),state(.7));
});
test('no-op keeps redo and new edit clears it; frame navigation alone is not an edit',()=>{
 const h=new History();h.record(state(1),state(.8));
 h.undo(state(.8));assert.equal(h.canRedo,true);
 h.record(state(1,0),state(1,1));assert.equal(h.canRedo,true);
 h.record(state(1),state(.6));assert.equal(h.canRedo,false);
});
test('deep snapshots preserve complete pose metadata and bounded history',()=>{
 const h=new History(2);const a=state(1);a.pose.frames[0].equipment=[{type:'band',anchorA:'leftHand'}];
 const b=structuredClone(a);b.pose.frames[0].equipment[0].anchorA='rightHand';
 h.record(a,b);a.pose.frames[0].equipment[0].anchorA='fixed';
 assert.equal(h.undo(b).pose.frames[0].equipment[0].anchorA,'leftHand');
 h.clear();for(let i=0;i<4;i++)h.record(state(i),state(i+1));assert.equal(h.past.length,2);
});
test('all packaged JS parses',()=>{
 for(const file of ['history.js','engine.js','studio.js','localization.js'])new vm.Script(fs.readFileSync(path.join(root,file),'utf8'),{filename:file});
});
test('pelvis movement preserves IK targets and clamps world bounds',()=>{
 const context={};context.window=context;vm.createContext(context);
 vm.runInContext(fs.readFileSync(path.join(root,'engine.js'),'utf8'),context);
 const rig=context.MozduljRig;const f=rig.baseFrame('crosslegged');
 const before=JSON.stringify(f.hands),feet=JSON.stringify(f.feet),y=f.root[1];
 rig.moveRoot(f,[0,-.05,0]);assert.ok(Math.abs(f.root[1]-(y-.05))<1e-8);
 assert.equal(JSON.stringify(f.hands),before);assert.equal(JSON.stringify(f.feet),feet);
 rig.moveRoot(f,[0,-100,0]);assert.equal(f.root[1],0);
});
