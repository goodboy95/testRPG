"""Saved-source revision 2: fitted coverage, coherent short hair, final static mesh budgets."""
import bpy,bmesh,math,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import mesh,colorize,apply,select,smooth,ribbon
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision')!=1:raise RuntimeError('Expected revision 1')
col=bpy.data.collections['ShenYan_Artwork'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[col.name]
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co/=factor
skin=bpy.data.objects['SkinVisible'];bm=bmesh.new();bm.from_mesh(skin.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if 1.09<f.calc_center_median().z<1.441 and abs(f.calc_center_median().x)>.048],context='FACES')
bmesh.ops.delete(bm,geom=[f for f in bm.faces if 1.09<f.calc_center_median().z<1.404],context='FACES')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(skin.data);bm.free()
def pigment(v):
    x,y,z=v;c=Vector((.43,.246,.160))
    lip=math.exp(-((x/.028)**4)-((z-1.493)/.009)**2) if y<-.07 else 0
    c=c.lerp(Vector((.38,.115,.088)),lip*.58)
    cheek=math.exp(-((abs(x)-.048)/.019)**2-((z-1.535)/.028)**2) if y<-.055 else 0
    c=c.lerp(Vector((.48,.219,.152)),cheek*.25)
    return (*c,1)
colorize(skin,pigment,bpy.data.materials['Shen_SkinEyes'])
for o in col.objects:
    if o.name.startswith('EyeWhite'):
        for v in o.data.vertices:v.co.y+=.0014
    elif o.name.startswith('Iris_'):
        side=-1 if '-1' in o.name else 1;cx=side*.0329*.91
        for v in o.data.vertices:
            v.co.x=cx+(v.co.x-cx)*1.16;v.co.z=1.5737+(v.co.z-1.5737)*1.16;v.co.y+=.00135
    elif o.name.startswith('Lace'):
        for v in o.data.vertices:v.co.z+=.020
pants=bpy.data.objects['CharcoalTrousers']
for v in pants.data.vertices:
    z=v.co.z
    if z>.74:
        t=min(1,(z-.74)/.10);v.co.y=v.co.y*(1-t)+max(-.098,min(.083,v.co.y*.65))*t
bm=bmesh.new();bm.from_mesh(pants.data)
for i in range(40):bmesh.ops.smooth_vert(bm,verts=list(bm.verts),factor=.48,use_axis_x=True,use_axis_y=True,use_axis_z=False)
bm.to_mesh(pants.data);bm.free()
for o in list(col.objects):
    if o.name=='Hair':bpy.data.objects.remove(o,do_unlink=True)
hairmat=bpy.data.materials['Shen_HairBrows']
# A continuous scalp volume follows the skull; low relief defines swept groupings.
verts=[];faces=[];N=96;R=28
for i in range(R+1):
    u=(i+.015)/(R+.015)
    for j in range(N):
        phi=j*math.tau/N
        front=max(0,math.cos(phi))
        end=1.93-.66*front
        leftbang=math.exp(-(((phi+math.pi)%(math.tau)-math.pi+.55)/.40)**2)
        end+=.24*leftbang
        theta=u*end
        relief=.0011*math.sin(phi*17+theta*5)*math.sin(theta)**2
        x=(.086+relief)*math.sin(theta)*math.sin(phi)
        y=-.044-(.104+relief)*math.sin(theta)*math.cos(phi)
        z=1.592+(.113+relief)*math.cos(theta)+.006*math.sin(phi)*math.sin(theta)
        verts.append((x*.91,y,z))
for i in range(R):
    for j in range(N):faces.append((i*N+j,i*N+(j+1)%N,(i+1)*N+(j+1)%N,(i+1)*N+j))
faces.append(tuple(reversed(range(N))))
hair=mesh('Hair',verts,faces,(.010,.006,.004),hairmat)
m=hair.modifiers.new('Hairline thickness','SOLIDIFY');m.thickness=.005;m.offset=-1;apply(hair,m)
# Sparse pointed overlapping fringe pieces, each follows the continuous cap.
for i in range(7):
    p=-.85+i*.21
    pts=[]
    for k in range(7):
        t=k/6;phi=p+.40*(1-t);theta=.5+t*(.85+.10*math.sin(i*1.7))
        pts.append((.091*.91*math.sin(theta)*math.sin(phi),-.044-.11*math.sin(theta)*math.cos(phi)-.001,1.592+.120*math.cos(theta)))
    ribbon('Fringe_'+str(i),pts,[.004,.010,.012,.012,.009,.005,.0004],.0018,(.012,.007,.0045),hairmat,6)
parts=[o for o in col.objects if o.name.startswith(('Hair','Fringe_'))];select(parts);bpy.ops.object.join();hair=bpy.context.object;hair.name='Hair'
hairmat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.72
for n in hairmat.node_tree.nodes:
    if n.type=='BSDF_PRINCIPLED':n.inputs['Specular IOR Level'].default_value=.22
# Source retains separate editable parts, export merges by the three materials.
budgets={'SkinVisible':10800,'CharcoalTrousers':3800,'OpenOvershirt':6500,'IvoryUndershirt':1200,'Hair':3900,'CanvasShoe':600,'RolledCuff':350,'EyeWhite':450,'CollarBand':350}
for o in col.objects:
    if o.type!='MESH':continue
    o.data.calc_loop_triangles()
    target=next((cap for prefix,cap in budgets.items() if o.name.startswith(prefix)),None)
    if target and len(o.data.loop_triangles)>target:
        mod=o.modifiers.new('Static surface simplification','DECIMATE');mod.ratio=target/len(o.data.loop_triangles);apply(o,mod)
    smooth(o);o['export_static']=True
height=max(v.co.z for o in col.objects if o.type=='MESH' for v in o.data.vertices)
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
bpy.context.scene['refine_revision']=2
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
count=0
for o in col.objects:
    if o.type=='MESH':o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
print('SHEN_REFINE_PASS revision=2 triangles='+str(count))
