"""Explicit local v4 approach repair. Back up current source first; never rebuild the village."""
from pathlib import Path
import sys,json,hashlib,shutil,math
import numpy as np
R=Path(__file__).resolve().parents[2]
W=R/'art/terrain/village-v4';D=R/'assets/generated/village/terrain-v4'
B=R/'outputs/home-access/before'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def smooth(t):
 t=np.clip(t,0,1);return t*t*(3-2*t)
def terrain():
 backup=W/'edits/home-access';backup.mkdir(parents=True,exist_ok=True)
 if (backup/'height.bin').exists():raise RuntimeError('Approach repair already applied; edit explicitly, do not replay.')
 for n in ['height.bin','protected.bin']:shutil.copyfile(W/n,backup/n)
 old=np.fromfile(W/'height.bin','<f4').reshape(512,512);h=old.copy()
 x,z=np.meshgrid(np.arange(512)*.5-128,np.arange(512)*.5-128)
 weight=(1-smooth((np.abs(x+56)-3)/3))*(1-smooth((z-60)/2))*(z>52)
 target=-1.215*smooth((z-52)/5.5)
 h=old+(target-old)*weight;h=h.astype('<f4')
 changed=np.abs(h-old)>.000001;h[~changed]=old[~changed]
 h.tofile(W/'height.bin');changed.astype('u1').tofile(backup/'repair-mask.bin')
 protected=np.fromfile(W/'protected.bin','u1').reshape(512,512);protected[changed]=0;protected.tofile(W/'protected.bin')
 assert not changed[z<=52].any() and not changed[np.abs(x+56)>=6].any()
 column=h[360:374,144];assert np.abs(np.diff(column)/.5).max()<math.tan(math.radians(20))
 m=json.loads((D/'manifest.json').read_text());m['files']['height.bin']=sha(W/'height.bin');m['files']['protected.bin']=sha(W/'protected.bin');m['protectedSamples']=int(protected.sum())
 m['homeAccessRepair']={'date':'2026-09-15','backup':str(backup.relative_to(R)).replace('\\','/'),'changedSamples':int(changed.sum()),'bounds':[-62,52,-50,62],'reason':'Continuous return from z=61 stalled on steep approach at z=58.21; ease road to opposite courtyard without changing home yard or movement rules.'}
 (D/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8');print('HOME_APPROACH_REPAIR',m['homeAccessRepair'])
def blend():
 import bpy
 from mathutils import Vector
 sys.path.insert(0,str(Path(__file__).parent));import build_village_v4 as v4
 for p in [v4.SOURCE,v4.GLB,v4.META]:
  dest=B/p.name
  if dest.exists():raise RuntimeError('Saved before-source exists: refuse to apply twice')
  shutil.copyfile(p,dest)
 before=np.fromfile(W/'edits/home-access/height.bin','<f4').reshape(512,512)
 def height(h,x,z):
  fx=(x+128)*2;fz=(z+128)*2;i=max(0,min(510,math.floor(fx)));j=max(0,min(510,math.floor(fz)));u=fx-i;t=fz-j
  a,b,c,d=map(float,[h[j,i],h[j,i+1],h[j+1,i],h[j+1,i+1]])
  return a+(b-a)*u+(c-a)*t if u+t<=1 else d+(c-d)*(1-u)+(b-d)*(1-t)
 bpy.ops.wm.open_mainfile(filepath=str(v4.SOURCE),load_ui=False);v4.setup();edited={}
 for o in bpy.data.objects:
  if o.type!='MESH' or not o.name.startswith(('Road_','RoadSeams_')):continue
  inv=o.matrix_world.inverted();count=0
  for vert in o.data.vertices:
   p=o.matrix_world@vert.co;x,z=p.x,-p.y
   if not(-62.5<x<-49.5 and 51.5<z<62.5):continue
   delta=v4.height(x,z)-height(before,x,z)
   if abs(delta)>.000001:vert.co=inv@Vector((p.x,p.y,p.z+delta));count+=1
  if count:o.data.update();edited[o.name]=count
 bpy.context.scene['homeAccessRepair']='2026-09-15 local approach reprojection'
 bpy.ops.wm.save_as_mainfile(filepath=str(v4.SOURCE));v4.export()
 (R/'outputs/home-access/mesh-repair.json').write_text(json.dumps(edited,indent=2));print('HOME_APPROACH_MESH_PASS',edited)
if __name__=='__main__':
 args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
 {'terrain':terrain,'blend':blend}[args[0]]()
