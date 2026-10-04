"""Isolated MPFB M0/M1 workflow. Run with system Python, or import functions in Blender.

Creation never overwrites a candidate. Render/check/export never save the source.
The official plugin and system pack live outside the game in ignored work/.
"""
from pathlib import Path
import argparse, datetime, hashlib, json, math, os, re, shutil, struct, subprocess, sys, time, traceback, zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/tools'))
WORK = ROOT / 'work/character-workflow'
ART = ROOT / 'art/blender/characters/mpfb-male-v1'
OUT = ROOT / 'outputs/character-workflow'
DEST = ROOT / 'assets/generated/characters/mpfb-male-v1'
VENDOR = WORK / 'vendor/mpfb2-2.0.17/src'
PACK = WORK / 'system-assets'
LOCK = {
    'mpfb': {'url': 'https://codeload.github.com/makehumancommunity/mpfb2/zip/refs/tags/v2.0.17',
             'file': 'mpfb2-2.0.17.zip', 'sha256': 'd08e726c798fdc4eefb02b06b6c4efe37d40b5439777e53cf96dce0e5073297d'},
    'assets': {'url': 'https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip',
               'file': 'system-assets-verified.zip', 'sha256': 'b542127a8e25547c7c29c19f2d1d2adb9a664c80396ecd694095dbc8028a0107'},
}
ASSETS = [('eyes','high-poly','Eyes'), ('eyebrows','eyebrow001','Eyebrows'),
          ('eyelashes','eyelashes01','Eyelashes'), ('teeth','teeth_base','Teeth'), ('tongue','tongue01','Tongue')]

CASUAL_ASSETS = [('hair','short01','Hair'), ('clothes','male_casualsuit01','Clothes'), ('clothes','shoes05','Clothes')]

def asset_role(name, kind):
    return 'clothes.'+name if kind.lower()=='clothes' else kind.lower()

def source_collection(recipe):
    return recipe.get('sourceCollection','mpfb-male-v1')

def sha(path):
    with Path(path).open('rb') as f:return hashlib.file_digest(f, 'sha256').hexdigest()

def rel(path):return Path(path).resolve().relative_to(ROOT).as_posix()

def write_json(path, value):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def require(ok, message):
    if not ok:raise RuntimeError(message)

def version_id(value):
    require(re.fullmatch(r'[a-z0-9][a-z0-9_-]{0,47}',value), 'Invalid version ID')
    return value

def source(version):return ART/'candidates'/version_id(version)/'character.blend'

def recipe_path(version):return ART/'recipes'/(version_id(version)+'.json')

def bootstrap():
    import bpy, importlib
    require(bpy.app.version[:2]==(5,2),'Expected Blender 5.2; no automatic upgrade')
    for item in LOCK.values():
        path=WORK/'downloads'/item['file']
        require(path.exists() and sha(path)==item['sha256'], 'Missing or changed download: '+str(path))
    if not VENDOR.exists():
        with zipfile.ZipFile(WORK/'downloads'/LOCK['mpfb']['file']) as z:z.extractall(WORK/'vendor')
    if not PACK.exists():
        with zipfile.ZipFile(WORK/'downloads'/LOCK['assets']['file']) as z:z.extractall(PACK)
    repo=next((r for r in bpy.context.preferences.extensions.repos if Path(r.directory)==VENDOR),None)
    if repo is None:
        bpy.ops.preferences.extension_repo_add(name='Character Workflow',type='LOCAL',use_custom_directory=True,custom_directory=str(VENDOR))
        repo=bpy.context.preferences.extensions.repos[-1]
    module='bl_ext.'+repo.module+'.mpfb'
    if module not in bpy.context.preferences.addons:bpy.ops.preferences.addon_enable(module=module)
    prefs=bpy.context.preferences.addons[module].preferences
    prefs.mpfb_second_root=str(PACK)
    def service(name):return getattr(importlib.import_module(module+'.services.'+name.lower()),name)
    service('LocationService').update_second_root()
    services={n:service(n) for n in ['HumanService','TargetService','AssetService','ObjectService','ExportService','ClothesService','LocationService']}
    services['Properties']=importlib.import_module(module+'.entities.objectproperties').HumanObjectProperties
    return services

def empty():
    import bpy
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)

