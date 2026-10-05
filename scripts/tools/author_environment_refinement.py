"""Independent saved-source art samples. Blender --python this_file -- init|export|check.

init refuses existing sources. Subsequent edits belong in the saved Blend files;
export never rebuilds or saves them. No characters, gameplay or terrain edits.
"""
import bpy, bmesh, hashlib, json, math, random, struct, sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_village_v1 as geo
import build_village_v7 as exporter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender/environment-refinement-v1'
DEST = ROOT / 'assets/generated/environment-refinement-v1'
exporter.DEST = DEST  # Keep newly shared images inside this independent sample.
SEEDS = {'home':'art/blender/home/home-anime-v8.blend',
         'classroom':'art/blender/dream_classroom/classroom-art-pass-v6.blend',
         'village':'art/blender/village/village-v8.blend'}

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def linear(hex):
    rgb=[int(hex[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb)

def material(name, color, rough=.8, metallic=0):
    mat=bpy.data.materials.new('Refined_'+name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*linear(color),1)
    bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metallic
    mat.diffuse_color=(*linear(color),1)
    return mat

def materials():
    specs={'wood':('bda080',.72),'plaster':('e1ded2',.94),'green':('94a99a',.86),
        'floor':('b0b2ac',.82),'metal':('667977',.49),'dark':('374748',.84),
        'blue':('7186a1',.92),'linen':('e7e4d5',.97),'roof':('63747a',.88),
        'stone':('a9a89a',.92),'bark':('8c795e',.95),'leaf':('638653',.94),
        'leaflight':('86a867',.94),'leafdark':('496d48',.94),'paper':('e6ddc2',.95)}
    mats={k:material(k,*v) for k,v in specs.items()}
    wood=mats['wood'];bs=wood.node_tree.nodes.get('Principled BSDF')
    tex=wood.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(SOURCE/'oak-albedo.png'),check_existing=True)
    tex.image.pack();wood.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Base Color'].default_value=(.82,.82,.82,1)
    for k in ('leaf','leaflight','leafdark'):mats[k].use_backface_culling=False
    return mats

def category(name):
    n=name.lower()
    if any(s in n for s in ('wood','timber','oak')):return 'wood'
    if any(s in n for s in ('plaster','ivory','v8_wall')):return 'plaster'
    if any(s in n for s in ('terrazzo','home_tile','paving','concrete')):return 'floor'
    if any(s in n for s in ('linen','fabric')):return 'linen'
    if n=='home_blue':return 'blue'
    if any(s in n for s in ('steel','metal','aluminium')):return 'metal'
    if any(s in n for s in ('greenpaint','home_green')):return 'green'
    if 'roof' in n:return 'roof'
    if 'bark' in n:return 'bark'
    return None

def recolor(objects,M):
    for o in objects:
        if o.type!='MESH':continue
        # Copy a mesh before remapping, so linked non-sample objects remain intact.
        if o.data.users>1:o.data=o.data.copy()
        for slot in o.material_slots:
            if slot.material and (k:=category(slot.material.name)):slot.material=M[k]

def setup(parent):
    geo.collection=parent.users_collection[0];geo.village=parent

def mesh(name,parent,builder,smooth=False):
    o=builder.finish(name,parent)
    bm=bmesh.new();bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(o.data);bm.free()
    if smooth:
        for f in o.data.polygons:f.use_smooth=True
    return o

def rounded_box(name,parent,position,size,mat,bevel=.025):
    b=geo.MeshBuilder();b.box(position,size,mat);o=mesh(name,parent,b)
    if bevel:
        bpy.context.view_layer.objects.active=o
        mod=o.modifiers.new('Crafted edges','BEVEL');mod.width=bevel;mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cloth(name,parent,x,z,w,d,height,mat,seed=1):
    rng=random.Random(seed);phase=rng.random()*math.tau;b=geo.MeshBuilder();nx,nz=44,36
    def p(i,j):
        u=i/nx*2-1;v=j/nz*2-1
        a=abs(u)
        if a<=.78: offset=a/.78*.65;drop=0
        elif a<=.9:
            t=(a-.78)/.12*math.pi/2;offset=.65+.10*math.sin(t);drop=.10*(1-math.cos(t))
        else: offset=.75+(a-.9)*.16;drop=.10+(a-.9)*2.55
        fold=.013*math.sin(u*8+v*2+phase)+.008*math.sin(v*10-u*3)
        return (x+math.copysign(offset,u),height-drop+fold,z+v*d/2)
    for i in range(nx):
        for j in range(nz):b.face([p(i,j),p(i,j+1),p(i+1,j+1),p(i+1,j)],mat)
    o=mesh(name,parent,b,True)
    mod=o.modifiers.new('Cloth edge thickness','SOLIDIFY');mod.thickness=.008
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)

