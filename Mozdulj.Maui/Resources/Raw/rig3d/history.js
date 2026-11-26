/* Pose history, intentionally independent of the camera and UI. */
(function(root){
'use strict';
const copy=value=>JSON.parse(JSON.stringify(value));
class PoseHistory {
  constructor(limit=60){this.limit=limit;this.clear();}
  clear(){this.past=[];this.future=[];this.pending=null;}
  begin(state){if(this.pending===null)this.pending=copy(state);}
  finish(state){
    if(this.pending===null)return false;
    const before=this.pending;this.pending=null;
    return this.record(before,state);
  }
  record(before,after){
    if(this.pending!==null||JSON.stringify(before.pose)===JSON.stringify(after.pose))return false;
    this.past.push(copy(before));
    if(this.past.length>this.limit)this.past.shift();
    this.future=[];
    return true;
  }
  get canUndo(){return this.past.length>0;}
  get canRedo(){return this.future.length>0;}
  undo(current){
    this.finish(current);
    if(!this.canUndo)return null;
    this.future.push(copy(current));
    return copy(this.past.pop());
  }
  redo(current){
    this.finish(current);
    if(!this.canRedo)return null;
    this.past.push(copy(current));
    return copy(this.future.pop());
  }
}
root.MozduljPoseHistory=PoseHistory;
if(typeof module!=='undefined')module.exports=PoseHistory;
})(typeof window!=='undefined'?window:globalThis);
