"""Create pack thumbnails from production Win2D validation captures (Pillow)."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('validation', type=Path)
    parser.add_argument('--variants', help='Comma-separated variants for a focused native validation')
    args = parser.parse_args()
    assert (args.validation/'pass.txt').is_file(), 'Run --validate-calabiyau successfully first'
    packs = json.loads((HERE/'packs.json').read_text(encoding='utf8'))
    if args.variants:
        selected = set(args.variants.split(','))
        packs = [pack for pack in packs if pack['variant'] in selected]
        assert {pack['variant'] for pack in packs} == selected, 'Unknown variant requested'
    groups = {}
    for pack in packs:
        image = Image.open(args.validation/'variants'/(pack['variant']+'.png')).convert('RGBA')
        bounds = image.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
        assert bounds, pack['key']
        image = image.crop(bounds)
        image.thumbnail((224, 224), Image.Resampling.LANCZOS)
        preview = Image.new('RGBA', (256, 256))
        preview.alpha_composite(image, ((256-image.width)//2, (256-image.height)//2))
        preview.save(HERE/'iconpacks'/pack['variant']/'preview-v2.png')
        groups.setdefault(pack['group'], (pack, preview))
    montage = Image.new('RGB', (1200, ((len(groups)+5)//6)*180), (24,28,35))
    draw = ImageDraw.Draw(montage)
    for i, (pack, preview) in enumerate(groups.values()):
        image = preview.copy()
        image.thumbnail((150,150), Image.Resampling.LANCZOS)
        x, y = i%6*200, i//6*180
        montage.paste(image, (x+25,y), image)
        draw.text((x+6,y+148), pack['group'], fill='white')
        draw.text((x+6,y+164), pack['variant'], fill=(150,160,170))
    montage.save(args.validation/'montage.png')
    print('Created',len(packs),'native-rendered thumbnails and',len(groups),'theme montage entries.')


if __name__ == '__main__':
    main()
