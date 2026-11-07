# 控制面板 UI 统一：接手与验证记录

日期：2026-11-07。对应规范：[控制面板 UI 规范](control-panel-ui-standard.md)。

## 接手状态

读取了 Claude 最新会话 `d759e18e-4f00-4ea5-9194-8570d865aee1` 及其三个子代理的本地记录。
21:58 的 UI 任务被拆成共享样式、设置/游戏面板、首页/素材库三项调查；约 22:11 中断，未留下最终审计报告或该轮 UI 统一实现。
此前已完成的双 60px 固定图标侧栏属于已有工作，本轮保留并重新验证。

接手时仓库已有大量未提交修改。因此另存 694 个相关源文件作为本轮基线，位置为
`Output/UIStandardization/baseline-20261107`；不以整个 Git 工作区差异冒充本轮修改。
本轮差异与文件散列保存在 `Output/UIStandardization/source-changes.patch` 和 `change-manifest.json`。

## 已实施内容

- 新增 `PanelDesign.xaml` 作为基础资源入口，统一字号、间距、圆角、控件高度、文字颜色、状态颜色与按钮样式。
- 设置工作区、首页和原 Widget 的桌面样式名称通过共享样式复用；独立设置控件、18 个游戏面板及桌面适配副本同步迁移。
- 合并重复样式，移除运行时重复指定的字号、圆角和高度；保留各游戏强调色及原有事件绑定。
- 普通可读文字最低 12px，普通控件最小 36px，素材卡操作 32px；固定素材卡、游戏预览及 overlay 使用明确例外。
- 首页窄屏状态工具与播放动作自动分行，640px 下仍可完整使用字段和按钮。
- 素材创建弹窗采用自动操作列宽、说明换行；CF 批量工具和视频帧率预设分行。长表单支持整体纵向滚动，创建/取消按钮保持固定。
- 增加静态规范检查与中英文隔离 UI 回归入口；修正旧测试中硬编码中文的断言，以及原生禁用状态切换后主题色检查过早的问题。

## 验证结果

| 检查 | 结果及证据 |
| --- | --- |
| 静态规范 | 通过：91 个面板 XAML，连同资源字典共 92 个文件，0 项问题。检查资源引用、显式可读字号与可推导的高度冲突。 |
| 桌面 WinUI x64 Debug | 通过；[构建日志](../Output/UIStandardization/controlpanel-build.log)。 |
| UWP x64 Debug | 通过，关闭签名；[构建日志](../Output/UIStandardization/widget-build.log)。 |
| 中文完整 UI | 通过；[结果](../Output/UIStandardization/validation-20261107-224344-633-zh-CN/Full/ui-pass.txt)。覆盖两种首页模式、四个高级设置页签、快速游戏切换、播放反馈与 640px 布局。 |
| 英文完整 UI | 通过；[结果](../Output/UIStandardization/validation-20261107-223805-821-en-US/Full/ui-pass.txt)，[640px 截图](../Output/UIStandardization/validation-20261107-223805-821-en-US/Full/ui-home-compact.png)。 |
| 导航和主题 | 中英文均通过；18 个独立设置页、34 个原生开关和 103 个滑块的主题色正确，切换编辑对象不改变播放选择；[结果](../Output/UIStandardization/validation-20261107-224344-633-zh-CN/Full/navigation-pass.txt)。 |
| 高级设置与音频通道 | 中英文均通过。中文记录在 `validation-20261107-222641-045-zh-CN`；英文 [Settings](../Output/UIStandardization/validation-20261107-224239-597-en-US/Settings/ui-pass.txt)、[AudioChannels](../Output/UIStandardization/validation-20261107-224239-597-en-US/AudioChannels/ui-pass.txt)。 |
| 素材库滚动 | 通过；VALORANT 图标/音效各 196 包，卡拉彼丘图标 162 包、语音 50 包，各 200 次快速滚动；两种 VALORANT 下拉框各 100 次反向滚动。[结果](../Output/PackScroll42-b5871841b41548dc9c9ed3142b9acb08/ui-pass.txt)。 |
| 来源选择器 | 通过；真实按钮确认/取消/确认、外部来源同步、素材样式保持不变。[结果](../Output/NavigationRedesign/source-ui-20261107-224602/source-ui-pass.txt)。 |

实际窗口检查覆盖英文游戏设置、素材库、图标创建和 CF 语音创建；截图检查覆盖中文高级设置、首页两种模式、窄屏英文及滚动后的素材卡片。
CF 语音弹窗的最后一个槽位曾被裁切；添加整个表单的纵向滚动后，重新构建两端，并在 1170×762 的实际英文窗口复验：滚动到末尾时，`Grenade-kill voice` 的选择、重置和叠加开关完整可见，创建/取消按钮仍固定可见。测试以取消退出，没有创建素材包。
完整自动回归运行于该滚动修正之前；最后修正的验证范围为双端重新构建、静态检查和上述实际窗口检查。

## 重跑方式

从仓库根目录，先构建桌面工程，再运行：

```powershell
python .\Tests\Regression\Test-PanelDesign.py
pwsh -NoProfile -File .\Tests\Regression\Test-PanelUi.ps1 -Language zh-CN
pwsh -NoProfile -File .\Tests\Regression\Test-PanelUi.ps1 -Language en-US
pwsh -NoProfile -File .\Tests\GameSources\Test-SourceSelector.ps1
```

素材库滚动测试使用已有开发测试素材及其独立副本，未从用户安装库提取或重新打包：

```powershell
& .\Tests\Regression\Test-PackScrolling.ps1 `
  -Payload .\ControlPanel\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64 `
  -LibraryRoot .\Output\ValidationFixtures42\PackScroll42-6d2b24db646f409a86dc56f808b274d1 `
  -SourceProfile .\Output\PackScroll42-6d2b24db646f409a86dc56f808b274d1
```

## 验证边界与交付状态

- UI 验证使用独立 `KILLCONFIRM_DATA_ROOT`。滚动测试的来源目录文件元信息检查为未改变；[记录](../Output/PackScroll42-b5871841b41548dc9c9ed3142b9acb08/library-readonly-result.json)。
- 本轮没有安装或覆盖用户正在使用的版本；源码和 Debug 构建留在工作区，没有创建 Git 提交。
- 构建仍有仓库原有警告，不宣称零警告。UWP 通过编译和资源打包，本轮未重新安装并实测 Game Bar 宿主。
- 隔离 UI 测试不代表真实游戏音频后端、真实素材全量导入或全部显示缩放组合的端到端验证。音频回归中需要外部真实素材根目录的附加检查未运行。
- `RenderTargetBitmap` 截图可能带透明背景或显示页面外装饰光晕；已结合实际窗口确认，不能将截图外延当作真实窗口溢出。
- Output 为本地验证证据，不随 Git 提交；长期维护入口是本规范、源码和测试脚本。

## 后续调整：UWP 面板底部

按后续要求，将 CS2/CSGO/LOL 模式选择器移到收起按钮上方并居中。展开/收起按钮共用浅色胶囊样式，最小宽高从固定 72×26 改为自适应的 60×22，文字 10、圆角 11，使用共享原生按钮状态。

UWP 常规构建与 `WidgetSwitchValidation=true` 的隔离构建均通过；XAML 结构检查确认上下顺序、原有命名控件与点击事件保留。现有验证器已同步新规格，本轮未安装或运行测试包，也未在真实 Game Bar 内复验。仅执行 `Compile` 的初次验证命令跳过了 XAML 生成，随后改用完整 `Build` 成功。

该调整的独立基线、差异及构建日志保存在 `Output/UIStandardization/uwp-footer-20261107`。
