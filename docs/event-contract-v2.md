# 事件契约 v2

服务端 `GET /events` 的每条事件在 **v1 扁平字段不变** 的前提下追加 v2 字段。服务与客户端随同一安装包发布，不做版本门控。

代码位置：服务端 `KillConfirmService/src/feedback/`，LOL 详情 `KillConfirmService/src/game_sources/lol/events.rs`，CS 详情 `KillConfirmService/src/game_sources/counter_strike/mapping.rs`；客户端 `Common/Events/`。

## 1. 信封

```json
{
  "id": 42,
  "source_generation": 7,
  "published_unix_ms": 1762531200000,

  "event_channel": "combat",
  "kill_count": 3,
  "is_headshot": false,
  "...": "其余 v1 字段与旧版逐字节一致",

  "schema": 2,
  "source": "lol",
  "intent": {
    "kind": "kill",
    "tier": 3,
    "modifiers": [],
    "play_main": true,
    "actor": "玩家#一区",
    "target": "对手#三区"
  },
  "detail": { "lol": { "event": "champion_kill", "...": "见第 4 节" } }
}
```

| 字段 | 说明 |
| --- | --- |
| `schema` | 固定 `2` |
| `source` | `cs2` / `csgo_legacy` / `lol`：发布时的来源选择，与 `source_generation` 同步更新。客户端不必为每批事件再查询来源 |
| `intent` | 与游戏无关的反馈意图，风格与音频只需要理解这一层 |
| `detail` | 可选；`{"cs": {...}}` 或 `{"lol": {...}}`，供游戏专属的表现、弹幕和统计使用 |

## 2. 通道（`event_channel`）

| 值 | 含义 | 动画 / 音频 |
| --- | --- | --- |
| `combat` | 击杀、助攻、击杀修饰 | 按风格规则 |
| `economy` | CS 经济 / 目标 | 仅限计分类风格 |
| `observation` | **来源事实记录**（如 LOL 全部 Riot 事件） | **从不触发**；客户端在进入风格路由前分流 |

## 3. `intent`

| 字段 | 取值 |
| --- | --- |
| `kind` | `kill` / `assist` / `death` / `objective` / `match_result` / `observation` |
| `tier` | 反馈档位：CS 为服务端计算的连杀数，LOL 为官方 `Multikill` 档位（1–5）；非击杀为 0 |
| `modifiers` | `headshot` / `melee` / `explosive` / `first` / `last`（仅 `kill`） |
| `play_main` | 是否播放主动画；`observation` 恒为 `false` |
| `actor` / `target` | 可选；为空字符串时省略 |

v1 → intent 映射（服务端唯一一处：`feedback/intent.rs`）：

| v1 `event_kind` / 条件 | `intent.kind` |
| --- | --- |
| `event_channel == observation` | `observation` |
| `is_assist` 或 `assist` | `assist` |
| `player_death` | `death` |
| `bomb_plant` / `bomb_defuse` / `hostage_interact` / `hostage_rescue` | `objective` |
| `round_win` / `round_loss` | `match_result` |
| 其他（`kill`、`kill_confirmation`…） | `kill` |

## 4. `detail.lol`

| 字段 | 说明 |
| --- | --- |
| `event` | 规范化事件名，见下表；未知事件为 `other` |
| `riot_event_name` / `riot_event_id` / `game_time` | Riot 原始事件名、`EventID`、`EventTime` |
| `relation` | 事件主体相对本机的阵营关系：`self` / `ally` / `enemy` / `neutral`（小兵、防御塔、野怪、无主体）/ `unknown`（缺阵营或名称不唯一） |
| `local_role` | 本机在事件中的角色：`killer` / `recipient` / `acer` / `victim` / `assister` / `none` |
| `actor` / `victim` / `assisters` | 主体（击杀者 / `Recipient` / `Acer`）、受害者、助攻者 |
| `multikill` | `Multikill.KillStreak`；本机击杀为合并后的档位（≥2 时出现） |
| `acing_team` / `dragon_type` / `stolen` / `structure` / `result` | `AcingTeam`、`DragonType`、`Stolen`（兼容 `"True"` / `"False"` 字符串）、防御塔/水晶名、`GameEnd.Result` |
| `extra` | 仅 `other`：未识别字段的原始 JSON（≤ 4 KiB） |

| Riot `EventName` | `event` | 发布方式 |
| --- | --- | --- |
| `ChampionKill`（本机击杀） | `champion_kill` | **combat 击杀反馈（现有行为）**，附 `detail.lol` |
| `ChampionKill`（其他情况，包括本机助攻 / 被击杀） | `champion_kill` | observation，用 `local_role` 区分 |
| `Multikill` | `multikill` | observation（本机档位同时并入击杀的 `kill_count`） |
| `GameStart` `MinionsSpawning` `FirstBrick` `FirstBlood` `Ace` `TurretKilled` `InhibKilled` `InhibRespawningSoon` `InhibRespawned` `DragonKill` `HeraldKill` `BaronKill` `HordeKill` `GameEnd` | 对应的 snake_case | observation |
| 其他 | `other` | observation |

observation 事件的 v1 投影：`event_channel = "observation"`、`event_kind = "lol_<event>"`、`kill_count = 0`、`play_main_animation = false`、`player_name = actor`、`target_name = victim`。

规则：
- 基线规则与击杀一致：首次接入、断线、切换来源、对局时间回退、`EventID` 重置都会重建基线，**不补发历史事件**。
- observation 不经过 600 ms 合并窗口，按 `EventID` 顺序在当次轮询发布；同一轮中先发布本机击杀反馈，再发布 observation。
- observation 字段格式异常（类型不符）时按缺省值处理，不会让整份快照解析失败。

## 5. `detail.cs`

`event_kind`、`animation_key`、`weapon_badge_key`、`weapon_name`、`money_reward`、`round_number`、`money_epoch`、`steamid`，与 v1 同名字段取值一致。

## 6. 触发衔接（待定，P7）

本机相关的 `first_blood` / `ace` 计划复用 `first` / `last` 修饰，`champion_kill` + `local_role=assister` 映射为 `assist`。实施时只修改 `lol/mapping.rs`（以及 Tracker 中的配对逻辑），信封格式不变。
