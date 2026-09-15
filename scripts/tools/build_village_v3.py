"""Saved-v2 derivative: three trees and two shops. init/graybox never rebuild v1/v2.
Use export after editing the saved v3; check is a temporary read-only re-export.
"""
from pathlib import Path
import argparse,hashlib,json,math,random,struct,sys,tempfile
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_village_v1 as base
import build_village_v2 as v2
import export_environment_cinematic_samples as ex

ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path('art/blender/village/village-v3.blend')
GLB=Path('assets/generated/village/3d/village-v3.glb')
META=GLB.with_name('village-v3-manifest.json')
TEX=ROOT/'art/blender/village/v3-textures'
TREES={'PlazaTree','Tree_037','Tree_043'}
PROPS={'V2_DeliveryTable_Mesh','V2_EmptyCrates_Mesh','V2_WorkTable_Mesh','V2_TimberRack_Mesh','V2_PendingStool_Mesh','V2_ReadyStool_Mesh','V2_ToolBoard_Mesh'}
REPLACED=TREES|PROPS|{'PublicShell_shop','PublicShell_workshop','V2_ShopAwning','V2_WorkshopCanopy'}
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def fingerprint(o):
    h=hashlib.sha256(v2.fingerprint(o).encode())
    if o.type=='MESH':
        for layer in o.data.uv_layers:
            for p in layer.data:h.update(struct.pack('<ff',*p.uv))
    return h.hexdigest()
def setup():
    v2.root=ROOT;v2.setup_loaded();base.gray=False
def texture(m,path,noncolor=False):
    n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(path),check_existing=True)
    if noncolor:n.image.colorspace_settings.name='Non-Color'
    return n
def material(key,color=(1,1,1),strength=.35,alpha=False):
    m=bpy.data.materials.new('V3_'+key);m.use_nodes=True;m.diffuse_color=(*color,1)
    b=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if b is None:
        b=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(b.outputs['BSDF'],out.inputs['Surface'])
    b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=.88
    if not gray:
        prefix={'CanopyGreen':'fabric','CanopyIvory':'fabric','Leaves':'branches'}.get(key,key.lower())
        p=TEX/(('branches' if key=='Leaves' else key.lower())+'-color.png')
        if not p.exists():p=TEX/(prefix+'-color.png')
        if p.exists():
            n=texture(m,p);m.node_tree.links.new(n.outputs['Color'],b.inputs['Base Color'])
            if alpha:m.node_tree.links.new(n.outputs['Alpha'],b.inputs['Alpha'])
        p=TEX/(prefix+'-normal.png')
        if p.exists():
            n=texture(m,p,True);normal=m.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=strength
            m.node_tree.links.new(n.outputs['Color'],normal.inputs['Color']);m.node_tree.links.new(normal.outputs['Normal'],b.inputs['Normal'])
        p=TEX/(prefix+'-rough.png')
        if p.exists():m.node_tree.links.new(texture(m,p,True).outputs['Color'],b.inputs['Roughness'])
    if alpha:m.use_backface_culling=False;m.surface_render_method='DITHERED'
    return m
def adopted(key,old):
    m=bpy.data.materials[old].copy();m.name='V3_'+key
    # Preserve adopted normal/ORM maps and use their original photographic base color.
    if not gray and key=='Plaster':
        p=TEX/'plaster-color.png'
        n=texture(m,p);b=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');m.node_tree.links.new(n.outputs['Color'],b.inputs['Base Color'])
    return m