def standard_recipe():
    return {'schema':1,'assetId':'npc_male_baseline','route':'A','mpfbVersion':'2.0.17',
            'macro':{'gender':1.0,'age':0.5,'muscle':0.5,'weight':0.5,'height':0.5,'proportions':0.5,
                     'cupsize':0.5,'firmness':0.5,'race':{'asian':1.0,'caucasian':0.0,'african':0.0}},
            'scale':0.1,'skin':'young_asian_male.mhmat','bodySubdivision':1,
            'assets':ASSETS,'noseVolume':0.0,'randomSeed':None,
            'note':'Native young-adult male East Asian preset, no identity fitting; age is MPFB normalized parameter, not years.'}

def create_character(recipe, services):
    import bpy
    empty()
    bpy.context.scene['workflow_recipe']=json.dumps(recipe)
    human=services['HumanService'];asset=services['AssetService']
    base=human.create_human(macro_detail_dict=recipe['macro'],scale=recipe['scale'])
    base.name='Male_Baseline';base['workflow_role']='body'
    if recipe['noseVolume']:
        target=Path(services['LocationService'].get_mpfb_data('targets'))/'nose/nose-volume-incr.target.gz'
        services['TargetService'].load_target(base,str(target),weight=recipe['noseVolume'])
    for edit in recipe.get('style',{}).get('nativeTargets',[]):
        target=Path(services['LocationService'].get_mpfb_data('targets'))/edit['path']
        require(target.is_file() and '..' not in edit['path'],'Invalid native target')
        require(sha(target)==edit['sha256'],'Native target hash changed')
        key=services['TargetService'].load_target(base,str(target),weight=0)
        key.slider_min=-1;key.value=edit['weight']
    if recipe.get('style',{}).get('pipeline')=='head_refine_v1':
        from character_head_refine import apply_correction
        apply_correction(base,recipe)
    sub=base.modifiers.new('Native smooth preview','SUBSURF');sub.levels=sub.render_levels=recipe['bodySubdivision']
    skin=asset.find_asset_absolute_path(recipe['skin'],asset_subdir='skins')
    require(skin is not None,'Missing official skin')
    human.set_character_skin(skin,base,skin_type='GAMEENGINE')
    adopted=[Path(skin)]
    for folder,name,kind in recipe['assets']:
        path=asset.find_asset_absolute_path(name+'.mhclo',asset_subdir=folder)
        require(path is not None,'Missing official '+name)
        obj=human.add_mhclo_asset(path,base,asset_type=kind,material_type='GAMEENGINE',subdiv_levels=recipe.get('hairSubdivision',1) if kind=='Hair' else 1)
        # add_mhclo_asset returns the asset object in MPFB 2.0.17.
        obj['workflow_role']=asset_role(name,kind);adopted.append(Path(path))
        if kind=='Hair' and recipe.get('hairColorFactor'):
            for mat in obj.data.materials:
                nodes=mat.node_tree.nodes;links=mat.node_tree.links
                bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
                color=bsdf.inputs['Base Color']
                if color.is_linked:
                    previous=color.links[0].from_socket
                    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY'
                    mix.inputs[0].default_value=1;mix.inputs[2].default_value=tuple(recipe['hairColorFactor'])+(1,)
                    links.new(previous,mix.inputs[1]);links.new(mix.outputs[0],color)
        if name=='shoes05' and recipe.get('hideUpperSocks'):
            group=obj.vertex_groups.new(name='Covered_upper_socks')
            indices=[v.index for v in obj.data.vertices if (obj.matrix_world@v.co).z>.075]
            if indices:group.add(indices,1,'REPLACE')
            mask=obj.modifiers.new('Hide socks beneath jeans','MASK')
            mask.vertex_group=group.name;mask.invert_vertex_group=True
    if recipe.get('style'):
        if recipe['style'].get('pipeline')=='head_refine_v1':
            from character_head_refine import finish_character
            finish_character(base,recipe)
        else:
            from character_stylized import apply_materials,create_hair
            apply_materials(base,recipe);create_hair(recipe)
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':
            for p in obj.data.polygons:p.use_smooth=True
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.view_layer.update()
    return base,adopted

