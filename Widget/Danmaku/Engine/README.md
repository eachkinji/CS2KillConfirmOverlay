# Danmaku Engine

弹幕逻辑集中在本目录。UI 控件只负责接收游戏事件和绘制，事件判断、反应配额、文案选择、等待队列、轨道布局和飞行动画分别由独立模块负责。

## Event reaction matrix

| 游戏事件 | 识别来源 | 核心弹幕 | 划水弹幕 | 总数 | 优先级 |
|---|---|---:|---:|---:|---:|
| 普通击杀 | `event_kind=kill` | 3 | 2 | 5 | 55 |
| 首杀 | `is_first_kill` | 3 | 2 | 5 | 65 |
| 爆头 | `is_headshot` | 4 | 2 | 6 | 75 |
| 投掷物击杀 | `is_grenade_kill` | 4 | 2 | 6 | 80 |
| 刀杀 | `is_knife_kill` | 4 | 2 | 6 | 85 |
| 连续击杀 | `kill_count >= 2` | 4 | 2 | 6 | 90 |
| 高连杀 | `kill_count >= 5` | 5 | 2 | 7 | 100 |
| 末杀 | `is_last_kill` | 5 | 2 | 7 | 100 |
| 助攻 | `event_kind=assist` | 2 | 3 | 5 | 35 |
| 阵亡 | `event_kind=player_death` | 3 | 2 | 5 | 60 |
| 回合胜利 | `event_kind=round_win` | 3 | 2 | 5 | 70 |
| 回合失败 | `event_kind=round_loss` | 3 | 2 | 5 | 70 |
| 下包 | `event_kind=bomb_plant` | 4 | 2 | 6 | 85 |
| 拆包 | `event_kind=bomb_defuse` | 4 | 2 | 6 | 90 |
| 接触人质 | `event_kind=hostage_interact` | 3 | 2 | 5 | 75 |
| 营救人质 | `event_kind=hostage_rescue` | 4 | 2 | 6 | 85 |

`../6657_memes.json` 是唯一允许的弹幕文本来源。`../Pools/event_reactions.json` 不保存任何弹幕文字，只为上述 16 种事件分别保存 `section + 1-based indices` 的核心/划水序号映射。运行时先校验序号，再按序号读取 6657 原文；主库加载或映射校验失败时不发送弹幕，不允许使用代码内文案或回退文案。

## Runtime invariants

- 活跃弹幕硬上限为 7，事件批次至少包含 5 条。
- 每条弹幕占用独立轨道，运行中不会被新事件抢占或移除。
- 每条弹幕从右侧屏外开始，到左侧完全离屏结束，飞行时间不超过 5 秒。
- 等待队列优先核心弹幕和高优先级事件；只有尚未发出的低优先级弹幕可在队列溢出时被淘汰。
