"""Saved-source revision 3: overlap-safe neck/cuffs and material continuity."""
import bpy,bmesh,math,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
from author_shen_yan_v2 import subset,colorize,apply,smooth
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
if bpy.context.scene.get('refine_revision')!=2:raise RuntimeError('Expected revision 2')
col=bpy.data.collections['ShenYan_Artwork'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[col.name]
factor=max(v.co.z for v in bpy.data.objects['SkinVisible'].data.vertices)/1.684413194656372
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co/=factor
bpy.data.objects.remove(bpy.data.objects['SkinVisible'],do_unlink=True)
skin=subset(bpy.data.objects['AnatomyAuthoringSource'],'SkinVisible',lambda p:(p.z>1.352 and (abs(p.x)<.061 or p.z>1.45)) or (abs(p.x)>.266 and .50<p.z<1.18),subdiv=1)
for v in skin.data.vertices:v.co.x*=.91
def pigment(v):
    x,y,z=v;c=Vector((.43,.246,.160))
    lip=math.exp(-((x/.032)**6)-((z-1.479)/.008)**2) if y<-.09 else 0
    c=c.lerp(Vector((.36,.128,.104)),lip*.55)
    cheek=math.exp(-((abs(x)-.049)/.020)**2-((z-1.536)/.028)**2) if y<-.065 else 0
    c=c.lerp(Vector((.47,.222,.155)),cheek*.28)
    return (*c,1)
colorize(skin,pigment,bpy.data.materials['Shen_SkinEyes'])
skin.data.calc_loop_triangles();m=skin.modifiers.new('Preserve facial shape','DECIMATE');m.ratio=10300/len(skin.data.loop_triangles);apply(skin,m);smooth(skin)
for o in col.objects:
    if o.name=='OpenOvershirt':colorize(o,(.145,.207,.263),bpy.data.materials['Shen_Wardrobe'])
    elif o.name=='IvoryUndershirt':colorize(o,(.61,.575,.49),bpy.data.materials['Shen_Wardrobe'])
    elif o.name.startswith('CollarBand'):colorize(o,(.135,.195,.25),bpy.data.materials['Shen_Wardrobe'])
    elif o.name.startswith('CollarFold'):colorize(o,(.155,.219,.275),bpy.data.materials['Shen_Wardrobe'])
    elif o.name.startswith('RolledCuff'):colorize(o,(.18,.25,.31),bpy.data.materials['Shen_Wardrobe'])
    elif o.name.startswith('Placket'):colorize(o,(.135,.193,.248),bpy.data.materials['Shen_Wardrobe'])
    elif o.name.startswith('CanvasShoe'):
        colorize(o,lambda p:(.16,.17,.165,1) if .026<p.z<.037 else ((.63,.61,.54,1) if p.z<.044 else (.40,.42,.398,1)),bpy.data.materials['Shen_Wardrobe'])
hair=bpy.data.objects['Hair']
for v in hair.data.vertices:
    x,y,z=v.co
    if z>1.65:v.co.z=1.65+(z-1.65)*.82
    if z<1.628 and y<-.09:
        v.co.z+=.014*max(0,min(1,(-y-.09)/.06))
    if z<1.58 and y>.01:v.co.z=1.58+(z-1.58)*.30
    # Subtle irregularity in the silhouette; no repeated spherical locks.
    if z>1.64:v.co.z+=.0016*math.sin(x*183+y*139)
colorize(hair,(.010,.006,.004),bpy.data.materials['Shen_HairBrows'])
# Trim secondary surface density without reducing the facial mesh again.
for o in col.objects:
    if o.type!='MESH':continue
    o['export_static']=True
    if o.name in('OpenOvershirt','CharcoalTrousers','Hair'):
        m=o.modifiers.new('Final static density','DECIMATE');m.ratio=.88;apply(o,m)
    smooth(o)
height=max(v.co.z for o in col.objects if o.type=='MESH' for v in o.data.vertices)
for o in col.objects:
    if o.type=='MESH':
        for v in o.data.vertices:v.co*=1.73/height
bpy.context.scene['refine_revision']=3
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
total=0
for o in col.objects:
    if o.type=='MESH':o.data.calc_loop_triangles();total+=len(o.data.loop_triangles)
print('SHEN_FIT_PASS triangles='+str(total))
