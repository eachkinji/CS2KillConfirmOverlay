# CF 图标包

在 CF 图标包库中使用“导入图标包”或“批量导入图标包”。每个 ZIP 对应一个独立套装，可有一层或多层外包装目录；不要求 manifest。文件名忽略大小写，支持原有 PNG、JPG、JPEG、WebP、TGA 导入。导入后规范为本地槽位名称。

## 独立资源包（4.5.1.42）

CF 的全部素材现已外置，包括原版。应用源码与安装包不再包含 CF 图标、包头图、音频、公共特效或旧序列帧；游戏选择器的 CF 标识属于界面资源，仍保留。原有 8 套图标和 11 套音频分别发行中文 ZIP，全部带 `pack_head.png`。每套图标包包含自己的主图及公共叠加层；原版包另存旧序列帧。测试目录仅保存无媒体的路由 JSON 样例。

资源包通过通常的单个、批量或拖放导入入口安装到 `LocalState/Packs/crossfire/icon_packs/<id>` 和 `voice_packs/<id>`。这些包使用 `package_kind: crossfire_icon` 或 `crossfire_voice`、`game_style: crossfire`、原有稳定 `id` 及中文 `display_name_zh_cn`。保留旧选择记录，重新导入同名包会更新素材并重新预载；缺少资源的旧内置条目不会继续显示在资源库。普通创作者图标包仍然不需要 manifest。

选择“原版”叠加或缺少事件的对应原图时，从独立安装的原版图标包读取。未安装原版包时没有内置图片兜底，需要先导入原版包。

从仓库外部的素材备份生成这些包：

```powershell
python .\Build-CrossfireExternalPacks.py --source 'D:\icon\CF独立资源\源素材' --output 'D:\icon\CF独立资源\独立包'
.\Tests\Regression\Test-CrossfireExternalPacks.ps1 -PackagesPath 'D:\icon\CF独立资源\独立包'
```

最终 MSIX 构建检查会拒绝再次混入 CF 素材；CSOL 的独立素材保持原有打包方式。

## 素材结构

```text
中文套装名/
  pack_head.png
  BADGE_MULTI1.PNG ... BADGE_MULTI6.PNG
  BADGE_HEADSHOT.PNG / BADGE_HEADSHOT_GOLD.PNG
  BADGE_KNIFE.PNG / BADGE_GRENADE.PNG
  BADGE_C4.PNG / BADGE_C4DEFUSE.PNG
  BADGE_WALLSHOT.PNG / BADGE_HEADWALLSHOT.PNG / BADGE_HEADWALLSHOT_GOLD.PNG
  FIRSTKILL.PNG / LASTKILL.PNG / REVENGE.PNG
  MULTI2_FX.PNG ... MULTI6_FX.PNG
  KILLMARK_HEADSHOT.PNG / KILLMARK_MULTIKILL.PNG
  KILLMARK_KNIFE.PNG / KILLMARK_GRENADE.PNG
  KILLMARK_UPGRADE1.PNG ... KILLMARK_UPGRADE3.PNG
  BADGE_ASSAULT1.PNG ... 等原有兵种徽章
  Sprite/
    SPRITE_01.PNG ... SPRITE_10.PNG
    SPRITENORMAL_01.PNG ... SPRITENORMAL_10.PNG
    SPRITESPECIAL_01.PNG ... SPRITESPECIAL_10.PNG
```

序列帧可以放在根目录或 `Sprite` 子目录。旧 `badgeex` 子目录继续支持。兼容 `[US]FIRSTKILL.PNG`、`[US]LASTKILL.PNG`、`[US]REVENGE.PNG`、`US_REVENGE.PNG` 和 `KILLMARK_UPGRADE_NA_1～3.PNG`；存在标准文件时优先使用标准文件。

## 叠加行为

