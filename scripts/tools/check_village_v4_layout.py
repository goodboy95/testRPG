"""Independent 0.5 m capsule footprint flood-fill and road/lot survey. No asset edits."""
from pathlib import Path
import json,collections,math
import numpy as np
import village_geometry as g
R=Path(__file__).resolve().parents[2]
def check():
 d=json.loads((R/'assets/generated/village/layout-v4.json').read_text());v=d['geometry'];x,z=np.meshgrid(np.arange(-103,104,.5),np.arange(-111,104,.5));blocked=np.zeros(x.shape,dtype=bool)
 def box(cx,cz,w,depth):return (np.abs(x-cx)<w/2+.30)&(np.abs(z-cz)<depth/2+.30)
 for b in v['buildings']:
  a=math.radians(b['yaw']);dx=x-b['position'][0];dz=z-b['position'][2];lx=dx*math.cos(a)-dz*math.sin(a);lz=dx*math.sin(a)+dz*math.cos(a)
  blocked|=(np.abs(lx)<b['size'][0]/2+.30)&(np.abs(lz)<b['size'][2]/2+.30)
  if b['kind'] in ['home','house']:
   px,pz,w,depth=g.pad(b);end=pz+depth
   blocked|=((np.abs(lx-w/2)<.4)|(np.abs(lx+w/2)<.4))&(lz>pz-.3)&(lz<end+.3)
   blocked|=(np.abs(lz-end)<.4)&(np.abs(lx)>1.1)&(np.abs(lx)<w/2+.3)
 for p in d['fixtures']['props']:
  if p['solid'] and p['kind']!='overhead':blocked|=box(p['position'][0],p['position'][2],p['size'][0],p['size'][2])
 for t in d['trees']:blocked|=box(t['x'],t['z'],.55,.55)
 blocked|=box(-28,29,2.6,2.6)
 river=np.abs(x-(49+4*np.sin(z/38)))<9
 for bridge in v['bridges']:river&=~((np.abs(x-g.stream(bridge['z']))<13)&(np.abs(z-bridge['z'])<bridge['width']/2-.3))
 blocked|=river
 def index(px,pz):return round((pz+111)*2),round((px+103)*2)
 seen=np.zeros(x.shape,dtype=bool);start=index(-56,49);q=collections.deque([start]);seen[start]=True
 while q:
  j,i=q.popleft()
  for a,b in [(j+1,i),(j-1,i),(j,i+1),(j,i-1)]:
   if 0<=a<seen.shape[0] and 0<=b<seen.shape[1] and not seen[a,b] and not blocked[a,b]:seen[a,b]=True;q.append((a,b))
 gates=[]
 for b in v['buildings']:
  px,pz,w,depth=g.pad(b);point=g.world(b,0,pz+depth-.7);j,i=index(*point)
  gates.append({'id':b['id'],'insideGate':point,'reachable':bool(seen[j,i])})
 report={'resolution':.5,'capsuleRadius':.3,'reachableCells':int(seen.sum()),'gates':gates,'note':'2D connectivity complements real engine slope/collision tests; not a physics substitute.'}
 out=R/'outputs/village-v4/layout-connectivity.json';out.write_text(json.dumps(report,indent=2));bad=[p for p in gates if not p['reachable']]
 print('LAYOUT_CONNECTIVITY',len(gates)-len(bad),'/',len(gates),bad);assert not bad
if __name__=='__main__':check()
