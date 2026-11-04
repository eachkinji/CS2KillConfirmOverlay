# Battlefield 6 effect source and port

Reference: <https://github.com/MinecraftGD656/gd656killicon>, author `Minecraft_GD656`,
commit `10d367eb6ddb158d2f5e3e250d2cf825d9b95a12`.
The upstream README and `gradle.properties` declare MIT (`mod_license=MIT`).
The upstream checkout used for this port did not contain a separate LICENSE file.
Preserve the upstream attribution and project-level third-party license notice when distributing.

This is official preset `src/main/resources/assets/gd656killicon/presets/official/00037.json`,
not the existing Battlefield 2042 animation. Reference implementation files are under
`src/main/java/org/mods/gd656killicon/client/render/`:

- `impl/ScrollingIconRenderer.java`: five visible icons, newest pinned at the right,
  100 ms queue interval, 80 ms ease-out quadratic movement; overflow retains its exit animation.
- `effect/IconEntranceBackground.java`: 15 GUI-unit gray/orange square and animated diagonals,
  200 ms fade-in, 200 ms shrink/fade-out; background starts before the delayed icon.
- Icons: 100 ms delay, 200 ms ease-out cubic entrance from zero size, 3.3 s hold,
  200 ms three-part blink exit (1 to 0.2 to 1 to 0). The hold clock includes entrance.
- `impl/SubtitleRenderer.java`: latest victim, orange/right-aligned, white-to-orange entry,
  200 ms fade-in, 3 s display and 300 ms blink exit.
- `impl/BonusListRenderer.java`: three right-aligned horizontal outlined labels,
  7-unit gaps, 8 s matching-label merge window and multiplication suffix, 20 ms entry,
  3 s display followed by 100 ms blink exits staggered every 200 ms.
- `impl/ScoreSubtitleRenderer.java`: actual accumulated reward, 1.25 s quintic-out counter,
  3.7 s display, 300 ms blink exit; no score-size pulse in this preset.
  The upstream name `enable_digital_scroll` means numeric interpolation, not vertical
  rolling digits: `DigitalScrollEffect.java:205` interpolates the value, then
  `ScoreSubtitleRenderer.java:163,238,557` rounds/formats/draws normal text.
  The bonus multiplication suffix is directly appended in `BonusListRenderer.java:1229`.
- `effect/SubtitleEntranceBackground.java`: mirrored sweep behind victim and forward sweep
  behind score; peak opacity is one minus the upstream transparency setting.

Minecraft GUI units are presented at a fixed 2x transform inside a fixed 607 x 260 canvas.
The transform, viewport and selection bounds do not change during animation. Native 2048 x 2048
textures remain intact. A raster cache selects 64/128/256/512/1024/2048 pixel levels from the
full icon size, current drawing transform and drawing-target DPI. Each level is generated
from the native original in at-most-2x high-quality downsampling stages; temporary intermediate
surfaces are disposed immediately. The 17 glow copies are composed once into a separate
padded surface, so a frame draws only the glow and icon with linear interpolation. Enlarging
the presentation requests a sharper level automatically rather than stretching a fixed
low-resolution thumbnail. Text formats and layouts are cached for the playback session.
The cached icon glow uses Win2D premultiplied source-over composition; upstream uses
additive blending. Offsets, opacity and timing are retained, with a slightly softer
glow where translucent copies overlap. Text retains upstream's eight offset copies.

The nine partial files separate constants, models, assets, playback, state/timeline, drawing
primitives, layer rendering, adaptive raster caching and text layout caching. Their compatibility-renderer counterparts are identical except
for namespace. Async resource loads are deduplicated, generation-checked and ordered through
the event queue. Playback cancellation additionally checks the shared play token and a local
session identifier. Cache invalidation disposes every owned bitmap.

The target application's real kill, headshot, melee, grenade, assist, objective and round-reward
events are mapped to this composition. Money is supplied by the event rather than invented.
Melee does not imply an unreported backstab. Victim names and reward tags are width-bounded to
keep the fixed canvas safe for long Chinese/English strings. Labels follow the UI language.
Upstream Minecraft-only per-entity damage readouts and honor badges need event data that the
current playback API does not provide; the renderer does not fabricate those events.

Assets live separately at `Assets/GameStyles/battlefield6/killconfirm/textures`.
Only currently reachable default, headshot, explosion, assist, capture and medic textures are
preloaded. Vehicle, vehicle-assist, critical and spotting source assets remain separately
available without decoding unused GPU bitmaps. Upstream default/explosion/critical PNGs are
byte-identical and share one decoded bitmap for built-in packs (custom pack overrides stay
distinct); destroy-vehicle/vehicle-assist PNGs are also byte-identical.
