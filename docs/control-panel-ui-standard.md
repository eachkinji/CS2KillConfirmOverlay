# 控制面板 UI 规范

版本：1.0 · 2026-11-07

适用于桌面控制面板，以及其复用的设置控件。保留现有浅色玻璃背景、游戏主题色和图标导航。规范对应实际 XAML 资源，不另建一套仅供参考的视觉值。

## 资源与源码入口

- 唯一基础资源：`Widget/Controls/Settings/PanelDesign.xaml`。
- `SettingsWorkspace.Styles.xaml`、`CompatibilityHome.Styles.xaml` 与 `KillConfirmWidgetPage.Styles.xaml` 中的桌面样式只保留兼容名称，通过 `BasedOn` 引用基础资源。
- 页面源文件在 `Widget/Pages/Main`、`Widget/Controls`、`CompatibilityHost/Integration`；`ControlPanel/Generated` 由构建脚本生成，禁止直接编辑。
- 独立 `UserControl` 必须直接合并 `ms-appx:///Controls/Settings/PanelDesign.xaml`，或通过共享兼容字典间接合并，避免依赖某个页面恰好提供同名资源。代码构建的对话框与卡片从同一资源字典读取样式和数值。
- `Widget` 与 `CompatibilityHost/Integration/Controls` 两套命名空间的相同控件必须同步维护。

## 字体层级

默认字体 `PanelFontFamily`：Segoe UI Variable Text；中文使用系统字体回退。图标字体、代码字体与游戏预览字体独立。

| 用途 | 样式 | 字号/字重 |
| --- | --- | --- |
| 页面标题 | `PanelPageTitleTextStyle` | 24 / SemiBold |
| 分区标题 | `PanelSectionTitleTextStyle` | 18 / SemiBold |
| 设置名称、卡片标题 | `PanelTitleTextStyle` | 14 / SemiBold |
| 正文、控件文字 | `PanelBodyTextStyle` | 13 / Normal |
| 辅助说明、元信息 | `PanelCaptionTextStyle` | 12 / Normal，行高 18，可换行 |

普通可读文字不得小于 12。不要通过缩小字号让长说明塞进固定高度；优先换行、自动行高和合理列宽。素材名称可以省略，但应保留可访问名称或工具提示。`FontIcon`、装饰图形及真正游戏内 overlay 的紧凑显示不按正文规则处理。

## 间距与形状

| 用途 | 规格 |
| --- | --- |
| 基础间距 | 4 / 8 / 12 / 16 / 24，使用 `PanelSpace*` |
| 页面卡片 | `PanelCardStyle`，圆角 20，内边距 24,20 |
| 卡片内分组、设置行卡片 | `PanelSectionCardStyle`，圆角 12，内边距 16,12 |
| 按钮、文本框、下拉框 | 圆角 10 |
| 普通控件 | 最小高度 36 |
| 紧凑素材卡操作 | 最小高度 32，文字 12，内边距 10,5 |
| 工具图标按钮 | 36×36，图标视觉尺寸与点击区域分开 |

用 `MinHeight` 允许内容与文字缩放，只有图标按钮、预览画布等尺寸明确的元素才固定 `Height`。一层页面玻璃即可，内层分组使用平面半透明背景，避免多层 Acrylic 叠加。

## 颜色与主题

| 角色 | 资源 | 默认值 |
| --- | --- | --- |
| 主文字 | `PanelTextPrimaryBrush` | #1C1C1E |
| 次文字 | `PanelTextSecondaryBrush` | #4B4B50 |
| 辅助文字 | `PanelTextMutedBrush` | #63636B |
| 默认强调色 | `PanelAccentBrush` | #007AFF |
| 成功 | `PanelSuccessBrush` | #16803C |
| 警告 | `PanelWarningBrush` | #9A5B00 |
| 危险 | `PanelDangerBrush` | #C42B1C |

