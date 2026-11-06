# 战地 1 / V / 2042 原版 HUD 核对

核对日期：2026-11-06。目标是游戏本体的击杀反馈，不以 Minecraft / RFC 移植效果作为原版依据。

## 来源与证据边界

| 游戏 | 原版参考 | 本轮可确认的内容 | 限制 |
| --- | --- | --- | --- |
| Battlefield 1 | EA / DICE 的 [Gameplay Series: Weapons](https://www.youtube.com/watch?v=3qqkfMXYtKI)，[Gameranx 的视频截图转载](https://gameranx.com/features/id/51507/article/battlefield-1-weapons-2016/)，[本轮检查的原图](https://gameranx.com/wp-content/uploads/2016/07/Battlefield-1-Gameplay-Series-Weapons.mp4_000062466.jpg) | 骷髅在左，红色名字在上，白色武器在下，数字在右；下面是较小的事件文字和较大的累计数字。文字明显不是项目原先统一的粗体。 | 这是 2016 年发售前官方实机视频，不能据此声称正式版的时间曲线、分数规则、透明度或字体文件完全相同。 |
| Battlefield V | [Steam 游戏截图库](https://steamcommunity.com/app/1238810/screenshots/)，[检查的玩家实机截图](https://images.steamusercontent.com/ugc/12483616544154310703/BDE6F31A4CCFA42D6D3C1E94DCC72D1DA06AC704/) | 从上往下为击杀图标、人名与奖励、较大的总数、较小的奖励明细；文字使用较轻字重。 | 玩家可能自定义 HUD 颜色；单帧不能确定爆头环或入场动画的时长。 |
| Battlefield 2042 | EA 官方 [显示设置说明](https://www.ea.com/able/resources/battlefield-2042/pc/display)，[3.2.0 更新说明](https://www.ea.com/ea-play/news/battlefield-2042-update-notes-3-2-0) | 官方明确击杀、爆头颜色可配置；3.2.0 增加可选的底部中央 Skull Train 伤害数字，表明版本与设置会影响反馈。 | 本轮没有取得足以重建正式版完整击杀动画的、明确版本和 HUD 配置的连贯原版录像。 |

前任下载的 `Output/bf-refs/bf2042/bf2042-gcores-enemykilled.png` 来自 [机核文章](https://www.gcores.com/articles/143991)。图中中央 `ENEMY KILLED 100` 与右侧 `+50 XP ENEMY KILLED` 同时出现，文章未说明此图的版本和改图情况。因此不把它当作标准 2042 HUD 的重做依据；也不据此断言该图一定被修改。

本地原版参考图保存在工作区根目录的 `Output/bf-refs-audit/bf1-gameplay.jpg`、`bfv-steam.jpg`，不作为应用资源发行。

## 已修正

- BF1 / V：新增独立 `CreateBattlefieldInfantryTextFormat()`，从统一 `Segoe UI Bold` 调整为 `Segoe UI Normal`。绘制与卡片尺寸测量使用同一格式，避免长名字的测量与渲染不一致。BF4 等使用原共享格式的效果不受影响。
- BFV：图标缩放从最近邻改为线性采样，改善约 22 像素显示尺寸和连续缩放中的锯齿。
- 上述改动在 `Widget/Controls/Animations/` 和 `CompatibilityHost/Renderer/Controls/Animations/` 同步。

Segoe UI 是当前项目的系统字体替代方案，**没有声称找到或集成原版字体**。本轮也没有根据单张截图臆造原版精确字号、间距或毫秒曲线。

## 保留和待进一步核对

- 金额、累计金额、CS2 事件/武器信息和分数来源保持现有行为。原版 BF1/V 的积分与本项目 `$300` 等 CS2 金额语义不同；不能为了视觉相似而把金额擅改为固定 100 分。
- BF1 的分段卡片背景和红名字、BFV 的图标与颜色保留。实际原版颜色允许用户配置，当前证据不足以指定唯一正确颜色。
- 2042 本轮没有改字重、颜色、布局或发光强度。稳定帧里仍可见很强的白/红背景辉光，这是现状记录，不代表已证明与原版一致。需要明确版本的普通击杀、爆头、快速连杀录像，再核对光晕、扫描条、字号及入场/退场曲线。

## 验证

新增 `CompatibilityHost/Validation/BattlefieldOriginalStyleValidation.cs` 和 `--validate-battlefield-original-styles` 入口。每例使用独立 presenter 和隔离设置目录；从入场开始持续调用真实绘制，约 1500 ms 时冻结播放时钟再保存稳定帧。

每款覆盖普通击杀、爆头、长名字和两次快速击杀，共 12 例通过。BFV 单事件断言只有一个图标；BF1/V 断言金额目标严格等于事件数乘 300，避免复用对象将前一案例累计进来。快速双杀的两行奖励正常分开，不是单事件重复绘制。测试不启动真实服务、不播放音频、不写用户设置。

```powershell
dotnet publish CompatibilityHost/CompatibilityHost.csproj -c Release -o Output/BFOriginal44-renderer
& .\Output\BFOriginal44-renderer\KillConfirmCompatibility.exe `
  --validate-battlefield-original-styles `
  'F:\KillConfirm FIX\KillConfirm FIX\Output\BFOriginal44-final' `
  'F:\KillConfirm FIX\KillConfirm FIX\Widget'
```

发布成功；最终截图与 12 项报告在仓库目录 `Output/BFOriginal44-final/`。使用 `*-hold.png` 和 `pass.txt`。早期 `BFStyles43` 为首次非透明入场帧，`BFOriginal44-before/after` 为验证器调试样本，均不作为最终评审证据。

已有回归脚本已适配当前代码结构，均运行通过：

- `Test-FeedbackIsolation.ps1`：补齐 `ResetBattlefield6HudState` 测试桩，并验证 BF1 入场只清理一次，后续奖励不重复重置。原有 7 种奖励/助攻、异步卡片隔离、独立到期、后发击杀优先及 PUBG 文案断言全部保留并通过。
- `Test-Battlefield2042Feedback.ps1`：保留 88 项事件/金额及布局断言；路由断言适配按游戏拆分的 partial，分别核对 Widget 和 CompatibilityHost 的样式分发、实际播放调用及全部参数顺序。刀杀、雷杀、助攻、金额和回合范围信息均正确传递，零奖励占位检查通过。

实际编译运行验证的是 CompatibilityHost；Widget 已同步源码，本轮未单独执行 UWP 构建或 Game Bar 实机验证。
