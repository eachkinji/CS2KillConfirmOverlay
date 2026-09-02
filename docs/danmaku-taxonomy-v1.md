# 6657 弹幕多维多标签语义标注体系 (Taxonomy v1) 规范文档

## 1. 项目背景与设计哲学

本项目旨在为 CS2KillConfirmOverlay 弹幕系统的 23521 条 6657 经典弹幕库建立一套工业级、细粒度、多维正交的语义标注体系（Taxonomy v1）。

### 核心设计原则
1. **语义特征与触发事件解耦 (Semantic vs Event Decoupling)**：
   - **语义标注关注文本本身**：刻画弹幕的语言学特征、修辞结构、情感立场、谈论主体与细粒度语义话题。
   - **游戏事件联动独立查询**：Taxonomy 绝非游戏内触发事件分类（如“击杀/阵亡/拆包/残局”）。后续 CS2 实时事件系统（如 `event_reactions.json`）将基于具体的游戏事件策略，向此多维语义库发起精准标签组合查询。
2. **KISS 原则与受控词表精确计数口径**：
   - **核心语义受控标签严格控制为 100 项**：
     - `targets` (8) + `stances` (13) + `topics` (39) + `formats` (17) + `culture` (7) + `context` (4) + `safety.severity` (4) + `safety.flags` (8) = **100 项**。
   - **全体系受控枚举总计 113 项**：
     - 在 100 项核心语义基础上，计入元数据维度的 `entities.type` (9) + `review.status` (4) = 113 项。
   - 专有名词采用可扩展 `entities` 列表（`[{"name": "...", "type": "..."}]`），不将动态人名/战队硬编码为互斥大类。
3. **单向索引绑定与零文本冗余**：
   - 原始文本源文件 `Widget/Danmaku/6657_memes.json` 保持严格不可变（23521 条，SHA256 锁定）。
   - 标注文件必须使用 `1-based index` 关联（1..23521），**绝对禁止在标注数据中包含或篡改原始弹幕文本**。
4. **分权协作与审核防伪造**：
   - 标注阶段：生成样本全部标为 `pending` 状态，严禁在未获批准前指定 `reviewer`。
   - 监督验收：待宿主（GPT-5.6 Sol）复审批准后方可转为 `approved`。

---

## 2. 标注维度与受控词表总览

| 维度字段 | 类型 | 选型 | 含义与枚举清单 |
|---|---|---|---|
| `index` | integer | 必填 | 1..23521 的 1-based 原始弹幕全局索引 |
| `targets` | string[] | 多选 (≥1) | **指涉对象** (8类)：`streamer`, `pro_player`, `pro_team`, `caster_host`, `chat_audience`, `game_system`, `external_figure`, `general_meta` |
| `stances` | string[] | 多选 (≥1) | **立场与情感** (13类)：`flame_streamer`, `flame_player`, `flame_team`, `flame_audience`, `flame_caster_host`, `flame_external_figure`, `cheer_praise`, `comfort_support`, `tease_playful`, `cynical_sarcastic`, `hype_excitement`, `melancholy_lament`, `neutral_informative` |
| `topics` | string[] | 多选 (≥1) | **细粒度语义话题** (39类)：涵盖主播生态(13类)、职业赛事(18类)、跨界日常泛文化(8类) |
| `formats` | string[] | 多选 (≥1) | **表达修辞与形式** (17类)：含 `plain_statement`、`direct_address_at`、`fake_official_news`、`quotation_remix`、`poem_lyric`、`copypasta_repetition`、`announcement_ad` 等（支持单选 `plain_statement`） |
| `culture` | string[] | 多选 (≥1) | **文化圈层** (7类)：`origin_6657`, `origin_cs_community`, `origin_bilibili`, `origin_abstract`, `origin_acg_subculture`, `origin_gaming_general`, `origin_internet_folklore` |
| `entities` | object[] | 数组 (≥0) | **专名实体**：`[{"name": string, "type": enum}]`，`type` 可选 (9类)：`streamer`, `player`, `team`, `coach`, `caster`, `acg_character`, `game_asset`, `org`, `other` |
| `context` | string | 单选 | **语境依赖度** (4类)：`standalone`, `stream_context`, `game_event`, `pro_scene_history`（表示文本理解对外部语境的依赖程度，非游戏事件分类） |
| `safety` | object | 对象 | **安全控制**：`{"severity": enum, "flags": string[]}`，包含 4 级严重度与 8 类违规特征 flags |
| `confidence` | number | 单值 | **标注置信度**：`0.0` ~ `1.0` (浮点数) |
| `review` | object | 对象 | **审核状态**：`{"status": enum, "reviewer": string, "comments": string}`，状态包含 4 类 (`pending`, `reviewed`, `approved`, `disputed`) |
| `source_tags` | string[] | 可选 | **历史/种子分类** (仅在具有线上精确匹配证据时方可填写) |

