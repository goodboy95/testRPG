"""One-time, CC0-based static Shen Yan authoring session.

The saved Blend is authoritative. This bootstrap refuses to overwrite it.
Subsequent edits are made to the saved mesh; export/check live in a separate tool.
The clothes are continuous surfaces derived from the anatomical topology.
This is scripted mesh authoring, not a claim of hand sculpting.
"""
import bpy
import bmesh
import math
import json
import hashlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
UPSTREAM = ROOT / 'work/character_sources/human-base-meshes-v1.4.1/expanded/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend'
ORIGINAL_ZIP = ROOT / 'work/character_sources/human-base-meshes-v1.4.1/human-base-meshes-bundle-v1.4.1.zip'
SOURCES = ROOT / 'art/sources/cc0/npc-human-base-v1'

def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    if objects: bpy.context.view_layer.objects.active = objects[0]

def smooth(obj):
    for poly in obj.data.polygons: poly.use_smooth = True

def apply(obj, mod):
    select([obj]); bpy.ops.object.modifier_apply(modifier=mod.name)

def colorize(obj, color, material):
    obj.data.materials.clear(); obj.data.materials.append(material)
    old = obj.data.color_attributes.get('Pigment')
    if old: obj.data.color_attributes.remove(old)
    attr = obj.data.color_attributes.new(name='Pigment', type='FLOAT_COLOR', domain='CORNER')
    for face in obj.data.polygons:
        for loop in face.loop_indices:
            v = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            attr.data[loop].color = color(v) if callable(color) else (*color[:3], 1)

def material(name, roughness, bump_scale, bump_distance):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    n=mat.node_tree.nodes; l=mat.node_tree.links; bs=n.get('Principled BSDF')
    c=n.new('ShaderNodeVertexColor'); c.layer_name='Pigment'; l.new(c.outputs['Color'],bs.inputs['Base Color'])
    noise=n.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=bump_scale; noise.inputs['Detail'].default_value=2
    bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.16; bump.inputs['Distance'].default_value=bump_distance
    l.new(noise.outputs['Fac'],bump.inputs['Height']); l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=roughness
    mat['authoring_method']='Vertex pigment and procedural micro-surface, baked later; no painted lighting'
    return mat

def mesh(name, vertices, faces, color, mat, subdiv=0):
    data=bpy.data.meshes.new(name); data.from_pydata(vertices,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    colorize(obj,color,mat); smooth(obj)
    if subdiv:
        mod=obj.modifiers.new('Surface continuity','SUBSURF');mod.levels=subdiv;apply(obj,mod)
    return obj

def ribbon(name, points, widths, thickness, color, mat, sides=8):
    """Tapered swept section, used for hair locks, tailoring seams and laces."""
    pts=[Vector(p) for p in points]; verts=[]; faces=[]
    for i,p in enumerate(pts):
        t=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
        outward=Vector((p.x, p.y+.035, (p.z-1.60)*.5)).normalized() if name.startswith('Lock') else Vector((0,-1,0))
        side=t.cross(outward).normalized()
        if side.length<.1: side=Vector((1,0,0))
        normal=side.cross(t).normalized()
        for j in range(sides):
            a=j*math.tau/sides
            verts.append(tuple(p+side*math.cos(a)*widths[i]+normal*math.sin(a)*thickness*min(1,widths[i]/.008)))
    for i in range(len(pts)-1):
        for j in range(sides):
            k=i*sides+j; faces.append((k,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,k+sides))
    faces.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    return mesh(name,verts,faces,color,mat,1)

def subset(source,name,predicate,offset=0,subdiv=0):
    obj=source.copy();obj.data=source.data.copy();obj.name=name;bpy.context.collection.objects.link(obj)
    for m in list(obj.modifiers):obj.modifiers.remove(m)
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if not predicate(f.calc_center_median())],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.normal_update()
    if offset:
        for v in bm.verts:v.co+=v.normal*offset
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    if subdiv:
        m=obj.modifiers.new('Tailored smooth surface','SUBSURF');m.levels=subdiv;apply(obj,m)
    smooth(obj);return obj

def skin_color(v):
    x,y,z=v
    c=Vector((.51,.29,.185))
    # Lips, natural cheek warmth and ear colour are pigment, not painted shadows.
    lips=math.exp(-((x/.032)**4+((z-1.491)/.008)**2))*max(0,min(1,(-y-.11)/.02))
    cheek=math.exp(-((abs(x)-.051)/.022)**2-((z-1.531)/.03)**2)*max(0,min(1,(-y-.07)/.04))
    c=c.lerp(Vector((.37,.115,.095)),lips*.66);c=c.lerp(Vector((.55,.25,.17)),cheek*.24)
    variation=.008*math.sin(x*260+z*79)*math.sin(z*310+y*143)
    return (*[max(0,value+variation) for value in c],1)

