"""Four-house derivative of saved, access-repaired v4. init refuses overwrite; check never writes assets."""
import bpy,bmesh,json,re,sys,math,tempfile,hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v4 as v4
import build_village_v3 as v3
import build_village_v1 as b
R=v4.R;SOURCE=R/'art/blender/village/village-v5.blend';GLB=R/'assets/generated/village/3d/village-v5.glb';META=GLB.with_name('village-v5-manifest.json')
LAYOUT=R/'scripts/presentation/VillageHouseArtLayout3D.cs';TEX=R/'art/blender/village/v5-textures'
data=json.loads(re.search(r'public const string Data = """\s*(.*?)\s*""";',LAYOUT.read_text(),re.S)[1]);IDS={x['id'] for x in data['houses']}
CHANGED={prefix+x for x in IDS for prefix in ['HouseShell_','Yard_']}
def sha(p):return v4.sha(p)
def projected_uv(o):
    uv=o.data.uv_layers.active
    for p in o.data.polygons:
        material=o.data.materials[p.material_index].name
        if not material.startswith(('V5_','V3_')):continue
        # Actual dimensions after the inherited house-prototype scale, never stretched by house width.
        normal=p.normal;axes=(0,1) if abs(normal.z)>.55 else (1,2) if abs(normal.x)>.5 else (0,2)
        scale=.5 if 'Roof' in material or 'Brick' in material else 1
        for li in p.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=tuple(co[i]*o.scale[i]*scale for i in axes)
def adopted(name,old,image=None,tint=None):
    m=bpy.data.materials[old].copy();m.name='V5_'+name
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    if image:
        # Remove obsolete colour links/nodes so neither exporter nor budget counts unused duplicates.
        for link in list(bs.inputs['Base Color'].links):m.node_tree.links.remove(link)
        for node in list(m.node_tree.nodes):
            if node.type=='TEX_IMAGE' and not node.outputs['Color'].is_linked and not node.outputs['Alpha'].is_linked:m.node_tree.nodes.remove(node)
        n=v3.texture(m,TEX/image);source=n.outputs['Color']
        if tint:
            mix=m.node_tree.nodes.new('ShaderNodeMix');mix.data_type='RGBA';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[7].default_value=(*tint,1)
            m.node_tree.links.new(source,mix.inputs[6]);source=mix.outputs[2]
        m.node_tree.links.new(source,bs.inputs['Base Color'])
    return m
def materials():
    result={k:bpy.data.materials[v] for k,v in {'roof':'V3_Roof','brick':'V3_Brick','stone':'V3_Stone','metal':'Village_Metal','green':'Village_FadedGreen','soil':'Village_GardenSoil','leaf':'Village_FoliageMid','cloth':'Village_SignIvory','bluecloth':'Village_TarpBlue'}.items()}
    for name,tint in {'Ivory':(1,.96,.88),'Silver':(.79,.85,.87),'Warm':(.88,.75,.62),'Lime':(.86,.92,.81)}.items():result[name]=adopted(name,'V3_Plaster','plaster-color.png',tint)
    result['wood']=adopted('Timber','V3_Wood','wood-color.png')
    result['repair']=adopted('Repair','V3_Plaster','repair-color.png')
    result['blue']=v3.v2.material('unused',(.21,.32,.36),.78);result['blue'].name='V5_BluePaint'
    return result
def shells():
    for spec in data['houses']:
        key=spec['id'];o=bpy.data.objects['HouseShell_'+key];o.data=o.data.copy()
        for slot in o.material_slots:
            old=slot.material.name
            if 'Plaster' in old:slot.material=mats[spec['wall']]
            elif old.startswith('Village_Roof'):slot.material=mats['roof']
            elif old=='Village_Stone':slot.material=mats['stone']
            elif old in ['Village_Wood','Village_FadedGreen']:slot.material=mats['blue' if key=='house03' else 'green' if key=='house15' else 'wood']
            elif old=='Village_Metal' and key=='house03':slot.material=mats['blue']
        projected_uv(o)
        yard=bpy.data.objects['Yard_'+key];yard.data=yard.data.copy()
        # Original yard structural components are concrete slab, plaster walls, stone caps, brick posts.
        bm=bmesh.new();bm.from_mesh(yard.data)
        discard=[f for f in bm.faces if yard.data.materials[f.material_index].name not in ['Village_Concrete','Village_Plaster','Village_Stone','Village_Brick']]
        bmesh.ops.delete(bm,geom=discard,context='FACES');bm.to_mesh(yard.data);bm.free()
        for slot in yard.material_slots:
            if slot.material.name=='Village_Plaster':slot.material=mats[spec['wall']]
            elif slot.material.name=='Village_Stone':slot.material=mats['stone']
            elif slot.material.name=='Village_Brick':slot.material=mats['brick']
        projected_uv(yard)