def home_details(parent,M,interior):
    setup(parent)
    if interior:
        old=bpy.data.objects.get('PlayerBed')
        if old:bpy.data.objects.remove(old,do_unlink=True)
        x,z=-4.85,3.1;b=geo.MeshBuilder()
        for xx in (-.63,.63):
            for zz in (-1.015,1.015):b.box((x+xx,.16,z+zz),(.08,.32,.08),M['wood'])
        b.box((x,.30,z),(1.43,.16,2.24),M['wood']);b.box((x,.72,z+1.06),(1.45,.85,.09),M['wood'])
        mesh('Refined_BedFrame',parent,b)
        rounded_box('Refined_Mattress',parent,(x,.48,z),(1.4,.22,2.18),M['linen'],.09)
        cloth('Refined_BlueDuvet',parent,x,z-.30,1.58,1.50,.635,M['blue'])
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=(x,-(z+.72),.65))
        pillow=bpy.context.object;pillow.name='Refined_Pillow';pillow.scale=(.53,.28,.115)
        for vert in pillow.data.vertices:
            for axis,power in ((0,.46),(1,.46),(2,.8)):
                val=vert.co[axis];vert.co[axis]=math.copysign(abs(val)**power,val)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);pillow.parent=parent;pillow.data.materials.append(M['linen'])
        for f in pillow.data.polygons:f.use_smooth=True
        # Side curtains retain the existing clear window aperture and walking volume.
        for cx in (-4.88,-2.94):
            b=geo.MeshBuilder()
            for i in range(18):
                u=i/18;v=(i+1)/18
                def p(t,y):return (cx+(t-.5)*.30,y,4.21+.045*math.cos(t*math.tau*3)+.02*math.sin(y*2))
                b.face([p(u,.94),p(v,.94),p(v,2.57),p(u,2.57)],M['linen'])
            mesh('Refined_WindowCurtain',parent,b,True)
        b=geo.MeshBuilder();b.rod((-5.1,2.63,4.20),(-2.7,2.63,4.20),.018,M['metal'],12)
        mesh('Refined_CurtainRod',parent,b)
    # Roof rafters and window reveals remain within the existing porch footprint.
    b=geo.MeshBuilder()
    for x in [i*.55 for i in range(-10,11)]:b.box((x,2.81,5.25),(.075,.16,1.48),M['wood'])
    for x in (-3.9,3.9):
        for xx in (x-1.035,x+1.035):b.box((xx,1.73,4.53),(.07,1.48,.13),M['wood'])
        b.box((x,.965,4.55),(2.24,.09,.30),M['stone'])
    mesh('Refined_PorchConstruction',parent,b)