- 原有主动画、连杀火焰、升级和兵种设置保持现有逻辑。
- “叠加特效 → 跟随图标包”启用包内连杀火焰、事件专属静态叠加、动态叠加。“关闭”不显示这些效果；“原版”使用现有内置火焰。
- `KILLMARK_*` 按击杀、爆头、刀杀、手雷事件选择，不依赖套装名称或编号。
- 黄金爆头和六杀选 `SPRITESPECIAL`，其他支持的击杀事件选 `SPRITENORMAL`；缺少该事件帧时回退到同编号 `SPRITE`。
- 动态层使用 10 个固定槽位，每帧 75 ms。缺帧留空，不压缩时间线。C4、首尾杀横幅、助攻、复仇不播放击杀动态层。
- 为 400×158 动态素材扩展画布宽度，保持主图的显示比例。
- 穿墙、穿墙爆头、复仇、重击资源可导入并通过对应显式事件键播放。素材支持不代表上游事件源一定能检测这些事件。
- 缺失主图沿用原有内置回退；套装只提供部分图标或只有叠加素材也可以导入。

统一文件清单位于 `CrossfirePackFormat`，由导入、编辑器和渲染器共用。

## 动画样式 2（4.5.1.40）

样式 2 使用本地参考播放器的完整分层渲染，样式 1 继续使用既有轨道。

- 主图每 15 ms 增加 0.33 内部缩放值，到 1.4 后归一化为显示比例 1；抖动 200 ms，随后每 20 ms 减少 0.02 透明度，总时长 1275 ms。
- 火焰使用 2.0 内部缩放上限、100 ms 抖动、每 20 ms 减少 0.15 透明度，总时长 345 ms。
- 抖动保留参考程序的负方向伪随机偏移和 60 Hz 往返插值。
- 主图、升级、事件叠加、序列帧使用滤色合成与亮度／饱和度效果；兵种徽章使用普通透明合成；火焰带三层白色阴影发光。
- 主图按事件使用尺寸和锚点，动态层仍保持独立的 75 ms 帧间隔。扩展画布保留横幅和宽幅特效，并维持原有主图显示比例。
- 按播放时钟的真实经过时间采样，30/60 FPS 不改变动画总时长。没有复制参考网页按回调次数计时导致的掉帧减速问题。

时间线可直接与参考 JavaScript 比较：

```powershell
.\Tests\Regression\Test-CrossfireStyle2.ps1 -ReferenceEnginePath 'D:\icon\engine\killmark-anim-engine.js'
```

滤色发生在本应用的图层之间。Game Bar 的透明窗口最终仍由系统合成到游戏上，不读取游戏背景像素来执行网页背景的滤色运算。

## CF 预载（4.5.1.41）

启动、切换图标包或修改火焰、升级、兵种设置时预载当前组合。样式 2 包含全部 25 个事件键及其 5 种兵种组合，共 150 项；相同事件的主图、火焰和事件动画共享已解码素材。导入包只扫描一次素材目录，保留根目录、Sprite、badgeex 及扩展名的查找顺序。

相同设置的预载请求共用任务。已经缓存的 CF 动画直接播放，不等待整包预载锁；尚未缓存的素材仍按序加载。CF 击杀画面不显示 Loading，准备进度保留在设置界面。切换包时释放缓存，异步加载期间设置改变的结果不写入当前缓存。

```powershell
.\Tests\Regression\Test-CrossfirePreloading.ps1
```

该测试执行实际预载清单、素材缓存和播放方法，验证全部事件覆盖、纹理复用、预载锁占用期间缓存播放、目录索引和设置隔离。图形与文件系统使用替身，不替代设备上的视觉检查。

## 整理本地素材

```powershell
python .\Build-CrossfirePacks.py --source 'D:\icon\全部击杀图标与徽章' --output 'D:\icon\CF图标包'
.\Tests\Regression\Test-CrossfirePackLayers.ps1 -PackagesPath 'D:\icon\CF图标包'
```

生成器保留套装源文件、加入明确头图，并为主图套装附带公共连杀火焰。输出文件名和包内根目录使用中文名称，附带清单和 SHA-256。源素材目录不改动。

测试编译实际导入收集函数、目录选择函数及叠加加载函数，以文件系统替身验证所有生成包、嵌套目录、别名、事件选择、缺帧、时间边界和资源释放；不替代游戏内视觉验证。
