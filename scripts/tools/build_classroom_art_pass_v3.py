"""Build, export, validate, and render classroom art pass v3.

V2 remains immutable.  `v3-build` opens the tracked v2 Blend as a seed,
rebuilds the visual-only art layer, packs every used image, and writes a new
v3 Blend/GLB pair.  Puzzle roots, desk roots, and the east-door hinge survive
unchanged so this asset can later replace v2 without changing Core state.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Vector


BLENDER_VERSION = (5, 2, 0)
ROOM_SIZE = (9.6, 7.2, 3.2)
V2_SOURCE = Path("art/blender/dream_classroom/classroom-art-pass-v2.blend")
V3_SOURCE = Path("art/blender/dream_classroom/classroom-art-pass-v3.blend")
V3_GLB = Path("assets/generated/dream_escape/3d/classroom-art-pass-v3.glb")
V3_MANIFEST = Path("assets/generated/dream_escape/3d/classroom-art-pass-v3-manifest.json")
TEXTURE_DIR = Path("art/blender/dream_classroom/textures-v3")
TEXTURE_MANIFEST = TEXTURE_DIR / "classroom-art-textures-v3-manifest.json"
CAPTURE = Path("outputs/classroom-3d-art-pass-v3/lookdev-southeast.png")
MAX_TRIANGLES = 80_000
MAX_MESHES = 140
MAX_GLB_BYTES = 35 * 1024 * 1024

STABLE_ROOTS = {
    "Wall_North", "Wall_South", "Wall_East", "Wall_West", "Window_West",
    "Door_East", "Door_East_Hinge", "DoorV3_Leaf", "Blackboard_Blank",
    "Blackboard_Revealed", "Clock_Seven", "DutyRoster_Second",
    "TeacherDesk_Closed", "TeacherDesk_Open", "MapCabinet_Closed",
    "MapCabinet_Open", "Chalk", "Room_Floor",
}
DESK_ROOTS = [f"StudentDeskGroup_{index:02d}" for index in range(1, 11)]
DESK_X = (-3.35, -1.68, 0.0, 1.68, 3.35)
DESK_Z = (-0.45, 1.28)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("v3-build", "art-export", "art-check", "capture"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def matrices_match(left, right, tolerance: float = 0.000001) -> bool:
    return all(abs(left[row][column] - right[row][column]) <= tolerance
               for row in range(4) for column in range(4))


def load_v2_helpers(root: Path):
    path = root / "scripts/tools/build_classroom_art_pass.py"
    spec = importlib.util.spec_from_file_location("town_rpg_classroom_v2", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load v2 classroom helpers: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def ensure_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
    return collection


def delete_object(obj: bpy.types.Object) -> None:
    for child in list(obj.children):
        delete_object(child)
    bpy.data.objects.remove(obj, do_unlink=True)


def clear_children(root: bpy.types.Object) -> None:
    for child in list(root.children):
        delete_object(child)


def remove_names(prefixes: tuple[str, ...], exclusions: set[str] | None = None) -> None:
    exclusions = exclusions or set()
    for obj in list(bpy.data.objects):
        if obj.name not in exclusions and obj.name.startswith(prefixes):
            delete_object(obj)


def load_image(root: Path, filename: str, colorspace: str) -> bpy.types.Image:
    path = root / TEXTURE_DIR / filename
    if not path.is_file():
        raise RuntimeError(f"Missing v3 texture: {path}")
    image = bpy.data.images.load(str(path), check_existing=True)
    image.name = f"V3_{Path(filename).stem}"
    image.colorspace_settings.name = colorspace
    return image


def rebuild_textured_material(root: Path, name: str, stem: str) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (620, 0)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (360, 0)
    albedo = nodes.new("ShaderNodeTexImage")
    albedo.name = "V3Albedo"; albedo.image = load_image(root, f"{stem}-albedo.png", "sRGB")
    albedo.extension = "REPEAT"; albedo.location = (-440, 170)
    normal = nodes.new("ShaderNodeTexImage")
    normal.name = "V3Normal"; normal.image = load_image(root, f"{stem}-normal.png", "Non-Color")
    normal.extension = "REPEAT"; normal.location = (-440, -80)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.42
    normal_map.location = (80, -90)
    orm = nodes.new("ShaderNodeTexImage")
    orm.name = "V3ORM"; orm.image = load_image(root, f"{stem}-orm.png", "Non-Color")
    orm.extension = "REPEAT"; orm.location = (-440, -350)
    separate = nodes.new("ShaderNodeSeparateColor"); separate.location = (-80, -330)
    links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(orm.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(separate.outputs["Blue"], bsdf.inputs["Metallic"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def rebuild_solid_material(name: str, color: tuple[float, float, float, float], roughness: float,
                           metallic: float = 0.0, alpha: float = 1.0) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes; links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def rebuild_image_material(root: Path, name: str, filename: str, alpha: bool, emission: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes; links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = load_image(root, filename, "sRGB")
    texture.extension = "CLIP" if alpha else "EXTEND"
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        links.new(texture.outputs["Alpha"], bsdf.inputs["Alpha"])
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"
    if emission:
        emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission_input is not None:
            links.new(texture.outputs["Color"], emission_input)
        if bsdf.inputs.get("Emission Strength") is not None:
            bsdf.inputs["Emission Strength"].default_value = emission
    bsdf.inputs["Roughness"].default_value = 0.9
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


def rebuild_materials(root: Path) -> dict[str, bpy.types.Material]:
    textured = {
        "M_OldPlaster": "plaster", "M_FadedTeal": "teal", "M_WornFloor": "floor",
        "M_WornWood": "wood", "M_DarkWood": "darkwood", "M_Blackboard": "blackboard",
    }
    materials = {name: rebuild_textured_material(root, name, stem) for name, stem in textured.items()}
    solids = {
        "M_DarkMetal": ((0.045, 0.055, 0.055, 1), 0.56, 0.72, 1.0),
        "M_AgedPaper": ((0.56, 0.50, 0.36, 1), 0.88, 0.0, 1.0),
        "M_DirtyGlass": ((0.12, 0.20, 0.20, 1), 0.22, 0.05, 0.30),
        "M_Amber": ((0.45, 0.24, 0.055, 1), 0.45, 0.62, 1.0),
        "M_Chalk": ((0.72, 0.70, 0.61, 1), 0.96, 0.0, 1.0),
        "M_FadedRed": ((0.28, 0.065, 0.05, 1), 0.82, 0.0, 1.0),
        "M_Baseboard": ((0.095, 0.068, 0.045, 1), 0.84, 0.0, 1.0),
        "M_Curtain": ((0.37, 0.35, 0.28, 1), 0.94, 0.0, 1.0),
        "M_Rubber": ((0.025, 0.028, 0.027, 1), 0.86, 0.0, 1.0),
        "M_Dust": ((0.23, 0.18, 0.11, 1), 0.96, 0.0, 0.45),
        "M_WallStain": ((0.055, 0.073, 0.067, 1), 0.98, 0.0, 0.42),
        "M_FoliageSilhouette": ((0.025, 0.06, 0.045, 1), 0.96, 0.0, 0.72),
    }
    for name, (color, roughness, metallic, alpha) in solids.items():
        materials[name] = rebuild_solid_material(name, color, roughness, metallic, alpha)
    materials["M_DetailDecal"] = rebuild_image_material(root, "M_DetailDecal", "details.png", True)
    materials["M_WindowExterior"] = rebuild_image_material(root, "M_WindowExterior", "window-exterior.png", False, 0.10)
    return materials


def join_meshes(objects: list[bpy.types.Object], name: str, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    if not objects:
        raise RuntimeError(f"Nothing to join for {name}")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = objects[0]
    result.name = name
    if parent is not None:
        world = result.matrix_world.copy()
        result.parent = parent
        result.matrix_world = world
    return result


def intervals(start: float, end: float, maximum: float = 1.2) -> list[tuple[float, float]]:
    length = end - start
    count = max(1, math.ceil(length / maximum))
    step = length / count
    return [(start + index * step, start + (index + 1) * step) for index in range(count)]


def rebuild_shell(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    shell = collections["Shell"]
    for prefix in ("Wall_North_", "Wall_South_", "Wall_East_", "Wall_West_", "ThirdPerson_WallCap_", "V3_WallSurface_"):
        remove_names((prefix,))

    def component(parts: list[bpy.types.Object], name: str, parent: bpy.types.Object) -> None:
        join_meshes(parts, name, parent)

    for wall_name, depth in (("North", -3.60), ("South", 3.60)):
        root = bpy.data.objects[f"Wall_{wall_name}"]
        pieces: list[bpy.types.Object] = []
        for x0, x1 in intervals(-4.80, 4.80):
            x = (x0 + x1) / 2; width = x1 - x0
            pieces.append(v2.new_box(f"V3Tmp_{wall_name}_Lower", (x, 0.70, depth), (width, 1.40, 0.18), shell, mats["M_FadedTeal"]))
            pieces.append(v2.new_box(f"V3Tmp_{wall_name}_Upper", (x, 2.30, depth), (width, 1.80, 0.18), shell, mats["M_OldPlaster"]))
            pieces.append(v2.new_box(f"V3Tmp_{wall_name}_Cap", (x, 3.70, depth), (width, 1.00, 0.18), shell, mats["M_OldPlaster"]))
        component(pieces, f"V3_WallSurface_{wall_name}", root)

    east = bpy.data.objects["Wall_East"]
    east_parts: list[bpy.types.Object] = []
    for start, end in ((-3.60, 1.64), (2.76, 3.60)):
        for z0, z1 in intervals(start, end):
            z = (z0 + z1) / 2; depth = z1 - z0
            east_parts.append(v2.new_box("V3Tmp_East_Lower", (4.80, 0.70, z), (0.18, 1.40, depth), shell, mats["M_FadedTeal"]))
            east_parts.append(v2.new_box("V3Tmp_East_Upper", (4.80, 2.30, z), (0.18, 1.80, depth), shell, mats["M_OldPlaster"]))
    for z0, z1 in intervals(1.64, 2.76):
        east_parts.append(v2.new_box("V3Tmp_East_Lintel", (4.80, 2.69, (z0 + z1) / 2), (0.18, 1.02, z1 - z0), shell, mats["M_OldPlaster"]))
    for z0, z1 in intervals(-3.60, 3.60):
        east_parts.append(v2.new_box("V3Tmp_East_Cap", (4.80, 3.70, (z0 + z1) / 2), (0.18, 1.00, z1 - z0), shell, mats["M_OldPlaster"]))
    component(east_parts, "V3_WallSurface_East", east)

    west = bpy.data.objects["Wall_West"]
    west_parts: list[bpy.types.Object] = []
    for start, end in ((-3.60, -2.60), (0.40, 3.60)):
        for z0, z1 in intervals(start, end):
            z = (z0 + z1) / 2; depth = z1 - z0
            west_parts.append(v2.new_box("V3Tmp_West_Lower", (-4.80, 0.70, z), (0.18, 1.40, depth), shell, mats["M_FadedTeal"]))
            west_parts.append(v2.new_box("V3Tmp_West_Upper", (-4.80, 2.30, z), (0.18, 1.80, depth), shell, mats["M_OldPlaster"]))
    for z0, z1 in intervals(-2.60, 0.40, 0.75):
        z = (z0 + z1) / 2; depth = z1 - z0
        west_parts.append(v2.new_box("V3Tmp_West_Sill", (-4.80, 0.39, z), (0.18, 0.78, depth), shell, mats["M_FadedTeal"]))
        west_parts.append(v2.new_box("V3Tmp_West_Header", (-4.80, 2.765, z), (0.18, 0.87, depth), shell, mats["M_OldPlaster"]))
    for z0, z1 in intervals(-3.60, 3.60):
        west_parts.append(v2.new_box("V3Tmp_West_Cap", (-4.80, 3.70, (z0 + z1) / 2), (0.18, 1.00, z1 - z0), shell, mats["M_OldPlaster"]))
    component(west_parts, "V3_WallSurface_West", west)

    # Preserve the stable floor object but give its top a room-scale 16x12 UV.
    floor = bpy.data.objects["Room_Floor"]
    floor.data.materials.clear(); floor.data.materials.append(mats["M_WornFloor"])
    uv = floor.data.uv_layers.active or floor.data.uv_layers.new(name="UVMap")
    top = max(floor.data.polygons, key=lambda polygon: polygon.normal.z)
    vertices = [floor.data.vertices[floor.data.loops[index].vertex_index].co for index in top.loop_indices]
    min_x, max_x = min(v.x for v in vertices), max(v.x for v in vertices)
    min_y, max_y = min(v.y for v in vertices), max(v.y for v in vertices)
    for loop_index, vertex in zip(top.loop_indices, vertices):
        uv.data[loop_index].uv = ((vertex.x - min_x) / (max_x - min_x), (vertex.y - min_y) / (max_y - min_y))
    floor.data.update()


def make_west_plane(name: str, x: float, center_y: float, center_z: float, height: float, depth: float,
                    collection: bpy.types.Collection, material: bpy.types.Material, parent: bpy.types.Object) -> bpy.types.Object:
    y0, y1 = center_y - height / 2, center_y + height / 2
    z0, z1 = center_z - depth / 2, center_z + depth / 2
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata([(x, -z0, y0), (x, -z1, y0), (x, -z1, y1), (x, -z0, y1)], [], [(0, 1, 2, 3)])
    mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="UVMap")
    for loop, coordinate in zip(uv.data, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop.uv = coordinate
    obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj)
    obj.parent = parent
    return obj


def curtain_mesh(name: str, x: float, z0: float, z1: float, y0: float, y1: float,
                 collection: bpy.types.Collection, material: bpy.types.Material, parent: bpy.types.Object) -> bpy.types.Object:
    columns, rows = 8, 12
    vertices = []
    for row in range(rows + 1):
        height = y0 + (y1 - y0) * row / rows
        for column in range(columns + 1):
            depth = z0 + (z1 - z0) * column / columns
            fold = math.sin(column / columns * math.pi * 6) * 0.035
            sag = math.sin(row / rows * math.pi) * 0.018
            vertices.append((x + fold + sag, -depth, height))
    faces = []
    for row in range(rows):
        for column in range(columns):
            a = row * (columns + 1) + column
            faces.append((a, a + 1, a + columns + 2, a + columns + 1))
    mesh = bpy.data.meshes.new(f"{name}_Mesh"); mesh.from_pydata(vertices, [], faces); mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            row, column = divmod(vertex_index, columns + 1)
            uv.data[loop_index].uv = (column / columns, row / rows)
    obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj); obj.parent = parent
    solidify = obj.modifiers.new("CurtainThickness", "SOLIDIFY"); solidify.thickness = 0.012
    bevel = obj.modifiers.new("CurtainSoftEdge", "BEVEL"); bevel.width = 0.006; bevel.segments = 2
    bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=solidify.name); bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return obj


def rebuild_window(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    root = bpy.data.objects["Window_West"]
    clear_children(root)
    remove_names(("Window_Sill", "Window_ExtraMullion", "Curtain_", "Window_Exterior", "V3_Window"), {"Window_West"})
    props, decals = collections["Props"], collections["Decals"]
    frame: list[bpy.types.Object] = []
    for y, height in ((0.78, 0.10), (2.35, 0.10), (1.55, 0.07)):
        frame.append(v2.new_box("V3Tmp_Window_Horizontal", (-4.61, y, -1.10), (0.20, height, 3.10), props, mats["M_DarkWood"], 0.012))
    for z in (-2.60, -1.85, -1.10, -0.35, 0.40):
        frame.append(v2.new_box("V3Tmp_Window_Vertical", (-4.61, 1.565, z), (0.20, 1.67, 0.075), props, mats["M_DarkWood"], 0.010))
    join_meshes(frame, "V3_Window_FourBayFrame", root)
    glass: list[bpy.types.Object] = []
    for z in (-2.225, -1.475, -0.725, 0.025):
        glass.append(v2.new_box("V3Tmp_Window_Glass", (-4.69, 1.565, z), (0.025, 1.43, 0.69), props, mats["M_DirtyGlass"], 0.002))
    join_meshes(glass, "V3_Window_Glass", root)
    v2.new_box("V3_Window_Sill", (-4.52, 0.72, -1.10), (0.34, 0.10, 3.24), props, mats["M_DarkWood"], 0.018, root)
    v2.new_box("V3_Window_CurtainRail", (-4.47, 2.47, -1.10), (0.08, 0.055, 3.40), props, mats["M_DarkMetal"], 0.008, root)
    curtain_mesh("V3_Window_CurtainLeft", -4.46, -2.58, -2.10, 0.84, 2.40, props, mats["M_Curtain"], root)
    curtain_mesh("V3_Window_CurtainRight", -4.46, -0.08, 0.40, 0.84, 2.40, props, mats["M_Curtain"], root)
    make_west_plane("Window_Exterior", -4.89, 1.565, -1.10, 1.55, 3.0, decals, mats["M_WindowExterior"], root)
    branches = [
        v2.new_box("V3Tmp_Branch", (-4.85, 1.40, -2.0), (0.035, 0.05, 1.15), decals, mats["M_FoliageSilhouette"], 0.01),
        v2.new_box("V3Tmp_Branch", (-4.84, 1.84, -0.15), (0.035, 0.045, 0.82), decals, mats["M_FoliageSilhouette"], 0.01),
        v2.new_box("V3Tmp_Branch", (-4.83, 1.08, -0.85), (0.035, 0.04, 0.56), decals, mats["M_FoliageSilhouette"], 0.01),
    ]
    join_meshes(branches, "V3_Window_NearBranches", root)


def rebuild_map_cabinet(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    states = collections["StateVariants"]
    for state, opened in (("MapCabinet_Closed", False), ("MapCabinet_Open", True)):
        root = bpy.data.objects[state]; clear_children(root)
        opaque: list[bpy.types.Object] = []
        opaque.append(v2.new_box("V3Tmp_CabinetBody", (-3.83, 1.05, -2.80), (1.15, 2.10, 0.62), states, mats["M_DarkWood"], 0.025))
        # Lower bank of shallow archival drawers.
        for index, height in enumerate((0.22, 0.45, 0.68), start=1):
            opaque.append(v2.new_box(f"V3Tmp_CabinetDrawer{index}", (-3.83, height, -2.455), (1.02, 0.19, 0.075), states, mats["M_WornWood"], 0.010))
            opaque.append(v2.new_box(f"V3Tmp_CabinetHandle{index}", (-3.83, height, -2.405), (0.25, 0.035, 0.035), states, mats["M_Amber"], 0.008))
        opaque.append(v2.new_box("V3Tmp_CabinetDivider", (-3.83, 0.83, -2.44), (1.06, 0.08, 0.08), states, mats["M_DarkWood"], 0.010))
        join_meshes(opaque, f"V3_{state}_Body", root)
        # A faded map remains behind glass in both states.
        v2.new_box(f"V3_{state}_Map", (-3.83, 1.46, -2.43), (0.83, 0.92, 0.022), states, mats["M_AgedPaper"], 0.004, root)
        if not opened:
            frame = [
                v2.new_box("V3Tmp_CabinetFrameTop", (-3.83, 1.98, -2.39), (1.02, 0.07, 0.08), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_CabinetFrameBottom", (-3.83, 0.92, -2.39), (1.02, 0.07, 0.08), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_CabinetFrameLeft", (-4.31, 1.45, -2.39), (0.07, 1.13, 0.08), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_CabinetFrameRight", (-3.35, 1.45, -2.39), (0.07, 1.13, 0.08), states, mats["M_WornWood"], 0.008),
            ]
            join_meshes(frame, "V3_MapCabinet_Closed_GlassFrame", root)
            v2.new_box("V3_MapCabinet_Closed_Glass", (-3.83, 1.45, -2.405), (0.88, 0.98, 0.025), states, mats["M_DirtyGlass"], 0.002, root)
        else:
            door = [
                v2.new_box("V3Tmp_OpenDoorTop", (-3.22, 1.98, -2.57), (0.08, 0.07, 1.04), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_OpenDoorBottom", (-3.22, 0.92, -2.57), (0.08, 0.07, 1.04), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_OpenDoorFront", (-3.22, 1.45, -2.08), (0.08, 1.13, 0.07), states, mats["M_WornWood"], 0.008),
                v2.new_box("V3Tmp_OpenDoorBack", (-3.22, 1.45, -3.06), (0.08, 1.13, 0.07), states, mats["M_WornWood"], 0.008),
            ]
            join_meshes(door, "V3_MapCabinet_Open_Door", root)
            v2.new_box("V3_MapCabinet_Open_Glass", (-3.215, 1.45, -2.57), (0.025, 0.98, 0.88), states, mats["M_DirtyGlass"], 0.002, root)
            v2.new_box("MapCabinet_Photo", (-3.83, 1.18, -2.385), (0.55, 0.40, 0.018), states, mats["M_AgedPaper"], 0.003, root)


def rebuild_teacher_desk(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    states = collections["StateVariants"]
    for state, opened in (("TeacherDesk_Closed", False), ("TeacherDesk_Open", True)):
        root = bpy.data.objects[state]; clear_children(root)
        parts = [
            v2.new_box("V3Tmp_TeacherTop", (0.40, 0.82, -2.12), (1.85, 0.10, 0.86), states, mats["M_WornWood"], 0.028),
            v2.new_box("V3Tmp_TeacherLeft", (-0.30, 0.40, -2.12), (0.32, 0.74, 0.76), states, mats["M_DarkWood"], 0.018),
            v2.new_box("V3Tmp_TeacherRight", (1.10, 0.40, -2.12), (0.32, 0.74, 0.76), states, mats["M_DarkWood"], 0.018),
            v2.new_box("V3Tmp_TeacherModesty", (0.40, 0.43, -1.72), (1.17, 0.57, 0.065), states, mats["M_DarkWood"], 0.015),
        ]
        drawer_z = -1.50 if opened else -1.73
        drawer_depth = 0.46 if opened else 0.10
        parts.append(v2.new_box("V3Tmp_TeacherDrawer", (0.40, 0.60, drawer_z), (0.62, 0.23, drawer_depth), states, mats["M_WornWood"], 0.012))
        parts.append(v2.new_box("V3Tmp_TeacherLock", (0.40, 0.60, drawer_z + drawer_depth / 2 + 0.025), (0.34, 0.15, 0.035), states, mats["M_DarkMetal"], 0.006))
        for x in (0.31, 0.40, 0.49):
            parts.append(v2.new_cylinder("V3Tmp_TeacherKey", (x, 0.60, drawer_z + drawer_depth / 2 + 0.05), 0.017, 0.024, states, mats["M_Amber"], 10))
        join_meshes(parts, f"V3_{state}_Desk", root)
        paper = v2.new_box(f"V3_{state}_Paper", (0.14, 0.89, -2.12), (0.43, 0.012, 0.31), states, mats["M_AgedPaper"], 0.003, root)
        paper.rotation_euler.z = 0.14
        v2.new_cylinder(f"V3_{state}_Cup", (0.92, 0.95, -2.10), 0.072, 0.22, states, mats["M_DarkMetal"], 16, root)


def rebuild_desks(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    furniture = collections["Furniture"]
    roots = [bpy.data.objects[name] for name in DESK_ROOTS]
    remove_names(("StudentDeskArt_",))
    parts: list[bpy.types.Object] = [
        v2.new_box("V3Tmp_DeskTop", (0, 0.79, 0), (1.02, 0.075, 0.58), furniture, mats["M_WornWood"], 0.024),
        v2.new_box("V3Tmp_DeskApron", (0, 0.64, -0.25), (0.91, 0.22, 0.04), furniture, mats["M_DarkWood"], 0.009),
        v2.new_box("V3Tmp_DeskShelf", (0, 0.48, 0.00), (0.86, 0.035, 0.42), furniture, mats["M_DarkWood"], 0.008),
        v2.new_box("V3Tmp_ChairSeat", (0, 0.47, 0.61), (0.47, 0.055, 0.41), furniture, mats["M_WornWood"], 0.020),
        v2.new_box("V3Tmp_ChairBack", (0, 0.78, 0.82), (0.47, 0.40, 0.045), furniture, mats["M_WornWood"], 0.018),
    ]
    for x in (-0.42, 0.42):
        for z in (-0.22, 0.22):
            parts.append(v2.new_cylinder("V3Tmp_DeskTube", (x, 0.38, z), 0.022, 0.72, furniture, mats["M_DarkMetal"], 12))
            parts.append(v2.new_cylinder("V3Tmp_DeskFoot", (x, 0.035, z), 0.034, 0.06, furniture, mats["M_Rubber"], 12))
    for x in (-0.19, 0.19):
        for z in (0.49, 0.72):
            parts.append(v2.new_cylinder("V3Tmp_ChairTube", (x, 0.23, z), 0.019, 0.43, furniture, mats["M_DarkMetal"], 12))
            parts.append(v2.new_cylinder("V3Tmp_ChairFoot", (x, 0.025, z), 0.029, 0.045, furniture, mats["M_Rubber"], 12))
    parts.append(v2.new_box("V3Tmp_DeskCrossbar", (0, 0.27, 0.22), (0.82, 0.032, 0.032), furniture, mats["M_DarkMetal"], 0.006))
    parts.append(v2.new_box("V3Tmp_ChairCrossbar", (0, 0.26, 0.70), (0.40, 0.028, 0.028), furniture, mats["M_DarkMetal"], 0.005))
    for x in (-0.40, 0.40):
        for z in (-0.20, 0.20):
            parts.append(v2.new_cylinder("V3Tmp_DeskScrew", (x, 0.838, z), 0.012, 0.010, furniture, mats["M_DarkMetal"], 10))
    prototype = join_meshes(parts, "StudentDeskArt_01")
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.context.view_layer.objects.active = prototype; prototype.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR"); prototype.select_set(False)
    prototype.parent = roots[0]; prototype.location = (0, 0, 0)
    prototype.data["town_rpg_v3_shared_desk"] = True
    for index, root in enumerate(roots[1:], start=2):
        instance = bpy.data.objects.new(f"StudentDeskArt_{index:02d}", prototype.data)
        furniture.objects.link(instance); instance.parent = root; instance.location = (0, 0, 0)


def rebuild_blackboards(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    states, decals = collections["StateVariants"], collections["Decals"]
    for state, revealed in (("Blackboard_Blank", False), ("Blackboard_Revealed", True)):
        root = bpy.data.objects[state]; clear_children(root)
        surface = v2.new_box(f"{state}_Surface", (0, 1.82, -3.48), (4.70, 1.42, 0.075), states, mats["M_Blackboard"], 0.015, root)
        _ = surface
        frames = [
            v2.new_box("V3Tmp_BoardTop", (0, 2.56, -3.42), (4.88, 0.085, 0.10), states, mats["M_DarkWood"], 0.012),
            v2.new_box("V3Tmp_BoardBottom", (0, 1.08, -3.42), (4.88, 0.10, 0.13), states, mats["M_DarkWood"], 0.012),
            v2.new_box("V3Tmp_BoardLeft", (-2.40, 1.82, -3.42), (0.085, 1.56, 0.10), states, mats["M_DarkWood"], 0.012),
            v2.new_box("V3Tmp_BoardRight", (2.40, 1.82, -3.42), (0.085, 1.56, 0.10), states, mats["M_DarkWood"], 0.012),
            v2.new_box("V3Tmp_BoardTray", (0, 1.04, -3.30), (4.70, 0.055, 0.23), states, mats["M_DarkWood"], 0.009),
            v2.new_box("V3Tmp_BoardEraser", (1.25, 1.12, -3.23), (0.27, 0.065, 0.11), states, mats["M_Rubber"], 0.010),
        ]
        join_meshes(frames, f"V3_{state}_Frame", root)
        if revealed:
            v2.make_vertical_plane("Blackboard_Revealed_Writing", 0, 1.83, -3.385, 3.62, 0.76,
                                   decals, mats["M_DetailDecal"], (0, 0.5), (1, 1), root)


def rebuild_clock_and_roster(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    props = collections["Props"]
    clock = bpy.data.objects["Clock_Seven"]; clear_children(clock)
    clock_parts = [
        v2.new_cylinder("V3Tmp_ClockRim", (2.72, 2.20, -3.40), 0.43, 0.11, props, mats["M_DarkMetal"], 48),
        v2.new_cylinder("V3Tmp_ClockFace", (2.72, 2.20, -3.335), 0.37, 0.025, props, mats["M_AgedPaper"], 48),
    ]
    # The helper's default cylinder axis is logical height; rotate into the wall.
    for item in clock_parts:
        item.rotation_euler.x = math.pi / 2
    for index in range(12):
        angle = index / 12 * math.tau
        x = 2.72 + math.sin(angle) * 0.29
        y = 2.20 + math.cos(angle) * 0.29
        tick = v2.new_box("V3Tmp_ClockTick", (x, y, -3.305), (0.025, 0.075 if index % 3 == 0 else 0.045, 0.018), props, mats["M_DarkMetal"], 0.003)
        tick.rotation_euler.y = -angle
        clock_parts.append(tick)
    minute = v2.new_box("V3Tmp_ClockMinute", (2.72, 2.31, -3.285), (0.027, 0.22, 0.022), props, mats["M_DarkMetal"], 0.003)
    hour = v2.new_box("V3Tmp_ClockHour", (2.64, 2.13, -3.28), (0.026, 0.19, 0.022), props, mats["M_DarkMetal"], 0.003)
    hour.rotation_euler.y = math.radians(-45)
    clock_parts.extend((minute, hour))
    join_meshes(clock_parts, "V3_Clock_Seven_Detailed", clock)

    roster = bpy.data.objects["DutyRoster_Second"]; clear_children(roster)
    roster_parts = [v2.new_box("V3Tmp_RosterPaper", (3.66, 1.76, -3.43), (0.76, 1.04, 0.035), props, mats["M_AgedPaper"], 0.006)]
    for row in range(8):
        roster_parts.append(v2.new_box("V3Tmp_RosterLine", (3.66, 2.05 - row * 0.12, -3.398), (0.55, 0.012, 0.010), props, mats["M_DarkMetal"], 0.002))
    for column in (-0.16, 0.0, 0.16):
        roster_parts.append(v2.new_box("V3Tmp_RosterColumn", (3.66 + column, 1.74, -3.395), (0.010, 0.78, 0.010), props, mats["M_DarkMetal"], 0.002))
    circle = v2.new_cylinder("V3Tmp_RosterSecond", (3.48, 1.88, -3.365), 0.065, 0.018, props, mats["M_FadedRed"], 24)
    circle.rotation_euler.x = math.pi / 2; roster_parts.append(circle)
    join_meshes(roster_parts, "V3_DutyRoster_Second_Detailed", roster)


def add_wear(v2, collections: dict[str, bpy.types.Collection], mats: dict[str, bpy.types.Material]) -> None:
    remove_names(("V3_WallWear", "V3Tmp_Wear"))
    decals = collections["Decals"]
    patches = [
        v2.new_box("V3Tmp_Wear", (-3.05, 0.44, -3.495), (0.82, 0.24, 0.012), decals, mats["M_WallStain"], 0.018),
        v2.new_box("V3Tmp_Wear", (3.05, 1.30, -3.495), (0.55, 0.16, 0.012), decals, mats["M_WallStain"], 0.015),
        v2.new_box("V3Tmp_Wear", (-1.80, 0.36, 3.495), (1.10, 0.18, 0.012), decals, mats["M_WallStain"], 0.018),
        v2.new_box("V3Tmp_Wear", (4.695, 0.42, -1.15), (0.012, 0.22, 0.90), decals, mats["M_WallStain"], 0.016),
        v2.new_box("V3Tmp_Wear", (-4.695, 0.34, 2.05), (0.012, 0.17, 1.20), decals, mats["M_WallStain"], 0.016),
    ]
    join_meshes(patches, "V3_WallWear")


def rebuild_lookdev(collection: bpy.types.Collection) -> None:
    for obj in list(collection.objects):
        delete_object(obj)
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new("ClassroomV3LookdevWorld")
    scene.world = world; world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.020, 0.045, 0.060, 1)
    background.inputs["Strength"].default_value = 0.40
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920; scene.render.resolution_y = 1080; scene.render.resolution_percentage = 100
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 2.0

    def look_at(obj: bpy.types.Object, target: Vector) -> None:
        obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()

    target = Vector((0, 0, 1.0))
    camera_specs = {
        # A full four-wall runtime room cannot be photographed from outside
        # like a cutaway dollhouse. These interior calibration angles inspect
        # the same hierarchy without hiding any exported wall.
        # Pull the adopted southeast dollhouse view far enough back to retain
        # the east-door leaf and both near-wall footprints in frame.  This is
        # a composition-only camera and is never exported to Godot.
        "LookdevV3_Camera_SE": ((8.60, -8.60, 7.80), (0.0, 0.0, 1.0), 44),
        "LookdevV3_Camera_Window": ((-2.05, 0.55, 1.82), (-4.62, 1.10, 1.55), 43),
        "LookdevV3_Camera_MapCabinet": ((0.0, -0.80, 1.85), (-3.83, 2.73, 1.12), 50),
        "LookdevV3_Camera_Desks": ((3.05, -2.72, 1.62), (0.0, -0.25, 0.62), 43),
    }
    for name, (position, focus, lens) in camera_specs.items():
        camera_data = bpy.data.cameras.new(f"{name}_Data")
        camera_data.type = "PERSP"; camera_data.lens = lens
        camera = bpy.data.objects.new(name, camera_data); collection.objects.link(camera)
        camera.location = position; look_at(camera, Vector(focus))
        if name.endswith("_SE"):
            scene.camera = camera

    sun_data = bpy.data.lights.new("LookdevV3_Sun_Data", "SUN")
    sun_data.color = (1.0, 0.55, 0.28); sun_data.energy = 1.15; sun_data.angle = math.radians(4.0)
    sun = bpy.data.objects.new("LookdevV3_Sun", sun_data); collection.objects.link(sun)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-61))

    window_data = bpy.data.lights.new("LookdevV3_Window_Data", "AREA")
    window_data.color = (1.0, 0.58, 0.30); window_data.energy = 700; window_data.shape = "RECTANGLE"
    window_data.size = 2.8; window_data.size_y = 1.45
    window = bpy.data.objects.new("LookdevV3_Window", window_data); collection.objects.link(window)
    window.location = (-5.15, 1.10, 1.65); look_at(window, Vector((0, 0.4, 0.9)))

    fill_data = bpy.data.lights.new("LookdevV3_CoolFill_Data", "AREA")
    fill_data.color = (0.20, 0.42, 0.55); fill_data.energy = 300; fill_data.shape = "DISK"; fill_data.size = 4.5
    fill = bpy.data.objects.new("LookdevV3_CoolFill", fill_data); collection.objects.link(fill)
    fill.location = (2.4, -0.2, 5.6); look_at(fill, target)

    # A restrained cyan side fill keeps the glass upper cabinet, drawers and
    # brass pulls readable without flattening the room's dark original-art
    # value structure.  Like every Lookdev light, it is excluded from export.
    cabinet_fill_data = bpy.data.lights.new("LookdevV3_CabinetFill_Data", "AREA")
    cabinet_fill_data.color = (0.18, 0.42, 0.55)
    cabinet_fill_data.energy = 45
    cabinet_fill_data.shape = "RECTANGLE"
    cabinet_fill_data.size = 1.8
    cabinet_fill_data.size_y = 1.2
    cabinet_fill = bpy.data.objects.new("LookdevV3_CabinetFill", cabinet_fill_data)
    collection.objects.link(cabinet_fill)
    cabinet_fill.location = (-1.45, 1.10, 2.65)
    look_at(cabinet_fill, Vector((-3.83, 2.73, 1.15)))
    cabinet_fill.hide_render = True


def purge_and_pack() -> None:
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    bpy.ops.file.pack_all()


def build_scene(root: Path) -> None:
    source = root / V2_SOURCE
    if not source.is_file():
        raise RuntimeError(f"Missing v2 source Blend: {source}")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    v2 = load_v2_helpers(root)
    collections = {name: ensure_collection(name) for name in ("Shell", "Furniture", "Props", "Decals", "StateVariants", "Lookdev")}
    door_hinge_before = bpy.data.objects["Door_East_Hinge"].matrix_world.copy()
    desk_transforms = {name: bpy.data.objects[name].matrix_world.copy() for name in DESK_ROOTS}
    materials = rebuild_materials(root)
    rebuild_shell(v2, collections, materials)
    rebuild_window(v2, collections, materials)
    rebuild_map_cabinet(v2, collections, materials)
    rebuild_teacher_desk(v2, collections, materials)
    rebuild_desks(v2, collections, materials)
    rebuild_blackboards(v2, collections, materials)
    rebuild_clock_and_roster(v2, collections, materials)
    add_wear(v2, collections, materials)
    rebuild_lookdev(collections["Lookdev"])
    if not matrices_match(bpy.data.objects["Door_East_Hinge"].matrix_world, door_hinge_before):
        raise RuntimeError("V3 art build changed the east-door hinge transform")
    for name, transform in desk_transforms.items():
        if not matrices_match(bpy.data.objects[name].matrix_world, transform):
            raise RuntimeError(f"V3 art build changed stable desk transform: {name}")
    scene = bpy.context.scene
    scene["town_rpg_asset"] = "dream_classroom_art_pass_v3"
    scene["town_rpg_room_size"] = "9.6x7.2x3.2"
    scene["town_rpg_wainscot_height"] = 1.4
    scene["town_rpg_window_size"] = "3.0x1.55"
    scene["town_rpg_visual_anchor"] = "dark-original-mood+v2-photoreal-geometry"
    purge_and_pack()


def exported_objects() -> list[bpy.types.Object]:
    return [obj for obj in bpy.context.scene.objects
            if not any(collection.name == "Lookdev" for collection in obj.users_collection)]


def validate_scene(root: Path) -> dict[str, object]:
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    scene = bpy.context.scene
    if scene.get("town_rpg_asset") != "dream_classroom_art_pass_v3":
        raise RuntimeError("Blend is not tagged as classroom art pass v3")
    if float(scene.get("town_rpg_wainscot_height", 0)) != 1.4 or scene.get("town_rpg_window_size") != "3.0x1.55":
        raise RuntimeError("V3 architectural calibration tags are missing")
    names = set(bpy.data.objects.keys())
    missing = sorted(STABLE_ROOTS.union(DESK_ROOTS) - names)
    if missing:
        raise RuntimeError(f"Missing stable v3 objects: {', '.join(missing)}")
    desks = [bpy.data.objects[f"StudentDeskArt_{index:02d}"] for index in range(1, 11)]
    if len({desk.data.as_pointer() for desk in desks}) != 1:
        raise RuntimeError("V3 student desks do not share one mesh data-block")
    expected = [(x, -z, 0.0) for z in DESK_Z for x in DESK_X]
    for name, position in zip(DESK_ROOTS, expected):
        if (bpy.data.objects[name].location - Vector(position)).length > 0.0001:
            raise RuntimeError(f"Stable desk root moved: {name}")
    # Preserve the authored v2 node transform byte-for-byte.  Door children
    # carry their world offsets; the stable hinge root itself is at origin.
    hinge = bpy.data.objects["Door_East_Hinge"].matrix_world.translation
    if hinge.length > 0.001:
        raise RuntimeError(f"East door hinge moved: {tuple(hinge)}")
    meshes = [obj for obj in exported_objects() if obj.type == "MESH"]
    triangles = sum(sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons) for obj in meshes)
    if triangles > MAX_TRIANGLES:
        raise RuntimeError(f"V3 triangle budget exceeded: {triangles}")
    if len(meshes) > MAX_MESHES:
        raise RuntimeError(f"V3 mesh budget exceeded: {len(meshes)}")
    external = []
    for image in bpy.data.images:
        if image.users and image.source == "FILE" and image.packed_file is None:
            external.append(image.name)
    if external:
        raise RuntimeError(f"V3 Blend retains external image links: {', '.join(sorted(external))}")
    texture_manifest = json.loads((root / TEXTURE_MANIFEST).read_text(encoding="utf-8"))
    return {
        "blenderVersion": bpy.app.version_string,
        "roomSizeMeters": list(ROOM_SIZE),
        "stableObjects": sorted(STABLE_ROOTS),
        "studentDeskGroups": DESK_ROOTS,
        "meshObjectCount": len(meshes),
        "uniqueMeshCount": len({obj.data.as_pointer() for obj in meshes}),
        "triangleCount": triangles,
        "materialCount": len({material.name for obj in meshes for material in obj.data.materials if material}),
        "packedImageCount": sum(1 for image in bpy.data.images if image.users and image.packed_file is not None),
        "textureFiles": {name: item["sha256"] for name, item in texture_manifest["outputs"].items()},
        "wainscotHeightMeters": 1.4,
        "westWindowMeters": [3.0, 1.55],
        "lightingTarget": {"lookdevExposure": 2.0, "worldEnergy": 0.40, "sunEnergy": 1.15,
                           "windowAreaEnergy": 700, "coolFillEnergy": 300},
    }


def export_glb(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in exported_objects():
        obj.select_set(True)
    result = bpy.ops.export_scene.gltf(
        filepath=str(path), export_format="GLB", use_selection=True, export_apply=True,
        export_animations=False, export_cameras=False, export_lights=False,
        export_materials="EXPORT", export_texcoords=True, export_normals=True,
        export_tangents=False, export_yup=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"V3 glTF export failed: {result}")


def write_runtime(root: Path, source: Path) -> dict[str, object]:
    summary = validate_scene(root)
    glb = root / V3_GLB
    export_glb(glb)
    if glb.stat().st_size > MAX_GLB_BYTES:
        raise RuntimeError(f"V3 GLB exceeds 35 MB: {glb.stat().st_size}")
    summary.update({
        "schemaVersion": 1,
        "sourceAuthority": "blend",
        "sourceFile": V3_SOURCE.as_posix(),
        "seedFile": V2_SOURCE.as_posix(),
        "runtimeFile": V3_GLB.as_posix(),
        "textureManifest": TEXTURE_MANIFEST.as_posix(),
        "sourceSha256": sha256(source),
        "runtimeSha256": sha256(glb),
        "runtimeBytes": glb.stat().st_size,
        "limits": {"triangles": MAX_TRIANGLES, "visibleMeshes": MAX_MESHES, "runtimeBytes": MAX_GLB_BYTES},
        "exportProfile": {"format": "GLB", "cameras": False, "lights": False, "animations": False,
                          "appliedModifiers": True, "tangents": "generated-by-godot-import"},
    })
    (root / V3_MANIFEST).write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return summary


def build(root: Path) -> None:
    build_scene(root)
    source = root / V3_SOURCE
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    summary = write_runtime(root, source)
    print(f"CLASSROOM_ART_V3_BUILD_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} bytes={summary['runtimeBytes']}")


def export(root: Path) -> None:
    source = root / V3_SOURCE
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = write_runtime(root, source)
    print(f"CLASSROOM_ART_V3_EXPORT_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} bytes={summary['runtimeBytes']}")


def check(root: Path) -> None:
    source, glb, manifest_path = root / V3_SOURCE, root / V3_GLB, root / V3_MANIFEST
    if not source.is_file() or not glb.is_file() or not manifest_path.is_file():
        raise RuntimeError("V3 Blend, GLB, or manifest is missing")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    summary = validate_scene(root)
    if manifest.get("sourceSha256") != sha256(source) or manifest.get("runtimeSha256") != sha256(glb):
        raise RuntimeError("V3 tracked binary hash does not match manifest")
    for key in ("stableObjects", "studentDeskGroups", "meshObjectCount", "triangleCount", "textureFiles", "packedImageCount"):
        if manifest.get(key) != summary[key]:
            raise RuntimeError(f"V3 manifest field is stale: {key}")
    if glb.stat().st_size > MAX_GLB_BYTES:
        raise RuntimeError("V3 GLB exceeds 35 MB")
    with tempfile.TemporaryDirectory(prefix="town-rpg-classroom-v3-") as temporary:
        candidate = Path(temporary) / "classroom-art-pass-v3.glb"
        export_glb(candidate)
        if sha256(candidate) != sha256(glb):
            raise RuntimeError("Fresh v3 GLB export is not deterministic")
    print(f"CLASSROOM_ART_V3_CHECK_PASS meshes={summary['meshObjectCount']} triangles={summary['triangleCount']} bytes={glb.stat().st_size}")


def capture(root: Path) -> None:
    bpy.ops.wm.open_mainfile(filepath=str(root / V3_SOURCE), load_ui=False)
    validate_scene(root)
    output = root / CAPTURE
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.image_settings.file_format = "PNG"
    captures = {
        "LookdevV3_Camera_SE": "lookdev-southeast.png",
        "LookdevV3_Camera_Window": "closeup-west-window.png",
        "LookdevV3_Camera_MapCabinet": "closeup-map-cabinet.png",
        "LookdevV3_Camera_Desks": "closeup-student-desks.png",
    }
    for camera_name, filename in captures.items():
        # Lookdev alone emulates the adopted roofless southeast dollhouse by
        # hiding the two near visual wall surfaces. Runtime/export visibility
        # remains untouched, and all closeups use the complete four-wall room.
        cutaway = camera_name == "LookdevV3_Camera_SE"
        for wall_name in ("V3_WallSurface_South", "V3_WallSurface_East"):
            bpy.data.objects[wall_name].hide_render = cutaway
        bpy.data.objects["Blackboard_Blank"].hide_render = True
        bpy.data.objects["Blackboard_Revealed"].hide_render = False
        bpy.data.objects["TeacherDesk_Closed"].hide_render = False
        bpy.data.objects["TeacherDesk_Open"].hide_render = True
        show_open_cabinet = camera_name == "LookdevV3_Camera_MapCabinet"
        bpy.data.objects["LookdevV3_CabinetFill"].hide_render = not show_open_cabinet
        bpy.data.objects["MapCabinet_Closed"].hide_render = show_open_cabinet
        bpy.data.objects["MapCabinet_Open"].hide_render = not show_open_cabinet
        bpy.context.scene.camera = bpy.data.objects[camera_name]
        bpy.context.scene.render.filepath = str(output.parent / filename)
        bpy.ops.render.render(write_still=True)
    for wall_name in ("V3_WallSurface_South", "V3_WallSurface_East"):
        bpy.data.objects[wall_name].hide_render = False
    print(f"CLASSROOM_ART_V3_CAPTURE_PASS files={len(captures)} directory={CAPTURE.parent.as_posix()}")


if __name__ == "__main__":
    args = parse_args()
    root = Path(args.root).resolve()
    {"v3-build": build, "art-export": export, "art-check": check, "capture": capture}[args.mode](root)
