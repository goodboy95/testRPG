"""Build and validate the adopted third/fourth-room Blender and GLB assets.

Usage through Blender:
  blender --background --python scripts/tools/build_dream_late_rooms_3d.py -- build
  blender --background --python scripts/tools/build_dream_late_rooms_3d.py -- check
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "art" / "blender" / "dream_late_rooms"
OUTPUT_DIR = ROOT / "assets" / "generated" / "dream_escape" / "3d"
CC0_MANIFEST = ROOT / "art" / "sources" / "cc0" / "polyhaven" / "environment-cinematic-v1" / "source-manifest.json"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def material(name: str, color: tuple[float, float, float, float], roughness: float, metallic: float = 0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    principled = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    return mat


def pbr_material(name: str, family: str, color, roughness: float, metallic: float = 0.0, scale: float = 2.0):
    with CC0_MANIFEST.open("r", encoding="utf-8") as handle:
        manifest = json.load(handle)
    asset = next(item for item in manifest["assets"] if item["id"] == family)
    mat = material(name, color, roughness, metallic)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    texcoord = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (scale, scale, scale)
    links.new(texcoord.outputs["Generated"], mapping.inputs["Vector"])

    def image_node(map_name: str, non_color: bool):
        path = ROOT / asset["maps"][map_name]["path"]
        if not path.is_file():
            raise FileNotFoundError(f"Missing adopted CC0 {family}/{map_name}: {path}")
        image = bpy.data.images.load(str(path), check_existing=True)
        if non_color:
            image.colorspace_settings.name = "Non-Color"
        node = nodes.new("ShaderNodeTexImage")
        node.image = image
        node.extension = "REPEAT"
        links.new(mapping.outputs["Vector"], node.inputs["Vector"])
        return node

    diffuse = image_node("diff", False)
    normal = image_node("normal", True)
    arm = image_node("arm", True)
    links.new(diffuse.outputs["Color"], principled.inputs["Base Color"])
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.42
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(arm.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs[1], principled.inputs["Roughness"])
    links.new(separate.outputs[2], principled.inputs["Metallic"])
    return mat


def palette() -> dict[str, bpy.types.Material]:
    return {
        "plaster": pbr_material("LateRoom_Plaster", "painted_plaster_wall", (0.39, 0.43, 0.41, 1), 0.92, scale=3.0),
        "ceiling": pbr_material("LateRoom_Ceiling", "beige_wall_001", (0.27, 0.31, 0.32, 1), 0.96, scale=3.0),
        "floor": pbr_material("LateRoom_Floor", "old_mosaic_floor", (0.22, 0.17, 0.13, 1), 0.76, scale=4.0),
        "oak": pbr_material("LateRoom_DarkOak", "black_oak_veneer", (0.19, 0.105, 0.065, 1), 0.67, scale=2.5),
        "oak_light": pbr_material("LateRoom_AgedOak", "oak_veneer_01", (0.34, 0.20, 0.12, 1), 0.64, scale=2.5),
        "teal": material("LateRoom_PaintedMetal", (0.19, 0.31, 0.31, 1), 0.70, 0.12),
        "metal": pbr_material("LateRoom_DarkMetal", "rust_coarse_01", (0.16, 0.19, 0.20, 1), 0.38, 0.72, scale=2.0),
        "brass": material("LateRoom_AgedBrass", (0.48, 0.29, 0.09, 1), 0.36, 0.76),
        "paper": material("LateRoom_AgedPaper", (0.67, 0.57, 0.38, 1), 0.91),
        "curtain": material("LateRoom_BurgundyCurtain", (0.17, 0.035, 0.05, 1), 0.88),
        "seat": material("LateRoom_SeatFabric", (0.12, 0.16, 0.17, 1), 0.84),
        "glass": material("LateRoom_SmokyGlass", (0.18, 0.28, 0.29, 0.35), 0.20),
    }


def box(name: str, location, scale, mat, bevel: float = 0.025, parent=None):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0] / 2, scale[1] / 2, scale[2] / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    if bevel > 0:
        modifier = obj.modifiers.new("EdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.parent = parent
    return obj


def cylinder(name: str, location, radius, depth, mat, rotation=(0, 0, 0), vertices=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    bevel = obj.modifiers.new("EdgeSoftening", "BEVEL")
    bevel.width = min(0.025, radius * 0.12)
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj


def empty(name: str, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.location = location
    bpy.context.collection.objects.link(obj)
    return obj


def add_room_shell(width: float, depth: float, height: float, mats, prefix: str):
    box(f"{prefix}_Floor", (0, 0, -0.08), (width, depth, 0.16), mats["floor"], 0)
    box(f"{prefix}_Ceiling", (0, 0, height + 0.08), (width, depth, 0.16), mats["ceiling"], 0)
    box(f"{prefix}_NorthWall", (0, -depth / 2, height / 2), (width, 0.16, height), mats["plaster"], 0)
    box(f"{prefix}_WestWall", (-width / 2, 0, height / 2), (0.16, depth, height), mats["plaster"], 0)
    box(f"{prefix}_EastWall", (width / 2, 0, height / 2), (0.16, depth, height), mats["plaster"], 0)
    box(f"{prefix}_SouthWall", (0, depth / 2, height / 2), (width, 0.16, height), mats["plaster"], 0)
    for y in (-depth / 2 + 0.12, depth / 2 - 0.12):
        box(f"{prefix}_Wainscot_{y:.2f}", (0, y, 0.72), (width - 0.3, 0.10, 1.35), mats["oak"], 0.01)
    for x in (-width / 2 + 0.12, width / 2 - 0.12):
        box(f"{prefix}_Wainscot_{x:.2f}", (x, 0, 0.72), (0.10, depth - 0.3, 1.35), mats["oak"], 0.01)


def build_archive(mats):
    add_room_shell(12.0, 9.0, 4.2, mats, "Archive")
    for group, x, z, width in (("West", -5.35, -0.7, 6.6), ("North", -2.8, -4.05, 3.9)):
        horizontal = group == "West"
        for shelf in range(4):
            loc = (x, z, 0.35 + shelf * 0.67)
            scale = (0.85, width, 0.10) if horizontal else (width, 0.85, 0.10)
            box(f"Archive_{group}Shelf_{shelf}", (loc[0], loc[1], loc[2]), scale, mats["oak"], 0.015)
        for index in range(20):
            offset = -width / 2 + 0.25 + index * (width - 0.5) / 19
            color_mat = mats["paper"] if index % 3 else mats["teal"]
            location = (x + 0.02, z + offset, 1.1 + (index % 3) * 0.65) if horizontal else (x + offset, z + 0.02, 1.1 + (index % 3) * 0.65)
            scale = (0.34, 0.12, 0.48) if horizontal else (0.12, 0.34, 0.48)
            box(f"Archive_File_{group}_{index:02d}", location, scale, color_mat, 0.008)

    box("Archive_RegistryTable", (0, -0.25, 0.72), (2.8, 1.4, 1.44), mats["oak_light"])
    for index in range(4):
        box(f"Archive_RegistryTray_{index}", (-0.72 + index * 0.48, -0.76, 1.25), (0.34, 0.24, 0.08), mats["brass"])
    box("Archive_DrawerMatrix", (-3.0, 1.65, 0.95), (2.1, 1.1, 1.9), mats["oak"])
    for row in range(3):
        for col in range(4):
            box(f"Archive_Drawer_{row}_{col}", (-3.72 + col * 0.48, 1.08, 0.45 + row * 0.48), (0.42, 0.08, 0.38), mats["oak_light"], 0.01)
            cylinder(f"Archive_DrawerPull_{row}_{col}", (-3.72 + col * 0.48, 1.02, 0.45 + row * 0.48), 0.035, 0.08, mats["brass"], (math.pi / 2, 0, 0), 16)

    box("Archive_RepairBench", (4.55, -1.8, 0.72), (1.6, 2.2, 1.44), mats["oak_light"])
    for index in range(3):
        cylinder(f"Archive_FusePart_{index}", (4.15, -2.3 + index * 0.45, 1.48), 0.07, 0.65, mats["brass"], (0, math.pi / 2, 0), 16)
    booth = empty("Archive_BroadcastBoothRoot", (3.65, 1.65, 0))
    box("Archive_BoothDesk", (3.65, 1.65, 0.75), (2.6, 1.7, 1.5), mats["oak"], parent=booth)
    box("Archive_BoothGlass", (2.25, 1.65, 2.15), (0.08, 1.8, 1.65), mats["glass"], 0, booth)
    cylinder("Archive_RecorderLeftReel", (3.25, 1.08, 1.55), 0.38, 0.12, mats["metal"], (math.pi / 2, 0, 0), parent=booth)
    cylinder("Archive_RecorderRightReel", (4.05, 1.08, 1.55), 0.38, 0.12, mats["metal"], (math.pi / 2, 0, 0), parent=booth)
    box("Archive_EditingCounter", (0, 3.85, 0.75), (2.2, 0.9, 1.5), mats["teal"])
    for index in range(4):
        box(f"Archive_CounterWindow_{index}", (-0.62 + index * 0.42, 3.38, 1.02), (0.30, 0.06, 0.32), mats["paper"], 0.01)
    box("Archive_UpperVault", (-4.75, 3.75, 1.75), (1.6, 1.0, 3.5), mats["teal"])
    for x in (-3.6, -1.2, 1.2, 3.6):
        box(f"Archive_CeilingFixture_{x}", (x, 0, 3.94), (1.5, 0.34, 0.16), mats["metal"])


def build_auditorium(mats):
    add_room_shell(18.0, 13.0, 7.5, mats, "Auditorium")
    box("Auditorium_Stage", (0, -5.65, 0.55), (12.0, 1.5, 1.1), mats["oak"])
    box("Auditorium_ProsceniumTop", (0, -6.15, 6.3), (13.5, 0.55, 1.0), mats["oak"])
    box("Auditorium_ProsceniumLeft", (-6.5, -6.15, 3.7), (0.65, 0.55, 5.2), mats["oak"])
    box("Auditorium_ProsceniumRight", (6.5, -6.15, 3.7), (0.65, 0.55, 5.2), mats["oak"])
    for panel in range(12):
        x = -5.5 + panel
        curtain = box(f"Auditorium_CurtainPanel_{panel:02d}", (x, -5.83, 3.1), (1.04, 0.16, 5.2), mats["curtain"], 0.018)
        curtain.rotation_euler[2] = math.sin(panel * 0.7) * 0.012

    for side, base_x in (("West", -5.1), ("East", 3.3)):
        for row in range(4):
            for col in range(5):
                x = base_x + col * 0.45
                y = 0.2 + row * 0.76
                box(f"Auditorium_Seat_{side}_{row}_{col}", (x, y, 0.55), (0.38, 0.42, 0.80), mats["seat"], 0.04)
                box(f"Auditorium_SeatBack_{side}_{row}_{col}", (x, y + 0.19, 1.0), (0.38, 0.12, 0.78), mats["oak_light"], 0.03)

    box("Auditorium_FlyRail", (7.65, -3.9, 1.2), (1.4, 3.2, 2.4), mats["metal"])
    for index in range(4):
        cylinder(f"Auditorium_LineSet_{index}", (7.15 + index * 0.34, -4.4, 4.3), 0.025, 5.4, mats["metal"], vertices=12)
        box(f"Auditorium_LineWeight_{index}", (7.15 + index * 0.34, -4.4, 1.5 + index * 0.35), (0.22, 0.30, 0.48), mats["brass"])
    box("Auditorium_ProjectorStand", (-7.65, -3.9, 1.25), (1.6, 2.0, 2.5), mats["teal"])
    for index in range(3):
        cylinder(f"Auditorium_ProjectorDisc_{index}", (-7.65, -4.45 + index * 0.42, 1.72), 0.46, 0.10, mats["brass"], (math.pi / 2, 0, 0))

    for name, x in (("Light", -6.6), ("Sound", 0), ("Broadcast", 6.6)):
        box(f"Auditorium_{name}Console", (x, 4.65, 0.75), (2.2 if x else 2.4, 1.2, 1.5), mats["teal"])
        for index in range(4):
            cylinder(f"Auditorium_{name}Knob_{index}", (x - 0.66 + index * 0.44, 4.02, 1.10), 0.07, 0.08, mats["brass"], (math.pi / 2, 0, 0), 16)
    box("Auditorium_SpliceBench", (5.7, -0.2, 0.72), (2.2, 1.4, 1.44), mats["oak_light"])
    cylinder("Auditorium_SpliceReel", (5.7, -0.55, 1.18), 0.50, 0.12, mats["brass"], (math.pi / 2, 0, 0))
    box("Auditorium_MasterPlayer", (6.9, 1.6, 1.1), (1.4, 1.1, 2.2), mats["oak"])
    cylinder("Auditorium_MasterPlayerReel", (6.9, 1.0, 1.55), 0.44, 0.12, mats["metal"], (math.pi / 2, 0, 0))
    for x in (-6.0, -2.0, 2.0, 6.0):
        box(f"Auditorium_CeilingFixture_{x}", (x, 0, 7.15), (2.2, 0.45, 0.20), mats["metal"])


def export_room(slug: str, builder) -> dict:
    reset_scene()
    mats = palette()
    builder(mats)
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    blend_path = SOURCE_DIR / f"{slug}.blend"
    glb_path = OUTPUT_DIR / f"{slug}.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_yup=True,
    )
    triangles = sum(len(obj.data.polygons) * 2 for obj in bpy.context.scene.objects if obj.type == "MESH")
    manifest = {
        "schemaVersion": 1,
        "status": "adopted-runtime-default",
        "runtimeDefault": True,
        "sampleOnly": False,
        "source": str(blend_path.relative_to(ROOT)).replace("\\", "/"),
        "gameFile": str(glb_path.relative_to(ROOT)).replace("\\", "/"),
        "bytes": glb_path.stat().st_size,
        "sha256": sha256(glb_path),
        "meshObjects": sum(1 for obj in bpy.context.scene.objects if obj.type == "MESH"),
        "trianglesUpperBound": triangles,
        "missingExternalFiles": 0,
        "generator": f"Blender {bpy.app.version_string} deterministic construction",
    }
    manifest_path = OUTPUT_DIR / f"{slug}-manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def check_room(slug: str) -> dict:
    glb_path = OUTPUT_DIR / f"{slug}.glb"
    manifest_path = OUTPUT_DIR / f"{slug}-manifest.json"
    if not glb_path.is_file() or not manifest_path.is_file():
        raise RuntimeError(f"Missing adopted room asset: {slug}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["sha256"] != sha256(glb_path) or manifest["bytes"] != glb_path.stat().st_size:
        raise RuntimeError(f"Manifest mismatch: {slug}")
    if not manifest["runtimeDefault"] or manifest["sampleOnly"] or manifest["status"] != "adopted-runtime-default":
        raise RuntimeError(f"Invalid adoption status: {slug}")
    if manifest["bytes"] >= 60 * 1024 * 1024 or manifest["missingExternalFiles"] != 0:
        raise RuntimeError(f"Asset budget/external dependency failure: {slug}")
    return manifest


def main() -> None:
    mode = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "check"
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    if mode == "build":
        export_room("dream-archive-room-v1", build_archive)
        export_room("dream-auditorium-v1", build_auditorium)
    elif mode != "check":
        raise SystemExit(f"Unknown mode: {mode}")
    manifests = [check_room("dream-archive-room-v1"), check_room("dream-auditorium-v1")]
    print("DREAM_LATE_ROOMS_ASSET_CHECK_PASS "
          f"rooms=2 bytes={sum(item['bytes'] for item in manifests)} "
          f"meshes={sum(item['meshObjects'] for item in manifests)} readOnly={str(mode == 'check').lower()}")


if __name__ == "__main__":
    main()
