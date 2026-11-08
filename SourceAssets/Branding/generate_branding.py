"""Reproducible application branding from the supplied SVG; no game logos."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont
import resvg_py

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
SOURCE = HERE / 'KCF_logo_vector.svg'
NORMALIZED = HERE / 'KCF_logo_icon.svg'
MARGIN = 1 / 16  # 6.25% safety margin on the long axis, preserving the art's aspect.
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
ASSETS = {
    'Widget/Assets/Square44x44Logo.scale-200.png': (88, 88, 88),
    'Widget/Assets/Square150x150Logo.scale-200.png': (300, 300, 300),
    'Widget/Assets/Square44x44Logo.targetsize-24_altform-unplated.png': (24, 24, 24),
    'Widget/Assets/Wide310x150Logo.scale-200.png': (620, 300, 300),
    'Widget/Assets/StoreLogo.png': (50, 50, 50),
    'Widget/Assets/LockScreenLogo.scale-200.png': (48, 48, 48),
    'Widget/Assets/SplashScreen.scale-200.png': (1240, 600, 300),
    'Widget/GameBar/Square44x44Logo.png': (88, 88, 88),
}


def png_bytes(image: Image.Image) -> bytes:
    stream = io.BytesIO()
    image.save(stream, format='PNG', optimize=False, compress_level=9)
    return stream.getvalue()


def normalized_svg() -> tuple[bytes, tuple[int, int, int, int]]:
    original = SOURCE.read_bytes()
    tree = ET.fromstring(original)
    view = tuple(float(value) for value in tree.attrib['viewBox'].split())
    # Bounds are measured on a deterministic native-coordinate render. The source
    # remains byte-for-byte unchanged; only the derived SVG viewport is cropped.
    probe = Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_string=original.decode('utf-8'),
        width=round(view[2]), height=round(view[3]), skip_system_fonts=True))).convert('RGBA')
    bounds = probe.getchannel('A').getbbox()
    if bounds is None:
        raise ValueError('The supplied SVG has no visible artwork.')
    left, top, right, bottom = bounds
    side = max(right - left, bottom - top) / (1 - 2 * MARGIN)
    center_x, center_y = (left + right) / 2 + view[0], (top + bottom) / 2 + view[1]
    tree.set('viewBox', f'{center_x-side/2:.6f} {center_y-side/2:.6f} {side:.6f} {side:.6f}')
    tree.set('width', '1024')
    tree.set('height', '1024')
    ET.register_namespace('', 'http://www.w3.org/2000/svg')
    return ET.tostring(tree, encoding='utf-8', xml_declaration=True) + b'\n', bounds


def render(svg: bytes, width: int, height: int, edge: int) -> Image.Image:
    # resvg antialiases each target directly from the original paths. Do not
    # derive small icons from another raster or add resize halos at the edge.
    icon = Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_string=svg.decode('utf-8'),
        width=edge, height=edge, skip_system_fonts=True))).convert('RGBA')
    canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    canvas.alpha_composite(icon, ((width - edge) // 2, (height - edge) // 2))
    bounds = canvas.getchannel('A').getbbox()
    if bounds is None or min(bounds[:2]) < 1 or bounds[2] >= width or bounds[3] >= height:
        raise ValueError(f'Clipped or missing artwork at {width}x{height}: {bounds}')
    if canvas.getpixel((0, 0))[3] != 0 or canvas.getchannel('A').getextrema()[1] != 255:
        raise ValueError('Transparency or opaque artwork was lost.')
    return canvas


def ico_bytes(frames: dict[int, bytes]) -> bytes:
    # Windows Vista+ accepts lossless PNG frames at every icon size. Explicit
    # headers keep the exact vector-rendered frame, with no implicit ICO resizing.
    header = struct.pack('<HHH', 0, 1, len(frames))
    offset = 6 + len(frames) * 16
    directory, payload = bytearray(), bytearray()
    for size, data in frames.items():
        directory.extend(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        payload.extend(data)
        offset += len(data)
    return header + directory + payload


def preview(svg: bytes, directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGB', (1000, 600), '#edf0f5')
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 17)
    small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 14)
    draw.text((24, 18), 'KCF application branding / original SVG colors + transparent assets', font=font, fill='#17202d')
    for column, color in enumerate(('#ffffff', '#17202d', '#d9dfe8')):
        left = 24 + column * 326
        draw.rounded_rectangle((left, 58, left + 300, 382), radius=18, fill=color)
        if column == 2:
            for y in range(76, 364, 24):
                for x in range(left + 6, left + 294, 24):
                    if ((x-left-6)//24 + (y-76)//24) % 2:
                        draw.rectangle((x, y, x+23, y+23), fill='#f3f5f8')
        sheet.paste(render(svg, 256, 256, 256), (left + 22, 89), render(svg, 256, 256, 256))
    draw.text((24, 408), 'Actual pixel sizes (88 / 64 / 48 / 32 / 24 / 16)', font=font, fill='#17202d')
    left = 24
    for size in (88, 64, 48, 32, 24, 16):
        image = render(svg, size, size, size)
        sheet.paste(image, (left, 450 + (88-size)//2), image)
        draw.text((left, 550), str(size), font=small, fill='#17202d')
        left += size + 48
    sheet.save(directory / 'branding-preview.png')
    render(svg, 512, 512, 512).save(directory / 'logo-transparent.png')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Compare every generated byte without changing assets.')
    parser.add_argument('--preview-dir', type=Path)
    options = parser.parse_args()
    svg, bounds = normalized_svg()
    outputs = {NORMALIZED: svg}
    records = []
    for relative, (width, height, edge) in ASSETS.items():
        image = render(svg, width, height, edge)
        outputs[ROOT / relative] = png_bytes(image)
        records.append({'path': relative, 'size': [width, height], 'alpha_bounds': list(image.getchannel('A').getbbox())})
    icon = ico_bytes({size: png_bytes(render(svg, size, size, size)) for size in ICO_SIZES})
    outputs[ROOT / 'Installer/Assets/KillConfirmOverlay.ico'] = icon
    # Pillow independently parses the ICO directory and every embedded PNG.
    decoded = Image.open(io.BytesIO(icon))
    if decoded.ico.sizes() != {(size, size) for size in ICO_SIZES}:
        raise ValueError('ICO directory does not contain every required size.')
    for size in ICO_SIZES:
        if decoded.ico.getimage((size, size)).mode != 'RGBA':
            raise ValueError('ICO transparency was lost.')
    manifest = {
        'source': 'KCF_logo_vector.svg',
        'source_origin': 'E:/Zac/Download/KCF_logo_vector.svg',
        'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'renderer': 'resvg-py 0.5.0 + Pillow 12.3.0',
        'original_alpha_bounds': list(bounds), 'icon_margin_per_side': MARGIN,
        'ico_sizes': list(ICO_SIZES), 'png_assets': records,
        'sha256': {str(path.relative_to(ROOT)).replace('\\', '/'): hashlib.sha256(data).hexdigest()
                   for path, data in outputs.items()},
    }
    outputs[HERE / 'manifest.json'] = (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    for path, data in outputs.items():
        if options.check:
            if not path.exists() or path.read_bytes() != data:
                raise SystemExit(f'Branding asset differs: {path}')
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    if options.preview_dir:
        preview(svg, options.preview_dir)
    print(f'PASS: {len(ASSETS)} transparent PNGs, {len(ICO_SIZES)} exact ICO frames, cropped SVG; '
          + ('byte-for-byte reproducible.' if options.check else 'generated and decoded.'))


if __name__ == '__main__':
    main()