状态必须同时有图形或文字，不仅靠颜色表达。中性文字在所有游戏页保持一致；`GameThemePalette` 的 `MutedText` / `SubtleText` 与辅助文字保持同步。各游戏强调色仍由 `GameThemePalette` 与 `ControlTheme.Apply` 控制，不把所有游戏改成默认蓝色。

`ApplyTheme` 只处理主题颜色与语义状态，不再重写圆角、字号和控件高度。新增中性颜色时先检查共享资源；旧名称应作为共享值的兼容别名存在。

## 控件与交互

- 普通动作使用 `PanelButtonStyle`；主要动作使用 `PanelPrimaryButtonStyle`；删除、清空等动作使用 `PanelDangerButtonStyle`。32px 操作使用相应 Compact 样式。
- 单纯更改颜色、内边距或圆角时，不复制整个 `ControlTemplate`。保留原生指针、按下、禁用与键盘焦点状态。主按钮的共享模板统一维护状态反馈。
- 文本框和下拉框使用 `PanelTextBoxStyle` / `PanelComboBoxStyle`；按钮与字段同排时按中线对齐。
- 图标按钮必须有 `AutomationProperties.Name` 或运行时等价设置，并提供工具提示。不要用只有颜色的状态点代替状态名称。
- 设置项使用“名称 + 说明 + 控件”的结构，长说明独立换行；恢复默认按钮与标题右对齐，沿用游戏强调色与共享主按钮状态模板。
- 切换设置页只改变编辑对象，不改变正在播放的游戏样式。

## 宽度与长文本

- 首页状态条在自身可用宽度小于 900 时，将工具按钮和游戏选择器移到第二行；播放控制区小于 840 时，将动作按钮移到字段下方。
- 640px 首页仍保留可读的音量、渲染质量和测试事件字段。不要通过缩小字号或压缩字段宽度强行保留单行布局。
- 创建素材包对话框的导入、重置等文字按钮使用自动列宽；CF 批量工具和视频帧率预设分行，封面说明允许换行。固定尺寸只用于封面预览、图标等明确对象。
- 长创建表单在共享弹窗入口提供整体纵向滚动，主操作按钮留在固定底部；不能只依赖内部列表滚动来处理整个表单超高。

## 保留的特定规格

- 两个 60px 图标侧边栏、其固定不展开行为和上下留白属于导航规范，沿用上一轮验证结果。
- 素材库卡片固定 270×108，以保证虚拟化与封面排布；卡片内操作使用 32px 紧凑规格。
- 游戏内 overlay 的 22px 工具区、26px 紧凑选择器，以及游戏画面预览和动画渲染尺寸是独立用途，不通过全局替换改变。
- UWP 面板底部将游戏模式选择器居中放在收起按钮上方；展开/收起按钮采用浅色胶囊，最小 60×22、圆角 11、文字 10，按内容自适应宽高。这是紧凑 overlay 控件的例外规格。
- 品牌标志、游戏封面、图标字形、预览画布可保留必要专用尺寸。新例外须说明用途，不能作为新一套普通按钮/表单规格。

## 修改后的检查

1. 运行 `python Tests/Regression/Test-PanelDesign.py`，严格检查 XAML 资源引用、显式可读字号及可推导的高度冲突。该检查不覆盖全部动态 C# 布局，也不替代视觉检查。
2. 桌面 WinUI 与 UWP 都构建成功，确认共享资源在两条路径均可加载。
3. 运行导航与游戏来源回归，验证两个固定侧栏、18 个游戏设置页、主题色与播放选择相互独立。
4. 检查首页两种显示模式、高级设置四个页签、素材选择和创建对话框；检查中英文长文本、滚动区域、按钮点击区域与下拉框。
5. 对照本次独立基线核对命名控件与事件，保留业务行为。UI 检查使用 `KILLCONFIRM_DATA_ROOT` 指向独立目录。
6. 更新验证记录，截图和构建日志保存在 `Output/UIStandardization`；不要把临时验证配置交付为用户配置。

本次接手说明与验证结果见同目录的 `control-panel-ui-validation.md`。
