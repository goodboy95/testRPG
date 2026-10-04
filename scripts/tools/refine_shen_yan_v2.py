"""Saved-source revision 1: tailored shirt topology, eye fit, cloth and hair volume."""
import bpy,bmesh,math,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import mesh,colorize,smooth,select,apply,ribbon,skin_color
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision',0)>=1:raise RuntimeError('Revision already applied')
model=bpy.data.collections['ShenYan_Artwork']
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[model.name]
skinmat=bpy.data.materials['Shen_SkinEyes'];cloth=bpy.data.materials['Shen_Wardrobe'];hairmat=bpy.data.materials['Shen_HairBrows']
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for o in model.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co/=factor
for o in list(model.objects):
    if o.name.startswith(('OpenOvershirt','IvoryUndershirt','Collar_','RolledCuff_','BreastPocket','ShirtButton_','Eye_','Iris_')):
        bpy.data.objects.remove(o,do_unlink=True)

def interpolate(z,rows):
    for a,b in zip(rows,rows[1:]):
        if a[0]<=z<=b[0]:
            t=(z-a[0])/(b[0]-a[0]);return tuple(a[i]*(1-t)+b[i]*t for i in range(1,len(a)))
    return rows[0][1:] if z<rows[0][0] else rows[-1][1:]

# Regular garment loops define the hem and front edges independently of body anatomy.
zs=[.891,.900,.928,.97,1.03,1.09,1.15,1.21,1.25,1.29,1.325,1.35,1.37,1.392,1.412,1.418]
rows=[(.89,.195,.115),(.97,.18,.110),(1.10,.185,.121),(1.23,.219,.119),(1.325,.235,.099),(1.35,.225,.080),(1.37,.195,.069),(1.392,.112,.064),(1.418,.067,.062)]
n=48;verts=[];faces=[]
for z in zs:
    rx,ry=interpolate(z,rows)
    gap=.034+.013*max(0,(z-1.17)/.248)
    a0=math.asin(min(.92,gap/rx))
    for j in range(n+1):
        a=a0+(math.tau-2*a0)*j/n
        fold=.0023*math.cos(a*9+z*7)*math.sin(math.pi*(z-.89)/.53)
        verts.append(((rx+fold)*math.sin(a),-(ry+fold*.5)*math.cos(a)-.012,z))
for i in range(len(zs)-1):
    for j in range(n):faces.append((i*(n+1)+j,i*(n+1)+j+1,(i+1)*(n+1)+j+1,(i+1)*(n+1)+j))
shirt=mesh('OpenOvershirt',verts,faces,(.145,.207,.263),cloth)
bm=bmesh.new();bm.from_mesh(shirt.data)
holes=[f for f in bm.faces if abs(f.calc_center_median().x)>.14 and ((f.calc_center_median().z-1.294)/.088)**2+((f.calc_center_median().y+.012)/.086)**2<1]
bmesh.ops.delete(bm,geom=holes,context='FACES')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
for sign in(-1,1):
    boundary=[e for e in bm.edges if e.is_boundary and all(v.co.x*sign>.09 and 1.17<v.co.z<1.405 for v in e.verts)]
    start=boundary[0].verts[0];ordered=[start];current=start;prior=None
    while True:
        candidate=next((e for e in current.link_edges if e in boundary and e!=prior),None)
        if not candidate:raise RuntimeError('Open armhole')
        nxt=candidate.other_vert(current)
        if nxt==start:break
        ordered.append(nxt);current=nxt;prior=candidate
        if len(ordered)>300:raise RuntimeError('Armhole traversal')
    angles=[math.atan2((v.co.z-1.294)/.088,(v.co.y+.012)/.086) for v in ordered]
    previous=ordered
    for center,ry,rz in [((sign*.251,-.012,1.278),.074,.076),((sign*.272,-.015,1.228),.068,.068),((sign*.296,-.023,1.164),.062,.062),((sign*.319,-.030,1.104),.056,.056),((sign*.321,-.031,1.098),.057,.057)]:
        ring=[]
        for a in angles:
            ring.append(bm.verts.new(Vector(center)+Vector((sign*.89,0,.456))*math.sin(a)*rz+Vector((0,1,0))*math.cos(a)*ry))
        for j in range(len(ring)):bm.faces.new((previous[j],previous[(j+1)%len(ring)],ring[(j+1)%len(ring)],ring[j]))
        previous=ring
