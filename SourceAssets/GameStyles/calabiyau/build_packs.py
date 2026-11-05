"""Bake extracted UMG Anim_Normal / KillHitPanelAnim into portable sprite commands.

Input: CUE4Parse JSON exports and decoded atlas PNGs in the research directory.
Usage: python build_packs.py <CalabiyauResearch>
Requires Pillow. No game executable or hit-feedback animation is shipped.
"""
import argparse
import bisect
import gzip
import hashlib
import json
import math
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
FPS = 60
IDENTITY = (1, 0, 0, 1, 0, 0)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def mul(a, b):
    return (a[0]*b[0]+a[1]*b[2], a[0]*b[1]+a[1]*b[3],
            a[2]*b[0]+a[3]*b[2], a[2]*b[1]+a[3]*b[3],
            a[4]*b[0]+a[5]*b[2]+b[4], a[4]*b[1]+a[5]*b[3]+b[5])


def translate(x, y):
    return (1, 0, 0, 1, x, y)


def xy(obj, default=0):
    return obj.get('X', default), obj.get('Y', default)


def sample(curve, seconds, fallback):
    if not curve:
        return fallback
    times = [t['Value'] for t in curve.get('Times', [])]
    if not times:
        return curve.get('DefaultValue', fallback) if curve.get('bHasDefaultValue') else fallback
    rate = curve.get('TickResolution', {})
    tick = seconds * rate.get('Numerator', 60000) / rate.get('Denominator', 1)
    vals = [v if isinstance(v, dict) else {'Value': v, 'InterpMode': 1} for v in curve['Values']]
    if tick <= times[0]:
        return vals[0]['Value']
    if tick >= times[-1]:
        return vals[-1]['Value']
    i = bisect.bisect_right(times, tick)-1
    a, b = vals[i:i+2]
    dt = times[i+1]-times[i]
    u = (tick-times[i])/dt
    if a['InterpMode'] == 1:
        return a['Value']
    if a['InterpMode'] != 2:
        return a['Value']+(b['Value']-a['Value'])*u
    return ((2*u**3-3*u**2+1)*a['Value'] + (u**3-2*u*u+u)*dt*a.get('Tangent', {}).get('LeaveTangent', 0)
            + (-2*u**3+3*u*u)*b['Value'] + (u**3-u*u)*dt*b.get('Tangent', {}).get('ArriveTangent', 0))


class Assets:
    def __init__(self, research):
        self.research = research
        self.sprites, self.atlases, self.crops = {}, {}, {}
        for path in (research/'theme-atlas-json').rglob('*.json'):
            for obj in read(path):
                if obj['Type'] == 'PaperSprite':
                    key = '/Game/'+str(path.relative_to(research/'theme-atlas-json'/'extracted'/'PM'/'Content').with_suffix('')).replace('\\', '/')
                    self.sprites[key] = obj['Properties']
        (HERE/'textures').mkdir(exist_ok=True)

    def crop(self, brush):
        ref = brush.get('ResourceObject', {}).get('ObjectPath', '')
        if ref not in self.sprites:
            return None
        if ref in self.crops:
            return self.crops[ref]
        p = self.sprites[ref]
        texture = p['BakedSourceTexture']['ObjectPath']
        if texture not in self.atlases:
            file = self.research/'theme-atlas-png'/'extracted'/'PM'/'Content'/(texture.removeprefix('/Game/')+'.png')
            if not file.exists() and '/HUDPC/' in texture:
                file = self.research/'preview'/'assets'/'hud-atlas.png'
            self.atlases[texture] = Image.open(file).convert('RGBA')
        uv, size = p['BakedSourceUV'], p['BakedSourceDimension']
        x, y, w, h = [int(round(v)) for v in (uv['X'], uv['Y'], size['X'], size['Y'])]
        atlas = self.atlases[texture]
        assert 0 <= x < x+w <= atlas.width and 0 <= y < y+h <= atlas.height, ref
        image = atlas.crop((x, y, x+w, y+h))
        name = hashlib.sha256(image.tobytes()+str(image.size).encode()).hexdigest()[:24]+'.png'
        if not (HERE/'textures'/name).exists():image.save(HERE/'textures'/name)
        self.crops[ref] = (name, w, h)
        return self.crops[ref]