def cloth_color(base):
    def color(v):
        variation=.012*math.sin(v.z*103+v.x*37)*math.sin(v.y*97-v.x*131)
        return (*[max(0,c+variation) for c in base],1)
    return color

def shoe(side,mat):
    x=.202*side
    levels=[(.008,.052,.126,-.041),(.018,.061,.133,-.041),(.034,.061,.132,-.042),(.044,.058,.129,-.041),(.070,.055,.119,-.034),(.096,.045,.095,-.014),(.125,.034,.055,.022)]
    verts=[]; faces=[]; n=32
    for z,rx,ry,cy in levels:
        for j in range(n):
            a=math.tau*j/n; verts.append((x+rx*math.cos(a),cy+ry*math.sin(a),z))
    for i in range(len(levels)-1):
        for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
    faces.extend([tuple(reversed(range(n))),tuple((len(levels)-1)*n+j for j in range(n))])
    obj=mesh('CanvasShoe_'+str(side),verts,faces,lambda v:(.17,.18,.17,1) if .028<v.z<.037 else ((.65,.63,.54,1) if v.z<.044 else (.42,.44,.405,1)),mat,1)
    for i in range(5):
        y=-.071+i*.020;z=.098+i*.003
        ribbon('Lace',[ (x-.023,y-.006,z),(x,y+.003,z+.008),(x+.023,y+.010,z)],[.0018]*3,.0012,(.75,.73,.65),mat,6)
    return obj

