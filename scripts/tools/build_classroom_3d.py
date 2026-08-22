"""Build and validate the deterministic Blender classroom MVP.

Run through Blender, not the system Python:
  blender --background --factory-startup --python scripts/tools/build_classroom_3d.py -- build --root <repo>
  blender --background --factory-startup --python scripts/tools/build_classroom_3d.py -- check --root <repo>
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import random
import sys
import tempfile

import bpy


BLENDER_VERSION = (5, 2, 0)
ROOM_SIZE = (9.6, 7.2, 3.2)
SOURCE_RELATIVE = Path("art/blender/dream_classroom/classroom-mvp.blend")
GLB_RELATIVE = Path("assets/generated/dream_escape/3d/classroom-mvp.glb")
MANIFEST_RELATIVE = Path("assets/generated/dream_escape/3d/classroom-3d-manifest.json")
REQUIRED_OBJECTS = {
    "Room_Floor",
    "Wall_North",
    "Wall_South",
    "Wall_East",
    "Wall_West",
    "Door_East",
    "Window_West",
    "Blackboard_Blank",
    "Blackboard_Revealed",
    "Clock_Seven",
    "DutyRoster_Second",
    "TeacherDesk_Closed",
    "TeacherDesk_Open",
    "MapCabinet_Closed",
    "MapCabinet_Open",
    "Chalk",
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check", "mvp-build", "mvp-check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def material(name: str, color: tuple[float, float, float, float], roughness: float, metallic: float = 0.0):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return result


def collection(name: str, parent: bpy.types.Collection | None = None) -> bpy.types.Collection:
    result = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(result)
    return result


def empty(name: str, target: bpy.types.Collection) -> bpy.types.Object:
    result = bpy.data.objects.new(name, None)
    target.objects.link(result)
    return result


def blender_position(logical: tuple[float, float, float]) -> tuple[float, float, float]:
    """Convert Godot-style X/Y-up/Z coordinates to Blender X/Y/Z-up coordinates."""
    x, height, depth = logical
    return x, -depth, height


def blender_dimensions(logical: tuple[float, float, float]) -> tuple[float, float, float]:
    width, height, depth = logical
    return width, depth, height


def box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    target: bpy.types.Collection,
    mat: bpy.types.Material,
    bevel: float = 0.0,
    parent: bpy.types.Object | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=blender_position(location))
    result = bpy.context.object
    result.name = name
    result.dimensions = blender_dimensions(dimensions)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for owner in list(result.users_collection):
        owner.objects.unlink(result)
    target.objects.link(result)
    result.data.materials.append(mat)
    result.parent = parent
    if bevel > 0:
        modifier = result.modifiers.new("SoftEdges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.context.view_layer.objects.active = result
        result.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        result.select_set(False)
    return result


def cylinder(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    target: bpy.types.Collection,
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 24,
    parent: bpy.types.Object | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=blender_position(location),
        rotation=rotation,
    )
    result = bpy.context.object
    result.name = name
    for owner in list(result.users_collection):
        owner.objects.unlink(result)
    target.objects.link(result)
    result.data.materials.append(mat)
    result.parent = parent
    return result


def make_desk_group(
    name: str,
    x: float,
    z: float,
    target: bpy.types.Collection,
    wood: bpy.types.Material,
    metal: bpy.types.Material,
    linked_from: dict[str, bpy.types.Mesh] | None,
) -> dict[str, bpy.types.Mesh]:
    root = empty(name, target)
    parts = {
        "Desktop": ((x, 0.78, z), (1.05, 0.08, 0.58), wood),
        "DeskLegL": ((x - 0.42, 0.39, z), (0.06, 0.72, 0.48), metal),
        "DeskLegR": ((x + 0.42, 0.39, z), (0.06, 0.72, 0.48), metal),
        "ChairSeat": ((x, 0.48, z + 0.58), (0.48, 0.07, 0.43), wood),
        "ChairBack": ((x, 0.76, z + 0.78), (0.48, 0.48, 0.06), wood),
        "ChairLegL": ((x - 0.18, 0.23, z + 0.58), (0.05, 0.44, 0.36), metal),
        "ChairLegR": ((x + 0.18, 0.23, z + 0.58), (0.05, 0.44, 0.36), metal),
    }
    meshes: dict[str, bpy.types.Mesh] = {}
    for part, (position, size, mat) in parts.items():
        if linked_from and part in linked_from:
            obj = bpy.data.objects.new(f"{name}_{part}", linked_from[part])
            target.objects.link(obj)
            obj.location = blender_position(position)
            obj.parent = root
        else:
            obj = box(f"{name}_{part}", position, size, target, mat, 0.025, root)
        meshes[part] = obj.data
    return meshes


def build_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"

    mats = {
        "floor": material("M_WornFloor", (0.20, 0.14, 0.105, 1.0), 0.78),
        "wall": material("M_OldPlaster", (0.42, 0.44, 0.39, 1.0), 0.92),
        "teal": material("M_FadedTeal", (0.08, 0.24, 0.25, 1.0), 0.86),
        "wood": material("M_WornWood", (0.32, 0.19, 0.105, 1.0), 0.72),
        "wood_dark": material("M_DarkWood", (0.17, 0.095, 0.06, 1.0), 0.8),
        "metal": material("M_DarkMetal", (0.085, 0.105, 0.11, 1.0), 0.52, 0.55),
        "board": material("M_Blackboard", (0.025, 0.095, 0.09, 1.0), 0.9),
        "chalk": material("M_Chalk", (0.82, 0.81, 0.70, 1.0), 1.0),
        "paper": material("M_AgedPaper", (0.65, 0.58, 0.43, 1.0), 0.88),
        "red": material("M_FadedRed", (0.48, 0.09, 0.07, 1.0), 0.8),
        "glass": material("M_DirtyGlass", (0.13, 0.28, 0.30, 1.0), 0.3, 0.1),
        "amber": material("M_Amber", (0.75, 0.42, 0.08, 1.0), 0.48, 0.2),
    }

    shell = collection("ClassroomShell")
    furniture = collection("ClassroomFurniture")
    states = collection("ClassroomStates")
    details = collection("ClassroomDetails")

    box("Room_Floor", (0, -0.09, 0), (9.6, 0.18, 7.2), shell, mats["floor"])

    north = empty("Wall_North", shell)
    box("Wall_North_Upper", (0, 2.35, -3.60), (9.9, 1.7, 0.18), shell, mats["wall"], parent=north)
    box("Wall_North_Lower", (0, 0.75, -3.60), (9.9, 1.5, 0.18), shell, mats["teal"], parent=north)

    south = empty("Wall_South", shell)
    box("Wall_South_Upper", (0, 2.35, 3.60), (9.9, 1.7, 0.18), shell, mats["wall"], parent=south)
    box("Wall_South_Lower", (0, 0.75, 3.60), (9.9, 1.5, 0.18), shell, mats["teal"], parent=south)

    east = empty("Wall_East", shell)
    box("Wall_East_Back", (4.80, 1.6, -0.98), (0.18, 3.2, 5.24), shell, mats["teal"], parent=east)
    box("Wall_East_Front", (4.80, 1.6, 3.18), (0.18, 3.2, 0.84), shell, mats["teal"], parent=east)
    box("Wall_East_DoorTop", (4.80, 2.67, 2.20), (0.18, 1.06, 1.12), shell, mats["wall"], parent=east)
    door = empty("Door_East", details)
    box("Door_East_FrameA", (4.69, 1.1, 1.62), (0.16, 2.2, 0.12), details, mats["wood_dark"], parent=door)
    box("Door_East_FrameB", (4.69, 1.1, 2.78), (0.16, 2.2, 0.12), details, mats["wood_dark"], parent=door)
    box("Door_East_Header", (4.69, 2.18, 2.20), (0.16, 0.12, 1.28), details, mats["wood_dark"], parent=door)

    west = empty("Wall_West", shell)
    box("Wall_West_Back", (-4.80, 1.6, -2.72), (0.18, 3.2, 1.76), shell, mats["teal"], parent=west)
    box("Wall_West_Front", (-4.80, 1.6, 1.72), (0.18, 3.2, 3.76), shell, mats["teal"], parent=west)
    box("Wall_West_WindowLow", (-4.80, 0.42, -1.10), (0.18, 0.84, 1.48), shell, mats["teal"], parent=west)
    box("Wall_West_WindowHigh", (-4.80, 2.68, -1.10), (0.18, 1.04, 1.48), shell, mats["wall"], parent=west)
    window = empty("Window_West", details)
    box("Window_West_Glass", (-4.69, 1.55, -1.10), (0.03, 1.34, 1.34), details, mats["glass"], parent=window)
    box("Window_West_Vertical", (-4.66, 1.55, -1.10), (0.05, 1.40, 0.055), details, mats["wood_dark"], parent=window)
    box("Window_West_Horizontal", (-4.66, 1.55, -1.10), (0.05, 0.055, 1.40), details, mats["wood_dark"], parent=window)

    board_blank = empty("Blackboard_Blank", states)
    box("Blackboard_Blank_Surface", (0, 1.82, -3.48), (3.9, 1.35, 0.08), states, mats["board"], 0.02, board_blank)
    board_revealed = empty("Blackboard_Revealed", states)
    box("Blackboard_Revealed_Surface", (0, 1.82, -3.475), (3.9, 1.35, 0.08), states, mats["board"], 0.02, board_revealed)
    for index, x in enumerate((-0.85, -0.28, 0.28, 0.85)):
        marker = box(f"Blackboard_Chalk_{index + 1}", (x, 1.82, -3.425), (0.25, 0.035, 0.018), states, mats["chalk"], parent=board_revealed)
        marker.rotation_euler.y = (index % 2) * 0.45

    clock = empty("Clock_Seven", details)
    cylinder("Clock_Face", (2.72, 2.35, -3.44), 0.31, 0.08, details, mats["paper"], (math.pi / 2, 0, 0), parent=clock)
    box("Clock_Minute", (2.72, 2.46, -3.385), (0.028, 0.22, 0.025), details, mats["metal"], parent=clock)
    hour = box("Clock_Hour", (2.65, 2.29, -3.38), (0.025, 0.18, 0.025), details, mats["amber"], parent=clock)
    hour.rotation_euler.y = 0.78

    roster = empty("DutyRoster_Second", details)
    box("DutyRoster_Paper", (3.66, 1.75, -3.47), (0.72, 1.02, 0.045), details, mats["paper"], parent=roster)
    for row in range(4):
        box(f"DutyRoster_Line_{row + 1}", (3.66, 2.02 - row * 0.18, -3.435), (0.48, 0.018, 0.012), details, mats["wood_dark"], parent=roster)
    cylinder("DutyRoster_Circle", (3.42, 1.84, -3.40), 0.075, 0.016, details, mats["red"], (math.pi / 2, 0, 0), 20, roster)

    teacher_closed = empty("TeacherDesk_Closed", states)
    box("TeacherDesk_Closed_Top", (0.4, 0.78, -2.18), (1.85, 0.12, 0.82), states, mats["wood"], 0.035, teacher_closed)
    box("TeacherDesk_Closed_BaseL", (-0.33, 0.38, -2.18), (0.30, 0.70, 0.72), states, mats["wood_dark"], 0.02, teacher_closed)
    box("TeacherDesk_Closed_BaseR", (1.13, 0.38, -2.18), (0.30, 0.70, 0.72), states, mats["wood_dark"], 0.02, teacher_closed)
    box("TeacherDesk_Closed_Drawer", (0.4, 0.55, -1.75), (0.62, 0.24, 0.12), states, mats["wood"], 0.015, teacher_closed)

    teacher_open = empty("TeacherDesk_Open", states)
    box("TeacherDesk_Open_Top", (0.4, 0.78, -2.18), (1.85, 0.12, 0.82), states, mats["wood"], 0.035, teacher_open)
    box("TeacherDesk_Open_BaseL", (-0.33, 0.38, -2.18), (0.30, 0.70, 0.72), states, mats["wood_dark"], 0.02, teacher_open)
    box("TeacherDesk_Open_BaseR", (1.13, 0.38, -2.18), (0.30, 0.70, 0.72), states, mats["wood_dark"], 0.02, teacher_open)
    box("TeacherDesk_Open_Drawer", (0.4, 0.55, -1.54), (0.62, 0.24, 0.48), states, mats["wood"], 0.015, teacher_open)

    cabinet_closed = empty("MapCabinet_Closed", states)
    box("MapCabinet_Closed_Body", (-3.83, 1.05, -2.80), (1.15, 2.10, 0.62), states, mats["wood_dark"], 0.03, cabinet_closed)
    box("MapCabinet_Closed_Door", (-3.83, 1.08, -2.46), (1.04, 1.90, 0.08), states, mats["wood"], 0.02, cabinet_closed)
    cylinder("MapCabinet_Closed_Knob", (-3.62, 1.05, -2.40), 0.045, 0.06, states, mats["amber"], (math.pi / 2, 0, 0), 12, cabinet_closed)

    cabinet_open = empty("MapCabinet_Open", states)
    box("MapCabinet_Open_Body", (-3.83, 1.05, -2.80), (1.15, 2.10, 0.62), states, mats["wood_dark"], 0.03, cabinet_open)
    door_open = box("MapCabinet_Open_Door", (-3.22, 1.08, -2.42), (0.08, 1.90, 1.04), states, mats["wood"], 0.02, cabinet_open)
    door_open.rotation_euler.z = math.pi / 2
    box("MapCabinet_Photo", (-3.83, 1.16, -2.43), (0.56, 0.42, 0.025), states, mats["paper"], parent=cabinet_open)

    chalk_root = empty("Chalk", states)
    cylinder("Chalk_Stick", (-4.48, 0.94, -1.10), 0.025, 0.22, states, mats["chalk"], (0, math.pi / 2, 0), 12, chalk_root)

    prototype_meshes: dict[str, bpy.types.Mesh] | None = None
    xs = (-3.35, -1.68, 0.0, 1.68, 3.35)
    zs = (-0.45, 1.28)
    counter = 1
    for z in zs:
        for x in xs:
            prototype_meshes = make_desk_group(
                f"StudentDeskGroup_{counter:02d}", x, z, furniture,
                mats["wood"], mats["metal"], prototype_meshes
            )
            counter += 1

    random.seed(3917)
    for index in range(12):
        x = random.uniform(-4.35, 4.35)
        z = random.uniform(-3.25, 3.25)
        length = random.uniform(0.10, 0.32)
        crack = box(f"FloorWear_{index + 1:02d}", (x, 0.012, z), (length, 0.012, 0.025), details, mats["wood_dark"])
        crack.rotation_euler.z = random.uniform(-math.pi, math.pi)

    validate_scene()


def validate_scene() -> dict[str, object]:
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    names = set(bpy.data.objects.keys())
    missing = sorted(REQUIRED_OBJECTS - names)
    if missing:
        raise RuntimeError(f"Missing required objects: {', '.join(missing)}")
    desk_groups = sorted(name for name in names if name.startswith("StudentDeskGroup_") and "_" not in name[17:])
    if len(desk_groups) != 10:
        raise RuntimeError(f"Expected 10 student desk groups, got {len(desk_groups)}")
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    if any(min(obj.dimensions) <= 0 for obj in mesh_objects):
        raise RuntimeError("Every mesh object must have non-zero dimensions")
    wall_meshes = [obj for obj in mesh_objects if obj.name.startswith("Wall_")]
    if not wall_meshes or any(min(obj.dimensions) < 0.05 for obj in wall_meshes):
        raise RuntimeError("Wall meshes must be closed solids with measurable thickness")
    return {
        "blenderVersion": bpy.app.version_string,
        "roomSizeMeters": list(ROOM_SIZE),
        "requiredObjects": sorted(REQUIRED_OBJECTS),
        "studentDeskGroups": desk_groups,
        "meshObjectCount": len(mesh_objects),
        "materialCount": len(bpy.data.materials),
    }


def export_glb(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_materials="EXPORT",
        export_texcoords=True,
        export_normals=True,
        export_yup=True,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"glTF export failed: {result}")


def build(root: Path) -> None:
    build_scene()
    source = root / SOURCE_RELATIVE
    glb = root / GLB_RELATIVE
    manifest = root / MANIFEST_RELATIVE
    source.parent.mkdir(parents=True, exist_ok=True)
    glb.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    export_glb(glb)
    summary = validate_scene()
    summary.update({
        "schemaVersion": 1,
        "sourceFile": SOURCE_RELATIVE.as_posix(),
        "runtimeFile": GLB_RELATIVE.as_posix(),
        "sourceSha256": sha256(source),
        "runtimeSha256": sha256(glb),
    })
    manifest.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"CLASSROOM_3D_BUILD_PASS meshes={summary['meshObjectCount']} desks=10")


def check(root: Path) -> None:
    source = root / SOURCE_RELATIVE
    glb = root / GLB_RELATIVE
    manifest_path = root / MANIFEST_RELATIVE
    if not source.is_file() or not glb.is_file() or not manifest_path.is_file():
        raise RuntimeError("Classroom 3D source, GLB, or manifest is missing")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene()
    if manifest.get("sourceSha256") != sha256(source):
        raise RuntimeError("Tracked Blender source hash does not match manifest")
    if manifest.get("runtimeSha256") != sha256(glb):
        raise RuntimeError("Tracked GLB hash does not match manifest")
    if manifest.get("requiredObjects") != summary["requiredObjects"]:
        raise RuntimeError("Required object manifest is stale")
    with tempfile.TemporaryDirectory(prefix="town-rpg-classroom-") as temporary:
        candidate = Path(temporary) / "classroom-mvp.glb"
        export_glb(candidate)
        if sha256(candidate) != sha256(glb):
            raise RuntimeError("Fresh GLB export is not deterministic")
    print(f"CLASSROOM_3D_CHECK_PASS meshes={summary['meshObjectCount']} desks=10")


def main() -> None:
    args = parse_args()
    root = Path(os.path.abspath(args.root))
    if args.mode in ("build", "mvp-build"):
        build(root)
    else:
        check(root)


if __name__ == "__main__":
    main()
