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

## Overview / 项目简介

Kill Confirm Overlay listens to Counter-Strike 2 Game State Integration events, plays contextual voice lines, and renders animated kill effects inside Xbox Game Bar. It stays visible over the game without reading from or injecting into the CS2 process.

Kill Confirm Overlay 通过 Counter-Strike 2 Game State Integration 接收对局事件，根据事件播放对应语音，并在 Xbox Game Bar 中显示击杀动画。它不读取或注入 CS2 游戏进程，同时可以作为悬浮窗保持在游戏画面上方。

The project has grown from a simple kill-confirm sound tool into a configurable presentation system with independent game-inspired styles, custom media, streak logic, event priorities, and high-resolution rendering.

项目已经从简单的击杀确认音效工具，发展为支持独立游戏风格、自定义媒体、连杀逻辑、事件优先级和高分辨率渲染的可配置展示系统。

> [!NOTE]
> The names of other games identify presentation styles inspired by those games. CS2 remains the game that provides the live events through GSI.
>
> 文中其他游戏的名称表示受对应游戏启发的展示风格。当前实际通过 GSI 提供实时对局事件的游戏仍然是 CS2。

> [!CAUTION]
> **Disclaimer:** All game-related resources (sound effects, icons, character likenesses, etc.) included in this project are the property of their respective copyright holders (Riot Games, Electronic Arts, Valve, etc.). This tool is an unofficial community project for learning, exchange, and testing only. It is not affiliated with, endorsed by, or sponsored by any of the aforementioned companies. Do not use this tool for piracy, commercial resale, or any illegal activity.
>
> **免责声明：** 本项目中包含的所有游戏相关资源（音效、图标、角色形象等）归各自版权方所有（Riot Games、Electronic Arts、Valve 等）。本工具为非官方社区项目，仅供学习、交流、测试使用，与上述公司无任何关联、亦未获其认可或赞助。切勿将本工具用于盗版、商业转售或任何违法活动。

## Open-source foundation and community collaboration / 开源基础与社区联动

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
      <br /><br />
      <strong>感谢 cskillconfirm 原项目开发者</strong><br /><br />
      特别感谢 <a href="https://github.com/st0nie">ston（st0nie）</a> 提供的开发思路，以及开源项目 <a href="https://github.com/st0nie/cskillconfirm"><code>cskillconfirm</code></a> 的基础代码。其 CS2 击杀确认方案为本项目提供了重要基础。本项目也使用了 <a href="https://github.com/st0nie/gsi-cs2-rs"><code>gsi-cs2-rs</code></a> 的相关思路与集成成果。
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
      <br /><br />
      <strong>更多 CS2 自定义需求：CS2 Customizer</strong><br /><br />
      如果你需要更丰富的个性化修改，包括准心、击杀音效与图标、HUD 配色、局内视角和道具瞄点，请访问 <a href="https://github.com/gufan0000/cs2-customizer"><code>gufan0000/cs2-customizer</code></a>。本项目与其深度联动，可以配合形成更完整的 CS2 自定义体验。
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