def box(b,p,s,mat):b.box(p,s,mat)
def tapered(builder,points,radii,mat,sides=10):
    rings=[];distance=0
    for i,pt in enumerate(points):
        pt=Vector(pt);axis=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])).normalized()
        helper=Vector((0,1,0)) if abs(axis.y)<.9 else Vector((1,0,0));u=axis.cross(helper).normalized();v=axis.cross(u).normalized()
        if i:distance+=(pt-Vector(points[i-1])).length
        rings.append(([pt+radii[i]*(math.cos(k*math.tau/sides)*u+math.sin(k*math.tau/sides)*v) for k in range(sides)],distance))
    for i in range(len(rings)-1):
        for k in range(sides):
            j=(k+1)%sides;builder.face([rings[i][0][k],rings[i][0][j],rings[i+1][0][j],rings[i+1][0][k]],mat,[(k/sides,rings[i][1]/1.5),((k+1)/sides,rings[i][1]/1.5),((k+1)/sides,rings[i+1][1]/1.5),(k/sides,rings[i+1][1]/1.5)])
    builder.face(list(reversed(rings[0][0])),mat);builder.face(rings[-1][0],mat)
def leaf_card(builder,center,size,rng):
    center=Vector(center);normal=Vector((rng.uniform(-1,1),rng.uniform(.25,1),rng.uniform(-1,1))).normalized()
    u=normal.cross(Vector((0,1,0))).normalized();v=normal.cross(u).normalized();a=rng.uniform(0,math.tau);u,v=u*math.cos(a)+v*math.sin(a),-u*math.sin(a)+v*math.cos(a)
    w=size*.50;h=size*.57;cell=rng.randrange(4);x=(cell%2)*.5;y=(cell//2)*.5
    # Folded twig plane: four triangles, softened silhouette and readable from below.
    pts=[center-u*w-v*h,center+u*w-v*h,center+u*w+v*h,center-u*w+v*h];mid=center+normal*size*.08
    uv=[(x+.004,y+.004),(x+.496,y+.004),(x+.496,y+.496),(x+.004,y+.496)]
    for i in range(4):builder.face([pts[i],pts[(i+1)%4],mid],mats['leaf'],[uv[i],uv[(i+1)%4],(x+.25,y+.25)])
def tree_mesh(hero):
    rng=random.Random(890 if hero else 307);b=base.MeshBuilder();crown=2.85 if hero else 2.5
    trunk=[(0,0,0),(.045,1.1,-.04),(-.09,2.35,.03),(.12,3.45,.10),(.20,4.5,-.06),(.12,5.5,.04),(0,6.5,0)]
    radii=[.45,.33,.26,.20,.14,.08,.025] if hero else [.235,.18,.145,.115,.085,.055,.018]
    tapered(b,trunk,radii,mats['bark'],12)
    if hero:
        for k in range(7):
            a=k*math.tau/7;tapered(b,[(math.cos(a)*.80,.02,math.sin(a)*.80),(math.cos(a)*.42,.18,math.sin(a)*.42),(0,.72,0)],[.04,.11,.16],mats['bark'],7)
    for k in range(12):
        a=k*2.399+rng.uniform(-.2,.2);level=3.3+(k%4)*.47;reach=crown*rng.uniform(.72,1)
        start=Vector((.08,level,0));end=Vector((math.cos(a)*reach,5.15+(k%4)*.38,math.sin(a)*reach))
        mid=start.lerp(end,.52)+Vector((math.sin(a)*.22,.20,math.cos(a)*.15))
        curved=[start,start.lerp(mid,.5)+Vector((.06,.07,0)),mid,mid.lerp(end,.55)+Vector((0,.12,0)),end]
        tapered(b,curved,[.12 if hero else .075,.086 if hero else .06,.052,.026,.009],mats['bark'],10)
        for twig in range(6):
            direction=Vector((math.cos(a+twig*.8)*.64,rng.uniform(.10,.65),math.sin(a+twig*.8)*.64))
            root=mid.lerp(end,twig/6);tip=root+direction;tapered(b,[root,tip],[.018,.003],mats['bark'],5)
            for j in range(4):
                pos=root.lerp(tip,j/3)+Vector((rng.uniform(-.28,.28),rng.uniform(-.12,.3),rng.uniform(-.28,.28)))
                leaf_card(b,pos,rng.uniform(.65,1.02),rng)
    for j in range(35):leaf_card(b,(rng.uniform(-.95,.95),rng.uniform(5.75,6.7),rng.uniform(-.95,.95)),rng.uniform(.68,1.05),rng)
    o=b.finish('V3_Prototype');mesh=o.data;bpy.data.objects.remove(o,do_unlink=True)
    for p in mesh.polygons:p.use_smooth=p.material_index==0
    mesh.update();normals=[];smooth={}
    # MeshBuilder emits independent corners; average coincident bark corners so
    # ring segments shade continuously without welding independent leaf cards.
    for poly in mesh.polygons:
        if poly.material_index==0:
            for vi in poly.vertices:
                key=tuple(round(c,5) for c in mesh.vertices[vi].co)
                smooth[key]=smooth.get(key,Vector())+poly.normal*poly.area
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            if poly.material_index==0:
                co=mesh.vertices[mesh.loops[li].vertex_index].co
                normals.append(tuple(smooth[tuple(round(c,5) for c in co)].normalized()))
            else:
                co=mesh.vertices[mesh.loops[li].vertex_index].co
                outward=Vector((co.x,co.y,(co.z-5.5)*.7+.6)).normalized()
                normals.append(tuple(outward))
    mesh.normals_split_custom_set(normals)
    return mesh
def facades():
    replacement={'Village_Plaster':mats['plaster'],'Village_Brick':mats['brick'],'Village_Stone':mats['stone'],'Village_Roof':mats['roof'],'Village_Wood':mats['wood']}
    for name in REPLACED-TREES:
        ob=bpy.data.objects[name];ob.data=ob.data.copy()
        for slot in ob.material_slots:
            if slot.material.name in replacement:slot.material=replacement[slot.material.name]
            elif name=='V2_ShopAwning' and slot.material.name in ('Village_FadedGreen','Village_SignIvory'):
                slot.material=mats['green' if slot.material.name=='Village_FadedGreen' else 'ivory']
        # Each material gets a predictable metric repeat on this isolated mesh only.
        uv=ob.data.uv_layers.active
        for poly in ob.data.polygons:
            mat=ob.data.materials[poly.material_index];scale=.5 if mat in (mats['roof'],mats['brick']) else 1
            for li in poly.loop_indices:
                if mat==mats['brick']:
                    co=ob.data.vertices[ob.data.loops[li].vertex_index].co
                    uv.data[li].uv=((-co.y if abs(poly.normal.x)>.5 else co.x)*.5,co.z*.5)
                else:uv.data[li].uv*=scale
    for key in ('shop','workshop'):
        spec=next(x for x in base.v['buildings'] if x['id']==key);w,h,d=spec['size'];b=base.MeshBuilder()
        # Thin construction within existing facade/sill/roof envelope.
        for side in (-1,1):
            for z in (-.87,.87):box(b,(side*(w/2+.04),1.4,z),(.055,1.4,.07),mats['wood'])
            for y in (.70,2.10):box(b,(side*(w/2+.04),y,0),(.055,.07,1.82),mats['wood'])
            box(b,(side*(w/2+.08),.67,0),(.15,.07,1.84),mats['stone'])
        for x in (-w/2+.10,w/2-.10):box(b,(x,h/2,d/2+.025),(.10,h,.035),mats['plaster'])
        for side in (-1,1):
            box(b,(0,h-.02,side*(d/2+.16)),(w+.28,.14,.14),mats['wood'])
            for x in (-w*.36,0,w*.36):box(b,(x,h-.16,side*(d/2+.05)),(.11,.25,.30),mats['wood'])
        # Shallow rounded ridge caps; avoids dense individually modelled roof tiles.
        for j in range(int(w/.36)):
            x=-w/2+j*.36;tapered(b,[(x,h+1.375,0),(x+.35,h+1.375,0)],[.132,.132],mats['roof'],8)
        ob=b.finish('V3_'+key+'_Joinery',bpy.data.objects['Building_'+key])
        for poly in ob.data.polygons:poly.use_smooth=False
    b=base.MeshBuilder()
    for i in range(15):
        x=-5.6+i*.8
        b.rod((x,3.116,4.5),(x,2.948,6.74),.008,mats['ivory'],5)
    b.finish('V3_ShopCanopySeams',bpy.data.objects['Building_shop'])
def glb_signatures(path):
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];data=json.loads(raw[20:20+n])
    def acc(idx):
        a=data['accessors'][idx];v=data['bufferViews'][a['bufferView']];sz={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}[a['componentType']]*{'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];s=28+n+v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',sz)
        return hashlib.sha256(b''.join(raw[s+i*stride:s+i*stride+sz] for i in range(a['count']))).hexdigest()
    result={}
    for node in data['nodes']:
        if 'mesh' in node:result[node['name']]=[(data['materials'][p['material']]['name'],acc(p['indices']),sorted((k,acc(v)) for k,v in p['attributes'].items())) for p in data['meshes'][node['mesh']]['primitives']]
    return result