def geometry():
    import bpy,numpy as np
    result={}
    graph=bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type not in ('MESH','CURVE') or 'workflow_role' not in obj:continue
        ev=obj.evaluated_get(graph);mesh=ev.to_mesh();mesh.calc_loop_triangles()
        if obj.type=='CURVE':
            from character_head_refine import curve_uv
            curve_uv(mesh)
        vertices=np.array([tuple(obj.matrix_world@v.co) for v in mesh.vertices],dtype=float)
        indices=np.array([tuple(t.vertices) for t in mesh.loop_triangles],dtype=np.int32)
        require(len(vertices)>0 and np.isfinite(vertices).all(),'Invalid mesh '+obj.name)
        require(len(mesh.uv_layers)>0,'Missing UV '+obj.name)
        require(obj['workflow_role'] not in result,'Duplicate part role '+obj['workflow_role'])
        result[obj['workflow_role']]={'vertices':vertices,'indices':indices,'polygons':tuple(tuple(p.vertices) for p in mesh.polygons),'surfaces':len(mesh.materials),
                                     'bounds':[vertices.min(axis=0).tolist(),vertices.max(axis=0).tolist()]}
        ev.to_mesh_clear()
    recipe=json.loads(bpy.context.scene['workflow_recipe'])
    expected={'body'}|{asset_role(name,kind) for folder,name,kind in recipe['assets']}
    if recipe.get('style') and recipe['style'].get('hair',{}).get('enabled',True):
        expected.update(recipe['style']['hair'].get('roles',['hair']))
    require(set(result)==expected,'Missing/duplicate body parts')
    return result

def summary(g):
    return {k:{'vertices':len(v['vertices']),'triangles':len(v['indices']),'surfaces':v['surfaces'],'bounds':v['bounds']} for k,v in g.items()}

def compare(a,b):
    import numpy as np
    require(set(a)==set(b),'Part mismatch')
    delta=0.0
    for key in a:
        # Blender may change a quad's tessellation diagonal when coordinates change.
        # Polygon connectivity is the authoritative topology, not that transient diagonal.
        require(a[key]['polygons']==b[key]['polygons'],'Topology mismatch '+key)
        require(a[key]['vertices'].shape==b[key]['vertices'].shape,'Vertex count mismatch '+key)
        delta=max(delta,float(np.abs(a[key]['vertices']-b[key]['vertices']).max()))
    return delta

def adopt_sources(paths, collection='mpfb-male-v1'):
    destination=ROOT/'art/sources/cc0'/version_id(collection);entries=[]
    for path in paths:
        require('CC0' in path.read_text(encoding='utf-8'),'No explicit CC0 in '+str(path))
        directory=path.parent;relative=directory.relative_to(PACK)
        for original in sorted(directory.rglob('*')):
            if not original.is_file():continue
            target=destination/'parts'/relative/original.relative_to(directory)
            if target.exists():require(sha(target)==sha(original),'Adopted source changed')
            else:
                target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,target)
        entries.append({'asset':str(path.relative_to(PACK)).replace('\\','/'),'license':'CC0-1.0',
                        'metadata':rel(destination/'parts'/path.relative_to(PACK)), 'metadataSha256':sha(path),
                        'files':[{'path':rel(p),'sha256':sha(p)} for p in sorted((destination/'parts'/relative).rglob('*')) if p.is_file()]})
    info={'date':'2026-09-20','tool':'MPFB 2.0.17 (GPL-3.0-or-later code; not shipped in game)',
          'coreAssetsLicense':'CC0-1.0','author':'MakeHuman Community; individual copyright notices preserved in metadata',
          'licenseUrl':'https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html',
          'downloads':LOCK,'adopted':entries,'restrictedSourcesUsed':False}
    path=destination/'provenance.json'
    if path.exists():require(json.loads(path.read_text(encoding='utf-8'))==info,'Provenance changed')
    else:write_json(path,info)
    return rel(path)

def audit_source_dependencies(check_only=False, collection='mpfb-male-v1'):
    """Preserve transitive material dependencies as well as top-level MHCLO files."""
    root=ROOT/'art/sources/cc0'/version_id(collection)
    provenance=json.loads((root/'provenance.json').read_text(encoding='utf-8'))
    pending=[PACK/item['asset'] for item in provenance['adopted']];seen=set();files=[]
    while pending:
        path=pending.pop().resolve()
        if path in seen:continue
        seen.add(path);relative=path.relative_to(PACK.resolve())
        require(path.is_file(),'Missing asset dependency '+str(relative))
        dest=root/'parts'/relative
        if dest.exists():require(sha(dest)==sha(path),'Changed source dependency')
        else:
            require(not check_only,'Unadopted dependency '+str(relative))
            dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
        files.append({'path':rel(dest),'sha256':sha(dest)})
        if path.suffix not in ('.mhclo','.mhmat'):continue
        content=path.read_text(encoding='utf-8')
        require('CC0' in content,'No explicit material/asset license '+str(relative))
        for line in content.splitlines():
            words=line.strip().split(maxsplit=1)
            if len(words)!=2:continue
            key,value=words
            if key in ('obj_file','material','diffuseTexture','normalmapTexture','bumpTexture','bumpmapTexture','specularmapTexture','displacementmapTexture','transparencymapTexture'):
                pending.append(path.parent/value)
    upstream=VENDOR.parent
    for name in ('LICENSE.ASSETS.md','LICENSE.CODE.md','LICENSE.md'):
        original=upstream/name;dest=root/'licenses'/name
        if dest.exists():require(sha(dest)==sha(original),'Changed license copy')
        else:
            require(not check_only,'Missing license copy '+name)
            dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,dest)
        files.append({'path':rel(dest),'sha256':sha(dest)})
    report={'files':sorted(files,key=lambda x:x['path']),'ignored':'Legacy GLSL shader/litsphere hints are not used by GAMEENGINE materials.'}
    dest=root/'dependencies.json'
    if dest.exists():require(json.loads(dest.read_text(encoding='utf-8'))==report,'Dependency inventory changed')
    else:
        require(not check_only,'Missing dependency inventory')
        write_json(dest,report)
    return report

