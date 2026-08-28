# 自定义模块：CS2 Customizer 序列帧兼容

在主程序左侧选择带有“自定义”文字图标的 **自定义模块**，导入 ZIP 或素材目录。
主程序和 Game Bar 的高级设置共用素材管理与播放器面板，可预览第 1～5 杀及爆头、导出兼容 ZIP、删除导入的副本。
默认没有内置素材；没有选中素材时不显示动画，不回退到 CF 图标或音效。

## 支持的目录

```text
我的素材/
  style.json          # 可选：name、author、version、description 等
  1.png               # 从左至右、从上至下排列的透明 PNG 图集
  1.json
  2.png + 2.json       # 2～5 杀可选；不要求提供所有等级
  ...
  1hs.png + 1hs.json   # 可选：同等级爆头变体，支持 1hs～5hs
```

ZIP 可以直接包含这些文件，也可以有单一外层目录。标准 PNG 和帧 JSON 导入时保持原始字节，导出时补充 `style.json` 的 `pack_version: 1`、名称和等级列表。
`preview.png`、`readme.txt`、`readme.md`、`license.txt` 会保留。

每个等级的 JSON 示例：

```json
{
  "frame_width": 256,
  "frame_height": 128,
  "frames": 30,
  "cols": 6,
  "rows": 5,
  "fps": 30,
  "hold_seconds": 0.5,
  "loop": false,
  "version": 1
}
```

也支持旧版 `1/`～`5/` 或 `kill1-1/`～`kill1-5/` 逐帧目录，其中放置 PNG/JPEG/BMP 等静态帧；WebP 静态帧取决于系统解码器支持。
旧版按文件名的字典序播放（与参考播放器一致，例如 `10.png` 在 `2.png` 前），建议用补零文件名。
导入时转换成标准 PNG 图集，保留透明度及父目录对应 JSON 的 FPS、末帧停留。

**首版不转换 GIF、APNG 或动画 WebP 的多帧内容。** 请先在 CS2 Customizer 中转换并导出图集 ZIP。
不导入原程序的全局设置、音效、GSI 服务或 Qt 界面；两边的显示位置和缩放分别设置。

## 播放行为与设置

- 使用单调时钟计算 `floor(经过秒数 × FPS)`；UI 延迟时跳到正确帧，不按定时器调用次数累加。
- 第 6 杀及以上使用第 5 杀。优先使用同等级 `hs`，缺失则使用普通素材；普通素材也缺失时不显示，不借用其他等级。
- 新击杀替换当前动画，播放一次。到末帧后停留 `hold_seconds`，随后淡出。
- FPS 默认跟随素材（缺省 30，范围 1～60），停留默认跟随素材（缺省 0，范围 0～10 秒）。可在高级设置覆盖，覆盖值不改写素材或导出 ZIP。
- 淡入 0.12 秒、淡出 0.25 秒，可关闭。默认采用单条生命的击杀计数，可更改计数模式。
- 原始显示宽度 350，保持比例；位置、缩放、偏移和外观设置与其他游戏隔离。预览自动适应画框，不模拟屏幕上的偏移。
- 使用原生 Win2D 渲染，加载时拆成 GPU 纹理页，缓存当前等级；不需要 Python、Qt 或 WebView。

## 限制与安全检查

每个变体最多读取 600 帧，单帧边长最大 4096，源图集最多 67,108,864 像素；大素材应降低分辨率以减少解码延迟和内存占用。
ZIP 最多 3200 项、解压后合计最多 512 MiB，压缩比最大 500；JSON 最大 1 MiB，单个附加文件最大 16 MiB。
导入拒绝越界路径、大小写重复路径、符号链接、无效帧网格及不可解码图像，失败后清理临时文件，不注册半成品。删除只移除导入副本，不修改原始文件。

## 验证

`Test-CustomSequences.ps1` 在 Windows 上编译实际格式、导入、导出与设置代码，通过 Windows 图片解码器和文件 API 测试；仅应用本地目录及素材目录注册使用测试适配器。
需要 PowerShell 7、Visual Studio C# 编译器、.NET Framework 4.8 引用程序集及 Windows SDK 10.0.26100.0。

```powershell
pwsh -NoProfile -File .\Test-CustomSequences.ps1
python .\Tests\CustomSequences\compare_reference.py D:\KBC\workshop\cs2-customizer <测试输出的 GUID 目录>
```

参考版本：[gufan0000/cs2-customizer](https://github.com/gufan0000/cs2-customizer)，提交 `a9d4ff4`。
对照脚本从本地参考库读取原始 `playback_state` 和 `probe_pack` 纯函数，比较 720 组时间状态并用原库检查导出的 ZIP；不修改或复制参考库代码到产品中。
自动检查不替代安装后的 Game Bar、全屏游戏及不同 DPI 下的视觉测试。
