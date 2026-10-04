"""Render saved authoring source or exported GLB; never alters either asset."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
suffix=args[0] if args else 'source'
source=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
if '--glb' in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets/generated/characters/shen-yan-v2/shen-yan-static-v2.glb'))
else:
    bpy.ops.wm.open_mainfile(filepath=str(source))
    for c in bpy.context.scene.collection.children:
        c.hide_render=c.name!='ShenYan_Artwork'; c.hide_viewport=c.name!='ShenYan_Artwork'
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
if '--ao' in args:
    # Read-only material diagnostic. The normal review always uses real PBR.
    for mat in bpy.data.materials:
        if not mat.use_nodes:continue
        tex=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and '_orm' in n.image.name),None)
        if tex:
            nodes=mat.node_tree.nodes;links=mat.node_tree.links
            sep=nodes.new('ShaderNodeSeparateColor');links.new(tex.outputs['Color'],sep.inputs[0])
            emission=nodes.new('ShaderNodeEmission');links.new(sep.outputs['Red'],emission.inputs['Color'])
            links.new(emission.outputs[0],nodes.get('Material Output').inputs['Surface'])
scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.14,.16,.19,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.view_settings.view_transform='AgX'
col=bpy.data.collections.new('ReviewOnly');scene.collection.children.link(col)
bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[col.name]
def look(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((2,-4,4),450,3),((-3,-2,2.5),260,3),((1,3,3.5),350,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=size;look(o,(0,0,1.0))
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object
mat=bpy.data.materials.new('ReviewGround');mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.11,.135,.16,1);mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.82;floor.data.materials.append(mat)
bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO'
views=[('front',(0,-5,1),(0,0,.87),2.05),('threequarter',(3,-5,1.5),(0,0,.9),2.05),('side',(5,0,1.1),(0,0,.9),2.05),('back',(0,5,1.1),(0,0,.9),2.05),('face',(0,-3,1.57),(0,-.025,1.552),.42),('face-threequarter',(.8,-2,1.61),(0,-.025,1.55),.45)]
if '--quick' in args:views=[views[0],views[4],views[2]]
out=ROOT/'outputs/shen-yan-v2'/suffix;out.mkdir(parents=True,exist_ok=True)
for name,pos,target,scale in views:
    camera.location=pos;look(camera,target);camera.data.ortho_scale=scale
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
print('SHEN_REVIEW_PASS output='+str(out)+' files='+str(len(views)))
