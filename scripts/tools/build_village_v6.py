"""Saved-v5/home-v1 derivative. Never rebuilds old assets. init refuses overwrite; check reexports in temp."""
import bpy,json,re,sys,math,random,tempfile,hashlib,struct
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v5 as v5
import build_village_v4 as v4
import build_village_v3 as v3
import build_village_v1 as b
R=v4.R;SOURCE=R/'art/blender/village/village-v6.blend';GLB=R/'assets/generated/village/3d/village-v6.glb';META=GLB.with_name('village-v6-manifest.json')
HS=R/'art/blender/home/home-identity-v2.blend';HG=R/'assets/generated/home/3d/home-identity-v2.glb';HM=HG.with_suffix('.json')
H0=R/'art/blender/home/home-v1.blend';G0=R/'assets/generated/home/3d/home-v1.glb'
LAYOUT=R/'scripts/presentation/HomeIdentityLayout3D.cs'
D=json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";',LAYOUT.read_text(),re.S)[1])
sha=v4.sha
def tri(mesh):mesh.calc_loop_triangles();return len(mesh.loop_triangles)
def prepare_initial():
    # Same packed CC0 maps imported from both saved sources must share one image datablock.
    images={}
    for image in bpy.data.images:
        if not image.packed_file:continue
        key=(hashlib.sha256(image.packed_file.data).hexdigest(),image.colorspace_settings.name)
        if key in images:image.user_remap(images[key])
        else:images[key]=image
    for o in bpy.data.objects:
        if o.type!='MESH' or not is_home_change(o.name):continue
        used={p.material_index for p in o.data.polygons}
        for i in reversed(range(len(o.data.materials))):
            if i not in used:o.data.materials.pop(index=i)
def is_home_change(n):return n.startswith(('Wall_south','Wall_north','Wall_west','Wall_east','Fence','RoofTiles','Plant')) or n in ['HomeOpenYardGate','PorchBench']
def changed(n):return is_home_change(n) or n.startswith('Tree_') and n not in ['Tree_037','Tree_043']
def material_signatures(path):
    raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+size])
    images=[]
    for image in d.get('images',[]):
        view=d['bufferViews'][image['bufferView']];start=28+size+view.get('byteOffset',0)
        images.append(hashlib.sha256(raw[start:start+view['byteLength']]).hexdigest())
    def texture(i):
        t=d['textures'][i];return {'image':images[t['source']],'sampler':d['samplers'][t['sampler']] if 'sampler' in t else {}}
    def normalize(value):
        if isinstance(value,list):return [normalize(x) for x in value]
        if isinstance(value,dict):return {k:(texture(v) if k=='index' else normalize(v)) for k,v in value.items()}
        return value
    return {m['name']:normalize(m) for m in d['materials']}
def setup_home():
    b.collection=bpy.data.collections.get('Home') or next(c for c in bpy.data.collections if bpy.data.objects['Home'].name in c.objects)
    b.village=bpy.data.objects['Home']
    b.gray=False
def mats():
    global M
    red=bpy.data.materials.get('V6_OchrePaint')
    if red is None:red=v3.v2.material('OchrePaint',(.31,.092,.052),.84);red.name='V6_OchrePaint'
    M={k:bpy.data.materials[n] for k,n in {'wall':'V5_Ivory','wood':'V5_Timber','roof':'V3_Roof','stone':'V3_Stone','metal':'Home_Aluminium','dark':'Village_Dark','repair':'V5_Repair','ivory':'Village_SignIvory'}.items()};M['red']=red
def assign(o,material,filter=None):
    o.data=o.data.copy()
    if material not in list(o.data.materials):o.data.materials.append(material)
    idx=list(o.data.materials).index(material)
    for p in o.data.polygons:
        if filter is None or filter(p):p.material_index=idx
