"""Offline home v1 authoring. Blender 5.2: -- graybox|build|export|check --root .

HomeLayout3D.Data is the only metric layout authority. Graybox uses work/ only.
Check reads the editable source, re-exports to a temporary directory and checks
the actual GLB. No generated characters, animations, cameras or lights exported.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys
import tempfile
import bpy
from mathutils import Vector, Matrix, Quaternion

sys.path.insert(0, str(Path(__file__).resolve().parent))
import remodel_manor_v3 as m
import export_environment_cinematic_samples as ex
from remodel_classroom_v6 import project_uv

LAYOUT = Path('scripts/presentation/HomeLayout3D.cs')
SOURCE = Path('art/blender/home/home-v1.blend')
GLB = Path('assets/generated/home/3d/home-v1.glb')
MANIFEST = GLB.with_name('home-v1-manifest.json')
LIMITS = {'triangles':180000, 'visibleMeshes':220, 'materials':24, 'glbBytes':60*1024*1024, 'textureMiB':256}


def read_layout(root):
    match = re.search(r'public const string Data = """\s*(.*?)\s*""";', (root/LAYOUT).read_text('utf-8'), re.S)
    if not match: raise RuntimeError('HomeLayout3D JSON missing')
    return json.loads(match[1])


def box(name, p, s, mat, bevel=.012):
    return m.box(name, p, s, mat, bevel)


def group(name, parts, parent=None):
    return m.finish(parts, name, parent)


def rod(name, a, b, radius, mat, vertices=12):
    return m.rod(name, a, b, radius, mat, vertices)


def empty(name, p=(0,0,0), parent=None):
    return m.empty(name, p, parent or house)


def plane_mesh(name, vertices, faces, mat):
    mesh=bpy.data.meshes.new(name+'Mesh')
    mesh.from_pydata([(x,-z,y) for x,y,z in vertices],[],faces);mesh.materials.append(mat);mesh.update()
    obj=bpy.data.objects.new(name,mesh);m.collection.objects.link(obj);project_uv(obj)
    return obj


def shell():
    h=v['height'];t=v['wallThickness']
    for s in v['structure']:
        if s['id']=='ClosedYardGate':continue
        group(s['id'],[box(s['id'],s['position'],s['size'],globals()[s['material']],.002)])
    g=next(s for s in v['structure'] if s['id']=='ClosedYardGate')
    gate=[box('GatePanel',g['position'],g['size'],green)]
    for x in [-1.3+i*.13 for i in range(21)]:gate.append(box('GateRib',(x,.78,11.84),(.02,1.35,.035),silver,.003))
    gate.extend([box('GateLatch',(.08,.95,11.80),(.25,.045,.04),silver),box('GateSeam',(0,.75,11.84),(.025,1.45,.02),dark)])
    group('ClosedYardGate',gate)
    for w in v['walls']:
        parts=[];skin=[]
        def segment(a,b,lo,hi):
            if b-a<.001 or hi-lo<.001:return
            x,z=((a+b)/2,w['at']) if w['axis']=='x' else (w['at'],(a+b)/2)
            s=(b-a,hi-lo,t) if w['axis']=='x' else (t,hi-lo,b-a)
            parts.append(box('Wall',(x,(hi+lo)/2,z),s,plaster,.003))
            # Interior baseboards, exterior tiled plinth: actual openings stay clear.
            for side in (-1,1):
                top=min(hi,.72 if w['id'] in ('south','north','west','east') else .09)
                if lo>=top:return
                p=(x, (lo+top)/2,z+side*.107) if w['axis']=='x' else (x+side*.107,(lo+top)/2,z)
                sz=(b-a,top-lo,.012) if w['axis']=='x' else (.012,top-lo,b-a)
                skin.append(box('Plinth',p,sz,tile,.001))
        cursor=w['from']
        for u,width,lo,hi in sorted(w['openings']):
            segment(cursor,u-width/2,0,h);segment(u-width/2,u+width/2,0,lo);segment(u-width/2,u+width/2,hi,h)
            cursor=u+width/2
        segment(cursor,w['to'],0,h)
        group('Wall_'+w['id'],parts+skin)
        for i,o in enumerate(w['openings']):
            if o[2]>0:window(w,i,o)
    parts=[]
    for x in (-6.02,6.02):
        parts.extend([rod('Downpipe',(x,3.28,-4.25),(x,.18,-4.25),.045,silver),
            rod('Outlet',(x,.18,-4.25),(x+.18*(1 if x>0 else -1),.13,-4.25),.045,silver)])
    group('Drainpipes',parts)
    # Thin real grout lines keep the quiet grey tile floor legible at room scale.
    grout=[]
    for x in range(-5,6):grout.append(box('FloorGrout',(x,-.006,0),(.007,.005,9),groutmat,.001))
    for z in range(-4,5):grout.append(box('FloorGrout',(0,-.006,z),(12,.005,.007),groutmat,.001))
    group('FloorGrout',grout)
    roof()


def window(w,i,o):
    u,width,lo,hi=o;axis=w['axis'];at=w['at'];height=hi-lo
    def b(name,du,y,span,thick,depth,mat,offset=0):
        p=(u+du,y,at+offset) if axis=='x' else (at+offset,y,u+du)
        s=(span,thick,depth) if axis=='x' else (depth,thick,span)
        return box(name,p,s,mat,.004)
    parts=[]
    for du in (-width/2+.032,width/2-.032,0):parts.append(b('WindowStile',du,(lo+hi)/2,.055,height,.12,silver))
    for y in (lo+.03,hi-.03):parts.append(b('WindowRail',0,y,width,.06,.12,silver))
    parts.append(b('Sill',0,lo-.027,width+.14,.06,.31,tile))
    for side in (-1,1):parts.append(b('WindowLatch',.08,lo+.48,.03,.14,.032,dark,side*.07))
    group(f'Window_{w["id"]}_{i}',parts)
    group(f'Glass_{w["id"]}_{i}',[b('Glass',0,(lo+hi)/2,width-.1,height-.1,.008,glass)])


def roof():
    # Two linked full tile fields. A tile is a curved shell with overlap, not a flat roof texture.
    parts=[]
    for x in (-6,6):
        parts.append(plane_mesh('Gable',[(x,3.4,-4.55),(x,4.65,0),(x,3.4,4.55)],[(0,1,2),(2,1,0)],plaster))
    parts.extend([box('EaveN',(0,3.35,-4.7),(12.6,.18,.16),dark),box('EaveS',(0,3.35,4.7),(12.6,.18,.16),dark)])
    group('RoofGables',parts)
    vertices=[];faces=[]
    for row in range(20):
        for column in range(42):
            x0=-6.3+column*.3;z0=row*.24
            start=len(vertices)
            for dz in (0,.28):
                for j in range(5):
                    x=x0+j*.075;z=z0+dz
                    y=4.65-z*(1.3/4.8)+math.sin(j/4*math.pi)*.027
                    vertices.append((x,y,z))
            for j in range(4):faces.append((start+j+5,start+j+6,start+j+1,start+j))
    obj=plane_mesh('RoofTilesSouth',vertices,faces,roofmat);group('RoofTilesSouth',[obj])
    other=obj.copy();other.data=obj.data;other.name='RoofTilesNorth';m.collection.objects.link(other)
    other.rotation_euler.z=math.pi
    ridge=[rod('Ridge',( -6.4,4.69,0),(6.4,4.69,0),.105,roofmat,16)]
    group('RoofRidge',ridge)


def doors():
    for d in v['doors']:
        x,_,z=d['position'];w=d['width'];h=d['height'];axis=d['axis'];parts=[]
        def p(u,y,dep):return (x+u,y,z+dep) if axis=='x' else (x+dep,y,z+u)
        def s(wid,hei,dep):return (wid,hei,dep) if axis=='x' else (dep,hei,wid)
        for u in (-w/2-.045,w/2+.045):parts.append(box('Jamb',p(u,h/2,0),s(.09,h+.07,.25),wood))
        parts.append(box('Header',p(0,h+.04,0),s(w+.18,.09,.25),wood))
        parts.append(box('Threshold',p(0,.004,0),s(w,.008,.22),silver,.001))
        group('DoorFrame_'+d['id'],parts)
        for side in range(2 if d['double'] else 1):
            length=w/2 if d['double'] else w
            sign=-1 if side==1 else 1
            hingePos=p(w/2 if side==1 else -w/2,0,0)
            hinge=empty(f'Door_{d["id"]}_Hinge{side}',hingePos)
            leaf=[]
            # Author in world coordinates, parent keeps the hinge pivot intact.
            center=(-w/2+length/2) if side==0 else (w/2-length/2)
            leaf.append(box('DoorLeaf',p(center,h/2,0),s(length-.03,h-.025,.045),wood,.008))
            for y,hei in ((h*.27,h*.4),(h*.74,h*.4)):
                for dep in (-.028,.028):leaf.append(box('InsetPanel',p(center,y,dep),s(length-.18,hei,.018),green if d['double'] else wood,.009))
            handle=center+sign*(length/2-.15)
            for dep in (-.065,.065):
                leaf.append(box('HandlePlate',p(handle,1.02,dep*.6),s(.045,.2,.015),silver,.006))
                leaf.append(box('Lever',p(handle-sign*.045,1.06,dep),s(.13,.022,.025),silver,.008))
            group(f'Door_{d["id"]}_Leaf{side}',leaf,hinge)


def furniture(f):
    name=f['id'];kind=f['kind'];x,_,z=f['position'];w,h,d=f['size'];parts=[]
    def b(n,dx,y,dz,s,mat=wood,bevel=.012):
        parts.append(box(n,(x+dx,y,z+dz),s,mat,bevel))
    def r(n,a,c,radius,mat=silver):
        parts.append(rod(n,(x+a[0],a[1],z+a[2]),(x+c[0],c[1],z+c[2]),radius,mat))
    def legs(top,spread=.09):
        for dx in (-w/2+spread,w/2-spread):
            for dz in (-d/2+spread,d/2-spread):b('Leg',dx,top/2,dz,(.065,top,.065))
    def cupboard(height=h):
        b('Carcass',0,height/2,0,(w,height,d))
        fx,fz=(1,0) if name in ('PlayerWardrobe','ShoeCabinet') else (-1,0) if kind in ('sink','stove') else (0,1) if name in ('PrepCabinet','Washbasin','Nightstand') else (0,-1)
        span=d if fx else w
        for u in (-span*.25,span*.25):
            dx=fx*(w/2+.012) if fx else u;dz=u if fx else fz*(d/2+.012)
            b('RaisedDoor',dx,height*.51,dz,(.025,height*.9,span*.48) if fx else (span*.48,height*.9,.025),ivory)
            handle=u+(.07 if u<0 else -.07)
            b('Pull',fx*(w/2+.045) if fx else handle,height*.59,handle if fx else fz*(d/2+.045),(.025,.16,.022) if fx else (.022,.16,.025),silver,.005)
        b('Kick',fx*(w/2+.01),.07,fz*(d/2+.01),(.02,.1,span-.09) if fx else (span-.09,.1,.02),dark)
    if gray:
        b(kind,0,h/2,0,(w,h,d),wood);return group(name,parts)
    if kind=='bed':
        facing=-1 if name=='PlayerBed' else 1
        legs(.24);b('Frame',0,.29,0,(w,.16,d))
        b('Mattress',0,.47,0,(w-.06,.22,d-.08),fabric,.055)
        b('Blanket',0,.6,.28*facing,(w-.08,.07,d*.65),blue,.035)
        b('Headboard',0,.69,(-d/2+.015)*facing,(w,.9,.085))
        count=2 if w>1.5 else 1
        for i in range(count):b('Pillow',(i-(count-1)/2)*.7,.635,-d*.33*facing,(.61,.14,.4),ivory,.06)
        for dz in (-.38,.14,.63):b('BlanketSeam',0,.642,dz*facing,(w-.12,.004,.012),fabric,.001)
    elif kind in ('table','desk','bench'):
        legs(h-.06);b('Top',0,h-.03,0,(w,.06,d));b('Apron',0,h-.15,0,(w-.13,.16,d-.13))
        if kind=='desk':
            b('Drawer',0,h-.18,d/2+.01,(w-.17,.16,.025),ivory)
            b('DrawerPull',0,h-.17,d/2+.05,(.22,.02,.025),silver)
            for i in range(5):
                b('ResearchBook',-.38,h+.017+i*.033,.04,(.34,.028,.24),[green,blue,paper][i%3],.003)
            b('Notebook',.22,h+.012,-.05,(.31,.018,.23),paper,.003)
            r('Pen',(.10,h+.027,-.08),(.34,h+.027,-.04),.005,dark)
            b('Backpack',.53,h+.15,.08,(.31,.3,.22),blue,.06)
            r('BagHandle',(.44,h+.32,.08),(.62,h+.32,.08),.015,dark)
            r('DeskLampStem',(-.61,h,.18),(-.61,h+.38,.18),.018,dark)
            b('DeskLampBase',-.61,h+.016,.18,(.19,.025,.16),dark)
            b('DeskLampHead',-.53,h+.37,.12,(.28,.055,.14),ivory,.025)
        if name=='DiningTable':
            parts.append(m.lathe('FruitBowl',(x,h,z),[(.13,0),(.20,.08),(.19,.085),(.11,.014)],ivory,24))
            for dx in (-.36,.36):parts.append(m.lathe('Cup',(x+dx,h,z),[(.037,0),(.045,.105),(.039,.107),(.032,.02)],ivory,20))
    elif kind=='chair':
        legs(.44);b('Seat',0,.455,0,(w,.055,d),wood)
        for dx in (-w/2+.035,w/2-.035):b('BackPost',dx,.66,d/2-.04,(.05,.48,.05))
        b('Backrest',0,.79,d/2-.04,(w,.18,.045))
        for dz in (-d/2+.05,d/2-.05):r('Brace',(-w/2+.06,.2,dz),(w/2-.06,.2,dz),.015,wood)
    elif kind in ('wardrobe','cabinet','nightstand'):
        cupboard()
        if kind=='wardrobe':b('Crown',0,h+.018,0,(w+.04,.045,d+.025))
        if kind=='nightstand':
            b('Book',0,h+.023,0,(.3,.045,.24),green)
            parts.append(m.lathe('WaterGlass',(x+.16,h,z+.12),[(.04,0),(.045,.12),(.038,.12),(.034,.014)],glass,20))
    elif kind=='sofa':
        b('Base',0,.23,0,(w,.27,d),dark,.035)
        b('Back',-w/2+.09,.57,0,(.18,.57,d),fabric,.06)
        for dz in (-d/2+.08,d/2-.08):b('Arm',0,.48,dz,(w,.28,.16),fabric,.055)
        for dz in (-.41,.41):b('Cushion',.07,.425,dz,(w-.2,.14,.76),fabric,.05)
    elif kind=='tv':
        b('Cabinet',0,.26,0,(w,.5,d))
        for dz in (-.45,.45):b('Front',-w/2-.015,.27,dz,(.02,.36,.82),ivory)
        b('Television',-.04,1.16,0,(.07,.81,1.37),dark,.018)
        b('Screen',-.082,1.16,0,(.008,.74,1.29),screen,.004)
        for dz in (-.46,.46):r('TvFoot',(-.03,.55,dz),(-.03,.76,dz),.023,dark)
    elif kind in ('counter','stove','sink','basin'):
        cupboard(h-.05);b('Worktop',0,h-.025,0,(w+.025,.05,d+.025),tile,.009)
        if kind in ('sink','basin'):
            # Raised rim + dark inset + recessed bottom read as an actual basin.
            bw,bd=(w*.72,d*.50);b('BasinWell',0,h+.008,0,(bw,.013,bd),silver)
            b('BasinBottom',0,h+.016,0,(bw*.78,.008,bd*.76),dark,.035)
            for dx in (-bw/2,bw/2):b('BasinRim',dx,h+.025,0,(.026,.05,bd),silver)
            for dz in (-bd/2,bd/2):b('BasinRim',0,h+.025,dz,(bw,.05,.026),silver)
            r('FaucetStem',(bw/2-.04,h,-bd/2-.08),(bw/2-.04,h+.29,-bd/2-.08),.018)
            r('FaucetSpout',(bw/2-.04,h+.29,-bd/2-.08),(bw/2-.04,h+.29,.02),.018)
            if kind=='basin':b('Mirror',0,1.65,-.31,(.88,.75,.025),silver,.003)
        elif kind=='stove':
            for dz in (-.32,.32):
                parts.append(m.lathe('Burner',(x,h+.035,z+dz),[(.13,0),(.13,.03),(.07,.034)],dark,24))
                for dx in (-.15,.15):r('PanRest',(dx,h+.07,dz-.15),(dx,h+.07,dz+.15),.008,dark)
            b('RangeHood',0,1.85,0,(w+.08,.2,d+.1),silver)
            b('HoodDuct',.17,2.41,0,(.26,.94,.34),silver)
            for dz in (-.32,.32):b('StoveKnob',-.25,h+.04,dz,(.05,.04,.05),dark)
        else:
            b('ChoppingBoard',-.3,h+.017,0,(.49,.025,.37),wood)
            parts.append(m.lathe('Kettle',(x+.49,h,z),[(.095,0),(.12,.05),(.10,.19),(.06,.21)],silver,24))
    elif kind=='fridge':
        b('Body',0,h/2,0,(w,h,d),ivory,.035)
        for y,hei in ((.58,1.04),(1.48,.61)):
            b('Door',0,y,d/2+.014,(w-.055,hei,.04),ivory,.024)
            b('Handle',w*.31,y,d/2+.06,(.027,.24,.035),silver)
        b('Seal',0,1.14,d/2+.04,(w-.04,.02,.008),dark,.001)
    elif kind=='washer':
        b('Body',0,h/2,0,(w,h,d),ivory,.025)
        b('ControlPanel',0,h-.09,-d/2-.008,(w-.06,.12,.035),silver)
        # Circular front opening with a real annular surround, rotated from horizontal.
        ring=m.lathe('WasherPorthole',(0,0,0),[(.21,0),(.235,.015),(.22,.055),(.16,.06),(.155,.025),(.21,0)],silver,32)
        ring.rotation_euler.x=math.pi/2;ring.location=(x,-(z-d/2-.028),.42);parts.append(ring)
        b('WasherGlass',0,.42,-d/2-.01,(.29,.29,.025),screen,.09)
    elif kind=='toilet':
        parts.append(m.lathe('Pedestal',(x,0,z-.04),[(.20,0),(.16,.13),(.22,.35),(.28,.41),(.27,.45),(.19,.46),(.16,.4)],ivory,32))
        b('Cistern',0,.67,-.29,(.46,.43,.19),ivory,.05)
        b('Flush',0,.895,-.29,(.065,.012,.04),silver,.007)
        b('SeatLid',0,.48,.02,(.48,.045,.49),ivory,.13)
    elif kind=='case':
        b('Luggage',0,.3,0,(w,.48,d),blue,.065)
        for dx in (-w*.34,w*.34):
            b('Wheel',dx,.065,0,(.085,.1,.12),dark,.03)
        r('HandleL',(-.15,.52,0),(-.15,.71,0),.012)
        r('HandleR',(.15,.52,0),(.15,.71,0),.012)
        r('Handle',(-.15,.71,0),(.15,.71,0),.016,dark)
        b('Zip',0,.3,-d/2-.004,(w-.04,.012,.012),silver,.001)
    elif kind=='plant':
        parts.append(m.lathe('TerracottaPot',(x,0,z),[(.14,0),(.20,.32),(.225,.33),(.225,.37),(.197,.37),(.177,.3),(.13,.04)],terracotta,24))
        for i in range(8):
            a=i*math.tau/8;dx=math.cos(a)*.24;dz=math.sin(a)*.24
            r('Stem',(0,.2,0),(dx*.8,.63,dz*.8),.009,green)
            verts=[(x,.48,z),(x+dx*.65-dz*.17,.67,z+dz*.65+dx*.17),(x+dx,.72,z+dz),(x+dx*.65+dz*.17,.64,z+dz*.65-dx*.17)]
            parts.append(plane_mesh('Leaf',verts,[(0,1,2,3),(3,2,1,0)],green))
    elif kind=='rack':
        for dx in (-w/2,w/2):
            for dz in (-d/2,d/2):r('RackLeg',(dx,0,dz),(dx,h,0),.021)
        r('Rail',(-w/2,h,0),(w/2,h,0),.025)
        for dx in (-.5,.3):
            r('Hanger',(dx,h-.05,0),(dx-.18,h-.2,0),.006)
            r('Hanger',(dx-.18,h-.2,0),(dx+.18,h-.2,0),.006)
            r('Hanger',(dx+.18,h-.2,0),(dx,h-.05,0),.006)
            b('Towel',dx,h-.55,0,(.39,.73,.014),fabric,.008)
    elif kind=='yardtap':
        b('Pillar',.14,.43,0,(.23,.86,.23),concrete)
        r('Tap',(.14,.81,0),(-.08,.81,0),.021)
        r('Spout',(-.08,.81,0),(-.08,.75,0),.021)
        b('Drain',-.08,.012,0,(.28,.015,.35),dark,.004)
    else:raise RuntimeError('Unimplemented furniture '+kind)
    return group(name,parts)


def fixtures():
    for l in v['lights']:
        x,y,z=l['position'];sx,sy,sz=l['switch']
        parts=[box('Mount',(x,y+.08,z),(.38,.08,.38),ivory,.04),box('Shade',(x,y+.013,z),(.32,.06,.32),lamp,.05)]
        group('LightFixture_'+l['id'],parts)
        # Flat switch panel belongs to the wall; runtime marker remains floor based.
        side=l['id'] not in ('living','porch')
        group('Switch_'+l['id'],[box('SwitchPlate',(sx,sy,sz),(.027,.12,.09) if side else (.09,.12,.027),ivory,.008),
            box('Rocker',(sx+(-.019 if sx<0 else .019) if side else sx,sy,sz if side else sz+(-.019 if l['id']=='living' else .019)),
                (.013,.063,.045) if side else (.045,.063,.013),silver,.004)])
    # Shower fittings on the east wall, no raised step or invisible floor obstacle.
    group('Shower',[rod('ShowerRail',(5.73,.95,-1.55),(5.73,2.13,-1.55),.018,silver),
        rod('ShowerArm',(5.73,2.1,-1.55),(5.41,2.1,-1.55),.018,silver),
        box('ShowerHead',(5.39,2.08,-1.55),(.23,.027,.18),silver),
        box('ShowerDrain',(5.16,.003,-1.52),(.15,.007,.15),dark,.002)])
    # Bathroom floor zones and curtain do not close off the traversable room.
    group('WetFloor',[box('ShowerTile',(5.15,-.004,-1.65),(1.22,.009,.96),ivory,.002)])


def setup():
    global house,wood,plaster,tile,concrete,silver,dark,green,blue,ivory,paper,fabric,glass,roofmat,screen,lamp,terracotta,groutmat
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m.root=root;m.gray=gray;m.collection=m.geo.collection('HomeV1')
    house=m.empty('Home');m.hall_root=house
    wood=m.pbr('Home_Wood',(.35,.23,.12),.73,asset='oak_veneer_01');m.wood=wood
    plaster=m.pbr('Home_Plaster',(.75,.74,.69),.88,asset='painted_plaster_wall',normal_strength=.15)
    tile=m.pbr('Home_Tile',(.38,.41,.39),.75,asset='terrazzo_tiles',diffuse=False,normal_strength=.15)
    groutmat=m.pbr('Home_Grout',(.19,.22,.20),.97)
    concrete=m.pbr('Home_Concrete',(.42,.44,.43),.93,asset='painted_plaster_wall',diffuse=False,normal_strength=.2)
    silver=m.pbr('Home_Aluminium',(.55,.59,.60),.35,.75)
    dark=m.pbr('Home_Dark',(.032,.041,.043),.75)
    green=m.pbr('Home_Green',(.17,.27,.20),.77)
    blue=m.pbr('Home_Blue',(.14,.25,.34),.85)
    ivory=m.pbr('Home_Ivory',(.78,.79,.71),.49)
    paper=m.pbr('Home_Paper',(.71,.67,.52),.91)
    fabric=m.pbr('Home_Fabric',(.42,.47,.40),.97)
    glass=m.pbr('Home_Glass',(.65,.8,.81),.18)
    glass.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value=.13
    glass.surface_render_method='DITHERED';glass.diffuse_color=(.65,.8,.81,.13)
    roofmat=m.pbr('Home_Roof',(.045,.06,.065),.9,asset='painted_plaster_wall',diffuse=False,normal_strength=.16)
    screen=m.pbr('Home_Screen',(.025,.053,.064),.26)
    lamp=m.pbr('Home_Lamp',(.82,.81,.67),.45)
    terracotta=m.pbr('Home_Terracotta',(.36,.19,.1),.91)


def required():
    return ['Home','Floor','Ceiling','Courtyard','RoofTilesSouth','RoofTilesNorth','ClosedYardGate']+[
        'Wall_'+w['id'] for w in v['walls']]+[f['id'] for f in v['furniture']]+[
        f'Door_{d["id"]}_Hinge{i}' for d in v['doors'] for i in range(2 if d['double'] else 1)]+[
        'Switch_'+l['id'] for l in v['lights']]


def build():
    setup();shell();doors()
    chairs=None
    for f in v['furniture']:
        if f['kind']=='chair' and chairs is not None:
            obj=chairs.copy();obj.data=chairs.data;obj.name=f['id'];m.collection.objects.link(obj)
            original=next(a for a in v['furniture'] if a['id']==chairs.name)['position']
            p=f['position'];obj.location+=Vector((p[0]-original[0],-(p[2]-original[2]),0))
        else:
            obj=furniture(f)
            if f['kind']=='chair':chairs=obj
    fixtures()
    for obj in ex.export_objects('home'):
        if obj.type!='MESH':continue
        if not obj.data.uv_layers:project_uv(obj)
        bpy.context.view_layer.objects.active=obj
        for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
    for image in bpy.data.images:
        if image.source=='FILE':image.pack()
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.context.scene['layoutVersion']=1;bpy.context.scene['status']='independent-preview'
    bpy.context.scene['visualAcceptance']='pending-human-review';bpy.context.scene['graybox']=gray
    (root/SOURCE).parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE))
    export()


def inspect_glb(path):
    raw=path.read_bytes()
    if raw[:4]!=b'glTF':raise RuntimeError('Invalid GLB')
    size,typ=struct.unpack_from('<II',raw,12)
    data=json.loads(raw[20:20+size]);names={n.get('name') for n in data['nodes']}
    missing=set(required())-names
    if missing:raise RuntimeError('GLB missing '+str(missing))
    if data.get('skins') or data.get('animations') or data.get('cameras'):raise RuntimeError('Environment contains character/animation/camera')
    for mesh in data['meshes']:
        for p in mesh['primitives']:
            if 'TEXCOORD_0' not in p['attributes']:raise RuntimeError('Missing exported UV')
    for im in data.get('images',[]):
        if 'bufferView' not in im:raise RuntimeError('GLB texture is not embedded')
    node={n['name']:n for n in data['nodes'] if 'name' in n}
    indices={n.get('name'):i for i,n in enumerate(data['nodes'])}
    parents={child:i for i,n in enumerate(data['nodes']) for child in n.get('children',[])}
    def world(i):
        n=data['nodes'][i]
        if 'matrix' in n:
            a=n['matrix'];local=Matrix([a[j:j+4] for j in range(0,16,4)]).transposed()
        else:
            q=n.get('rotation',[0,0,0,1]);s=n.get('scale',[1,1,1])
            local=Matrix.Translation(n.get('translation',[0,0,0])) @ Quaternion((q[3],q[0],q[1],q[2])).to_matrix().to_4x4() @ Matrix.Diagonal((*s,1))
        return world(parents[i]) @ local if i in parents else local
    floor=node['Floor'];matrix=world(indices['Floor']);corners=[]
    for primitive in data['meshes'][floor['mesh']]['primitives']:
        accessor=data['accessors'][primitive['attributes']['POSITION']];lo=accessor['min'];hi=accessor['max']
        corners.extend(matrix @ Vector((x,y,z)) for x in (lo[0],hi[0]) for y in (lo[1],hi[1]) for z in (lo[2],hi[2]))
    extent=[max(p[i] for p in corners)-min(p[i] for p in corners) for i in range(3)]
    if abs(extent[0]-v['width'])>.002 or abs(extent[2]-v['depth'])>.002:
        raise RuntimeError('Actual GLB floor dimensions differ from shared layout: '+str(extent))
    for door in v['doors']:
        for i in range(2 if door['double'] else 1):
            name=f'Door_{door["id"]}_Hinge{i}';actual=world(indices[name]).translation
            expected=Vector(door['position']);expected[0 if door['axis']=='x' else 2]+=door['width']/2*(-1 if i==0 else 1)
            if (actual-expected).length>.002:raise RuntimeError('Actual GLB door pivot mismatch '+name)
    if node['RoofTilesSouth']['mesh']!=node['RoofTilesNorth']['mesh']:raise RuntimeError('Roof tile fields are not linked')
    for name in ('DiningChair1','DiningChair2'):
        if node[name]['mesh']!=node['DeskChair']['mesh']:raise RuntimeError('Chair meshes are not shared')
    for primitive in data['meshes'][node['RoofTilesSouth']['mesh']]['primitives']:
        accessor=data['accessors'][primitive['attributes']['NORMAL']];view=data['bufferViews'][accessor['bufferView']]
        offset=20+size+8+view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',12)
        if accessor['componentType']!=5126:raise RuntimeError('Expected float roof normals')
        if any(struct.unpack_from('<fff',raw,offset+i*stride)[1]<.8 for i in range(accessor['count'])):
            raise RuntimeError('Roof tile normals must face outward/upward')
    return {'nodes':len(data['nodes']),'meshes':len(data['meshes']),'images':len(data.get('images',[]))}


def metrics():
    summary=ex.summarize('home');memory=ex.texture_memory_mib('home')
    if summary['triangleCount']>LIMITS['triangles'] or summary['maximumVisibleMeshCount']>LIMITS['visibleMeshes'] or summary['materialCount']>LIMITS['materials'] or memory>LIMITS['textureMiB']:
        raise RuntimeError('Home budget exceeded '+str(summary)+' textureMiB='+str(memory))
    if summary['missingExternalFiles']:raise RuntimeError('Missing packed textures')
    return summary,memory


def export():
    summary,memory=metrics();ex.export('home',root/GLB)
    if (root/GLB).stat().st_size>LIMITS['glbBytes']:raise RuntimeError('GLB byte budget exceeded')
    actual=inspect_glb(root/GLB)
    source_manifest=json.loads((root/ex.SOURCE_MANIFEST).read_text('utf-8'))
    sources=[{'id':a['id'],'assetUrl':a['assetUrl'],'license':source_manifest['license'],
              'maps':{k:v['sha256'] for k,v in a['maps'].items()}}
             for a in source_manifest['assets'] if a['id'] in ('oak_veneer_01','painted_plaster_wall','terrazzo_tiles')]
    data={'schemaVersion':1,'status':'independent-preview','runtimeDefault':False,'graybox':gray,
        'visualAcceptance':'pending-human-review','sourceFile':SOURCE.as_posix(),'runtimeGlbFile':GLB.as_posix(),
        'sourceSha256':ex.sha256(root/SOURCE),'runtimeGlbSha256':ex.sha256(root/GLB),
        'runtimeGlbBytes':(root/GLB).stat().st_size,'layoutSource':LAYOUT.as_posix(),'layoutSha256':ex.sha256(root/LAYOUT),
        'layout':v,'geometry':summary,'actualGlb':actual,'limits':LIMITS,'textureMemoryEstimateMiB':memory,
        'provenance':{'date':'2026-09-12','tool':'Blender Python offline procedural modeling','model':bpy.app.version_string,
            'script':'scripts/tools/build_home_v1.py','prompt':'User-approved modest inhabited rural bungalow, 12x9m, two bedrooms, living room, kitchen and bathroom, enclosed small courtyard; modern household furniture, maintained plaster and aluminium windows.',
            'referenceImages':[],'parameters':{'layoutVersion':1,'units':'metres','runtimeGeneration':False},
            'postprocessing':['component metric UV','applied bevels','linked chair and roof meshes','packed existing CC0 PBR textures'],
            'sourceAssets':sources if not gray else []},
        'exportProfile':{'animations':False,'characters':False,'cameras':False,'lights':False,'runtimeDependsOnBlender':False}}
    (root/MANIFEST).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')
    if not gray:
        record={'id':'home_environment_v1','status':'adopted-independent-preview','sourceFile':SOURCE.as_posix(),
            'gameFile':GLB.as_posix().removeprefix('assets/generated/'),'sourceSha256':data['sourceSha256'],
            'runtimeGlbSha256':data['runtimeGlbSha256'],'generator':'Offline Blender Python; original procedural meshes',
            'model':bpy.app.version_string,'date':'2026-09-12',**data['provenance']}
        path=root/'assets/generated/provenance.json';text=path.read_text('utf-8');formatted=json.dumps(record,ensure_ascii=False,indent=2)
        needle='"id": "'+record['id']+'"'
        if needle in text:
            start=text.rfind('{',0,text.index(needle));_,length=json.JSONDecoder().raw_decode(text[start:])
            text=text[:start]+formatted+text[start+length:]
        else:
            end=text.rfind(']');text=text[:end].rstrip()+',\n'+formatted+'\n'+text[end:]
        json.loads(text);path.write_text(text,'utf-8')
    print(f'HOME_V1_EXPORT_PASS graybox={gray} triangles={summary["triangleCount"]} meshes={summary["meshObjectCount"]} materials={summary["materialCount"]} textureMiB={memory}')


def check():
    data=json.loads((root/MANIFEST).read_text('utf-8'))
    for key,path in [('sourceSha256',SOURCE),('runtimeGlbSha256',GLB),('layoutSha256',LAYOUT)]:
        if data[key]!=ex.sha256(root/path):raise RuntimeError('Stale '+key)
    bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False)
    summary,memory=metrics()
    if summary!=data['geometry'] or memory!=data['textureMemoryEstimateMiB']:raise RuntimeError('Stale geometry metrics')
    if inspect_glb(root/GLB)!=data['actualGlb']:raise RuntimeError('Stale GLB metadata')
    if data['layout']!=v or data['status']!='independent-preview' or data['runtimeDefault']:raise RuntimeError('Unsafe metadata')
    with tempfile.TemporaryDirectory(prefix='town-rpg-home-check-') as tmp:
        target=Path(tmp)/GLB.name;ex.export('home',target)
        if ex.sha256(target)!=data['runtimeGlbSha256']:raise RuntimeError('Non deterministic GLB re-export')
    print('HOME_V1_CHECK_PASS rooms=5 doors=5 lights=6 readOnly=true')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check']);parser.add_argument('--root',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root).resolve();v=read_layout(root);gray=args.mode=='graybox'
    if bpy.app.version[:3]!=(5,2,0):raise RuntimeError('Use installed Blender 5.2.0; no toolchain upgrade')
    if gray:
        SOURCE=Path('work/home-v1-graybox/home-v1-graybox.blend');GLB=Path('work/home-v1-graybox/home-v1-graybox.glb');MANIFEST=GLB.with_suffix('.json')
    if args.mode in ('build','graybox'):build()
    elif args.mode=='export':bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False);export()
    else:check()
