"""Add an idempotent full-scene cinematic detail layer to the approved samples.

This script deliberately does not rebuild either environment.  It opens the
hand-authored v5/v2 Blend, replaces only the generated full-detail collection,
repairs reusable instance/pivot data, and saves the authoritative source.

Blender 5.2 usage:

    blender --background --python-exit-code 1 \
      --python scripts/tools/expand_environment_cinematic_full.py -- classroom-expand --root .
    blender --background --python-exit-code 1 \
      --python scripts/tools/expand_environment_cinematic_full.py -- manor-check --root .
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import math
from pathlib import Path
import sys

import bpy
from mathutils import Euler, Matrix, Vector


BLENDER_VERSION = (5, 2, 0)
SOURCES = {
    "classroom": Path("art/blender/dream_classroom/classroom-art-pass-v5.blend"),
    "manor": Path("art/blender/dream_manor/dream-manor-hall-v2.blend"),
}
COLLECTION_NAME = "CinematicFullDetails"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("classroom-expand", "classroom-check", "manor-expand", "manor-check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args(raw)


def required_object(name: str) -> bpy.types.Object:
    result = bpy.data.objects.get(name)
    if result is None:
        raise RuntimeError(f"Required environment object is missing: {name}")
    return result


def required_material(name: str) -> bpy.types.Material:
    result = bpy.data.materials.get(name)
    if result is None:
        raise RuntimeError(f"Required environment material is missing: {name}")
    return result


def reset_collection() -> bpy.types.Collection:
    old = bpy.data.collections.get(COLLECTION_NAME)
    if old is not None:
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(old)
    collection = bpy.data.collections.new(COLLECTION_NAME)
    bpy.context.scene.collection.children.link(collection)
    return collection


def delete_prefixes(prefixes: tuple[str, ...]) -> None:
    for obj in list(bpy.data.objects):
        if obj.name.startswith(prefixes):
            bpy.data.objects.remove(obj, do_unlink=True)


@dataclass
class MeshAccumulator:
    vertices: list[tuple[float, float, float]] = field(default_factory=list)
    faces: list[tuple[int, ...]] = field(default_factory=list)

    def _append(self, vertices: list[Vector], faces: list[tuple[int, ...]]) -> None:
        offset = len(self.vertices)
        self.vertices.extend(tuple(value) for value in vertices)
        self.faces.extend(tuple(offset + index for index in face) for face in faces)

    def add_box(self, center: tuple[float, float, float], size: tuple[float, float, float],
                rotation: tuple[float, float, float] = (0.0, 0.0, 0.0)) -> None:
        cx, cy, cz = center
        sx, sy, sz = (value / 2.0 for value in size)
        matrix = Matrix.Translation(Vector((cx, cy, cz))) @ Euler(rotation).to_matrix().to_4x4()
        vertices = [
            matrix @ Vector((x, y, z))
            for x, y, z in ((-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
                            (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz))
        ]
        self._append(vertices, [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
                                (1, 5, 6, 2), (2, 6, 7, 3), (4, 0, 3, 7)])

    def add_cylinder(self, center: tuple[float, float, float], radius: float, depth: float,
                     axis: tuple[float, float, float] = (0.0, 0.0, 1.0), segments: int = 12) -> None:
        direction = Vector(axis).normalized()
        helper = Vector((0.0, 0.0, 1.0)) if abs(direction.z) < 0.9 else Vector((1.0, 0.0, 0.0))
        u = direction.cross(helper).normalized()
        v = direction.cross(u).normalized()
        middle = Vector(center)
        vertices = []
        for side in (-1.0, 1.0):
            cap = middle + direction * (depth * 0.5 * side)
            for index in range(segments):
                angle = math.tau * index / segments
                vertices.append(cap + radius * (math.cos(angle) * u + math.sin(angle) * v))
        faces: list[tuple[int, ...]] = [tuple(reversed(range(segments))), tuple(range(segments, segments * 2))]
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((index, nxt, segments + nxt, segments + index))
        self._append(vertices, faces)

    def add_rod(self, start: tuple[float, float, float], end: tuple[float, float, float],
                radius: float, segments: int = 10) -> None:
        a, b = Vector(start), Vector(end)
        delta = b - a
        self.add_cylinder(tuple((a + b) * 0.5), radius, delta.length, tuple(delta), segments)

    def add_torus(self, center: tuple[float, float, float], major_radius: float, minor_radius: float,
                  normal: tuple[float, float, float], major_segments: int = 18, minor_segments: int = 6) -> None:
        n = Vector(normal).normalized()
        helper = Vector((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else Vector((1.0, 0.0, 0.0))
        u = n.cross(helper).normalized()
        v = n.cross(u).normalized()
        middle = Vector(center)
        vertices: list[Vector] = []
        for major_index in range(major_segments):
            angle = math.tau * major_index / major_segments
            radial = math.cos(angle) * u + math.sin(angle) * v
            for minor_index in range(minor_segments):
                minor_angle = math.tau * minor_index / minor_segments
                vertices.append(middle + radial * (major_radius + minor_radius * math.cos(minor_angle))
                                + n * (minor_radius * math.sin(minor_angle)))
        faces = []
        for major_index in range(major_segments):
            next_major = (major_index + 1) % major_segments
            for minor_index in range(minor_segments):
                next_minor = (minor_index + 1) % minor_segments
                faces.append((major_index * minor_segments + minor_index,
                              next_major * minor_segments + minor_index,
                              next_major * minor_segments + next_minor,
                              major_index * minor_segments + next_minor))
        self._append(vertices, faces)

    def finish(self, name: str, material: bpy.types.Material, collection: bpy.types.Collection,
               parent: bpy.types.Object | None = None, preserve_world: bool = True,
               bevel: float = 0.008) -> bpy.types.Object:
        if not self.vertices:
            raise RuntimeError(f"Cannot create empty generated detail mesh: {name}")
        mesh = bpy.data.meshes.new(f"{name}_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.materials.append(material)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        if parent is not None:
            obj.parent = parent
            obj.matrix_parent_inverse = parent.matrix_world.inverted() if preserve_world else Matrix.Identity(4)
        if bevel > 0.0:
            modifier = obj.modifiers.new(name="EdgeSoftening", type="BEVEL")
            modifier.width = bevel
            modifier.segments = 2
            modifier.limit_method = "ANGLE"
        return obj


def share_student_desk_meshes() -> int:
    reference = required_object("StudentDeskArt_01")
    linked = 0
    for index in range(2, 19):
        obj = required_object(f"StudentDeskArt_{index:02d}")
        if obj.data != reference.data:
            obj.data = reference.data
            linked += 1
    return linked


def add_classroom_details(collection: bpy.types.Collection) -> None:
    delete_prefixes(("V5_Desk08_", "V5_Desk09_", "V5_Desk14_", "V5_Desk15_",
                     "V5_TeacherDrawer_", "V5_TeacherHandle_", "V5_TeacherLockWheel_"))
    shared = share_student_desk_meshes()

    metal = required_material("M_Cinematic_Classroom_Metal")
    wood = required_material("M_Cinematic_Classroom_Wood")
    darkwood = required_material("M_Cinematic_Classroom_Darkwood")
    brass = required_material("M_Cinematic_AgedBrass")
    paper = required_material("M_Cinematic_AgedPaper")
    fabric = required_material("M_FadedRed")

    desk_metal, desk_paper, desk_wood = MeshAccumulator(), MeshAccumulator(), MeshAccumulator()
    desk_positions = [(x, y) for y in (0.40, -1.45, -3.05) for x in (-4.25, -2.55, -0.85, 0.85, 2.55, 4.25)]
    for index, (x, y) in enumerate(desk_positions, start=1):
        for dx in (-0.40, 0.40):
            for dy in (-0.25, 0.25):
                desk_metal.add_cylinder((x + dx, y + dy, 0.542), 0.013, 0.014, segments=10)
        for dx in (-0.31, 0.31):
            desk_metal.add_rod((x + dx, y - 0.24, 0.37), (x + dx, y + 0.24, 0.37), 0.009)
        for dy in (-0.22, 0.0, 0.22):
            desk_metal.add_rod((x - 0.31, y + dy, 0.37), (x + 0.31, y + dy, 0.37), 0.009)
        for dx in (-0.38, 0.38):
            for dy in (-0.23, 0.23):
                desk_metal.add_box((x + dx, y + dy, 0.31), (0.07, 0.05, 0.035))
        if index in (4, 7, 10, 13, 16, 18):
            offset = -0.13 if index % 2 else 0.12
            desk_paper.add_box((x + offset, y + 0.02, 0.557), (0.36, 0.25, 0.018), (0.0, 0.0, 0.04 * (index % 3 - 1)))
            desk_wood.add_rod((x - 0.12, y - 0.17, 0.575), (x + 0.18, y - 0.10, 0.575), 0.008, 8)
    desk_metal.finish("FULL_Classroom_DeskHardware", metal, collection, bevel=0.003)
    desk_paper.finish("FULL_Classroom_DeskPapers", paper, collection, bevel=0.003)
    desk_wood.finish("FULL_Classroom_DeskPencils", wood, collection, bevel=0.003)

    window_wood, window_brass, curtain_detail = MeshAccumulator(), MeshAccumulator(), MeshAccumulator()
    for y in (-0.05, 1.85):
        window_wood.add_box((-5.67, y, 1.77), (0.07, 0.09, 1.84))
    for center_y in (-1.0, 0.9, 2.8):
        window_wood.add_box((-5.665, center_y, 1.77), (0.07, 1.72, 0.075))
        window_brass.add_box((-5.61, center_y + 0.45, 1.72), (0.055, 0.13, 0.20))
        window_brass.add_rod((-5.57, center_y + 0.45, 1.68), (-5.57, center_y + 0.65, 1.68), 0.018, 10)
    for edge in (-1.76, 3.56):
        for offset in (-0.12, -0.04, 0.04, 0.12):
            curtain_detail.add_rod((-5.65, edge + offset, 0.88), (-5.65, edge + offset, 2.70), 0.012, 8)
    window_wood.finish("FULL_Classroom_WindowJoinery", darkwood, collection, parent=required_object("Window_West"), bevel=0.006)
    window_brass.finish("FULL_Classroom_WindowHardware", brass, collection, parent=required_object("Window_West"), bevel=0.004)
    curtain_detail.finish("FULL_Classroom_CurtainFolds", fabric, collection, parent=required_object("Window_West"), bevel=0.003)

    wall_wood, wall_metal, wall_paper = MeshAccumulator(), MeshAccumulator(), MeshAccumulator()
    for x in (2.38, 3.55, 4.72):
        wall_wood.add_box((x, -4.29, 2.29), (1.04, 0.075, 0.055))
        wall_wood.add_box((x, -4.29, 0.95), (1.04, 0.075, 0.055))
        wall_wood.add_box((x - 0.50, -4.29, 1.62), (0.055, 0.075, 1.39))
        wall_wood.add_box((x + 0.50, -4.29, 1.62), (0.055, 0.075, 1.39))
    for row in range(4):
        for column in range(7):
            wall_metal.add_cylinder((-4.75 + (column - 3) * 0.055, -4.245,
                                     3.05 + (row - 1.5) * 0.055), 0.010, 0.018, (0, 1, 0), 8)
    wall_metal.add_box((5.73, -0.65, 1.42), (0.05, 0.32, 0.44))
    for y in (-0.77, -0.53):
        wall_metal.add_box((5.69, y, 1.42), (0.035, 0.05, 0.16))
    for index, (x, z, width, height) in enumerate(((2.25, 1.78, 0.72, 0.44), (3.55, 1.62, 0.62, 0.78),
                                                   (4.72, 1.86, 0.70, 0.46))):
        wall_paper.add_box((x, -4.235, z), (width, 0.018, height), (0, 0, 0.015 * (index - 1)))
    wall_wood.finish("FULL_Classroom_RearWallJoinery", darkwood, collection, bevel=0.006)
    wall_metal.finish("FULL_Classroom_RearWallHardware", metal, collection, bevel=0.003)
    wall_paper.finish("FULL_Classroom_RearWallNotices", paper, collection, bevel=0.002)

    door = required_object("Door_East_Hinge")
    door_wood, door_brass = MeshAccumulator(), MeshAccumulator()
    for center_z, height in ((0.60, 0.72), (1.55, 0.72)):
        for y in (0.16, 0.86):
            door_wood.add_box((-0.095, y, center_z), (0.035, 0.055, height))
        for z in (center_z - height / 2, center_z + height / 2):
            door_wood.add_box((-0.095, 0.51, z), (0.035, 0.75, 0.055))
    door_brass.add_box((-0.101, 0.51, 0.22), (0.026, 0.72, 0.26))
    door_brass.add_box((-0.115, 0.84, 1.03), (0.035, 0.20, 0.34))
    for z in (0.34, 1.12, 2.02):
        door_brass.add_cylinder((-0.105, 0.04, z), 0.026, 0.18, (0, 0, 1), 10)
    door_wood.finish("FULL_Classroom_DoorMoulding", darkwood, collection, parent=door,
                     preserve_world=False, bevel=0.004)
    door_brass.finish("FULL_Classroom_DoorHardware", brass, collection, parent=door,
                      preserve_world=False, bevel=0.004)

    for state_name in ("MapCabinet_Closed", "MapCabinet_Open"):
        cabinet_wood, cabinet_brass = MeshAccumulator(), MeshAccumulator()
        for x in (-5.48, -4.62):
            cabinet_wood.add_box((x, 3.295, 1.62), (0.045, 0.04, 1.18))
        for z in (1.05, 2.19):
            cabinet_wood.add_box((-5.05, 3.295, z), (0.90, 0.04, 0.045))
        for z in (0.25, 0.51, 0.77):
            cabinet_brass.add_box((-5.05, 3.285, z), (0.36, 0.025, 0.075))
            cabinet_brass.add_box((-5.37, 3.285, z), (0.16, 0.026, 0.09))
        cabinet_brass.add_torus((-4.58, 3.275, 1.28), 0.085, 0.018, (0, 1, 0), 16, 6)
        parent = required_object(state_name)
        cabinet_wood.finish(f"FULL_{state_name}_Joinery", darkwood, collection, parent=parent, bevel=0.004)
        cabinet_brass.finish(f"FULL_{state_name}_Hardware", brass, collection, parent=parent, bevel=0.003)

    for state_name, front_y in (("TeacherDesk_Closed", 2.825), ("TeacherDesk_Open", 2.505)):
        teacher_wood, teacher_brass = MeshAccumulator(), MeshAccumulator()
        for x in (-0.04, 0.39, 0.82):
            for z in (0.41, 0.65):
                teacher_wood.add_box((x, front_y, z), (0.36, 0.035, 0.17))
                teacher_brass.add_box((x, front_y - 0.025, z), (0.13, 0.025, 0.028))
        for x in (0.44, 0.55, 0.66):
            teacher_brass.add_cylinder((x, front_y - 0.036, 0.69), 0.052, 0.032, (0, 1, 0), 16)
        parent = required_object(state_name)
        teacher_wood.finish(f"FULL_{state_name}_DrawerFaces", wood, collection, parent=parent, bevel=0.004)
        teacher_brass.finish(f"FULL_{state_name}_Lockset", brass, collection, parent=parent, bevel=0.003)

    ceiling_metal, floor_wear = MeshAccumulator(), MeshAccumulator()
    for x in (-3.70, -1.25, 1.25, 3.70):
        ceiling_metal.add_box((x, 2.40, 3.87), (1.72, 0.25, 0.07))
        for dx in (-0.68, 0.68):
            ceiling_metal.add_rod((x + dx, 2.40, 3.90), (x + dx, 2.40, 4.00), 0.012, 8)
    for x in (-2.0, 2.0):
        ceiling_metal.add_cylinder((x, -0.60, 3.91), 0.18, 0.08, segments=16)
        for angle in range(0, 360, 90):
            rad = math.radians(angle)
            ceiling_metal.add_box((x + 0.55 * math.cos(rad), -0.60 + 0.55 * math.sin(rad), 3.60),
                                  (0.12, 0.12, 0.025), (0, 0, rad))
    for x, y, length, angle in ((-3.7, 2.9, 1.3, 0.18), (-1.4, -0.4, 1.8, -0.12),
                                (2.4, -2.2, 1.2, 0.22), (4.4, 1.0, 0.8, -0.28)):
        floor_wear.add_box((x, y, 0.012), (length, 0.025, 0.008), (0, 0, angle))
    ceiling_metal.finish("FULL_Classroom_CeilingHardware", metal, collection, bevel=0.004)
    floor_wear.finish("FULL_Classroom_FloorWear", darkwood, collection, bevel=0.001)

    bpy.context.scene["studentDeskSharedMeshCount"] = 18
    bpy.context.scene["deskMeshesRelinked"] = shared


def rebase_hinge(name: str, pivot: tuple[float, float, float]) -> None:
    hinge = required_object(name)
    child_world = {child: child.matrix_world.copy() for child in hinge.children}
    hinge.matrix_world = Matrix.Translation(Vector(pivot))
    for child, matrix in child_world.items():
        child.matrix_world = matrix


def panel_frame_y(acc: MeshAccumulator, x: float, y: float, z: float, width: float, height: float,
                  depth: float = 0.035) -> None:
    for px in (x - width / 2, x + width / 2):
        acc.add_box((px, y, z), (0.055, depth, height))
    for pz in (z - height / 2, z + height / 2):
        acc.add_box((x, y, pz), (width, depth, 0.055))


def panel_frame_x(acc: MeshAccumulator, x: float, y: float, z: float, width: float, height: float,
                  depth: float = 0.035) -> None:
    for py in (y - width / 2, y + width / 2):
        acc.add_box((x, py, z), (depth, 0.055, height))
    for pz in (z - height / 2, z + height / 2):
        acc.add_box((x, y, pz), (depth, width, 0.055))


def add_manor_door_details(collection: bpy.types.Collection, wood: bpy.types.Material,
                           brass: bpy.types.Material) -> None:
    hinges = {
        "Door_Front_Hinge_L": (-1.23, 6.80, 0.0),
        "Door_Front_Hinge_R": (1.23, 6.80, 0.0),
        "Door_Room1_Hinge": (-3.39, -2.62, 0.0),
        "Door_Room2_Hinge": (3.39, -3.78, 0.0),
        "Door_Room3_Hinge": (-3.39, -11.42, 0.0),
        "Door_Room4_Hinge_L": (-1.36, -18.0, 0.0),
        "Door_Room4_Hinge_R": (1.36, -18.0, 0.0),
    }
    for name, pivot in hinges.items():
        rebase_hinge(name, pivot)

    door_specs = [
        ("Door_Front_Hinge_L", -0.63, 6.735, 1.38, 1.02, (0, 1, 0), -0.12),
        ("Door_Front_Hinge_R", 0.63, 6.735, 1.38, 1.02, (0, 1, 0), 0.12),
        ("Door_Room1_Hinge", -3.325, -3.20, 1.30, 1.00, (1, 0, 0), -3.305),
        ("Door_Room2_Hinge", 3.325, -3.20, 1.30, 1.00, (1, 0, 0), 3.305),
        ("Door_Room3_Hinge", -3.325, -12.00, 1.30, 1.00, (1, 0, 0), -3.305),
        ("Door_Room4_Hinge_L", -0.695, -17.935, 1.38, 1.12, (0, 1, 0), -0.12),
        ("Door_Room4_Hinge_R", 0.695, -17.935, 1.38, 1.12, (0, 1, 0), 0.12),
    ]
    for index, (hinge_name, x, y, z, width, normal, handle_axis) in enumerate(door_specs, start=1):
        wood_acc, brass_acc = MeshAccumulator(), MeshAccumulator()
        if normal == (0, 1, 0):
            panel_frame_y(wood_acc, x, y - 0.018, 0.70, width * 0.72, 0.66)
            panel_frame_y(wood_acc, x, y - 0.018, 1.83, width * 0.72, 0.82)
            brass_acc.add_box((handle_axis, y - 0.035, 1.10), (0.16, 0.035, 0.34))
            brass_acc.add_torus((handle_axis, y - 0.058, 1.10), 0.07, 0.015, (0, 1, 0), 16, 6)
        else:
            panel_frame_x(wood_acc, x - 0.018 * (1 if x > 0 else -1), y, 0.68, width * 0.72, 0.64)
            panel_frame_x(wood_acc, x - 0.018 * (1 if x > 0 else -1), y, 1.78, width * 0.72, 0.78)
            handle_y = -3.56 if "Room1" in hinge_name else (-2.84 if "Room2" in hinge_name else -12.36)
            face_x = x - 0.035 * (1 if x > 0 else -1)
            brass_acc.add_box((face_x, handle_y, 1.08), (0.035, 0.16, 0.34))
            brass_acc.add_torus((face_x - 0.022 * (1 if x > 0 else -1), handle_y, 1.08),
                                0.07, 0.015, (1, 0, 0), 16, 6)
        hinge = required_object(hinge_name)
        wood_acc.finish(f"FULL_Manor_DoorMoulding_{index:02d}", wood, collection, parent=hinge, bevel=0.005)
        brass_acc.finish(f"FULL_Manor_DoorHardware_{index:02d}", brass, collection, parent=hinge, bevel=0.004)


def add_wainscot_panels(collection: bpy.types.Collection, wood: bpy.types.Material) -> None:
    west = MeshAccumulator()
    east = MeshAccumulator()
    west_segments = ((0.3, 6.3), (-2.45, -0.25), (-11.25, -3.95), (-17.55, -12.75))
    east_segments = ((0.3, 6.3), (-2.45, -0.25), (-17.55, -3.95))
    for acc, x, segments, face in ((west, -3.205, west_segments, -1), (east, 3.205, east_segments, 1)):
        for low, high in segments:
            center = low + 0.70
            while center < high - 0.35:
                panel_frame_x(acc, x + face * 0.012, center, 0.72, min(1.10, high - low - 0.18), 0.86, 0.028)
                center += 1.30
    south = MeshAccumulator()
    for x in (-2.75, -1.82, 1.82, 2.75):
        panel_frame_y(south, x, 6.61, 0.72, 0.76, 0.86, 0.028)
    north = MeshAccumulator()
    for x in (-2.42, 2.42):
        panel_frame_y(north, x, -17.81, 0.72, 1.35, 0.86, 0.028)
    west.finish("FULL_Manor_WainscotWest", wood, collection, bevel=0.004)
    east.finish("FULL_Manor_WainscotEast", wood, collection, bevel=0.004)
    south.finish("FULL_Manor_WainscotSouth", wood, collection, bevel=0.004)
    north.finish("FULL_Manor_WainscotNorth", wood, collection, bevel=0.004)


def add_manor_details(collection: bpy.types.Collection) -> None:
    wood = required_material("M_Cinematic_Manor_Wood")
    brass = required_material("M_Cinematic_Manor_Brass")
    glass = required_material("M_Cinematic_Manor_Glass")
    iron = required_material("M_Cinematic_Manor_Iron")
    paper = required_material("M_Cinematic_Manor_Paper")
    rug = required_material("M_Cinematic_Manor_Rug")
    root = required_object("DreamManorHall")

    add_manor_door_details(collection, wood, brass)
    add_wainscot_panels(collection, wood)

    sconce_brass, sconce_glass = MeshAccumulator(), MeshAccumulator()
    sconce_specs = ((-3.08, 5.0, -1), (3.08, 5.0, 1), (-3.08, -1.0, -1),
                    (3.08, -5.8, 1), (-3.08, -9.0, -1), (3.08, -15.4, 1))
    for x, y, side in sconce_specs:
        sconce_brass.add_torus((x + side * 0.06, y, 2.30), 0.18, 0.035, (1, 0, 0), 20, 6)
        sconce_brass.add_rod((x + side * 0.08, y, 2.30), (x - side * 0.28, y, 2.28), 0.027, 12)
        sconce_brass.add_cylinder((x - side * 0.30, y, 2.34), 0.095, 0.06, (0, 0, 1), 16)
        for angle in range(0, 360, 90):
            rad = math.radians(angle)
            sconce_glass.add_rod((x - side * 0.31, y + 0.11 * math.cos(rad), 2.36 + 0.11 * math.sin(rad)),
                                 (x - side * 0.31, y + 0.18 * math.cos(rad), 2.62 + 0.18 * math.sin(rad)),
                                 0.009, 8)
    sconce_brass.finish("FULL_Manor_SconceBrasswork", brass, collection, parent=root, bevel=0.003)
    sconce_glass.finish("FULL_Manor_SconceShadeRibs", glass, collection, parent=root, bevel=0.002)

    fireplace_wood, fireplace_iron, fireplace_brass = MeshAccumulator(), MeshAccumulator(), MeshAccumulator()
    for y in (2.56, 4.34):
        fireplace_wood.add_box((-3.78, y, 1.12), (0.08, 0.15, 1.82))
    for z in (0.22, 1.98):
        fireplace_wood.add_box((-3.78, 3.45, z), (0.08, 1.93, 0.14))
    fireplace_wood.add_box((-3.72, 3.45, 2.16), (0.18, 2.25, 0.16))
    for y in (3.10, 3.80):
        fireplace_iron.add_rod((-3.93, y, 0.24), (-3.93, y, 1.20), 0.022, 10)
        fireplace_iron.add_rod((-3.93, y - 0.20, 0.42), (-3.93, y + 0.20, 0.42), 0.018, 10)
    fireplace_brass.add_rod((-3.84, 2.72, 0.18), (-3.84, 2.72, 1.20), 0.018, 10)
    fireplace_brass.add_rod((-3.84, 2.62, 0.18), (-3.84, 2.82, 0.18), 0.018, 10)
    fireplace_wood.finish("FULL_Manor_FireplaceMoulding", wood, collection, parent=root, bevel=0.007)
    fireplace_iron.finish("FULL_Manor_FireplaceGrate", iron, collection, parent=root, bevel=0.003)
    fireplace_brass.finish("FULL_Manor_FireTools", brass, collection, parent=root, bevel=0.003)

    furniture_wood, furniture_brass, furniture_fabric = MeshAccumulator(), MeshAccumulator(), MeshAccumulator()
    for z in (0.38, 0.62):
        furniture_wood.add_box((3.02, 1.72, z), (0.035, 1.35, 0.18))
        furniture_brass.add_box((2.99, 1.72, z), (0.025, 0.16, 0.035))
    for y in (-6.25, -6.80, -7.35):
        furniture_fabric.add_torus((2.67, y, 1.04), 0.055, 0.010, (1, 0, 0), 12, 5)
    for y in (-6.25, -6.80, -7.35):
        furniture_wood.add_box((2.69, y, 0.80), (0.035, 0.42, 0.04))
    furniture_wood.finish("FULL_Manor_FurnitureJoinery", wood, collection, parent=root, bevel=0.005)
    furniture_brass.finish("FULL_Manor_FurnitureHardware", brass, collection, parent=root, bevel=0.003)
    furniture_fabric.finish("FULL_Manor_BenchTufting", rug, collection, parent=root, bevel=0.002)

    beam_iron = MeshAccumulator()
    for y in (5.2, 3.1, 1.0, -1.4, -3.8, -6.2, -8.6, -11.0, -13.4, -15.8):
        for x in (-2.25, 0.0, 2.25):
            beam_iron.add_box((x, y, 3.88), (0.23, 0.24, 0.07))
            for dx in (-0.07, 0.07):
                beam_iron.add_cylinder((x + dx, y - 0.13, 3.88), 0.012, 0.025, (0, 1, 0), 8)
    beam_iron.finish("FULL_Manor_BeamStraps", iron, collection, parent=root, bevel=0.003)

    reserved = MeshAccumulator()
    for y in (-8.1, -13.0):
        panel_frame_x(reserved, 3.20, y, 1.95, 1.55, 2.10, 0.035)
        reserved.add_box((3.18, y, 2.98), (0.045, 1.82, 0.16))
    reserved.finish("FULL_Manor_ReservedModules", wood, collection, parent=root, bevel=0.005)

    gallery_wood, gallery_paper = MeshAccumulator(), MeshAccumulator()
    gallery_specs = ((-3.18, -6.6, 2.55, 0.82, 0.62), (3.18, -10.3, 2.55, 0.75, 0.92),
                     (-3.18, -14.7, 2.58, 0.88, 0.66), (3.18, 0.9, 2.58, 0.72, 0.88))
    for x, y, z, width, height in gallery_specs:
        panel_frame_x(gallery_wood, x, y, z, width, height, 0.055)
        gallery_paper.add_box((x - 0.025 * (1 if x > 0 else -1), y, z), (0.025, width - 0.10, height - 0.10))
    gallery_wood.finish("FULL_Manor_GalleryFrames", wood, collection, parent=root, bevel=0.005)
    gallery_paper.finish("FULL_Manor_GalleryMats", paper, collection, parent=root, bevel=0.002)


def validate(kind: str) -> dict:
    required = {
        "classroom": ("FULL_Classroom_DeskHardware", "FULL_Classroom_WindowHardware",
                      "FULL_Classroom_DoorMoulding", "FULL_MapCabinet_Closed_Hardware",
                      "FULL_TeacherDesk_Closed_Lockset", "FULL_Classroom_CeilingHardware"),
        "manor": ("FULL_Manor_DoorMoulding_01", "FULL_Manor_DoorMoulding_07",
                  "FULL_Manor_WainscotWest", "FULL_Manor_SconceBrasswork",
                  "FULL_Manor_FireplaceMoulding", "FULL_Manor_ReservedModules"),
    }[kind]
    missing = [name for name in required if bpy.data.objects.get(name) is None]
    if missing:
        raise RuntimeError(f"Full-scene detail layer is incomplete: {missing}")
    if bpy.context.scene.get("fullScene") is not True or bpy.context.scene.get("visualDirectionApproved") is not True:
        raise RuntimeError("Full-scene approval metadata is missing")
    if bpy.context.scene.get("sampleOnly") is not False:
        raise RuntimeError("Approved full scene still identifies as sample-only")
    collection = bpy.data.collections.get(COLLECTION_NAME)
    mesh_count = sum(1 for obj in collection.all_objects if obj.type == "MESH") if collection else 0
    return {"kind": kind, "fullDetailMeshes": mesh_count, "objects": len(bpy.data.objects),
            "materials": len(bpy.data.materials), "images": len(bpy.data.images)}


def main() -> None:
    options = parse_args()
    if bpy.app.version[:3] != BLENDER_VERSION:
        raise RuntimeError(f"Expected Blender {BLENDER_VERSION}, got {bpy.app.version[:3]}")
    kind, action = options.mode.split("-", 1)
    source = Path(options.root).resolve() / SOURCES[kind]
    if not source.is_file():
        raise RuntimeError(f"Authoritative environment Blend is missing: {source}")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    if action == "expand":
        collection = reset_collection()
        if kind == "classroom":
            add_classroom_details(collection)
        else:
            add_manor_details(collection)
        scene = bpy.context.scene
        scene["sampleOnly"] = False
        scene["runtimeDefault"] = True
        scene["fullScene"] = True
        scene["visualDirectionApproved"] = True
        scene["visualApprovalDate"] = "2026-08-30"
        scene["artStatus"] = "cinematic-full-runtime"
        bpy.context.view_layer.update()
        bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    result = validate(kind)
    print(f"ENVIRONMENT_CINEMATIC_FULL_{kind.upper()}_{action.upper()}_PASS "
          f"details={result['fullDetailMeshes']} objects={result['objects']} materials={result['materials']}")


if __name__ == "__main__":
    main()
