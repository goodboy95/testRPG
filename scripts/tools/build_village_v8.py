"""V8 look-development sample. Saved-source production, never rebuilds old sources.

Blender --background --factory-startup --python this_file -- init|export|check
init refuses both existing editable sources. export does not save Blender files.
Original authored geometry/textures only; no generated-image service or download.
"""
import bpy, bmesh, json, math, random, sys, hashlib
from pathlib import Path
from mathutils import Vector
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v1 as geom
import build_village_v7 as stream

R=Path(__file__).resolve().parents[2]
SOURCE=R/'art/blender/village/village-v8.blend'
HOME=R/'art/blender/home/home-anime-v8.blend'
BASE=R/'art/blender/village/village-v7.blend'
HOMEBASE=R/'art/blender/home/home-identity-v2.blend'
DEST=R/'assets/generated/village/stream-v8'
HOMEDEST=R/'assets/generated/home/anime-v8/home-anime-v8.glb'
TERRAIN=R/'assets/generated/village/terrain-v8'
SAMPLE_TREES=('Tree_023','Tree_028','Tree_040','PlazaTree')
PALETTE={'wall':'e8d8b5','roof':'7c8c90','rooflight':'909c9b','wood':'97704c','woodlight':'bd9767',
 'stone':'aaa691','paving':'c4bca5','red':'b5654d','green':'789877','ivory':'f0e4c7','glass':'5f8e9b',
 'metal':'718886','dark':'465b60','bark':'827052','leaf':'7aa65f','leaflight':'a4bc72','leafdark':'5b895d',
 'brick':'b19179','soil':'99916d','grass':'96ac75'}

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def linear(hex):
    c=[int(hex[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)

def setup(root):
    geom.collection=root.users_collection[0];geom.village=root

def materials():
    result={}
    for key,value in PALETTE.items():
        name='V8_'+key;mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes=True;bs=mat.node_tree.nodes.get('Principled BSDF');col=linear(value)
        bs.inputs['Base Color'].default_value=(*col,1);bs.inputs['Roughness'].default_value=.88 if key!='glass' else .42
        bs.inputs['Specular IOR Level'].default_value=.18;mat.diffuse_color=(*col,1)
        # Low-frequency original pigment variation, authored mathematically and packed.
        if key in ('wall','wood','woodlight','stone','paving','roof','brick'):
            size=128;y,x=np.mgrid[:size,:size]/size
            wave=.018*np.sin(x*math.tau*3+np.sin(y*math.tau*2))+.012*np.cos(y*math.tau*4+x*math.tau)
            if key.startswith('wood'):wave+=.025*np.sin(y*math.tau*14+.7*np.sin(x*math.tau))
            pixels=np.ones((size,size,4),dtype=np.float32)
            # Byte-backed generated images store sRGB values; factors above are linear.
            # Supplying linear values here would encode dark/yellow pigment maps twice.
            for k in range(3):pixels[:,:,k]=np.clip((int(value[k*2:k*2+2],16)/255)*(1+wave),0,1)
            im=bpy.data.images.new(name+'_pigment',width=size,height=size);im.pixels.foreach_set(pixels.ravel());im.pack()
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
            mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        result[key]=mat
    return result

def key_for(name):
    s=name.lower()
    if any(k in s for k in ('glass','screen')):return 'glass'
    if 'roof' in s:return 'roof'
    if 'ochre' in s or 'red' in s:return 'red'
    if 'canopygreen' in s or 'fadedgreen' in s:return 'green'
    if 'bark' in s:return 'bark'
    if 'leaf' in s or 'foliage' in s or s=='home_green':return 'leaf'
    if 'brick' in s:return 'brick'
    if any(k in s for k in ('wood','timber','plywood')):return 'woodlight' if 'pale' in s or 'edge' in s else 'wood'
    if any(k in s for k in ('dark','ink')):return 'dark'
    if 'stone' in s or 'mortar' in s:return 'stone'
    if any(k in s for k in ('metal','aluminium','galvanised','tarp','blue')):return 'metal'
    if any(k in s for k in ('concrete','tile','grout')):return 'paving'
    if any(k in s for k in ('paper','fabric','sack','jute','ivory')):return 'ivory'
    return 'wall'

def remat(o,M,only=None):
    o.data=o.data.copy()
    for slot in o.material_slots:
        if slot.material and (only is None or slot.material.name in only):slot.material=M[key_for(slot.material.name)]

def finish(b,name,parent,bevel=0):
    o=b.finish(name,parent)
    if bevel:
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.to_mesh(o.data);bm.free()
        mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=2;mod.limit_method='ANGLE'
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def roof(parent,M,w,h,d,name):
    b=geom.MeshBuilder();reach=d/2+.36;half=w/2+.38;rise=1.38
    # Closed roof skin with a gently relaxed eave, shared by all four LODs.
    for sign in (-1,1):
        for iz in range(6):
            t0=iz/6;t1=(iz+1)/6
            def height(t):return h+rise*(1-t)+.11*t**5
            z0=sign*reach*t0;z1=sign*reach*t1;y0=height(t0);y1=height(t1)
            p=[(-half,y0,z0),(half,y0,z0),(half,y1,z1),(-half,y1,z1)]
            if sign==1:p.reverse()
            b.face(p,M['roof']);b.face([(x,y-.12,z) for x,y,z in reversed(p)],M['roof'])
            for x in (-half,half):b.rod((x,y0,z0),(x,y1,z1),.065,M['rooflight'],6)
        b.box((0,height(1)-.085,sign*reach),(w+.78,.15,.13),M['wood'])
        # Widely spaced shallow seams imply tile runs instead of thousands of noisy tiles.
        for ix in range(29):
            x=-half+.06+ix*(2*half-.12)/28
            for iz in range(6):
                t0=iz/6;t1=(iz+1)/6
                b.rod((x,height(t0)+.016,sign*reach*t0),(x,height(t1)+.016,sign*reach*t1),.014,M['rooflight'],4)
        for row in range(1,9):
            t=row/9
            b.rod((-half,height(t)+.022,sign*reach*t),(half,height(t)+.022,sign*reach*t),.022,M['rooflight'],5)
    b.rod((-half-.025,h+rise+.035,0),(half+.025,h+rise+.035,0),.105,M['rooflight'],8)
    return finish(b,name,parent)

def remove_roof(o):
    bm=bmesh.new();bm.from_mesh(o.data)
    faces=[f for f in bm.faces if 'roof' in o.data.materials[f.material_index].name.lower()]
    bmesh.ops.delete(bm,geom=faces,context='FACES');bm.to_mesh(o.data);bm.free()

def shop_style(id,M):
    root=bpy.data.objects['Building_'+id];h=3.8 if id=='shop' else 3.6;d=9 if id=='shop' else 8
    remove_roof(bpy.data.objects['PublicShell_'+id])
    for o in list(root.children_recursive):
        if o.type=='MESH':remat(o,M)
    roof(root,M,12,h,d,'V8_'+id+'_Roof')
    b=geom.MeshBuilder()
    for x in (-5.9,5.9):
        b.box((x,h*.48,d/2+.05),(.16,h*.96,.12),M['stone'])
    b.box((0,h-.15,d/2+.10),(12.12,.17,.18),M['wood'])
    for x in (-4.2,4.2):
        for xx in (x-.87,x+.87):b.box((xx,1.72,d/2+.11),(.095,1.65,.19),M['wood'])
        b.box((x,2.57,d/2+.12),(1.90,.13,.27),M['wood'])
        b.box((x,.88,d/2+.16),(1.98,.12,.38),M['stone'])
        b.box((x,1.72,d/2+.17),(.055,1.58,.05),M['woodlight'])
    # A continuous skirting course gives weight without overfilling the working forecourt.
    for i in range(23):
        x=-5.72+i*.52
        if abs(x)<1.85:continue
        b.box((x,.28,d/2+.04),(.50,.45,.075),M['stone' if id=='shop' else 'brick'])
    finish(b,'V8_'+id+'_Facade',root,.015)
    if id=='shop':
        for n in ('V2_ShopAwning','V3_ShopCanopySeams'):
            if bpy.data.objects.get(n):bpy.data.objects.remove(bpy.data.objects[n],do_unlink=True)
        b=geom.MeshBuilder()
        for i in range(14):
            x0=-5.6+i*.8;x1=x0+.8;mat=M['green' if i%2==0 else 'ivory']
            for k in range(6):
                t=k/6;u=(k+1)/6
                def y(v):return 3.14-.22*v-.13*math.sin(v*math.pi)
                b.face([(x0,y(t),4.5+2.25*t),(x0,y(u),4.5+2.25*u),(x1,y(u),4.5+2.25*u),(x1,y(t),4.5+2.25*t)],mat)
            # Soft scalloped edge rather than a ruler-straight fabric strip.
            for k in range(6):
                a=k/6;c=(k+1)/6
                b.face([(x0+.8*a,2.93,6.75),(x0+.8*a,2.77-.055*math.sin(a*math.pi),6.75),(x0+.8*c,2.77-.055*math.sin(c*math.pi),6.75),(x0+.8*c,2.93,6.75)],mat)
        finish(b,'V8_ShopCanvas',root)
    else:
        b=geom.MeshBuilder()
        for x in (-5.2,5.2):
            b.rod((x,2.40,9.35),(x,2.98,8.55),.05,M['wood'],8)
        b.box((0,2.96,9.58),(11.06,.18,.14),M['wood'])
        finish(b,'V8_WorkshopFascia',root,.012)

def home_style(root,M):
    setup(root)
    for o in list(root.children_recursive):
        if o.type!='MESH':continue
        n=o.name
        if n.startswith(('Wall_south','Wall_north','Wall_east','Wall_west')):remat(o,M,{'V5_Ivory'})
        elif n.startswith(('RoofTiles','RoofRidge')):bpy.data.objects.remove(o,do_unlink=True)
        elif n.startswith(('Fence','V6_','Plant')) or n in ('HomeOpenYardGate','PorchBench','Porch','PorchCanopy','Courtyard','PorchPillarEast','PorchPillarWest','RoofGables'):remat(o,M)
    roof(root,M,12,3.32,9,'V8_HomeRoof')
    b=geom.MeshBuilder()
    # Fence caps remain outside the exact 2.8 m gate, with no change to collision.
    for x in (-4.4,4.4):b.box((x,1.52,11.9),(6.02,.12,.28),M['roof'])
    for x in (-7.4,7.4):b.box((x,1.52,3),(.28,.12,18),M['roof'])
    b.box((0,1.52,-5.9),(14.6,.12,.28),M['roof'])
    for x in (-3.9,3.9):
        b.box((x,.98,4.57),(2.22,.11,.28),M['stone'])
        b.box((x,2.47,4.57),(2.24,.12,.24),M['wood'])
        for xx in (x-1.08,x+1.08):b.box((xx,1.72,4.57),(.10,1.45,.16),M['wood'])
        b.box((x,1.73,4.62),(.055,1.43,.04),M['woodlight'])
    for x in (-5.7,5.7):
        b.rod((x,2.42,5.84),(x+(.40 if x<0 else -.40),2.86,5.84),.047,M['wood'],8)
    for x in (-5.3,-3.6,-1.8,1.8,3.6,5.3):b.box((x,2.84,5.65),(.11,.17,.80),M['wood'])
    b.box((0,2.99,6.10),(12.18,.22,.15),M['wood'])
    finish(b,'V8_HomeJoinery',root,.012)
    # Sparse paving joints: original ground plane and entry slope retained.
    b=geom.MeshBuilder()
    for x in (-6,-3,0,3,6):b.box((x,.023,8.95),(.014,.003,5.65),M['stone'])
    for z in (6.7,8.5,10.3,11.7):b.box((0,.023,z),(14.65,.003,.014),M['stone'])
    finish(b,'V8_HomePaving',root)

def tree_mesh(M,kind):
    rng=random.Random(800+kind);b=geom.MeshBuilder();hero=kind==3
    height=6.65 if hero else (6.6 if kind==1 else 6.0);width=2.8 if hero else (1.9 if kind==1 else 2.5)
    # Bent, tapering stem and asymmetrical branching; all roots stay at original anchors.
    stem=[(0,0,0),(.08,1.8,.02),(-.14,3.2,.04),(.16,height-.9,-.05)]
    radii=[.43 if hero else .21,.27 if hero else .15,.18 if hero else .10,.025]
    import build_village_v3 as v3
    v3.tapered(b,stem,radii,M['bark'],10)
    clusters=[]
    for k in range(9 if hero else 7):
        angle=k*2.399+kind*.6;r=width*rng.uniform(.55,.86)
        center=(math.cos(angle)*r,height-1.75+(k%3)*.56,math.sin(angle)*r)
        branch=(center[0]*.58,center[1]-.45,center[2]*.58)
        b.rod((-.04,2.8+(k%2)*.55,0),branch,.085 if hero else .06,M['bark'],7)
        b.rod(branch,center,.038,M['bark'],6)
        clusters.append((center,(width*.52,.80 if not hero else 1.04,width*.48)))
    clusters.append(((.1,height-.05,0),(width*.57,.80,width*.53)))
    # Uneven, scalloped leaf groups. Small rounded lobes break the silhouette without spikes.
    for idx,(center,scale) in enumerate(clusters):
        for sub in range(9):
            cx,cy,cz=center;sx,sy,sz=scale
            if sub:
                a=(sub-1)*math.tau/8+idx*.27
                cx+=math.cos(a)*sx*.78;cz+=math.sin(a)*sz*.78;cy+=rng.uniform(-.23,.25)
                sx*=rng.uniform(.39,.58);sz*=rng.uniform(.39,.58);sy*=rng.uniform(.42,.65)
            # Flatten the enclosing mass; layered sprays below carry the leafy edge.
            sy*=.66
            rings=7;segments=12;verts=[]
            for j in range(rings+1):
                phi=math.pi*j/rings
                for k in range(segments):
                    a=math.tau*k/segments;warp=1+.065*math.sin(k*2.3+j*1.7+idx)
                    verts.append((cx+sx*math.sin(phi)*math.cos(a)*warp,cy+sy*math.cos(phi),cz+sz*math.sin(phi)*math.sin(a)*warp))
            for j in range(rings):
                mat=M['leaflight' if (idx+sub)%5==0 else 'leafdark' if (idx+sub)%7==0 else 'leaf']
                for k in range(segments):
                    nxt=(k+1)%segments
                    b.face([verts[j*segments+k],verts[(j+1)*segments+k],verts[(j+1)*segments+nxt],verts[j*segments+nxt]],mat)
        # Closed, folded leaf fans along the perimeter and visible upper surface.
        # Broad leaves survive at play distance without alpha-card noise or new surfaces.
        cx,cy,cz=center;sx,sy,sz=scale
        for spray in range(26):
            a=spray*2.399+idx*.43;r=rng.uniform(.40,.95)
            anchor=Vector((cx+math.cos(a)*sx*r,cy+sy*.22+rng.uniform(-.08,.08),cz+math.sin(a)*sz*r))
            b.rod(tuple(Vector(center)+(anchor-Vector(center))*.55),tuple(anchor),.012,M['bark'],5)
            for leaf in range(3):
                theta=a+(leaf-1)*.63;length=rng.uniform(.28,.49);width=length*.36
                direction=Vector((math.cos(theta),rng.uniform(-.28,.18),math.sin(theta)))
                across=Vector((-math.sin(theta),0,math.cos(theta)))
                start=anchor-direction*.12;tip=anchor+direction*length
                middle=anchor+direction*(length*.42)
                left=middle+across*width;right=middle-across*width;ridge=middle+Vector((0,.075,0))
                mat=M['leaflight' if spray%4==0 else 'leaf']
                for face in ((start,left,ridge),(left,tip,ridge),(tip,right,ridge),(right,start,ridge),(start,right,tip,left)):
                    b.face([tuple(p) for p in face],mat)
    o=finish(b,'V8_TreePrototype_'+str(kind),geom.village)
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for p in o.data.polygons:p.use_smooth=True
    mesh=o.data;mesh.name='V8_Crown_'+str(kind);bpy.data.objects.remove(o,do_unlink=True);return mesh

def signature(o):
    if o.type!='MESH':return None
    h=hashlib.sha256()
    h.update(str(tuple(o.matrix_world)).encode());h.update(str([tuple(v.co) for v in o.data.vertices]).encode())
    h.update(str([(tuple(f.vertices),f.material_index) for f in o.data.polygons]).encode());h.update(str([m.name if m else None for m in o.data.materials]).encode())
    return h.hexdigest()

def save_source(path,baseline,before):
    preserved={n:s for n,s in before.items() if bpy.data.objects.get(n) and signature(bpy.data.objects[n])==s}
    bpy.context.scene['v8Preserved']=json.dumps(preserved)
    bpy.context.scene['v8BaselineSha256']=sha(baseline)
    bpy.context.scene['v8Method']='Original scripted geometry and low-frequency pigment textures; not hand sculpted'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    return preserved

def init():
    assert not SOURCE.exists() and not HOME.exists(),'Existing v8 source; edit saved source and export. No overwrite.'
    bpy.ops.wm.open_mainfile(filepath=str(BASE),load_ui=False);before={o.name:signature(o) for o in bpy.data.objects if o.type=='MESH'}
    setup(bpy.data.objects['VillageV1']);M=materials();shop_style('shop',M);shop_style('workshop',M)
    home_style(bpy.data.objects['HomeExterior'],M);setup(bpy.data.objects['VillageV1'])
    prototypes=[tree_mesh(M,k) for k in range(4)]
    for n,k in zip(SAMPLE_TREES,range(4)):bpy.data.objects[n].data=prototypes[k]
    for o in list(bpy.data.objects):
        if o.type!='MESH':continue
        if o.name.startswith('V2_') or o.name in ('VillageSquare','PlazaTreeSeat') or o.name in ('Road_home_gate','Road_home_lane','Road_workshop_path','Road_community_path'):
            remat(o,M)
    village_preserved=save_source(SOURCE,BASE,before)
    bpy.ops.wm.open_mainfile(filepath=str(HOMEBASE),load_ui=False);before={o.name:signature(o) for o in bpy.data.objects if o.type=='MESH'}
    M=materials();home_style(bpy.data.objects['Home'],M);home_preserved=save_source(HOME,HOMEBASE,before)
    record=dict(status='sample-awaiting-visual-review',visualApproval=False,runtimeDefault=False,method='Blender scripted mesh authoring',
        palette=PALETTE,buildings=['home','shop','workshop'],trees=list(SAMPLE_TREES),
        oldVillage=sha(BASE),oldHome=sha(HOMEBASE),preservedVillageMeshes=len(village_preserved),preservedHomeMeshes=len(home_preserved),
        sourceAssets=['v7 saved source','home-identity-v2 saved source','previously adopted CC0 unchanged outside sample'],
        generator='Original Python geometry; deterministic seeds 800-803; no image model',
        stages={'baseline':'complete','sample':'in-production','remainingBuildings':'planned','fullEnvironment':'planned','adoption':'pending'})
    (SOURCE.parent/'v8-provenance.json').write_text(json.dumps(record,indent=2),encoding='utf-8')

original_simplify=stream.simplify
original_colour=stream.colour
def colour(material,uv):
    c=original_colour(material,uv)
    if material and material.name.startswith('V8_'):
        bs=material.node_tree.nodes.get('Principled BSDF') if material.use_nodes else None
        if bs and bs.inputs['Base Color'].is_linked:
            return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c[:3])+(1,)
    return c
def simplify(o,lod,proxy,category):
    has_v8=any(m and m.name.startswith('V8_') for m in o.data.materials)
    if not has_v8 or category=='building':return original_simplify(o,lod,proxy,category)
    bpy.context.view_layer.objects.active=o
    if lod:
        mod=o.modifiers.new('V8 planar detail','DECIMATE');mod.decimate_type='DISSOLVE';mod.angle_limit=math.radians(.001 if category=='ground' else 2 if lod==1 else 7)
        mod.delimit={'MATERIAL'};mod.use_dissolve_boundaries=False;bpy.ops.object.modifier_apply(modifier=mod.name)
    if category=='tree' and lod>=2:
        mod=o.modifiers.new('Retain grouped crown silhouette','DECIMATE');mod.ratio=.65 if lod==2 else .42;bpy.ops.object.modifier_apply(modifier=mod.name)
    if lod>=2:
        mesh=o.data;uv=mesh.uv_layers.active;old=list(mesh.materials);layer=mesh.color_attributes.new(name='ProxyColour',type='FLOAT_COLOR',domain='CORNER')
        for f in mesh.polygons:
            mat=old[f.material_index]
            for i in f.loop_indices:layer.data[i].color=stream.colour(mat,uv.data[i].uv if uv else (0,0))
            f.material_index=0
        mesh.materials.clear();mesh.materials.append(proxy)
    used={f.material_index for f in o.data.polygons}
    for i in reversed(range(len(o.data.materials))):
        if i not in used:o.data.materials.pop(index=i)
    o.data.update()

def configure():
    stream.SOURCE=SOURCE;stream.BASE=BASE;stream.DEST=DEST;stream.simplify=simplify;stream.colour=colour

def export():
    configure();stream.build(version=8,authored_smooth=True)
    manifest_path=DEST/'manifest.json';manifest=json.loads(manifest_path.read_text())
    manifest['source']='saved v7 derivative; original v8 scripted sample geometry/pigments; existing CC0 elsewhere; no characters'
    manifest['visualApproval']=False;manifest['sampleBuildings']=['home','shop','workshop'];manifest['sampleTrees']=list(SAMPLE_TREES)
    manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    export_home()

def export_home():
    configure()
    bpy.ops.wm.open_mainfile(filepath=str(HOME),load_ui=False)
    HOMEDEST.parent.mkdir(parents=True,exist_ok=True)
    root=bpy.data.objects['Home']
    objects=[root]+[o for o in root.children_recursive if o.type in ('MESH','EMPTY')]
    for o in objects:o.hide_set(False)
    stream.export_glb(objects,HOMEDEST)
    HOMEDEST.with_suffix('.json').write_text(json.dumps(dict(sourceSha256=sha(HOME),glbSha256=sha(HOMEDEST),baselineSha256=sha(HOMEBASE),visualApproval=False),indent=2))

def check():
    configure();stream.check()
    hm=json.loads(HOMEDEST.with_suffix('.json').read_text());assert hm['sourceSha256']==sha(HOME) and hm['glbSha256']==sha(HOMEDEST)
    old_nodes=stream.glb_data(R/'assets/generated/home/3d/home-identity-v2.glb')['nodes']
    new_nodes={n['name']:n for n in stream.glb_data(HOMEDEST)['nodes'] if 'name' in n}
    for n in old_nodes:
        if n.get('name','').startswith('Door_') and 'Hinge' in n['name']:
            assert n['name'] in new_nodes,('missing door hinge',n['name'])
            for key in ('translation','rotation','scale','matrix'):assert n.get(key)==new_nodes[n['name']].get(key),(n['name'],key)
    for p in (SOURCE,HOME):
        bpy.ops.wm.open_mainfile(filepath=str(p),load_ui=False)
        preserved=json.loads(bpy.context.scene['v8Preserved'])
        for n,s in preserved.items():assert signature(bpy.data.objects[n])==s,('preserved',p,n)
    assert sha(BASE)==json.loads((SOURCE.parent/'v8-provenance.json').read_text())['oldVillage']
    assert sha(HOMEBASE)==hm['baselineSha256']
    print('V8_CHECK_PASS sourcesProtected=true interiorPreserved=true visualApproval=false',flush=True)

if __name__=='__main__':
    mode=sys.argv[sys.argv.index('--')+1]
    if mode=='init':init()
    elif mode=='export':export()
    elif mode=='export-home':export_home()
    elif mode=='check':check()
    else:raise ValueError(mode)
