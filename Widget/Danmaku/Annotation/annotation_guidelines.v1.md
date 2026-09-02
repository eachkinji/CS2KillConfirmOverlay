# 6657 弹幕多维语义标注质量规范指南 (Annotation Guidelines v1)

## 1. 核心铁律与基础原则

1. **源数据不可变性 (Source Immutability)**：
   - 原始文件 `Widget/Danmaku/6657_memes.json`（23521 条，SHA256 严格锁定为 `9bd3ed7ae963714a34d481bde45df597e4d4db49ee23c39d67506f11b4e32183`）绝对不可修改、不可重排。
   - 标注条目严禁复制、篡改或携带原始文本字段（`text`, `content` 等），一律使用 `1-based index` 索引引用。
2. **客观事实与严格文本锚定 (Strict Grounding)**：
   - 标注仅基于弹幕字面实际出现的内容与公认稳定指代，**严禁脑补与过度推理**。
   - 严禁仅因文化标签为 `origin_6657` 就机械泛化添加实体“玩机器”。
3. **审核状态反伪造 (Review Anti-Spoofing)**：
   - 生成/返修标注条目状态一律为 `pending`。
   - 在 `pending` 状态下严禁填写 `reviewer` 字段；未获宿主正式复审批准前，严禁伪称 `approved`。

---

## 2. 实体提取与规范化细则 (Entities & Aliases)

### 2.1 实体类型定义 (Entity Types)
- `streamer`: 玩机器/主播本人（规范名统一为 `玩机器`）。
- `player`: CS 职业选手（使用官方比赛 ID 标准大小写，如 `s1mple`, `ZywOo`, `NiKo`, `m0NESY`, `donk`, `sh1ro`, `karrigan`, `ELiGE`, `apEX` 等）。
- `team`: CS 职业战队（使用标准官方英文名，如 `FaZe`, `Spirit`, `Vitality`, `Falcons`, `NaVi`, `Liquid`, `Virtus.pro`, `G2`, `MOUZ`, `Astralis`, `TYLOO`, `LVG`, `The MongolZ` 等）。
- `coach`: **战队教练/监督**（如 `zonic`, `hally`, `B1ad3`, `XTQZZZ`, `kassad`, `Taz`, `kuben` 等，**严禁挤压归入 player/caster/other**）。
- `caster`: CS 解说/主持/主播同行（如 `冬瓜强`, `QUQU`, `马西西`, `CSBOY`, `茄子` 等）。
- `acg_character`: 二次元/动漫/虚构角色（如 `长崎素世`, `千早爱音`, `丰川祥子`, `千石由乃` 等）。
- `game_asset`: 游戏本体/地图/饰品资产（如 `Cache`, `Vertigo`, `黑神话：悟空` 等）。
- `org`: 官方机构/赛事方/组织平台（如 `Valve`, `BLAST`, `HLTV`, `CNCS`, `斗鱼`, `哔哩哔哩` 等）。
- `other`: 跨圈现实名人/其他外部实体（如 `C罗`, `姆巴佩`, `Verstappen`, `亚里士多德`, `OBS` 等）。

### 2.2 跨圈对象处理规范 (Cross-Scene Entities)
- **DOTA2 / LOL / F1 / 足球等圈外主体**：
  - 选手/选手外号（如 `Ame`, `Faker`, `Chovy`, `Bin`, `JackeyLove`, `Verstappen`, `梅西`, `C罗`）或战队/车队/俱乐部（如 `T1`, `BLG`, `EDG`, `Gen.G`, `XG`, `Team Secret`, `Red Bull Racing`, `Ferrari`, `Real Madrid`）：
    - `targets` 必须标注为 `external_figure`，**严禁机械标注为 CS 的 `pro_player` 或 `pro_team`**。
    - `entities.type` 统一归入 `other` 或 `org`。
    - `culture` 应标注 `origin_gaming_general` 或 `origin_internet_folklore`，**禁止套用 CS 枪法/经济/道具等游戏专属 topics**。
    - 涉及非电竞赛事、车队/俱乐部、比赛规则与赛果时，使用 `external_sports_competition`。
    - 涉及圈外人物外貌身材、恋情八卦、家庭私生活时，使用 `external_figure_personal_life`。

### 2.3 规范名映射铁律
- 提取实体时必须转换并使用官方规范名（参考 `entity_aliases.v1.json`）。
- 示例：
  - “大表哥 / 卡里根” -> `{"name": "karrigan", "type": "player"}`
  - “绿龙 / 绿龙队” -> `{"name": "Spirit", "type": "team"}`
  - “佐尼克 / 光头教练” -> `{"name": "zonic", "type": "coach"}`
  - “哈利 / 绿龙教练” -> `{"name": "hally", "type": "coach"}`
  - “刘一博 / 哈基玩 / 玩大哥” -> `{"name": "玩机器", "type": "streamer"}`