def facade_details():
    for spec in data['houses']:
        key=spec['id'];building=next(x for x in v4.v['buildings'] if x['id']==key);w,h,d=building['size'];parent=bpy.data.objects['Building_'+key];mesh=b.MeshBuilder()
        # Narrow lintel and wall plinth, with no change to doorway aperture.
        mesh.box((0,2.48,d/2+.035),(1.7,.11,.08),mats['stone'])
        for side in [-1,1]:
            mesh.box((side*w/2,h-.10,0),(.08,.12,d+.14),mats['wood'])
            for floor in range(2 if h>5 else 1):
                for x in [-w*.3,w*.3]:
                    y=1.4+floor*2.9
                    mesh.box((x,y-.77,side*(d/2+.10)),(1.9,.08,.34),mats['stone'])
                    # Thin painted jamb borders surround existing glazing, not cover it.
                    for dx in [-.89,.89]:mesh.box((x+dx,y,side*(d/2+.058)),(.055,1.4,.06),mats['blue' if key=='house03' else 'wood'])
        # House-specific repairs: polygonal plaster patches follow wall surfaces, no floating decals.
        patches={'house02':[(-3.4,.24,.85,.4),(.95,1.0,.28,.70)],'house03':[(-3.7,3.08,1.65,.42)],'house13':[(3.05,1.16,1.55,.65)],'house15':[(-3.5,.2,1.25,.20)]}[key]
        for x,y,pw,ph in patches:
            z=d/2+.013
            mesh.face([(x,y,z),(x+pw*.88,y,z),(x+pw*.88,y+ph*.24,z),(x+pw,y+ph*.24,z),(x+pw,y+ph*.84,z),(x+pw*.70,y+ph*.84,z),(x+pw*.70,y+ph,z),(x+.09,y+ph,z),(x+.09,y+ph*.64,z),(x,y+ph*.64,z)],mats['brick' if key=='house13' else 'stone' if key=='house15' else 'repair'])
        if key in ['house13','house15']:
            for sign in [-1,1]:
                width=w/2-.84
                mesh.box((sign*(.84+width/2),.35,d/2+.027),(width,.67,.028),mats['brick' if key=='house13' else 'stone'])
        if key=='house13':
            # Exposed brick on the street-facing low wall and a side-wall lower band.
            mesh.box((w/2+.015,.36,0),(.025,.69,d-.12),mats['brick'])
        if key=='house15':
            mesh.box((w/2+.018,.37,0),(.025,.7,d-.1),mats['stone'])
            # Rain downpipe terminates over the barrel mouth.
            mesh.rod((3.75,h-.1,d/2+.13),(3.75,1.1,d/2+.13),.038,mats['metal'])
            mesh.rod((3.75,1.1,d/2+.13),(3.75,1.1,4.7),.038,mats['metal'])
        ob=mesh.finish('V5_'+key+'_Facade',parent);projected_uv(ob)