---

## 3. 细粒度话题领域 (Topics, 39类) 详解

### A. 主播相关生态 (13类，仅限明确确指玩机器本人时使用)
1. `streamer_appearance_pig_weight`: 主播外貌体型、猪梗、体重身材、面相发型调侃。
2. `streamer_intelligence`: 主播智商、大聪明、反应迟钝、逻辑理解力调侃。
3. `streamer_skill_gameplay`: 主播个人 CS 游戏水平、枪法身法、天梯段位、下饭空枪。
4. `streamer_casting_knowledge`: 主播赛事解说水平、战局分析、预言剧本、解说风格评价。
5. `streamer_bias_favoritism`: 主播解说屁股歪、偏袒特定战队/选手、双标评论。
6. `streamer_stubborn_rage`: 主播情绪嘴硬、红温破防、狡辩甩锅、急躁互动。
7. `streamer_schedule_laziness`: 主播作息懒惰、迟到早退、鸽播、加钟熬夜、修仙作息。
8. `streamer_tech_hardware`: 主播设备技术、电脑配置、OBS 画质模糊、外设麦克风事故。
9. `streamer_commercial_gifting`: 主播商业化、办卡开通舰长、开箱抽奖、广告赞助商务。
10. `streamer_smoking_health`: 主播抽烟、咽炎咳嗽、健康状况、体检日常。
11. `streamer_food_diet`: 主播外卖夜宵、饮食结构、吃药喝水。
12. `streamer_romance_sexuality`: 主播恋爱感情、相亲八卦、性相关话题戏谑。
13. `streamer_career_finance`: 主播职业生涯、收入身价、录像店老板、早年客服经历、经济账。

### B. 职业选手与战队 (18类)
1. `pro_gunplay_aim`: 选手枪法定位、扫射控枪、准星瞄准微操。
2. `pro_headshot`: 选手精准爆头、一颗定位秒杀、爆头率讨论。
3. `pro_sniper_awp`: 选手狙击、AWP 甩狙、大狙架点、防守狙博弈与空枪。
4. `pro_utility_grenade`: 选手道具投掷、闪光弹配合、烟雾封锁、燃烧弹失误。
5. `pro_knife_melee`: 选手近战刀杀、背刺侮辱、电击枪幽默名场面。
6. `pro_entry_trade`: 选手突破手 Entry 首杀、补枪人头互换、拉枪线。
7. `pro_multikill_clutch`: 选手多杀残局、1vX 极限博弈、神级反杀高光。
8. `pro_whiff_blunder`: 选手白给失误、空枪背身、迷路战犯级表现。
9. `pro_eco_save`: 经济局决策、保枪、强起 Eco 翻盘、买枪策略。
10. `pro_score_outcome`: 比赛实时比分、输赢胜负走势、赛点加时决胜。
11. `pro_comeback_choke`: 翻盘与被翻盘、痛失好局、窒息被逆转。
12. `pro_tactics_igl`: 战术体系、战术指挥 IGL 决策、暂停调整、赛前布置。
13. `pro_ranking_rating`: 排名数据、Rating、TOP20 排名、世界战队积分榜。
14. `pro_transfer_roster`: 战队转会期变阵、选手身价买断、替补试训。
15. `pro_trophy_history`: 战队/选手冠军历史、Major 夺冠荣耀、王朝兴衰。
16. `pro_cheating_matchfixing`: 作弊质疑、雷达透视、假赛菠菜盘口、道德违规。
17. `pro_personal_appearance`: 职业选手/教练/圈内人物外貌颜值、身材体型、穿搭发型调侃（与主播外貌严格隔离）。
18. `pro_personal_relationships`: 职业选手/教练/圈内人物恋爱感情、女友八卦、家庭婚恋、CP戏谑（与主播感情严格隔离）。

