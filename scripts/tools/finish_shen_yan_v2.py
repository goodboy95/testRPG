"""Saved-source revision 5: fringe fit, visible cuff clearance and static density."""
import bpy,bmesh,math,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import apply
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision')!=4:raise RuntimeError('Expected revision 4')
col=bpy.data.collections['ShenYan_Artwork']
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co/=factor
hair=bpy.data.objects['Hair']
# The first surface is the scalp (20 x 72); following nine patches are swept fringe.
assert len(hair.data.vertices)==1440+21*91
for v in hair.data.vertices:
    if v.index<1440:
        row=v.index//72;phi=(v.index%72)*math.tau/72
        v.co.z+=.010*max(0,math.cos(phi))*(row/19)**8
    elif v.index<1440+9*91:
        index=(v.index-1440)//91;t=((v.index-1440)%91//7)/12
        v.co.z-=(.008+.025*index/8)*t**2
        v.co.y-=.0025*t**2
bm=bmesh.new();bm.from_mesh(hair.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(hair.data);bm.free()
skin=bpy.data.objects['SkinVisible']
for v in skin.data.vertices:
    x,y,z=v.co
    if abs(x)>.21 and z>1.055:
        side=1 if x>0 else -1;axis=Vector((side*.456*.91,0,-.89)).normalized();center=Vector((side*.319*.91,-.030,1.105))
        along=(v.co-center).dot(axis);shrink=max(0,min(1,(.030-along)/.035))*.20;foot=center+axis*along;v.co=foot+(v.co-foot)*(1-shrink)
    if y<-.10 and 1.574<z<1.597:v.co.z+=.0007*math.exp(-((abs(x)-.030)/.016)**4)*math.exp(-((z-1.582)/.010)**2)
for o in col.objects:
    if o.type!='MESH':continue
    old=o.data.color_attributes.get('SurfaceRoughness')
    if old:o.data.color_attributes.remove(old)
    a=o.data.color_attributes.new(name='SurfaceRoughness',type='FLOAT_COLOR',domain='CORNER')
    value=.53 if o.data.materials[0].name=='Shen_SkinEyes' else (.59 if o.data.materials[0].name=='Shen_HairBrows' else .82)
    if o.name.startswith(('Iris_','EyeWhite')):value=.22
    for c in a.data:c.color=(value,value,value,1)
m=hair.modifiers.new('Static hair density','DECIMATE');m.ratio=.75;apply(hair,m)
height=max(v.co.z for o in col.objects if o.type=='MESH' for v in o.data.vertices)
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
bpy.context.scene['refine_revision']=5
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
total=0
for o in col.objects:
    if o.type=='MESH':o.data.calc_loop_triangles();total+=len(o.data.loop_triangles)
assert total<=32000,total
print('SHEN_FINISH_PASS triangles='+str(total))
props=bpy.ops.export_scene.gltf.get_rna_type().properties
for p in props:
    if any(w in p.identifier for w in ('color','tangent','material','image','texture','apply','selection','attribute')):
        print(p.identifier, [(e.identifier,e.name) for e in p.enum_items] if p.type=='ENUM' else p.type)
