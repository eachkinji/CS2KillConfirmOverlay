# Shared/：Widget 与桌面兼容显示共用的源码

`Widget/`（UWP Game Bar）和 `CompatibilityHost/`（桌面叠加层）过去各自维护一份渲染、素材、弹幕和事件代码（`CompatibilityHost/Renderer/` 是从 Widget 复制出来的拷贝）。现在两端共用的代码只保留一份，放在本目录，由两个项目各自编译：

| 项目 | 引用方式 |
| --- | --- |
| `Widget/KillConfirmGameBar.csproj` | 逐个 `<Compile Include="..\Shared\…" Link="原路径" />`（UWP 项目需要显式列出编译项） |
| `CompatibilityHost/CompatibilityHost.csproj` | `<Compile Include="../Shared/**/*.cs" />` |

目录结构与 `Widget/` 一致（`Controls/`、`Services/`、`Danmaku/`、`Helpers/`），另有 `Events/` 存放事件契约 v2 的客户端部分。

## 命名空间与平台差异

- Widget 定义了 `WINDOWS_UWP`，CompatibilityHost 没有定义。
- 每个文件用 `#if WINDOWS_UWP` 选择 `KillConfirmGameBar.*` 或 `KillConfirmCompatibility.*` 命名空间，因此两端原有的调用方代码不需要修改。
- 两端确实不同的代码行，按端放在 `#if WINDOWS_UWP … #else … #endif` 中，每端编译的代码与抽取前完全一致。后续确认某段差异只是一侧遗漏的修复时，再合并成同一份代码。
- 只能使用 C# 7.3 语法（Widget 是 UWP 项目）。

## 新增代码放哪里

| 情况 | 位置 |
| --- | --- |
| 两端都需要 | `Shared/`，两端编译器都要能通过 |
| 只属于 UWP / 只属于桌面 | `Widget/` / `CompatibilityHost/`（桌面端适配优先使用 `*.Desktop.cs` 分部文件） |
| 不依赖 UI 的通用契约（素材包格式等） | `Common/`（三端共享，包括 ControlPanel） |

## 抽取工具

`Tools/SharedSource/extract_shared.py`：

- `inventory`：输出 `Output/SharedSource/inventory.json`，列出各文件的分类、差异行数和处理状态。
- `apply --max-diff-ratio R`：抽取 `namespace_only` 文件，以及差异比例不超过 R 的 `drifted` 文件。

以下情况会跳过，文件继续留在各自的项目中：差异段落在原有 `#if` 块内、XAML 代码隐藏文件、只存在于一端、差异比例过大。抽取前，两端工作副本会备份到 `Output/SharedSource/backup-*`。

`CompatibilityHost/Renderer/` 中剩余的文件是桌面端专属代码或待人工合并的文件，清单见最新的 `inventory.json`。
