# CF 图标包

在 CF 图标包库中使用“导入图标包”或“批量导入图标包”。每个 ZIP 对应一个独立套装，可有一层或多层外包装目录；不要求 manifest。文件名忽略大小写，支持原有 PNG、JPG、JPEG、WebP、TGA 导入。导入后规范为本地槽位名称。

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

## 整理本地素材

```powershell
python .\Build-CrossfirePacks.py --source 'D:\icon\全部击杀图标与徽章' --output 'D:\icon\CF图标包'
.\Tests\Regression\Test-CrossfirePackLayers.ps1 -PackagesPath 'D:\icon\CF图标包'
```

生成器保留套装源文件、加入明确头图，并为主图套装附带公共连杀火焰。输出文件名和包内根目录使用中文名称，附带清单和 SHA-256。源素材目录不改动。

测试编译实际导入收集函数、目录选择函数及叠加加载函数，以文件系统替身验证所有生成包、嵌套目录、别名、事件选择、缺帧、时间边界和资源释放；不替代游戏内视觉验证。
