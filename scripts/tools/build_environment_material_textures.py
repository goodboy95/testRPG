"""Build deterministic PBR textures for the environment-detail approval samples.

The output is intentionally stored below ``work/environment-detail-samples``.
It is not a runtime dependency and must not replace adopted assets until the
visual approval gate has passed.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import tempfile

from PIL import Image, ImageChops, ImageDraw, ImageFilter


SIZE = 2048
LANDSCAPE_SIZE = (1024, 768)
OUTPUT_RELATIVE = Path("work/environment-detail-samples/textures-v2")
MANIFEST_NAME = "environment-material-samples-manifest.json"
FAMILIES = ("plaster", "dark-oak", "terrazzo", "aged-brass", "textile")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    parser.add_argument("--root", default=".")
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def noise(size: tuple[int, int], seed: int, strength: int, blur: float, base: int = 128) -> Image.Image:
    rng = random.Random(seed)
    small_size = (max(16, size[0] // 64), max(16, size[1] // 64))
    small = Image.new("L", small_size)
    small.putdata([max(0, min(255, base + rng.randint(-strength, strength)))
                   for _ in range(small.width * small.height)])
    broad = small.resize(size, Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(blur))
    fine = Image.effect_noise(size, max(2.0, strength * 0.18)).filter(ImageFilter.GaussianBlur(0.35))
    return Image.blend(broad, fine, 0.18)


def colorize(gray: Image.Image, dark: tuple[int, int, int], light: tuple[int, int, int]) -> Image.Image:
    channels = []
    for low, high in zip(dark, light):
        channels.append(gray.point(lambda value, a=low, b=high: round(a + (b - a) * value / 255)))
    return Image.merge("RGB", tuple(channels))


def normal_from_height(height: Image.Image, strength: float) -> Image.Image:
    left = ImageChops.offset(height, -1, 0)
    right = ImageChops.offset(height, 1, 0)
    up = ImageChops.offset(height, 0, -1)
    down = ImageChops.offset(height, 0, 1)
    dx = ImageChops.subtract(left, right, scale=max(0.1, 2.0 / strength), offset=128)
    dy = ImageChops.subtract(up, down, scale=max(0.1, 2.0 / strength), offset=128)
    return Image.merge("RGB", (dx, dy, Image.new("L", height.size, 247)))


def orm(height: Image.Image, roughness: int, metallic: int) -> Image.Image:
    cavity = ImageChops.invert(height).point(lambda value: 218 + value * 25 // 255)
    rough = Image.new("L", height.size, roughness)
    metal = Image.new("L", height.size, metallic)
    return Image.merge("RGB", (cavity, rough, metal))


def plaster() -> tuple[Image.Image, Image.Image, Image.Image]:
    height = noise((SIZE, SIZE), 5201, 27, 1.2, 136)
    albedo = colorize(height, (68, 66, 59), (145, 132, 111))
    patch_layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    patch_draw = ImageDraw.Draw(patch_layer, "RGBA")
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(5219)
    # Broad hand-trowelled patches provide the missing middle-frequency form.
    for _ in range(46):
        cx = rng.randrange(-100, SIZE + 100)
        cy = rng.randrange(-100, SIZE + 100)
        rx = rng.randrange(90, 310)
        ry = rng.randrange(35, 150)
        count = rng.randrange(7, 13)
        points = []
        for index in range(count):
            angle = index / count * math.tau
            jitter = rng.uniform(0.68, 1.20)
            points.append((cx + math.cos(angle) * rx * jitter,
                           cy + math.sin(angle) * ry * jitter))
        patch_draw.polygon(points, fill=(43, 47, 43, rng.randrange(5, 15)))
    patch_layer = patch_layer.filter(ImageFilter.GaussianBlur(22.0))
    albedo = Image.alpha_composite(albedo.convert("RGBA"), patch_layer).convert("RGB")
    draw = ImageDraw.Draw(albedo, "RGBA")
    # Sparse authored cracks and damp blooms; avoid uniform procedural waves.
    for _ in range(24):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        points = [(x, y)]
        for _ in range(rng.randrange(3, 8)):
            x += rng.randrange(-70, 71)
            y += rng.randrange(30, 125)
            points.append((x, y))
        draw.line(points, fill=(30, 34, 32, rng.randrange(45, 100)), width=rng.choice((2, 3, 4)))
        hdraw.line(points, fill=rng.randrange(54, 82), width=rng.choice((2, 3)))
    damp_layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    damp_draw = ImageDraw.Draw(damp_layer, "RGBA")
    for _ in range(13):
        cx, cy = rng.randrange(SIZE), rng.randrange(SIZE)
        radius = rng.randrange(75, 235)
        points = []
        count = rng.randrange(8, 15)
        for index in range(count):
            angle = index / count * math.tau
            local = radius * rng.uniform(0.45, 1.15)
            points.append((cx + math.cos(angle) * local,
                           cy + math.sin(angle) * local * rng.uniform(0.28, 0.62)))
        damp_draw.polygon(points, fill=(27, 53, 48, rng.randrange(7, 21)))
    damp_layer = damp_layer.filter(ImageFilter.GaussianBlur(34.0))
    albedo = Image.alpha_composite(albedo.convert("RGBA"), damp_layer).convert("RGB")
    return albedo, normal_from_height(height, 1.15), orm(height, 218, 0)


def dark_oak() -> tuple[Image.Image, Image.Image, Image.Image]:
    height = noise((SIZE, SIZE), 6101, 18, 0.7, 132)
    albedo = colorize(height, (22, 12, 8), (82, 49, 28))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(6113)
    # Vertical grain matches Arts-and-Crafts doors and panelling.
    for _ in range(330):
        x = rng.randrange(SIZE)
        amplitude = rng.randrange(3, 22)
        period = rng.randrange(150, 520)
        points = []
        for y in range(-32, SIZE + 32, 32):
            points.append((x + math.sin((y + rng.randrange(20)) / period * math.tau) * amplitude, y))
        alpha = rng.randrange(18, 64)
        draw.line(points, fill=(14, 7, 5, alpha), width=rng.choice((1, 1, 2, 3)))
        hdraw.line(points, fill=rng.randrange(88, 117), width=1)
    for _ in range(8):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        rx, ry = rng.randrange(10, 30), rng.randrange(28, 72)
        draw.arc((x-rx, y-ry, x+rx, y+ry), 22, 338, fill=(12, 7, 5, 76), width=3)
        hdraw.arc((x-rx, y-ry, x+rx, y+ry), 22, 338, fill=82, width=3)
    return albedo, normal_from_height(height, 0.85), orm(height, 174, 0)


def terrazzo() -> tuple[Image.Image, Image.Image, Image.Image]:
    height = noise((SIZE, SIZE), 7103, 10, 1.0, 133)
    albedo = colorize(height, (48, 48, 43), (91, 82, 68))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(7121)
    palette = ((174, 157, 126, 180), (40, 70, 69, 190), (112, 67, 43, 180),
               (204, 190, 155, 165), (32, 36, 35, 170))
    for _ in range(14500):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        rx = rng.choice((1, 2, 2, 3, 4, 5))
        ry = rng.choice((1, 2, 3, 4))
        draw.ellipse((x-rx, y-ry, x+rx, y+ry), fill=rng.choice(palette))
        hdraw.ellipse((x-rx, y-ry, x+rx, y+ry), fill=rng.randrange(125, 157))
    tile = SIZE // 8
    for coordinate in range(tile, SIZE, tile):
        draw.line((coordinate, 0, coordinate, SIZE), fill=(21, 22, 21, 140), width=5)
        draw.line((0, coordinate, SIZE, coordinate), fill=(21, 22, 21, 140), width=5)
        hdraw.line((coordinate, 0, coordinate, SIZE), fill=68, width=5)
        hdraw.line((0, coordinate, SIZE, coordinate), fill=68, width=5)
    return albedo, normal_from_height(height, 0.82), orm(height, 172, 0)


def aged_brass() -> tuple[Image.Image, Image.Image, Image.Image]:
    height = noise((SIZE, SIZE), 8101, 24, 0.9, 140)
    albedo = colorize(height, (57, 38, 14), (156, 112, 37))
    draw = ImageDraw.Draw(albedo, "RGBA")
    rng = random.Random(8111)
    for _ in range(520):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        radius = rng.randrange(2, 20)
        draw.ellipse((x-radius, y-radius, x+radius, y+radius),
                     fill=(23, 73, 66, rng.randrange(12, 74)))
    return albedo, normal_from_height(height, 0.55), orm(height, 112, 215)


def textile() -> tuple[Image.Image, Image.Image, Image.Image]:
    height = noise((SIZE, SIZE), 9109, 12, 0.5, 135)
    albedo = colorize(height, (25, 54, 50), (70, 91, 77))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    step = 22
    for p in range(-SIZE, SIZE * 2, step):
        draw.line((p, 0, p + SIZE, SIZE), fill=(164, 120, 57, 22), width=1)
        draw.line((p, SIZE, p + SIZE, 0), fill=(20, 32, 30, 38), width=1)
        hdraw.line((p, 0, p + SIZE, SIZE), fill=147, width=1)
    return albedo, normal_from_height(height, 0.62), orm(height, 220, 0)


def landscape() -> Image.Image:
    width, height = LANDSCAPE_SIZE
    image = Image.new("RGB", LANDSCAPE_SIZE, (89, 80, 61))
    draw = ImageDraw.Draw(image, "RGBA")
    # Muted, original landscape study used only as an in-world painting.
    for y in range(height):
        t = y / height
        draw.line((0, y, width, y), fill=(round(104 - 55*t), round(105 - 45*t), round(91 - 45*t), 255))
    draw.polygon(((0, 380), (130, 285), (280, 365), (430, 210), (610, 360),
                  (785, 260), (1024, 390), (1024, 768), (0, 768)), fill=(37, 54, 49, 255))
    draw.polygon(((0, 505), (190, 410), (375, 488), (560, 355), (760, 470),
                  (910, 405), (1024, 455), (1024, 768), (0, 768)), fill=(24, 43, 39, 255))
    draw.polygon(((0, 590), (250, 545), (480, 590), (680, 520), (1024, 610),
                  (1024, 768), (0, 768)), fill=(67, 69, 49, 255))
    draw.polygon(((0, 650), (230, 610), (470, 660), (720, 585), (1024, 670),
                  (1024, 768), (0, 768)), fill=(111, 87, 49, 255))
    rng = random.Random(10103)
    for _ in range(155):
        x = rng.randrange(width)
        base = rng.randrange(420, 705)
        crown = rng.randrange(8, 28)
        draw.line((x, base, x, base-crown*2), fill=(26, 31, 25, 190), width=rng.choice((1, 2, 3)))
        draw.ellipse((x-crown, base-crown*3, x+crown, base-crown), fill=(29, 48, 36, rng.randrange(100, 210)))
    return image.filter(ImageFilter.GaussianBlur(0.55))


def build_images(target: Path) -> dict:
    target.mkdir(parents=True, exist_ok=True)
    builders = {
        "plaster": plaster,
        "dark-oak": dark_oak,
        "terrazzo": terrazzo,
        "aged-brass": aged_brass,
        "textile": textile,
    }
    files: dict[str, dict] = {}
    for name, builder in builders.items():
        albedo, normal, packed = builder()
        for suffix, image in (("albedo", albedo), ("normal", normal), ("orm", packed)):
            path = target / f"{name}-{suffix}.png"
            image.save(path, "PNG", optimize=True, compress_level=9)
            files[path.name] = {"size": list(image.size), "mode": image.mode, "sha256": sha256(path)}
    painting = landscape()
    painting_path = target / "landscape-painting.png"
    painting.save(painting_path, "PNG", optimize=True, compress_level=9)
    files[painting_path.name] = {"size": list(painting.size), "mode": painting.mode,
                                "sha256": sha256(painting_path)}
    return {
        "schemaVersion": 1,
        "status": "visual-sample-only",
        "generator": "Pillow deterministic environment material sample",
        "runtimeDependency": False,
        "families": list(FAMILIES),
        "files": files,
    }


def build(root: Path) -> None:
    target = root / OUTPUT_RELATIVE
    manifest = build_images(target)
    (target / MANIFEST_NAME).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"ENVIRONMENT_MATERIAL_TEXTURE_BUILD_PASS families={len(FAMILIES)} files={len(manifest['files'])}")


def check(root: Path) -> None:
    target = root / OUTPUT_RELATIVE
    manifest_path = target / MANIFEST_NAME
    if not manifest_path.is_file():
        raise RuntimeError("Environment sample texture manifest is missing")
    current = json.loads(manifest_path.read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="town-rpg-environment-material-sample-") as temporary:
        candidate = build_images(Path(temporary))
        if candidate != current:
            raise RuntimeError("Environment material sample is not deterministic")
    print(f"ENVIRONMENT_MATERIAL_TEXTURE_CHECK_PASS families={len(FAMILIES)} readOnly=true")


if __name__ == "__main__":
    args = parse_args()
    project_root = Path(args.root).resolve()
    build(project_root) if args.mode == "build" else check(project_root)
