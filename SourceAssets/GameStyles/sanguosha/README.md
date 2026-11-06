# 三国杀 / Sanguosha

内置“连破播报”系列：首破、连破、三破、四破、五破、六破、七破。第 7 杀以后继续播放七破。来源说明见 `THIRD-PARTY-NOTICES.txt`。

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

七个系列只用到 RegionAttachment，以及 RGBA、Attachment、Translate、Rotate、Scale、Shear、DrawOrder、Event 时间线。没有网格、IK 或路径约束，所以每个绘制命令都是精确的仿射四边形；录制时校验平行四边形误差小于 0.0003 px。

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

每个系列常驻 3–6 张原版 2048 px 图集，约 47–94 MB。运行时常驻首破、当前系列和下一系列，播放时预取下一杀的系列。