def export_glb(path):
    ex.export('village',path)
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n])
    for m in d['materials']:
        if m['name']=='V3_Leaves':m.update(alphaMode='MASK',alphaCutoff=.45,doubleSided=True)
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4);tail=raw[20+n:]
    path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(j)+len(tail))+struct.pack('<II',len(j),0x4E4F534A)+j+tail)
def preserved(path):
    old=glb_signatures(ROOT/v2.GLB);new=glb_signatures(path);kept={k:v for k,v in old.items() if k not in REPLACED}
    for name,sig in kept.items():assert new.get(name)==sig,'GLB changed outside sample: '+name
    return len(kept)
def inspect(path):
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n]);nodes={x.get('name'):x for x in d['nodes']}
    assert not d.get('skins') and not d.get('animations') and not d.get('cameras')
    assert all(x in nodes for x in REPLACED)
    assert nodes['Tree_037']['mesh']==nodes['Tree_043']['mesh']!=nodes['PlazaTree']['mesh']
    assert len([x for x in nodes if x and x.startswith('Tree_')])==57
    for mesh in d['meshes']:
        for p in mesh['primitives']:
            assert 'TEXCOORD_0' in p['attributes'] and 'NORMAL' in p['attributes']
            a=d['accessors'][p['attributes']['NORMAL']];v=d['bufferViews'][a['bufferView']];offset=28+n+v.get('byteOffset',0)+a.get('byteOffset',0)
            for i in range(0,a['count'],97):
                xyz=struct.unpack_from('<fff',raw,offset+i*v.get('byteStride',12));assert all(math.isfinite(x) for x in xyz) and abs(sum(x*x for x in xyz)-1)<.004
    leaf=next(m for m in d['materials'] if m['name']=='V3_Leaves');assert leaf['alphaMode']=='MASK' and leaf['doubleSided']
    assert all('bufferView' in image for image in d['images'])
    return {'nodes':len(d['nodes']),'meshes':len(d['meshes']),'images':len(d['images']),'ordinaryTrees':57,'sampleTrees':3}
