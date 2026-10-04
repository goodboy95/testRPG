"""Revision 6: replace cropped skin boundaries with continuous covered anatomy."""
import bpy,math,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import subset,colorize,apply,smooth
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision')!=5:raise RuntimeError('Expected revision 5')
col=bpy.data.collections['ShenYan_Artwork'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[col.name]
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co/=factor
bpy.data.objects.remove(bpy.data.objects['SkinVisible'],do_unlink=True)
skin=subset(bpy.data.objects['AnatomyAuthoringSource'],'SkinVisible',lambda p:(p.z>1.350 and (abs(p.x)<.145 or p.z>1.45)) or (abs(p.x)>.235 and .50<p.z<1.22),subdiv=1)
for v in skin.data.vertices:
    v.co.x*=.91;x,y,z=v.co
    if z<1.445 and abs(x)<.16:
        t=max(0,min(1,(1.445-z)/.044));back=max(0,min(1,(y+.052)/.055))
        v.co.x*=1-.44*t;v.co.y=-.038+(y+.038)*(1-.70*t*back)
    if abs(x)>.20 and z>1.04:
        side=1 if x>0 else -1;axis=Vector((side*.456*.91,0,-.89)).normalized();center=Vector((side*.319*.91,-.030,1.105))
        along=(v.co-center).dot(axis);shrink=max(0,min(1,(.045-along)/.045))*.68;foot=center+axis*along;v.co=foot+(v.co-foot)*(1-shrink)
def pigment(p):
    x,y,z=p;c=Vector((.40,.232,.156))
    if y<-.085:
        c=c.lerp(Vector((.32,.12,.102)),math.exp(-((x/.030)**6)-((z-1.484)/.012)**2)*.55)
        c=c.lerp(Vector((.43,.205,.146)),math.exp(-((abs(x)-.047)/.024)**2-((z-1.535)/.03)**2)*.22)
    return (*c,1)
colorize(skin,pigment,bpy.data.materials['Shen_SkinEyes'])
skin.data.calc_loop_triangles();m=skin.modifiers.new('Static facial density','DECIMATE');m.ratio=10300/len(skin.data.loop_triangles);apply(skin,m);smooth(skin)
a=skin.data.color_attributes.new(name='SurfaceRoughness',type='FLOAT_COLOR',domain='CORNER')
for c in a.data:c.color=(.53,.53,.53,1)
skin['export_static']=True
hair=bpy.data.objects['Hair']
for v in hair.data.vertices:
    q=Vector((v.co.x/.079,(v.co.y+.044)/.099,(v.co.z-1.596)/.104))
    length=q.length
    # Keep shallow relief, remove floating open seams above the crown.
    if length>1.026:
        q*= (1.026+(length-1.026)*.13)/length
        v.co=Vector((q.x*.079,q.y*.099-.044,q.z*.104+1.596))
for color in hair.data.color_attributes['SurfaceRoughness'].data:color.color=(.76,.76,.76,1)
for name,rough,scale,dist in [('Shen_SkinEyes',.53,195,.00022),('Shen_Wardrobe',.82,300,.00045),('Shen_HairBrows',.76,230,.00020)]:
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');links.new(bs.outputs[0],output.inputs[0])
    c=nodes.new('ShaderNodeVertexColor');c.layer_name='Pigment';links.new(c.outputs[0],bs.inputs['Base Color'])
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=scale;noise.inputs['Detail'].default_value=2
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.14;bump.inputs['Distance'].default_value=dist;links.new(noise.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=rough
    if name=='Shen_HairBrows':bs.inputs['Specular IOR Level'].default_value=.08
height=max(v.co.z for o in col.objects if o.type=='MESH' for v in o.data.vertices)
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
bpy.context.scene['refine_revision']=6;bpy.context.scene['textures_baked']=False
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
print('SHEN_CLEARANCE_PASS revision=6')
