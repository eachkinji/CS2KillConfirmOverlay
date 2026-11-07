# 三国杀 / Sanguosha

当前仅保留“十周年连破播报”：首破、连破、三破、四破、五破、六破、七破；第 8 杀及以后播放神威震乾坤。爆头、刀杀、雷杀分别使用万夫莫敌、神医妙手、青囊济世，共 11 条动画。“官阶破敌神兵”图标与语音包已从可选内置素材中移除，原始研究素材仍保留。来源说明见 `THIRD-PARTY-NOTICES.txt`。

## 还原方式

这个风格不重画动画，也不使用手写的粒子或缓动曲线。

1. **原版数据。** `killconfirm/<tier>/` 下的图集页 PNG 与客户端 `res/assets/animate/battle/killEffect/` 中的文件逐字节一致。语音同样逐字节复制：`soundpacks/sanguosha/Nkill.mp3` 就是原版的 `newkill_*.mp3`。
2. **原版引擎计算。** `tools/record.html` 在无头 Chrome 中加载游戏自带的 LayaAir 2.x 与 Spine 4.0.56 运行库（来自 `libs/min/laya.sgs`），按 `NewKillEffect.playKillEffect` 的方式播放原版 `.skel`，并以 60 Hz 步进。每一帧记录 `SpineSkeletonRenderer` 交给 `Graphics.drawTriangles` 的全部绘制命令：图集页、区域 UV、四边形顶点、混合模式，以及经 `_mixRGBandAlpha` 打包后的顶点色。`convert_drawlist.py` 把这些命令写成 `*.sgsd.gz`。
3. **Win2D 回放。** `Controls/Animations/Sanguosha/SanguoshaFrameRenderer.cs` 按 Laya WebGL 的规则回放命令：
   - 纹理使用预乘 alpha。
   - 着色器按 `texel × (r·a, g·a, b·a, a)` 着色。
   - 混合模式：normal 为 `ONE, ONE_MINUS_SRC_ALPHA`；light 为 `ONE, ONE`；screen 为 `ONE, ONE_MINUS_SRC_COLOR`。
4. **原版播放逻辑。**
   - `NewKillEffect.modalBg`：整个舞台铺 70% 黑幕，动画结束（PLAY_COMPLETE）时撤除。
   - Spine 事件 `zhenping` 触发 `ShakeTool.StartShake([stage], 0.5, 10, 8)`：每 100 ms 一次随机偏移，偏移范围 ±8 px，共 5 次，每次在 99.9 ms 内线性回位。

连破系列只用到 RegionAttachment，以及 RGBA、Attachment、Translate、Rotate、Scale、Shear、DrawOrder、Event 时间线。没有网格、IK 或路径约束，所以每个绘制命令都是精确的仿射四边形；录制时校验平行四边形误差小于 0.0003 px。

## 验证

```powershell
# 1. 用原版引擎渲染参考帧（预乘 RGBA）
python tools/bake_server.py --page bake.html --fps 60 --out <参考输出>
# 2. 录制绘制命令并转换
python tools/bake_server.py --page record.html --fps 60 --out <录制输出>
python tools/convert_drawlist.py <录制输出>/drawlists <clips目录>
# 3. 暂存素材
python Build-SanguoshaAssets.py --animate <killEffect目录> --voices <语音目录> --clips <clips目录> --logo <logo.png>
# 4. 生产 Win2D 渲染器逐帧对比原版引擎输出
KillConfirmCompatibility.exe --validate-sanguosha <测试输出> <Widget/Assets/GameStyles/sanguosha/killconfirm> <参考输出>/raw
```

`tools/` 需要 `laya_lib/`，即从 `libs/min/laya.sgs` 解出的 `laya.core.min.js`、`laya.ani.min.js`、`spine-core-4.0.min.js` 和 `laya.spine.min.js`。这些是游戏代码，不提交到仓库。

在 7 个系列、109 个采样帧上，生产渲染器与原版引擎输出的平均差异最大为 1.05/255。几乎所有帧的 p99 都 ≤ 2，最差 p99 为 14，差异出现在 MSAA 抗锯齿边缘。

## 显存

