"""Import and validate the CC0 material sources used by the cinematic samples.

``import-sources`` is the only networked mode. ``build`` and ``check`` work from
the adopted source bytes and never contact a remote service.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import tempfile
import urllib.request

from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps


SOURCE_ROOT = Path("art/sources/cc0/polyhaven/environment-cinematic-v1")
TEXTURE_ROOT = Path("art/blender/environment-cinematic-samples/textures-v1")
BOARD_PATH = Path("art/concepts/environment-cinematic/environment-source-board-v1.png")
SOURCE_MANIFEST = SOURCE_ROOT / "source-manifest.json"
TEXTURE_MANIFEST = TEXTURE_ROOT / "texture-manifest.json"
FONT_PATH = Path(r"C:\Windows\Fonts\msyh.ttc")

ASSETS = {
    "painted_plaster_wall": {
        "label": "风化涂层灰泥",
        "usage": "教室上墙、青绿墙裙",
        "maps": {
            "diff": ("painted_plaster_wall_diff_2k.jpg", "32f4f4e5ded7d20e55e337004d0d9691"),
            "normal": ("painted_plaster_wall_nor_gl_2k.jpg", "18faf9f7949aefdb3ae7cef7195cba04"),
            "arm": ("painted_plaster_wall_arm_2k.jpg", "b66f912016257faa7e71a1530e387545"),
        },
    },
    "old_mosaic_floor": {
        "label": "旧马赛克地面",
        "usage": "教室旧水磨石基底",
        "maps": {
            "diff": ("old_mosaic_floor_diff_2k.jpg", "ebbcab3e98b336711d1b44a3611fdcf5"),
            "normal": ("old_mosaic_floor_nor_gl_2k.jpg", "faa410e99a1cb2a4ae32918b454a90da"),
            "arm": ("old_mosaic_floor_arm_2k.jpg", "121b7ed2031b0726a08e4f772617b04c"),
        },
    },
    "oak_veneer_01": {
        "label": "橡木木皮",
        "usage": "教室课桌、黑板框",
        "maps": {
            "diff": ("oak_veneer_01_diff_2k.jpg", "0ad13b8daf8dc00318a437f56e54c484"),
            "normal": ("oak_veneer_01_nor_gl_2k.jpg", "a6b8dd99a4407aa5dd24d5eee25492d3"),
            "arm": ("oak_veneer_01_arm_2k.jpg", "e7f6ef48ffdc1b41aeff9bc6cb170532"),
        },
    },
    "rust_coarse_01": {
        "label": "粗糙锈蚀金属",
        "usage": "桌椅钢管、旧五金",
        "maps": {
            "diff": ("rust_coarse_01_diff_2k.jpg", "c1f3d0f3e8ea08bfed17bd27a5bb4a45"),
            "normal": ("rust_coarse_01_nor_gl_2k.jpg", "70e8ba5a8244cffb052b9ba194610867"),
            "arm": ("rust_coarse_01_arm_2k.jpg", "85be306b28a377c9a60217d107c3dcdf"),
        },
    },
    "beige_wall_001": {
        "label": "暖灰旧灰泥",
        "usage": "洋房门厅上墙",
        "maps": {
            "diff": ("beige_wall_001_diff_2k.jpg", "c927ac1136767f1df758ae064f50a2d1"),
            "normal": ("beige_wall_001_nor_gl_2k.jpg", "127b2f5f2f8c2e57b1f08b263c0690bd"),
            "arm": ("beige_wall_001_arm_2k.jpg", "4b2733ac3dba7944316915e180a70a24"),
        },
    },
    "black_oak_veneer": {
        "label": "深色橡木木皮",
        "usage": "洋房门、护墙板",
        "maps": {
            "diff": ("black_oak_veneer_diff_2k.jpg", "41e682d7c8e5b5598e39d5b7d100817f"),
            "normal": ("black_oak_veneer_nor_gl_2k.jpg", "c49d68106d8ebca578983744c49bda60"),
            "arm": ("black_oak_veneer_arm_2k.jpg", "a40ed5cad30616dfc55f8aab1386fd65"),
        },
    },
    "fabric_pattern_07": {
        "label": "传统织物",
        "usage": "洋房长地毯",
        "maps": {
            "diff": ("fabric_pattern_07_col_1_2k.jpg", "ad5b3f69e889a6890d6413ea1568ff25"),
            "normal": ("fabric_pattern_07_nor_gl_2k.jpg", "bf1eb2488da43265bd49e42778b6ae51"),
            "arm": ("fabric_pattern_07_arm_2k.jpg", "61384156e3e50ee66815a885c9d40c81"),
        },
    },
    "terrazzo_tiles": {
        "label": "水磨石方砖",
        "usage": "洋房门厅地面",
        "maps": {
            "diff": ("terrazzo_tiles_diff_2k.jpg", "b4bfb01525c22e611d9a0651f372ae91"),
            "normal": ("terrazzo_tiles_nor_gl_2k.jpg", "6fdd6cc116b9018108b91f3d5ddf1cff"),
            "arm": ("terrazzo_tiles_arm_2k.jpg", "0fb422cdf520645d1f842fb455915130"),
        },
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("import-sources", "build", "check"))
    parser.add_argument("--root", default=".")
    return parser.parse_args()


def md5(path: Path) -> str:
    digest = hashlib.md5()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def url_for(asset_id: str, filename: str) -> str:
    return f"https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/{asset_id}/{filename}"


def fetch(url: str, destination: Path) -> None:
    request = urllib.request.Request(url, headers={"User-Agent": "town-rpg-offline-art-pipeline/1.0"})
    destination.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(request, timeout=90) as response, destination.open("wb") as handle:
        while block := response.read(1024 * 1024):
            handle.write(block)


def import_sources(root: Path) -> None:
    source_root = root / SOURCE_ROOT
    manifest_assets = []
    for asset_id, asset in ASSETS.items():
        asset_root = source_root / asset_id
        for role, (filename, expected_md5) in asset["maps"].items():
            destination = asset_root / filename
            if not destination.is_file() or md5(destination) != expected_md5:
                fetch(url_for(asset_id, filename), destination)
            if md5(destination) != expected_md5:
                raise RuntimeError(f"Poly Haven source hash mismatch: {destination}")
        thumb = asset_root / f"{asset_id}-thumbnail.png"
        if not thumb.is_file():
            fetch(f"https://cdn.polyhaven.com/asset_img/thumbs/{asset_id}.png?width=512&height=512", thumb)
        manifest_assets.append(source_record(root, asset_id, asset, thumb))
    manifest = {
        "schemaVersion": 1,
        "provider": "Poly Haven",
        "license": "CC0 1.0 / Public Domain",
        "licenseUrl": "https://polyhaven.com/license",
        "retrievedAt": "2026-08-29",
        "redistribution": "Raw source files may be redistributed under CC0; attribution retained for provenance.",
        "assets": manifest_assets,
    }
    SOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    (root / SOURCE_MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"ENVIRONMENT_CINEMATIC_SOURCE_IMPORT_PASS assets={len(ASSETS)} maps={len(ASSETS) * 3}")


def source_record(root: Path, asset_id: str, asset: dict, thumb: Path) -> dict:
    return {
        "id": asset_id,
        "name": asset["label"],
        "usage": asset["usage"],
        "assetUrl": f"https://polyhaven.com/a/{asset_id}",
        "thumbnail": thumb.relative_to(root).as_posix(),
        "thumbnailSha256": sha256(thumb),
        "maps": {
            role: {
                "path": (SOURCE_ROOT / asset_id / filename).as_posix(),
                "url": url_for(asset_id, filename),
                "md5": expected,
                "sha256": sha256(root / SOURCE_ROOT / asset_id / filename),
                "colorSpace": "sRGB" if role == "diff" else "Non-Color",
            }
            for role, (filename, expected) in asset["maps"].items()
        },
    }


def grade(source: Image.Image, black: tuple[int, int, int], white: tuple[int, int, int],
          contrast: float = 1.0, saturation: float = 1.0) -> Image.Image:
    gray = ImageOps.grayscale(source)
    result = ImageOps.colorize(gray, black=black, white=white)
    result = ImageEnhance.Contrast(result).enhance(contrast)
    return ImageEnhance.Color(result).enhance(saturation)


GRADES = {
    "classroom-plaster": ("painted_plaster_wall", (45, 43, 37), (173, 158, 133), 1.08, 0.78),
    "classroom-teal": ("painted_plaster_wall", (8, 28, 30), (52, 91, 86), 1.12, 1.05),
    "classroom-floor": ("old_mosaic_floor", (20, 25, 25), (105, 96, 76), 1.08, 0.80),
    "classroom-wood": ("oak_veneer_01", (25, 12, 7), (132, 75, 34), 1.13, 0.92),
    "classroom-darkwood": ("oak_veneer_01", (10, 5, 3), (72, 36, 17), 1.16, 0.84),
    "classroom-metal": ("rust_coarse_01", (18, 19, 18), (93, 63, 37), 1.15, 0.72),
    "manor-plaster": ("beige_wall_001", (46, 38, 29), (155, 126, 88), 1.06, 0.72),
    "manor-wood": ("black_oak_veneer", (16, 7, 4), (96, 48, 22), 1.12, 0.88),
    "manor-floor": ("terrazzo_tiles", (30, 28, 22), (133, 112, 75), 1.06, 0.82),
    "manor-rug": ("fabric_pattern_07", (22, 12, 9), (112, 61, 33), 1.10, 0.92),
}


def build_outputs(root: Path, target: Path, board_path: Path) -> dict:
    target.mkdir(parents=True, exist_ok=True)
    files = {}
    for name, (asset_id, black, white, contrast, saturation) in GRADES.items():
        diffuse = root / SOURCE_ROOT / asset_id / ASSETS[asset_id]["maps"]["diff"][0]
        with Image.open(diffuse) as source:
            output = grade(source.convert("RGB"), black, white, contrast, saturation)
        path = target / f"{name}-albedo.png"
        output.save(path, "PNG", optimize=True, compress_level=9)
        files[path.name] = {"size": list(output.size), "mode": output.mode, "sha256": sha256(path), "source": asset_id}
    build_board(root, board_path)
    return {
        "schemaVersion": 1,
        "status": "lookdev-sample-only",
        "runtimeDependency": False,
        "sourceManifest": SOURCE_MANIFEST.as_posix(),
        "texelDensity": {"heroPixelsPerMeter": 512, "shellPixelsPerMeter": 256},
        "files": files,
        "board": {"path": BOARD_PATH.as_posix(), "sha256": sha256(board_path)},
    }


def font(size: int) -> ImageFont.FreeTypeFont:
    if not FONT_PATH.is_file():
        raise RuntimeError(f"Required font missing: {FONT_PATH}")
    return ImageFont.truetype(str(FONT_PATH), size)


def build_board(root: Path, destination: Path) -> None:
    canvas = Image.new("RGB", (3840, 2160), (12, 18, 23))
    draw = ImageDraw.Draw(canvas)
    draw.text((72, 48), "电影式半写实环境素材候选 · 第一验收点", font=font(44), fill=(234, 225, 204))
    draw.text((72, 112), "CC0 扫描材质直接采用 / 付费资产暂不购买 / 地域与谜题核心物件自制",
              font=font(25), fill=(154, 176, 178))
    card_w, card_h = 880, 760
    for index, (asset_id, asset) in enumerate(ASSETS.items()):
        column, row = index % 4, index // 4
        x, y = 60 + column * 945, 190 + row * 820
        draw.rounded_rectangle((x, y, x + card_w, y + card_h), radius=18, fill=(22, 31, 37), outline=(65, 91, 92), width=2)
        thumb = root / SOURCE_ROOT / asset_id / f"{asset_id}-thumbnail.png"
        with Image.open(thumb) as source:
            preview = ImageOps.fit(source.convert("RGB"), (820, 500), Image.Resampling.LANCZOS)
        canvas.paste(preview, (x + 30, y + 30))
        draw.text((x + 30, y + 554), asset["label"], font=font(29), fill=(238, 224, 194))
        draw.text((x + 30, y + 600), asset["usage"], font=font(23), fill=(185, 197, 193))
        draw.text((x + 30, y + 645), f"Poly Haven · {asset_id}", font=font(20), fill=(120, 158, 161))
        draw.text((x + 30, y + 684), "CC0 · 2K Albedo / Normal(GL) / ARM", font=font(20), fill=(215, 172, 92))
    draw.rounded_rectangle((60, 1845, 1835, 2085), radius=18, fill=(27, 29, 30), outline=(128, 104, 60), width=2)
    draw.text((92, 1876), "付费待审批", font=font(28), fill=(226, 186, 104))
    draw.text((92, 1924), "首轮不建议购买。Fab / BlenderKit 仅在 CC0 样板无法满足英雄小件时再提交价格与许可。",
              font=font(22), fill=(199, 194, 181))
    draw.rounded_rectangle((1900, 1845, 3780, 2085), radius=18, fill=(24, 32, 35), outline=(55, 99, 92), width=2)
    draw.text((1932, 1876), "项目自制", font=font(28), fill=(132, 191, 174))
    draw.text((1932, 1924), "课桌、地图柜、黑板机关、值日表、时钟、洋房门、护墙板、壁炉与全部谜题文字。",
              font=font(22), fill=(199, 205, 193))
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, "PNG", optimize=True, compress_level=9)


def build(root: Path) -> None:
    validate_sources(root)
    manifest = build_outputs(root, root / TEXTURE_ROOT, root / BOARD_PATH)
    (root / TEXTURE_MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"ENVIRONMENT_CINEMATIC_TEXTURE_BUILD_PASS materials={len(GRADES)} assets={len(ASSETS)}")


def validate_sources(root: Path) -> None:
    manifest_path = root / SOURCE_MANIFEST
    if not manifest_path.is_file():
        raise RuntimeError("Run import-sources before build/check")
    current = json.loads(manifest_path.read_text(encoding="utf-8"))
    if current.get("license") != "CC0 1.0 / Public Domain" or len(current.get("assets", [])) != len(ASSETS):
        raise RuntimeError("Source manifest is incomplete")
    for asset_id, asset in ASSETS.items():
        for filename, expected in asset["maps"].values():
            path = root / SOURCE_ROOT / asset_id / filename
            if not path.is_file() or md5(path) != expected:
                raise RuntimeError(f"Source file is missing or changed: {path}")


def check(root: Path) -> None:
    validate_sources(root)
    current = json.loads((root / TEXTURE_MANIFEST).read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="town-rpg-cinematic-materials-") as temporary:
        temp_root = Path(temporary)
        candidate = build_outputs(root, temp_root / "textures", temp_root / "board.png")
    if candidate != current:
        raise RuntimeError("Cinematic environment materials are not deterministic")
    print(f"ENVIRONMENT_CINEMATIC_SOURCE_CHECK_PASS assets={len(ASSETS)} materials={len(GRADES)} readOnly=true")


if __name__ == "__main__":
    arguments = parse_args()
    project_root = Path(arguments.root).resolve()
    if arguments.mode == "import-sources":
        import_sources(project_root)
    elif arguments.mode == "build":
        build(project_root)
    else:
        check(project_root)
