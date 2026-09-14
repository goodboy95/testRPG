"""Offline village v1: Blender 5.2 -- graybox|build|export|check --root .

Original rural exterior meshes. Reuses adopted CC0 maps and derives the player's
house exterior from Home v1 without editing that source or its runtime asset.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import bpy
from mathutils import Vector,Matrix,Quaternion

sys.path.insert(0,str(Path(__file__).resolve().parent))
import village_geometry as g
import remodel_manor_v3 as material_helpers
import export_environment_cinematic_samples as ex
from remodel_classroom_v6 import project_uv

SOURCE=Path('art/blender/village/village-v1.blend')
GLB=Path('assets/generated/village/3d/village-v1.glb')
MANIFEST=GLB.with_name('village-v1-manifest.json')
HOME=Path('art/blender/home/home-v1.blend')
LIMITS={'triangles':1200000,'visibleMeshes':600,'materials':64,'glbBytes':100*1024*1024,'textureMiB':384}

def xyz(p):return p[0],-p[2],p[1]

class MeshBuilder:
    def __init__(self):self.vertices=[];self.faces=[];self.materials=[];self.face_materials=[];self.uv=[]
    def face(self,points,mat,uv=None):
        if mat not in self.materials:self.materials.append(mat)
        start=len(self.vertices);self.vertices.extend(xyz(p) for p in points)
        self.faces.append(tuple(range(start,start+len(points))));self.face_materials.append(self.materials.index(mat))
        if uv is None:
            a,b,c=map(Vector,points[:3]);normal=(b-a).cross(c-a)
            axis=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=axis]
            uv=[(p[axes[0]],p[axes[1]]) for p in points]
        self.uv.extend(uv)
    def box(self,p,s,mat):
        x,y,z=p;w,h,d=(n/2 for n in s)
        for points in [
            [(-w,-h,d),(w,-h,d),(w,h,d),(-w,h,d)],[(w,-h,-d),(-w,-h,-d),(-w,h,-d),(w,h,-d)],
            [(w,-h,d),(w,-h,-d),(w,h,-d),(w,h,d)],[(-w,-h,-d),(-w,-h,d),(-w,h,d),(-w,h,-d)],
            [(-w,h,d),(w,h,d),(w,h,-d),(-w,h,-d)],[(-w,-h,-d),(w,-h,-d),(w,-h,d),(-w,-h,d)]]:
            self.face([(a+x,b+y,c+z) for a,b,c in points],mat)
    def rod(self,a,b,r,mat,sides=10):
        a=Vector(a);b=Vector(b);axis=(b-a).normalized();helper=Vector((0,1,0)) if abs(axis.y)<.95 else Vector((1,0,0))
        u=axis.cross(helper).normalized();w=axis.cross(u).normalized()
        ring=[u*math.cos(i*2*math.pi/sides)*r+w*math.sin(i*2*math.pi/sides)*r for i in range(sides)]
        for i in range(sides):
            j=(i+1)%sides;self.face([a+ring[i],a+ring[j],b+ring[j],b+ring[i]],mat)
        self.face([a+v for v in reversed(ring)],mat);self.face([b+v for v in ring],mat)
    def finish(self,name,parent=None):
        mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(self.vertices,[],self.faces)
        for mat in self.materials:mesh.materials.append(mat)
        mesh.update();layer=mesh.uv_layers.new(name='UVMap')
        for poly,mi in zip(mesh.polygons,self.face_materials):poly.material_index=mi
        for loop in mesh.loops:layer.data[loop.index].uv=self.uv[loop.vertex_index]
        obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj)
        obj.parent=parent or village
        return obj

def empty(name,p=(0,0,0),yaw=0):
    obj=bpy.data.objects.new(name,None);collection.objects.link(obj);obj.location=xyz(p);obj.rotation_euler.z=math.radians(yaw);obj.parent=village;return obj

def add_material(name,color,roughness=.85,asset=None,diffuse=False,metallic=0):
    return material_helpers.pbr('Village_'+name,color,roughness,metallic,asset,diffuse,normal_strength=.18)

def setup():
    global collection,village,mats
    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection=bpy.data.collections.new('VillageV1');bpy.context.scene.collection.children.link(collection)
    village=bpy.data.objects.new('VillageV1',None);collection.objects.link(village)
    material_helpers.root=root;material_helpers.gray=gray
    mats={
        'plaster':add_material('Plaster',(.65,.66,.59),asset='painted_plaster_wall'),
        'warm_plaster':add_material('WarmPlaster',(.68,.60,.46),asset='painted_plaster_wall'),
        'cool_plaster':add_material('CoolPlaster',(.49,.57,.53),asset='painted_plaster_wall'),
        'brick':add_material('Brick',(.40,.22,.15),asset='painted_plaster_wall'),
        'brick_light':add_material('BrickLight',(.47,.28,.19)),
        'mortar':add_material('Mortar',(.40,.42,.36)),
        'roof':add_material('Roof',(.075,.10,.105),asset='painted_plaster_wall'),
        'roof_warm':add_material('RoofWarm',(.15,.14,.115),asset='painted_plaster_wall'),
        'concrete':add_material('Concrete',(.42,.45,.41),asset='painted_plaster_wall'),
        'stone':add_material('Stone',(.36,.40,.35),asset='terrazzo_tiles'),
        'wood':add_material('Wood',(.28,.21,.12),asset='oak_veneer_01',diffuse=True),
        'dark':add_material('Dark',(.035,.05,.045)),
        'glass':add_material('WindowGlass',(.13,.23,.25),.21,metallic=.25),
        'metal':add_material('Metal',(.42,.49,.48),.43,metallic=.65),
        'red':add_material('FadedRed',(.43,.15,.12)),
        'green':add_material('FadedGreen',(.18,.31,.24)),
        'ivory':add_material('SignIvory',(.83,.82,.65)),
        'blue':add_material('TarpBlue',(.13,.28,.35)),
        'soil':add_material('GardenSoil',(.28,.25,.16),asset='painted_plaster_wall'),
        'grass':add_material('Grass',(.32,.41,.21),.99),
        'foliage0':add_material('FoliageDark',(.16,.29,.105),.98),
        'foliage1':add_material('FoliageMid',(.25,.37,.13),.98),
        'foliage2':add_material('FoliageLight',(.34,.43,.17),.98),
        'water':add_material('StreamWater',(.16,.31,.30),.24,metallic=.15),
        'mountain':add_material('DistantMountain',(.16,.24,.19),.99),
        'mountain_far':add_material('FarMountain',(.27,.34,.31),.99)
    }
    # An original, repeatable colour texture adds fine variation without a new asset dependency.
    if not gray:
        import numpy as np
        rng=np.random.default_rng(412);size=512;y,x=np.mgrid[:size,:size];noise=np.zeros((size,size))
        for cells,amplitude in [(4,.023),(16,.015),(64,.009),(256,.005)]:
            lattice=rng.random((cells+1,cells+1))-.5;lattice[-1,:]=lattice[0,:];lattice[:,-1]=lattice[:,0]
            xx=x*cells/size;yy=y*cells/size;ix=xx.astype(int);iy=yy.astype(int);u=xx-ix;t=yy-iy
            u=u*u*(3-2*u);t=t*t*(3-2*t)
            noise+=amplitude*((lattice[iy,ix]*(1-u)+lattice[iy,ix+1]*u)*(1-t)+(lattice[iy+1,ix]*(1-u)+lattice[iy+1,ix+1]*u)*t)
        pixels=np.ones((size,size,4),dtype=np.float32)
        for channel,value in enumerate((.32,.40,.24)):pixels[:,:,channel]=value+noise
        im=bpy.data.images.new('VillageOriginalGrass512',width=size,height=size);im.pixels.foreach_set(pixels.ravel());im.pack()
        node=mats['grass'].node_tree.nodes.new('ShaderNodeTexImage');node.image=im
        mats['grass'].node_tree.links.new(node.outputs['Color'],mats['grass'].node_tree.nodes['Principled BSDF'].inputs['Base Color'])

def terrain():
    a,z,w,d=v['bounds'];step=v['grid'];nx=round(w/step)+1;nz=round(d/step)+1
    verts=[(a+ix*step,g.ground(v,a+ix*step,z+iz*step),z+iz*step) for iz in range(nz) for ix in range(nx)]
    faces=[]
    for iz in range(nz-1):
        for ix in range(nx-1):
            i=iz*nx+ix;faces.extend([(i,i+nx,i+1),(i+1,i+nx,i+nx+1)])
    mesh=bpy.data.meshes.new('TerrainMesh');mesh.from_pydata([xyz(p) for p in verts],[],faces);mesh.materials.append(mats['grass']);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        p=verts[loop.vertex_index];uv.data[loop.index].uv=(p[0]/5,p[2]/5)
    for poly in mesh.polygons:poly.use_smooth=True
    obj=bpy.data.objects.new('Terrain',mesh);collection.objects.link(obj);obj.parent=village
    water=MeshBuilder()
    for zz in range(-112,104):
        water.face([(g.stream(zz)-5.2,g.base(0,zz)-.82,zz),(g.stream(zz+1)-5.2,g.base(0,zz+1)-.82,zz+1),
                    (g.stream(zz+1)+5.2,g.base(0,zz+1)-.82,zz+1),(g.stream(zz)+5.2,g.base(0,zz)-.82,zz)],mats['water'])
    water.finish('StreamWater')

def clip_polygon(subject,polygon):
    # Clockwise or counter-clockwise convex clip polygon, in X/Z coordinates.
    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(polygon,polygon[1:]+polygon[:1]));orientation=1 if area>0 else -1
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if not subject:break
        def distance(p):return orientation*((b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0]))
        result=[];previous=subject[-1];pd=distance(previous)
        for point in subject:
            dd=distance(point)
            if (dd>=-1e-7)!=(pd>=-1e-7):
                t=pd/(pd-dd);result.append((previous[0]+(point[0]-previous[0])*t,previous[1]+(point[1]-previous[1])*t))
            if dd>=-1e-7:result.append(point)
            previous=point;pd=dd
        subject=result
    cleaned=[]
    for point in subject:
        if not cleaned or math.dist(cleaned[-1],point)>1e-6:cleaned.append(point)
    if len(cleaned)>1 and math.dist(cleaned[0],cleaned[-1])<1e-6:cleaned.pop()
    if len(cleaned)<3 or abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(cleaned,cleaned[1:]+cleaned[:1])))<1e-6:return []
    return cleaned

def roads():
    for road_index,r in enumerate(v['roads']):
        builder=MeshBuilder();edge=MeshBuilder()
        for a,b in zip(r['points'],r['points'][1:]):
            length=math.dist(a,b);steps=math.ceil(length/.7);dx=(b[0]-a[0])/length;dz=(b[1]-a[1])/length;wx=-dz*r['width']/2;wz=dx*r['width']/2
            polygon=[(a[0]+wx,a[1]+wz),(b[0]+wx,b[1]+wz),(b[0]-wx,b[1]-wz),(a[0]-wx,a[1]-wz)]
            for xx in range(math.floor(min(p[0] for p in polygon)),math.ceil(max(p[0] for p in polygon))):
                for zz in range(math.floor(min(p[1] for p in polygon)),math.ceil(max(p[1] for p in polygon))):
                    for tri in [[(xx,zz),(xx,zz+1),(xx+1,zz)],[(xx+1,zz),(xx,zz+1),(xx+1,zz+1)]]:
                        points=clip_polygon(tri,polygon)
                        if len(points)<3:continue
                        center=(sum(p[0] for p in points)/len(points),sum(p[1] for p in points)/len(points))
                        if g.on_bridge(v,*center):continue
                        builder.face([(x,g.triangulated(v,x,z)+.04+road_index*.001,z) for x,z in points],mats[r['surface']])
            for i in range(steps):
                p=(a[0]+(b[0]-a[0])*i/steps,a[1]+(b[1]-a[1])*i/steps);q=(a[0]+(b[0]-a[0])*(i+1)/steps,a[1]+(b[1]-a[1])*(i+1)/steps)
                if g.on_bridge(v,(p[0]+q[0])/2,(p[1]+q[1])/2):continue
                # Thin concrete expansion joints; irregular stone seams use the same underlying walkable surface.
                if i%(5 if r['surface']=='concrete' else 2)==0:
                    c=(p[0],p[1]);hh=g.triangulated(v,*c)+.057+road_index*.001
                    edge.rod((c[0]+wx,hh,c[1]+wz),(c[0]-wx,hh,c[1]-wz),.007,mats['mortar'],4)
        builder.finish('Road_'+r['id']);edge.finish('RoadSeams_'+r['id'])
    # A modest irregular paved clearing under the village tree; no monumental plaza.
    b=MeshBuilder()
    for x in range(-17,0):
        for z in range(4,15):
            if (x+8)**2/95+(z-9)**2/42>1:continue
            b.face([(x,g.triangulated(v,x,z)+.027,z),(x,g.triangulated(v,x,z+1)+.027,z+1),
                    (x+1,g.triangulated(v,x+1,z+1)+.027,z+1),(x+1,g.triangulated(v,x+1,z)+.027,z)],mats['stone'])
    b.finish('VillageSquare')

def windows(builder,w,h,d,floors):
    for floor in range(floors):
        y=1.4+floor*2.9;winh=1.25
        for x in (-w*.30,w*.30):
            for zz in (-d/2-.018,d/2+.018):
                builder.box((x,y,zz),(1.65,winh,.045),mats['glass'])
                for xx in (x-.85,x+.85,x):builder.box((xx,y,zz+(.025 if zz>0 else -.025)),(.045,winh+.10,.06),mats['metal'])
                for yy in (y-winh/2-.025,y+winh/2+.025):builder.box((x,yy,zz),(1.75,.05,.08),mats['metal'])
                builder.box((x,y-winh/2-.10,zz),(1.88,.12,.32),mats['stone'])
        for xx in (-w/2-.025,w/2+.025):
            builder.box((xx,y,0),(.05,winh,1.6),mats['glass'])
            for zz in (-.82,.82,0):builder.box((xx,y,zz),(.07,winh+.10,.045),mats['metal'])
            for yy in (y-winh/2-.025,y+winh/2+.025):builder.box((xx,yy,0),(.08,.05,1.72),mats['metal'])

def roof(builder,w,h,d,variant):
    half=d/2+.35;ridge=h+1.35;mat=mats['roof_warm' if variant==2 else 'roof']
    for x in (-w/2,w/2):
        points=[(x,h,-d/2),(x,ridge,0),(x,h,d/2)]
        builder.face(points if x>0 else list(reversed(points)),mats['plaster'])
    for zz in (-half,half):builder.box((0,h-.03,zz),(w+.65,.17,.16),mat)
    cols=math.ceil((w+.7)/.32);rows=math.ceil(half/.28)
    for sign in (-1,1):
        for row in range(rows):
            for col in range(cols):
                x=-w/2-.35+col*(w+.7)/cols;z=row*half/rows
                for k in range(4):
                    a=x+k*(w+.7)/cols/4;b=x+(k+1)*(w+.7)/cols/4
                    def p(px,pz,phase):return px,ridge-(pz/half)*1.37+.022*math.sin(phase*math.pi),pz*sign
                    points=[p(a,z+half/rows+.018,k/4),p(b,z+half/rows+.018,(k+1)/4),p(b,z,(k+1)/4),p(a,z,k/4)]
                    builder.face(points if sign>0 else list(reversed(points)),mat)
    builder.rod((-w/2-.4,ridge+.025,0),(w/2+.4,ridge+.025,0),.12,mat,12)

def house_prototype(variant,two_storey):
    w=10;d=7;h=6.1 if two_storey else 3.5;b=MeshBuilder()
    plaster=mats[['plaster','cool_plaster','warm_plaster','plaster'][variant]]
    b.box((0,h/2,0),(w,h,d),plaster);b.box((0,.35,0),(w+.02,.7,d+.02),mats['mortar' if variant==2 else 'stone'])
    if variant==2:
        for row in range(6):
            for col in range(30):
                x=-w/2+(col+.5)*w/30+(row%2)*.13
                if x>w/2-.1:continue
                for zz in (-d/2-.015,d/2+.015):b.box((x,.07+row*.105,zz),(.29,.088,.025),mats['brick' if (row+col)%3 else 'brick_light'])
    windows(b,w,h,d,2 if two_storey else 1)
    b.box((0,1.15,d/2+.035),(1.45,2.30,.09),mats['green' if variant%2==0 else 'wood'])
    for x in (-.76,.76):b.box((x,1.18,d/2+.045),(.065,2.42,.16),mats['wood'])
    b.box((0,2.39,d/2+.045),(1.59,.065,.16),mats['wood'])
    b.box((.52,1.15,d/2+.10),(.08,.19,.03),mats['metal'])
    b.box((0,2.64,d/2+.7),(4.1,.16,1.45),mats['concrete'])
    for x in (-1.85,1.85):b.box((x,1.30,d/2+1.26),(.12,2.6,.12),mats['metal'])
    if two_storey:
        b.box((0,3.16,d/2+.64),(4.8,.20,1.30),mats['concrete'])
        for x in [i*.32-2.24 for i in range(15)]:b.rod((x,3.3,d/2+1.22),(x,4.1,d/2+1.22),.018,mats['metal'],6)
        b.rod((-2.35,4.10,d/2+1.22),(2.35,4.10,d/2+1.22),.032,mats['metal'],8)
    roof(b,w,h,d,variant)
    for x in (-4.87,4.87):b.rod((x,h-.12,-3.57),(x,.13,-3.57),.045,mats['metal'],8)
    # Cable loop, meter box, restrained house-number plaque, and simple air-conditioning unit.
    b.box((-1.32,1.72,d/2+.12),(.23,.34,.13),mats['ivory'])
    b.box((3.8,2.58,d/2+.32),(.82,.46,.50),mats['ivory'])
    for i in range(6):b.box((3.8,2.44+i*.055,d/2+.58),(.70,.018,.012),mats['metal'])
    return b

def house_yard(spec,parent):
    a,z,w,d=g.pad(spec);end=z+d;b=MeshBuilder();h=.85
    b.box((a+w/2,.025,z+d/2),(w,.10,d),mats['concrete'])
    for x in (a,a+w):
        b.box((x,h/2,z+d/2),(.18,h,d),mats['plaster']);b.box((x,h+.035,z+d/2),(.25,.07,d+.08),mats['stone'])
    side=(w-2.8)/2
    for sign in (-1,1):
        x=sign*(1.4+side/2);b.box((x,h/2,end),(side,h,.18),mats['plaster']);b.box((x,h+.035,end),(side,.07,.26),mats['stone'])
        b.box((sign*1.48,.65,end),(.27,1.3,.27),mats['brick'])
    # Low garden strip and household objects stay against walls; gate and route stay clear.
    house_depth=spec['size'][2];house_width=spec['size'][0]
    x=-house_width/2+.7;zz=house_depth/2+3.0
    b.box((x,.18,zz),(1.0,.36,2.0),mats['soil'])
    for row in range(4):
        b.rod((x-.3,.37,zz-.7+row*.45),(x,.63,zz-.7+row*.45),.055,mats['foliage1'],6)
        b.rod((x+.3,.37,zz-.7+row*.45),(x,.63,zz-.7+row*.45),.055,mats['foliage2'],6)
    b.box((house_width/2-.6,.45,house_depth/2+1.9),(.52,.9,.52),mats['blue' if spec['variant']%2 else 'green'])
    if spec['variant']%2==0:
        line_x=a+.35
        for zz in (end-1,end-4.4):b.rod((line_x,0,zz),(line_x,1.85,zz),.026,mats['metal'],8)
        b.rod((line_x,1.85,end-1),(line_x,1.85,end-4.4),.015,mats['metal'],6)
        for i in range(3):b.box((line_x,1.43,end-1.6-i*.6),(.012,.73,.38),mats[['ivory','blue','green'][i]])
    b.finish('Yard_'+spec['id'],parent)

def signs(name,text,p,size,parent,width):
    builder=MeshBuilder();builder.box(p,(width,size*1.5,.14),mats['green' if name!='station' else 'ivory']);builder.finish('SignBoard_'+name,parent)
    curve=bpy.data.curves.new('SignText_'+name,'FONT');curve.body=text;curve.align_x='CENTER';curve.align_y='CENTER';curve.size=size;curve.extrude=.004
    curve.font=bpy.data.fonts.load(str(root/'assets/fonts/source-han-sans/SourceHanSansSC-Medium.otf'))
    obj=bpy.data.objects.new('SignText_'+name,curve);collection.objects.link(obj);obj.parent=parent
    obj.location=xyz((p[0],p[1],p[2]+.085));obj.rotation_euler=(math.pi/2,0,0)
    curve.materials.append(mats['dark' if name=='station' else 'ivory'])
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False);project_uv(obj)
    # Font em sizes are not physical cap heights; fit the actual outline to the board.
    xs=[p.co.x for p in obj.data.vertices];ys=[p.co.y for p in obj.data.vertices]
    factor=min(width*.82/(max(xs)-min(xs)),size/(max(ys)-min(ys)));cx=(min(xs)+max(xs))/2;cy=(min(ys)+max(ys))/2
    for vertex in obj.data.vertices:
        vertex.co.x=(vertex.co.x-cx)*factor;vertex.co.y=(vertex.co.y-cy)*factor;vertex.co.z*=min(factor,2)
    obj.data.update();project_uv(obj)

def public_building(spec):
    w,h,d=spec['size'];kind=spec['kind'];parent=empty('Building_'+spec['id'],spec['position'],spec['yaw']);b=MeshBuilder()
    b.box((0,h/2,0),(w,h,d),mats['warm_plaster' if kind=='station' else 'plaster'])
    b.box((0,.4,0),(w+.02,.8,d+.02),mats['brick' if kind in ('station','workshop','rail_shed') else 'stone'])
    windows(b,w,h,d,1)
    doorway=3.2 if kind in ('shop','workshop') else 1.7
    b.box((0,1.2,d/2+.03),(doorway,2.4,.07),mats['dark'])
    if kind=='shop':
        b.box((0,1.1,d/2+.075),(2.9,2.2,.025),mats['glass'])
        for x in (-1.5,0,1.5):b.box((x,1.15,d/2+.11),(.06,2.3,.08),mats['metal'])
        # Sloped canvas awning with alternating faded panels and a valance.
        for i in range(12):
            a=-w/2+.6+i*(w-1.2)/12;ww=(w-1.2)/12
            b.face([(a,2.8,d/2),(a+ww,2.8,d/2),(a+ww,2.55,d/2+2.2),(a,2.55,d/2+2.2)],mats['ivory' if i%2 else 'green'])
            b.box((a+ww/2,2.45,d/2+2.2),(ww,.2,.018),mats['ivory' if i%2 else 'green'])
        for x in (-w/2+.7,w/2-.7):b.rod((x,0,d/2+2),(x,2.58,d/2+2),.035,mats['metal'])
        b.box((-4,1,d/2+.36),(1.25,1.85,.66),mats['ivory']);b.box((-4,1.2,d/2+.71),(1.06,1.32,.025),mats['glass'])
        for row in range(3):
            for col in range(5):b.rod((-4.4+col*.2,.75+row*.35,d/2+.73),(-4.4+col*.2,.99+row*.35,d/2+.73),.042,mats['green'],8)
    elif kind=='workshop':
        for i in range(17):b.box((0,.12+i*.14,d/2+.09),(3.2,.12,.07),mats['metal'])
        b.box((-4.7,1,d/2+.4),(1.2,2,.8),mats['blue'])
        for i in range(5):b.box((4.5,.09+i*.10,d/2+.45),(1.7,.08,.9),mats['wood'])
    else:
        b.box((0,1.15,d/2+.07),(doorway-.12,2.3,.09),mats['green' if kind!='station' else 'wood'])
    if kind in ('station','community'):
        b.box((0,3.1,d/2+1.4),(w+.7,.2,2.8),mats['roof'])
        for x in (-w/2+.3,w/2-.3):b.rod((x,0,d/2+2.4),(x,3.05,d/2+2.4),.07,mats['wood'])
    roof(b,w,h,d,2 if kind in ('station','rail_shed') else 0)
    b.finish('PublicShell_'+spec['id'],parent)
    names={'shop':'乔姨杂货铺','clinic':'村卫生室','workshop':'修理铺','community':'村民活动室','station':'雾山站'}
    if kind in names:signs(kind,names[kind],(0,3.1 if kind not in ('station','community') else 3.75,d/2+.12),.40 if kind!='station' else .65,parent,min(w-1,5.4))
    pad=MeshBuilder();a,z,pw,pd=g.pad(spec);pad.box((a+pw/2,-.025,z+pd/2),(pw,.1,pd),mats['concrete']);pad.finish('Forecourt_'+spec['id'],parent)

def import_home():
    allowed=lambda n: n in {'Home','Courtyard','Ceiling','Porch','PorchCanopy','PorchPillarWest','PorchPillarEast','Drainpipes','RoofGables','RoofTilesSouth','RoofTilesNorth','PorchBench','YardTap','ClothesRack','DoorFrame_entry'} or n.startswith(('Fence','Door_entry_','Plant')) or any(n==p+w or n.startswith(p+w+'_') for p in ('Wall_','Window_','Glass_') for w in ('south','north','west','east'))
    with bpy.data.libraries.load(str(root/HOME),link=False) as (source,target):target.objects=[n for n in source.objects if allowed(n)]
    loaded=[o for o in target.objects if o]
    for obj in loaded:
        if obj.name not in collection.objects:collection.objects.link(obj)
    bpy.context.view_layer.update();home=bpy.data.objects.get('Home');home.name='HomeExterior';home.parent=village
    home_spec=next(b for b in v['buildings'] if b['kind']=='home');home.location=xyz(home_spec['position']);home.rotation_euler.z=math.radians(home_spec['yaw'])
    for obj in loaded:
        if obj is not home and obj.parent is None:obj.parent=home
        if obj.type=='MESH' and obj.name.startswith('Glass_'):
            obj.data.materials.clear();obj.data.materials.append(mats['glass'])
        if obj.name=='Courtyard':obj.location.z+=.04
        if obj.name=='Porch':obj.location.z+=.035
    gate=MeshBuilder()
    for sign in (-1,1):
        gate.box((sign*2.05,.7,11.76),(1.3,1.4,.07),mats['green'])
        for i in range(9):gate.box((sign*2.05-.6+i*.15,.7,11.70),(.023,1.28,.035),mats['metal'])
    gate.finish('HomeOpenYardGate',home)
    for name in ('PlayerBed','FamilyBed','KitchenSink','Toilet','ClosedYardGate'):
        if name in bpy.data.objects:raise RuntimeError('Interior payload leaked to exterior '+name)

def buildings():
    cache={}
    for spec in v['buildings']:
        if spec['kind']=='home':continue
        if spec['kind']!='house':public_building(spec);continue
        key=(spec['variant'],spec['size'][1]>5)
        parent=empty('Building_'+spec['id'],spec['position'],spec['yaw'])
        if key not in cache:
            obj=house_prototype(*key).finish('HouseShell_'+spec['id'],parent);cache[key]=obj.data
        else:
            obj=bpy.data.objects.new('HouseShell_'+spec['id'],cache[key]);collection.objects.link(obj);obj.parent=parent
        obj.scale=(spec['size'][0]/10,spec['size'][2]/7,spec['size'][1]/(6.1 if key[1] else 3.5))
        house_yard(spec,parent)
    import_home()

def bridges_and_banks():
    for spec in v['bridges']:
        b=MeshBuilder();x=g.stream(spec['z']);z=spec['z'];y=g.base(0,z)+.06;span=spec['halfSpan'];width=spec['width']
        b.box((x,y-.16,z),(span*2,.32,width),mats['stone'])
        for xx in [x-span+i*1.6 for i in range(math.ceil(span*2/1.6)+1)]:
            for side in (-1,1):b.box((xx,y+.48,z+side*width/2),(.16,.96,.16),mats['stone'])
        for side in (-1,1):
            b.box((x,y+1,z+side*width/2),(span*2+.15,.13,.23),mats['stone'])
            b.box((x,y+.47,z+side*width/2),(span*2,.12,.15),mats['stone'])
        for xx in (x-span*.55,x+span*.55):
            b.box((xx,y-1.6,z),(1.1,3,width-.5),mats['mortar'])
        # Long deck blocks and transverse joints make the bridge read as a maintained village crossing.
        for xx in range(math.floor(x-span),math.ceil(x+span)):
            b.rod((xx,y+.003,z-width/2+.1),(xx,y+.003,z+width/2-.1),.012,mats['mortar'],4)
        b.finish('Bridge_'+spec['id'])
    b=MeshBuilder()
    for z in range(-111,104,2):
        if any(abs(z-br['z'])<br['width']/2+1.1 for br in v['bridges']):continue
        for side in (-1,1):
            x=g.stream(z)+side*8.7;y=g.base(x,z)
            b.box((x,y+.3,z),(.34,1.1,2.08),mats['stone']);b.box((x,y+.86,z),(.46,.09,2.08),mats['concrete'])
            if z%6==1:b.box((x,y+.37,z),(.45,1.24,.45),mats['mortar'])
    b.finish('StreamBankParapets')

def bench(builder,p,width=2.2):
    x,y,z=p
    for i in range(4):builder.box((x,y+.45,z-.24+i*.15),(width,.065,.12),mats['wood'])
    for yy in (.69,.85):builder.box((x,y+yy,z-.27),(width,.12,.055),mats['wood'])
    for xx in (x-width*.36,x+width*.36):
        for zz in (-.21,.21):builder.box((xx,y+.22,z+zz),(.07,.44,.07),mats['metal'])
        builder.box((xx,y+.67,z-.3),(.06,.43,.06),mats['metal'])

def landmarks():
    for l in v['landmarks']:
        if l['id']=='plaza_tree':continue
        b=MeshBuilder();x,y,z=l['position']
        if 'bench' in l['id']:bench(b,(x,y,z),l['size'][0])
        elif 'crates' in l['id']:
            for level in range(2):
                for i in range(3):
                    xx=x-.5+i*.5;yy=y+.25+level*.45
                    b.box((xx,yy,z),(.46,.4,.8),mats['wood'])
                    for row in range(3):b.box((xx,yy-.13+row*.13,z+.41),(.44,.06,.025),mats['warm_plaster'])
        elif 'noticeboard' in l['id']:
            for xx in (-.94,.94):b.box((x+xx,y+.95,z),(.075,1.9,.075),mats['wood'])
            b.box((x,y+1.4,z),(2,.9,.09),mats['green'])
            for i in range(4):b.box((x-.7+i*.45,y+1.40,z+.051),(.35,.6,.012),mats['ivory'])
            b.box((x,y+1.95,z),(2.2,.1,.38),mats['roof'])
        else:
            for i in range(3):b.box((x-.6+i*.6,y+.21,z),(.55,.42,.66),mats['wood'])
        b.finish('Landmark_'+l['id'])
    # Railroad, ballast, a safe low platform, signals and old line-side equipment.
    b=MeshBuilder();z=-101
    for x in range(-47,21):
        y=g.base(x,z);b.box((x,y-.07,z),(1.1,.18,3.5),mats['stone'])
        if x%1==0:b.box((x,y+.09,z),(.19,.14,2.15),mats['wood'])
    for zz in (-.72,.72):b.box((-13,g.base(0,z)+.20,z+zz),(68,.16,.07),mats['metal'])
    for x in (-48,22):
        y=g.base(x,z);b.rod((x,y,z-2.5),(x,y+3.3,z-2.5),.075,mats['metal']);b.box((x,y+2.9,z-2.45),(.4,.9,.25),mats['dark'])
        b.box((x,y+2.99,z-2.29),(.16,.16,.025),mats['red'])
    b.finish('OldRailway')
    # Utility poles and gently sagging wires follow the inhabited west road.
    b=MeshBuilder();poles=v['utilityPoles']
    for x,z in poles:
        y=g.ground(v,x,z);b.rod((x,y,z),(x,y+6.6,z),.085,mats['concrete']);b.box((x,y+6.25,z),(1.25,.1,.10),mats['metal'])
        for xx in (-.45,.45):b.rod((x+xx,y+6.2,z),(x+xx,y+6.45,z),.044,mats['ivory'],8)
    for a,c in zip(poles,poles[1:]):
        for side in (-.45,.45):
            points=[]
            for i in range(17):
                t=i/16;x=a[0]*(1-t)+c[0]*t;z=a[1]*(1-t)+c[1]*t
                y=(g.ground(v,*a)+6.45)*(1-t)+(g.ground(v,*c)+6.45)*t-.8*math.sin(math.pi*t)
                points.append((x+side,y,z))
            for p,q in zip(points,points[1:]):b.rod(p,q,.009,mats['dark'],4)
    b.finish('UtilityPolesAndWires')

def tree_mesh(variant):
    b=MeshBuilder();b.rod((0,0,0),(.12,4.9,0),.19,mats['wood'],12)
    for angle in (0,1.2,2.5,3.8,5.0):
        x=math.cos(angle)*(1.3+variant*.12);z=math.sin(angle)*1.5;b.rod((0,2.7,0),(x,4.8,z),.075,mats['wood'],8)
    obj=b.finish('TreePrototype'+str(variant))
    parts=[obj]
    # Faceted, asymmetric crowns with no transparent planes or alpha-sorting dependence.
    for i,(x,y,z,s) in enumerate([(0,5.5,0,2.1),(-1.1,4.8,.3,1.65),(1.25,5.1,-.45,1.7),(.2,4.9,1.3,1.7),(-.25,6.2,-.5,1.45)]):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1,location=xyz((x,y,z)))
        leaf=bpy.context.object;leaf.scale=(s*(1+variant*.06),s*.94,s*(.9+(i%2)*.12));leaf.data.materials.append(mats['foliage'+str((i+variant)%3)])
        for vertex in leaf.data.vertices:
            p=vertex.co;vertex.co*=1+.075*math.sin(p.x*7+p.y*3+i)*math.cos(p.z*9-p.x*4)
        for polygon in leaf.data.polygons:polygon.use_smooth=True
        for c in list(leaf.users_collection):c.objects.unlink(leaf)
        collection.objects.link(leaf);project_uv(leaf);parts.append(leaf)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.join();return obj

def vegetation():
    prototypes=[tree_mesh(i) for i in range(3)];data=[p.data for p in prototypes]
    for p in prototypes:bpy.data.objects.remove(p,do_unlink=True)
    for i,(x,z,height,variant) in enumerate(g.trees(v)):
        obj=bpy.data.objects.new(f'Tree_{i:03}',data[variant]);collection.objects.link(obj);obj.parent=village
        obj.location=xyz((x,g.ground(v,x,z),z));obj.scale=(height/7,)*3;obj.rotation_euler.z=math.radians((i*73)%360)
    obj=bpy.data.objects.new('PlazaTree',data[1]);collection.objects.link(obj);obj.parent=village;obj.location=xyz((-12,1.35,10));obj.scale=(1.35,1.35,1.35)
    planter=MeshBuilder();x=-12;z=10;y=1.35
    for i in range(24):
        a=i*math.pi/12;c=(i+1)*math.pi/12
        planter.rod((x+1.3*math.cos(a),y+.34,z+1.3*math.sin(a)),(x+1.3*math.cos(c),y+.34,z+1.3*math.sin(c)),.16,mats['stone'],6)
    planter.finish('PlazaTreeSeat')
    # Small vegetable plots occupy the southern/eastern open ground.
    for index,(x,z,w,d) in enumerate([(13,69,20,16),(82,77,18,14),(19,-91,18,10)]):
        b=MeshBuilder()
        for row in range(int(w/1.1)):
            xx=x-w/2+.6+row*1.1
            for segment in range(int(d)):
                zz=z-d/2+.5+segment;yy=g.triangulated(v,xx,zz)
                b.box((xx,yy+.06,zz),(.74,.13,.92),mats['soil'])
                for side in (-1,1):b.rod((xx,yy+.14,zz),(xx+side*.23,yy+.39,zz+.12),.065,mats['foliage'+str((row+segment)%3)],5)
        b.finish('VegetablePlot_'+str(index))
    scenic_mountains()

def scenic_mountains():
    # One continuous, irregular heightfield meets the village rim. No overlapping spheres.
    peaks=[(-170,-90,58,65),(-185,30,78,65),(-155,140,42,85),(-90,-200,95,95),(40,-215,110,85),
           (135,-155,72,80),(185,-35,68,80),(200,100,55,100),(-80,185,38,90),(80,205,30,100)]
    def height(x,z):
        outside=max(-104-x,x-104,-112-z,z-104,0)
        hills=sum(h*math.exp(-((x-cx)**2+(z-cz)**2)/(radius*radius)) for cx,cz,h,radius in peaks)*.42
        detail=math.sin(x*.067)*math.cos(z*.052)*3+math.sin(x*.027+z*.045)*4
        return g.base(x,z)+4.5+g.smooth(outside/100)*(hills+detail)
    nx=209;nz=217;step=4;vertices=[(x*step-416,height(x*step-416,z*step-432),z*step-432) for z in range(nz) for x in range(nx)];faces=[]
    for z in range(nz-1):
        for x in range(nx-1):
            px=x*step-414;pz=z*step-430
            if -104<px<104 and -112<pz<104:continue
            i=z*nx+x;faces.extend([(i,i+nx,i+1),(i+1,i+nx,i+nx+1)])
    mesh=bpy.data.meshes.new('ScenicMountainTerrainMesh');mesh.from_pydata([xyz(p) for p in vertices],[],faces)
    mesh.materials.append(mats['grass']);mesh.update();uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        p=vertices[loop.vertex_index];uv.data[loop.index].uv=(p[0]/8,p[2]/8)
    for polygon in mesh.polygons:
        polygon.use_smooth=True
    obj=bpy.data.objects.new('ScenicMountainTerrain',mesh);collection.objects.link(obj);obj.parent=village

def required():
    return ['VillageV1','Terrain','StreamWater','VillageSquare','HomeExterior','HomeOpenYardGate','Bridge_south','Bridge_north','OldRailway','PlazaTree']+['Building_'+b['id'] for b in v['buildings'] if b['kind']!='home']

def inspect_glb(path):
    raw=path.read_bytes()
    if raw[:4]!=b'glTF':raise RuntimeError('Invalid GLB')
    length=struct.unpack_from('<I',raw,12)[0];data=json.loads(raw[20:20+length]);nodes={n.get('name'):n for n in data['nodes']}
    if set(required())-nodes.keys():raise RuntimeError('Missing actual GLB roots '+str(set(required())-nodes.keys()))
    if data.get('skins') or data.get('animations') or data.get('cameras'):raise RuntimeError('Unexpected animation/character/camera')
    for forbidden in ('PlayerBed','FamilyBed','KitchenSink','Toilet','ClosedYardGate'):
        if forbidden in nodes:raise RuntimeError('Interior payload in exterior GLB')
    for mesh in data['meshes']:
        for p in mesh['primitives']:
            if 'TEXCOORD_0' not in p['attributes']:raise RuntimeError('Missing UV')
            if 'NORMAL' not in p['attributes']:raise RuntimeError('Missing normals')
            accessor=data['accessors'][p['attributes']['NORMAL']];view=data['bufferViews'][accessor['bufferView']]
            offset=28+length+view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',12)
            for i in range(0,accessor['count'],97):
                normal=struct.unpack_from('<fff',raw,offset+i*stride)
                if not all(math.isfinite(n) for n in normal) or abs(sum(n*n for n in normal)-1)>.004:raise RuntimeError('Invalid normal')
    if any('bufferView' not in image for image in data.get('images',[])):raise RuntimeError('Unpacked image')
    # Real vertex data, rather than a metadata-only statement of ground compatibility.
    terrain=data['meshes'][nodes['Terrain']['mesh']];samples=0
    for prim in terrain['primitives']:
        accessor=data['accessors'][prim['attributes']['POSITION']];view=data['bufferViews'][accessor['bufferView']]
        offset=28+length+view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',12)
        for i in range(0,accessor['count'],97):
            x,y,z=struct.unpack_from('<fff',raw,offset+i*stride)
            if abs(y-g.ground(v,x,z))>.004:raise RuntimeError(f'Terrain vertex mismatch {x,y,z}')
            samples+=1
    for a,b in [('house02','house05'),('house04','house07'),('house03','house10')]:
        if nodes['HouseShell_'+a]['mesh']!=nodes['HouseShell_'+b]['mesh']:raise RuntimeError('House shell meshes not shared')
    trees=[n['mesh'] for name,n in nodes.items() if name and name.startswith('Tree_') and 'mesh' in n]
    if len(set(trees))>3 or len(trees)!=len(g.trees(v)):raise RuntimeError('Tree instances are not shared')
    return {'nodes':len(data['nodes']),'meshes':len(data['meshes']),'images':len(data.get('images',[])),'terrainSamplesChecked':samples,'trees':len(trees)}

def export():
    summary=ex.summarize('village');memory=ex.texture_memory_mib('village')
    if summary['triangleCount']>LIMITS['triangles'] or summary['maximumVisibleMeshCount']>LIMITS['visibleMeshes'] or summary['materialCount']>LIMITS['materials'] or memory>LIMITS['textureMiB']:
        raise RuntimeError('Village budget exceeded '+str(summary)+' memory='+str(memory))
    if summary['missingExternalFiles']:raise RuntimeError('Missing source textures')
    ex.export('village',root/GLB);actual=inspect_glb(root/GLB)
    if (root/GLB).stat().st_size>LIMITS['glbBytes']:raise RuntimeError('GLB budget exceeded')
    source_manifest=json.loads((root/ex.SOURCE_MANIFEST).read_text('utf-8'))
    sources=[{'id':a['id'],'assetUrl':a['assetUrl'],'license':source_manifest['license'],'maps':{k:val['sha256'] for k,val in a['maps'].items()}} for a in source_manifest['assets'] if a['id'] in ('oak_veneer_01','painted_plaster_wall','terrazzo_tiles')]
    meta={'schemaVersion':1,'status':'independent-preview','runtimeDefault':False,'graybox':gray,'visualAcceptance':'pending-human-review',
        'sourceFile':SOURCE.as_posix(),'runtimeGlbFile':GLB.as_posix(),'sourceSha256':ex.sha256(root/SOURCE),'runtimeGlbSha256':ex.sha256(root/GLB),
        'runtimeGlbBytes':(root/GLB).stat().st_size,'layoutSource':g.LAYOUT.as_posix(),'layoutSha256':ex.sha256(root/g.LAYOUT),
        'homeSourceSha256':ex.sha256(root/HOME),'geometry':summary,'actualGlb':actual,'limits':LIMITS,'textureMemoryEstimateMiB':memory,'layout':v,
        'provenance':{'date':'2026-09-12','tool':'Blender Python offline procedural modelling','model':bpy.app.version_string,
            'script':'scripts/tools/build_village_v1.py','prompt':'Approved compact contemporary Chinese mountain village with 24 buildings, a stream, two bridges, two walking loops, shop plaza, disused railway station, and a reusable Home v1 exterior. Full environment, no NPC or story.',
            'referenceImages':[],'parameters':{'units':'metres','layoutVersion':1,'grassSeed':412,'grassTextureSize':512,'runtimeGeneration':False},
            'postprocessing':['metric UVs','packed CC0 PBR maps','linked house and tree meshes','derived exterior-only Home v1','font outlines from bundled Source Han Sans 2.005R'],
            'sourceAssets':sources},'exportProfile':{'characters':False,'animations':False,'cameras':False,'lights':False,'runtimeDependsOnBlender':False}}
    (root/MANIFEST).write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n','utf-8')
    if not gray:
        record={'id':'village_environment_v1','status':'adopted-independent-preview','sourceFile':SOURCE.as_posix(),'gameFile':GLB.as_posix().removeprefix('assets/generated/'),
            'sourceSha256':meta['sourceSha256'],'runtimeGlbSha256':meta['runtimeGlbSha256'],**meta['provenance']}
        path=root/'assets/generated/provenance.json';text=path.read_text('utf-8');formatted=json.dumps(record,ensure_ascii=False,indent=2);needle='"id": "'+record['id']+'"'
        if needle in text:
            start=text.rfind('{',0,text.index(needle));_,length=json.JSONDecoder().raw_decode(text[start:]);text=text[:start]+formatted+text[start+length:]
        else:
            end=text.rfind(']');text=text[:end].rstrip()+',\n'+formatted+'\n'+text[end:]
        json.loads(text);path.write_text(text,'utf-8')
    print('VILLAGE_V1_EXPORT_PASS '+json.dumps({'triangles':summary['triangleCount'],'meshes':summary['meshObjectCount'],'materials':summary['materialCount'],'textureMiB':memory}),flush=True)

def build():
    setup();terrain();roads();buildings();bridges_and_banks();landmarks();vegetation()
    for obj in bpy.data.objects:
        if obj.type=='MESH' and not obj.data.uv_layers:project_uv(obj)
    # Appending the preserved house can duplicate the same adopted JPEG maps.
    # Unify by file bytes and colour space before packing/exporting, without changing any pixels.
    canonical={};replacements={}
    for image in list(bpy.data.images):
        if image.packed_file is not None:raw=bytes(image.packed_file.data)
        elif image.source=='FILE' and Path(bpy.path.abspath(image.filepath)).is_file():raw=Path(bpy.path.abspath(image.filepath)).read_bytes()
        else:continue
        key=(hashlib.sha256(raw).hexdigest(),image.colorspace_settings.name)
        if key in canonical:replacements[image]=canonical[key]
        else:canonical[key]=image
    for mat in bpy.data.materials:
        if not mat.use_nodes:continue
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image in replacements:node.image=replacements[node.image]
    for image in replacements:
        if image.users==0:bpy.data.images.remove(image)
    for image in bpy.data.images:
        if image.source=='FILE':image.pack()
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.context.scene['status']='independent-preview';bpy.context.scene['layoutVersion']=1;bpy.context.scene['runtimeDefault']=False
    (root/SOURCE).parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE));export()

def check():
    meta=json.loads((root/MANIFEST).read_text('utf-8'))
    for key,path in [('sourceSha256',SOURCE),('runtimeGlbSha256',GLB),('layoutSha256',g.LAYOUT),('homeSourceSha256',HOME)]:
        if meta[key]!=ex.sha256(root/path):raise RuntimeError('Stale '+key)
    bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False)
    if meta['layout']!=v or meta['runtimeDefault'] or meta['status']!='independent-preview':raise RuntimeError('Unsafe metadata')
    if ex.summarize('village')!=meta['geometry'] or inspect_glb(root/GLB)!=meta['actualGlb']:raise RuntimeError('Stale geometry')
    with tempfile.TemporaryDirectory(prefix='town-rpg-village-check-') as tmp:
        target=Path(tmp)/GLB.name;ex.export('village',target)
        if ex.sha256(target)!=meta['runtimeGlbSha256']:raise RuntimeError('Non-deterministic re-export')
    print('VILLAGE_V1_CHECK_PASS buildings=24 bridges=2 readOnly=true',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check']);parser.add_argument('--root',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root).resolve();v=g.read(root);gray=args.mode=='graybox'
    if bpy.app.version[:3]!=(5,2,0):raise RuntimeError('Use installed Blender 5.2.0')
    if gray:
        SOURCE=Path('work/village-v1-graybox/village-v1-graybox.blend');GLB=Path('work/village-v1-graybox/village-v1-graybox.glb');MANIFEST=GLB.with_suffix('.json')
    if args.mode in ('build','graybox'):build()
    elif args.mode=='export':bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False);export()
    else:check()