class Scene:
    def __init__(self, path, assets, crosshair=False, animation_name=None, root_name=None):
        self.path = path
        self.data, self.assets = read(path), assets
        self.crosshair = crosshair
        self.nodes = {o['Name']: o for o in self.data if ':WidgetTree\'' in o.get('Outer', {}).get('ObjectName', '')}
        self.root = self.nodes[root_name or ('KillHitPanel' if crosshair else 'CanvasPanel_Root')]
        name = animation_name or ('KillHitPanelAnim' if crosshair else 'Anim_Normal')
        self.scene = next(o for o in self.data if o['Type'] == 'MovieScene' and o['Name'] == name)
        animation = next(o for o in self.data if o['Type'] == 'WidgetAnimation' and o['Name'] == name+'_INST')
        bindings = {b['AnimationGuid']: b for b in animation['Properties'].get('AnimationBindings',[])}
        self.tracks, self.omitted, self.textures = {}, set(), []
        self.duration = round(self.scene['Properties']['PlaybackRange']['Value']['UpperBound']['Value']['Value']/60000, 3)
        assert 0 < self.duration <= 15
        for binding in self.scene['Properties'].get('ObjectBindings', []):
            # BindingName is an editor label and often names a deleted widget.
            # Only AnimationBindings resolve the live widget/slot by GUID.
            if binding['ObjectGuid'] not in bindings:continue # Deleted possessable, no live UMG target.
            name = bindings[binding['ObjectGuid']]['WidgetName']
            # This legacy alias is present in the default UMG animation as well.
            if name == 'WidgetSwitcher_Icon' and name not in self.nodes:
                name = 'Image_FeedbackIcon'
            for ref in binding['Tracks']:
                track = self.ref(ref)['Properties']
                if self.ref(ref)['Type'] == 'MovieSceneWidgetMaterialTrack':
                    for section in track.get('Sections', []):
                        self.tracks.setdefault(name, []).append(('__material__', self.ref(section)['Properties']))
                    continue
                if 'PropertyPath' not in track:
                    self.omitted.add(self.ref(ref)['Type'])
                    continue
                for section in track.get('Sections', []):
                    self.tracks.setdefault(name, []).append((track['PropertyPath'], self.ref(section)['Properties']))
        self.desired_cache = {}
        self.paint_queue = None

    def submit_paint(self, layer, obj, time, callback):
        # Slate sorts all draw elements by their final LayerId, not by widget
        # tree traversal. A same-Z canvas sibling can paint below a nested
        # child visited earlier. Keep insertion order only within one layer.
        def draw():
            self.current, self.time = obj, time
            callback()
        if self.paint_queue is None:
            draw()
        else:
            self.paint_queue.append((layer, self.path.stem, obj['Name'], draw))

    def paint_order_key(self, item):
        return (item[0],)

    def paint_ordered(self, parent_size, matrix, time, output):
        queue = []
        self.paint_queue = queue
        try:
            self.paint(self.root, {}, parent_size, matrix, 1, time, output, True)
            ordered = sorted(queue, key=self.paint_order_key)
            self.last_paint_order = [(layer, page, node) for layer,page,node,_ in ordered]
            for _,_,_,draw in ordered:draw()
        finally:
            self.paint_queue = None

    def ref(self, ref):
        return self.data[int(ref['ObjectPath'].rsplit('.', 1)[1])]

    def children(self, obj):
        slots = [self.ref(r)['Properties'] for r in obj.get('Properties', {}).get('Slots', [])]
        return [(self.ref(s['Content']), s) for s in sorted(slots, key=lambda s:s.get('ZOrder', 0))]

    def desired(self, obj):
        name = obj['Name']
        if name in self.desired_cache:
            return self.desired_cache[name]
        p = obj.get('Properties', {})
        if obj['Type'] in ('Image', 'PMImage'):
            result = xy(p.get('Brush', {}).get('ImageSize', {}), 32)
        else:
            result = [0, 0]
            for child, slot in self.children(obj):
                if obj['Type'] in ('HorizontalBox', 'Overlay'):
                    size = self.desired(child)
                    padding = slot.get('Padding', {})
                    size = [size[0]+padding.get('Left',0)+padding.get('Right',0), size[1]+padding.get('Top',0)+padding.get('Bottom',0)]
                    result[0] = result[0]+size[0] if obj['Type'] == 'HorizontalBox' else max(result[0],size[0])
                    result[1] = max(result[1],size[1])
                    continue
                layout = slot.get('LayoutData', {})
                offsets = layout.get('Offsets', {})
                size = self.desired(child) if slot.get('bAutoSize') else (offsets.get('Right', 100), offsets.get('Bottom', 30))
                anchors = layout.get('Anchors', {})
                lo, hi = xy(anchors.get('Minimum', {})), xy(anchors.get('Maximum', {}))
                for i, field in enumerate(('Left', 'Top')):
                    offset = abs(offsets.get(field, 0)) if lo[i] == hi[i] and lo[i] in (0, 1) else 0
                    result[i] = max(result[i], max(0, size[i])+offset)
        self.desired_cache[name] = result
        return result

    def values(self, obj, slot, time):
        p = obj.get('Properties', {})
        rt = p.get('RenderTransform', {})
        layout = slot.get('LayoutData', {})
        offsets = layout.get('Offsets', {})
        color = p.get('ColorAndOpacity', {})
        v = dict(x=offsets.get('Left', 0), y=offsets.get('Top', 0), w=offsets.get('Right', 100), h=offsets.get('Bottom', 30),
                 tx=rt.get('Translation', {}).get('X', 0), ty=rt.get('Translation', {}).get('Y', 0),
                 sx=rt.get('Scale', {}).get('X', 1), sy=rt.get('Scale', {}).get('Y', 1), angle=rt.get('Angle', 0),
                 shx=rt.get('Shear', {}).get('X', 0), shy=rt.get('Shear', {}).get('Y', 0),
                 px=p.get('RenderTransformPivot', {}).get('X', .5), py=p.get('RenderTransformPivot', {}).get('Y', .5),
                 opacity=p.get('RenderOpacity', 1), visible=1 if p.get('Visibility') in ('ESlateVisibility::Hidden', 'ESlateVisibility::Collapsed') else 0,
                 r=color.get('R', 1), g=color.get('G', 1), b=color.get('B', 1), a=color.get('A', 1), percent=p.get('Percent', 0))
        # Normal-kill entry panels are activated by the game page controller;
        # explicit visibility tracks below still take precedence.
        if obj['Name'] in ('CanvasPanel_48', 'CanvasPanel_Injured'):
            v['visible'] = 0
        for prop, section in self.tracks.get(obj['Name'], []):
            mapping = {'RenderOpacity': [('opacity', 'FloatCurve')], 'Visibility': [('visible', 'ByteCurve')],
                       'Percent': [('percent', 'FloatCurve')],
                       'ColorAndOpacity': [('r', 'RedCurve'), ('g', 'GreenCurve'), ('b', 'BlueCurve'), ('a', 'AlphaCurve')],
                       'RenderTransformPivot': [('px', 'Curves'), ('py', 'Curves[1]')],
                       'LayoutData.Offsets': [('x', 'LeftCurve'), ('y', 'TopCurve'), ('w', 'RightCurve'), ('h', 'BottomCurve')]}.get(prop, [])
            if prop == 'RenderTransform':
                mask = section.get('TransformMask', {}).get('Mask', 127)
                mapping = [(field, key) for bit, field, key in [(1, 'tx', 'Translation'), (2, 'ty', 'Translation[1]'), (4, 'angle', 'Rotation'),
                           (8, 'sx', 'Scale'), (16, 'sy', 'Scale[1]'), (32, 'shx', 'Shear'), (64, 'shy', 'Shear[1]')] if mask & bit]
            for field, key in mapping:
                v[field] = sample(section.get(key), time, v[field])
        return v

    def paint(self, obj, slot, parent_size, parent_matrix, opacity, time, output, root=False, layer=0):
        p, kind = obj.get('Properties', {}), obj['Type']
        if kind not in ('CanvasPanel', 'Overlay', 'HorizontalBox', 'Image', 'PMImage', 'ProgressBar', 'SizeBox', 'ScaleBox', 'Border', 'Spacer','WidgetSwitcher') and not (hasattr(self, 'paint_extra') and (kind in ('SimpleMeshWidget','ParticleSystemWidget','TextBlock') or kind.startswith('WBP_'))):
            self.omitted.add(kind)
            return layer
        v = self.values(obj, slot, time)
        # The page controller makes its root visible when dispatching a kill.
        # Descendant visibility still follows the asset and Anim_Normal tracks.
        if not root and v['visible'] in (1, 2):
            return layer
        alpha = opacity*v['opacity']
        # Zero render opacity does not remove a Slate widget from layout or
        # from its parent's maximum LayerId; collapsed/hidden visibility does.
        if alpha <= .0001 and self.paint_queue is None:
            return layer
        layout = slot.get('LayoutData', {})
        anchors = layout.get('Anchors', {})
        lo, hi = xy(anchors.get('Minimum', {})), xy(anchors.get('Maximum', {}))
        align = xy(layout.get('Alignment', {}))
        if root:
            x, y, w, h = 0, 0, *parent_size
        else:
            size = self.desired(obj) if slot.get('bAutoSize') else (v['w'], v['h'])
            w = size[0] if lo[0] == hi[0] else parent_size[0]*(hi[0]-lo[0])-v['x']-v['w']
            h = size[1] if lo[1] == hi[1] else parent_size[1]*(hi[1]-lo[1])-v['y']-v['h']
            # SConstraintCanvas ignores alignment on a stretched axis.
            x = parent_size[0]*lo[0]+v['x']-(w*align[0] if lo[0] == hi[0] else 0)
            y = parent_size[1]*lo[1]+v['y']-(h*align[1] if lo[1] == hi[1] else 0)
        pivot = (w*v['px'], h*v['py'])
        a = math.radians(v['angle'])
        matrix = translate(-pivot[0], -pivot[1])
        matrix = mul(matrix, (v['sx'], 0, 0, v['sy'], 0, 0))
        matrix = mul(matrix, (1, math.tan(math.radians(v['shy'])), math.tan(math.radians(v['shx'])), 1, 0, 0))
        matrix = mul(matrix, (math.cos(a), math.sin(a), -math.sin(a), math.cos(a), 0, 0))
        matrix = mul(mul(matrix, translate(x+pivot[0]+v['tx'], y+pivot[1]+v['ty'])), parent_matrix)
        max_layer = layer
        if hasattr(self, 'paint_extra'):
            if kind.startswith('WBP_'):
                max_layer = max(max_layer, self.paint_extra(obj, v, w, h, matrix, alpha, time, output, layer))
            elif kind in ('SimpleMeshWidget','ParticleSystemWidget','TextBlock'):
                draw_layer=layer
                if kind=='TextBlock':
                    # UTextBlock defaults to shaped text (not SimpleTextMode).
                    # FSlateTextLayout reserves one layer for line highlights,
                    # then FSlateTextRun increments again before its glyphs.
                    shadow=p.get('ShadowColorAndOpacity',{}).get('A',0)>0 and any(xy(p.get('ShadowOffset',{}),1))
                    draw_layer+= (0 if p.get('bSimpleTextMode',False) else 2)+(1 if shadow else 0)
                    max_layer=draw_layer
                self.submit_paint(draw_layer, obj, time, lambda:self.paint_extra(obj, v, w, h, matrix, alpha, time, output, draw_layer))
        if kind in ('Image', 'PMImage'):
            self.submit_paint(layer, obj, time, lambda:self.brush(p.get('Brush', {}), w, h, matrix, alpha, v, output))
        elif kind == 'ProgressBar':
            style = p.get('WidgetStyle', {})
            self.submit_paint(layer, obj, time, lambda:self.brush(style.get('BackgroundImage', {}), w, h, matrix, alpha, v, output))
            fraction = max(0, min(1, v['percent']))
            fill_color = p.get('FillColorAndOpacity', {})
            fv = dict(v, **{k:v[k]*fill_color.get(k.upper(), 1) for k in ('r','g','b','a')})
            self.submit_paint(layer+1, obj, time, lambda:self.brush(style.get('FillImage', {}), w, h, matrix, alpha, fv, output, fraction, p.get('BarFillType', '')))
            max_layer = layer + (1 if fraction > 0 else 0)
        elif kind == 'Border':
            self.submit_paint(layer, obj, time, lambda:self.brush(p.get('Background', {}), w, h, matrix, alpha, v, output))
        children = self.children(obj)
        cursor = 0
        fixed = sum(self.desired(c)[0]+s.get('Padding',{}).get('Left',0)+s.get('Padding',{}).get('Right',0)
                    for c,s in children if s.get('Size',{}).get('SizeRule') != 'ESlateSizeRule::Fill')
        weight = sum(s.get('Size',{}).get('Value',1) for _,s in children if s.get('Size',{}).get('SizeRule') == 'ESlateSizeRule::Fill')
        child_layer, last_z, visible_children = layer, None, 0
        for child, childslot in children:
            # The cooked game's DefaultEngine.ini explicitly enables this:
            # [/Script/Slate.SlateSettings] bExplicitCanvasChildZOrder=True.
            # Match SConstraintCanvas, SOverlay and SPanel layer propagation.
            child_visible = self.values(child, childslot, time)['visible'] not in (1, 2)
            if kind == 'CanvasPanel' and child_visible:
                z = childslot.get('ZOrder', 0)
                if last_z is not None and z > last_z + .00001:child_layer = max_layer + 1
                last_z = z
            elif kind == 'Overlay' and child_visible:
                child_layer = max_layer + (1 if visible_children else 0)
            elif kind == 'Border':child_layer = layer + 1
            elif kind != 'CanvasPanel':child_layer = layer
            if kind in ('HorizontalBox', 'Overlay', 'WidgetSwitcher'):
                cw,ch = self.desired(child)
                pad = childslot.get('Padding',{})
                pl,pt,pr,pb = [pad.get(k,0) for k in ('Left','Top','Right','Bottom')]
                available_w = w
                if kind == 'HorizontalBox':
                    fill = childslot.get('Size',{}).get('SizeRule') == 'ESlateSizeRule::Fill'
                    available_w = max(0,w-fixed)*childslot.get('Size',{}).get('Value',1)/max(1,weight) if fill else cw+pl+pr
                halign = childslot.get('HorizontalAlignment', 'HAlign_Fill')
                valign = childslot.get('VerticalAlignment', 'VAlign_Fill')
                outw = max(0,available_w-pl-pr) if halign.endswith('Fill') else cw
                outh = max(0,h-pt-pb) if valign.endswith('Fill') else ch
                cx,cy = cursor+pl,pt
                if halign.endswith('Center'): cx += (available_w-pl-pr-outw)/2
                elif halign.endswith('Right'): cx += available_w-pl-pr-outw
                if valign.endswith('Center'): cy += (h-pt-pb-outh)/2
                elif valign.endswith('Bottom'): cy += h-pt-pb-outh
                childslot = {'LayoutData': {'Offsets': {'Left':cx,'Top':cy,'Right':outw,'Bottom':outh}}}
                if kind == 'HorizontalBox': cursor += available_w
            max_layer = max(max_layer, self.paint(child, childslot, (w, h), matrix, alpha, time, output, layer=child_layer))
            if child_visible:visible_children += 1
        return max_layer

    def brush(self, brush, w, h, matrix, alpha, v, output, fraction=1, fill_type=''):
        tint = brush.get('TintColor', {}).get('SpecifiedColor', {})
        alpha *= v['a']*tint.get('A', 1)
        if alpha <= .0001 or w <= 0 or h <= 0 or fraction <= 0 or brush.get('DrawAs') == 'ESlateBrushDrawType::NoDrawType':
            return
        sprite = self.assets.crop(brush)
        if sprite is None:
            if brush.get('ResourceObject'):
                self.omitted.add(brush['ResourceObject'].get('ObjectName', '').split("'")[0])
            return
        name, sw, sh = sprite
        if name not in self.textures:
            self.textures.append(name)
        index = self.textures.index(name)
        color = [v[k]*tint.get(k.upper(), 1) for k in ('r','g','b')]
        # Original UMG colors are linear; Win2D bitmap tint works in sRGB.
        color = [12.92*c if c <= .0031308 else 1.055*max(0,c)**(1/2.4)-.055 for c in color]
        rects = [(0, 0, w, h, 0, 0, sw, sh)]
        margin = brush.get('Margin', {})
        if brush.get('DrawAs') in ('ESlateBrushDrawType::Box','ESlateBrushDrawType::Border') and margin:
            left, top, right, bottom = [margin.get(k, 0) for k in ('Left','Top','Right','Bottom')]
            xs, ys = [0, left*sw, sw*(1-right), sw], [0, top*sh, sh*(1-bottom), sh]
            scale_x = min(1, w/max(.0001, sw*(left+right)))
            scale_y = min(1, h/max(.0001, sh*(top+bottom)))
            xd, yd = [0, left*sw*scale_x, w-right*sw*scale_x, w], [0, top*sh*scale_y, h-bottom*sh*scale_y, h]
            rects = [(xd[x],yd[y],xd[x+1]-xd[x],yd[y+1]-yd[y],xs[x],ys[y],xs[x+1]-xs[x],ys[y+1]-ys[y])
                     for y in range(3) for x in range(3) if not (x==y==1 and brush.get('DrawAs') == 'ESlateBrushDrawType::Border')]
        for dx,dy,dw,dh,sx,sy,rw,rh in rects:
            if min(dw,dh,rw,rh) <= .0001:
                continue
            if fraction < 1:
                if fill_type.endswith('RightToLeft'):
                    dx += dw*(1-fraction); sx += rw*(1-fraction)
                elif fill_type.endswith('BottomToTop'):
                    dy += dh*(1-fraction); sy += rh*(1-fraction)
                if fill_type.endswith(('TopToBottom','BottomToTop')):
                    dh *= fraction; rh *= fraction
                else:
                    dw *= fraction; rw *= fraction
            output.append([round(n, 5) for n in [index,dx,dy,dw,dh,sx,sy,rw,rh,*matrix,*color,max(0,min(1,alpha))]])

    def bake(self):
        frames = []
        for frame in range(math.ceil(self.duration*FPS)):
            commands = []
            self.paint(self.root, {}, (120, 122), IDENTITY, 1, frame/FPS, commands, root=True)
            frames.append(commands)
        assert any(frames), ('No visible sprites', str(self.path), self.omitted)
        # Keep the original HUD center while expanding the transparent viewport
        # to encompass all transformed sprite corners throughout the animation.
        radius = [60, 61]
        for commands in frames:
            for c in commands:
                for x in (c[1], c[1]+c[3]):
                    for y in (c[2], c[2]+c[4]):
                        radius[0] = max(radius[0], abs(x*c[9]+y*c[11]+c[13]-60))
                        radius[1] = max(radius[1], abs(x*c[10]+y*c[12]+c[14]-61))
        # A few source sprites start thousands of pixels off-screen. Keep their
        # original motion but clip the remote part to a bounded overlay surface.
        if max(radius) > 508:
            self.omitted.add('offscreen motion outside 1024px overlay viewport')
        width, height = [min(1024, math.ceil(r+4)*2) for r in radius]
        for commands in frames:
            for c in commands:
                c[13] = round(c[13]+width/2-60,5)
                c[14] = round(c[14]+height/2-61,5)
        return dict(version=1, fps=FPS, duration=self.duration, width=width, height=height, textures=self.textures, frames=frames)