### C. 泛文化、日常与跨界 (8类)
1. `external_sports_competition`: F1/足球/篮球等非电竞赛事、车队/俱乐部、比赛规则、赛果比分与竞技讨论。
2. `external_figure_personal_life`: 圈外人物/现实名人/跨界角色的外貌身材、恋爱家庭、私生活八卦（与主播及CS职业圈严格隔离）。
3. `daily_life_work`: 现实生活打工、考研考公、日常琐事、生活哲学感慨。
4. `acg_popculture`: 二次元动漫 (如 MyGO, BanG Dream 等)、影视游戏跨界亚文化。
5. `historical_memes`: 经典名场面复刻、历史老梗、“那晚”、老 CS 版本情怀。
6. `hardware_skin_economy`: CS 饰品皮肤、开箱磨损贴纸行情、硬件外设。
7. `pro_scene_drama`: 职业圈八卦节奏、解说恩怨、社区撕扯互喷。
8. `misc_unclear`: 其它杂项、语义模糊、无明确意图的零散语句。

---

## 4. 文本表达形式 (Formats, 17类) 详解

一条弹幕可标注多个表达形式；对于没有特殊修辞的常规文本，**明确支持且允许单选 `plain_statement`**。

- `plain_statement`: 普通直陈/平铺直叙陈述句 (如 “感觉自己老了，也不该看直播了”)。
- `direct_address_at`: 艾特呼叫/喊话/指名道姓 (如 “玩大哥你还记得...”, “刘一博社员...”)。
- `fake_official_news`: 假新闻/伪造官方通报/洋葱新闻 (如 “官方通报：冬瓜强社长...”)。仅用于仿公文通报语态，普通玩笑不应滥用。
- `quotation_remix`: 名言台词化用/经典语录改写/混搭台词。
- `poem_lyric`: 定型诗词/押韵打油诗/歌词改编。
- `copypasta_repetition`: 长段发疯小作文/复读机/长篇复制粘贴。
- `announcement_ad`: 公告通知/抽奖活动启事/买卖招租广告。
- `single_word_or_char`: 单字/超短词 (如 '6', '?', '急', '典'，仅限长度 <= 3 超短词)。
- `repeated_symbols`: 连续标点/连续 emoji 刷屏 (如 '???', '！！！', '😭😭😭')。
- `formulaic_template`: 经典定式句型 (如 “以前...现在...”, “我可以接受...但我不能接受...”)。
- `idiom_adaptation`: 成语化用/四字古文词组化用。
- `parallelism_listing`: 排比/盘点/列举式阵容 (如 “TOP1: ... TOP2: ...”)。
- `rhetorical_question`: 反问/质问句。
- `dialogue_roleplay`: 对话剧场/角色扮演演绎 (如 “长崎素世：... 千早爱音：...”)。
- `slang_argot`: 密集黑话/黑称/专属缩写。
- `exaggeration_hyperbole`: 夸张夸大/神格化/极端比喻。
- `homophonic_pun`: 谐音梗/谐音双关 (如 “难宫雨”, “出身主义”)。

---

## 5. 专名实体 (Entities) 判定与规范化准则

专名实体数组 `entities` 采用开放式对象列表 `[{"name": string, "type": enum}]`。为了保证标注的一致性与严谨性，必须恪守以下实体判定与提取原则：

1. **严格文本锚定原则 (Strict Grounding)**：
   - 实体仅在**文本中明确提及、存在公认稳定别称（如“哈基玩/玩大哥/大表哥/雨神/猎小鹰”）或具有绝对确凿指代**时方可加入。
   - **绝对禁止仅因 `culture` 标记了 `origin_6657` 就泛化自动添加“玩机器”实体**。如果文本仅使用第二人称“你”、泛指“兄弟们”或自言自语而未出现主播明示名称/稳定外号，严禁在 `entities` 中添加玩机器。
2. **禁止过度脑补与臆测**：
   - 对于跨界文化或二创对话中的外部角色，若文本未明示具体作品或角色全称且存在多种可能，不要凭空断定具体实体，可仅标注已明确确证的实体（如《黑神话：悟空》）或置空。
   - 严禁强行将文本中未出现且无代称的人物/选手（如把无 NiKo 的 Falcons 战术讨论硬塞 NiKo）加入实体列表。
3. **跨圈对象处理规范 (Cross-Scene Objects)**：
   - DOTA2 / LOL / F1 / 足球等圈外主体（如 `Ame`, `Faker`, `T1`, `C罗`, `Verstappen` 等）：
     - `targets` 必须标注为 `external_figure`，**严禁机械标注为 CS 的 `pro_player` 或 `pro_team`**。
     - `entities.type` 统一归入 `other` 或 `org`。
4. **实体类型与战队教练 (Coach)**：
   - 战队教练（如 `zonic`, `hally`, `B1ad3`, `XTQZZZ`, `kassad`, `Taz`, `kuben` 等）统一使用独立实体类型 `coach`，**严禁塞入 caster/player/other**。
