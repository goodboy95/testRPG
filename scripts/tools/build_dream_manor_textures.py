"""Build and validate deterministic Dream Manor PBR atlases.

Run with the repository Python runtime, never from the game:
  python scripts/tools/build_dream_manor_textures.py build --root .
  python scripts/tools/build_dream_manor_textures.py check --root .

The three material families are deliberately compact. Each atlas contains
several authored regions selected by UVs in ``build_dream_manor_3d.py``.
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


TEXTURE_SIZE = 2048
DETAIL_SIZE = 1024
TEXTURE_RELATIVE = Path("art/blender/dream_manor/textures-v1")
MANIFEST_NAME = "dream-manor-textures-v1-manifest.json"
FILES = (
    "manor-building-albedo.png",
    "manor-building-normal.png",
    "manor-building-orm.png",
    "manor-furnishings-albedo.png",
    "manor-furnishings-normal.png",
    "manor-furnishings-orm.png",
    "manor-rug-albedo.png",
    "manor-rug-normal.png",
    "manor-rug-orm.png",
    "manor-details.png",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    parser.add_argument("--root", required=True)
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def noise_field(size: tuple[int, int], seed: int, strength: int, base: int = 128) -> Image.Image:
    rng = random.Random(seed)
    small = Image.new("L", (max(8, size[0] // 32), max(8, size[1] // 32)))
    small.putdata([max(0, min(255, base + rng.randint(-strength, strength))) for _ in range(small.width * small.height)])
    broad = small.resize(size, Image.Resampling.BICUBIC)
    fine = Image.effect_noise(size, max(1.0, strength * 0.45))
    return Image.blend(broad, fine, 0.24)


def colorize(gray: Image.Image, dark: tuple[int, int, int], light: tuple[int, int, int]) -> Image.Image:
    return Image.merge("RGB", tuple(gray.point(lambda p, a=a, b=b: int(a + (b - a) * p / 255)) for a, b in zip(dark, light)))


def normal_from_height(height: Image.Image, scale: float = 1.5) -> Image.Image:
    left = ImageChops.offset(height, -1, 0)
    right = ImageChops.offset(height, 1, 0)
    up = ImageChops.offset(height, 0, -1)
    down = ImageChops.offset(height, 0, 1)
    dx = ImageChops.subtract(left, right, scale=max(0.1, 2.0 / scale), offset=128)
    dy = ImageChops.subtract(up, down, scale=max(0.1, 2.0 / scale), offset=128)
    blue = Image.new("L", height.size, 245)
    return Image.merge("RGB", (dx, dy, blue))


def orm_from_height(height: Image.Image, roughness: int, metallic: int = 0) -> Image.Image:
    ao = ImageChops.invert(height).point(lambda p: 205 + p * 30 // 255)
    return Image.merge("RGB", (ao, Image.new("L", height.size, roughness), Image.new("L", height.size, metallic)))


def paste_region(target: Image.Image, region: Image.Image, quadrant: int) -> None:
    half = target.width // 2
    x = (quadrant % 2) * half
    y = (quadrant // 2) * half
    target.paste(region.resize((half, half), Image.Resampling.LANCZOS), (x, y))


def plaster_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 1103, 30, 143).filter(ImageFilter.GaussianBlur(0.8))
    albedo = colorize(height, (88, 84, 74), (174, 159, 133))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(1139)
    for _ in range(42):
        x = rng.randrange(size)
        y = rng.randrange(size)
        points = [(x, y)]
        for _ in range(rng.randint(2, 6)):
            x += rng.randint(-38, 38)
            y += rng.randint(20, 74)
            points.append((x, y))
        draw.line(points, fill=(42, 46, 43, rng.randint(30, 75)), width=1)
        hdraw.line(points, fill=rng.randint(70, 100), width=1)
    return albedo, height


def stone_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 1201, 22, 150)
    albedo = colorize(height, (63, 69, 67), (126, 123, 106))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    row_h = size // 8
    for row in range(9):
        y = row * row_h
        draw.line((0, y, size, y), fill=(38, 40, 39, 210), width=5)
        hdraw.line((0, y, size, y), fill=45, width=5)
        offset = 0 if row % 2 == 0 else size // 12
        for x in range(offset, size, size // 6):
            draw.line((x, y, x, min(size, y + row_h)), fill=(38, 40, 39, 180), width=4)
            hdraw.line((x, y, x, min(size, y + row_h)), fill=55, width=4)
    return albedo, height


def terrazzo_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 1301, 12, 142)
    albedo = colorize(height, (74, 65, 56), (128, 113, 91))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(1327)
    palette = [(191, 173, 139, 170), (67, 78, 75, 170), (119, 72, 48, 165), (206, 195, 168, 150)]
    for _ in range(size * 8):
        x, y = rng.randrange(size), rng.randrange(size)
        radius = rng.choice((1, 1, 2, 2, 3))
        draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=rng.choice(palette))
        hdraw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=rng.randint(135, 175))
    tile = size // 8
    for p in range(0, size, tile):
        draw.line((p, 0, p, size), fill=(35, 34, 31, 95), width=2)
        draw.line((0, p, size, p), fill=(35, 34, 31, 95), width=2)
        hdraw.line((p, 0, p, size), fill=85, width=2)
        hdraw.line((0, p, size, p), fill=85, width=2)
    return albedo, height


def slate_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 1409, 18, 130)
    albedo = colorize(height, (31, 42, 48), (75, 82, 82))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    sh = size // 14
    sw = size // 8
    for row in range(15):
        y = row * sh
        offset = -(sw // 2) if row % 2 else 0
        draw.line((0, y, size, y), fill=(15, 22, 26, 170), width=3)
        hdraw.line((0, y, size, y), fill=70, width=3)
        for x in range(offset, size, sw):
            draw.line((x, y, x, min(size, y + sh)), fill=(18, 25, 28, 130), width=2)
            hdraw.line((x, y, x, min(size, y + sh)), fill=80, width=2)
    return albedo, height


def wood_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 2101, 22, 133)
    albedo = colorize(height, (43, 25, 17), (117, 72, 43))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    rng = random.Random(2111)
    for _ in range(180):
        y = rng.randrange(size)
        bend = rng.randint(-10, 10)
        draw.line((0, y, size // 2, y + bend, size, y), fill=(28, 15, 11, rng.randint(18, 55)), width=rng.choice((1, 1, 2)))
        hdraw.line((0, y, size // 2, y + bend, size, y), fill=rng.randint(95, 125), width=1)
    return albedo, height


def brass_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 2203, 30, 146)
    albedo = colorize(height, (77, 52, 22), (175, 127, 48))
    draw = ImageDraw.Draw(albedo, "RGBA")
    rng = random.Random(2221)
    for _ in range(100):
        x, y = rng.randrange(size), rng.randrange(size)
        draw.ellipse((x-3, y-3, x+3, y+3), fill=(38, 64, 54, rng.randint(25, 80)))
    return albedo, height


def upholstery_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 2309, 18, 140)
    albedo = colorize(height, (38, 65, 61), (89, 105, 83))
    draw = ImageDraw.Draw(albedo, "RGBA")
    hdraw = ImageDraw.Draw(height)
    step = size // 24
    for p in range(-size, size * 2, step):
        draw.line((p, 0, p + size, size), fill=(181, 139, 68, 28), width=1)
        draw.line((p, size, p + size, 0), fill=(181, 139, 68, 20), width=1)
        hdraw.line((p, 0, p + size, size), fill=150, width=1)
    return albedo, height


def iron_region(size: int) -> tuple[Image.Image, Image.Image]:
    height = noise_field((size, size), 2401, 25, 125)
    albedo = colorize(height, (24, 29, 29), (74, 72, 62))
    draw = ImageDraw.Draw(albedo, "RGBA")
    rng = random.Random(2417)
    for _ in range(180):
        x, y = rng.randrange(size), rng.randrange(size)
        draw.ellipse((x-4, y-2, x+4, y+2), fill=(120, 57, 30, rng.randint(20, 70)))
    return albedo, height


def rug_images(size: int) -> tuple[Image.Image, Image.Image, Image.Image]:
    albedo = Image.new("RGB", (size, size), (75, 36, 27))
    height = noise_field((size, size), 3109, 14, 139)
    draw = ImageDraw.Draw(albedo)
    hdraw = ImageDraw.Draw(height)
    border = size // 12
    colors = [(35, 71, 70), (159, 119, 63), (177, 156, 115), (92, 44, 30)]
    draw.rectangle((border, border, size-border, size-border), outline=colors[1], width=border//3)
    draw.rectangle((border*2, border*2, size-border*2, size-border*2), outline=colors[0], width=border//5)
    cell = size // 10
    for y in range(border*2, size-border*2, cell):
        for x in range(border*2, size-border*2, cell):
            r = cell // 3
            color = colors[((x // cell) + (y // cell)) % len(colors)]
            draw.polygon(((x, y-r), (x+r, y), (x, y+r), (x-r, y)), fill=color)
            hdraw.polygon(((x, y-r), (x+r, y), (x, y+r), (x-r, y)), fill=154)
    fibers = noise_field((size, size), 3191, 25, 128)
    albedo = Image.blend(albedo, colorize(fibers, (54, 46, 39), (130, 112, 88)), 0.12)
    normal = normal_from_height(height, 0.8)
    orm = orm_from_height(height, 214)
    return albedo, normal, orm


def details_image(size: int) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(4103)
    for _ in range(260):
        x, y = rng.randrange(size), rng.randrange(size)
        radius = rng.randint(2, 16)
        draw.ellipse((x-radius, y-radius//2, x+radius, y+radius//2), fill=(54, 42, 32, rng.randint(8, 34)))
    for index in range(16):
        x = 64 + (index % 4) * 240
        y = 64 + (index // 4) * 240
        draw.rectangle((x, y, x+172, y+172), outline=(171, 132, 65, 115), width=4)
        draw.rectangle((x+12, y+12, x+160, y+160), outline=(24, 55, 56, 90), width=2)
    return image


def build_images() -> dict[str, Image.Image]:
    half = TEXTURE_SIZE // 2
    building_albedo = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    building_height = Image.new("L", (TEXTURE_SIZE, TEXTURE_SIZE))
    building_roughness = (218, 195, 198, 205)
    for index, builder in enumerate((plaster_region, stone_region, terrazzo_region, slate_region)):
        albedo, height = builder(half)
        paste_region(building_albedo, albedo, index)
        paste_region(building_height, height, index)
    building_normal = normal_from_height(building_height, 1.25)
    building_orm = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    for index, roughness in enumerate(building_roughness):
        x = (index % 2) * half
        y = (index // 2) * half
        height = building_height.crop((x, y, x+half, y+half))
        building_orm.paste(orm_from_height(height, roughness), (x, y))

    furnishing_albedo = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    furnishing_height = Image.new("L", (TEXTURE_SIZE, TEXTURE_SIZE))
    furnishing_orm = Image.new("RGB", (TEXTURE_SIZE, TEXTURE_SIZE))
    regions = ((wood_region, 176, 0), (brass_region, 118, 215), (upholstery_region, 205, 0), (iron_region, 145, 150))
    for index, (builder, roughness, metallic) in enumerate(regions):
        albedo, height = builder(half)
        paste_region(furnishing_albedo, albedo, index)
        paste_region(furnishing_height, height, index)
        x = (index % 2) * half
        y = (index // 2) * half
        furnishing_orm.paste(orm_from_height(height, roughness, metallic), (x, y))
    furnishing_normal = normal_from_height(furnishing_height, 1.0)
    rug_albedo, rug_normal, rug_orm = rug_images(TEXTURE_SIZE)
    return {
        "manor-building-albedo.png": building_albedo,
        "manor-building-normal.png": building_normal,
        "manor-building-orm.png": building_orm,
        "manor-furnishings-albedo.png": furnishing_albedo,
        "manor-furnishings-normal.png": furnishing_normal,
        "manor-furnishings-orm.png": furnishing_orm,
        "manor-rug-albedo.png": rug_albedo,
        "manor-rug-normal.png": rug_normal,
        "manor-rug-orm.png": rug_orm,
        "manor-details.png": details_image(DETAIL_SIZE),
    }


def write_images(target: Path) -> None:
    target.mkdir(parents=True, exist_ok=True)
    for filename, image in build_images().items():
        image.save(target / filename, format="PNG", optimize=False, compress_level=9)


def manifest_for(target: Path) -> dict:
    entries = {}
    for filename in FILES:
        path = target / filename
        with Image.open(path) as image:
            entries[filename] = {
                "width": image.width,
                "height": image.height,
                "mode": image.mode,
                "sha256": sha256(path),
            }
    return {
        "schemaVersion": 1,
        "generator": "scripts/tools/build_dream_manor_textures.py",
        "materialFamilies": {
            "building": ["plaster", "stone", "terrazzo", "slate"],
            "furnishings": ["dark_oak", "aged_brass", "upholstery", "dark_iron"],
            "rug": ["faded_rust", "deep_teal", "muted_cream"],
        },
        "files": entries,
    }


def build(root: Path) -> None:
    target = root / TEXTURE_RELATIVE
    write_images(target)
    manifest = manifest_for(target)
    (target / MANIFEST_NAME).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"DREAM_MANOR_TEXTURE_BUILD_PASS files={len(FILES)} size={TEXTURE_SIZE}")


def check(root: Path) -> None:
    target = root / TEXTURE_RELATIVE
    manifest_path = target / MANIFEST_NAME
    if not manifest_path.is_file():
        raise RuntimeError("Dream Manor texture manifest is missing")
    tracked = json.loads(manifest_path.read_text(encoding="utf-8"))
    current = manifest_for(target)
    if tracked != current:
        raise RuntimeError("Dream Manor texture manifest is stale")
    with tempfile.TemporaryDirectory(prefix="town-rpg-dream-manor-textures-") as temporary:
        candidate = Path(temporary)
        write_images(candidate)
        fresh = manifest_for(candidate)
        if fresh["files"] != current["files"]:
            raise RuntimeError("Dream Manor textures are not deterministic")
    print(f"DREAM_MANOR_TEXTURE_CHECK_PASS files={len(FILES)} size={TEXTURE_SIZE} readOnly=true")


def main() -> None:
    args = parse_args()
    root = Path(args.root).resolve()
    if args.mode == "build":
        build(root)
    else:
        check(root)


if __name__ == "__main__":
    main()