bm.normal_update();bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(shirt.data);bm.free()
m=shirt.modifiers.new('Tailoring subdivision','SUBSURF');m.levels=2;apply(shirt,m)
colorize(shirt,(.145,.207,.263),cloth)
m=shirt.modifiers.new('Hem thickness','SOLIDIFY');m.thickness=.002;m.use_rim_only=True;apply(shirt,m)

# Crewneck undershirt as its own continuously lofted shell.
zs=[.934,.94,1.04,1.16,1.27,1.32,1.35,1.365]
rows=[(.934,.165,.100),(1.16,.181,.11),(1.27,.190,.096),(1.32,.17,.088),(1.35,.115,.072),(1.365,.068,.060)]
verts=[];faces=[];n=48
for z in zs:
    rx,ry=interpolate(z,rows)
    for j in range(n):
        a=math.tau*j/n;verts.append((rx*math.sin(a),-ry*math.cos(a)-.012,z))
for i in range(len(zs)-1):
    for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
tee=mesh('IvoryUndershirt',verts,faces,(.61,.575,.49),cloth,1)
m=tee.modifiers.new('Knit edge','SOLIDIFY');m.thickness=.002;m.use_rim_only=True;apply(tee,m)

# Collar band around the neckline, then two folded panels.
verts=[];faces=[];n=32
for k,(rx,ry,z) in enumerate([(.068,.063,1.410),(.070,.066,1.437)]):
    for j in range(n+1):
        a=.75+(math.tau-1.5)*j/n;verts.append((rx*math.sin(a),-ry*math.cos(a)-.012,z))
for j in range(n):faces.append((j,j+1,n+2+j,n+1+j))
band=mesh('CollarBand',verts,faces,(.135,.195,.25),cloth,1)
m=band.modifiers.new('Fabric band','SOLIDIFY');m.thickness=.003;apply(band,m)
for sign in(-1,1):
    o=mesh('CollarFold_'+str(sign),[(sign*.045,-.061,1.438),(sign*.095,-.070,1.407),(sign*.105,-.131,1.347),(sign*.055,-.144,1.371),(sign*.044,-.084,1.414)],[(0,1,2,3,4)],(.155,.219,.275),cloth)
    m=o.modifiers.new('Collar thickness','SOLIDIFY');m.thickness=.003;apply(o,m)
    m=o.modifiers.new('Rolled collar edges','BEVEL');m.width=.0025;m.segments=3;apply(o,m)
    verts=[];faces=[];n=32
    axis=Vector((sign*.456,0,-.89));across=Vector((sign*.89,0,.456));center=Vector((sign*.319,-.030,1.105))
    for z,rad in [(-.020,.059),(-.016,.061),(.010,.061),(.015,.058)]:
        for j in range(n):
            a=j*math.tau/n;verts.append(tuple(center+axis*z+across*math.sin(a)*rad+Vector((0,1,0))*math.cos(a)*rad))
    for k in range(3):
        for j in range(n):faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    cuff=mesh('RolledCuff_'+str(sign),verts,faces,(.18,.25,.31),cloth,1)
    # Actual front placket follows the shape of the shirt edge.
    pts=[]
    for z in [.90,.97,1.05,1.13,1.21,1.28,1.34]:
        rx,ry=interpolate(z,[(.89,.195,.115),(.97,.18,.110),(1.10,.185,.121),(1.23,.219,.119),(1.325,.235,.099),(1.35,.225,.080)])
        x=.034+.013*max(0,(z-1.17)/.248);y=-ry*math.sqrt(1-(x/rx)**2)-.016
        pts.append((sign*x,y,z))
    ribbon('Placket_'+str(sign),pts,[.003]*len(pts),.001,(.135,.193,.248),cloth,6)
    if sign==-1:
        for i in range(5):
            x,y,z=pts[i]
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=(x-.007,y-.002,z+.025))
            o=bpy.context.object;o.name='ShirtButton_'+str(i);o.scale=(.003,.001,.003);select([o]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);colorize(o,(.34,.37,.38),cloth);smooth(o)
mesh('BreastPocket',[(-.088,-.126,1.280),(-.144,-.109,1.28),(-.145,-.117,1.221),(-.114,-.126,1.207),(-.084,-.136,1.221)],[(0,1,2,3,4)],(.146,.211,.270),cloth)