def home_style(root):
    for o in list(bpy.data.objects):
        n=o.name
        if n.startswith(('Wall_south','Wall_north','Wall_west','Wall_east')):
            outward=Vector((0,-1,0)) if 'south' in n else Vector((0,1,0)) if 'north' in n else Vector((-1,0,0)) if 'west' in n else Vector((1,0,0))
            assign(o,M['wall'],lambda p:p.normal.dot(outward)>.5)
        elif n.startswith('Fence'):assign(o,M['wall'])
        elif n.startswith('RoofTiles'):assign(o,M['roof'])
        elif n=='HomeOpenYardGate':assign(o,M['red'])
        elif n=='PorchBench':assign(o,M['wood'])
        elif n.startswith('Plant') and o.type=='MESH':
            # Retain plant volume/position and leaves; vary only the existing pot surfaces.
            o.data=o.data.copy()
            for slot in o.material_slots:
                if any(s in slot.material.name.lower() for s in ['ceramic','terra','pot']):slot.material=M['red' if n=='Plant1' else 'stone' if n=='Plant2' else 'ivory']
    mesh=b.MeshBuilder()
    # The existing 2.8m opening is untouched: all coloured trim lies outside its sides.
    for sign in [-1,1]:
        mesh.box((sign*1.60,.76,12.015),(.36,1.52,.04),M['red'])
        mesh.box((sign*5.70,.22,5.86),(.175,.44,.175),M['wood'])
        mesh.box((sign*5.70,2.81,5.86),(.175,.20,.175),M['wood'])
        mesh.box((sign*3.52,.24,4.511),(5.00,.46,.016),M['stone'])
        mesh.box((sign*6.015,.24,0),(.025,.46,8.8),M['stone'])
    mesh.box((0,2.965,6.112),(12.15,.15,.045),M['wood'])
    for sign in [-1,1]:mesh.box((sign*.88,1.22,4.515),(.12,2.44,.035),M['red'])
    mesh.box((0,2.49,4.515),(1.88,.10,.035),M['red'])
    for x in [-5.5,-3.6,3.6,5.5]:mesh.box((x,2.86,5.7),(.10,.13,.73),M['wood'])
    for x,y,w,h in [(-2.7,.65,.7,.24),(5.15,.5,.43,.32)]:
        mesh.face([(x,y,4.514),(x+w,y,4.514),(x+w,y+h*.6,4.514),(x+w*.72,y+h*.6,4.514),(x+w*.72,y+h,4.514),(x,y+h,4.514)],M['repair'])
    mesh.finish('V6_HomeTrim',root)
    for p in D['props']:
        ob=b.empty('V6_'+p['id'],p['position']);ob.parent=root;mesh=b.MeshBuilder()
        if p['id']=='Bicycle':
            def rod(a,c,r=.017,mat=None,sides=6):mesh.rod(a,c,r,mat or M['red'],sides)
            # Low-poly tubular rims/tyres with open wheel centres and shared steel spokes.
            for x in [-.58,.58]:
                for j in range(24):
                    a=j*math.tau/24;c=(j+1)*math.tau/24
                    rod((x+.325*math.cos(a),.345+.325*math.sin(a),0),(x+.325*math.cos(c),.345+.325*math.sin(c),0),.024,M['dark'],6)
                    if j%3==0:rod((x,.345,0),(x+.304*math.cos(a),.345+.304*math.sin(a),0),.003,M['metal'],4)
            rear=(-.58,.345,0);pedal=(-.12,.31,0);seat=(-.22,.80,0);head=(.40,.84,0);front=(.58,.345,0)
            for a,c in [(rear,pedal),(pedal,seat),(seat,rear),(seat,head),(head,pedal),(head,front)]:rod(a,c)
            rod(seat,(-.24,.93,0),.016,M['metal']);mesh.box((-.25,.95,0),(.25,.055,.20),M['dark'])
            rod(head,(.38,1.035,0),.014,M['metal']);rod((.38,1.035,-.21),(.38,1.035,.21),.014,M['metal'])
            rod((-.12,.31,-.12),(-.12,.31,.12),.019,M['metal']);mesh.box((-.10,.27,.16),(.17,.035,.11),M['dark']);mesh.box((-.16,.36,-.16),(.17,.035,.11),M['dark'])
            for z in [-.10,.10]:rod((-.78,.73,z),(-.38,.73,z),.010,M['metal']);rod((-.58,.35,z),(-.73,.72,z),.009,M['metal'])
            rod((-.1,.36,.04),(-.24,.02,.20),.010,M['metal'])
        else:
            for y in [.05,.30,.56]:mesh.box((0,y,0),(.62,.065,.52),M['wood'])
            for x in [-.28,.28]:
                for z in [-.23,.23]:mesh.box((x,.31,z),(.055,.62,.055),M['wood'])
            for i in range(2):mesh.box((0,.13+i*.27,0),(.42,.09,.38),M['ivory'])
        mesh.finish(ob.name+'_Mesh',ob)

