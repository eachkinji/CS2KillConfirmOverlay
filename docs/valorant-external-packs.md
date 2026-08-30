# VALORANT external material packs

The app discovers external packs from its private app-data folder:

```text
Packs/valorant/plugins/<pack-id>/
├─ manifest.json
├─ textures/
│  ├─ emblem.png
│  ├─ frame.png
│  ├─ killpip_up.png
│  └─ killpip_hover.png
├─ shared/textures/        # optional per-pack shared particles/masks
└─ audio/                  # optional; WAV/OGG/MP3 plus optional audio manifest
```

Open the exact folder from **VAL advanced settings > External native assets**.
Restart the app after adding or removing a pack so the selector catalog is rebuilt.

`manifest.json` format version 1:

```json
{
  "format_version": 1,
  "id": "valorant_example",
  "display_name": "Example",
  "display_name_zh_cn": "示例",
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

`frame`, `ring`, dissolve textures, `blade`, and `special_frame` may be omitted.
When a shared texture is absent from the pack, the loader checks
`Packs/valorant/shared/textures` and then the compatibility assets bundled with
the app. Existing built-in themes can also be overridden without a manifest by
mirroring their relative path below `Packs/valorant/visual`.