5. **实体名称规范化 (Canonical Naming & Aliases)**：
   - **职业战队**：统一使用战队标准官方英文名（如 `FaZe`, `Spirit`, `Vitality`, `Falcons`, `NaVi`, `Liquid`, `Virtus.pro`, `G2`, `MOUZ`, `Astralis`, `TYLOO`, `LVG`, `The MongolZ` 等）。
   - **职业选手**：统一使用官方比赛 ID 标准大小写（如 `s1mple`, `ZywOo`, `NiKo`, `m0NESY`, `donk`, `sh1ro`, `karrigan`, `GuardiaN`, `ELiGE`, `apEX` 等）。
   - **战队教练**：统一使用规范英文 ID（如 `zonic`, `hally`, `B1ad3`, `XTQZZZ`, `kassad` 等）。
   - **解说/主播/人物**：统一使用标准中文/官方名（如 `玩机器`, `刘一博`, `冬瓜强`, `QUQU`, `马西西` 等）。
   - **ACG 角色/游戏资产**：统一使用规范全名（如 `长崎素世`, `千早爱音`, `丰川祥子`, `黑神话：悟空` 等）。
   - 详细规范名与别名映射见 `Widget/Danmaku/Annotation/entity_aliases.v1.json`。

---

## 6. 安全强度与毒性控制 (Safety) 规范

安全字段结构为对象：
```json
"safety": {
  "severity": "safe" | "borderline_playful" | "sensitive_flame" | "toxic_vulgar",
  "flags": ["none"] | ["profanity", "personal_attack", ...]
}
```

### 严重度 `severity`
- `safe`: 安全无害（中性、客观讨论、正常赞赏、温和玩梗，无脏话攻击）。
- `borderline_playful`: 擦边戏谑（直播间常见轻度互损、弱冒犯调侃，属于良性互动）。
- `sensitive_flame`: 敏感/较重攻击（尖锐对立情绪、强烈破防、激烈开会、包含明显粗口脏话）。
- `toxic_vulgar`: 严重违规剧毒（恶性人身辱骂、极度下流粗俗、高危违规词汇）。

### 标志 `flags` (多选)
- `profanity`: 脏话/粗口/粗鄙词汇（**注意：强烈脏话严禁标为 safe**，一旦出现 profanity，severity 至少为 `borderline_playful` 或 `sensitive_flame`）。
- `personal_attack`: 人身攻击/对外貌、智力或人格的恶性侮辱。
- `sexual_content`: 性暗示/低俗涉黄/黄色戏谑。
- `violent_imagery`: 暴力血腥意象/身体伤害威胁。
- `discriminatory`: 歧视/地域黑/群体侮辱。
- `self_harm`: 自残自杀诱导/极端绝望。
- `spam_noise`: 垃圾广告/无意义字符刷屏。
- `none`: 无安全风险（仅当 flags 无任何上述风险项时使用）。

---

## 7. 批次划分清单 (Manifest) 与校验约束

全量 23521 条数据按每批 500 条（第 48 批 21 条）划分在 `manifest.json` 中：
- `batch_001.json`: Index 1 .. 500
- `batch_002.json`: Index 501 .. 1000
- ...
- `batch_048.json`: Index 23501 .. 23521

### 校验工具 `validate_annotations.py` 核心铁律
1. **严格 JSON Schema 验证**：通过 `jsonschema` 严格执行 `additionalProperties: false` 及全部字段约束。
2. **批次无缝 100% 覆盖**：每批内部索引必须从 `start_index` 严格单调连续覆盖至 `end_index`，绝不允许遗漏任何中间序号。
3. **标签去重**：各数组字段禁止重复值。
4. **审核反伪造**：`pending` 状态下严禁包含非空 `reviewer`。
5. **覆盖率检查退出码**：`--check-coverage` 在批次未完整就绪时默认返回非零退出码（exit code 1），仅在显式传入 `--allow-incomplete` 时以 0 退出。
6. **态度立场与主体强一致性 (Stance-Target Coherence)**：
   - `flame_streamer` -> `streamer`
   - `flame_player` -> `pro_player`
   - `flame_team` -> `pro_team`
   - `flame_audience` -> `chat_audience`
   - `flame_caster_host` -> `caster_host`
   - `flame_external_figure` -> `external_figure`
   - 严禁为了迎合校验而机械硬加 target，必须依据弹幕字面真实指涉与立场修正 stance/target。
