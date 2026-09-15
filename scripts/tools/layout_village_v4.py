"""Authored compact village layout. Produces the shared offline/runtime document and review plan.
This edits only v4 layout/review files, never Blender or authored Terrain3D regions.
"""
import copy,json,math,re,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import village_geometry as g
R=Path(__file__).resolve().parents[2]
DEST=R/'assets/generated/village/layout-v4.json'
OUT=R/'outputs/village-v4'
SITES={'home':(-56,40),'house02':(-71,40),'house03':(-40,40),'house04':(-71,67),
 'house05':(-56,67),'house06':(-40,67),'house07':(-71,17),'house08':(-71,-1),
 'house09':(-71,-20),'house10':(-53,-10),'house11':(-54,-33),'house12':(-4,-21),
 'house13':(18,40),'house14':(20,4),'house15':(81,40),'house16':(83,20),
 'house17':(83,1),'house18':(81,-22),'shop':(-20,16),'workshop':(-23,-4),
 'clinic':(10,16),'community':(-48,15),'station':(-29,-46),'rail_shed':(0,-41)}
GROUPS={'plaza':(-16,0,19),'shop':(-16,0,19),'workshop':(0,0,16),'station':(0,-2.025,45)}
def length(p):return sum(math.dist(a,b) for a,b in zip(p,p[1:]))
def prop_group(name):
 if name in ['Noticeboard','VillageDesk','VillageChair','PaperCabinet','CollectionBox','PlazaBench']:return 'plaza'
 if name in ['WorkTable','ToolBoard','TimberRack','PendingStool','ReadyStool','RepairCabinet','SortingBins'] or 'Workshop' in name:return 'workshop'
 return 'shop'
def render(d, filename="village-v4-site-plan.png", title="村庄 v4 · 紧凑布局 / 米"):
 OUT.mkdir(parents=True,exist_ok=True)
 im=Image.new('RGB',(1680,1540),'#eeeae0');dr=ImageDraw.Draw(im);font=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
 dr.text((50,24),title,fill='#273b34',font=font(32))
 def p(x,z):return 60+(x+104)*6,100+(z+112)*6
 dr.rectangle((*p(-104,-112),*p(104,104)),fill='#c8d0b6',outline='#607058',width=2)
 for z in range(-112,105):
  dr.line([p(g.stream(z)-4.5,z),p(g.stream(z)+4.5,z)],fill='#74a0a2',width=7)
 for b in d['geometry']['buildings']:
  rect=g.pad(b);x,z,w,h=rect;corners=[g.world(b,a,c) for a,c in [(x,z),(x+w,z),(x+w,z+h),(x,z+h)]]
  dr.polygon([p(*v) for v in corners],fill='#d8d2b8',outline='#928d75')
 for r in d['geometry']['roads']:dr.line([p(*v) for v in r['points']],fill='#f5eaca',width=round(r['width']*6),joint='curve')
 for b in d['geometry']['buildings']:
  w,h,depth=b['size'];points=[g.world(b,x,z) for x,z in [(-w/2,-depth/2),(w/2,-depth/2),(w/2,depth/2),(-w/2,depth/2)]]
  dr.polygon([p(*v) for v in points],fill='#ab8065' if b['kind']!='house' else '#53675e',outline='#33443b')
  q=p(b['position'][0],b['position'][2]);label={'home':'家','shop':'杂货','workshop':'修理','community':'活动','clinic':'诊所','station':'旧站','rail_shed':'附屋'}.get(b['id'],b['id'].replace('house',''))
  dr.text(q,label,fill='white',anchor='mm',font=font(16))
 for br in d['geometry']['bridges']:
  x=g.stream(br['z']);dr.rectangle((*p(x-13,br['z']-1.8),*p(x+13,br['z']+1.8)),fill='#b6b4a2',outline='#526559',width=2)
 for zone in d['fixtures']['zones']+d['planningZones']:
  x,z,w,h=zone['rect'];dr.rectangle((*p(x,z),*p(x+w,z+h)),outline='#34688a',width=2)
 for i,route in enumerate(d['geometry']['routes']):
  dr.line([p(*v) for v in route['points']],fill=['#bc693f','#448889','#744f72'][i],width=3)
  dr.text((1330,200+i*95),route['id']+'\n'+str(round(length(route['points']),1))+' m',fill='#293b36',font=font(18))
 dr.text((1330,550),'24 栋建筑\n住宅／院地原尺度\n家、溪流、双桥固定\n蓝框：工作净空\n\n主路 4 m\n支路 3.5 m\n巷道 2.5 m',fill='#293b36',font=font(18))
 dr.line([p(-95,94),p(-75,94)],fill='#293b36',width=4);dr.text(p(-95,97),'20 m',fill='#293b36',font=font(18))
 im.save(OUT/filename)
