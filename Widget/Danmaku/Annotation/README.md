# 6657 弹幕全量细粒度标注基础设施 (Danmaku Annotation Infrastructure)

本目录为 6657 弹幕全量细粒度多维语义标注体系（Taxonomy v1）的规范定义、受控词表、批次切分清单、待审校准样本及校验工具链。

## 核心原则与架构铁律

1. **源数据不可变性 (Source Immutability)**
   - `Widget/Danmaku/6657_memes.json` 包含 23521 条原始弹幕，SHA256 严格锁定为 `9bd3ed7ae963714a34d481bde45df597e4d4db49ee23c39d67506f11b4e32183`。
   - 严禁修改、追加、重排源文件。
2. **轻量索引引用 (1-Based Index Only)**
   - 所有标注数据必须仅通过 `1-based index` 关联源数据（1..23521）。
   - **严禁在标注文件中复制或篡改原始弹幕文本**（防止数据冗余与文本失真）。
3. **多维多标签语义解耦 (Semantic vs Event Decoupling)**
   - 标注维度专注于语言学、13 类态度立场（含 flame_caster_host 与 flame_external_figure）、39 类细粒度语义话题（含 pro_personal_* 与 external_*）、17 类修辞形式、9 类专名实体（含 coach 独立分类）、文化圈层与安全控制，核心语义枚举严格控制为 100 项。
   - `context` 维度仅表达文本理解对外部语境的依赖程度，绝非游戏事件分类；后续 CS2 事件联动系统另做独立多维查询。
4. **批次无缝 100% 覆盖 (Seamless 100% Coverage)**
   - 全量 23521 条按每批 500 条（末批 21 条）切分为 48 个批次，定义于 `manifest.json`，每批内部必须完整连续无缝覆盖。
5. **审核反伪造 (Review Anti-Spoofing)**
   - 未审样本 `review.status` 必须为 `pending`，且严禁填写 `reviewer`。

---

## 目录结构

```
Widget/Danmaku/Annotation/
├── taxonomy.v1.json                        # 受控词表规范定义 (100 core enums: 39 topics, 13 stances, 17 formats, 8 targets...)
├── schema.v1.json                          # JSON Schema 严格验证定义 (Draft 2020-12)
├── entity_aliases.v1.json                  # 受控专名实体规范名与高频别名映射表
├── annotation_guidelines.v1.md             # 后续批次标注与质量规范操作指南
├── manifest.json                           # 48 批次切分元数据与进度追踪清单 (1..23521)
├── validate_annotations.py                 # 严格校验与全量合并 CLI 工具
├── batches/                                # 批次标注存放目录 (batch_001.json .. batch_048.json)
│   └── .gitkeep
└── calibration_samples/                    # 待审校准样本集
    └── calibration_sample_batch.json       # 20 条代表性样本 (status=pending, 待宿主复审)
```

---

## 校验与工具使用说明

### 1. 基础设施自检 (验证源文件哈希、词表、Schema、Manifest 与待审校准样本)
```bash
python Widget/Danmaku/Annotation/validate_annotations.py --verify-infra
```

### 2. 校验指定批次文件
```bash
python Widget/Danmaku/Annotation/validate_annotations.py --validate-file Widget/Danmaku/Annotation/batches/batch_001.json
```

### 3. 查看全量进度与覆盖率 (不完整时默认以非零退出码退出)
```bash
python Widget/Danmaku/Annotation/validate_annotations.py --check-coverage
# 若允许在未完成状态下以 0 退出（如监控巡检）：
python Widget/Danmaku/Annotation/validate_annotations.py --check-coverage --allow-incomplete
```

### 4. 全量完成时导出合并文件
```bash
python Widget/Danmaku/Annotation/validate_annotations.py --check-coverage --merge-output Widget/Danmaku/Annotation/6657_annotations_v1.json
```

---

## 本地 Web GUI 标注审阅系统 (Danmaku Annotation GUI)

为了方便快速审阅与修改 23,521 条标注数据，提供了零第三方依赖的本地 HTML GUI 服务。

### 1. 启动 GUI

**使用 PowerShell 脚本一键启动：**
```powershell
.\Start-DanmakuAnnotationGui.ps1
```

**或者使用 Python 启动：**
```bash
python Widget/Danmaku/Annotation/start_gui.py
```
启动后会自动在系统默认浏览器中打开 `http://127.0.0.1:8765`。

### 2. 界面与核心功能特性

1. **中文界面与只读原文**：
   - 顶部提供全量进度与审定状态概览。
   - 弹幕原文从 `6657_memes.json` 按 index 实时映射加载，界面醒目呈现且**严格只读**，杜绝误改。
2. **多维筛选与快捷预设**：
   - 全局搜索：支持弹幕原文、序号、实体名、备注、标签组合实时检索。
   - 快捷预设按钮：一键定位「🔥 喷主播 (`flame_streamer`)」、「⚠️ 敏感/毒性」、「⏳ 仅待审 (`pending`)」、「🏷️ 含有实体」、「📉 低置信度」等。
   - 多维联动过滤：支持按 48 批次、审核状态、Target、Stance、Topic、Safety 等级精确过滤。
3. **细粒度多标签与实体编辑**：
   - 受控枚举（Targets、Stances、Topics、Formats、Culture）采用可搜索 Tag/Chip 选择器，展示中文名称与英文原始枚举值。
   - 专名实体支持从受控规范表快捷选填，亦可动态增删改实体名称与类型（streamer, player, team, coach, caster, acg_character, game_asset, org, other）。
   - 安全控制（Safety）：Severity 与 Flags 联动防呆，选填风险标签时自动关联非 safe 等级。
   - 置信度（Confidence）与审核状态（Review）：待审状态下自动清空并禁用审核人（防伪造），已复核/已批准状态要求明确填写审核人。
4. **高频快捷键操作**：
   - `Ctrl + S`：保存当前条目修改。
   - `Ctrl + Enter`：**保存当前并自动跳转下一条**（适合高频连续复审）。
   - `Alt + ←` / `Alt + →`：上一条 / 下一条快速切换。
   - `Esc`：放弃当前未保存的修改并重置。
   - `Ctrl + F`：快速聚焦搜索框。
5. **安全持久化机制**：
   - 写入前自动在本地运行全套 Schema/Taxonomy 校验。
   - 写入前自动在 `Widget/Danmaku/Annotation/.backups/` 生成时间戳备份。
   - 采用**临时文件 + 原子替换（Atomic Replace）**写入目标批次文件（`batch_xxx.json`）与全量合并文件（`6657_annotations_v1.json`），失败不破坏原文件。

