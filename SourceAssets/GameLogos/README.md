# Game logo sources

Verified on 2026-11-06. These are existing game brand assets, not generated artwork.

## Calabiyau / Strinova

- [Official Steam store listing](https://store.steampowered.com/app/1282270/Strinova/) identifies app `1282270` as Strinova, developed and published by iDreamSky.
- Original: [`strinova-client.ico`](strinova-client.ico), downloaded directly from [Steam's asset CDN](https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/1282270/b6443d324a9ad9929616a9499166a8d822449d20.ico).
- The `clienticon` hash was obtained from the [Steam app metadata mirror](https://api.steamcmd.net/v1/info/1282270), not a fan-made logo collection. The fresh CDN download is byte-identical to the original used by Claude.
- `Widget/Assets/GameLogos/calabiyau.png` uses the 256×256 icon representation with rounded corners. It is the orange-and-white client symbol, not the full Chinese or English wordmark.
- Original ICO SHA-256: `4b00942c9387b292102cc49418f13d10d300c36c60e4464aa5c487508be5104c`.
- Current sidebar PNG SHA-256: `7fe641663e7490073dd3a2446e7b4d54eaf64d9254a57077e307ef37304b5f56`.

## Battlefield 6

- [EA's official Battlefield 6 page](https://www.ea.com/games/battlefield/battlefield-6) directly links the white text logo in its navigation and footer.
- Unmodified official asset: [`battlefield6-wordmark-white.svg`](battlefield6-wordmark-white.svg), from [EA's asset CDN](https://drop-assets.ea.com/images/4kbsx0T0Nk4d0NoEPmjX9o/3de945f398cfe52d93ed365ae964ccc7/Logo_Wordmark_Secondary_White_Compressed.svg).
- `Widget/Assets/GameLogos/battlefield6.png` is a **derived navigation icon**, not the complete official logo: Claude extracted only the final “6” from the [Fandom copy of the wordmark](https://static.wikia.nocookie.net/battlefield/images/3/3c/Battlefield_6_Logo.svg/revision/latest?cb=20250724202332), centered it at 66% height on a 256×256 near-black tile, and rounded the tile corners. The source glyph agrees with EA's official glyph after viewBox scaling (1397.13×200 versus EA's 200×29 canvas).
- Use the complete official SVG when the full brand wordmark is required. The square tile is a compact sidebar treatment only; its background and rounding are project styling.
- Official SVG SHA-256: `cd0c4955053b26c691fa2eb21d29e4ab82801df8c6828127fd4a0d03bc24e6d7`.
- Current sidebar PNG SHA-256: `8f503aef54b774bbac1a0c4176664f045bc5e1ee162306db14f890203ae57fca`.

The current PNGs were inspected and left unchanged during this provenance check. Trademarks and artwork remain the property of their respective owners.
