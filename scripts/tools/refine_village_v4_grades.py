"""Explicit terrain edit during v4 graybox authoring. Backs up current float maps first.
Smooth new soil approaches while locking fixed anchors and occupied pads. Not an initializer.
"""
from pathlib import Path
from datetime import datetime
import json,hashlib
import numpy as np
from prepare_village_v4_terrain import distance,path_dist,smooth
R=Path(__file__).resolve().parents[2];W=R/'art/terrain/village-v4';D=R/'assets/generated/village/terrain-v4'
def refine():
 data=json.loads((R/'assets/generated/village/layout-v4.json').read_text());v=data['geometry'];h=np.fromfile(W/'height.bin','<f4').reshape(512,512);old=h.copy()
 backup=W/'edits'/datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True);(backup/'height.bin').write_bytes((W/'height.bin').read_bytes())
 x,z=np.meshgrid(np.arange(512)*.5-128,np.arange(512)*.5-128);locked=np.fromfile(W/'protected.bin','u1').reshape(512,512).astype(bool)
 for b in v['buildings']:locked|=distance(x,z,b)<.1
 for p in data['fixtures']['props']:
  if p['kind']!='overhead':
   px,_,pz=p['position'];w,_,d=p['size'];locked|=(np.abs(x-px)<w/2+.2)&(np.abs(z-pz)<d/2+.2)
 influence=np.zeros(h.shape)
 for road in v['roads']:influence=np.maximum(influence,1-smooth((path_dist(x,z,road['points'])-road['width']/2)/8))
 editable=(influence>.001)&~locked
 for _ in range(180):
  blurred=(np.roll(h,1,0)+np.roll(h,-1,0)+np.roll(h,1,1)+np.roll(h,-1,1))/4
  h[editable]=blurred[editable]
 h.astype('<f4').tofile(W/'height.bin')
 manifest=json.loads((D/'manifest.json').read_text());manifest['files']['height.bin']=hashlib.sha256((W/'height.bin').read_bytes()).hexdigest();manifest['gradeRefinement']={'backup':str(backup.relative_to(R)),'iterations':180,'changedSamples':int((h!=old).sum())};(D/'manifest.json').write_text(json.dumps(manifest,indent=2))
 print('V4_GRADE_REFINED',manifest['gradeRefinement'])
if __name__=='__main__':refine()
