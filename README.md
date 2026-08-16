<div align="center">
  <img src="Widget/Assets/Square150x150Logo.scale-200.png" width="112" alt="Kill Confirm Overlay logo" />

# Kill Confirm Overlay / 击杀确认覆盖层

**A customizable CS2 kill-confirm experience built for Xbox Game Bar.**
**为 Xbox Game Bar 打造的可自定义 CS2 击杀确认体验。**

<p>
  <strong>English</strong> · <a href="README.zh-CN.md">简体中文</a>
</p>

<a href="https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv"><img src="https://img.shields.io/badge/Download_下载-Quark_夸克网盘-6C5CE7?style=for-the-badge" alt="Download from Quark Drive / 从夸克网盘下载" /></a>

**Access code / 提取码：`7Twv`**

<p>
  <a href="https://github.com/eachkinji/CS2KillConfirmOverlay/releases"><img src="https://img.shields.io/github/v/release/eachkinji/CS2KillConfirmOverlay?display_name=tag&style=flat-square&label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC%20Release" alt="Latest release 最新版本" /></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows" alt="Windows 10 and 11" />
  <img src="https://img.shields.io/badge/Xbox-Game%20Bar-107C10?style=flat-square&logo=xbox" alt="Xbox Game Bar" />
  <a href="LICENSE"><img src="https://img.shields.io/github/license/eachkinji/CS2KillConfirmOverlay?style=flat-square" alt="License" /></a>
</p>
</div>

## Overview

Kill Confirm Overlay listens to Counter-Strike 2 Game State Integration events, plays contextual voice lines, and renders animated kill effects inside Xbox Game Bar. It stays visible over the game without reading from or injecting into the CS2 process.

The project has grown from a simple kill-confirm sound tool into a configurable presentation system with independent game-inspired styles, custom media, streak logic, event priorities, and high-resolution rendering.

> [!NOTE]
> The names of other games identify presentation styles inspired by those games. CS2 remains the game that provides the live events through GSI.

> [!CAUTION]
> **Disclaimer:** All game-related resources (sound effects, icons, character likenesses, etc.) included in this project are the property of their respective copyright holders (Riot Games, Electronic Arts, Valve, etc.). This tool is an unofficial community project for learning, exchange, and testing only. It is not affiliated with, endorsed by, or sponsored by any of the aforementioned companies. Do not use this tool for piracy, commercial resale, or any illegal activity.

## Open-source foundation and community collaboration

<table>
  <tr>
    <td align="center" width="120">
      <a href="https://github.com/st0nie">
        <img src="https://avatars.githubusercontent.com/u/42872734?v=4&s=160" width="80" alt="st0nie avatar" /><br />
        <strong>ston · st0nie</strong>
      </a>
    </td>
    <td>
      <strong>Thanks to the original cskillconfirm developer</strong><br /><br />
      Special thanks to <a href="https://github.com/st0nie">ston (st0nie)</a> for the original idea and foundational code provided by <a href="https://github.com/st0nie/cskillconfirm"><code>cskillconfirm</code></a>. Its CS2 kill-confirm approach made this project possible. This project also uses ideas and integration work from <a href="https://github.com/st0nie/gsi-cs2-rs"><code>gsi-cs2-rs</code></a>.
    </td>
  </tr>
  <tr>
    <td align="center" width="120">
      <a href="https://github.com/gufan0000">
        <img src="https://avatars.githubusercontent.com/u/113977586?v=4&s=160" width="80" alt="gufan0000 avatar" /><br />
        <strong>gufan0000</strong>
      </a>
    </td>
    <td>
      <strong>More CS2 customization: CS2 Customizer</strong><br /><br />
      If you want more extensive customization—including crosshairs, kill sounds and icons, HUD colors, in-game view settings, and utility lineups—visit <a href="https://github.com/gufan0000/cs2-customizer"><code>gufan0000/cs2-customizer</code></a>. The two projects work in close coordination and are deeply integrated with each other's customization workflow.
    </td>
  </tr>
</table>

## Highlights

- **Xbox Game Bar overlay** — open, position, scale, and pin the widget with `Win + G`.
- **Context-aware kill events** — normal kills, headshots, knife kills, first kills, final kills, assists, streaks, and spectated-teammate events.
- **Eleven built-in presentation styles** — CrossFire, CSOL, VALORANT, Battlefield 1, Battlefield V, Battlefield 4, Battlefield 2042, PUBG, Delta Force, Doubao, and Dagoujiao.
- **Flexible streak logic** — life-based, round-based, and looped streak windows with a configurable loop point from 2 to 50 kills.
- **Custom images and audio** — choose bundled assets or import supported images and voice lines for individual events and styles.
- **Advanced audio control** — per-style priorities, volume controls, event-specific sounds, and configurable playback speed/pitch behavior.
- **Bomb audio timeline** — optional sounds for plant, defuse, explosion, and new-round reset, with a smooth 40-second acceleration between configurable initial and final speeds.
- **High-resolution visuals** — optimized rendering, positioning, and scaling for high-DPI displays and 4:3 fullscreen resolutions.
- **Independent profiles** — style, asset-pack, animation, and advanced settings are persisted separately to reduce cross-style interference.
- **Bilingual interface and update checks** — English/Simplified Chinese UI plus a check against the latest published GitHub Release when the widget opens.

