链接：https://pan.quark.cn/s/1f3cfbcf8d5f?pwd=7Twv

提取码：7Twv

本项目使用了AI生成的代码

This project incorporates AI-generated code.

----

目前收集到的反馈有需要：

战争前线

OverWatch

塔可夫

CODOL

卡拉彼丘

COD16/19

Apex

The finals

CSOL

战地6

R6

混搭音效

这些游戏和功能不一定都做，需要看情况

----

[English](README.md) | [简体中文](README.zh-CN.md)

# KillConfirmGameBar
KillConfirmGameBar is a Counter-Strike 2 kill-confirm overlay for Xbox Game Bar.

It plays voice lines and shows animated effects when CS2 reports kills through Game State Integration. The overlay runs inside Xbox Game Bar, so it can stay on top while you play.

## Roadmap

Implemented today:

- Kill voice lines for CS2 kill events.
- Kill confirmation icons and animated overlay effects.

Planned work:

- Code-driven kill effects for broader icon compatibility. The current overlay still uses animation frame assets.
- More character voice packs.
- A more complete settings panel, including finer animation frame-rate controls.
- More in-match voice events, such as C4 events, round-start voice lines, and round-end voice lines.

## Important Notes

- The overlay listens on a local port for CS2 Game State Integration events. System-wide proxy tools can prevent the local service from receiving events. If the widget starts but kills do not trigger any sound or icon, turn off the system proxy before playing.
- This is especially relevant for proxy clients such as Clash-style tools. Add this note to video descriptions and setup guides so users know to disable the system proxy when troubleshooting.

## Features

- CS2 kill-confirm sound effects.
- Xbox Game Bar overlay widget.
- Animated effects for normal kills, headshots, knife kills, first kills, and last kills.
- Voice-pack switching from the control panel.
- Small-group Windows installer support.

Current voice packs:

- `swat GR / swat BL`
- `tiger GR / tiger BL`
- `cfsex`
- `women GR / women BL`

## Requirements

- Windows 10/11
- Xbox Game Bar enabled
- Counter-Strike 2

If you want to build from source, you also need:

- Rust toolchain
- Visual Studio or Visual Studio Build Tools with Windows/UWP/MSIX tooling
- Inno Setup 6, only for building the optional `.exe` installer

## Install

For normal use, download a release package and run the installer or install script included with that release.

After installing:

1. Open Xbox Game Bar with `Win + G`.
2. Open the Kill Confirm Overlay widget.
3. Start CS2.
4. The installer will try to configure CS2 Game State Integration automatically.

The overlay uses a small local companion service. The installer sets up the package and the local connection needed by the Xbox Game Bar widget.

## Release Signatures (Sigstore)

Every published release is signed with keyless [Sigstore](https://www.sigstore.dev/) signing (Fulcio + Rekor). Each release asset `FILE` has a matching `FILE.sig` bundle attached. Verifying a bundle proves that the file was released from this repository and was not modified in transit — it does not grant Windows SmartScreen/Antivirus trust (that would require a separate Authenticode certificate).

To verify a downloaded artifact, install [cosign](https://docs.sigstore.dev/cosign/installation/) and run:

```bash
cosign verify-blob \
  --bundle FILE.sig \
  --certificate-identity "https://github.com/eachkinji/CS2KillConfirmOverlay/.github/workflows/sigstore-sign.yml@refs/tags/*" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  FILE
```

On Windows, the repository includes a helper that downloads cosign for you:

```powershell
.\verify-release.ps1 -ArtifactPath .\KillConfirmGameBar_Setup_3.1.14.0.exe
```

If an artifact is added to a release after it was published, re-run the [Sign release artifacts with Sigstore](https://github.com/eachkinji/CS2KillConfirmOverlay/actions/workflows/sigstore-sign.yml) workflow manually to sign it.

## CS2 Game State Integration

CS2 needs a GSI config that points to:

```text
http://127.0.0.1:10087/
```

The installer tries to create this config automatically. If kill events do not trigger, place `KillConfirmService/gsi/gamestate_integration_killconfirm.cfg` in the CS2 cfg folder manually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\
```

The upstream GSI reference config is available here:

```text
https://github.com/st0nie/gsi-cs2-rs/blob/main/gsi_cfg/gamestate_integration_fast.cfg
```

## Build From Source

From the repository root:

```powershell
.\Build-IntegratedPackage.ps1
```

To create a transferable install package:

```powershell
.\Build-TransferPackage.ps1
```

To create the optional installer:

```powershell
.\Build-Installer.ps1
```

## Project Layout

- `KillConfirmService`: Rust local service for CS2 Game State Integration and audio playback.
- `Widget`: Xbox Game Bar widget.
- `Package`: Windows packaging project.
- `Installer`: installer wrapper files.
- `SourceAssets`: source animations, audio, icons, and voice packs.

Generated package-ready files are refreshed from `SourceAssets` during the build.

## Notes

- The app communicates only with a local service on `127.0.0.1`.
- Voice-pack behavior is controlled by `sound.lua` files inside each sound pack.
- Only install sound packs from sources you trust.
- Test signing files are for development builds only. Public releases are signed with keyless Sigstore signing; see [Release Signatures](#release-signatures-sigstore).

## Credits

The Rust service is based on the open-source `cskillconfirm` project by st0nie:

```text
[https://github.com/st0nie/cskillconfirm]
```

This project also uses `gsi-cs2-rs`:

```text
[https://github.com/st0nie/gsi-cs2-rs]
```

Additionally, this project incorporates resources and inspiration from the following works:

**gd656killicon** by MinecraftGD656:

```text
[https://github.com/MinecraftGD656/gd656killicon]
```

**Steam Workshop Item (ID: 2721562982)**:

```text
[https://steamcommunity.com/sharedfiles/filedetails/?id=2721562982]
```

## License

This project is licensed under the GNU Affero General Public License v3.0. See `LICENSE`.

This project is not affiliated with Valve, Microsoft, Xbox, CrossFire, Valorant, Battlefield, or any other game publisher.