def baseline(args, services):
    import bpy
    target=source(args.version)
    require(not target.exists() and not recipe_path(args.version).exists(),'Candidate already exists; choose a new version')
    recipe=standard_recipe()
    if args.parent:
        parent=source(args.parent);require(parent.exists(),'Parent missing')
        recipe=json.loads(recipe_path(args.parent).read_text(encoding='utf-8'))
        recipe['parent']={'version':args.parent,'sourceSha256':sha(parent)}
    else:recipe['parent']=None
    recipe['version']=args.version;recipe['noseVolume']=args.nose_volume
    if args.casual:
        require(args.parent is not None,'Clothed candidate must reference a parent baseline')
        recipe['assets']=ASSETS+[(folder,args.hair if kind=='Hair' else name,kind) for folder,name,kind in CASUAL_ASSETS]
        recipe['hairColorFactor']=[.23,.25,.29]
        recipe['hairSubdivision']=0
        recipe['hideUpperSocks']=True
        recipe['sourceCollection']='mpfb-male-casual-'+args.hair
        recipe['appearance']='Official '+args.hair+' hair, navy shirt/jeans male_casualsuit01, white shoes05; masked upper socks; dark hair tint; no face edits'
    if args.style:
        require(args.parent is not None,'Style candidate requires a parent')
        style_path=ROOT/args.style
        recipe['style']=json.loads(style_path.read_text(encoding='utf-8'))
        recipe['styleSource']={'path':rel(style_path),'sha256':sha(style_path)}
        for edit in recipe['style']['nativeTargets']:
            native_path=Path(services['LocationService'].get_mpfb_data('targets'))/edit['path']
            edit['sha256']=sha(native_path)
        recipe['assets']=[entry for entry in recipe['assets'] if entry[2]!='Hair']
        recipe['sourceCollection']='mpfb-male-stylized-v2'
        recipe['appearance']='Natural proportion animation style; original swept opaque hair; derived skin and cloth materials'
        recipe['authoredHair']={'tool':'Blender Python editable mesh and Bezier guides','script':'scripts/tools/character_stylized.py','restrictedSourcesUsed':False}
        if recipe['style'].get('pipeline')=='head_refine_v1':
            recipe['appearance']='Head refinement '+recipe['style']['phase']+'; original MPFB body and casual clothes; reversible lip correction and locally derived official skin'
            recipe['authoredHair']={'tool':'Blender native bevel curves and editable coverage mesh','script':'scripts/tools/character_head_refine.py','enabled':recipe['style']['hair']['enabled'],'restrictedSourcesUsed':False}
    base,paths=create_character(recipe,services)
    provenance=adopt_sources(paths,source_collection(recipe))
    audit_source_dependencies(collection=source_collection(recipe))
    target.parent.mkdir(parents=True,exist_ok=True)
    bpy.context.scene['workflow_recipe']=json.dumps(recipe)
    bpy.context.scene['runtimeReady']=False
    bpy.ops.file.pack_all()
    require(target.resolve()==source(args.version).resolve() and target.suffix=='.blend','Unsafe candidate save destination')
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    write_json(recipe_path(args.version),recipe)
    report={'source':rel(target),'sourceSha256':sha(target),'recipe':recipe,'geometry':summary(geometry()),
            'provenance':provenance,'G1':'pending agent review and user confirmation','G2_G5':'not tested','runtimeReady':False}
    write_json(ART/'reports'/(args.version+'-creation.json'),report)
    return report

