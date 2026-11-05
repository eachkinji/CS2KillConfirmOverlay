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
