import sys,json,hashlib
from pathlib import Path
sys.path.insert(0,str(Path.cwd()/'scripts/tools'))
import character_workflow as w
import bpy
w.bootstrap()
rows={}
for version in ('v007-casual','v010-stylized-v2','v012-head-structure','v014-head-material','v016-head-shoulders'):
 bpy.ops.wm.open_mainfile(filepath=str(w.source(version)))
 b=next(o for o in bpy.data.objects if o.get('workflow_role')=='body')
 def digest(data):return hashlib.sha256(json.dumps(data,separators=(',',':')).encode()).hexdigest()
 rows[version]={'sourceSha256':w.sha(w.source(version)),'basisSha256':digest([list(v.co) for v in b.data.shape_keys.key_blocks[0].data]),'polygonSha256':digest([list(p.vertices) for p in b.data.polygons]),'uvSha256':digest([[list(u.uv) for u in layer.data] for layer in b.data.uv_layers]),'nativeKeyNames':[k.name for k in b.data.shape_keys.key_blocks],'hasCustomNormals':b.data.has_custom_normals,'smoothFaces':sum(p.use_smooth for p in b.data.polygons),'totalFaces':len(b.data.polygons)}
for v in ('v012-head-structure','v014-head-material','v016-head-shoulders'):
 for k in ('basisSha256','polygonSha256','uvSha256'):
  assert rows[v][k]==rows['v007-casual'][k],(v,k)
rows['baselineUnchanged']={p:w.sha(w.ART/p) for p in ('candidates/v001/character.blend','approved/v001/character.blend')}
assert len(set(rows['baselineUnchanged'].values()))==1
w.write_json(w.OUT/'head-refine/source-integrity.json',rows)
print('HEAD_SOURCE_INTEGRITY_PASS topology=true uv=true basis=true sourceHashesRecorded=true')
