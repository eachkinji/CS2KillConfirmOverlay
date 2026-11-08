# 应用品牌素材

`KCF_logo_vector.svg` 是用户提供的 `E:/Zac/Download/KCF_logo_vector.svg` 原文件副本，字节保持不变。`title`、`desc` 仅为素材元数据。源文件及生成结果的 SHA-256 见 [manifest.json](manifest.json)。

`KCF_logo_icon.svg` 只调整原图的视口：按透明度测得实际图形边界，去掉外围空白，保持比例并在长轴两侧各留 6.25% 安全边距。所有颜色、渐变、白色图形和透明背景沿用原 SVG。PNG 和 ICO 的每个尺寸均由 SVG 独立栅格化，不由小图放大，也不替换任何游戏 LOGO。

在仓库根目录运行：

```powershell
python -m pip install -r SourceAssets/Branding/requirements.txt
python SourceAssets/Branding/generate_branding.py --preview-dir Output/Branding-20261108
python SourceAssets/Branding/generate_branding.py --check
```

固定使用 `resvg-py 0.5.0`、`Pillow 12.3.0`，无字体或外部媒体依赖。预览标签使用本机 Segoe UI，仅用于审阅，不进入图标资产或可复现散列。

生成器验证每张 PNG 的尺寸、透明角、图形边界，以及 ICO 的全部 10 个独立 RGBA 帧。`--check` 在内存重新生成并逐字节比对归档 SVG、8 张 PNG、ICO 和清单，不写入应用资产。

| 入口 | 来源 |
| --- | --- |
| UWP 磁贴、应用列表、商店、锁屏、启动画面 | `Widget/Assets/` 现有 Logo 与 SplashScreen 文件；清单路径保持不变 |
| Game Bar 扩展 | `Widget/GameBar/Square44x44Logo.png` |
| 控制面板 EXE、WinUI 窗口/任务栏 | 工程 `ApplicationIcon` 与发布后的 `Assets/KillConfirmOverlay.ico`，窗口显式 `SetIcon` |
| 兼容显示 EXE、布局编辑窗口 | 工程 `ApplicationIcon` 与 WPF 内嵌 ICO 资源 |
| 后台服务、Widget Bridge、设置启动器 EXE | `KillConfirmService/build.rs` 将同一 ICO 编入三个 Windows 可执行文件 |
| 安装器、安装快捷方式、卸载入口 | `Installer/Assets/KillConfirmOverlay.ico` 及 Inno Setup `SetupIconFile`、`IconFilename`、`UninstallDisplayIcon` |

ICO 包含 16、20、24、32、40、48、64、96、128、256 像素帧。未发现应用托盘实现，因此没有新增托盘入口。源文件保留于本目录；这些是应用品牌构建资产，不属于 `导入素材/` 的游戏素材 ZIP。

本次生成不执行应用构建或覆盖正式安装。审阅图为 `Output/Branding-20261108/branding-preview.png`，透明预览为同目录 `logo-transparent.png`；窗口、EXE 资源和安装入口由随后统一构建验证。
