"""Build and validate the expanded 12 x 9 x 4 m classroom art pass v4."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Vector

BLENDER_VERSION = (5, 2, 0)
ROOM_SIZE = (12.0, 9.0, 4.0)
SOURCE = Path("art/blender/dream_classroom/classroom-art-pass-v4.blend")
RUNTIME = Path("assets/generated/dream_escape/3d/classroom-art-pass-v4.glb")
MANIFEST = Path("assets/generated/dream_escape/3d/classroom-art-pass-v4-manifest.json")
SEED = Path("art/blender/dream_classroom/classroom-art-pass-v3.blend")
CAPTURE = Path("outputs/classroom-v4/lookdev-southeast.png")
MAX_TRIANGLES = 100_000
MAX_MESHES = 170
MAX_BYTES = 45 * 1024 * 1024

STABLE = {
    "Room_Floor", "Room_Ceiling", "Wall_North", "Wall_South", "Wall_East", "Wall_West",
    "Door_East", "Door_East_Hinge", "DoorV4_Leaf", "Window_West", "WindowCache_Closed",
    "WindowCache_Open", "ClockHand_Pickup", "Blackboard_Blank", "Blackboard_Revealed",
    "Clock_Missing", "Clock_HandOnly", "Clock_KeyOnly", "Clock_Ready", "Clock_Restored",
    "Roster_Missing", "Roster_Inserted", "Roster_Aligned", "TeacherDesk_Closed",
    "TeacherDesk_Open", "MapCabinet_Closed", "MapCabinet_Open", "TransomCache_Closed",
    "TransomCache_Open",
}
DESK_ROOTS = [f"StudentDeskGroup_{index:02d}" for index in range(1, 19)]


def args():
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("v4-build", "art-export", "art-check", "capture"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def digest(path: Path):
    result = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def collection(name):
    current = bpy.data.collections.get(name)
    if current is None:
        current = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(current)
    return current


def link_only(obj, target):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    target.objects.link(obj)


def material(name, color=(0.25, 0.25, 0.25, 1), roughness=0.8, metallic=0.0):
    current = bpy.data.materials.get(name)
    if current is not None:
        return current
    current = bpy.data.materials.new(name)
    current.diffuse_color = color
    current.use_nodes = True
    bsdf = current.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return current


def empty(name, target, position=(0, 0, 0)):
    x, height, depth = position
    obj = bpy.data.objects.new(name, None)
    obj.location = (x, -depth, height)
    target.objects.link(obj)
    return obj


def parent_keep_world(obj, parent):
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def box(name, position, size, target, mat, bevel=0.0, parent=None):
    x, height, depth = position
    width, size_height, size_depth = size
    bpy.ops.mesh.primitive_cube_add(location=(x, -depth, height))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (width, size_depth, size_height)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    link_only(obj, target)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("WornEdges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def cylinder(name, position, radius, height, target, mat, vertices=16, parent=None, wall=False):
    x, logical_y, depth = position
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height,
                                       location=(x, -depth, logical_y))
    obj = bpy.context.object
    obj.name = name
    if wall:
        obj.rotation_euler.x = math.pi / 2
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    link_only(obj, target)
    obj.data.materials.append(mat)
    if parent is not None:
        parent_keep_world(obj, parent)
    return obj


def join(parts, name, parent=None):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    if parent is not None:
        parent_keep_world(result, parent)
    return result


def reset_scene(root: Path):
    seed = root / SEED
    if not seed.is_file():
        raise RuntimeError(f"Missing v3 seed: {seed}")
    bpy.ops.wm.open_mainfile(filepath=str(seed))
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for current in list(bpy.data.collections):
        if current.name != "Collection":
            bpy.data.collections.remove(current)
    base = bpy.data.collections.get("Collection")
    if base is not None:
        base.name = "Shell"


def build_shell(cols, mats):
    shell, props = cols["Shell"], cols["Props"]
    floor = empty("Room_Floor", shell)
    box("V4_Floor", (0, -0.09, 0), (12, 0.18, 9), shell, mats["floor"], 0.01, floor)
    ceiling = empty("Room_Ceiling", shell)
    box("V4_Ceiling", (0, 4.04, 0), (12, 0.16, 9), shell, mats["plaster"], 0.01, ceiling)
    north = empty("Wall_North", shell)
    south = empty("Wall_South", shell)
    for root, z in ((north, -4.5), (south, 4.5)):
        box(f"{root.name}_Lower", (0, 0.7, z), (12, 1.4, .2), shell, mats["teal"], .01, root)
        box(f"{root.name}_Upper", (0, 2.7, z), (12, 2.6, .2), shell, mats["plaster"], .01, root)
    east = empty("Wall_East", shell)
    for name, z, depth in (("North", -.575, 7.85), ("South", 4.18, .64)):
        box(f"Wall_East_{name}_Lower", (6, .7, z), (.2, 1.4, depth), shell, mats["teal"], .01, east)
        box(f"Wall_East_{name}_Upper", (6, 2.7, z), (.2, 2.6, depth), shell, mats["plaster"], .01, east)
    box("Wall_East_Lintel", (6, 3.32, 3.35), (.2, 1.36, 1.06), shell, mats["plaster"], .01, east)
    west = empty("Wall_West", shell)
    for name, z, depth in (("North", -3.75, 1.5), ("South", 3.45, 2.1)):
        box(f"Wall_West_{name}_Lower", (-6, .7, z), (.2, 1.4, depth), shell, mats["teal"], .01, west)
        box(f"Wall_West_{name}_Upper", (-6, 2.7, z), (.2, 2.6, depth), shell, mats["plaster"], .01, west)
    box("Wall_West_Sill", (-6, .42, -.9), (.2, .84, 5.7), shell, mats["teal"], .01, west)
    box("Wall_West_Header", (-6, 3.37, -.9), (.2, 1.26, 5.7), shell, mats["plaster"], .01, west)

    for z in (-4.35, 4.35):
        box(f"Baseboard_{z}", (0, .10, z), (11.7, .20, .10), props, mats["darkwood"], .02)
    for x in (-5.85, 5.85):
        box(f"Baseboard_{x}", (x, .10, 0), (.10, .20, 8.7), props, mats["darkwood"], .02)


def build_window(cols, mats):
    props, states = cols["Props"], cols["StateVariants"]
    root = empty("Window_West", props)
    parts = []
    for y in (.82, 2.72, 1.77):
        parts.append(box("Window_H", (-5.86, y, -.9), (.20, .10, 5.72), props, mats["darkwood"], .01))
    for z in (-3.75, -1.85, .05, 1.95):
        parts.append(box("Window_V", (-5.86, 1.77, z), (.20, 1.98, .09), props, mats["darkwood"], .01))
    join(parts, "Window_ThreeBay_Frame", root)
    glass = []
    for z in (-2.80, -.90, 1.0):
        glass.append(box("Window_Glass", (-5.94, 1.77, z), (.025, 1.72, 1.78), props, mats["glass"], 0))
    join(glass, "Window_ThreeBay_Glass", root)
    box("Window_Sill", (-5.70, .76, -.9), (.40, .12, 5.86), props, mats["wood"], .02, root)
    box("Window_CurtainRail", (-5.72, 2.88, -.9), (.07, .07, 6.0), props, mats["metal"], .01, root)
    for z in (-3.55, 1.75):
        box(f"Curtain_{z}", (-5.69, 1.78, z), (.06, 1.88, .34), props, mats["paper"], .02, root)

    for state_name, opened in (("WindowCache_Closed", False), ("WindowCache_Open", True)):
        state = empty(state_name, states)
        latch_heights = (2.18, 1.22, 1.70) if opened else (1.70, 1.70, 1.70)
        for index, (z, height) in enumerate(zip((-2.8, -.9, 1.0), latch_heights), 1):
            box(f"{state_name}_Latch{index}", (-5.60, height, z), (.10, .24, .12), states, mats["amber"], .01, state)
            box(f"{state_name}_Scratch{index}", (-5.585, height, z + .20), (.025, .36, .02), states, mats["paper"], 0, state)
        drawer_depth = .42 if opened else .12
        drawer_x = -5.42 if opened else -5.62
        box(f"{state_name}_Drawer", (drawer_x, .55, -.9), (drawer_depth, .28, 1.16), states, mats["wood"], .015, state)
        if opened:
            box("Chalk", (-5.18, .67, -1.15), (.22, .06, .04), states, mats["paper"], .01, state)
            cylinder("WindowCache_ClockKey", (-5.17, .68, -.88), .06, .16, states, mats["amber"], 12, state)
            box("WindowCache_RosterSlider", (-5.18, .68, -.60), (.28, .03, .18), states, mats["glass"], .005, state)


def build_desk_mesh(cols, mats):
    target = cols["Furniture"]
    pieces = [
        box("DeskTop", (0, .79, 0), (1.02, .075, .58), target, mats["wood"], .025),
        box("DeskShelf", (0, .49, 0), (.86, .035, .42), target, mats["darkwood"], .008),
        box("ChairSeat", (0, .47, .61), (.47, .055, .41), target, mats["wood"], .02),
        box("ChairBack", (0, .78, .82), (.47, .40, .045), target, mats["wood"], .018),
    ]
    for x in (-.42, .42):
        for z in (-.22, .22):
            pieces.append(cylinder("DeskTube", (x, .38, z), .022, .72, target, mats["metal"], 12))
    for x in (-.19, .19):
        for z in (.49, .72):
            pieces.append(cylinder("ChairTube", (x, .23, z), .019, .43, target, mats["metal"], 12))
    pieces.append(box("DeskCrossbar", (0, .27, .22), (.82, .032, .032), target, mats["metal"], .006))
    return join(pieces, "StudentDeskArt_Prototype")


def build_desks(cols, mats):
    target = cols["Furniture"]
    prototype = build_desk_mesh(cols, mats)
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.context.view_layer.objects.active = prototype
    prototype.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    prototype.select_set(False)
    prototype.hide_render = True
    positions = [(x, z) for z in (-.40, 1.45, 3.05) for x in (-4.25, -2.55, -.85, .85, 2.55, 4.25)]
    for index, (x, z) in enumerate(positions, 1):
        root = empty(f"StudentDeskGroup_{index:02d}", target, (x, 0, z))
        item = bpy.data.objects.new(f"StudentDeskArt_{index:02d}", prototype.data)
        target.objects.link(item)
        item.parent = root
        item.location = (0, 0, 0)
        if index in (2, 7, 12, 17):
            book = box(f"DeskBook_{index:02d}", (x + .15, .86, z), (.36, .035, .24), target, mats["paper"], .006)
            parent_keep_world(book, root)
    bpy.data.objects.remove(prototype, do_unlink=True)


def build_blackboard_and_front(cols, mats):
    states, props = cols["StateVariants"], cols["Props"]
    for state_name, revealed in (("Blackboard_Blank", False), ("Blackboard_Revealed", True)):
        root = empty(state_name, states)
        box(f"{state_name}_Surface", (0, 2.12, -4.36), (5.6, 1.62, .08), states, mats["board"], .015, root)
        for name, pos, size in (
            ("Top", (0, 2.98, -4.30), (5.82, .10, .12)), ("Bottom", (0, 1.27, -4.30), (5.82, .11, .14)),
            ("Left", (-2.85, 2.12, -4.30), (.10, 1.82, .12)), ("Right", (2.85, 2.12, -4.30), (.10, 1.82, .12))):
            box(f"{state_name}_{name}", pos, size, states, mats["darkwood"], .012, root)
        box(f"{state_name}_Tray", (0, 1.20, -4.18), (5.6, .07, .24), states, mats["darkwood"], .01, root)
        if revealed:
            for index, x in enumerate((-1.25, 0, 1.25)):
                box(f"BlackboardSymbol{index}", (x, 2.12, -4.13), (.62, .09, .035), states, mats["paper"], .01, root)

    for state_name, opened in (("TeacherDesk_Closed", False), ("TeacherDesk_Open", True)):
        root = empty(state_name, states)
        box(f"{state_name}_Top", (.55, .86, -3.25), (2.0, .11, .92), states, mats["wood"], .03, root)
        box(f"{state_name}_Body", (.55, .43, -3.28), (1.88, .78, .72), states, mats["darkwood"], .02, root)
        drawer_z = -2.72 if opened else -2.88
        box(f"{state_name}_Drawer", (.55, .62, drawer_z), (.72, .25, .42 if opened else .10), states, mats["wood"], .012, root)
        box(f"{state_name}_Lock", (.55, .62, drawer_z + (.24 if opened else .08)), (.38, .17, .04), states, mats["metal"], .006, root)
        box(f"{state_name}_Paper", (.20, .94, -3.25), (.48, .015, .34), states, mats["paper"], .003, root)

    pickup = empty("ClockHand_Pickup", props)
    cylinder("ClockHand_Pickup_Hub", (3.65, .07, -3.25), .07, .025, props, mats["amber"], 16, pickup)
    hand = box("ClockHand_Pickup_Needle", (3.65, .09, -3.10), (.035, .025, .32), props, mats["amber"], .005, pickup)
    hand.rotation_euler.z = .42


def clock_geometry(name, states, mats, hand, key, seven):
    root = empty(name, states)
    cylinder(f"{name}_Rim", (3.55, 2.66, -4.30), .44, .12, states, mats["metal"], 40, root, True)
    cylinder(f"{name}_Face", (3.55, 2.66, -4.22), .37, .025, states, mats["paper"], 40, root, True)
    if hand:
        angle = math.radians(-150 if seven else -90)
        needle = box(f"{name}_Hour", (3.55, 2.66, -4.17), (.035, .24, .025), states, mats["metal"], .003, root)
        needle.rotation_euler.y = angle
    box(f"{name}_Minute", (3.55, 2.80, -4.165), (.03, .27, .025), states, mats["metal"], .003, root)
    if key:
        cylinder(f"{name}_Key", (3.99, 2.56, -4.28), .055, .18, states, mats["amber"], 12, root)


def build_clock_roster(cols, mats):
    states = cols["StateVariants"]
    clock_geometry("Clock_Missing", states, mats, False, False, False)
    clock_geometry("Clock_HandOnly", states, mats, True, False, False)
    clock_geometry("Clock_KeyOnly", states, mats, False, True, False)
    clock_geometry("Clock_Ready", states, mats, True, True, False)
    clock_geometry("Clock_Restored", states, mats, True, True, True)
    for state_name, overlay, aligned in (("Roster_Missing", False, False), ("Roster_Inserted", True, False), ("Roster_Aligned", True, True)):
        root = empty(state_name, states)
        box(f"{state_name}_Paper", (4.75, 2.20, -4.31), (.92, 1.25, .04), states, mats["paper"], .006, root)
        for row in range(7):
            box(f"{state_name}_Row{row}", (4.75, 2.55 - row * .15, -4.275), (.66, .012, .012), states, mats["metal"], 0, root)
        if overlay:
            x = 4.75 if aligned else 4.52
            box(f"{state_name}_Overlay", (x, 2.20, -4.235), (.58, .92, .018), states, mats["glass"], .003, root)
        if aligned:
            cylinder("Roster_Second_Circle", (4.48, 2.39, -4.19), .07, .018, states, mats["red"], 24, root, True)


def build_cabinet_transom_door(cols, mats):
    states, props = cols["StateVariants"], cols["Props"]
    for state_name, opened in (("MapCabinet_Closed", False), ("MapCabinet_Open", True)):
        root = empty(state_name, states)
        box(f"{state_name}_Body", (-5.05, 1.15, -3.78), (1.32, 2.30, .68), states, mats["darkwood"], .025, root)
        for row in (.25, .51, .77):
            box(f"{state_name}_Drawer{row}", (-5.05, row, -3.40), (1.16, .21, .08), states, mats["wood"], .01, root)
            box(f"{state_name}_Handle{row}", (-5.05, row, -3.34), (.28, .04, .04), states, mats["amber"], .008, root)
        box(f"{state_name}_Map", (-5.05, 1.62, -3.39), (.95, 1.02, .025), states, mats["paper"], .004, root)
        if opened:
            box("MapCabinet_Negative", (-5.05, 1.25, -3.32), (.58, .03, .42), states, mats["glass"], .003, root)
            box("MapCabinet_Overlay", (-4.85, 1.29, -3.30), (.50, .025, .36), states, mats["glass"], .003, root)
        else:
            box("MapCabinet_Glass", (-5.05, 1.62, -3.34), (1.08, 1.18, .025), states, mats["glass"], .003, root)

    door_root = empty("Door_East", props)
    hinge = empty("Door_East_Hinge", props, (5.86, 0, 3.86))
    hinge.parent = door_root
    leaf = box("DoorV4_Leaf", (5.86, 1.18, 3.35), (.16, 2.36, 1.02), props, mats["darkwood"], .025)
    parent_keep_world(leaf, hinge)
    box("DoorV4_Glass", (5.76, 1.72, 3.35), (.025, .58, .52), props, mats["glass"], .004, hinge)
    cylinder("DoorV4_Handle", (5.68, 1.03, 3.02), .055, .16, props, mats["amber"], 16, hinge)
    for state_name, opened in (("TransomCache_Closed", False), ("TransomCache_Open", True)):
        root = empty(state_name, states)
        x = 5.70 if not opened else 5.48
        box(f"{state_name}_Box", (x, 3.02, 3.35), (.30 if opened else .18, .42, .90), states, mats["wood"], .015, root)
        cylinder(f"{state_name}_Ring", (5.45, 2.84, 3.35), .09, .03, states, mats["amber"], 18, root)
        if opened:
            box("TransomCache_Tape", (5.30, 2.90, 3.35), (.06, .14, .48), states, mats["metal"], .005, root)


def build_decor(cols, mats):
    props = cols["Props"]
    for index, (x, z) in enumerate(((-3.7, -2.4), (-1.25, -2.4), (1.25, -2.4), (3.7, -2.4)), 1):
        box(f"Fluorescent_{index}", (x, 3.82, z), (1.55, .09, .16), props, mats["paper"], .02)
        box(f"FluorescentWire_{index}", (x, 3.93, z), (.035, .22, .035), props, mats["metal"], .004)
    for index, (x, z) in enumerate(((-2.0, .6), (2.0, .6)), 1):
        cylinder(f"CeilingFanHub_{index}", (x, 3.70, z), .15, .22, props, mats["metal"], 20)
        for angle in (0, math.pi / 2):
            blade = box(f"CeilingFanBlade_{index}", (x, 3.58, z), (1.62, .035, .16), props, mats["darkwood"], .02)
            blade.rotation_euler.z = angle
    box("NoticeBoard", (3.55, 1.62, 4.36), (2.4, 1.28, .08), props, mats["wood"], .02)
    box("WallSpeaker", (-4.75, 3.05, 4.35), (.55, .42, .16), props, mats["metal"], .03)
    box("Conduit_East", (5.86, 3.36, 0), (.06, .06, 6.6), props, mats["metal"], .005)
    box("SwitchBox", (5.75, 1.42, .65), (.18, .32, .22), props, mats["metal"], .01)
    box("BroomHandle", (-5.55, .82, 3.65), (.035, 1.64, .035), props, mats["wood"], .005)
    box("Dustpan", (-5.50, .15, 3.38), (.34, .25, .42), props, mats["metal"], .02)
    box("WasteBasket", (5.10, .30, -3.65), (.48, .60, .48), props, mats["metal"], .03)


def build(root: Path):
    reset_scene(root)
    cols = {name: collection(name) for name in ("Shell", "Furniture", "Props", "Decals", "StateVariants", "Lookdev")}
    mats = {
        "plaster": material("M_OldPlaster", (.22, .22, .19, 1)),
        "teal": material("M_FadedTeal", (.06, .20, .20, 1)),
        "floor": material("M_WornFloor", (.13, .14, .13, 1)),
        "wood": material("M_WornWood", (.29, .18, .09, 1)),
        "darkwood": material("M_DarkWood", (.10, .06, .035, 1)),
        "board": material("M_Blackboard", (.025, .08, .07, 1)),
        "metal": material("M_DarkMetal", (.05, .06, .06, 1), .55, .65),
        "paper": material("M_AgedPaper", (.58, .50, .36, 1)),
        "glass": material("M_DirtyGlass", (.10, .18, .18, .34), .20),
        "amber": material("M_Amber", (.46, .24, .06, 1), .42, .55),
        "red": material("M_FadedRed", (.35, .05, .04, 1)),
    }
    build_shell(cols, mats)
    build_window(cols, mats)
    build_desks(cols, mats)
    build_blackboard_and_front(cols, mats)
    build_clock_roster(cols, mats)
    build_cabinet_transom_door(cols, mats)
    build_decor(cols, mats)
    scene = bpy.context.scene
    scene["town_rpg_room_size"] = "12x9x4"
    scene["town_rpg_desk_count"] = 18
    scene["town_rpg_schema"] = 1
    for image in bpy.data.images:
        if image.source == "FILE" and image.filepath:
            try:
                image.pack()
            except RuntimeError:
                pass
    source = root / SOURCE
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))


def export(root: Path, destination: Path):
    source = root / SOURCE
    if not source.is_file():
        raise RuntimeError(f"Missing v4 source: {source}")
    bpy.ops.wm.open_mainfile(filepath=str(source))
    destination.parent.mkdir(parents=True, exist_ok=True)
    for obj in bpy.data.objects:
        obj.hide_set(obj.name.startswith("Lookdev"))
    bpy.ops.export_scene.gltf(filepath=str(destination), export_format="GLB", export_cameras=False,
                              export_lights=False, export_animations=False, export_apply=True,
                              export_image_format="AUTO")


def stats():
    names = set(bpy.data.objects.keys())
    missing = sorted(STABLE - names)
    desks = sorted(name for name in names if name.startswith("StudentDeskGroup_"))
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH" and not obj.name.startswith("Lookdev")]
    triangles = 0
    for obj in meshes:
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
    shared = len({bpy.data.objects[f"StudentDeskArt_{index:02d}"].data.as_pointer() for index in range(1, 19)}) == 1
    variant_groups = [
        ["WindowCache_Closed", "WindowCache_Open"],
        ["Blackboard_Blank", "Blackboard_Revealed"],
        ["Clock_Missing", "Clock_HandOnly", "Clock_KeyOnly", "Clock_Ready", "Clock_Restored"],
        ["Roster_Missing", "Roster_Inserted", "Roster_Aligned"],
        ["TeacherDesk_Closed", "TeacherDesk_Open"],
        ["MapCabinet_Closed", "MapCabinet_Open"],
        ["TransomCache_Closed", "TransomCache_Open"],
    ]
    def descendant_count(root_name):
        root = bpy.data.objects[root_name]
        def descends_from(mesh):
            parent = mesh.parent
            while parent is not None:
                if parent == root:
                    return True
                parent = parent.parent
            return False
        return sum(1 for mesh in meshes if descends_from(mesh))
    all_variant_names = {name for group in variant_groups for name in group}
    variant_meshes = sum(descendant_count(name) for name in all_variant_names)
    maximum_visible = len(meshes) - variant_meshes + sum(max(descendant_count(name) for name in group) for group in variant_groups)
    return missing, desks, meshes, triangles, shared, maximum_visible


def validate(root: Path, runtime: Path, write_manifest=True):
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    missing, desks, meshes, triangles, shared, maximum_visible = stats()
    if missing or desks != DESK_ROOTS or not shared:
        raise RuntimeError(f"v4 structure invalid missing={missing} desks={len(desks)} shared={shared}")
    if triangles > MAX_TRIANGLES or maximum_visible > MAX_MESHES or runtime.stat().st_size > MAX_BYTES:
        raise RuntimeError(f"v4 budget exceeded triangles={triangles} visible={maximum_visible} bytes={runtime.stat().st_size}")
    manifest = {
        "schemaVersion": 1,
        "blenderVersion": "5.2.0 LTS",
        "roomSizeMeters": list(ROOM_SIZE),
        "walkableSizeMeters": [11.4, 8.4],
        "studentDeskGroups": DESK_ROOTS,
        "sharedStudentDeskMesh": shared,
        "requiredObjects": sorted(STABLE),
        "meshObjectCount": len(meshes),
        "maximumVisibleMeshCount": maximum_visible,
        "triangleCount": triangles,
        "materialCount": len(bpy.data.materials),
        "sourceFile": SOURCE.as_posix(),
        "seedFile": SEED.as_posix(),
        "runtimeFile": RUNTIME.as_posix(),
        "sourceSha256": digest(root / SOURCE),
        "runtimeSha256": digest(runtime),
        "runtimeBytes": runtime.stat().st_size,
        "limits": {"triangles": MAX_TRIANGLES, "visibleMeshes": MAX_MESHES, "runtimeBytes": MAX_BYTES},
        "exportProfile": {"format": "GLB", "cameras": False, "lights": False, "animations": False,
                          "runtimeDependsOnBlender": False},
    }
    if write_manifest:
        path = root / MANIFEST
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def capture(root: Path):
    bpy.ops.wm.open_mainfile(filepath=str(root / SOURCE))
    lookdev = collection("Lookdev")
    world = bpy.context.scene.world or bpy.data.worlds.new("ClassroomV4World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.015, .032, .042, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .32
    camera_data = bpy.data.cameras.new("LookdevV4CameraData")
    camera = bpy.data.objects.new("LookdevV4Camera", camera_data)
    lookdev.objects.link(camera)
    camera.location = (8.8, -9.4, 7.4)
    target = Vector((0, 0, 1.2))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 46
    bpy.context.scene.camera = camera
    sun_data = bpy.data.lights.new("LookdevV4SunData", "SUN")
    sun_data.energy = 1.4
    sun_data.color = (1.0, .56, .28)
    sun = bpy.data.objects.new("LookdevV4Sun", sun_data)
    lookdev.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-58))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 1920, 1080, 100
    scene.render.filepath = str(root / CAPTURE)
    (root / CAPTURE).parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)


def main():
    options = args()
    root = Path(options.root).resolve()
    if options.mode == "v4-build":
        build(root)
        export(root, root / RUNTIME)
        manifest = validate(root, root / RUNTIME)
        print(f"CLASSROOM_V4_BUILD_PASS triangles={manifest['triangleCount']} desks=18")
    elif options.mode == "art-export":
        export(root, root / RUNTIME)
        manifest = validate(root, root / RUNTIME)
        print(f"CLASSROOM_V4_EXPORT_PASS sha256={manifest['runtimeSha256']}")
    elif options.mode == "art-check":
        with tempfile.TemporaryDirectory() as temporary:
            candidate = Path(temporary) / "classroom-art-pass-v4.glb"
            export(root, candidate)
            manifest = validate(root, candidate, write_manifest=False)
            if manifest["runtimeSha256"] != digest(root / RUNTIME):
                raise RuntimeError("Temporary v4 export hash differs from tracked runtime GLB")
        print("CLASSROOM_V4_CHECK_PASS room=12x9x4 desks=18")
    else:
        capture(root)
        print(f"CLASSROOM_V4_CAPTURE_PASS file={CAPTURE.as_posix()}")


if __name__ == "__main__":
    main()
