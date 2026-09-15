"""Initial v4 derivative from an export of the currently saved native terrain.
Never overwrite an authored v4. Runtime textures/materials reference the adopted CC0 originals.
"""
from pathlib import Path
import json,shutil,hashlib,math
import numpy as np
import village_geometry as g
R=Path(__file__).resolve().parents[2];D=R/'assets/generated/village/terrain-v4';W=R/'art/terrain/village-v4'
def smooth(t):t=np.clip(t,0,1);return t*t*(3-2*t)
def distance(x,z,b):
 a=math.radians(b['yaw']);dx=x-b['position'][0];dz=z-b['position'][2];lx=dx*math.cos(a)-dz*math.sin(a);lz=dx*math.sin(a)+dz*math.cos(a)
 px,pz,w,d=g.pad(b);return np.maximum.reduce([px-lx,lx-px-w,pz-lz,lz-pz-d])
def path_dist(x,z,points):
 out=np.full(x.shape,1e6)
 for a,b in zip(points,points[1:]):
  dx=b[0]-a[0];dz=b[1]-a[1];t=np.clip(((x-a[0])*dx+(z-a[1])*dz)/(dx*dx+dz*dz),0,1)
  out=np.minimum(out,np.hypot(x-a[0]-t*dx,z-a[1]-t*dz))
 return out
def prepare():
 if (D/'authored.json').exists():raise RuntimeError('v4 already authored; use native editor and export/check.')
 data=json.loads((R/'assets/generated/village/layout-v4.json').read_text());v=data['geometry'];old=g.read(R)
 x,z=np.meshgrid(np.arange(512)*.5-128,np.arange(512)*.5-128)
 baseline=np.fromfile(W/'baseline/height.bin','<f4').reshape(512,512);h=baseline.copy()
 control=np.fromfile(W/'baseline/control.bin','<u4').reshape(512,512).copy();color=np.fromfile(W/'baseline/color.bin','u1').reshape(512,512,4).copy()
 protected=(distance(x,z,old['buildings'][0])<.8)|((x>=-74)&(x<=-39)&(z>=52)&(z<=58))|(np.abs(x-(49+4*np.sin(z/38)))<17)
 affected=np.zeros(x.shape);neutral=(40-z)*.045-2.6*(1-smooth((np.abs(x-(49+4*np.sin(z/38)))-4.5)/4))
 for b in old['buildings'][1:]+v['buildings'][1:]:affected=np.maximum(affected,1-smooth(distance(x,z,b)/5))
 for road in old['roads']+v['roads']:affected=np.maximum(affected,1-smooth((path_dist(x,z,road['points'])-road['width']/2)/3))
 h=h*(1-affected)+neutral*affected
 for b in v['buildings']:
  t=1-smooth(distance(x,z,b)/3);h=h*(1-t)+b['position'][1]*t
 # Transfer the authored plaza, including its work surfaces and local wear, to the new plaza.
 dx,_,dz=data['groups']['plaza'];sx=x-dx;sz=z-dz
 rectangle=np.maximum.reduce([-25-sx,sx-3,-sz,sz-21]);weight=1-smooth(rectangle/3)
 ix=np.clip(np.rint((sx+128)*2).astype(int),0,511);iz=np.clip(np.rint((sz+128)*2).astype(int),0,511)
 h=h*(1-weight)+baseline[iz,ix]*weight
 # Whole shop/repair pads and fixture groups retain their original relative elevations.
 for p in data['fixtures']['props']:
  if p['kind']=='overhead':continue
  px,py,pz=p['position'];w,_,depth=p['size'];outside=np.maximum(np.abs(x-px)-w/2,np.abs(z-pz)-depth/2)
  # Most props sit on the original authored ground; transfer that ground, not their bottoms.
  k=data['propGroups'][p['id']];tx,_,tz=data['groups'][k]
  srcx=np.clip(np.rint((x-tx+128)*2).astype(int),0,511);srcz=np.clip(np.rint((z-tz+128)*2).astype(int),0,511)
  t=1-smooth(outside/1.5);h=h*(1-t)+baseline[srcz,srcx]*t
 h[protected]=baseline[protected]
 changed=(affected>.001)&~protected;control[changed]=1<<22 # Retain adopted macro colour / neutral roughness.
 # Maintained dirt shoulders. Terrain3D control: base bits 27..31, overlay 22..26, blend 14..21.
 for road in v['roads']:
  d=path_dist(x,z,road['points'])-road['width']/2;t=1-smooth(np.maximum(d,0)/1.8)
  use=(t>.01)&~protected;control[use]=(1<<22)|(np.rint(t[use]*220).astype('uint32')<<14)
 use=(weight>.99)&~protected;control[use]=np.fromfile(W/'baseline/control.bin','<u4').reshape(512,512)[iz[use],ix[use]]
 color[use]=np.fromfile(W/'baseline/color.bin','u1').reshape(512,512,4)[iz[use],ix[use]]
 W.mkdir(parents=True,exist_ok=True);h.astype('<f4').tofile(W/'height.bin');control.astype('<u4').tofile(W/'control.bin');color.tofile(W/'color.bin');protected.astype('u1').tofile(W/'protected.bin')
 olddir=R/'assets/generated/village/terrain-sample-v1';D.mkdir(parents=True,exist_ok=True)
 shutil.copytree(olddir/'data',D/'data',dirs_exist_ok=True)
 text=(olddir/'terrain.tscn').read_text(encoding='utf-8').replace('terrain-sample-v1/data','terrain-v4/data');(D/'terrain.tscn').write_text(text,encoding='utf-8')
 details=json.loads((olddir/'details.json').read_text());kept=[]
 for p in details:
  q=dict(p)
  if -25<=p['x']<=3 and 0<=p['z']<=21:q['x']+=dx;q['z']+=dz
  if any(g.outside(g.local(b,q['x'],q['z']),g.pad(b))<.5 for b in v['buildings']):continue
  if any(g.segment_distance((q['x'],q['z']),a,b)<r['width']/2+.25 for r in v['roads'] for a,b in zip(r['points'],r['points'][1:])):continue
  kept.append(q)
 (D/'details.json').write_text(json.dumps(kept,indent=2),encoding='utf-8')
 def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
 meta={'version':4,'spacing':.5,'origin':[-128,-128],'dimensions':[512,512],'source':'currently saved terrain-sample-v1 native export','protectedSamples':int(protected.sum()),'changedSamples':int((np.abs(h-baseline)>.0001).sum()),'detailInstances':len(kept),'files':{n:sha(W/n) for n in ['height.bin','control.bin','color.bin','protected.bin']}}
 (D/'manifest.json').write_text(json.dumps(meta,indent=2),encoding='utf-8');print('V4_TERRAIN_PREPARED',meta['protectedSamples'],meta['changedSamples'])
if __name__=='__main__':prepare()