切换到十周年包时准备 11 条动画的元数据和适用的 LZ4 文件。正常播放仅解码当前帧及邻近帧，复用四个 CPU 帧缓冲，不再把全部图集上传到显存。原 46 张图集的 RGBA 解码总量约 793 MiB；现在仅在缺少精确尺寸/颜色变体时临时加载当前档图集，用于生成缓存和原画回退。换包或显式停止时释放播放资源。

## 5.9.1 清晰度与连续性修复

- 撤销此前 720/1440 px 图集缩小及英雄图 2400 px 缩小，Widget 和 SourceAssets 的 46 张页面恢复为原始 PNG 字节。同步恢复 SGSD 页尺寸，UV、绘制顺序、动画时长、60 Hz 关键姿态和事件数据不变。核验记录位于 `Output/ValidationSanguosha591/original-atlas-restoration.json`。
- 实际呈现时刻与 60 Hz 录制时钟并不同步。只取向下整帧会在边界抖动时重复一个姿态并跳过下一个。现按实际经过时间在相邻姿态之间插值顶点与颜色；仅在两帧绘制数量、区域 ID、混合方式全部一致时插值，附件替换与绘制顺序变化仍离散切换。整数帧输出保持原姿态。
- `--validate-sanguosha` 对全部 11 条动画的每一帧走实际直接绘制、命令列表栅格化及像素读回，记录 p50/p95 耗时，并输出 1x/2x 中间帧。它同时验证有抖动的 60 Hz 时间序列、附件/混合切换不插值以及插值缓冲复用。已有原版引擎逐帧参考图仍可用作可选精度对比。


## 当前源码与安装包边界（5.9.2.3）

`SourceAssets/GameStyles/sanguosha/killconfirm/lz4/` 和对应的 `Widget/Assets/` 目录保留本机离线预渲染实验结果，不提交到 Git，也不随当前正式安装包分发。原始 PNG、SGSD、Native/Lz4 源码与许可证、`Tools/SanguoshaLz4` 重建工具继续入库。缺少离线预渲染文件时，运行时使用精确缓存生成和原图集回退；干净 checkout 不需要复制这两份约 12 GiB 的本机实验目录。

下面的 profile 和导出验收内容为此前实验记录，不代表当前安装包携带这些大型预渲染文件。

<!-- BEGIN SANGUOSHA LZ4 -->
## LZ4 预渲染与显存优化

正式 LZ4 资源在 `killconfirm/lz4/<profile>/`。默认 250% 显示缩放（240 DPI）对应桌面 `desktop-1375x773` 和 UWP `uwp-2750x1548-s1p71875`，每个 profile 含 11 片、共 1516 帧。桌面使用生产的 1375×773 物理像素 / 96-DPI target 与完整居中矩阵链；UWP 必须在真实 240 DPI、1100×619 DIP 的 target 上用原 `.6875` 逻辑缩放生成 2750×1548 物理像素。不能将 96-DPI 合成缩放结果直接当作真实 DPI 已逐像素验收。

原 `SanguoshaShake` 算法及事件保留，使用用户已接受的固定 seed `20261107`，抖动已烘入，播放时不再二次施加。输出为透明 BGRA8 premultiplied 的逐帧 LZ4，压解及落盘重读逐帧 SHA-256 校验；旁置 metadata 记录尺寸、DPI、矩阵、源文件及成品 hash。压缩无损仅指该明确渲染条件下的字节保持，不代表任意尺寸、颜色或刷新率均与原矢量/图集绘制逐像素等价。

该方案用磁盘换显存：两个默认 profile 合计约 **6 GiB**，具体以导出清单为准；这不是安装包压缩后大小，UWP 沙箱中的按需下载缓存还会占用额外磁盘。非默认尺寸或颜色按实际渲染参数自动生成精确缓存，各宿主自生成缓存按旧到新维持 **8 GiB** 预算；UWP 下载的正式三国杀 LZ4 另有独立 8 GiB 缓存预算，不挤占其他资源原有的 256 MiB 预算。未生成相应变体时仅当前档原画兜底。

原始 PNG 图集与 SGSD 继续保留，供原画兜底和新变体生成。当前正常 LZ4 路径只保留播放所需的有界帧缓冲。1600×900 原生参考仅保留于 Output，正式目录不自动包含它。维护入口为 `Tools/SanguoshaLz4`；正式资源同步由 `Sync-ActualUwpAssets.ps1` 校验完整清单后执行。
<!-- END SANGUOSHA LZ4 -->
