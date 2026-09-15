"""v4 transforms saved v3 assets. init refuses an existing source; export/check read saved v4.
Roads and utility wiring are the only rebuilt meshes. Original builders are never executed.
"""
import json,math,struct,sys,tempfile,hashlib
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v3 as v3
import build_village_v1 as b
import village_geometry as g
R=Path(__file__).resolve().parents[2];SOURCE=R/'art/blender/village/village-v4.blend';GLB=R/'assets/generated/village/3d/village-v4.glb';META=GLB.with_name('village-v4-manifest.json')
LAYOUT=R/'assets/generated/village/layout-v4.json';data=json.loads(LAYOUT.read_text());v=data['geometry']
heights=np.fromfile(R/'art/terrain/village-v4/height.bin','<f4').reshape(512,512)
def height(x,z):
 fx=(x+128)*2;fz=(z+128)*2;i=max(0,min(510,math.floor(fx)));j=max(0,min(510,math.floor(fz)));u=fx-i;t=fz-j
 a,c,d,e=map(float,[heights[j,i],heights[j,i+1],heights[j+1,i],heights[j+1,i+1]])
 return a+(c-a)*u+(d-a)*t if u+t<=1 else e+(d-e)*(1-u)+(c-e)*(1-t)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def setup():
 v3.setup();b.v=v
 for key in ['concrete','mortar','dark','metal','ivory','stone']:b.mats[key]=bpy.data.materials[{'concrete':'Village_Concrete','ivory':'Village_SignIvory'}.get(key,'Village_'+key.title())]
def roads():
 for idx,r in enumerate(v['roads']):
  mesh=b.MeshBuilder();seams=b.MeshBuilder()
  for a,c in zip(r['points'],r['points'][1:]):
   dx=c[0]-a[0];dz=c[1]-a[1];length=math.hypot(dx,dz)
   if length<.01:continue
   wx=-dz/length*r['width']/2;wz=dx/length*r['width']/2
   polygon=[(a[0]+wx,a[1]+wz),(c[0]+wx,c[1]+wz),(c[0]-wx,c[1]-wz),(a[0]-wx,a[1]-wz)]
   for ix in range(math.floor(min(p[0] for p in polygon)*2),math.ceil(max(p[0] for p in polygon)*2)):
    for iz in range(math.floor(min(p[1] for p in polygon)*2),math.ceil(max(p[1] for p in polygon)*2)):
     x=ix*.5;z=iz*.5
     for tri in [[(x,z),(x,z+.5),(x+.5,z)],[(x+.5,z),(x,z+.5),(x+.5,z+.5)]]:
      pts=b.clip_polygon(tri,polygon)
      if len(pts)<3 or g.on_bridge(v,sum(p[0] for p in pts)/len(pts),sum(p[1] for p in pts)/len(pts)):continue
      mesh.face([(x,height(x,z)+.035+idx*.0005,z) for x,z in pts],b.mats[r['surface']])
   for i in range(1,int(length/3)):
    t=i*3/length;x=a[0]+dx*t;z=a[1]+dz*t
    if not g.on_bridge(v,x,z):seams.rod((x+wx,height(x+wx,z+wz)+.05+idx*.0005,z+wz),(x-wx,height(x-wx,z-wz)+.05+idx*.0005,z-wz),.006,b.mats['mortar'],4)
  mesh.finish('Road_'+r['id'])
  if seams.faces:seams.finish('RoadSeams_'+r['id'])
def wires():
 mesh=b.MeshBuilder();poles=v['utilityPoles']
 for x,z in poles:
  y=height(x,z);mesh.rod((x,y,z),(x,y+6.6,z),.085,b.mats['concrete']);mesh.box((x,y+6.25,z),(1.25,.1,.1),b.mats['metal'])
 for a,c in zip(poles,poles[1:]):
  for side in [-.45,.45]:
   points=[]
   for i in range(17):
    t=i/16;x=a[0]*(1-t)+c[0]*t;z=a[1]*(1-t)+c[1]*t;y=(height(*a)+6.45)*(1-t)+(height(*c)+6.45)*t-.8*math.sin(math.pi*t);points.append((x+side,y,z))
   for p,q in zip(points,points[1:]):mesh.rod(p,q,.009,b.mats['dark'],4)
 mesh.finish('UtilityPolesAndWires')
