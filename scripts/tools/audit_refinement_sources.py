"""Strip look-development helpers from only the new review sources, then audit them."""
import bpy,json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import author_environment_refinement as author

records={}
for kind in author.SEEDS:
    path=author.SOURCE/(kind+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path),load_ui=False)
    excluded=set()
    for collection in bpy.data.collections:
        if collection.name.startswith('Lookdev'):excluded.update(collection.all_objects)
    for obj in bpy.data.objects:
        if obj.name.startswith('Lookdev') or obj.type=='ARMATURE':
            excluded.add(obj);excluded.update(obj.children_recursive)
    removed=sorted(o.name for o in excluded)
    for obj in excluded:bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)
    assert not any(o.type=='ARMATURE' or 'Protagonist' in o.name or 'Lookdev' in o.name for o in bpy.data.objects)
    records[kind]={'removedHelpers':removed,'objectCount':len(bpy.data.objects),'armatures':0,
        'largestMeshes':sorted([(o.name,len(o.data.vertices)) for o in bpy.data.objects if o.type=='MESH'],key=lambda x:-x[1])[:8]}
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
(author.DEST/'source-audit.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
author.export();author.check()