def source_checks():
    bpy.context.view_layer.update()
    old=json.loads(bpy.context.scene['v3PreservedObjects'])
    for name,sig in old.items():assert fingerprint(bpy.data.objects[name])==sig,'source changed: '+name
    assert bpy.data.objects['Tree_037'].data==bpy.data.objects['Tree_043'].data
    for name in REPLACED:
        o=bpy.data.objects[name];assert o.type=='MESH' and o.data.uv_layers
        assert all(math.isfinite(v) for uv in o.data.uv_layers.active.data for v in uv.uv)
        assert all(abs(p.normal.length-1)<.002 for p in o.data.polygons if p.area>1e-9)
    return old
def export():
    old=source_checks();summary=ex.summarize('village');memory=ex.texture_memory_mib('village')
    assert summary['triangleCount']-718571<=35000
    assert summary['triangleCount']-89856+524288+679<=1200000
    assert summary['maximumVisibleMeshCount']-1+124+2<=600 and summary['materialCount']+3<=64
    assert memory+93.34<=384 and not summary['missingExternalFiles'],memory
    export_glb(ROOT/GLB);actual=inspect(ROOT/GLB);kept=preserved(ROOT/GLB)
    assert (ROOT/GLB).stat().st_size<=100*1024**2
    metadata={'version':3,'status':'independent-art-sample','runtimeDefault':False,'visualAcceptance':'pending-human-review','source':str(SOURCE),'glb':str(GLB),'sourceSha256':sha(ROOT/SOURCE),'glbSha256':sha(ROOT/GLB),'baselineSourceSha256':sha(ROOT/v2.SOURCE),'baselineGlbSha256':sha(ROOT/v2.GLB),'geometry':summary,'actualGlb':actual,'preservedMeshCount':kept,'modifiedObjects':sorted(REPLACED),'preservedObjects':old,'textureMemoryMiB':memory,'combinedTextureMemoryMiB':round(memory+93.34,2),'combinedConservativeTriangles':summary['triangleCount']-89856+524288+679,'sourceProvenance':'art/sources/cc0/village-art-v3/provenance.json','tool':'Blender '+bpy.app.version_string,'date':'2026-09-14'}
    (ROOT/META).write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    print('VILLAGE_V3_EXPORT_PASS',summary['triangleCount'],memory,kept,flush=True)