def tree_mesh(kind):
    rng=random.Random(613+kind);mesh=b.MeshBuilder();v3.mats={'leaf':bpy.data.materials['V3_Leaves']};bark=bpy.data.materials['V3_Bark']
    v3.tapered(mesh,[(0,0,0),(.035,1.6,0),(-.05,3,.06),(.07,4.4,0),(0,6.4,0)],[.235,.17,.13,.075,.012],bark,8)
    for k in range(7):
        a=k*2.399+kind*.7;level=3.2+(k%3)*.44;reach=(2.55 if kind==0 else 1.95 if kind==1 else 2.35)*rng.uniform(.75,1)
        start=Vector((0,level,0));end=Vector((math.cos(a)*reach,5.2+(k%3)*.43+( .28 if kind==1 else 0),math.sin(a)*reach));mid=start.lerp(end,.5)+Vector((0,.18,0))
        v3.tapered(mesh,[start,mid,end],[.085,.043,.006],bark,7)
        for j in range(3):
            root=mid.lerp(end,j/3);tip=root+Vector((math.cos(a+j)*.5,.24,math.sin(a+j)*.5));v3.tapered(mesh,[root,tip],[.016,.003],bark,4)
            for q in range(5 if kind!=2 else 4):
                p=root.lerp(tip,q/4)+Vector((rng.uniform(-.32,.32),rng.uniform(-.15,.24),rng.uniform(-.32,.32)))
                v3.leaf_card(mesh,p,rng.uniform(.85,1.15),rng)
    for j in range(20):v3.leaf_card(mesh,(rng.uniform(-.8,.8),rng.uniform(5.8,6.6),rng.uniform(-.8,.8)),rng.uniform(.9,1.14),rng)
    ob=mesh.finish('V6_TreePrototype');result=ob.data;result.name='V6_Tree_'+D['treeVariants'][kind];bpy.data.objects.remove(ob,do_unlink=True)
    for p in result.polygons:p.use_smooth=p.material_index==0
    assert tri(result)<=D['treeTriangleLimit'],tri(result)
    return result
def verify_source():
    old=json.loads(bpy.context.scene['v6Preserved'])
    for name,sig in old.items():assert v3.fingerprint(bpy.data.objects[name])==sig,'preserved '+name
    return old
def emit(source,glb,manifest,baseline,baseglb,home=False):
    old=verify_source();summary=v3.ex.summarize('home' if home else 'village');memory=v3.ex.texture_memory_mib('home' if home else 'village')
    v3.export_glb(glb)
    original=v3.glb_signatures(baseglb);actual=v3.glb_signatures(glb)
    preserved={k:v for k,v in original.items() if not (is_home_change(k) if home else changed(k))}
    assert all(actual.get(k)==sig for k,sig in preserved.items())
    assert not summary['missingExternalFiles']
    combined=summary['triangleCount']-89856+524288+679 if not home else summary['triangleCount']
    if not home:
        assert combined<=1200000 and memory+93.34<=384,(combined,memory)
        assert summary['materialCount']+3<=64 and summary['maximumVisibleMeshCount']+125<=600
        v3.inspect(glb)
    else:assert summary['materialCount']<=24 and memory<=256 and summary['triangleCount']<=180000
    assert glb.stat().st_size<=100*1024**2
    out={'version':6,'runtimeDefault':True,'status':'adopted-home-trees','sourceSha256':sha(source),'glbSha256':sha(glb),'baselineSourceSha256':sha(baseline),'baselineGlbSha256':sha(baseglb),'layoutSha256':sha(LAYOUT),'geometry':summary,'textureMiB':memory,'combinedTextureMiB':memory+(0 if home else 93.34),'combinedTriangles':combined,'preservedMeshCount':len(preserved),'modifiedObjects':[k for k in original if k not in preserved],'terrainHashes':json.loads(v5.META.read_text())['terrainHashes'],'preservedObjects':old,'tool':'Blender '+bpy.app.version_string,'date':'2026-09-15','humanAcceptance':'accepted-for-adoption-2026-09-15'}
    if not home:out['trees']=[{'name':o.name,'mesh':o.data.name,'triangles':tri(o.data),'location':list(o.location),'scale':list(o.scale)} for o in bpy.data.objects if o.name.startswith('Tree_') or o.name=='PlazaTree']
    manifest.write_text(json.dumps(out,indent=2),encoding='utf-8');print('V6_EXPORT_PASS',source.name,summary['triangleCount'],memory,flush=True)
