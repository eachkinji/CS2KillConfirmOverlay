"""Regression for nested Slate paint layers; no game export or GPU required."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'SourceAssets/GameStyles/calabiyau'))
from build_packs import Scene, IDENTITY


class Fixture(Scene):
    def __init__(self):
        self.data = []
        self.path = Path('fixture.json')
        self.tracks = {}
        self.omitted = set()
        self.desired_cache = {}
        self.paint_queue = None
        self.textures = []

    def add(self, kind, name, **properties):
        obj = {'Name': name, 'Type': kind, 'Properties': properties}
        self.data.append(obj)
        return obj

    def panel(self, name, children, **properties):
        slots = []
        for child, z in children:
            content = self.data.index(child)
            self.data.append({'Properties': {'ZOrder': z, 'Content': {'ObjectPath': 'fixture.' + str(content)}}})
            slots.append({'ObjectPath': 'fixture.' + str(len(self.data) - 1)})
        return self.add('CanvasPanel', name, Slots=slots, **properties)

    def paint_extra(self, obj, values, w, h, matrix, alpha, time, output, layer=0):
        if alpha > 0: output.append(obj["Name"])
        return layer

    def brush(self, brush, w, h, matrix, alpha, values, output, *args):
        if alpha > 0:
            output.append(self.current['Name'])


scene = Fixture()
nested = scene.add('Image', 'nested-foreground')
background = scene.add('Image', 'late-background')
front = scene.add('Image', 'next-z-group')
# First child starts on its parent's layer, even if its authored Z is 10.
# Add a lower-Z sibling so the nested foreground really advances one layer.
low = scene.add('Image', 'nested-background')
subtree = scene.panel('nested-canvas-2', [(low, 0), (nested, 10)])
scene.root = scene.panel('root', [(subtree, 0), (background, 0), (front, 1)])
output = []
scene.paint_ordered((120, 122), IDENTITY, 0, output)
assert output == ['nested-background', 'late-background', 'nested-foreground', 'next-z-group'], output
assert [layer for layer, _, _ in scene.last_paint_order] == [0, 0, 1, 2]

# An invisible-by-opacity subtree still reserves its Slate layers. Hidden and
# collapsed widgets, in contrast, do not participate in arranged paint layers.
subtree['Properties']['RenderOpacity'] = 0
output = []
scene.paint_ordered((120, 122), IDENTITY, 0, output)
assert output == ['late-background', 'next-z-group'], output
assert next(layer for layer, _, name in scene.last_paint_order if name == 'next-z-group') == 2
subtree['Properties']['Visibility'] = 'ESlateVisibility::Collapsed'
scene.paint_ordered((120, 122), IDENTITY, 0, [])
assert next(layer for layer, _, name in scene.last_paint_order if name == 'next-z-group') == 1
scene = Fixture()
label = scene.add('TextBlock', 'shaped-text')
frame = scene.add('Image', 'frame')
front = scene.add('Image', 'next-layer')
scene.root = scene.panel('root', [(label, 0), (frame, 0), (front, 1)])
output = []
scene.paint_ordered((120, 122), IDENTITY, 0, output)
assert output == ['frame', 'shaped-text', 'next-layer'], output
assert [layer for layer, _, _ in scene.last_paint_order] == [0, 2, 3]
print('PASS: shaped-text layers; nested same-Z sibling order, explicit Z transitions, opacity and visibility layer propagation.')

# The custom PMImage native paint implementation is unavailable. This narrowly
# scoped rule is calibrated against Flavia gameplay: Moon < icon < silver ring.
from reconstruct import EffectScene
class FlaviaFixture(Fixture):
    image_paint_layer = EffectScene.image_paint_layer

for variant in ('10502201', '10502202', '10502203', '10502204', '10502205', '10113203'):
    scene = FlaviaFixture()
    scene.path = Path('WBP_IconFeedbackPage_PC_' + variant + '.json')
    icon = scene.add('PMImage', 'Image_FeedbackIcon')
    moon = scene.add('Image', 'Moon')
    ring = scene.add('Image', 'BG_1')
    scene.root = scene.panel('root', [(icon, 0), (moon, 0), (ring, 1)])
    output = []
    scene.paint_ordered((120, 122), IDENTITY, 0, output)
    if variant.startswith('105022'):
        assert output == ['Moon', 'Image_FeedbackIcon', 'BG_1'], (variant, output)
        assert [layer for layer, _, _ in scene.last_paint_order] == [0, 1, 2]
    else:
        assert output == ['Image_FeedbackIcon', 'Moon', 'BG_1'], output
        assert [layer for layer, _, _ in scene.last_paint_order] == [0, 0, 1]

scene = EffectScene.__new__(EffectScene)
scene.star_index = lambda obj: 0
star = {'Type': 'WBP_IconFeedbackStar_Flavia_C'}
scene.star_animations = {}
assert scene.child_animation(star) == 'Anim_StarShowUp'
for count in range(2, 6):
    scene.star_animations = {0: {'AnimName': 'Anim_star_' + str(count)}}
    assert scene.child_animation(star) == 'Anim_star_' + str(count)
assert scene.child_animation({'Type': 'WBP_FeedBackSets_Count_C'}) == 'Anim_ComboKill'
print('PASS: five Flavia PMImage paint layers, unaffected-family guard, and custom moon-phase animation selection.')

class ClipFixture(Fixture):
    child_clip_rect = EffectScene.child_clip_rect
    def brush(self, brush, w, h, matrix, alpha, values, output, *args):
        output.append((self.current['Name'], self.current_clip))

scene = ClipFixture()
scene.path = Path('WBP_IconFeedbackPage_PC_10102206.json')
symbol = scene.add('Image', 'moving-symbol')
outside = scene.add('Image', 'unclipped-sibling')
window = scene.panel('window', [(symbol, 0)], Clipping='EWidgetClipping::ClipToBounds')
scene.root = scene.panel('root', [(window, 0), (outside, 1)])
output = []
scene.paint_ordered((120, 122), (2, 0, 0, 3, 10, 20), 0, output)
assert output == [('moving-symbol', (10, 20, 210, 110)), ('unclipped-sibling', None)], output
# Negative parent scale is normalized; nested windows intersect in scene space.
assert scene.child_clip_rect(window, 10, 20, (-2, 0, 0, 3, 100, 50), (85, 40, 150, 90)) == (85, 50, 100, 90)
try:
    scene.child_clip_rect(window, 10, 20, (1, .1, 0, 1, 0, 0), None)
    raise AssertionError('Rotated clip silently accepted')
except ValueError:
    pass
scene.path = Path('WBP_IconFeedbackPage_PC_10502201.json')
assert scene.child_clip_rect(window, 10, 20, IDENTITY, None) is None

from bake_effects import Bake
bake = Bake.__new__(Bake)
bake.textures = []; bake.commands = []
dest = (0, 0, 20, 20); source = (10, 20, 40, 40)
bake.command('event-icon.png', dest, source, (2, 0, 0, 2, 90, 180), clip_rect=(100, 200, 130, 230))
assert bake.commands[0][1:9] == list(dest + source), bake.commands
assert bake.commands[0][19:] == [100, 200, 30, 30]
bake.command('event-icon.png', dest, source, (2, 0, 0, 2, 90, 240), clip_rect=(100, 200, 130, 230))
assert len(bake.commands) == 1, 'Fully outside moving icon was not culled'
print('PASS: inherited clip geometry, deferred draw isolation, scaling/reflection/intersection, rotation rejection and v2 live-icon command preservation.')