---

## 3. 话题领域 (Topics, 39类) 边界与四层隔离规则

### 3.1 主播专属 Topics (Streamer Topics, 13类)
- 所有 `streamer_*` 系列话题（共 13 类）**仅当弹幕明确谈论玩机器主播本人时方可使用**。
- `targets` 中必须同时包含 `streamer`。
- **严禁将职业选手、解说或其他圈外人物的外貌、身材、恋爱、游戏失误错套进 `streamer_*`**。

### 3.2 职业选手生活与个人话题隔离 (Pro Scene Topics)
- `pro_personal_appearance`：专门用于职业选手/教练/圈内人物的外貌颜值、体型身材（如大壮健身、发型、面相）调侃，与 `streamer_appearance_pig_weight` 及 `external_figure_personal_life` 严格隔离。
- `pro_personal_relationships`：专门用于职业选手/教练/圈内人物的恋爱感情、女友八卦、家庭婚恋、CP戏谑，与 `streamer_romance_sexuality` 及 `external_figure_personal_life` 严格隔离。

### 3.3 圈外与跨界话题隔离 (External Topics)
- `external_sports_competition`：专门用于 F1、足球、篮球等非电竞赛事、车队/俱乐部竞技走势、比赛规则、裁判争议与赛果比分讨论（与 CS 赛事 topics 隔离）。
- `external_figure_personal_life`：专门用于圈外人物/现实名人/跨界角色的外貌身材、恋爱家庭、私生活八卦，与主播（`streamer_*`）和 CS 职业圈（`pro_personal_*`）形成四层严格正交隔离。

---

## 4. 表达修辞与形式 (Formats) 判定边界

- `plain_statement`：常规平铺直叙陈述句，无特殊夸张或格式化套路。支持单选。
- `single_word_or_char`：**仅限单字或超短词**（如 '6', '?', '急', '典', '乐', '寄', '稳' 等，字符长度通常 <= 3）。长句、成语化用严禁滥用此标签。
- `fake_official_news`：**仅限具有仿官方通报、假公告语态的文本**（如 “官方通报：...”, “XX俱乐部重磅宣布...”, “特此通知...”）。普通夸奖或调侃不得滥用。
- `parallelism_listing`：**仅限具有明显排比、盘点、榜单列举结构的文本**（如 “TOP1: ... TOP2: ... TOP3: ...” 或多行对仗阵容盘点）。
- `formulaic_template`：经典定式句型（如 “以前...现在...”, “玩大哥你还记得...”, “我可以接受...但我不能接受...”）。

---

## 5. 实体 (Entities) 与主体 (Targets) 联动一致性规则

标注系统实施严格的双向逻辑一致性约束：
1. 若 `entities` 中包含 `type: "streamer"`，`targets` 必须包含 `streamer`。
2. 若 `entities` 中包含 `type: "player"`，`targets` 必须包含 `pro_player`。
3. 若 `entities` 中包含 `type: "team"`，`targets` 必须包含 `pro_team`。
4. 若 `entities` 中包含 `type: "coach"`，`targets` 必须包含 `pro_player` 或 `caster_host`。
5. 若 `entities` 中包含 `type: "caster"`，`targets` 必须包含 `caster_host`。
6. 若 `entities` 中包含 `type: "acg_character"`，`targets` 必须包含 `external_figure`。

---

## 6. 态度立场 (Stances) 与主体 (Targets) 强一致性铁律

为了防止情感立场与指涉主体错位，Validator 工具执行以下强一致性阻断：
1. `flame_streamer` -> `targets` 必须包含 `streamer`
2. `flame_player` -> `targets` 必须包含 `pro_player`
3. `flame_team` -> `targets` 必须包含 `pro_team`
4. `flame_audience` -> `targets` 必须包含 `chat_audience`
5. `flame_caster_host` -> `targets` 必须包含 `caster_host`
6. `flame_external_figure` -> `targets` 必须包含 `external_figure`

> [!IMPORTANT]
> **严禁为了迎合校验而机械硬加 target！**
> 强一致性规则旨在暴露分类定义与实际语义的不一致。当触发校验错误时，标注员**必须首先依据弹幕字面真实指涉与立场修正 stance 或 target**，绝不可通过盲目添加无关 target 的方式绕过检查。

---

## 7. 安全强度与毒性控制 (Safety) 规范

- 严重度层级：`safe` -> `borderline_playful` -> `sensitive_flame` -> `toxic_vulgar`。
- **Profanity 铁律**：凡包含脏话、粗口（如 profanity flag）的弹幕，**严禁标注为 safe**，严重度至少为 `borderline_playful` 或 `sensitive_flame`。
- **None 互斥铁律**：`flags: ["none"]` 仅在无任何违规风险时单选使用，严禁与 `profanity`、`personal_attack` 等风险标签混用。
