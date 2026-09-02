# Danmaku Engine

弹幕引擎负责 CS2 局内与直播模拟弹幕的完整生命周期管理。UI 控件 (`DanmakuOverlay`) 仅负责 Canvas 渲染与轨道飞行动画；会话启停、状态监控、语义画像加权、事件冲击调度、等待队列与去重降级均由独立模块负责。

## 核心架构与数据流

```text
       ┌────────────────────────┐
       │   /gsi-status 接口     │
       └───────────┬────────────┘
                   │
         [GsiStatusMonitor] (单例状态源)
                   │
                   ▼ (绿色/开播状态驱动)
      [DanmakuSessionController] (会话控制器)
                   │
 ┌─────────────────┴─────────────────┐
 │                                   │
 ▼                                   ▼
[DanmakuLiveScheduler]        [DanmakuImpulseManager]
 (持续节拍与随机抖动)             (游戏事件冲击与衰减)
         │                           │
         └─────────────┬─────────────┘
                       │
                       ▼
             [DanmakuWeightEngine]
             (硬过滤 + 自然度历史 + 加权轮盘)
             ├── 语义索引: SemanticAnnotationRepository (6657_annotations_v1.json)
             ├── 语义画像: SemanticProfileRepository (semantic_event_profiles.json)
             └── 人工映射: DanmakuEventPoolRepository (event_reactions.json)
                       │
                       ▼
             [DanmakuPendingQueue]
                       │
                       ▼
             [DanmakuOverlay (Canvas UI)]
```

## 会话生命周期 (GSI 绿灯驱动)

1. **统一开播判定**：复用设置页绿灯同一数据源 (`/gsi-status`) 与逻辑（`serviceReachable && posts > 0 && last_post_age_ms <= 120000`）。
2. **非绿变绿（开播）**：启动单一弹幕直播会话，重置历史与调度器，进入持续直播弹幕生成状态。持续为绿不得重复启动。
3. **绿变非绿（下播）**：立即停止产生新弹幕，丢弃等待队列和未发出的事件计划；屏幕上正在飞行的弹幕自然飞离屏幕后结束。
4. **再次变绿**：创建全新会话，跨会话消息不残留。
5. **设置联动**：当弹幕开关关闭时立即停止并释放定时器；重新开启且 GSI 为绿时干净启动。

## 弹幕混合与事件冲击 (Event Impulse)

- **平静阶段**：按基准间隔（约 2.5 ~ 5.0 秒，含随机抖动 Jitter）分发普通直播气氛与闲聊弹幕。
- **事件冲击**：发生游戏事件（如击杀、残局、爆头、多杀等）时，向调度器增加带 TTL 和线性衰减的上下文冲击，在未来数秒内以更高密度（约 0.8 ~ 1.8 秒一条）穿插发送：
  - 约 50% 现有人工事件核心弹幕（高可靠锚点）；
  - 约 25% 语义加权相关弹幕（从 100 类标注语义库根据 target/stance/topic/format 动态匹配）；
  - 约 15% 人工气氛弹幕；
  - 约 10% 闲聊/跑题弹幕（增强自然度）。
- **自然度控制**：
  - 最近已发文本窗口（64 条）硬去重；
  - 细粒度 topic 与 format 短期重复冷却惩罚；
  - 连续相同立场（stance）降权，促进弹幕情感多样化；
  - 权重设上下限（0.05 ~ 50.0），避免乘法爆炸。

## 降级策略与兼容性

- **原始文本只读**：`Widget/Danmaku/6657_memes.json` 永远只读，不修改分类、不修改文本。
- **标注库降级**：若 `6657_annotations_v1.json` 或 `semantic_event_profiles.json` 缺失或解析失败，引擎安全回退至现有人工 16 类事件映射与普通气氛池，不影响弹幕正常显示与原功能。
- **明确不支持实体替换**：本轮排除玩家 ID、昵称、战队名的动态槽位改写，原标注中的 target/entity 仅用于描述 6657 原始语境。

## Event reaction matrix (人工核心锚点)

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

## Runtime invariants

- 活跃弹幕硬上限为 7。
- 每条弹幕占用独立轨道，运行中不会被新事件抢占或移除。
- 每条弹幕从右侧屏外开始，到左侧完全离屏结束，飞行时间不超过 5 秒。
- 诊断日志记录结构化调度事件与源序号，禁止输出完整弹幕文本。