def prop_mesh(p):
    mesh=b.MeshBuilder();w,h,d=p['size'];kind=p['kind'];wood=mats['wood'];metal=mats['metal'];cloth=mats['bluecloth' if p['house']=='house03' else 'cloth']
    def box(x,y,z,s,mat=wood):mesh.box((x,y,z),s,mat)
    def rod(a,c,r=.018,mat=wood,sides=8):mesh.rod(a,c,r,mat,sides)
    if kind=='bench':
        for i in range(3):box(0,h-.06,-d/2+(i+.5)*d/3,(w,.08,d/3-.012))
        for x in [-w*.38,w*.38]:
            for z in [-d*.32,d*.32]:box(x,(h-.1)/2,z,(.065,h-.1,.065))
        box(0,.18,0,(w*.85,.07,.06))
    elif kind in ['shelf','cabinet']:
        for x in [-w/2+.04,w/2-.04]:
            for z in [-d/2+.04,d/2-.04]:box(x,h/2,z,(.07,h,.07),metal if kind=='cabinet' else wood)
        for y in [.07,h*.50,h-.035]:box(0,y,0,(w,.055,d))
        if kind=='cabinet':
            box(0,h*.5,d/2-.025,(w-.08,h-.12,.05),mats['blue']);box(w*.32,h*.55,d/2+.015,(.035,.18,.03),metal)
            box(0,h/2,-d/2+.02,(w,.04+h-.12,.035),mats['blue'])
            for x in [-w/2+.02,w/2-.02]:box(x,h/2,0,(.035,h-.12,d),mats['blue'])
        else:
            for level in [.1,h*.5+.035]:
                box(0,level+.13,0,(w*.68,.25,d*.75),mats['cloth'])
                for x in [-w*.3,w*.3]:box(x,level+.14,0,(.025,.27,d*.77),wood)
    elif kind=='laundry':
        for z in [-d/2+.025,d/2-.025]:rod((0,0,z),(0,h,z),.023,metal);box(0,.025,z,(w,.05,.18),metal)
        rod((0,h-.05,-d/2),(0,h-.05,d/2),.012,metal)
        for i in range(3):
            z=-d*.3+i*d*.30
            # Slight folds modelled in a strip, cloth ends above ground.
            for j in range(5):
                z0=z+(j-2.5)*.10;z1=z0+.10;x0=.024*math.sin(j*2);x1=.024*math.sin((j+1)*2)
                mesh.face([(x0,h-.10,z0),(x1,h-.10,z1),(x1,h-.78-(i%2)*.17,z1),(x0,h-.78-(i%2)*.17,z0)],cloth)
                mesh.face([(x0+.004,h-.78-(i%2)*.17,z0),(x1+.004,h-.78-(i%2)*.17,z1),(x1+.004,h-.10,z1),(x0+.004,h-.10,z0)],cloth)
    elif kind=='cloth':
        for i in range(6):box(-w/2+(i+.5)*w/6,h/2,math.sin(i*1.7)*.012,(w/6+.002,h,.015),cloth)
    elif kind=='baskets':
        for x,z,scale in [(-.23,-.20,.58),(.20,.20,.7)]:
            for y in [.07,.16,.25,.34]:
                box(x,y,z-scale/2,(scale,.065,.035));box(x,y,z+scale/2,(scale,.065,.035));box(x-scale/2,y,z,(.035,.065,scale));box(x+scale/2,y,z,(.035,.065,scale))
            box(x,.03,z,(scale,.04,scale))
            for sx in [-1,1]:box(x+sx*scale/2,.21,z,(.025,.4,.05))
    elif kind=='garden':
        box(0,.10,0,(w,.2,d),mats['soil'])
        for x in [-w/2,w/2]:box(x,.12,0,(.07,.24,d),wood)
        for z in [-d/2,d/2]:box(0,.12,z,(w,.24,.07),wood)
        for z in [-d*.38,0,d*.38]:
            rod((-w*.35,.22,z),(0,h,z),.02);rod((w*.35,.22,z),(0,h,z),.02)
        rod((0,h,-d*.4),(0,h,d*.4),.02)
        for i in range(12):
            x=(-1 if i%2 else 1)*w*.24;z=-d*.40+(i//2)*d*.15;top=.38+(i%3)*.09;rod((x,.22,z),(x,top+.20,z),.009,mats['leaf'],5)
            for sign in [-1,1]:
                mesh.face([(x,top,z),(x+sign*.20,top+.08,z-.09),(x+sign*.28,top+.02,z+.02),(x,top-.015,z+.08)],mats['leaf'])
    elif kind=='barrel':
        n=20;r=w/2
        for i in range(n):
            a=i*math.tau/n;c=(i+1)*math.tau/n
            mesh.face([(r*math.cos(a),0,r*math.sin(a)),(r*math.cos(c),0,r*math.sin(c)),(r*math.cos(c),h,r*math.sin(c)),(r*math.cos(a),h,r*math.sin(a))],mats['green'])
            for y in [.10,h-.06]:rod((r*math.cos(a),y,r*math.sin(a)),(r*math.cos(c),y,r*math.sin(c)),.018,metal)
        # Recessed dark lid/opening below rim; no new water effect.
        mesh.face([(r*.94*math.cos(i*math.tau/n),h-.04,r*.94*math.sin(i*math.tau/n)) for i in reversed(range(n))],mats['stone'])
    elif kind=='mat':
        for i in range(9):box(-w/2+(i+.5)*w/9,h/2,0,(w/9-.015,h,d))
    else:raise ValueError(kind)
    parent=b.empty('V5_'+p['house']+'_'+p['id'],p['position']);parent.parent=bpy.data.objects['Building_'+p['house']]
    ob=mesh.finish(parent.name+'_Mesh',parent);projected_uv(ob)
def source_checks():
    saved=json.loads(bpy.context.scene['v5PreservedObjects'])
    for name,sig in saved.items():assert v3.fingerprint(bpy.data.objects[name])==sig,'outside scope '+name
    for name in CHANGED:assert bpy.data.objects[name].data.uv_layers
    assert all(v3.fingerprint(bpy.data.objects['Building_'+k])==saved['Building_'+k] for k in IDS)
    return saved
def export():
    saved=source_checks();summary=v3.ex.summarize('village');memory=v3.ex.texture_memory_mib('village');oldmeta=json.loads(v4.META.read_text())
    assert not summary['missingExternalFiles'];assert summary['triangleCount']-oldmeta['geometry']['triangleCount']<=15000
    assert memory-oldmeta['textureMiB']<=20 and memory+93.34<=384,(memory,oldmeta['textureMiB'])
    assert summary['materialCount']+3<=64 and summary['maximumVisibleMeshCount']-1+126<=600
    combined=summary['triangleCount']-89856+524288+679;assert combined<=1200000
    v3.export_glb(GLB);actual=v3.inspect(GLB);old=v3.glb_signatures(v4.GLB);new=v3.glb_signatures(GLB)
    kept={k for k in old if k not in CHANGED};assert all(new.get(k)==old[k] for k in kept)
    assert GLB.stat().st_size<=100*1024**2
    meta={'version':5,'status':'independent-house-sample','runtimeDefault':False,'sourceSha256':sha(SOURCE),'glbSha256':sha(GLB),'baselineSourceSha256':sha(v4.SOURCE),'baselineGlbSha256':sha(v4.GLB),'fixtureLayoutSha256':sha(LAYOUT),'layoutSha256':sha(v4.LAYOUT),'geometry':summary,'textureMiB':memory,'combinedTextureMiB':memory+93.34,'combinedTriangles':combined,'modifiedObjects':sorted(CHANGED),'preservedMeshCount':len(kept),'actualGlb':actual,'preservedObjects':saved,'terrainHashes':{str(p.relative_to(R)):sha(p) for p in (R/'assets/generated/village/terrain-v4').rglob('*') if p.is_file() and not p.name.endswith('.uid')},'tool':'Blender '+bpy.app.version_string,'date':'2026-09-15','humanAcceptance':'pending','sourceProvenance':'art/blender/village/v5-textures/provenance.json'}
    META.write_text(json.dumps(meta,indent=2),encoding='utf-8');print('VILLAGE_V5_EXPORT_PASS',summary['triangleCount'],memory,len(kept),flush=True)
def init():
    global mats
    if SOURCE.exists():raise RuntimeError('Saved v5 exists: edit then export, never initialize again.')
    bpy.ops.wm.open_mainfile(filepath=str(v4.SOURCE),load_ui=False);v4.setup()
    before={o.name:v3.fingerprint(o) for o in bpy.data.objects if o.name not in CHANGED};mats=materials();shells();facade_details()
    for p in data['props']:prop_mesh(p)
    bpy.context.view_layer.update();bpy.context.scene['v5PreservedObjects']=json.dumps(before);bpy.context.scene['sampleOnly']=True;bpy.context.scene['runtimeDefault']=False
    source_checks();bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));export()
def check():
    m=json.loads(META.read_text())
    for p,key in [(SOURCE,'sourceSha256'),(GLB,'glbSha256'),(v4.SOURCE,'baselineSourceSha256'),(v4.GLB,'baselineGlbSha256'),(LAYOUT,'fixtureLayoutSha256')]:assert sha(p)==m[key],key
    for p,s in m['terrainHashes'].items():assert sha(R/p)==s,p
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False);v4.setup();source_checks();assert v3.ex.summarize('village')==m['geometry'];v3.inspect(GLB)
    old=v3.glb_signatures(v4.GLB);new=v3.glb_signatures(GLB);assert all(new.get(k)==sig for k,sig in old.items() if k not in CHANGED)
    with tempfile.TemporaryDirectory(prefix='village-v5-check-') as t:
        p=Path(t)/'check.glb';v3.export_glb(p);assert sha(p)==m['glbSha256']
    print('VILLAGE_V5_CHECK_PASS readOnly=true preserved='+str(m['preservedMeshCount']),flush=True)
if __name__=='__main__':
    mode=sys.argv[sys.argv.index('--')+1]
    if mode=='init':init()
    elif mode=='export':bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False);v4.setup();export()
    elif mode=='check':check()
    else:raise ValueError(mode)
