"""Revision 7: explicitly triangulated outer collar panels for reliable baking."""
import bpy,sys,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import mesh,apply
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision')!=6:raise RuntimeError('Expected saved revision 6')
col=bpy.data.collections['ShenYan_Artwork'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[col.name]
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for sign in (-1,1):
    bpy.data.objects.remove(bpy.data.objects['CollarFold_'+str(sign)],do_unlink=True)
    vertices=[(sign*x*.91*factor,y*factor,z*factor) for x,y,z in [(.045,-.061,1.438),(.095,-.070,1.407),(.105,-.131,1.347),(.055,-.144,1.371),(.044,-.084,1.414)]]
    faces=[(0,1,4),(1,2,4),(2,3,4)]
    if sign==1:faces=[tuple(reversed(f)) for f in faces]
    o=mesh('CollarFold_'+str(sign),vertices,faces,(.155,.219,.275),bpy.data.materials['Shen_Wardrobe'])
    m=o.modifiers.new('Fabric thickness','SOLIDIFY');m.thickness=.0012*factor;m.offset=-1;apply(o,m)
    o['export_static']=True
    rough=o.data.color_attributes.new(name='SurfaceRoughness',type='FLOAT_COLOR',domain='CORNER')
    for c in rough.data:c.color=(.82,.82,.82,1)
bpy.context.scene['refine_revision']=7;bpy.context.scene['textures_baked']=False
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE));print('SHEN_COLLAR_REPAIR_PASS revision=7')