def tree(parent,original,M,seed):
    """Tapered connected limbs and folded opaque leaves; no spherical crown blobs."""
    setup(parent);rng=random.Random(seed);b=geo.MeshBuilder()
    def branch(a,c,r1,r2):
        a=Vector(a);c=Vector(c);axis=(c-a).normalized();u=axis.cross(Vector((0,0,1))).normalized()
        if u.length<.1:u=Vector((1,0,0))
        v=axis.cross(u).normalized();sides=9
        for i in range(sides):
            t=i*math.tau/sides;s=(i+1)*math.tau/sides
            ra=u*math.cos(t)+v*math.sin(t);rb=u*math.cos(s)+v*math.sin(s)
            b.face([a+ra*r1,a+rb*r1,c+rb*r2,c+ra*r2],M['bark'])
    branch((0,0,0),(.10,2.5,0),.30,.22)
    branch((.10,2.5,0),(.10,5.55,0),.22,.035)
    for level in range(4):
        for k in range(7):
            a=k*math.tau/7+level*.67+rng.uniform(-.18,.18);radius=2.9-level*.35
            start=Vector((.10,2.4+level*.83,0));end=Vector((math.cos(a)*radius,4.25+level*.67,math.sin(a)*radius))
            mid=start.lerp(end,.56)+Vector((0,.28,0));branch(start,mid,.10-level*.013,.055);branch(mid,end,.055,.015)
            for j in range(8):
                t=.38+j*.082;base=mid.lerp(end,t)
                aa=a+(-1 if j%2 else 1)*rng.uniform(.65,1.25)
                tip=base+Vector((math.cos(aa)*.74,rng.uniform(.04,.34),math.sin(aa)*.74))
                branch(base,tip,.018,.004)
                for h in range(13):
                    p=base.lerp(tip,(h+1)/14);theta=aa+(-1 if h%2 else 1)*rng.uniform(.7,1.6)
                    length=rng.uniform(.18,.29);axis=Vector((math.cos(theta),rng.uniform(-.3,.55),math.sin(theta)))
                    side=Vector((-math.sin(theta),0,math.cos(theta)))*length*.40
                    q=p+axis*length;center=p+axis*length*.53+Vector((0,.022,0))
                    left=p+axis*length*.42+side;right=p+axis*length*.42-side
                    mat=M[rng.choices(['leaf','leaflight','leafdark'],[5,3,2])[0]]
                    b.face([p,left,center],mat);b.face([left,q,center],mat);b.face([q,right,center],mat);b.face([right,p,center],mat)
    obj=mesh('Refined_'+original.name.removeprefix('Refined_'),parent,b,True)
    obj.matrix_world=original.matrix_world.copy()
    # Original trees carry a mix of applied/unapplied scaling; keep local crown size
    # explicitly comparable rather than multiplying it by original mesh extents.
    obj.scale=original.scale.copy()
    bpy.data.objects.remove(original,do_unlink=True)

def village_details(parent,M):
    targets=[bpy.data.objects['HomeExterior'],bpy.data.objects['Building_shop']]
    for o in targets:recolor(o.children_recursive,M)
    home_details(targets[0],M,False)
    shop=targets[1];setup(shop);b=geo.MeshBuilder()
    # Individual eave ends, shopfront slats and window returns are medium-scale detail.
    for i in range(23):b.box((-5.7+i*.51,3.60,4.57),(.075,.16,.56),M['wood'])
    for x in (-4.2,4.2):
        for j in range(7):b.box((x-.69+j*.23,1.5,4.68),(.035,1.10,.055),M['wood'])
        b.box((x,.91,4.64),(1.78,.075,.35),M['stone'])
    mesh('Refined_ShopJoinery',shop,b)
    for name,seed in [('PlazaTree',920),('Tree_023',921),('Tree_028',922),('Tree_040',923)]:
        tree(parent,bpy.data.objects[name],M,seed)

def init():
    if any((SOURCE/(k+'.blend')).exists() for k in SEEDS):raise RuntimeError('Saved sample exists. Edit it; use export. init refuses overwrite.')
    for kind,seed in SEEDS.items():
        bpy.ops.wm.open_mainfile(filepath=str(ROOT/seed),load_ui=False)
        M=materials()
        if kind=='home':
            root=bpy.data.objects['Home'];recolor(root.children_recursive,M);home_details(root,M,True)
        elif kind=='village':village_details(bpy.data.objects['VillageV1'],M)
        else:
            objects=[o for o in bpy.data.objects if not o.name.startswith('Lookdev')]
            recolor(objects,M)
        bpy.context.scene['refinementBaseline']=seed
        bpy.context.scene['refinementBaselineSha256']=sha(ROOT/seed)
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(kind+'.blend')))
    export()