def build():
    global mats
    if not gray and (ROOT/SOURCE).exists():raise RuntimeError('v3 exists. Use export after editing; init refuses overwrite.')
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/v2.SOURCE),load_ui=False);setup()
    before={o.name:fingerprint(o) for o in bpy.data.objects if o.name not in REPLACED}
    mats={'bark':material('Bark',(.33,.31,.25),.5),'leaf':material('Leaves',(.26,.38,.13),alpha=True),'roof':material('Roof',(.24,.28,.27)),
          'brick':material('Brick',(.46,.40,.34)),'green':material('CanopyGreen',(.28,.40,.29)),'ivory':material('CanopyIvory',(.76,.75,.65)),
          'plaster':adopted('Plaster','Village_Plaster'),'wood':adopted('Wood','Village_Wood'),'stone':adopted('Stone','Village_Stone')}
    for name,mesh in [('PlazaTree',tree_mesh(True)),('Tree_037',tree_mesh(False))]:bpy.data.objects[name].data=mesh
    bpy.data.objects['Tree_043'].data=bpy.data.objects['Tree_037'].data
    facades();bpy.context.view_layer.update();bpy.context.scene['v3PreservedObjects']=json.dumps(before,sort_keys=True);bpy.context.scene['sampleOnly']=True;bpy.context.scene['runtimeDefault']=False
    source_checks()
    dest=ROOT/('work/village-art-v3-graybox/village-v3-graybox.blend' if gray else SOURCE);dest.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(dest))
    if gray:
        summary=ex.summarize('village');(dest.parent/'shape-check.json').write_text(json.dumps(summary,indent=2));print('VILLAGE_V3_SHAPE_PASS',summary['triangleCount'],flush=True)
    else:export()
def check():
    meta=json.loads((ROOT/META).read_text(encoding='utf-8'))
    for p,key in [(SOURCE,'sourceSha256'),(GLB,'glbSha256'),(v2.SOURCE,'baselineSourceSha256'),(v2.GLB,'baselineGlbSha256')]:assert sha(ROOT/p)==meta[key],key
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/SOURCE),load_ui=False);setup();source_checks();assert ex.summarize('village')==meta['geometry'];assert inspect(ROOT/GLB)==meta['actualGlb'];preserved(ROOT/GLB)
    with tempfile.TemporaryDirectory(prefix='village-v3-check-') as d:
        out=Path(d)/'check.glb';export_glb(out);assert sha(out)==meta['glbSha256'],'non-deterministic export'
    print('VILLAGE_V3_CHECK_PASS readOnly=true',meta['preservedMeshCount'],flush=True)
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=['graybox','init','export','check']);args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);gray=args.mode=='graybox'
    if args.mode in ('graybox','init'):build()
    elif args.mode=='export':bpy.ops.wm.open_mainfile(filepath=str(ROOT/SOURCE),load_ui=False);setup();export()
    else:check()