def make():
 old=g.read(R);v=copy.deepcopy(old);v['version']=4
 for b in v['buildings']:
  x,z=SITES[b['id']];b['position']=[x,round(g.base(x,z),5),z]
  if b['id']=='house10':b['yaw']=0
 # Public working pads remain at their original elevations, preserving every prop's relative height.
 for b in v['buildings']:
  if b['id'] in ['shop','workshop']:b['position'][1]=next(a for a in old['buildings'] if a['id']==b['id'])['position'][1]
 roads=[]
 def road(id,width,points,surface='stone'):roads.append(dict(id=id,width=width,surface=surface,points=points))
 road('home_lane',4,[[-82,55],[-56,55],[-30,53],[-28,32],[-20,24],[-8,25]],'concrete')
 road('home_gate',2.8,[[-56,51.8],[-56,55]],'concrete')
 road('south_loop',2.5,[[-82,55],[-82,29],[-71,29],[-58,29],[-58,4],[-71,4],[-71,10]])
 road('community_path',2.5,[[-37,15],[-34,15],[-34,22],[-32,25]],'concrete')
 road('north_spine',4,[[-8,25],[-8,9],[7,-7],[9,-16],[12,-35],[7,-30],[-14,-25],[-28,-33],[-28,-39]],'concrete')
 road('west_return',3.5,[[-32,25],[-34,22],[-34,7],[-36,-12],[-37,-27],[-28,-33]])
 road('creek_south',3.5,[[-8,25],[0,28],[16,28],[38,28],[66,28],[68,26]],'concrete')
 road('creek_north',3.5,[[9,-16],[22,-26],[28,-30],[38,-31],[65,-31],[68,-31]])
 road('west_bank',2.5,[[38,28],[36,6],[35,-13],[36,-31]])
 road('east_bank',2.5,[[68,26],[68,2],[68,-31],[73,-40],[80,-45]])
 road('south_return',2.5,[[16,28],[9,28],[1,46],[-15,51],[-30,53]])
 road('workshop_path',2.5,[[-23,2],[-29,11],[-34,7]],'concrete')
 road('station_court',4,[[-41,-37],[-28,-37],[-12,-34],[0,-35],[12,-35]],'concrete')
 road('west_houses',2.5,[[-60,-20],[-58,-20],[-37,-27],[-34,7]])
 road('north_house',2.5,[[-45,-33],[-37,-33],[-37,-27]])
 road('house10_entry',2.5,[[-53,.5],[-53,4],[-58,4]])
 road('house07_entry',2.5,[[-71,29],[-71,28]])
 road('house14_entry',2.5,[[20,-6.5],[20,-10],[7,-7]])
 road('east_houses',2.5,[[72.5,20],[68,20],[68,1],[72.5,1]])
 road('east_south_entry',2.5,[[81,29.5],[81,27],[68,26]])
 road('east_north_entry',2.5,[[81,-32],[81,-36],[73,-36]])
 v['roads']=roads
 home=[[-56,49],[-56,55],[-30,53],[-28,32],[-20,24]]
 creek=[[-8,25],[0,28],[16,28],[38,28],[66,28],[68,26],[68,2],[68,-31],[65,-31],[38,-31],[28,-30],[22,-26],[9,-16],[-8,9],[-12,25],[-28,32],[-30,53],[-56,55],[-56,49]]
 rail=[[-32,25],[-34,22],[-34,7],[-36,-12],[-37,-27],[-28,-33],[-28,-37],[-12,-34],[0,-35],[12,-35],[9,-16],[7,-7],[-8,9],[-8,25],[-32,25]]
 v['routes']=[dict(id=id,points=p) for id,p in [('home_shop',home),('creek_loop',creek),('rail_loop',rail)]]
 for l in v['landmarks']:
  k='station' if l['id'].startswith('station') else 'plaza' if l['id'] in ['plaza_tree','noticeboard'] else 'shop';dx,dy,dz=GROUPS[k];l['position']=[a+b for a,b in zip(l['position'],[dx,dy,dz])]
 v['utilityPoles']=[[-84,29],[-82,58],[-31,60],[-30,37],[3,26],[-40,-13],[-40,-35],[-17,-50]]
 ext=json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";', (R/'scripts/presentation/VillageExteriorLayout3D.cs').read_text(),re.S)[1])
 groups={}
 for p in ext['props']:
  k=prop_group(p['id']);groups[p['id']]=k;p['position']=[a+b for a,b in zip(p['position'],GROUPS[k])]
 for z in ext['zones']:
  k='workshop' if 'work' in z['id'].lower() or 'stool' in z['id'].lower() else 'shop' if 'load' in z['id'].lower() or 'shop' in z['id'].lower() else 'plaza';dx,_,dz=GROUPS[k];z['rect'][0]+=dx;z['rect'][1]+=dz
 for r in ext['routes']:
  k='workshop' if r['id'] in ['workbench','stool'] else 'shop' if r['id']=='delivery' else 'plaza';dx,_,dz=GROUPS[k];r['points']=[[x+dx,z+dz] for x,z in r['points']]
 ext['version']=4
 data={'version':4,'geometry':v,'fixtures':ext,'groups':GROUPS,'propGroups':groups,'trees':[],
       'planningZones':[{'id':'sparring_with_border','rect':[-53.5,21.5,11,11]},{'id':'forest_return','rect':[78,-49,4,4]}]}
 # Preserve tree identities; relocate only trees overlapping new houses/roads/work areas.
 for i,(x,z,height,variant) in enumerate(g.trees(old)):
  def clear(x,z):return not any(g.outside(g.local(b,x,z),g.pad(b))<2 for b in v['buildings']) and not any(g.segment_distance((x,z),a,b)<r['width']/2+2 for r in roads for a,b in zip(r['points'],r['points'][1:])) and not any(g.outside((x,z),q['rect'])<2 for q in ext['zones']+data['planningZones']) and math.hypot(x+28,z-29)>8
  ox,oz=x,z
  if not clear(x,z):
   for step in range(1,70):
    found=False
    for k in range(16):
     tx=ox+step*1.5*math.cos(k*math.tau/16);tz=oz+step*1.5*math.sin(k*math.tau/16)
     if -97<tx<98 and -105<tz<95 and abs(tx-g.stream(tz))>15 and clear(tx,tz) and all(math.hypot(tx-t['x'],tz-t['z'])>4 for t in data['trees']):x,z=tx,tz;found=True;break
    if found:break
  data['trees'].append(dict(id=f'Tree_{i:03}',x=round(x,4),z=round(z,4),height=height,variant=variant))
 DEST.parent.mkdir(parents=True,exist_ok=True);DEST.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');render(data)
 table=['# v4 建筑位置与院地登记','\n单位米；Godot X/Y/Z，朝向单位度。','\n| 建筑 | 原位置 | 新位置 | 原/新朝向 | 院地局部矩形 |','|---|---|---|---|---|']
 for a,b in zip(old['buildings'],v['buildings']):table.append(f"| {b['id']} | {a['position']} | {b['position']} | {a['yaw']} / {b['yaw']} | {g.pad(b)} |")
 (OUT/'layout-register.md').write_text('\n'.join(table),encoding='utf-8')
 print(json.dumps({r['id']:round(length(r['points']),2) for r in v['routes']}))
 print('PROPS',groups,'ZONES',ext['zones'])
if __name__=='__main__':make()