def export():
    records={}
    for kind,seed in SEEDS.items():
        source=SOURCE/(kind+'.blend');bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False)
        excluded=set()
        for col in bpy.data.collections:
            if col.name.startswith('Lookdev'):excluded.update(col.all_objects)
        objects=[o for o in bpy.data.objects if o not in excluded and not o.name.startswith('Lookdev') and o.type in ('MESH','EMPTY')]
        for o in objects:o.hide_set(False)
        out=DEST/(kind+'.glb');exporter.export_glb(objects,out)
        records[kind]={'source':str(source.relative_to(ROOT)).replace('\\','/'),'sourceSha256':sha(source),
            'baseline':seed,'baselineSha256':bpy.context.scene['refinementBaselineSha256'],
            'glbSha256':sha(out),'glbBytes':out.stat().st_size}
    (DEST/'manifest.json').write_text(json.dumps({'version':1,'visualApproval':False,'formalDefault':False,
        'method':'Saved Blender derivatives, scripted geometry and visual iteration; no character export',
        'sources':records},indent=2),encoding='utf-8')

def refine():
    """One recorded visual repair on the saved samples; never touches the seed sources."""
    for kind in ('home','village'):
        source=SOURCE/(kind+'.blend');bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False)
        if bpy.context.scene.get('refinementRepair1'):continue
        keys=('wood','linen','blue','metal','stone') if kind=='home' else ('bark','leaf','leaflight','leafdark')
        M={k:bpy.data.materials['Refined_'+k] for k in keys}
        if kind=='home':
            for o in list(bpy.data.objects):
                if o.name.startswith('Refined_'):bpy.data.objects.remove(o,do_unlink=True)
            home_details(bpy.data.objects['Home'],M,True)
        else:
            for name,seed in [('PlazaTree',920),('Tree_023',921),('Tree_028',922),('Tree_040',923)]:
                tree(bpy.data.objects['VillageV1'],bpy.data.objects['Refined_'+name],M,seed)
        bpy.context.scene['refinementRepair1']=True
        bpy.ops.wm.save_as_mainfile(filepath=str(source))
    export()

def check():
    manifest=json.loads((DEST/'manifest.json').read_text())
    for kind,r in manifest['sources'].items():
        assert sha(ROOT/r['source'])==r['sourceSha256']
        assert sha(ROOT/r['baseline'])==r['baselineSha256']
        p=DEST/(kind+'.glb');assert sha(p)==r['glbSha256']
        d=exporter.glb_data(p);names={n.get('name','') for n in d['nodes']}
        assert not d.get('skins') and not d.get('animations')
        assert not any('Lookdev' in n or 'Protagonist' in n for n in names)
        if kind=='home':
            assert {'Refined_Pillow','Refined_BlueDuvet','Refined_Mattress'}<=names
            old=exporter.glb_data(ROOT/'assets/generated/home/anime-v8/home-anime-v8.glb')
            new={n.get('name'):n for n in d['nodes']}
            for n in old['nodes']:
                if n.get('name','').startswith('Door_') and 'Hinge' in n['name']:
                    for field in ('translation','rotation','scale','matrix'):assert n.get(field)==new[n['name']].get(field)
        if kind=='classroom':assert {'Clock_'+s for s in ('Missing','HandOnly','KeyOnly','Ready','Restored')}<=names
    print('ENVIRONMENT_REFINEMENT_ASSET_PASS oldSourcesProtected=true doorPivotsPreserved=true noCharacters=true')

if __name__=='__main__':
    mode=sys.argv[sys.argv.index('--')+1]
    {'init':init,'export':export,'check':check,'refine':refine}[mode]()
