"""Village v2 first exterior batch. Derives from saved v1; never rebuilds v1.

Blender 5.2: --background --python-exit-code 1 --python this_file -- graybox|build|export|check --root .
build is a deliberate reconstruction of v2 only. Use export after manual editing.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import struct
import sys
import tempfile
import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v1 as base
import village_geometry as geo
import export_environment_cinematic_samples as ex

LAYOUT=Path('scripts/presentation/VillageExteriorLayout3D.cs')
SOURCE=Path('art/blender/village/village-v2.blend')
GLB=Path('assets/generated/village/3d/village-v2.glb')
MANIFEST=GLB.with_name('village-v2-manifest.json')
REPLACED={'PublicShell_shop','PublicShell_workshop','Landmark_noticeboard','Landmark_shop_bench','Landmark_shop_crates'}


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def read_layout():
    return json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";', (root/LAYOUT).read_text('utf-8'),re.S)[1])


def setup_loaded():
    base.root=root;base.v=geo.read(root);base.gray=False
    base.collection=bpy.data.collections['VillageV1'];base.village=bpy.data.objects['VillageV1']
    aliases={'plaster':'Plaster','brick':'Brick','stone':'Stone','wood':'Wood','metal':'Metal','green':'FadedGreen','ivory':'SignIvory','dark':'Dark','roof':'Roof','roof_warm':'RoofWarm','glass':'WindowGlass','blue':'TarpBlue','warm_plaster':'WarmPlaster','mortar':'Mortar'}
    base.mats={key:bpy.data.materials['Village_'+value] for key,value in aliases.items()}


def material(name,color,rough=.8,metal=0):
    m=bpy.data.materials.new('V2_'+name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if p is None:
        p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    return m


def finish(builder,name,parent):
    ob=builder.finish(name,parent)
    if not gray:
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bm.to_mesh(ob.data);bm.free()
        bevel=ob.modifiers.new('Small manufactured edges','BEVEL');bevel.width=.008;bevel.segments=2
        bevel.limit_method='ANGLE'
        # Apply now so saved source and repeated exports have the same geometry.
        bpy.context.view_layer.objects.active=ob;ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=bevel.name);ob.select_set(False)
    return ob


def box(b,p,s,m):b.box(p,s,mats[m])
def rod(b,a,c,r,m,sides=10):b.rod(a,c,r,mats[m],sides)


def sloped_panel(b,x0,x1,z0,z1,y0,y1,thickness,mat):
    top=[(x0,y0,z0),(x0,y1,z1),(x1,y1,z1),(x1,y0,z0)]
    bottom=[(x,y-thickness,z) for x,y,z in top]
    b.face(top,mats[mat]);b.face(list(reversed(bottom)),mats[mat])
    for i in range(4):
        j=(i+1)%4;b.face([top[i],bottom[i],bottom[j],top[j]],mats[mat])


def parent(name,p=(0,0,0)):
    o=base.empty(name,p);o.parent=extension;return o


def wear(b,w,h,d):
    # Local, deterministic wear on frequently handled front edges, not global dirt.
    for i in range(7):
        x=-w*.41+i*w*.127
        box(b,(x,h+.003,d*.37),(.035+(i%3)*.018,.003,.009),'edge')


def table(b,w,h,d,drawer=False):
    for i in range(4):box(b,(0,h-.045,-d/2+(i+.5)*d/4),(w,.085,d/4-.006),'wood')
    for x in (-w/2+.075,w/2-.075):
        for z in (-d/2+.075,d/2-.075):box(b,(x,(h-.09)/2,z),(.075,h-.09,.075),'wood')
        box(b,(x,.22,0),(.055,.07,d-.12),'wood')
    for z in (-d/2+.065,d/2-.065):box(b,(0,h-.17,z),(w-.08,.18,.05),'wood')
    if drawer:
        box(b,(0,h-.17,d/2+.005),(w*.5,.15,.04),'wood')
        rod(b,(-.12,h-.17,d/2+.04),(.12,h-.17,d/2+.04),.012,'metal')
    wear(b,w,h,d)


def cabinet(b,w,h,d):
    box(b,(0,h/2,0),(w,h,d),'blue')
    for x in (-w*.245,w*.245):
        box(b,(x,h*.52,d/2+.008),(w*.475,h*.89,.02),'steel')
        box(b,(x*.25,h*.56,d/2+.027),(.025,.16,.025),'dark')
        for z in range(3):box(b,(x,.22+z*.045,d/2+.025),(w*.28,.012,.005),'dark')
    for x in (-w*.4,w*.4):box(b,(x,.025,0),(.05,.05,d*.8),'dark')


def crate(b,w,h,d,offset=(0,0,0)):
    x,y,z=offset
    for xx in (-w/2+.025,w/2-.025):
        for zz in (-d/2+.025,d/2-.025):box(b,(x+xx,y+h/2,z+zz),(.045,h,.045),'wood')
    for i in range(3):
        yy=y+(i+.5)*h/3
        for zz in (-d/2,d/2):box(b,(x,yy,z+zz),(w,h/3-.025,.035),'wood')
        for xx in (-w/2,w/2):box(b,(x+xx,yy,z),(.035,h/3-.025,d),'wood')
    box(b,(x,y+.025,z),(w,.05,d),'wood')


def ellipse(name,p,scale,mat,host):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=base.xyz(p))
    ob=bpy.context.object;ob.name=name;ob.scale=(scale[0],scale[2],scale[1]);ob.parent=host
    for c in list(ob.users_collection):c.objects.unlink(ob)
    base.collection.objects.link(ob);ob.data.materials.append(mat)
    for face in ob.data.polygons:face.use_smooth=True
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return ob


def props():
    for spec in layout['props']:
        host=parent('V2_'+spec['id'],spec['position']);host['fixture']=spec['id']
        w,h,d=spec['size'];kind=spec['kind'];b=base.MeshBuilder()
        if kind=='overhead' and not gray:continue
        if gray:
            box(b,(0,h/2,0),(w,h,d),'blue');b.finish(host.name+'_Gray',host);continue
        if kind in ('table','desk','workbench'):
            table(b,w,h,d,kind!='table')
            if kind=='workbench':
                # Vice occupies the left end; the central .9 m of the worktop stays empty.
                box(b,(-w*.36,h+.09,0),(.26,.18,.18),'steel')
                for z in (-.12,.12):box(b,(-w*.36,h+.21,z),(.28,.06,.06),'metal')
                rod(b,(-w*.36,h+.08,.1),(-w*.36,h+.08,.34),.022,'metal')
                rod(b,(-w*.36-.1,h+.08,.34),(-w*.36+.1,h+.08,.34),.012,'metal')
            elif kind=='desk':
                book=base.MeshBuilder();box(book,(.24,h+.014,0),(.3,.028,.22),'paper')
                box(book,(.24,h+.032,0),(.305,.005,.225),'green')
                rod(book,(-.16,h+.012,.08),(.015,h+.012,.08),.007,'dark')
                finish(book,'V2_RegistrationBook',host)
            else:
                for i in range(3):
                    parcel=base.MeshBuilder();x=-.45+i*.43
                    box(parcel,(x,h+.12,0),(.32,.24,.28),'paper')
                    box(parcel,(x,h+.243,0),(.018,.004,.285),'twine')
                    box(parcel,(x,h+.243,0),(.325,.004,.018),'twine')
                    finish(parcel,'V2_DeliveryParcel_'+str(i),host)
        elif kind=='chair':
            table(b,w,.44,d)
            for x in (-w*.42,w*.42):box(b,(x,h*.65,-d*.42),(.05,h*.7,.05),'wood')
            for yy in (.67,.83):box(b,(0,yy,-d*.42),(w,.09,.04),'wood')
        elif kind=='bench':
            base.bench(b,(0,0,0),w)
        elif kind=='stool':
            table(b,w,h,d);box(b,(w*.3,h-.17,d/2),(.075,.15,.014),'edge')
        elif kind in ('cabinet','bins'):cabinet(b,w,h,d)
        elif kind=='fridge':
            box(b,(0,h/2,0),(w,h,d),'ivory');box(b,(0,h*.54,d/2+.008),(w*.87,h*.78,.025),'glass')
            box(b,(w*.34,h*.56,d/2+.035),(.03,.27,.025),'metal')
            for yy in (.38,.75,1.12):box(b,(0,yy,d/2+.025),(w*.8,.018,.015),'metal')
        elif kind=='board':
            for x in (-.94,.94):box(b,(x,1,0),(.09,2,.09),'wood')
            box(b,(0,1.42,0),(2,.98,.10),'green')
            for x in (-1,1):box(b,(x,1.42,.065),(.045,1,.035),'wood')
            for yy in (.93,1.92):box(b,(0,yy,.065),(2,.04,.035),'wood')
            box(b,(0,2.03,0),(2.2,.1,.45),'roof')
            for i in range(4):
                sheet=base.MeshBuilder();x=-.7+i*.46
                box(sheet,(x,1.43,.061),(.34,.59,.008),'paper')
                for row in range(6):box(sheet,(x-.015,1.59-row*.056,.067),(.20-(row%3)*.027,.005,.002),'ink')
                for xx in (-.12,.12):rod(sheet,(x+xx,1.68,.065),(x+xx,1.68,.079),.012,'metal')
                finish(sheet,'V2_NoticePaper_'+str(i),host)
        elif kind=='mailbox':
            box(b,(0,.46,0),(.055,.92,.055),'metal')
            box(b,(0,1.04,0),(w,.36,d),'green')
            box(b,(0,1.235,0),(w+.025,.03,d+.04),'metal')
            box(b,(0,1.13,d/2+.004),(w*.7,.025,.005),'dark')
            box(b,(0,1.03,-d/2-.01),(w*.85,.26,.015),'steel')
            rod(b,(.12,1.01,-d/2-.02),(.12,1.01,-d/2-.035),.02,'metal')
        elif kind=='post':box(b,(0,h/2,0),(w,h,d),'steel')
        elif kind=='crates':
            for i in (-1,1):crate(b,.6,.36,.6,(i*.35,.03,0))
            crate(b,.6,.36,.6,(-.35,.39,0))
        elif kind=='sacks':
            for zz in (-.48,.48):box(b,(0,.07,zz),(w,.14,.08),'wood')
            for i in range(6):box(b,(-w/2+(i+.5)*w/6,.16,0),(w/6-.025,.07,d),'wood')
            for i,(x,y,z) in enumerate([(-.52,.43,-.2),(.18,.43,-.2),(-.52,.7,-.13),(.36,.43,.28)]):
                ellipse('V2_FlourSack_'+str(i),(x,y,z),(.38,.23,.25),mats['fabric'],host)
                box(b,(x,y+.225,z),(.19,.005,.11),'paper')
                rod(b,(x-.3,y+.1,z-.12),(x-.3,y+.1,z+.12),.008,'twine')
                for j in range(12):
                    angle=j*math.tau/12;next_angle=(j+.7)*math.tau/12
                    rod(b,(x+.365*math.cos(angle),y+.025,z+.244*math.sin(angle)),
                        (x+.365*math.cos(next_angle),y+.025,z+.244*math.sin(next_angle)),.0035,'twine',6)
        elif kind=='cart':
            for i in range(5):box(b,(-.42+i*.21,.4,0),(.19,.065,1.3),'wood')
            for x in (-.42,.42):
                box(b,(x,.33,0),(.05,.08,1.4),'steel')
                rod(b,(x,.3,.6),(x,1.05,.85),.023,'steel')
                for z in (-.48,.48):
                    rod(b,(x-.055,.18,z),(x+.055,.18,z),.17,'dark',16)
                    rod(b,(x-.06,.18,z),(x+.06,.18,z),.08,'metal',12)
            rod(b,(-.42,1.05,.85),(.42,1.05,.85),.026,'steel')
        elif kind=='timber':
            for i in range(6):box(b,(0,.065+i*.075,(-1)**i*.02),(w,.065,d*.8),'wood')
            for x in (-.55,.55):box(b,(x,.27,d*.43),(.035,.5,.025),'twine')
        elif kind=='toolboard':
            box(b,(0,h/2,0),(w,h,.045),'pale_wood')
            for x in (-w*.46,w*.46):box(b,(x,h/2,.035),(.04,h,.03),'metal')
            for i in range(8):
                x=-1+i*.27;yy=.7+(i%2)*.22
                rod(b,(x,yy-.28,.10),(x,yy,.10),.024,'wood')
                if i%3==0:box(b,(x,yy,.10),(.20,.07,.07),'steel')
                elif i%3==1:rod(b,(x,yy,.10),(x,yy+.16,.10),.01,'metal')
                else:box(b,(x+.065,yy-.12,.10),(.11,.28,.025),'metal')
                rod(b,(x,yy+.02,.04),(x,yy+.02,.11),.01,'metal')
            # Recognisable hand plane on a small lower shelf.
            box(b,(0,.10,.08),(2.25,.055,.18),'wood');box(b,(-.65,.18,.13),(.30,.10,.085),'wood')
            box(b,(-.65,.245,.13),(.08,.035,.08),'steel')
        finish(b,'V2_'+spec['id']+'_Mesh',host)


def facades():
    for id in ('shop','workshop'):
        spec=next(s for s in base.v['buildings'] if s['id']==id);w,h,d=spec['size'];host=bpy.data.objects['Building_'+id]
        b=base.MeshBuilder()
        box(b,(0,h/2,0),(w,h,d),'plaster');box(b,(0,.32,0),(w+.025,.64,d+.025),'brick' if id=='workshop' else 'stone')
        base.windows(b,w,h,d,1);base.roof(b,w,h,d,0)
        if id=='shop':
            box(b,(0,1.14,d/2+.04),(3,2.28,.07),'glass')
            for x in (-1.5,0,1.5):box(b,(x,1.14,d/2+.09),(.045,2.28,.05),'metal')
            for x in (-.12,.12):box(b,(x,1.10,d/2+.135),(.024,.28,.035),'metal')
        else:
            for i in range(17):box(b,(0,.12+i*.14,d/2+.075),(3.2,.12,.065),'steel')
            for x in (-1.64,1.64):box(b,(x,1.23,d/2+.1),(.075,2.5,.08),'metal')
            box(b,(0,2.55,d/2+.08),(3.42,.20,.22),'blue')
            box(b,(0,.20,d/2+.12),(.2,.025,.035),'dark')
        # Sills, wall repairs and an eaves gutter carry the same restrained language.
        for x in (-4.2,4.2):
            box(b,(x,.9,d/2+.04),(1.6,.06,.18),'stone')
        for i in range(5):box(b,(-w*.4+i*.18,.2+(i%2)*.12,d/2+.021),(.12,.05,.007),'mortar')
        rod(b,(-w/2-.15,h+.12,d/2+.3),(w/2+.15,h+.12,d/2+.3),.065,'metal')
        rod(b,(-w/2+.16,h+.12,d/2+.3),(-w/2+.16,.1,d/2+.3),.045,'metal')
        finish(b,'PublicShell_'+id,host)
    b=base.MeshBuilder();h=3.1
    # Shop awning: clear lower valance 2.7 m, independent from the building shell.
    for i in range(14):
        x=-5.6+i*.8
        sloped_panel(b,x,x+.8,4.5,6.75,h,2.92,.025,'ivory' if i%2 else 'green')
        box(b,(x+.4,2.81,6.75),(.797,.22,.018),'ivory' if i%2 else 'green')
    for x in (-5.5,0,4.2):rod(b,(x,3.08,4.51),(x,2.87,6.72),.025,'steel')
    rod(b,(-5.55,2.9,6.73),(5.55,2.9,6.73),.026,'steel')
    finish(b,'V2_ShopAwning',bpy.data.objects['Building_shop'])
    b=base.MeshBuilder()
    for i in range(27):
        x=-5.5+i*.41
        sloped_panel(b,x,min(5.5,x+.415),4.05,9.6,3.28,3.0,.04,'steel')
        rod(b,(x,3.29,4.05),(x,3.01,9.6),.012,'metal',6)
    for z in (4.15,6.8,9.35):box(b,(0,2.92 if z>8 else 3.06,z),(10.8,.10,.08),'steel')
    for x in (-5.25,5.25):
        rod(b,(x,2.5,9.35),(x,2.96,8.7),.03,'steel')
    rod(b,(-5.5,2.99,9.6),(5.5,2.99,9.6),.055,'metal')
    rod(b,(5.4,2.99,9.6),(5.4,.12,9.6),.035,'metal')
    finish(b,'V2_WorkshopCanopy',bpy.data.objects['Building_workshop'])


def ground_details():
    b=base.MeshBuilder()
    # Sample the existing triangulated surface, with no new raised platform.
    for ix in range(8):
        for iz in range(8):
            x=-21+ix*.5;z=5.65+iz*.55
            tile=[(x,z),(x+.49,z),(x+.49,z+.54),(x,z+.54)]
            for xx in range(math.floor(x),math.ceil(x+.49)):
                for zz in range(math.floor(z),math.ceil(z+.54)):
                    for tri in [[(xx,zz),(xx,zz+1),(xx+1,zz)],[(xx+1,zz),(xx,zz+1),(xx+1,zz+1)]]:
                        pts=base.clip_polygon(tri,tile)
                        if len(pts)>=3:b.face([(px,geo.triangulated(base.v,px,pz)+.075,pz) for px,pz in pts],mats['stone'])
    b.finish('V2_OfficePaving',extension)
    # Small pads and extended feet follow local ground variation under furniture.
    for spec in layout['props']:
        if spec['id'] not in ('VillageDesk','VillageChair','PaperCabinet','PlazaBench','Handcart','ShopBench'):continue
        x,y,z=spec['position'];w,h,d=spec['size'];b=base.MeshBuilder()
        for xx in (-w*.4,w*.4):
            for zz in (-d*.35,d*.35):
                bottom=geo.triangulated(base.v,x+xx,z+zz)-y
                if bottom<0:box(b,(xx,bottom/2,zz),(.08,-bottom+.025,.08),'wood')
        if b.faces:b.finish('V2_'+spec['id']+'_GroundFeet',bpy.data.objects['V2_'+spec['id']])


def fingerprint(ob):
    h=hashlib.sha256();h.update(str(tuple(round(x,6) for row in ob.matrix_world for x in row)).encode())
    if ob.type=='MESH':
        for v in ob.data.vertices:h.update(struct.pack('<fff',*v.co))
        for p in ob.data.polygons:h.update(str((tuple(p.vertices),p.material_index)).encode())
        h.update(str([s.material.name for s in ob.material_slots]).encode())
    return h.hexdigest()


def preserved_glb_meshes():
    """Compare actual vertex/index/UV payloads independently of glTF buffer indices."""
    def signatures(path):
        raw=path.read_bytes();length=struct.unpack_from('<I',raw,12)[0];data=json.loads(raw[20:20+length])
        def accessor(index):
            a=data['accessors'][index];view=data['bufferViews'][a['bufferView']]
            size={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}[a['componentType']]*{'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
            start=28+length+view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',size)
            return hashlib.sha256(b''.join(raw[start+i*stride:start+i*stride+size] for i in range(a['count']))).hexdigest()
        result={}
        for node in data['nodes']:
            if 'mesh' not in node:continue
            primitives=[]
            for p in data['meshes'][node['mesh']]['primitives']:
                primitives.append((data['materials'][p['material']]['name'],accessor(p['indices']),
                    sorted((key,accessor(value)) for key,value in p['attributes'].items())))
            result[node['name']]=sorted(primitives)
        return result
    old=signatures(root/base.GLB);new=signatures(root/GLB);count=0
    for name,value in old.items():
        if name in REPLACED:continue
        if name not in new or new[name]!=value:raise RuntimeError('Preserved GLB mesh changed '+name)
        count+=1
    return count


def check_clearances():
    for z in layout['zones']:
        x,a,w,d=z['rect']
        for p in layout['props']:
            if not p['solid'] or p['kind']=='overhead':continue
            px,_,pz=p['position'];pw,_,pd=p['size']
            if min(x+w,px+pw/2)-max(x,px-pw/2)>.001 and min(a+d,pz+pd/2)-max(a,pz-pd/2)>.001:
                raise RuntimeError('Clearance occupied: '+z['id']+' by '+p['id'])


def build():
    global extension,mats
    check_clearances();bpy.ops.wm.open_mainfile(filepath=str(root/base.SOURCE),load_ui=False);setup_loaded()
    untouched={o.name:fingerprint(o) for o in bpy.data.objects if o.name not in REPLACED}
    for name in REPLACED:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
    extension=base.empty('VillageExteriorV2')
    mats=dict(base.mats)
    pale=mats['wood'].copy();pale.name='V2_PalePlywood'
    shader=next(n for n in pale.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    for link in list(shader.inputs['Base Color'].links):pale.node_tree.links.remove(link)
    shader.inputs['Base Color'].default_value=(.64,.53,.37,1);mats['pale_wood']=pale
    mats.update(steel=material('WeatheredGalvanised',(.31,.38,.37),.65,.45),edge=material('WornWoodEdge',(.47,.35,.20)),
        paper=material('UncoatedPaper',(.68,.62,.47)),fabric=material('FlourSack',(.57,.50,.35)),twine=material('Jute',(.30,.23,.13)),ink=material('PaperInk',(.24,.27,.24)))
    if not gray:facades()
    else:
        # Gray shell replacements preserve the original dimensions.
        for id in ('shop','workshop'):
            s=next(s for s in base.v['buildings'] if s['id']==id);b=base.MeshBuilder()
            box(b,(0,s['size'][1]/2,0),s['size'],'plaster');b.finish('PublicShell_'+id,bpy.data.objects['Building_'+id])
    props()
    if not gray:
        shared={}
        for spec in layout['props']:
            if spec['kind'] not in ('stool','post'):continue
            ob=bpy.data.objects['V2_'+spec['id']+'_Mesh'];key=(spec['kind'],tuple(spec['size']))
            if key in shared:ob.data=shared[key]
            else:shared[key]=ob.data
        ground_details()
    bpy.context.view_layer.update()
    for name,signature in untouched.items():
        if fingerprint(bpy.data.objects[name])!=signature:raise RuntimeError('Changed preserved object '+name)
    bpy.context.scene['v2PreservedObjects']=json.dumps(untouched,sort_keys=True)
    bpy.context.scene['exteriorBatch']=1;bpy.context.scene['visualAcceptance']='pending-human-review'
    (root/SOURCE).parent.mkdir(parents=True,exist_ok=True);bpy.ops.wm.save_as_mainfile(filepath=str(root/SOURCE));export()


def export():
    summary=ex.summarize('village');memory=ex.texture_memory_mib('village')
    limits=base.LIMITS
    for key,limit in [('triangleCount','triangles'),('maximumVisibleMeshCount','visibleMeshes'),('materialCount','materials')]:
        if summary[key]>limits[limit]:raise RuntimeError('Budget exceeded '+key)
    if memory>limits['textureMiB'] or summary['missingExternalFiles']:raise RuntimeError('Invalid textures')
    ex.export('village',root/GLB);actual=base.inspect_glb(root/GLB)
    actual['preservedMeshesChecked']=preserved_glb_meshes()
    if (root/GLB).stat().st_size>limits['glbBytes']:raise RuntimeError('GLB budget')
    meta=dict(schemaVersion=2,status='independent-preview',runtimeDefault=False,graybox=gray,visualAcceptance='pending-human-review',
        sourceFile=SOURCE.as_posix(),runtimeGlbFile=GLB.as_posix(),sourceSha256=sha(root/SOURCE),runtimeGlbSha256=sha(root/GLB),
        runtimeGlbBytes=(root/GLB).stat().st_size,baseSourceSha256=sha(root/base.SOURCE),baseGlbSha256=sha(root/base.GLB),
        baseLayoutSha256=sha(root/geo.LAYOUT),exteriorLayoutSha256=sha(root/LAYOUT),homeSourceSha256=sha(root/base.HOME),
        geometry=summary,actualGlb=actual,limits=limits,textureMemoryEstimateMiB=memory,layout=layout,
        preservedObjects=json.loads(bpy.context.scene['v2PreservedObjects']),
        provenance=dict(date='2026-09-14',tool='Blender offline Python modelling',model=bpy.app.version_string,
            script='scripts/tools/build_village_v2.py',prompt='Approved first village exterior batch: everyday village desk and noticeboard, grocery loading, woodworking repair workshop. No story interactions.',
            referenceImages=['outputs/village-v1/validation/1920x1080/05-shop.png','outputs/village-v1/validation/1920x1080/06-plaza-tree.png','outputs/village-v1/validation/1920x1080/09-workshop.png'],
            parameters=dict(units='metres',baseVersion=1,exteriorVersion=2,bevelMetres=.008,runtimeGeneration=False),
            sourceAssets=['saved village-v1.blend','previously adopted Poly Haven CC0 wood/plaster/stone maps','original geometry and constant-color materials'],
            postprocessing=['metric UVs','small edge bevels','independent fixture and paper objects','preserved baseline geometry verified by fingerprint']),
        exportProfile=dict(characters=False,animations=False,cameras=False,lights=False,runtimeDependsOnBlender=False))
    (root/MANIFEST).write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n','utf-8')
    if not gray:
        path=root/'assets/generated/provenance.json';text=path.read_text('utf-8')
        record=dict(id='village_environment_v2',status='adopted-independent-preview',sourceFile=SOURCE.as_posix(),gameFile=GLB.as_posix().removeprefix('assets/generated/'),sourceSha256=meta['sourceSha256'],runtimeGlbSha256=meta['runtimeGlbSha256'],**meta['provenance'])
        needle='"id": "'+record['id']+'"';formatted=json.dumps(record,ensure_ascii=False,indent=2)
        if needle in text:
            start=text.rfind('{',0,text.index(needle));_,length=json.JSONDecoder().raw_decode(text[start:]);text=text[:start]+formatted+text[start+length:]
        else:
            end=text.rfind(']');text=text[:end].rstrip()+',\n'+formatted+'\n'+text[end:]
        json.loads(text);path.write_text(text,'utf-8')
    print('VILLAGE_V2_EXPORT_PASS '+json.dumps(dict(geometry=summary,textureMiB=memory)),flush=True)


def check():
    check_clearances();meta=json.loads((root/MANIFEST).read_text('utf-8'))
    for key,p in [('sourceSha256',SOURCE),('runtimeGlbSha256',GLB),('baseSourceSha256',base.SOURCE),('baseGlbSha256',base.GLB),('baseLayoutSha256',geo.LAYOUT),('exteriorLayoutSha256',LAYOUT),('homeSourceSha256',base.HOME)]:
        if meta[key]!=sha(root/p):raise RuntimeError('Stale '+key)
    bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False);setup_loaded();bpy.context.view_layer.update()
    for name,sig in meta['preservedObjects'].items():
        if fingerprint(bpy.data.objects[name])!=sig:raise RuntimeError('Preserved object mismatch '+name)
    actual=base.inspect_glb(root/GLB);actual['preservedMeshesChecked']=preserved_glb_meshes()
    if ex.summarize('village')!=meta['geometry'] or actual!=meta['actualGlb']:raise RuntimeError('Geometry mismatch')
    with tempfile.TemporaryDirectory(prefix='village-v2-check-') as temp:
        dest=Path(temp)/GLB.name;ex.export('village',dest)
        if sha(dest)!=meta['runtimeGlbSha256']:raise RuntimeError('Re-export mismatch')
    print('VILLAGE_V2_CHECK_PASS readOnly=true preserved='+str(len(meta['preservedObjects'])),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','build','export','check']);parser.add_argument('--root',required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root).resolve();gray=args.mode=='graybox'
    if bpy.app.version[:3]!=(5,2,0):raise RuntimeError('Blender 5.2.0 required')
    layout=read_layout();base.root=root;base.v=geo.read(root)
    if gray:
        SOURCE=Path('work/village-v2-graybox/village-v2-graybox.blend');GLB=SOURCE.with_suffix('.glb');MANIFEST=SOURCE.with_suffix('.json')
    if args.mode in ('build','graybox'):build()
    elif args.mode=='export':bpy.ops.wm.open_mainfile(filepath=str(root/SOURCE),load_ui=False);setup_loaded();export()
    else:check()
