"""One-time saved-artwork revision 4: cloth clearance and swept short hair.

Not an initializer: refuses any source other than revision 3. Material bake must
be repeated after editing. The source Blend remains the editable authority.
"""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).parent))
from author_shen_yan_v2 import mesh, colorize, smooth, select

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
if scene.get('refine_revision') != 3:
    raise RuntimeError('Expected saved revision 3; do not reapply this editing operation.')
col = bpy.data.collections['ShenYan_Artwork']
bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[col.name]
factor = max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for obj in col.objects:
    if obj.type == 'MESH':
        for v in obj.data.vertices: v.co /= factor

skin = bpy.data.objects['SkinVisible']
for v in skin.data.vertices:
    x,y,z = v.co
    if z < 1.448 and abs(x) < .11:
        # The neckline remains continuous; covered neck vertices tuck inside the shirt.
        t = max(0, min(1, (1.448-z)/.045))
        back = max(0, min(1, (y+.050)/.05))
        v.co.x *= 1-.26*t
        v.co.y = -.035 + (y+.035)*(1-.52*t*back)
    if abs(x) > .21 and z > 1.055:
        side = 1 if x > 0 else -1
        axis = Vector((side*.456*.91,0,-.89)).normalized()
        center = Vector((side*.319*.91,-.030,1.105))
        along = (v.co-center).dot(axis)
        shrink = max(0,min(1,(.028-along)/.04))*.35
        foot = center+axis*along
        v.co = foot+(v.co-foot)*(1-shrink)
    if y < -.10 and 1.558 < z < 1.597:
        eye = math.exp(-((abs(x)-.030)/.016)**4)
        if z > 1.574: v.co.z -= .0015*eye*math.exp(-((z-1.582)/.010)**2)
        else: v.co.z += .0006*eye*math.exp(-((z-1.568)/.008)**2)

def pigment(p):
    x,y,z = p
    c = Vector((.40,.232,.156))
    if y < -.09:
        lip = math.exp(-((x/.030)**6)-((z-1.484)/.010)**2)
        c = c.lerp(Vector((.32,.12,.102)),lip*.55)
        cheek = math.exp(-((abs(x)-.047)/.024)**2-((z-1.535)/.03)**2)
        c = c.lerp(Vector((.43,.205,.146)),cheek*.22)
    return (*c,1)
colorize(skin,pigment,bpy.data.materials['Shen_SkinEyes'])

for o in list(col.objects):
    if o.name == 'Hair': bpy.data.objects.remove(o,do_unlink=True)
hairmat=bpy.data.materials['Shen_HairBrows']
verts=[];faces=[];N=72;R=19
for i in range(R+1):
    u=(i+.02)/(R+.02)
    for j in range(N):
        phi=j*math.tau/N
        front=max(0,math.cos(phi));back=max(0,-math.cos(phi))
        end=1.89-.57*front-.08*back
        theta=u*end
        wave=.00065*math.sin(phi*23+theta*7)*math.sin(theta)**2
        x=(.079+wave)*math.sin(theta)*math.sin(phi)
        y=-.044-(.099+wave)*math.sin(theta)*math.cos(phi)
        z=1.596+(.104+wave)*math.cos(theta)
        verts.append((x,y,z))
for i in range(R):
    for j in range(N): faces.append((i*N+j,i*N+(j+1)%N,(i+1)*N+(j+1)%N,(i+1)*N+j))
faces.append(tuple(reversed(range(N))))
cap=mesh('Hair',verts,faces,(.011,.008,.006),hairmat)

def lock(name,controls,width,bulge,color):
    points=[Vector(p) for p in controls];vs=[];fs=[];rows=12;across=6
    def bez(t):
        return points[0]*(1-t)**3+points[1]*3*t*(1-t)**2+points[2]*3*t*t*(1-t)+points[3]*t**3
    for i in range(rows+1):
        t=i/rows;p=bez(t)
        tangent=(bez(min(1,t+.005))-bez(max(0,t-.005))).normalized()
        normal=Vector((p.x/.079,(p.y+.044)/.099,(p.z-1.596)/.104)).normalized()
        side=tangent.cross(normal).normalized();normal=side.cross(tangent).normalized()
        w=width*(.30+.83*math.sin(math.pi*t)**.6)*(1-t**8)+.00012
        for j in range(across+1):
            s=-1+2*j/across
            ridge=(max(0,1-s*s)**1.4)*bulge*math.sin(math.pi*t)**.4
            groove=.00025*math.cos(s*math.pi*3)*math.sin(math.pi*t)
            vs.append(tuple(p+side*s*w+normal*(ridge+groove)))
    for i in range(rows):
        for j in range(across):
            a=i*(across+1)+j;fs.append((a,a+1,a+across+2,a+across+1))
    return mesh(name,vs,fs,color,hairmat)

# Broad swept groups cross the forehead diagonally; unequal length and width break repetition.
for i in range(9):
    t=i/8
    root=(.025+.016*t,-.010-.074*t,1.701-.020*t)
    mid1=(.008-.022*t,-.064-.052*t,1.706-.026*t)
    mid2=(-.052+.049*t,-.111-.032*t,1.660-.018*t)
    tip=(-.071+.080*t,-.091-.058*t,1.605+.029*t+.004*math.sin(i*1.83))
    lock('SweptLock_'+str(i),[root,mid1,mid2,tip],.009+.003*math.sin(i*1.9)**2,.0024,(.014,.0095,.0065))
# The short side follows the part toward the right temple.
for i in range(5):
    t=i/4
    lock('ShortSide_'+str(i),[(.041,-.073+.095*t,1.684),(.068,-.079+.09*t,1.675),(.080,-.075+.087*t,1.626),(.073,-.073+.086*t,1.569+.022*t)],.009,.0017,(.012,.008,.0055))
# Crown/back locks follow the head rather than standing as a flat cap.
for i in range(7):
    t=i/6;x=-.060+.117*t
    lock('CrownLock_'+str(i),[(.026,.008,1.694),(x,.040,1.686),(x*.98,.064,1.62),(x*.91,.050,1.568+.006*math.sin(i))],.0085,.0016,(.012,.0085,.006))
parts=[o for o in col.objects if o.name.startswith(('Hair','SweptLock_','ShortSide_','CrownLock_'))]
select(parts);bpy.ops.object.join();hair=bpy.context.object;hair.name='Hair'
# Replace baked nodes with explicit authoring inputs before the next bake.
for name,rough,scale,dist in [('Shen_SkinEyes',.53,195,.00022),('Shen_Wardrobe',.82,300,.00045),('Shen_HairBrows',.59,230,.00025)]:
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');links.new(bs.outputs[0],output.inputs[0])
    pigment_node=nodes.new('ShaderNodeVertexColor');pigment_node.layer_name='Pigment';links.new(pigment_node.outputs[0],bs.inputs['Base Color'])
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=scale;noise.inputs['Detail'].default_value=2
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.14;bump.inputs['Distance'].default_value=dist
    links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=rough
for o in col.objects:
    if o.type!='MESH':continue
    smooth(o);o['export_static']=True
height=max(v.co.z for o in col.objects if o.type=='MESH' for v in o.data.vertices)
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
scene['refine_revision']=4;scene['textures_baked']=False
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
total=0
for o in col.objects:
    if o.type=='MESH':o.data.calc_loop_triangles();total+=len(o.data.loop_triangles)
print('SHEN_POLISH_PASS revision=4 triangles='+str(total))