# Remove anatomical muscle/genital surface cues from the trousers, preserving the crotch topology.
pants=bpy.data.objects['CharcoalTrousers'];bm=bmesh.new();bm.from_mesh(pants.data)
for i in range(45):bmesh.ops.smooth_vert(bm,verts=list(bm.verts),factor=.42,use_axis_x=True,use_axis_y=True,use_axis_z=False)
bm.to_mesh(pants.data);bm.free()
bins={}
for j in range(15):
    z=.10+j*.05;ps=[v.co.copy() for v in pants.data.vertices if abs(v.co.z-z)<.028 and v.co.x>0]
    if ps:bins[j]=((min(v.x for v in ps)+max(v.x for v in ps))/2,(min(v.y for v in ps)+max(v.y for v in ps))/2,(max(v.x for v in ps)-min(v.x for v in ps))/2,(max(v.y for v in ps)-min(v.y for v in ps))/2)
for v in pants.data.vertices:
    x,y,z=v.co
    if z<.77:
        j=min(bins,key=lambda j:abs(.10+j*.05-z));cx,cy,rx,ry=bins[j];side=1 if x>0 else -1
        a=math.atan2((y-cy)/max(ry,.01),(abs(x)-cx)/max(rx,.01))
        targetx,targety=interpolate(z,[(.10,.052,.060),(.32,.063,.066),(.5,.066,.065),(.67,.077,.080),(.77,.084,.086)])
        wrinkle=.0016*math.sin(z*100+a*3)*math.exp(-((z-.3)/.17)**2)
        v.co.x=side*(cx+(targetx+wrinkle)*math.cos(a));v.co.y=cy+(targety+wrinkle)*math.sin(a)
colorize(pants,(.043,.052,.060),cloth)

# Explicit eye spheres replace upstream object-transform dependencies.
for side in(-1,1):
    cx=side*.0329;cz=1.5737
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=.0122,location=(cx,-.12182,cz))
    eye=bpy.context.object;eye.name='EyeWhite_'+str(side);select([eye]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);colorize(eye,(.73,.715,.66),skinmat);smooth(eye)
    verts=[(cx,-.13412,cz)];faces=[];n=40
    for r in [.0022,.0025,.0046,.0054]:
        for j in range(n):
            a=j*math.tau/n;verts.append((cx+r*math.cos(a),-.12182-math.sqrt(.0123**2-r*r)-.00012,cz+r*math.sin(a)))
    for j in range(n):faces.append((0,1+(j+1)%n,1+j))
    for k in range(3):
        for j in range(n):faces.append((1+k*n+j,1+k*n+(j+1)%n,1+(k+1)*n+(j+1)%n,1+(k+1)*n+j))
    iris=mesh('Iris_'+str(side),verts,faces,(.06,.030,.012),skinmat)
    attr=iris.data.color_attributes['Pigment']
    for face in iris.data.polygons:
        for li in face.loop_indices:
            co=iris.data.vertices[iris.data.loops[li].vertex_index].co;r=math.hypot(co.x-cx,co.z-cz)
            attr.data[li].color=(.003,.002,.001,1) if r<=.0025 else ((.040,.025,.012,1) if r>.0047 else (.090,.047,.021,1))

# Weld the intersecting scalp/lock masses, then refine the solid silhouette.
locks=[o for o in model.objects if o.name.startswith(('HairScalp','Lock'))]
select(locks);bpy.ops.object.join();hair=bpy.context.object;hair.name='Hair'
rem=hair.modifiers.new('Unified hair volume','REMESH');rem.mode='VOXEL';rem.voxel_size=.0018;rem.use_smooth_shade=True;apply(hair,rem)
mod=hair.modifiers.new('Soften lock ridges','SMOOTH');mod.factor=.75;mod.iterations=9;apply(hair,mod)
hair.data.calc_loop_triangles();dec=hair.modifiers.new('Hair contour retessellation','DECIMATE');dec.ratio=min(1,5200/len(hair.data.loop_triangles));apply(hair,dec)
colorize(hair,(.020,.013,.010),hairmat);smooth(hair)
hairmat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.68
for mat in [skinmat,cloth,hairmat]:
    for n in mat.node_tree.nodes:
        if n.type=='BUMP':n.inputs['Strength'].default_value=.06;n.inputs['Distance'].default_value=.00015

# Gentle global width edit yields a leaner young-adult silhouette, including coherent eye spacing.
for o in model.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co.x*=.91
        o['export_static']=True
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
        o.data.calc_loop_triangles();print('PART',o.name,len(o.data.loop_triangles))
height=max(v.co.z for o in model.objects if o.type=='MESH' for v in o.data.vertices)
for o in model.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
bpy.context.scene['refine_revision']=1
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
print('SHEN_REFINE_PASS revision=1')