def write_clip(path, clip):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(gzip.compress(json.dumps(clip, separators=(',', ':')).encode(), mtime=0))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('research', type=Path)
    parser.add_argument('--research-only', action='store_true', help='Explicitly allow generating withdrawn, inaccurate research data; never publish these as working packs.')
    args = parser.parse_args()
    if not args.research_only:
        parser.error('This reconstruction was rejected for visual errors. Research use only; pass --research-only explicitly.')
    assets = Assets(args.research)
    crosshair = Scene(next((args.research/'structure-parsed').rglob('WBP_CrossHairPanel.json')), assets, True)
    write_clip(HERE/'crosshair.json.gz', crosshair.bake())
    assert not crosshair.omitted, crosshair.omitted
    catalog = []
    for path in sorted((args.research/'structure-parsed').rglob('WBP_IconFeedbackPage_PC_*.json')):
        variant = path.stem.rsplit('_',1)[1]
        group = path.parent.name.removeprefix('IconFeedback')
        scene = Scene(path, assets)
        clip = scene.bake()
        folder = HERE/'iconpacks'/variant
        write_clip(folder/'lower.json.gz', clip)
        entry = dict(key='calabiyau_'+variant, name=group+' · '+variant+'（2D试验）', group=group, variant=variant,
                     source=path.stem, lower='iconpacks/'+variant+'/lower.json.gz', crosshair='crosshair.json.gz',
                     scope='Anim_Normal sprite layers; no particles, meshes, shader materials, dynamic text or combo stars',
                     excluded=sorted(scene.omitted), duration=clip['duration'], textures=len(clip['textures']))
        (folder/'manifest.json').write_text(json.dumps(entry,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        catalog.append(entry)
    assert len(catalog) == 161
    (HERE/'packs.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    rows = '\n'.join('            new CalabiyauPack("'+p['key']+'", "'+p['name']+'", "'+p['variant']+'"),' for p in catalog)
    source = '''// Generated by SourceAssets/GameStyles/calabiyau/build_packs.py.
using System;
namespace KillConfirmCompatibility.Contracts
{
    public sealed class CalabiyauPack
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Variant { get; }
        public string LowerPath => "iconpacks/" + Variant + "/lower.json.gz";
        public string PreviewUri => "ms-appx:///Assets/GameStyles/calabiyau/iconpacks/" + Variant + "/preview.png";
        public CalabiyauPack(string key, string name, string variant) { Key = key; DisplayName = name; Variant = variant; }
    }
    public static class CalabiyauPacks
    {
        public static readonly CalabiyauPack[] All = {
'''+rows+'''
        };
        public static CalabiyauPack Find(string key)
        {
            foreach (var pack in All) if (string.Equals(pack.Key, key, StringComparison.OrdinalIgnoreCase)) return pack;
            return null;
        }
    }
}
'''
    (REPO/'Common'/'CalabiyauPacks.cs').write_text(source,encoding='utf8')
    print('Generated',len(catalog),'variant packs,',len(assets.crops),'sprite crops and shared kill-only crosshair.')


if __name__ == '__main__':
    main()
