# Category and approved transparent game logo sources

## Current scope: seven categories and four approved games

Verified on **2026-11-07**. The final user-approved scope is **seven category icons plus four transparent individual game logos: Battlefield 4, Battlefield 2042, Delta Force, and PUBG**. The other 14 individual game/brand/custom icons retain the user’s original selection, including Battlefield 6, novelty styles, and the custom-module image. Their SHA-256 hashes match the pre-task baseline. The restoration report below is an earlier checkpoint: the 16 initially replaced game assets were restored before the user explicitly approved the four later exceptions.

- Original backups: `Output/RealGameLogos20261107/previous-runtime/`.
- Restoration evidence: `Output/RealGameLogos20261107/game-logo-restoration.json`.
- Current scope, unchanged-game hashes, category source URLs, and game-change evidence links: `provenance.json`.
- Verification that the other 14 game icons and all seven category assets are untouched by the later two-game step: `Output/TransparentDeltaPubg-20261107/unaffected-verification.json`.

Generic genres have no official game-brand logo. Each category uses a genuine representative game/brand asset from within that category. The category image is independent of the corresponding user-selected individual game logo. No generated artwork, extracted digits, recoloring, cropping, or added tiles are used for the category assets.

| Category | Representative | Official source | Exact download |
| --- | --- | --- | --- |
| `traditional` | `crossfire` | [Source](https://cf.qq.com/) | [Asset](https://cf.qq.com/favicon.ico) |
| `hero` | `overwatch` | [Source](https://store.steampowered.com/app/2357570/) | [Asset](https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/2357570/c05905cbf26020ffefcae9b39038d9ad523e2d68.ico) |
| `survival` | `pubg` | [Source](https://store.steampowered.com/app/578080/) | [Asset](https://shared.fastly.steamstatic.com/community_assets/images/apps/578080/f962202b06de547cf47c156bdd7aaa5bf7f2cdbb.ico) |
| `battlefield` | `battlefield6` | [Source](https://store.steampowered.com/app/2807960/) | [Asset](https://shared.fastly.steamstatic.com/community_assets/images/apps/2807960/3059e94fb89816fe1929a238ac2b55e8be348408.ico) |
| `cod` | `modernwarfare2019` | [Source](https://store.steampowered.com/app/2000950/) | [Asset](https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/2000950/485450eba5e95e25805d9a34a668d5dc6b51d129.ico) |
| `cardmoba` | `sanguosha` | [Source](https://www.sanguosha.com/) | [Asset](https://www.sanguosha.com/assets/favicon.ico) |
| `meme` | `doubao` | [Source](https://www.doubao.com/) | [Asset](https://lf-flow-web-cdn.doubao.com/obj/flow-doubao/favicon/new-doubao/192x192.png) |

Original downloads are retained under `Web-20261107/`. ICOs are decoded at the largest native size and saved to PNG without resizing, with identical decoded pixels. Original PNGs are copied byte-for-byte. Steam metadata was used only to discover publisher-provided client-icon hashes; image bytes were downloaded from Valve’s CDN. Extra unused downloads are research references, not instructions to replace individual game logos.

## User-approved transparent game icons

- **Delta Force:** the green triangular mark from the [official Chinese website](https://df.qq.com/), downloaded as the [official favicon](https://game.gtimg.cn/images/dfm/favicon.ico). Decode its native 128×128 image to PNG with identical RGBA pixels; do not add a tile or recolor. Three corners have alpha 0 and the bottom-right has alpha 4 from the original mark’s antialiased edge.
- **PUBG:** the gold mark from [KRAFTON’s official PUBG press kit](https://press.krafton.com/PUBG/Focus/Logo-PUBG-Battlegrounds). The downloaded transparent 360×125 PNG contains a standalone boxed PUBG mark above the separate BATTLEGROUNDS line. The runtime PNG is the exact rectangle `[131, 0, 233, 57]`, preserving all original pixels and the registered-trademark symbol. It has no added background; all four corners have alpha 0. No redrawing, color replacement, or scaling is applied to the runtime PNG.
- Delta/PUBG original downloads, exact URLs, hashes, alpha metrics, and before-change backups: [`TransparentDeltaPubg-20261107/provenance.json`](TransparentDeltaPubg-20261107/provenance.json). The light/dark 24/32/48 px QA sheet is `Output/TransparentDeltaPubg-20261107/transparent-logo-review.png`; its backgrounds are for review only.
- **Battlefield 4:** the original “4” numeral is a pixel-identical rectangle from EA Battlelog’s transparent PNG wordmark. **Battlefield 2042:** the final mark matches the user’s reference: square **BF** with vertical **2042**, in its original turquoise, with a native transparent background. It is the existing [SteamGridDB icon 8306 by Peggin](https://www.steamgriddb.com/icon/8306), decoded from its 256×256 ICO without changing pixels. The earlier horizontal numeral extraction is superseded. Original downloads, exact source URLs, extraction details, alpha validation, and final hashes are recorded in [`TransparentBattlefield-20261107/provenance.json`](TransparentBattlefield-20261107/provenance.json).

## Earlier individual-logo provenance (unchanged runtime selection)

The earlier source notes below describe the original game icons preserved by this update.

### Game logo sources

Verified on 2026-11-06. These are existing game brand assets, not generated artwork.

## Calabiyau / Strinova

- [Official Steam store listing](https://store.steampowered.com/app/1282270/Strinova/) identifies app `1282270` as Strinova, developed and published by iDreamSky.
- Original: [`strinova-client.ico`](strinova-client.ico), downloaded directly from [Steam's asset CDN](https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/1282270/b6443d324a9ad9929616a9499166a8d822449d20.ico).
- The `clienticon` hash was obtained from the [Steam app metadata mirror](https://api.steamcmd.net/v1/info/1282270), not a fan-made logo collection. The fresh CDN download is byte-identical to the original used by Claude.
- `Widget/Assets/GameLogos/calabiyau.png` uses the 256×256 icon representation with rounded corners. It is the orange-and-white client symbol, not the full Chinese or English wordmark.
- Original ICO SHA-256: `4b00942c9387b292102cc49418f13d10d300c36c60e4464aa5c487508be5104c`.
- Current sidebar PNG SHA-256: `7fe641663e7490073dd3a2446e7b4d54eaf64d9254a57077e307ef37304b5f56`.

## Battlefield 5 / 6 transparent selector update — 2026-11-07

- Battlefield 5 keeps the user-selected transparent V PNG. Its grey UI backdrop was removed.
- Battlefield 6 now uses the exact numeral path from [EA's official SVG](https://drop-assets.ea.com/images/4kbsx0T0Nk4d0NoEPmjX9o/3de945f398cfe52d93ed365ae964ccc7/Logo_Wordmark_Secondary_White_Compressed.svg), stored unmodified as `battlefield6-wordmark-white.svg`. The numeral is displayed in dark monochrome on transparency for legibility on the light panel; no generated/reconstructed shape is used.
- The game selector no longer substitutes the Battlefield 6 kill-skull texture. Both desktop and UWP selectors use the same GameLogos source.
- Exact vector derivative and hashes: `TransparentBattlefield56-20261107/provenance.json`.
- The earlier near-black square tile is superseded. Other game artwork is unchanged.
