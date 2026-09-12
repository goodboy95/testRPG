"""Export and validate the approved full-scene cinematic environments.

Blender 5.2 usage:

    blender --background --factory-startup --python-exit-code 1 \
      --python scripts/tools/export_environment_cinematic_samples.py -- classroom-export --root .
    blender --background --factory-startup --python-exit-code 1 \
      --python scripts/tools/export_environment_cinematic_samples.py -- classroom-check --root .

The source Blend files are hand-authored successors to the preserved v4/v1
runtime assets. This tool never reconstructs them; it only exports, writes
manifests, and performs a read-only deterministic re-export check.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import tempfile

import bpy


BLENDER_VERSION = (5, 2, 0)
SOURCE_MANIFEST = Path("art/sources/cc0/polyhaven/environment-cinematic-v1/source-manifest.json")
SPECS = {
    "classroom": {
        "source": Path("art/blender/dream_classroom/classroom-art-pass-v5.blend"),
        "glb": Path("assets/generated/dream_escape/3d/classroom-art-pass-v5.glb"),
        "manifest": Path("assets/generated/dream_escape/3d/classroom-art-pass-v5-manifest.json"),
        "baseline": "assets/generated/dream_escape/3d/classroom-art-pass-v4.glb",
        "maxTriangles": 150_000,
        "maxVisibleMeshes": 200,
        "maxMaterials": 24,
        "maxBytes": 60 * 1024 * 1024,
        "textureMemoryMiB": 74.7,
        "required": ["Blackboard_Blank", "Blackboard_Revealed", "TeacherDesk_Closed", "TeacherDesk_Open",
                     "Clock_Missing", "Clock_Restored", "Roster_Missing", "Roster_Aligned",
                     "StudentDeskGroup_01", "StudentDeskGroup_18", "V5_Blackboard_Crown",
                     "FULL_Classroom_DeskHardware", "FULL_Classroom_WindowHardware",
                     "FULL_Classroom_DoorMoulding", "FULL_MapCabinet_Closed_Hardware",
                     "FULL_TeacherDesk_Closed_Lockset", "FULL_Classroom_CeilingHardware"],
    },
    "manor": {
        "source": Path("art/blender/dream_manor/dream-manor-hall-v2.blend"),
        "glb": Path("assets/generated/dream_escape/3d/dream-manor-hall-v2.glb"),
        "manifest": Path("assets/generated/dream_escape/3d/dream-manor-hall-v2-manifest.json"),
        "baseline": "assets/generated/dream_escape/3d/dream-manor-hall-v1.glb",
        "maxTriangles": 180_000,
        "maxVisibleMeshes": 220,
        "maxMaterials": 24,
        "maxBytes": 60 * 1024 * 1024,
        "textureMemoryMiB": 64.0,
        "required": ["DreamManorHall", "Door_Front_Root", "Door_Room1_Root", "Door_Room4_Root",
                     "V2_FrontDoor_LockPlate", "V2_Room1_KeyPlate", "V2_Sconce1_Backplate",
                     "FULL_Manor_DoorMoulding_01", "FULL_Manor_DoorMoulding_07",
                     "FULL_Manor_WainscotWest", "FULL_Manor_SconceBrasswork",
                     "FULL_Manor_FireplaceMoulding", "FULL_Manor_ReservedModules"],
    },
}


def args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("classroom-export", "classroom-check", "manor-export", "manor-check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def descendants(root: bpy.types.Object) -> set[bpy.types.Object]:
    result = {root}
    for child in root.children:
        result.update(descendants(child))
    return result


def export_objects(kind: str) -> list[bpy.types.Object]:
    lookdev = bpy.data.collections.get("Lookdev")
    excluded = set(lookdev.all_objects) if lookdev else set()
    if kind == "manor":
        hall = bpy.data.objects.get("DreamManorHall")
        sample_details = bpy.data.collections.get("CinematicSampleDetails")
        full_details = bpy.data.collections.get("CinematicFullDetails")
        if hall is None or sample_details is None or full_details is None:
            raise RuntimeError("Manor cinematic runtime roots are missing")
        objects = descendants(hall) | set(sample_details.all_objects) | set(full_details.all_objects)
    else:
        objects = set(bpy.data.objects)
    return sorted((obj for obj in objects if obj not in excluded and obj.type not in {"CAMERA", "LIGHT"}),
                  key=lambda obj: obj.name)


def export(kind: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    objects = export_objects(kind)
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next((obj for obj in objects if obj.type == "EMPTY"), objects[0])
    result = bpy.ops.export_scene.gltf(
        filepath=str(destination), export_format="GLB", use_selection=True, export_apply=True,
        export_animations=False, export_cameras=False, export_lights=False,
        export_materials="EXPORT", export_yup=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB export failed: {result}")


def summarize(kind: str) -> dict:
    objects = export_objects(kind)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
    materials = sorted({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})
    source_materials = [bpy.data.materials[name] for name in materials]
    images = sorted({node.image.name for material in source_materials if material.use_nodes
                     for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image})
    missing_external = []
    for item in bpy.data.images:
        if item.source == "FILE" and item.filepath and item.packed_file is None:
            resolved = Path(bpy.path.abspath(item.filepath))
            if not resolved.is_file():
                missing_external.append(str(resolved))
    maximum_visible = len(meshes)
    if kind == "classroom":
        variant_groups = (
            ("WindowCache_Closed", "WindowCache_Open"),
            ("Blackboard_Blank", "Blackboard_Revealed"),
            ("Clock_Missing", "Clock_HandOnly", "Clock_KeyOnly", "Clock_Ready", "Clock_Restored"),
            ("Roster_Missing", "Roster_Inserted", "Roster_Aligned"),
            ("TeacherDesk_Closed", "TeacherDesk_Open"),
            ("MapCabinet_Closed", "MapCabinet_Open"),
            ("TransomCache_Closed", "TransomCache_Open"),
        )
        def mesh_descendants(root_name: str) -> int:
            root = bpy.data.objects[root_name]
            return sum(1 for item in descendants(root) if item.type == "MESH")
        variant_meshes = sum(mesh_descendants(name) for group in variant_groups for name in group)
        maximum_visible = len(meshes) - variant_meshes + sum(
            max(mesh_descendants(name) for name in group) for group in variant_groups
        )
    return {
        "objectCount": len(objects),
        "meshObjectCount": len(meshes),
        "maximumVisibleMeshCount": maximum_visible,
        "uniqueMeshCount": len({obj.data.as_pointer() for obj in meshes}),
        "triangleCount": triangles,
        "materialCount": len(materials),
        "materials": materials,
        "packedOrReferencedImages": images,
        "missingExternalFiles": sorted(missing_external),
    }


def validate_source(kind: str, root: Path) -> tuple[dict, dict]:
    spec = SPECS[kind]
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    scene = bpy.context.scene
    if (scene.get("sampleOnly") is not False or scene.get("runtimeDefault") is not True or
            scene.get("fullScene") is not True or scene.get("visualDirectionApproved") is not True):
        raise RuntimeError("Approved full-scene status metadata is missing or unsafe")
    if kind == "classroom" and scene.get("studentDeskSharedMeshCount") != spec.get("deskCount", 18):
        raise RuntimeError("The classroom shared student-desk count does not match its export profile")
    names = set(bpy.data.objects.keys())
    missing = sorted(set(spec["required"]) - names)
    if missing:
        raise RuntimeError(f"Required sample objects are missing: {missing}")
    summary = summarize(kind)
    if summary["missingExternalFiles"]:
        raise RuntimeError(f"Missing external files: {summary['missingExternalFiles']}")
    limits = {
        "triangles": spec["maxTriangles"], "visibleMeshes": spec["maxVisibleMeshes"],
        "materials": spec["maxMaterials"], "glbBytes": spec["maxBytes"],
    }
    if summary["triangleCount"] > limits["triangles"] or summary["maximumVisibleMeshCount"] > limits["visibleMeshes"]:
        raise RuntimeError(f"Geometry budget exceeded: {summary}")
    if summary["materialCount"] > limits["materials"]:
        raise RuntimeError(f"Material budget exceeded: {summary['materialCount']}")
    cc0 = json.loads((root / SOURCE_MANIFEST).read_text(encoding="utf-8"))
    source_assets = [{"id": item["id"], "assetUrl": item["assetUrl"], "license": cc0["license"],
                      "maps": {name: value["sha256"] for name, value in item["maps"].items()}}
                     for item in cc0["assets"]]
    return summary, {"limits": limits, "sourceAssets": source_assets}


def texture_memory_mib(kind: str) -> float:
    materials = {slot.material for obj in export_objects(kind) if obj.type == "MESH"
                 for slot in obj.material_slots if slot.material and slot.material.use_nodes}
    images = {node.image for material in materials for node in material.node_tree.nodes
              if node.type == "TEX_IMAGE" and node.image}
    return round(sum(image.size[0] * image.size[1] * 4 * 4 / 3 for image in images) / 1024**2, 2)


def write_manifest(kind: str, root: Path, summary: dict, metadata: dict) -> dict:
    spec = SPECS[kind]
    source = root / spec["source"]
    glb = root / spec["glb"]
    result = {
        "schemaVersion": 2,
        "status": "cinematic-full-runtime",
        "sampleOnly": False,
        "runtimeDefault": True,
        "previousRuntimeAsset": spec["baseline"],
        "blenderVersion": bpy.app.version_string,
        "sourceFile": spec["source"].as_posix(),
        "runtimeGlbFile": spec["glb"].as_posix(),
        "sourceSha256": sha256(source),
        "runtimeGlbSha256": sha256(glb),
        "runtimeGlbBytes": glb.stat().st_size,
        "geometry": summary,
        "textureMemoryEstimateMiB": texture_memory_mib(kind) if spec.get("deskCount") == 12 or spec.get("actualTextureMemory") else spec["textureMemoryMiB"],
        "lightingDecision": "AO/cavity plus shadowed realtime lights and ReflectionProbe; LightmapGI remains a post-approval feasibility gate",
        "exportProfile": {"format": "GLB", "animations": False, "cameras": False, "lights": False,
                          "lookdevCollectionExcluded": True, "runtimeDependsOnBlender": False},
        **metadata,
    }
    path = root / spec["manifest"]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


def run(kind: str, mode: str, root: Path) -> None:
    spec = SPECS[kind]
    source = root / spec["source"]
    glb = root / spec["glb"]
    manifest_path = root / spec["manifest"]
    for path in (source, root / SOURCE_MANIFEST):
        if not path.is_file():
            raise RuntimeError(f"Required sample input is missing: {path}")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary, metadata = validate_source(kind, root)
    if mode == "export":
        export(kind, glb)
        if glb.stat().st_size > spec["maxBytes"]:
            raise RuntimeError(f"GLB budget exceeded: {glb.stat().st_size}")
        manifest = write_manifest(kind, root, summary, metadata)
        print(f"ENVIRONMENT_CINEMATIC_{kind.upper()}_EXPORT_PASS triangles={summary['triangleCount']} "
              f"meshes={summary['meshObjectCount']} materials={summary['materialCount']} bytes={manifest['runtimeGlbBytes']}")
        return
    for path in (glb, manifest_path):
        if not path.is_file():
            raise RuntimeError(f"Required exported sample is missing: {path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if (manifest.get("sampleOnly") is not False or manifest.get("runtimeDefault") is not True or
            manifest.get("status") != "cinematic-full-runtime"):
        raise RuntimeError("Manifest full-runtime status is unsafe")
    if manifest.get("sourceSha256") != sha256(source) or manifest.get("runtimeGlbSha256") != sha256(glb):
        raise RuntimeError("Full-runtime hashes are stale")
    with tempfile.TemporaryDirectory(prefix=f"town-rpg-{kind}-cinematic-") as temporary:
        candidate = Path(temporary) / glb.name
        export(kind, candidate)
        if sha256(candidate) != sha256(glb):
            raise RuntimeError("Fresh GLB export is not deterministic")
    print(f"ENVIRONMENT_CINEMATIC_{kind.upper()}_CHECK_PASS triangles={summary['triangleCount']} "
          f"meshes={summary['meshObjectCount']} materials={summary['materialCount']} readOnly=true")


def main() -> None:
    options = args()
    kind, mode = options.mode.split("-", 1)
    run(kind, mode, Path(options.root).resolve())


if __name__ == "__main__":
    main()