## How it works

```text
CS2 Game State Integration
            ↓
Local Rust service on 127.0.0.1:10087
            ↓
Event classification and audio playback
            ↓
Xbox Game Bar animated overlay
```

The companion service receives GSI data locally. The widget then selects the appropriate animation, image, and audio based on the active style and event priority.

## Requirements

- Windows 10 or Windows 11
- Xbox Game Bar enabled
- Counter-Strike 2

## Installation

1. Download the package from [Quark Drive](https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv) using access code `7Twv`, or use the [GitHub Releases](https://github.com/eachkinji/CS2KillConfirmOverlay/releases) page.
2. Choose the installer that fits your system:
   - **With dependencies — recommended for new users:** includes prerequisites required for first-time installation.
   - **Dependency-free — recommended for updates:** intended for systems where an earlier version already works correctly.
3. Run the installer.
4. Press `Win + G`, open Kill Confirm Overlay, and pin it if desired.
5. Start CS2. The installer will attempt to configure Game State Integration automatically.

## CS2 Game State Integration

The overlay listens at:

```text
http://127.0.0.1:10087/
```

The installer attempts to create the required GSI configuration automatically. If events do not trigger, copy `KillConfirmService/gsi/gamestate_integration_killconfirm.cfg` into:

```text
C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\
```

The upstream reference configuration is available in [`gsi-cs2-rs`](https://github.com/st0nie/gsi-cs2-rs/blob/main/gsi_cfg/gamestate_integration_fast.cfg).

## Troubleshooting

- **No sound or animation after a kill:** confirm that the local service is running and the GSI file is in the correct CS2 `cfg` directory.
- **Using Clash or another system-wide proxy:** disable the system proxy or exclude `127.0.0.1`. Some proxy configurations intercept local traffic and prevent the service from receiving GSI events.
- **Updating an existing installation:** use the dependency-free installer only if the previous version and its prerequisites already work correctly.
- **Custom content:** only import asset and voice packs from sources you trust.

## Build from source

Source builds require the Rust toolchain, Visual Studio or Visual Studio Build Tools with Windows/UWP/MSIX tooling, and Inno Setup 6 when building the optional `.exe` installers.

From the repository root:

```powershell
.\Build-IntegratedPackage.ps1
```

Create the transferable packages:

```powershell
.\Build-TransferPackage.ps1
```

Create the installers:

```powershell
.\Build-Installer.ps1
```

## Project layout

- `KillConfirmService` — Rust local service for GSI event processing and audio playback.
- `Widget` — Xbox Game Bar interface, settings, and visual effects.
- `Package` — Windows packaging project.
- `Installer` — installer definitions and supporting files.
- `SourceAssets` — source animations, images, audio, icons, and built-in style packs.

Package-ready resources are refreshed from `SourceAssets` during the build.

## Additional credits

- [`gd656killicon`](https://github.com/MinecraftGD656/gd656killicon) by MinecraftGD656.
- [Steam Workshop item 2721562982](https://steamcommunity.com/sharedfiles/filedetails/?id=2721562982).

This project incorporates AI-generated code.

## License and disclaimer

Licensed under the [GNU Affero General Public License v3.0](LICENSE).

This project is not affiliated with Valve, Microsoft, Xbox, CrossFire, Riot Games, Electronic Arts, Krafton, Tencent, ByteDance, or any other game publisher. All referenced product names and trademarks belong to their respective owners.

## Star history

<a href="https://www.star-history.com/?repos=eachkinji%2FCS2KillConfirmOverlay&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=eachkinji/CS2KillConfirmOverlay&type=date&theme=dark&legend=top-left&sealed_token=h14p8Daxv7z793bAlZPP_xcy-2SfQzit57_QKp1CIDyUiRpKdDYeatoUoGr3P65j-0F_24GIvkRDmXv79WOffQwuxurzGiDaGYxA6ZDI8l1fwrvi2PKpv5L8C5ujaoZ47FA" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=eachkinji/CS2KillConfirmOverlay&type=date&legend=top-left&sealed_token=h14p8Daxv7z793bAlZPP_xcy-2SfQzit57_QKp1CIDyUiRpKdDYeatoUoGr3P65j-0F_24GIvkRDmXv79WOffQwuxurzGiDaGYxA6ZDI8l1fwrvi2PKpv5L8C5ujaoZ47FA" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=eachkinji/CS2KillConfirmOverlay&type=date&legend=top-left&sealed_token=h14p8Daxv7z793bAlZPP_xcy-2SfQzit57_QKp1CIDyUiRpKdDYeatoUoGr3P65j-0F_24GIvkRDmXv79WOffQwuxurzGiDaGYxA6ZDI8l1fwrvi2PKpv5L8C5ujaoZ47FA" />
 </picture>
</a>