def author():
    if SOURCE.exists():raise RuntimeError('Saved artwork exists. Edit it; never bootstrap over it.')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(UPSTREAM),link=False) as (a,b):
        b.collections=['Body Male - Realistic'];b.texts=['README']
    original=b.collections[0];bpy.context.scene.collection.children.link(original);bpy.context.view_layer.update()
    SOURCES.mkdir(parents=True,exist_ok=True)
    (SOURCES/'UPSTREAM_README.txt').write_text(bpy.data.texts['README'].as_string(),encoding='utf-8')
    source_info={'url':'https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip','packageSha256':hashlib.sha256(ORIGINAL_ZIP.read_bytes()).hexdigest(),'collection':'Body Male - Realistic','author':original.asset_data.author,'license':original.asset_data.license,'adopted':'2026-09-18','note':'Independent upstream package; no protagonist or rejected NPC meshes. Unrelated Rain Rig License text is not the asset license; bundle README and selected collection both explicitly CC0.'}
    assert source_info['license']=='CC0'
    bpy.data.libraries.write(str(SOURCES/'body-male-realistic.blend'),{original},fake_user=True)
    source_info.update({'extractedSource':(SOURCES/'body-male-realistic.blend').relative_to(ROOT).as_posix(),
        'extractedSourceSha256':hashlib.sha256((SOURCES/'body-male-realistic.blend').read_bytes()).hexdigest(),
        'readmeSha256':hashlib.sha256((SOURCES/'UPSTREAM_README.txt').read_bytes()).hexdigest(),
        'extractionTool':'Blender '+bpy.app.version_string+'; selected collection copied independently before authoring modifications'})
    (SOURCES/'provenance.json').write_text(json.dumps(source_info,indent=2),encoding='utf-8')
    body=next(o for o in original.all_objects if o.name=='GEO-body_male_realistic')
    origin=body.location.copy()
    for o in original.all_objects:o.location-=origin
    bpy.context.view_layer.update()
    for o in original.all_objects:
        select([o]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    body.name='AnatomyAuthoringSource'
    for m in list(body.modifiers):body.modifiers.remove(m)
    for v in body.data.vertices:
        x,y,z=v.co
        # Young, softer face; restrained nasal projection and jaw width.
        if z>1.43:
            jaw=math.exp(-((z-1.485)/.036)**2)
            v.co.x*=1-.055*jaw
            nose=math.exp(-(x/.023)**2-((z-1.534)/.037)**2)*max(0,min(1,(-y-.126)/.026))
            v.co.y+=.007*nose
            cheek=math.exp(-((abs(x)-.052)/.025)**2-((z-1.533)/.026)**2)
            v.co.y-=.0025*cheek
    body.data.update()
    mat_skin=material('Shen_SkinEyes',.52,195,.00032)
    mat_cloth=material('Shen_Wardrobe',.80,260,.00055)
    mat_hair=material('Shen_HairBrows',.43,170,.0003)
    model=bpy.data.collections.new('ShenYan_Artwork');bpy.context.scene.collection.children.link(model)
    # Work in a dedicated authoring collection.
    layer=bpy.context.view_layer.layer_collection.children[model.name];bpy.context.view_layer.active_layer_collection=layer
    skin=subset(body,'SkinVisible',lambda p:p.z>1.367 or (abs(p.x)>.295 and .50<p.z<1.092),subdiv=1)
    colorize(skin,skin_color,mat_skin)
    for o in list(original.all_objects):
        if o==body:continue
        for col in list(o.users_collection):col.objects.unlink(o)
        model.objects.link(o);o.name='Eye_'+('L' if o.location.x>=0 else 'R')
        colorize(o,(.72,.69,.62),mat_skin);smooth(o)
    # Eyes were transformed to world vertices. Their pupil centres follow original socket coordinates.
    for sign in (-1,1):
        cx=sign*.0329;cz=1.5737
        verts=[(cx,-.1340,cz)];faces=[];n=32
        for ring in range(1,5):
            r=.0052*ring/4
            for j in range(n):
                a=math.tau*j/n;verts.append((cx+r*math.cos(a),-.12182-math.sqrt(.0123**2-r*r),cz+r*math.sin(a)))
        for j in range(n):faces.append((0,1+j,1+(j+1)%n))
        for r in range(3):
            for j in range(n):faces.append((1+r*n+j,1+(r+1)*n+j,1+(r+1)*n+(j+1)%n,1+r*n+(j+1)%n))
        def iris(v):
            r=math.sqrt((v.x-cx)**2+(v.z-cz)**2)
            if r<.0026:return (.004,.003,.002,1)
            return (.075,.043,.019,1) if r<.0048 else (.012,.009,.006,1)
        mesh('Iris_'+str(sign),verts,faces,iris,mat_skin)
        points=[(sign*x,y,z) for x,y,z in [(.015,-.141,1.599),(.027,-.143,1.606),(.044,-.135,1.606),(.057,-.123,1.601)]]
        ribbon('Brow_'+str(sign),points,[.0018,.0038,.0033,.0008],.0007,(.025,.016,.011),mat_hair,6)
    shirt=subset(body,'OpenOvershirt',lambda p:.89<p.z<1.412 and (abs(p.x)<.235 or p.z>1.078) and not (p.y<-.035 and abs(p.x)<(.038+.043*max(0,(p.z-1.19)/.22))) and not(p.z>1.364 and abs(p.x)<.062),offset=.021,subdiv=1)
    for v in shirt.data.vertices:
        if abs(v.co.x)<.225 and v.co.z<1.19:
            v.co.x*=1.04
            v.co.y+=(-1 if v.co.y<0 else 1)*(.006+ .0025*math.sin(v.co.z*78+v.co.x*34))
    colorize(shirt,cloth_color((.17,.235,.29)),mat_cloth)
    m=shirt.modifiers.new('Woven fabric thickness','SOLIDIFY');m.thickness=.0025;apply(shirt,m)
    tee=subset(body,'IvoryUndershirt',lambda p:.925<p.z<1.371 and abs(p.x)<.205,offset=.009,subdiv=1)
    colorize(tee,cloth_color((.68,.64,.53)),mat_cloth)
    m=tee.modifiers.new('Knit thickness','SOLIDIFY');m.thickness=.0018;apply(tee,m)
    trousers=subset(body,'CharcoalTrousers',lambda p:.105<p.z<1.017 and abs(p.x)<(.29 if p.z<.78 else .235),offset=.012,subdiv=1)
    for v in trousers.data.vertices:
        x,y,z=v.co
        if z<.83:
            center=(.202-(z-.105)/.725*.095)*(1 if x>0 else -1)
            v.co.x=center+(x-center)*1.16
            v.co.y+=(-1 if y<.005 else 1)*(.008+.0035*math.sin(z*65+x*45)*math.exp(-((z-.46)/.20)**2))
    colorize(trousers,cloth_color((.044,.054,.061)),mat_cloth)
    # Folded shirt collar is a panel with an actual tip, continuous seam and thickness.
    for sign in (-1,1):
        verts=[(sign*.053,-.055,1.413),(sign*.094,-.065,1.388),(sign*.101,-.118,1.328),(sign*.054,-.137,1.356),(sign*.040,-.088,1.392)]
        o=mesh('Collar_'+str(sign),verts,[(0,1,2,3,4)],(.155,.218,.275),mat_cloth)
        m=o.modifiers.new('Collar backing','SOLIDIFY');m.thickness=.003;apply(o,m)
        m=o.modifiers.new('Collar edge roll','BEVEL');m.width=.003;m.segments=3;apply(o,m)
        # Cuff follows the arm cross-section instead of a disconnected cylinder.
        cuff=subset(body,'RolledCuff_'+str(sign),lambda p:1.075<p.z<1.108 and p.x*sign>.235,offset=.030,subdiv=1)
        colorize(cuff,cloth_color((.205,.275,.335)),mat_cloth)
        m=cuff.modifiers.new('Cuff fabric','SOLIDIFY');m.thickness=.005;apply(cuff,m)
        for i in range(5):
            z=.952+i*.071;x=sign*(.043+.028*max(0,(z-1.19)/.22));y=-.153 if z>1.18 else -.124
            if sign==-1:
                bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=(x,y,z))
                button=bpy.context.object;button.name='ShirtButton_'+str(i);button.scale=(.0035,.0016,.0035);select([button]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);colorize(button,(.41,.43,.40),mat_cloth);smooth(button)
    # Breast pocket: tailored plane, rounded by a small bevel rather than a box.
    o=mesh('BreastPocket',[(-.085,-.159,1.278),(-.147,-.139,1.274),(-.144,-.142,1.208),(-.113,-.156,1.195),(-.079,-.166,1.21)],[(0,1,2,3,4)],(.164,.231,.286),mat_cloth)
    m=o.modifiers.new('Pocket cloth','SOLIDIFY');m.thickness=.002;apply(o,m)
    for sign in(-1,1):shoe(sign,mat_cloth)
    cap=subset(body,'HairScalp',lambda p:p.z>(1.635 if p.y<-.079 else 1.584 if abs(p.x)>.054 else 1.555),offset=.007,subdiv=1)
    colorize(cap,(.019,.013,.010),mat_hair)
    # Broad swept locks from a side part; pointed ends and flattening define the silhouette.
    for i in range(13):
        f=i/12
        pts=[(.035-.006*f,.040-.17*f,1.691-.034*f),(.000-.023*f,.036-.185*f,1.716-.038*f),(-.043-.010*f,.025-.19*f,1.700-.053*f),(-.076+.015*f,.013-.185*f,1.660-.061*f),(-.083+.030*f,-.014-.157*f,1.624-.033*f)]
        ribbon('Lock_sweep_'+str(i),pts,[.010,.018,.020,.014,.0009],.006,(.021+f*.008,.015+f*.005,.011+f*.003),mat_hair)
    for i in range(10):
        f=i/9
        pts=[(.040,.048-.174*f,1.684-.028*f),(.072,.046-.180*f,1.680-.034*f),(.091,.029-.178*f,1.639-.028*f),(.091,.005-.145*f,1.589+.012*f)]
        ribbon('Lock_short_'+str(i),pts,[.009,.014,.013,.0008],.0045,(.020,.014,.010),mat_hair)
    for side in(-1,1):
        for i in range(6):
            f=i/5
            pts=[(side*(.037+.035*f),.045,1.682-.02*f),(side*(.049+.033*f),.077,1.648-.018*f),(side*(.051+.028*f),.074,1.597-.04*f),(side*(.054+.020*f),.055,1.554-.016*f)]
            ribbon('Lock_back_'+str(side)+'_'+str(i),pts,[.009,.013,.012,.0008],.004,(.019,.013,.010),mat_hair)
    # Preserve upstream anatomy separately; it is never included in the export selection.
    original.hide_render=True;original.hide_viewport=True
    for o in model.objects:
        if o.type=='MESH':
            o['export_static']=True
            # Recalculate consistently; downstream tests reject missing or invalid normals.
            bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    height=max(v.co.z for o in model.objects if o.type=='MESH' for v in o.data.vertices)
    scale=1.73/height
    for o in model.objects:
        if o.type=='MESH':
            for v in o.data.vertices:v.co*=scale
    bpy.context.scene['asset_id']='shen_yan.static_v2'
    bpy.context.scene['runtimeReady']=False
    bpy.context.scene['creation_method']='CC0 anatomical mesh edit, derived continuous garment shells, authored tapered hair sections and vertex pigment; no rejected model reuse'
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    count=0
    for o in model.objects:
        if o.type=='MESH':o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
    print('SHEN_STATIC_AUTHOR_PASS triangles='+str(count))

if __name__=='__main__':author()
