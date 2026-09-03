# 6657 弹幕事件适配二次标注基础设施 (Event Fit Annotation V2 - 人工白名单精修版)

本目录为 6657 弹幕面向 CS2 游戏即时事件冲激（Impulse Driving）与余波复盘（Aftermath Replay）构建的独立二次事件适配标注体系（Taxonomy & Dataset V2）。

---

## 核心设计与准入铁律

1. **人工逐条裁决白名单唯一真值 (Human-Reviewed Allowlist as Single Truth)**
   - 全量具体事件标签严格由人工逐条裁决白名单 [`reviewed_allowlists.event_fit.v2.json`](file:///F:/KillConfirm%20FIX/KillConfirm%20FIX/Widget/Danmaku/EventFitAnnotationV2/reviewed_allowlists.event_fit.v2.json) 提供。
   - 正则仅用于候选挖掘，未在人工白名单中的条目一律归为 `ambient_only`，杜绝正则自动批准带来的语义漂移。

2. **严格即时事件对齐 (Strict Instant Event Fit)**
   - `opening_wait`: 仅限观众在主播未播/刚上线瞬间说的开门催播；
   - `kill_praise`: 仅限直接夸赞当前主播击杀/枪法/控枪；
   - `death_flame`: 仅限直接开喷当前主播死亡/失误/菜/白给/空枪；
   - `death_question`: 仅限针对当前主播暴毙/致命失误的问号或难以置信反问；
   - `round_win` / `round_loss`: 仅限直接庆祝或哀叹当前小局胜负；
   - 任何需要解释上下文才能勉强成立的条目一律归入 `ambient_only`。

3. **非主播专名实体绝对排除 (Zero Non-Streamer Entity Leakage)**
   - 出现除当前主播自身别名（玩机器/玩神/机哥/6657/刘一博）之外的任何选手、战队、解说、其他主播、IP角色一律归入 `ambient_only`。

4. **剔除所有泛关键词误命中 (Anti-Regression)**
   - 彻底剔除借用开播/下播/牛逼/没了/输赢等词汇进行日常闲聊、梗研究、生活抱怨、论文/礼物/房管等非即时话题。

5. **宁少勿滥 (Precision over Padding)**
   - 如实反映社区梗库在严苛标准下的极少数真实存量（全库共 33 条真实事件驱动弹幕），坚决不降标凑数。

---

## 目录结构

```
Widget/Danmaku/EventFitAnnotationV2/
├── README.md                           # 本说明文档
├── schema.event_fit.v2.json            # JSON Schema Draft 2020-12 严格约束
├── taxonomy.event_fit.v2.json          # 受控分类体系与时相定义
├── reviewed_allowlists.event_fit.v2.json # 人工逐条裁决唯一批准白名单
├── event_fit_annotations_v2.json       # 全量 23,521 条二次事件适配标注数据
├── supplemental_opening_wait_v2.json   # 独立新增的 140 条开门/催播/等待/开播到场弹幕
├── supplemental_session_end_v2.json    # GSI 结束专用的 10 条原库告别/刷新失望弹幕
├── supplemental_kill_praise_v2.json    # 独立新增的 140 条短促击杀夸奖弹幕
├── supplemental_death_question_v2.json # 独立新增且明确标注来源的 40 条死亡问号反应
├── supplemental_death_flame_source_v2.json # 原库引用的 15 条骂菜/空枪/白给弹幕
├── scripts/generate_event_pools_v4.py   # 从6657原库及语义标注生成16个千条事件池
├── validate_event_fit_v2.py            # 严格自动化自检与防回归验证工具
├── validate_supplemental_opening_wait_v2.py # 开场补充池单行、去重、意图与实体校验
├── validate_supplemental_session_end_v2.py # 结束池源索引、哈希、语义家族与去重校验
├── validate_supplemental_kill_praise_v2.py # 击杀夸奖池单行、去重、正向意图与实体校验
├── validate_supplemental_death_question_v2.py # 死亡问号池来源、短句、去重与问号校验
├── validate_supplemental_death_flame_source_v2.py # 死亡骂菜池源索引、原文与哈希校验
├── reports/
│   ├── quality_report.md               # 质量报告：精确分布、时相与直球度、实体与泛话题剔除分析
│   └── review_samples.md               # 审阅样本集：全量展示批准入选原文及详细拒绝反例
└── scripts/
    ├── build_dataset_v2.py             # 白名单驱动的数据集生成脚本
    └── generate_reports.py             # 质量报告与审阅样本生成脚本
```

---

## 验证与运行指令

运行全量自检与防回归测试：
```bash
python Widget/Danmaku/EventFitAnnotationV2/validate_event_fit_v2.py
python Widget/Danmaku/EventFitAnnotationV2/validate_supplemental_opening_wait_v2.py
python Widget/Danmaku/EventFitAnnotationV2/validate_supplemental_session_end_v2.py
python Widget/Danmaku/EventFitAnnotationV2/validate_supplemental_kill_praise_v2.py
python Widget/Danmaku/EventFitAnnotationV2/validate_supplemental_death_question_v2.py
python Widget/Danmaku/EventFitAnnotationV2/validate_supplemental_death_flame_source_v2.py
```

## 独立开场补充池

`supplemental_opening_wait_v2.json` 不占用原库索引，也不修改旧标注。它提供 140 条经过人工整理的开场专用短弹幕：`open_door` 40 条、`urge_start` 40 条、`waiting` 30 条、`arrival` 30 条。运行时已在 GSI 会话开场阶段接入，前半段优先发送 `open_door` / `urge_start`，稳态日常和游戏事件不读取该池。

`supplemental_session_end_v2.json` 引用原库中 10 条经过人工复核的 GSI 结束弹幕，其中 4 条属于“刷新→失望→崩溃”模板。结束调度每次推荐播放 4 条，同一语义家族最多 1 条，避免近义变体在同一次结束阶段连续复读。

`supplemental_kill_praise_v2.json` 不占用原库索引，也不修改旧标注。它提供 140 条击杀专用夸奖短句：`short_hype` 40 条、`aim_praise` 40 条、`kill_hype` 40 条、`aftermath_praise` 20 条。突发阶段优先短句，余波阶段再使用枪法、关键击杀和局势评价。

`supplemental_death_question_v2.json` 提供 40 条死亡瞬间问号反应：纯问号、难以置信、死亡反问、空枪反问各 10 条。原始 23,521 条中不存在纯 `？/？？？` 独立弹幕，因此该池明确标为 `new_curated`，不得表示成原库摘录。

`supplemental_death_flame_source_v2.json` 只引用原始弹幕库，提供 15 条直接骂主播菜、空枪、白给或劝别玩的弹幕；每条均保留 1-based 原始索引、完整原文与 SHA-256，运行时仅把原文中的换行渲染为空格。

以上五个 V2 补充池保留作历史标注与对照数据，当前不再打入应用包。

## 当前运行时：千条事件池 V4

当前事件池位于 `../EventPools/`：16类事件各自一个独立 JSON，每个文件至少1000条。所有条目均为 `context_rewrite`，并保留 `source_index`、可在对应原文中逐字找到的 `source_excerpt`、单行 `text` 和 `template_id`。运行时会回查 `6657_memes.json` 并强制检查每池最低数量、文本唯一性及来源一致性。

事件派发只读取这16个事件池，不再读取原生小池，也不再使用全量语义库作为事件兜底。全量语义标注仍用于日常氛围选择以及事件池生成时的素材过滤。开场与结束数据独立位于 `../LifecyclePools/`。

日常氛围弹幕拥有独立时间轴，从 GSI 会话开始持续到结束；事件冲激只叠加事件弹幕，不再暂停日常流。每个游戏事件仍拥有独立冲激与余波，多个同时发生的事件不会互相覆盖。