def open_candidate(args):
    import bpy
    path=source(args.version);require(path.exists(),'Candidate missing')
    before=sha(path);bpy.ops.wm.open_mainfile(filepath=str(path))
    recipe=json.loads(recipe_path(args.version).read_text(encoding='utf-8'))
    require(json.loads(bpy.context.scene['workflow_recipe'])==recipe,'Source recipe mismatch')
    return path,before,recipe

def check(args, services):
    import bpy
    path,before,recipe=open_candidate(args)
    dependencies=audit_source_dependencies(check_only=True,collection=source_collection(recipe))
    if recipe.get('styleSource'):
        require(sha(ROOT/recipe['styleSource']['path'])==recipe['styleSource']['sha256'],'Changed style design')
    original=geometry();create_character(recipe,services)
    reproduced=geometry();delta=compare(original,reproduced)
    require(delta<1e-6,'Recipe reproduction differs')
    # Mutate and restore a real target on the reopened source; refit its attached parts.
    bpy.ops.wm.open_mainfile(filepath=str(path));base=next(o for o in bpy.data.objects if o.get('workflow_role')=='body')
    target=Path(services['LocationService'].get_mpfb_data('targets'))/'nose/nose-volume-incr.target.gz'
    key_name=services['TargetService'].filename_to_shapekey_name(str(target))
    key=base.data.shape_keys.key_blocks.get(key_name)
    if key is None:key=services['TargetService'].load_target(base,str(target),weight=0)
    key.value=0 if recipe['noseVolume'] else .15
    for child in base.children:
        if child.type=='MESH':services['ClothesService'].fit_clothes_to_human(child,base)
    bpy.context.view_layer.update();changed=geometry();change=compare(original,changed)
    require(change>1e-5,'Parameter had no observable geometry effect')
    key.value=recipe['noseVolume']
    for child in base.children:
        if child.type=='MESH':services['ClothesService'].fit_clothes_to_human(child,base)
    bpy.context.view_layer.update();restored=compare(original,geometry())
    require(restored<1e-6,'Target reset did not restore baseline')
    correction_check=None
    if recipe.get('style',{}).get('pipeline')=='head_refine_v1':
        key=base.data.shape_keys.key_blocks['Neutral_Lip_Closure']
        key.value=0;bpy.context.view_layer.update();disabled=geometry()
        correction_delta=compare(original,disabled)
        require(correction_delta>1e-5,'Corrective switch had no effect')
        key.value=recipe['style']['corrective']['weight'];bpy.context.view_layer.update()
        correction_restore=compare(original,geometry())
        require(correction_restore<1e-6,'Corrective did not restore exactly')
        correction_check={'offDeltaMeters':correction_delta,'restoreDeltaMeters':correction_restore}
    curve_check=None
    curves=[o for o in bpy.context.scene.objects if o.type=='CURVE' and o.get('workflow_role','').startswith('hair')]
    if curves:
        point=curves[0].data.splines[0].bezier_points[1];position=point.co.copy()
        point.co.x+=.002;bpy.context.view_layer.update();curve_delta=compare(original,geometry())
        require(curve_delta>.0001,'Editing curve does not drive mesh')
        point.co=position;bpy.context.view_layer.update();curve_restore=compare(original,geometry())
        require(curve_restore<1e-6,'Curve restore differs')
        curve_check={'editDeltaMeters':curve_delta,'restoreDeltaMeters':curve_restore}
    require(sha(path)==before,'Check changed source')
    report={'sourceSha256':before,'reproductionMaxDeltaMeters':delta,'mutationMaxDeltaMeters':change,
            'restorationMaxDeltaMeters':restored,'toleranceMeters':1e-6,'sourceUnchanged':True,
            'dependencyFilesChecked':len(dependencies['files']),'parts':summary(original),'correctiveSwitch':correction_check,'curveEdit':curve_check}
    write_json(OUT/args.version/'check.json',report);return report

