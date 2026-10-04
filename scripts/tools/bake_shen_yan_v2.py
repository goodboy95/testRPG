"""Bake the actual saved static artwork's pigment, procedural normal and AO/roughness.

No image projection or invented high-poly sculpt is used. Keeps separate editing modules.
"""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rebake='--rebake' in sys.argv
if bpy.context.scene.get('textures_baked') and not rebake:raise RuntimeError('Already baked; use explicit --rebake after editing source pigment/surface attributes.')
if rebake:
    for name,rough,scale,dist in [('Shen_SkinEyes',.53,195,.00022),('Shen_Wardrobe',.82,300,.00045),('Shen_HairBrows',.76,230,.00020)]:
        mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        output=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');links.new(bs.outputs[0],output.inputs[0])
        pigment=nodes.new('ShaderNodeVertexColor');pigment.layer_name='Pigment';links.new(pigment.outputs[0],bs.inputs['Base Color'])
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=scale;noise.inputs['Detail'].default_value=2
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.14;bump.inputs['Distance'].default_value=dist
        links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal']);bs.inputs['Roughness'].default_value=rough
col=bpy.data.collections['ShenYan_Artwork'];objects=[o for o in col.objects if o.type=='MESH']
out=SOURCE.parent/'textures';out.mkdir(exist_ok=True)
for img in list(bpy.data.images):
    if img.name.startswith(('shen_skin_','shen_wardrobe_','shen_hair_')):
        if img.users:raise RuntimeError('Old baked image is still referenced: '+img.name)
        bpy.data.images.remove(img)
for o in objects:
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    if o.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for f in o.data.polygons:f.use_smooth=True
    # The iris ring boundary uses topology, avoiding floating-point star-shaped pupils.
    if o.name.startswith('Iris_'):
        a=o.data.color_attributes['Pigment']
        for loop in o.data.loops:
            index=loop.vertex_index
            a.data[loop.index].color=(.003,.002,.001,1) if index<=80 else ((.08,.042,.020,1) if index<=120 else (.018,.010,.005,1))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=8;scene.render.bake.use_clear=False
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
    for d in prefs.devices:d.use=d.type=='CUDA'
    scene.cycles.device='GPU'
except Exception:scene.cycles.device='CPU'
records=[]
for name,slug in [('Shen_SkinEyes','skin'),('Shen_Wardrobe','wardrobe'),('Shen_HairBrows','hair')]:
    mat=bpy.data.materials[name];selected=[o for o in objects if mat in o.data.materials[:]]
    bpy.ops.object.select_all(action='DESELECT')
    for o in selected:o.select_set(True)
    bpy.context.view_layer.objects.active=selected[0]
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.022,margin_method='ADD',area_weight=.4,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF');output=nodes.get('Material Output')
    original=bs.outputs['BSDF'];pigment=next(n for n in nodes if n.type=='VERTEX_COLOR')
    emit=nodes.new('ShaderNodeEmission');ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.06
    comb=nodes.new('ShaderNodeCombineColor');comb.mode='RGB';comb.inputs[1].default_value=bs.inputs['Roughness'].default_value;comb.inputs[2].default_value=0
    links.new(ao.outputs['AO'],comb.inputs[0])
    rough=nodes.new('ShaderNodeVertexColor');rough.layer_name='SurfaceRoughness'
    links.new(rough.outputs['Color'],comb.inputs[1])
    baked={}
    for channel in ['albedo','normal','orm']:
        size=2048 if slug!='hair' else 1024
        img=bpy.data.images.new('shen_'+slug+'_'+channel,width=size,height=size,alpha=False)
        img.colorspace_settings.name='sRGB' if channel=='albedo' else 'Non-Color'
        img.generated_color=(.5,.5,1,1) if channel=='normal' else ((1,.7,0,1) if channel=='orm' else (0,0,0,1))
        target=nodes.new('ShaderNodeTexImage');target.image=img;nodes.active=target
        for n in nodes:n.select=False
        target.select=True
        if channel=='normal':links.new(original,output.inputs['Surface']);bake_type='NORMAL'
        else:
            links.new(pigment.outputs['Color'] if channel=='albedo' else comb.outputs[0],emit.inputs['Color'])
            links.new(emit.outputs[0],output.inputs['Surface']);bake_type='EMIT'
        bpy.ops.object.bake(type=bake_type)
        img.filepath_raw=str(out/(img.name+'.png'));img.file_format='PNG';img.save()
        img.filepath_raw=bpy.path.relpath(str(out/(img.name+'.png')))
        baked[channel]=img;print('BAKED',slug,channel,flush=True)
    # Explicit glTF-compatible PBR nodes, including ORM's AO input group.
    nodes.clear();output=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');links.new(bs.outputs[0],output.inputs[0])
    for channel in ['albedo','normal','orm']:
        tex=nodes.new('ShaderNodeTexImage');tex.image=baked[channel];tex.label=channel
        if channel=='albedo':links.new(tex.outputs['Color'],bs.inputs['Base Color'])
        elif channel=='normal':
            normal=nodes.new('ShaderNodeNormalMap');links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs[0],bs.inputs['Normal'])
        else:
            sep=nodes.new('ShaderNodeSeparateColor');sep.mode='RGB';links.new(tex.outputs[0],sep.inputs[0]);links.new(sep.outputs['Green'],bs.inputs['Roughness']);links.new(sep.outputs['Blue'],bs.inputs['Metallic'])
            group=bpy.data.node_groups.get('glTF Material Output')
            if not group:
                group=bpy.data.node_groups.new('glTF Material Output','ShaderNodeTree');group.interface.new_socket(name='Occlusion',in_out='INPUT',socket_type='NodeSocketFloat')
            settings=nodes.new('ShaderNodeGroup');settings.node_tree=group;links.new(sep.outputs['Red'],settings.inputs['Occlusion'])
    if slug=='hair':bs.inputs['Specular IOR Level'].default_value=.08
    records.append({'material':name,'resolution':size,'method':'Cycles EMIT pigment / NORMAL from geometry and procedural bump / AO+part roughness+zero metallic ORM','images':[Path(bpy.path.abspath(i.filepath_raw)).relative_to(ROOT).as_posix() for i in baked.values()]})
scene['textures_baked']=True
(SOURCE.parent/'bake-record.json').write_text(json.dumps({'tool':'Blender '+bpy.app.version_string,'date':'2026-09-18','materials':records,'highPolySculpt':False,'generatedConceptProjected':False},indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));print('SHEN_BAKE_PASS materials=3 images=9')