def init():
 if SOURCE.exists():raise RuntimeError('Saved v4 exists. Use export, not init.')
 bpy.ops.wm.open_mainfile(filepath=str(R/v3.SOURCE),load_ui=False);setup()
 for spec in v['buildings']:
  if spec['id']=='home':continue
  o=bpy.data.objects['Building_'+spec['id']];o.location=b.xyz(spec['position']);o.rotation_euler.z=math.radians(spec['yaw'])
 for name in ['PlazaTree','PlazaTreeSeat','VillageSquare','V2_OfficePaving']:
  bpy.data.objects[name].location+=Vector(b.xyz(data['groups']['plaza']))
 for p in data['fixtures']['props']:
  bpy.data.objects['V2_'+p['id']].location=b.xyz(p['position'])
 for name in ['OldRailway','Landmark_station_bench','Landmark_station_luggage']:
  bpy.data.objects[name].location+=Vector(b.xyz((0,-2.025,45)))
 for tree in data['trees']:
  o=bpy.data.objects[tree['id']];o.location=b.xyz((tree['x'],height(tree['x'],tree['z']),tree['z']))
 for o in list(bpy.data.objects):
  if o.name.startswith(('Road_','RoadSeams_')) or o.name=='UtilityPolesAndWires':bpy.data.objects.remove(o,do_unlink=True)
 roads();wires();bpy.context.view_layer.update()
 # Project only independent public paving onto the relocated authored surface, preserving UVs.
 for name in ['VillageSquare','V2_OfficePaving']:
  o=bpy.data.objects[name];o.data=o.data.copy();inverse=o.matrix_world.inverted()
  for vert in o.data.vertices:
   p=o.matrix_world@vert.co;world=Vector((p.x,p.y,height(p.x,-p.y)+(.045 if name=='V2_OfficePaving' else .028)));vert.co=inverse@world
  o.data.update()
 bpy.context.scene['layoutVersion']=4;bpy.context.scene['layoutSha256']=sha(LAYOUT)
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));export()
def export():
 assert bpy.context.scene['layoutSha256']==sha(LAYOUT),'Layout changed: explicitly update saved source before export.'
 summary=v3.ex.summarize('village');memory=v3.ex.texture_memory_mib('village');assert not summary['missingExternalFiles']
 v3.export_glb(GLB)
 old=v3.glb_signatures(R/v3.GLB);new=v3.glb_signatures(GLB);exceptions={'UtilityPolesAndWires','VillageSquare','V2_OfficePaving'}|{n for n in old if n.startswith(('Road_','RoadSeams_'))}
 preserved={n for n in old if n not in exceptions}
 assert all(new.get(n)==old[n] for n in preserved),'Mesh changed outside road/paving scope'
 assert memory+93.34<=384 and summary['materialCount']+3<=64 and GLB.stat().st_size<100*1024**2
 combined=summary['triangleCount']-89856+524288+679
 assert combined<=1200000,(combined,summary)
 meta={'version':4,'source':str(SOURCE.relative_to(R)),'glb':str(GLB.relative_to(R)),'sourceSha256':sha(SOURCE),'glbSha256':sha(GLB),'layoutSha256':sha(LAYOUT),'baselineSourceSha256':sha(R/v3.SOURCE),'baselineGlbSha256':sha(R/v3.GLB),'geometry':summary,'preservedMeshes':len(preserved),'replacedMeshes':sorted(exceptions),'textureMiB':memory,'combinedTextureMiB':memory+93.34,'combinedTriangles':combined,'sourceProvenance':'art/sources/cc0/village-art-v3/provenance.json'}
 META.write_text(json.dumps(meta,indent=2),encoding='utf-8');print('VILLAGE_V4_EXPORT_PASS',combined,len(preserved),flush=True)
def check():
 m=json.loads(META.read_text());assert sha(SOURCE)==m['sourceSha256'] and sha(GLB)==m['glbSha256'] and sha(LAYOUT)==m['layoutSha256']
 bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False);setup()
 for o in bpy.data.objects:
  if o.type=='MESH':assert o.data.uv_layers and all(math.isfinite(c) for vert in o.data.vertices for c in vert.co),o.name
 for spec in v['buildings']:
  if spec['id']=='home':continue
  o=bpy.data.objects['Building_'+spec['id']];assert (o.location-Vector(b.xyz(spec['position']))).length<.001
 with tempfile.TemporaryDirectory(prefix='village-v4-check-') as temp:
  p=Path(temp)/'check.glb';v3.export_glb(p);assert sha(p)==m['glbSha256'],'Reexport differs'
 print('VILLAGE_V4_CHECK_PASS readOnly=true',flush=True)
if __name__=='__main__':
 mode=sys.argv[sys.argv.index('--')+1]
 if mode=='init':init()
 elif mode=='export':bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False);setup();export()
 elif mode=='check':check()
 else:raise ValueError(mode)