def init():
    assert not SOURCE.exists() and not HS.exists(),'Existing source: edit and export instead.'
    bpy.ops.wm.open_mainfile(filepath=str(v5.SOURCE),load_ui=False);v4.setup();before={o.name:v3.fingerprint(o) for o in bpy.data.objects if not changed(o.name)}
    mats();home_style(bpy.data.objects['HomeExterior']);prototypes=[tree_mesh(i) for i in range(3)]
    for o in bpy.data.objects:
        if o.name.startswith('Tree_') and o.name not in ['Tree_037','Tree_043']:
            # Stable distribution: narrow crowns beside built-up west lanes, open at creek, broad elsewhere.
            kind=2 if abs(o.location.x-v4.g.stream(-o.location.y))<15 else int(o.name[-3:])%3
            o.data=prototypes[kind]
    prepare_initial();bpy.context.view_layer.update();bpy.context.scene['v6Preserved']=json.dumps(before);verify_source();bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));emit(SOURCE,GLB,META,v5.SOURCE,v5.GLB)
    init_home()
def init_home():
    assert SOURCE.exists() and not HS.exists(),'home init requires saved village and absent home derivative'
    bpy.ops.wm.open_mainfile(filepath=str(H0),load_ui=False);setup_home();before={o.name:v3.fingerprint(o) for o in bpy.data.objects if not is_home_change(o.name)}
    names=['V5_Ivory','V5_Timber','V3_Roof','V3_Stone','V3_Brick','Village_Metal','Village_Dark','V5_Repair','Village_SignIvory','V6_OchrePaint']
    with bpy.data.libraries.load(str(SOURCE),link=False) as (a,c):c.materials=names
    mats();home_style(bpy.data.objects['Home']);prepare_initial();bpy.context.view_layer.update();bpy.context.scene['v6Preserved']=json.dumps(before);bpy.ops.wm.save_as_mainfile(filepath=str(HS));emit(HS,HG,HM,H0,G0,True)
def check():
    for source,glb,manifest,baseline,baseglb in [(SOURCE,GLB,META,v5.SOURCE,v5.GLB),(HS,HG,HM,H0,G0)]:
        m=json.loads(manifest.read_text())
        for p,key in [(source,'sourceSha256'),(glb,'glbSha256'),(baseline,'baselineSourceSha256'),(baseglb,'baselineGlbSha256'),(LAYOUT,'layoutSha256')]:assert sha(p)==m[key],str(p)
        for p,sig in m['terrainHashes'].items():assert sha(R/p)==sig,p
        oldsig=v3.glb_signatures(baseglb);newmat=material_signatures(glb);oldmat=material_signatures(baseglb)
        for name,parts in oldsig.items():
            if name in m['modifiedObjects']:continue
            for mat,_,_ in parts:assert newmat[mat]==oldmat[mat],'shared material changed '+mat
        bpy.ops.wm.open_mainfile(filepath=str(baseline),load_ui=False)
        def structural():
            result={}
            for o in bpy.data.objects:
                if o.type!='MESH' or not is_home_change(o.name):continue
                result[o.name]=([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons])
                if o.name.startswith('Wall_'):
                    n=o.name;out=Vector((0,-1,0)) if 'south' in n else Vector((0,1,0)) if 'north' in n else Vector((-1,0,0)) if 'west' in n else Vector((1,0,0))
                    result[o.name+'_inward']=[(tuple(p.vertices),o.data.materials[p.material_index].name) for p in o.data.polygons if p.normal.dot(out)<=.5]
            return result
        expected=structural()
        bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False);verify_source();assert structural()==expected,'home structure or interior face changed';assert v3.ex.summarize('village')==m['geometry']
        with tempfile.TemporaryDirectory(prefix='v6-readonly-') as t:
            p=Path(t)/'check.glb';v3.export_glb(p);assert sha(p)==m['glbSha256']
    print('VILLAGE_V6_CHECK_PASS readOnly=true home=true trees=58',flush=True)
if __name__=='__main__':
    mode=sys.argv[sys.argv.index('--')+1]
    if mode=='init':init()
    elif mode=='init-home':init_home()
    elif mode=='check':check()
    elif mode=='export':
        for source,glb,manifest,baseline,baseglb,home in [(SOURCE,GLB,META,v5.SOURCE,v5.GLB,False),(HS,HG,HM,H0,G0,True)]:
            bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False);emit(source,glb,manifest,baseline,baseglb,home)
    else:raise ValueError(mode)
