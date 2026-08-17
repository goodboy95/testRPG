"""Build and validate deterministic pixel-art assets for the dream escape rooms.

This is an offline production tool. The Godot project consumes only the generated
PNG and JSON files and has no runtime dependency on Python or Pillow.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "assets" / "generated" / "dream_escape" / "pixel"

PALETTE = {
    "transparent": (0, 0, 0, 0),
    "void": (11, 20, 32, 255),
    "navy": (16, 30, 45, 255),
    "navy_2": (22, 42, 57, 255),
    "teal_dark": (27, 54, 63, 255),
    "teal": (43, 78, 82, 255),
    "teal_light": (74, 111, 108, 255),
    "plaster": (71, 82, 84, 255),
    "plaster_light": (95, 101, 96, 255),
    "plaster_dark": (48, 60, 65, 255),
    "wood_dark": (54, 39, 36, 255),
    "wood": (91, 61, 43, 255),
    "wood_light": (132, 91, 57, 255),
    "paper": (200, 193, 166, 255),
    "paper_shadow": (151, 147, 129, 255),
    "chalk": (227, 222, 197, 255),
    "amber": (219, 159, 78, 255),
    "amber_light": (242, 202, 112, 255),
    "rust": (132, 70, 50, 255),
    "red": (172, 72, 66, 255),
    "skin": (224, 177, 133, 255),
    "skin_shadow": (183, 125, 95, 255),
    "hair": (27, 28, 37, 255),
    "hair_light": (50, 47, 56, 255),
    "shirt_red": (142, 54, 48, 255),
    "shirt_red_light": (177, 73, 57, 255),
    "trouser": (29, 44, 67, 255),
    "shoe": (30, 34, 43, 255),
    "blouse": (207, 207, 184, 255),
    "xiaoyu_teal": (49, 129, 128, 255),
    "outline": (9, 14, 22, 255),
}


def c(name: str) -> tuple[int, int, int, int]:
    return PALETTE[name]


def rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], fill: str, outline: str | None = None) -> None:
    draw.rectangle(box, fill=c(fill), outline=c(outline) if outline else None)


def render_tiles() -> Image.Image:
    image = Image.new("RGBA", (256, 256), c("transparent"))
    draw = ImageDraw.Draw(image)

    for variant in range(4):
        x = variant * 32
        # Upper plaster wall.
        rect(draw, (x, 0, x + 31, 31), "plaster", "plaster_dark")
        draw.line((x, 22, x + 31, 22), fill=c("plaster_dark"))
        if variant & 1:
            draw.line((x + 7, 3, x + 10, 8, x + 8, 13), fill=c("plaster_dark"))
        if variant & 2:
            rect(draw, (x + 23, 8, x + 26, 11), "plaster_light")

        # Teal-painted lower wall.
        rect(draw, (x, 32, x + 31, 63), "teal_dark", "navy")
        draw.line((x, 36, x + 31, 36), fill=c("teal"))
        if variant == 1:
            draw.line((x + 14, 40, x + 12, 51, x + 16, 60), fill=c("navy_2"))
        if variant == 3:
            rect(draw, (x + 4, 48, x + 6, 50), "teal")

        # Worn square floor tile.
        rect(draw, (x, 64, x + 31, 95), "navy_2", "void")
        draw.line((x + 2, 92, x + 29, 92), fill=c("teal_dark"))
        if variant == 0:
            rect(draw, (x + 7, 74, x + 9, 76), "teal_dark")
        elif variant == 1:
            draw.line((x + 20, 67, x + 17, 77, x + 21, 82), fill=c("teal_dark"))
        elif variant == 2:
            rect(draw, (x + 3, 87, x + 5, 89), "plaster_dark")
        else:
            draw.line((x + 7, 68, x + 8, 72), fill=c("teal") )

        # Darker floor for room edges.
        rect(draw, (x, 96, x + 31, 127), "navy", "void")
        draw.line((x + 2, 123, x + 29, 123), fill=c("navy_2"))
        if variant in (1, 3):
            rect(draw, (x + 24, 104, x + 26, 106), "teal_dark")

        # Baseboard transition tile.
        rect(draw, (x, 128, x + 31, 159), "teal_dark")
        rect(draw, (x, 128, x + 31, 133), "wood_dark")
        draw.line((x, 134, x + 31, 134), fill=c("wood_light"))
        if variant == 2:
            draw.line((x + 17, 136, x + 18, 153), fill=c("navy_2"))

    # Reusable shadow and threshold tiles.
    for variant in range(4):
        x = variant * 32
        rect(draw, (x, 160, x + 31, 191), "void")
        if variant == 0:
            rect(draw, (x + 2, 160, x + 29, 167), "wood_dark")
        elif variant == 1:
            draw.polygon([(x, 160), (x + 31, 160), (x + 31, 191)], fill=c("navy"))
        elif variant == 2:
            draw.polygon([(x, 160), (x, 191), (x + 31, 160)], fill=c("navy"))
        else:
            rect(draw, (x + 5, 173, x + 26, 175), "navy_2")

    # Remaining cells are decorative rubble/dust variants.
    for gy in range(6, 8):
        for gx in range(8):
            x, y = gx * 32, gy * 32
            rect(draw, (x, y, x + 31, y + 31), "transparent")
            color = "paper_shadow" if gx % 2 == 0 else "teal_light"
            rect(draw, (x + 6 + gx % 3, y + 12, x + 8 + gx % 3, y + 13), color)
            rect(draw, (x + 19, y + 21 - gx % 4, x + 20, y + 22 - gx % 4), color)

    return image


def draw_blackboard(draw: ImageDraw.ImageDraw, origin: tuple[int, int], revealed: bool) -> None:
    x, y = origin
    rect(draw, (x, y, x + 211, y + 57), "wood_dark", "outline")
    rect(draw, (x + 5, y + 5, x + 206, y + 49), "teal_dark", "wood_light")
    draw.arc((x + 22, y + 13, x + 91, y + 41), 185, 348, fill=c("teal_light"))
    draw.arc((x + 103, y + 10, x + 179, y + 43), 14, 168, fill=c("teal_light"))
    rect(draw, (x + 72, y + 51, x + 139, y + 55), "wood")
    if not revealed:
        return
    # Window -> clock -> duty roster pictograms, no digits.
    rect(draw, (x + 45, y + 17, x + 62, y + 33), "transparent", "chalk")
    draw.line((x + 53, y + 17, x + 53, y + 33), fill=c("chalk"))
    draw.line((x + 45, y + 25, x + 62, y + 25), fill=c("chalk"))
    draw.line((x + 68, y + 25, x + 79, y + 25), fill=c("chalk"))
    draw.polygon([(x + 79, y + 22), (x + 84, y + 25), (x + 79, y + 28)], fill=c("chalk"))
    draw.ellipse((x + 91, y + 16, x + 108, y + 33), outline=c("chalk"))
    draw.line((x + 99, y + 24, x + 99, y + 18), fill=c("chalk"))
    draw.line((x + 99, y + 24, x + 94, y + 29), fill=c("chalk"))
    draw.line((x + 114, y + 25, x + 125, y + 25), fill=c("chalk"))
    draw.polygon([(x + 125, y + 22), (x + 130, y + 25), (x + 125, y + 28)], fill=c("chalk"))
    rect(draw, (x + 137, y + 14, x + 158, y + 35), "transparent", "chalk")
    for row in range(3):
        draw.line((x + 141, y + 20 + row * 5, x + 154, y + 20 + row * 5), fill=c("chalk"))


def draw_map_cabinet(draw: ImageDraw.ImageDraw, origin: tuple[int, int], opened: bool) -> None:
    x, y = origin
    rect(draw, (x + 8, y + 4, x + 87, y + 91), "wood_dark", "outline")
    rect(draw, (x + 14, y + 10, x + 81, y + 84), "teal", "wood_light")
    if opened:
        rect(draw, (x + 4, y + 13, x + 42, y + 80), "wood", "outline")
        rect(draw, (x + 10, y + 20, x + 36, y + 73), "paper_shadow", "wood_light")
        draw.line((x + 16, y + 28, x + 31, y + 42, x + 18, y + 62), fill=c("teal_dark"))
        rect(draw, (x + 51, y + 23, x + 75, y + 54), "paper", "outline")
        for face in range(3):
            draw.ellipse((x + 55 + face * 6, y + 29, x + 58 + face * 6, y + 32), fill=c("plaster_dark"))
        draw.line((x + 55, y + 38, x + 70, y + 38), fill=c("rust"))
    else:
        draw.line((x + 48, y + 13, x + 48, y + 81), fill=c("wood_dark"))
        draw.polygon([(x + 48, y + 39), (x + 41, y + 51), (x + 55, y + 51)], fill=c("amber"))


def draw_window(draw: ImageDraw.ImageDraw, origin: tuple[int, int]) -> None:
    x, y = origin
    rect(draw, (x + 7, y + 6, x + 88, y + 89), "wood_dark", "outline")
    rect(draw, (x + 12, y + 11, x + 83, y + 83), "teal_light", "paper_shadow")
    for column in range(1, 3):
        draw.line((x + 12 + column * 24, y + 11, x + 12 + column * 24, y + 83), fill=c("wood_dark"), width=2)
    draw.line((x + 12, y + 48, x + 83, y + 48), fill=c("wood_dark"), width=2)
    for pane in range(3):
        px = x + 21 + pane * 24
        draw.line((px, y + 24, px + 7, y + 32), fill=c("amber_light"), width=2)
    draw.polygon([(x + 13, y + 83), (x + 53, y + 83), (x + 82, y + 62), (x + 82, y + 83)], fill=(242, 202, 112, 48))


def draw_clock(draw: ImageDraw.ImageDraw, origin: tuple[int, int]) -> None:
    x, y = origin
    draw.ellipse((x + 7, y + 7, x + 56, y + 56), fill=c("paper_shadow"), outline=c("outline"), width=2)
    draw.ellipse((x + 12, y + 12, x + 51, y + 51), fill=c("navy_2"), outline=c("paper"))
    cx, cy = x + 32, y + 32
    draw.line((cx, cy, cx, y + 16), fill=c("amber_light"), width=2)
    draw.line((cx, cy, x + 21, y + 43), fill=c("amber_light"), width=2)
    rect(draw, (cx - 1, cy - 1, cx + 1, cy + 1), "amber")


def draw_roster(draw: ImageDraw.ImageDraw, origin: tuple[int, int]) -> None:
    x, y = origin
    rect(draw, (x + 8, y + 5, x + 71, y + 90), "paper", "outline")
    rect(draw, (x + 14, y + 12, x + 65, y + 17), "paper_shadow")
    for row in range(4):
        ry = y + 27 + row * 13
        draw.line((x + 17, ry, x + 62, ry), fill=c("plaster_dark"))
        rect(draw, (x + 18, ry - 5, x + 21, ry - 2), "teal")
    draw.ellipse((x + 15, y + 31, x + 29, y + 44), outline=c("red"), width=2)


def draw_teacher_desk(draw: ImageDraw.ImageDraw, origin: tuple[int, int], opened: bool) -> None:
    x, y = origin
    rect(draw, (x + 5, y + 10, x + 106, y + 64), "wood_dark", "outline")
    rect(draw, (x, y + 5, x + 111, y + 18), "wood_light", "outline")
    rect(draw, (x + 11, y + 23, x + 100, y + 57), "wood", "wood_dark")
    if opened:
        rect(draw, (x + 22, y + 19, x + 89, y + 43), "navy", "outline")
        rect(draw, (x + 27, y + 24, x + 84, y + 38), "wood_dark")
        rect(draw, (x + 55, y + 27, x + 70, y + 30), "amber")
    else:
        rect(draw, (x + 41, y + 27, x + 70, y + 43), "navy_2", "outline")
        for dot in range(3):
            rect(draw, (x + 47 + dot * 8, y + 33, x + 49 + dot * 8, y + 37), "amber")
    rect(draw, (x + 11, y + 57, x + 18, y + 69), "wood_dark")
    rect(draw, (x + 93, y + 57, x + 100, y + 69), "wood_dark")


def draw_student_desk(draw: ImageDraw.ImageDraw, origin: tuple[int, int], variant: int) -> None:
    x, y = origin
    rect(draw, (x + 5, y + 7, x + 50, y + 26), "wood", "outline")
    rect(draw, (x + 3, y + 4, x + 52, y + 13), "wood_light", "outline")
    rect(draw, (x + 10, y + 27, x + 15, y + 42), "wood_dark")
    rect(draw, (x + 40, y + 27, x + 45, y + 42), "wood_dark")
    rect(draw, (x + 19, y + 30, x + 36, y + 45), "teal_dark", "outline")
    if variant == 1:
        draw.line((x + 14, y + 9, x + 26, y + 9), fill=c("paper_shadow"))
    elif variant == 2:
        rect(draw, (x + 36, y + 7, x + 42, y + 9), "chalk")
    elif variant == 3:
        draw.line((x + 8, y + 17, x + 22, y + 14), fill=c("wood_dark"))


def render_props() -> Image.Image:
    image = Image.new("RGBA", (512, 256), c("transparent"))
    draw = ImageDraw.Draw(image)
    draw_blackboard(draw, (0, 0), False)
    draw_blackboard(draw, (216, 0), True)
    draw_map_cabinet(draw, (0, 64), False)
    draw_map_cabinet(draw, (96, 64), True)
    draw_window(draw, (192, 64))
    draw_clock(draw, (288, 64))
    draw_roster(draw, (352, 64))
    draw_teacher_desk(draw, (0, 160), False)
    draw_teacher_desk(draw, (112, 160), True)
    rect(draw, (228, 168, 249, 174), "chalk", "paper_shadow")
    for variant in range(4):
        draw_student_desk(draw, (256 + variant * 64, 160), variant)
    return image


def draw_character_frame(
    draw: ImageDraw.ImageDraw,
    origin: tuple[int, int],
    direction: int,
    phase: int,
    xiaoyu: bool,
) -> None:
    ox, oy = origin
    bob = 1 if phase in (1, 3) else 0
    step = (-2, 0, 2, 0)[phase]
    skin = "skin" if not xiaoyu else "skin"
    top = "blouse" if xiaoyu else "shirt_red"
    top_light = "xiaoyu_teal" if xiaoyu else "shirt_red_light"
    bottom = "trouser"

    # Grounding shadow.
    draw.ellipse((ox + 8, oy + 42, ox + 24, oy + 46), fill=(9, 14, 22, 110))
    head_y = oy + 5 + bob
    body_y = oy + 20 + bob

    if direction == 3:  # back
        draw.ellipse((ox + 9, head_y, ox + 22, head_y + 15), fill=c("hair"), outline=c("outline"))
        rect(draw, (ox + 10, body_y, ox + 21, body_y + 14), top, "outline")
        if xiaoyu:
            draw.ellipse((ox + 20, head_y + 9, ox + 25, head_y + 21), fill=c("hair"), outline=c("outline"))
            rect(draw, (ox + 10, body_y + 1, ox + 21, body_y + 3), "xiaoyu_teal")
    elif direction in (1, 2):  # left/right
        flip = -1 if direction == 1 else 1
        head_x = ox + (9 if flip < 0 else 10)
        draw.ellipse((head_x, head_y, head_x + 13, head_y + 15), fill=c(skin), outline=c("outline"))
        rect(draw, (head_x, head_y, head_x + 12, head_y + 7), "hair")
        eye_x = head_x + (3 if flip < 0 else 10)
        rect(draw, (eye_x, head_y + 9, eye_x, head_y + 9), "outline")
        rect(draw, (ox + 10, body_y, ox + 21, body_y + 14), top, "outline")
        rect(draw, (ox + (7 if flip < 0 else 21), body_y + 3 + step // 2,
                    ox + (10 if flip < 0 else 24), body_y + 13 + step // 2), skin, "outline")
        if xiaoyu:
            draw.ellipse((head_x - (4 if flip < 0 else -12), head_y + 7,
                          head_x + (1 if flip < 0 else 17), head_y + 19), fill=c("hair"), outline=c("outline"))
            rect(draw, (ox + 12, body_y + 2, ox + 19, body_y + 4), "xiaoyu_teal")
    else:  # front
        draw.ellipse((ox + 9, head_y, ox + 22, head_y + 15), fill=c(skin), outline=c("outline"))
        rect(draw, (ox + 9, head_y, ox + 22, head_y + 6), "hair")
        rect(draw, (ox + 11, head_y + 3, ox + 13, head_y + 8), "hair")
        rect(draw, (ox + 18, head_y + 3, ox + 21, head_y + 8), "hair")
        rect(draw, (ox + 12, head_y + 10, ox + 12, head_y + 10), "outline")
        rect(draw, (ox + 19, head_y + 10, ox + 19, head_y + 10), "outline")
        rect(draw, (ox + 10, body_y, ox + 21, body_y + 14), top, "outline")
        rect(draw, (ox + 7, body_y + 2 - step // 2, ox + 10, body_y + 13 - step // 2), skin, "outline")
        rect(draw, (ox + 21, body_y + 2 + step // 2, ox + 24, body_y + 13 + step // 2), skin, "outline")
        if xiaoyu:
            draw.ellipse((ox + 20, head_y + 8, ox + 25, head_y + 21), fill=c("hair"), outline=c("outline"))
            rect(draw, (ox + 21, head_y + 8, ox + 23, head_y + 10), "xiaoyu_teal")
            draw.polygon([(ox + 13, body_y + 1), (ox + 18, body_y + 1), (ox + 16, body_y + 8)], fill=c("xiaoyu_teal"))

    # Legs and shoes, visible for every direction.
    left_shift = step if phase in (1, 3) else 0
    right_shift = -step if phase in (1, 3) else 0
    rect(draw, (ox + 10 + left_shift, body_y + 14, ox + 15 + left_shift, oy + 42), bottom, "outline")
    rect(draw, (ox + 17 + right_shift, body_y + 14, ox + 22 + right_shift, oy + 42), bottom, "outline")
    rect(draw, (ox + 9 + left_shift, oy + 41, ox + 15 + left_shift, oy + 44), "shoe", "outline")
    rect(draw, (ox + 17 + right_shift, oy + 41, ox + 23 + right_shift, oy + 44), "shoe", "outline")
    if not xiaoyu and direction == 0:
        rect(draw, (ox + 13, body_y + 3, ox + 18, body_y + 4), top_light)


def render_character(xiaoyu: bool) -> Image.Image:
    image = Image.new("RGBA", (128, 192), c("transparent"))
    draw = ImageDraw.Draw(image)
    for direction in range(4):
        for phase in range(4):
            draw_character_frame(draw, (phase * 32, direction * 48), direction, phase, xiaoyu)
    return image


def render_ambient() -> Image.Image:
    image = Image.new("RGBA", (128, 64), c("transparent"))
    draw = ImageDraw.Draw(image)
    for frame in range(4):
        x = frame * 32
        for index in range(7):
            px = x + 3 + ((index * 11 + frame * 3) % 26)
            py = 3 + ((index * 7 + frame * 2) % 25)
            alpha = 72 if index % 2 else 112
            draw.point((px, py), fill=(242, 202, 112, alpha))
            if index == frame:
                draw.point((px + 1, py), fill=(242, 202, 112, alpha // 2))
    draw.polygon([(0, 32), (56, 32), (112, 63), (30, 63)], fill=(242, 202, 112, 28))
    draw.polygon([(34, 32), (62, 32), (105, 63), (70, 63)], fill=(242, 202, 112, 20))
    return image


BUILDERS: dict[str, tuple[tuple[int, int], Callable[[], Image.Image]]] = {
    "school-interior-tiles-v1.png": ((256, 256), render_tiles),
    "classroom-props-v1.png": ((512, 256), render_props),
    "dream-ambient-v1.png": ((128, 64), render_ambient),
    "protagonist-walk-4x4-v1.png": ((128, 192), lambda: render_character(False)),
    "lin-xiaoyu-walk-4x4-v2.png": ((128, 192), lambda: render_character(True)),
}

REGIONS = {
    "blackboard_blank": [0, 0, 212, 58],
    "blackboard_revealed": [216, 0, 212, 58],
    "map_cabinet_locked": [0, 64, 96, 96],
    "map_cabinet_open": [96, 64, 96, 96],
    "window_marked": [192, 64, 96, 96],
    "clock_seven": [288, 64, 64, 64],
    "duty_roster_second": [352, 64, 80, 96],
    "teacher_desk_locked": [0, 160, 112, 72],
    "teacher_desk_open": [112, 160, 112, 72],
    "chalk_pickup": [224, 160, 32, 16],
    "student_desk_0": [256, 160, 56, 48],
    "student_desk_1": [320, 160, 56, 48],
    "student_desk_2": [384, 160, 56, 48],
    "student_desk_3": [448, 160, 56, 48],
}


def png_bytes(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def opaque_color_count(image: Image.Image) -> int:
    pixels = image.get_flattened_data()
    return len({pixel[:3] for pixel in pixels if pixel[3] == 255})


def has_transparent_corner(image: Image.Image) -> bool:
    return any(image.getpixel(point)[3] == 0 for point in [(0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1)])


def validate_image(name: str, image: Image.Image, size: tuple[int, int]) -> None:
    if image.mode != "RGBA":
        raise ValueError(f"{name}: expected RGBA, got {image.mode}")
    if image.size != size:
        raise ValueError(f"{name}: expected {size}, got {image.size}")
    if opaque_color_count(image) > 32:
        raise ValueError(f"{name}: opaque palette exceeds 32 colors")
    if name != "school-interior-tiles-v1.png" and not has_transparent_corner(image):
        raise ValueError(f"{name}: expected at least one transparent corner")


def render_all() -> dict[str, tuple[Image.Image, bytes]]:
    rendered: dict[str, tuple[Image.Image, bytes]] = {}
    for name, (size, builder) in BUILDERS.items():
        image = builder()
        validate_image(name, image, size)
        rendered[name] = (image, png_bytes(image))
    return rendered


def manifest(rendered: dict[str, tuple[Image.Image, bytes]]) -> dict:
    return {
        "schemaVersion": 1,
        "generatedBy": "scripts/tools/build_dream_pixel_art.py",
        "pillowVersion": Image.__version__,
        "tileSize": 32,
        "characterFrameSize": [32, 48],
        "characterLayout": {"columns": 4, "rows": ["down", "left", "right", "up"]},
        "files": {
            name: {
                "width": image.width,
                "height": image.height,
                "mode": image.mode,
                "opaqueColors": opaque_color_count(image),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
            for name, (image, data) in rendered.items()
        },
        "classroomPropRegions": REGIONS,
    }


def build() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    rendered = render_all()
    for name, (_, data) in rendered.items():
        (OUTPUT / name).write_bytes(data)
    (OUTPUT / "dream-pixel-art-manifest.json").write_text(
        json.dumps(manifest(rendered), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"DREAM_PIXEL_ART_BUILT files={len(rendered)} output={OUTPUT}")


def check() -> None:
    rendered = render_all()
    expected_manifest = json.dumps(manifest(rendered), ensure_ascii=False, indent=2) + "\n"
    failures: list[str] = []
    for name, (_, data) in rendered.items():
        path = OUTPUT / name
        if not path.exists():
            failures.append(f"missing {path}")
        elif path.read_bytes() != data:
            failures.append(f"content mismatch {path}")
    manifest_path = OUTPUT / "dream-pixel-art-manifest.json"
    if not manifest_path.exists():
        failures.append(f"missing {manifest_path}")
    elif manifest_path.read_text(encoding="utf-8") != expected_manifest:
        failures.append(f"content mismatch {manifest_path}")
    if failures:
        raise SystemExit("DREAM_PIXEL_ART_CHECK_FAIL\n" + "\n".join(failures))
    print(f"DREAM_PIXEL_ART_CHECK_PASS files={len(rendered)} paletteMax=32")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    args = parser.parse_args()
    if args.mode == "build":
        build()
    else:
        check()


if __name__ == "__main__":
    main()