def stage_export(services):
    import bpy
    base=next(o for o in bpy.data.objects if o.get('workflow_role')=='body')
    services['ExportService'].bake_modifiers_remove_helpers(base,bake_masks=True,bake_subdiv=True,remove_helpers=True)
    # This process is already a temporary in-memory copy of the saved source.
    # Evaluate curves before removing their bevel-profile helper objects.
    for obj in list(bpy.context.scene.objects):
        if obj.type=='CURVE' and 'workflow_role' in obj:
            graph=bpy.context.evaluated_depsgraph_get()
            mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
            from character_head_refine import curve_uv
            curve_uv(mesh)
            replacement=bpy.data.objects.new(obj.name+'_export',mesh);bpy.context.collection.objects.link(replacement)
            replacement.matrix_world=obj.matrix_world;replacement['workflow_role']=obj['workflow_role']
            bpy.data.objects.remove(obj,do_unlink=True)
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or 'workflow_role' not in obj:bpy.data.objects.remove(obj,do_unlink=True);continue
        ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh=bpy.data.meshes.new_from_object(ev,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
        obj.modifiers.clear();obj.data=mesh
        obj.select_set(True)
    for mat in bpy.data.materials:
        if mat.use_nodes:
            bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
            if bsdf and 'Emission Strength' in bsdf.inputs:bsdf.inputs['Emission Strength'].default_value=0
    recipe=json.loads(bpy.context.scene['workflow_recipe'])
    if recipe.get('style') and recipe['style'].get('pipeline')!='head_refine_v1':
        # Compatibility does not implement linear vertex-color conversion like EEVEE.
        # Bake only the broad albedo zones to the original UV in the export copy.
        mat=base.data.materials[0];nodes=mat.node_tree.nodes;links=mat.node_tree.links
        shader=nodes.get('Principled BSDF');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        color=shader.inputs['Base Color'].links[0].from_socket
        emit=nodes.new('ShaderNodeEmission');links.new(color,emit.inputs['Color']);links.new(emit.outputs[0],output.inputs['Surface'])
        image=bpy.data.images.new('Stylized_Skin_Albedo_2K',width=2048,height=2048,alpha=False)
        texture=nodes.new('ShaderNodeTexImage');texture.image=image;nodes.active=texture
        bpy.ops.object.select_all(action='DESELECT');base.select_set(True);bpy.context.view_layer.objects.active=base
        scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1
        bpy.ops.object.bake(type='EMIT',margin=8)
        links.new(shader.outputs[0],output.inputs['Surface']);links.new(texture.outputs['Color'],shader.inputs['Base Color'])
        nodes.remove(emit);image.pack()
        for attr in list(base.data.color_attributes):base.data.color_attributes.remove(attr)
        for obj in bpy.context.scene.objects:obj.select_set(obj.type=='MESH')

def correct_gltf_alpha(glb, recipe):
    """MPFB marks every GameEngine material BLEND. Correct only export metadata.

    Opaque skin/inner mouth write depth. Keep native coverage alpha for cards.
    MASK trials destroyed the soft eyebrow coverage; retain BLEND for this G1 asset.
    The official high-poly eyes also contain a transparent outer layer.
    Geometry, textures and authoritative Blender materials are unchanged.
    """
    raw=glb.read_bytes();length,kind=struct.unpack_from('<II',raw,12)
    require(kind==0x4E4F534A,'Missing GLB JSON chunk')
    data=json.loads(raw[20:20+length])
    material_roles={}
    for node in data.get('nodes',[]):
        role=node.get('extras',{}).get('workflow_role')
        if role and 'mesh' in node:
            for primitive in data['meshes'][node['mesh']]['primitives']:material_roles[primitive['material']]=role
    for index,mat in enumerate(data['materials']):
        role=material_roles.get(index,'')
        card=role in ('eyebrows','eyelashes');eye=role=='eyes';hair=role=='hair'
        mat['alphaMode']='BLEND' if card or eye else 'OPAQUE'
        mat.pop('alphaCutoff',None)
        if hair and not recipe.get('style'):
            mat['alphaMode']='MASK';mat['alphaCutoff']=.15
            if recipe.get('hairColorFactor'):mat['pbrMetallicRoughness']['baseColorFactor']=recipe['hairColorFactor']+[1]
    encoded=json.dumps(data,separators=(',',':')).encode('utf-8')
    encoded+=b' '*((-len(encoded))%4)
    tail=raw[20+length:]
    glb.write_bytes(struct.pack('<III',0x46546C67,2,20+len(encoded)+len(tail))+struct.pack('<II',len(encoded),kind)+encoded+tail)

def export(args,services):
    import bpy
    path,before,recipe=open_candidate(args)
    dest=DEST/(args.export_id or args.version);require(not dest.exists(),'Export version already exists')
    stage_export(services);stats=summary(geometry())
    dest.mkdir(parents=True);glb=dest/'character.glb'
    bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,
        export_animations=False,export_skins=False,export_morph=False,export_cameras=False,export_lights=False,export_tangents=True,export_extras=True)
    correct_gltf_alpha(glb,recipe)
    raw=glb.read_bytes();length,kind=struct.unpack_from('<II',raw,12);data=json.loads(raw[20:20+length])
    require(kind==0x4E4F534A,'Invalid GLB')
    triangles=sum(data['accessors'][p['indices']]['count']//3 for m in data['meshes'] for p in m['primitives'])
    require(triangles==sum(p['triangles'] for p in stats.values()),'GLB triangle mismatch')
    require(not data.get('skins') and not data.get('animations'),'Unexpected dynamic export')
    require(all('TEXCOORD_0' in p['attributes'] and 'NORMAL' in p['attributes'] for m in data['meshes'] for p in m['primitives']),'Missing UV/normals')
    empty();bpy.ops.import_scene.gltf(filepath=str(glb))
    require(len([o for o in bpy.context.scene.objects if o.type=='MESH'])==len(data['meshes']),'Reimport mismatch')
    require(sha(path)==before,'Export changed source')
    report={'schema':1,'assetId':'npc_male_baseline.'+args.version,'source':rel(path),'sourceSha256':before,
        'glb':rel(glb),'glbSha256':sha(glb),'triangles':triangles,'meshInstances':len(data['meshes']),
        'surfaces':sum(len(m['primitives']) for m in data['meshes']),'embeddedImages':len(data.get('images',[])),
        'meshRoles':{data['meshes'][n['mesh']]['name']:n.get('extras',{}).get('workflow_role','') for n in data['nodes'] if 'mesh' in n},
        'reviewBodyHeight':1.6273698806762695,'skins':0,'animations':0,'reimported':True,'runtimeReady':False,'visualApproval':False,
        'geometry':stats,'recipe':recipe,'materialConversion':'MPFB GAMEENGINE Principled, static export copy; emission disabled',
        'exportVersion':args.export_id or args.version,'alphaPolicy':'Role-based: opaque body/clothing/authored hair; BLEND eyes/brows/lashes; MASK .15 official hair cards. Static preview only.',
        'provenance':'art/sources/cc0/'+source_collection(recipe)+'/provenance.json'}
    write_json(dest/'manifest.json',report);return report

def render(args,services):
    import bpy
    from mathutils import Vector
    path,before,recipe=open_candidate(args)
    body_height=geometry()['body']['bounds'][1][2]
    source_roles={o.name:o.get('workflow_role') for o in bpy.context.scene.objects if o.type in ('MESH','CURVE')}
    if args.glb:
        empty();bpy.ops.import_scene.gltf(filepath=str(DEST/(args.export_id or args.version)/'character.glb'))
        for o in bpy.context.scene.objects:
            if o.type=='MESH' and 'workflow_role' not in o:
                require(o.name in source_roles,'Unknown legacy mesh role')
                o['workflow_role']=source_roles[o.name]
    else:stage_export(services)
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
    scene.world=bpy.data.worlds.new('NeutralReview');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.16,.16,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
    def look(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
    for pos,power,size in [((3,-4,4),380,4),((-3,-2,3),220,4),((0,4,3),220,4)]:
        bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;look(o,(0,0,1.4))
    objs=[o for o in scene.objects if o.type=='MESH']
    points=[o.matrix_world@Vector(v) for o in objs for v in o.bound_box]
    height=1.6273698806762695;head=height-.125
    bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
    views=[('head-front',0,head,.42),('head-left45',-45,head,.42),('head-right45',45,head,.42),
           ('head-left90',-90,head,.42),('head-right90',90,head,.42),
           ('body-front',0,height/2,height*1.15),('body-side',90,height/2,height*1.15),('body-back',180,height/2,height*1.15)]
    if recipe.get('style',{}).get('reviewRearViews'):
        views += [('head-back',180,head,.42),('head-ear-left',-135,head,.42),('head-ear-right',135,head,.42)]
    if args.quick:views=[views[0],views[2],views[4]]
    output=OUT/args.version/('blender-glb' if args.glb else 'blender')/datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    output.mkdir(parents=True)
    gray=bpy.data.materials.new('StructureClay');gray.use_nodes=True
    p=gray.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(.45,.45,.45,1);p.inputs['Roughness'].default_value=.8
    samples=[]
    for mode in (['material','gray','gray-hair'] if recipe.get('style') else ['material','gray']):
        for o in objs:
            o.hide_render=mode!='material' and (o.get('workflow_role') in ('eyebrows','eyelashes') or (mode=='gray' and o.get('workflow_role','').startswith('hair')))
            if mode!='material':
                for i in range(len(o.data.materials)):o.data.materials[i]=gray
        for name,angle,z,scale in views:
            r=math.radians(angle);cam.location=(3*math.sin(r),-3*math.cos(r),z)
            look(cam,(0,0,z));cam.data.ortho_scale=scale
            scene.render.filepath=str(output/(mode+'-'+name+'.png'));bpy.ops.render.render(write_still=True)
            samples.append({'image':rel(scene.render.filepath),'mode':mode,'view':name,'angle':angle,'centerZ':z,'orthoScale':scale})
    require(sha(path)==before,'Render changed source')
    report={'sourceSha256':before,'glbSha256':sha(DEST/(args.export_id or args.version)/'character.glb') if args.glb else None,
            'views':samples,'visualApproval':False,'grayPolicy':'No texture/normal details; eyebrows, eyelashes and hair cards hidden to avoid opaque card artifacts'}
    write_json(output/'results.json',report);return {'output':str(output),'images':len(samples)}

def doctor(args,services):
    import bpy
    base,_=create_character(standard_recipe(),services)
    try:gpu=subprocess.check_output(['nvidia-smi','--query-gpu=name,memory.total,driver_version','--format=csv,noheader'],text=True).strip()
    except (OSError,subprocess.CalledProcessError):gpu=None
    report={'blender':bpy.app.version_string,'mpfb':'2.0.17','downloads':LOCK,
        'addonPath':rel(VENDOR/'mpfb'),'isolatedUserResources':os.environ.get('BLENDER_USER_RESOURCES'),
        'createHuman':True,'parts':summary(geometry()),'gpu':gpu,
        'mcp':{'installed':'Blender Lab MCP 1.0.0','currentSessionCallable':False,'status':'not passed: tool unavailable in current session'},
        'execution':'Blender Python batch','compatibility':'creation and official asset attachment tested; later stages separate',
        'userActiveMinutes':None,'paidCost':0}
    write_json(OUT/'environment.json',report);return report

def parse():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['doctor','baseline','render','check','export'])
    parser.add_argument('--version',default='v001',type=version_id)
    parser.add_argument('--parent',type=version_id)
    parser.add_argument('--export-id',type=version_id,help='New immutable export revision, independent of source version')
    parser.add_argument('--nose-volume',type=float,default=0.0)
    parser.add_argument('--hair',choices=['short01','short02','short04'],default='short02')
    parser.add_argument('--style',help='Project-relative immutable style design JSON')
    parser.add_argument('--casual',action='store_true',help='Derive a clothed static candidate using locked official CC0 assets')
    parser.add_argument('--glb',action='store_true');parser.add_argument('--quick',action='store_true')
    parser.add_argument('--blender',default='D:/ProgramFiles/Blender/blender.exe')
    argv=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
    args=parser.parse_args(argv)
    require(0<=args.nose_volume<=.2,'M0/M1 probe restricted to small nose-volume changes')
    return args

def main():
    args=parse();started=time.perf_counter()
    try:import bpy
    except ImportError:
        env=os.environ.copy();env['BLENDER_USER_RESOURCES']=str(WORK/'blender-user')
        (WORK/'blender-user').mkdir(parents=True,exist_ok=True)
        logdir=OUT/'logs';logdir.mkdir(parents=True,exist_ok=True)
        logfile=logdir/(datetime.datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+args.command+'-'+args.version+'.log')
        command=[args.blender,'--background','--factory-startup','--python-exit-code','1','--python',str(Path(__file__).resolve()),'--',*sys.argv[1:]]
        with logfile.open('w',encoding='utf-8') as log:
            result=subprocess.run(command,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
        print(logfile.read_text(encoding='utf-8')[-4500:])
        write_json(logfile.with_suffix('.json'),{'exitCode':result.returncode,'seconds':time.perf_counter()-started,'log':rel(logfile),'command':sys.argv[1:]})
        return result.returncode
    try:
        services=bootstrap();result=globals()[args.command](args,services)
        print('CHARACTER_WORKFLOW_'+args.command.upper()+'_PASS '+json.dumps(result,ensure_ascii=True),flush=True)
    except Exception:
        failure={'command':args.command,'version':args.version,'traceback':traceback.format_exc()}
        write_json(OUT/'failures'/(datetime.datetime.now().strftime('%Y%m%d-%H%M%S')+'.json'),failure)
        raise
    return 0

if __name__=='__main__':sys.exit(main())
