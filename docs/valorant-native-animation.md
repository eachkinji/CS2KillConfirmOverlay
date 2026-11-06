# VALORANT native animation coverage

The November 2026 local extraction contains 203 external icon configurations.
Twelve enable `Special_Overrides` with per-kill `KillBannerWings` widgets:

| Configurations | Image layers for kills 1–6 |
| --- | --- |
| Champions 2024 | 3, 4, 6, 9, 11, 12 |
| Champions 2025 / Midas | 4, 6, 6, 7, 12, 17 |
| Champions 2026 Dragon and Snake | 13, 13, 13, 13, 15, 21 |
| Hazard, four variants | 2, 2, 2, 2, 2, 2 |
| Yokai, four variants | 3, 3, 3, 4, 6, 10 |

The optional renderer restores these widgets from their original image layers
and MovieScene curves. Hazard's old hand-coded blade is disabled only when a
native widget is present, preventing duplicate frames/blades. Old packages keep
their previous behavior. The built-in Base resources remain built in.

The other 191 configurations retain the common renderer. This count does not
mean every possible shader effect has been reproduced. The complete exported
KillBanner directory was also checked for WidgetBlueprintGeneratedClass,
Material, and MaterialInstanceConstant exports, and every KillBannerData was
checked for widget/material references. Five Ego2 configurations reference
BlankWidget in disabled overrides. The parent OptionalEffects value is null.
Commando's standard badge references `Commando_Dynamic_Portrait_MI`; the cooked
material lacks its shader expression graph. Its package preserves the original
PortraitTex as a static fallback and records the unimplemented dynamic portrait
and character substitution in `render_limitations`. It is not rendered as a
made-up animation. The shared existing dissolve/particle renderer is retained.

## Source evidence

The private extraction retains the original readable JSON, textures, raw
packages, and a separate JSON export with `provider.ReadScriptData = true`.
`Export-NativeAnimations.py` follows the actual `OnFadeIn` / `OnFadeOut` entry
points through their Ubergraph jumps to `PlayAnimation`. It rejects unsupported
control flow, weighted curves, non-image canvas children, or ambiguous layouts
instead of silently inventing motion. Each converted tier records the source
widget path and `playback_evidence`, including entry and play-statement offsets.

For the champion widgets, OnFadeIn uses one forward playback at speed 0.6.
OnFadeOut uses one reverse playback at speed 1.0, including the separately
authored Outro sequences. Midas kill 6 and the 2026 widgets pass StartAtTime
-0.5. UUMGSequencePlayer first calculates the reverse cursor from the end and
then clamps it to the sequence range, so those negative values start at the end
and do not introduce a half-second delay. This matches the engine's
[UMGSequencePlayer implementation](https://github.com/chenyong2github/UnrealEngine/blob/5.3/Engine/Source/Runtime/UMG/Private/Animation/UMGSequencePlayer.cpp#L236).

`KillBanner_Base.PassDataToWidgets` statement 662 computes CurrentKillCount - 1;
statement 704 indexes Overrides with that value and calls GenerateOptionalFX.
The separate Array_LastIndex path is the out-of-range fallback. Midas's seventh
entry is identical to its sixth entry, including textures, curves and playback
parameters. The application's six-kill cap therefore does not shift tiers.

`KillBanner_Base` places OptionalEffects after GlobalHolder in Overlay_33,
outside GlobalHolder's translation and opacity. The native layer renderer uses
that order and independent outro clock. Asset duration includes the optional
outro, so the common holder's earlier fade does not truncate it.

Canvas slots use the engine constructor defaults when cooked JSON omits a
member: `Offsets=(0,0,100,30)` and `bAutoSize=false`. Brush dimensions are used
only for explicit auto-size slots. For fixed zero anchors the parent desired
size is the maximum child size plus absolute slot offset; child Alignment does
not shrink that parent size. These rules follow
[UCanvasPanelSlot](https://github.com/chenyong2github/UnrealEngine/blob/5.3/Engine/Source/Runtime/UMG/Private/Components/CanvasPanelSlot.cpp#L8)
and [SConstraintCanvas](https://github.com/chenyong2github/UnrealEngine/blob/5.3/Engine/Source/Runtime/Slate/Private/Widgets/Layout/SConstraintCanvas.cpp#L326).
This matters especially for Champions 2024, whose omitted slot width is 100,
not the 224/400-pixel texture width. All twelve source root canvases use scale
3. GenerateOptionalFX only creates and adds the widget; it does not replace
that scale. The application's separate UMG-to-output scale is approximately
0.3659, and is applied once.

## Rendering and lifetime

The renderer samples linear, constant, and cubic Hermite channels using their
source tick resolution, transforms the original textures at draw time, and
preserves subpixel movement. Metadata is bounded and all node, timing, key and
tangent numbers must be finite. The renderer also rejects non-finite composed
matrices before passing them to Win2D.

Only the selected tier owns GPU textures. A generation check rejects stale
asynchronous loads; disposal and publication share a lock. Same-id reimports
carry a resource revision through both the texture cache and resource broker.

The composition validation saves actual Win2D native/legacy image pairs for
each kill tier, compares immediate and recorded rendering, checks final alpha
is zero, and can compare every submitted native-window frame with a direct
reference at enlarged physical resolution. The lifecycle regression controls
real bitmap-load completion order to check disposal, same-tier and cross-tier
races. These checks verify the reconstructed renderer and its resource
lifetime; they are not a claim of pixel identity with a running game capture.
