# VALORANT external resource packages

The application ships only the Base icon and Base native audio. Every other
VALORANT theme is installed as two independent ZIP packages:

- `valorant_icon`: visual profile and textures.
- `valorant_voice`: numbered kill audio, headshot variants, appear, and transition audio.

Use the normal **Import full pack** action in the icon or voice library while
the VALORANT game style is selected, or drag one or more ZIP archives directly
onto the matching library area. Installed packages are stored below:

```text
Packs/valorant/
├─ icon_packs/<icon-package-id>/
└─ voice_packs/<voice-package-id>/
```

Both halves use different `id` values and the same `association_id`. When
**Sync voice and icon packs** is enabled, the application switches the other
selector only if an installed package has the same association id. If the
counterpart is absent, the current selection remains unchanged. When sync is
disabled, neither selector changes the other.

Both icon and voice packages may include an optional library cover image at the
package root. Name it `pack_head.png`, `pack_head.jpg`, `pack_head.jpeg`, or
`pack_head.webp`. The file is copied with the package and used as its thumbnail
in both the settings library and Game Bar selector.

Icon manifest example:

```json
{
  "format_version": 2,
  "package_kind": "valorant_icon",
  "id": "valorant_icon_example_v1",
  "association_id": "valorant:example_v1",
  "display_name": "Example V1",
  "display_name_zh_cn": "示例 V1",
  "profile": {
    "accent": "#57F2D1",
    "emblem": "emblem.png",
    "frame": "frame.png",
    "bar": "killpip_up.png",
    "bar_hover": "killpip_hover.png",
    "ring": "ring.png",
    "frame_dissolve": "frame_dissolve.png",
    "badge_dissolve": "badge_dissolve.png",
    "headshot_x": 0,
    "headshot_y": 0,
    "slice_size": 147
  }
}
```

Voice manifests also use the service sound-pack fields `game_style`, `version`,
and `audio`. Native `appear` and `transition` slots use gain `0.3`. Per-streak
headshot slots are named `headshot_1` through `headshot_5`; an empty `headshot`
slot explicitly means that the theme has no generic headshot cue.

The voice library also accepts a simple creator ZIP without a v2 manifest. Put
`1.wav` through `5.wav`, optional `headshot.wav`, and optional `pack_head.*` in
one folder inside the ZIP. Import opens the regular VALORANT voice-pack editor
before saving, so the name and individual slots can still be reviewed.

The internal FModel conversion tool is maintained with the private VALORANT
resource repository rather than the public application source tree.
