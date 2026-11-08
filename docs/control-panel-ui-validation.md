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

## 2026-11-08：数据源隔离、下拉列表与动画预载

本节记录本轮截图反馈及后续清晰度、首次播放问题的修改。证据集中在 [UIFixes-20261108](../Output/UIFixes-20261108/)、[独立 UWP 预载验证](../Output/WidgetSwitch-20261108-205037/switch-pass.txt) 和 [CalabiyauHD20261108](../Output/CalabiyauHD20261108/)。这是一轮源码与隔离验证记录，尚不表示已经更新用户安装版本。

### 源码变化

- 状态灯与标签共用居中对齐；测试事件下拉列表改为实际可见内容高度，修复 LOL 列表大片空白及 CS 列表末项不可达的问题。
- LOL 本地服务页只显示其对局数据源与轮询间隔，移除无用的安装目录入口；10087、备用端口及 CS 配置入口仅显示在 CS 数据源下。轮询选择器在窄页面分行，保留完整标签及自定义输入。
- 战斗与视效标题统一为游戏来源 LOGO、来源名称及“战斗与视效”，删除截图指出的重复说明。各素材风格的战斗规则与显示设置按 CS2、CS:GO、LOL 分开保存；恢复默认不改变其他来源。
- 三个来源都移除“退出游戏后自动关闭”的设置和运行逻辑。当前 LOL 采集集成不接收观战身份数据，因此隐藏 LOL 的观战开关；此限制不等同于 LOL 游戏没有观战功能。体验增强移除顶部说明，顺序统一为击杀音效打断、角色语音打断、图标与音效联动。
- 音效打断、观战、弹幕开关与连杀增益保留已有来源键；只有 CS2 可以读取旧全局键，修改不再回写全局键。桌面服务同步也通过来源 Store 读取观战值。素材与音频包选择未因此改为来源隔离。
- 实际来源 API 验证发现 WinRT HTTP 缓存会在成功写入 CSGO 设置后，仍对普通 GET 返回上一轮 LOL 响应；同端点唯一查询参数立即返回正确值，[诊断记录](../Output/UIFixes-20261108/source-api-diagnostic.log)保留了期望、实际及新 URL 对照。本地认证客户端现使用 `MostRecent` 读取和 `NoCache` 写入策略，固定 URL 的完整断言重跑通过。
- CF 缺少 `headshot_vvip` 等图时，UWP 原先会下载整个原版目录；现在缓存目录列表并只取命中的文件，重复使用的图只解码一次。精英“小翅膀”只用于 `multi1`～`multi6`，样式 2 不再对其他事件例外放行。详见 [CF 图标包](crossfire-icon-packs.md#2026-11-08uwp-按文件预载与精英层限制)。
- 三国杀两端在选择时解码并驻留当前包全部 12 档、48 张原生 atlas 页，播放不再临时读盘或解码；换包释放缓存。处理加载期间切包的过期结果及释放，UWP 按当前内存预算检查下一张原生页，内存不足时报告准备失败，不把部分加载标为完成。
- 战地 2042 的骷髅缓存改为随输出分辨率生成，切换清晰度后重建，避免先压成固定小图再放大；三角洲行动的骷髅缩放使用高质量采样。[战地 2042 原生渲染对比](../Output/UIFixes-20261108/raster-detail/pass.txt)覆盖 1280、1920、3200 三档及切回低档的缓存刷新。
- 按用户后续要求，卡拉彼丘取消 2× 高清大包和 BC3 新压缩试验，内置默认恢复原 1× 图集，外置图标交付恢复原已确认的小包；六档预载与此前其他游戏修复保留。

### 已有验证记录

下表区分已经落盘的结果与新增验证入口；后续最终重跑以目录中的最终报告为准，不把较早通过记录当作后续每项修改的验证。

| 项目 | 已记录结果与范围 |
| --- | --- |
| 常规 UWP 与隔离包构建 | 已通过；[常规构建](../Output/UIFixes-20261108/widget-build.log)、[隔离包构建](../Output/UIFixes-20261108/uwp-validation-build.log)。保留编译器及打包警告。 |
| 体验隔离与 HTTP 修复后桌面构建 | 已通过，0 错误、112 个现有警告；[构建日志](../Output/UIFixes-20261108/experience-fix-build.log)。 |
| 英文完整 UI、设置 | 已通过；[英文报告](../Output/UIFixes-20261108/english-ui.log)。覆盖游戏面板、快速切换和设置持久化。 |
| 中文设置 | 已通过；[设置报告](../Output/UIFixes-20261108/settings-ui.log)。七个可见设置行，详情展开/收起及音频行为独立保存。 |
| 来源、端口、测试事件与体验隔离 | HTTP 修复后完整重跑通过真实按钮确认/取消、外部来源同步、CS/LOL 端口隔离、测试事件末项可达，以及三来源首次进入、反向回访、旧键不回写、实际服务 API 的音效打断/观战/连杀增益值；[最终报告](../Output/NavigationRedesign/source-ui-20261108-205517/source-ui-pass.txt)、[启动日志](../Output/UIFixes-20261108/source-ui-final.log)。 |
| CF 稀疏回退与预载 | 独立 UWP：982 文件的测试原版目录中，连续 51 次缺图回退只下载 1 文件、解码 1 次；完整当前组合预载 616 ms，所有事件/兵种组合首次帧最多 34 ms，播放时文件请求、下载与解码均为零。[原始记录](../Output/WidgetSwitch-20261108-205037/switch-pass.txt)。 |
| 三国杀完整预载 | 独立 UWP：12 档 / 48 页预载 4240 ms；全部档位首次帧最多 23 ms，第二轮最多 16 ms，启动最多 4 ms；两轮均走实际默认 `native-atlas` 路线，首次播放没有文件请求、下载或解码。[原始记录](../Output/WidgetSwitch-20261108-205037/switch-pass.txt)。 |
| 预载生命周期回归 | `Test-CrossfireSparsePreload.ps1`、`Test-SanguoshaAtlasPreloading.ps1`、`Test-AnimationPreloadLifecycle.ps1`、`Test-CrossfirePreloading.ps1`、`Test-SimpleImagePreloading.ps1` 已通过。覆盖共享纹理、全部档位准备、暖播零加载、切包取消与释放，以及低内存时拒绝错误就绪。 |
| 卡拉彼丘高清包生命周期（已取消） | 旧验证记录仅作研究历史保留，不代表当前交付；当前版本恢复原小包。 |
| 卡拉彼丘交付回退 | 高清大包和新压缩方案已取消；当前版本以[正式清单](../../导入素材/素材清单.json)与 [README](../../导入素材/README.md) 为准。 |
| 卡拉彼丘高分辨率采样 | `10305201` 的 1280、1920、3200 三种输出尺寸及三个时刻通过生产 Win2D 渲染与连续源参考比较；[报告与数值](../Output/CalabiyauHD20261108/RasterQualityCurrent/pass.txt)。该项覆盖这一复杂主题的高分辨率采样，全库另按 768×768 逻辑画布做上述双时间点抽样。 |

### 内存、宿主与交付边界

三国杀全包驻留是约 1.27 GiB 的进程内存规模：最终预载检查日志在准备后为 1251 MiB，播放检查时达到 1296 MiB（约 1.27 GiB）。48 张原生页的 RGBA 像素总量约 797 MiB，没有为减小驻留而降低纹理解码分辨率。换包后检查到缓存计数为零，但该次应用内存读数仍为 1296 MiB；不能把缓存清空等同于系统内存读数立即下降，也不以即时工作集变化证明释放完成。缓存对象的去重释放与加载中取消由生命周期回归检查。测试机器报告的应用预算为 52690 MiB；低预算拒绝路径另用回归夹具验证，不能由本机充裕预算推断所有机器均能驻留全包。

该性能测试安装并运行的是独立身份的 `KillConfirmGameBar.SwitchValidation` UWP，结束后移除；**没有在真实 Game Bar 游戏进程或实际游戏对局内复验**。图形首帧时间反映该独立 UWP 窗口中的 Win2D 路线，不覆盖游戏竞争 GPU、后台 Game Bar 生命周期或所有设备预算。测试使用工作区开发素材和小型隔离夹具，没有修改用户正式素材库或覆盖正式安装包。

**用户已取消卡拉彼丘高清大包与新压缩方案。** 当前安装继续使用原小包，开发交付目录恢复原已确认的 161 款小包。高清/BC3 产物仅保留在 Output 作为已取消的历史，不用于安装或交付；不从用户安装库反向打包。

回退只针对卡拉彼丘高清重烘焙及压缩接入，保留此前界面、来源隔离、CF/三国杀预载、战地/三角洲采样、Logo 与首次语音修复。

重跑独立预载验证时，先以 `WidgetSwitchValidation=true` 构建隔离包，再运行 `Widget/Validation/New-NativePreloadFixture.ps1` 创建夹具，将返回的 `CatalogProfile`、`Payload` 与实际隔离包路径传给 `Widget/Validation/Test-GameSwitch.ps1 -NativePreloadOnly`。来源 UI 与体验隔离验证继续由 `Tests/GameSources/Test-SourceSelector.ps1` 启动。

### 同轮追加：应用 Logo 与首次语音截断

用户提供的 KCF SVG 已作为应用 Logo。原文件保留于 [品牌源目录](../SourceAssets/Branding/README.md)，PNG 与 ICO 各尺寸直接由矢量栅格化，覆盖 UWP/Game Bar、启动画面、控制面板、兼容显示、服务及启动器、安装与卸载入口。[预览](../Output/Branding-20261108/branding-preview.png)及[生成验证](../Output/Branding-20261108/validation.txt)记录透明背景、图形安全边距和可复现结果。不会替换各游戏自己的 Logo。

首次语音被截断定位到设置同步的两条路径：重复提交同一输出设备会销毁正在播放的输出流；兼容显示的旧 `/soundpack` 同步会在现代双通道配置后停止语音。服务现在对同设备选择保持原流，真实设备切换失败时保留旧流，显式重载仍可重新打开设备。兼容显示统一同步现代音效/语音通道；同包的首次旧配置迁移、重复同步、音量及打断偏好更新不停止当前句，实际换包或禁用仍停止对应通道。

[音频专项](../Output/UIFixes-20261108/audio-regression-final.log) 29 项和[播放规则回归](../Output/UIFixes-20261108/soundplayback-regression-final.log) 47 项通过；两组筛选存在交集，不作为独立用例总数相加。新增测试使用生产设备切换逻辑与真实 Rodio Sink/Mixer，逐样本验证 8 秒语音在中途刷新设置后完整结束，同时验证换包、禁用及音效/语音独立停止。测试不要求音频硬件，也不表示已在实际游戏中试听。

追加变更后的[控制面板](../Output/UIFixes-20261108/controlpanel-branding-build.log)、[兼容显示](../Output/UIFixes-20261108/compatibility-branding-audio-build.log)、[服务与启动器](../Output/UIFixes-20261108/service-branding-audio-build.log)构建通过；[真实服务下的来源 UI 回归](../Output/UIFixes-20261108/source-ui-branding-final.log)再次通过。保留原有编译警告，没有覆盖用户正式安装。

UWP 游戏风格选择器的紧凑图标缩为 18×14 DIP、容器 24×20 DIP，保留原生箭头空间。展开与收起改为调整同一视觉对象，避免原生 ComboBox 缓存展开时的大图标。[隔离 UWP 报告](../Output/WidgetSwitch-20261108-213111/switch-pass.txt)覆盖全部 18 款、弹出菜单中选择 CF→PUBG、反复展开收起及名称刷新；[截图](../Output/WidgetSwitch-20261108-213111/compact-game-selector-pubg.png)确认 PUBG 与箭头完整。[最终常规 UWP 构建](../Output/UIFixes-20261108/uwp-final-build.log)通过，隔离验证包已经移除。五个编译后的桌面及辅助程序也通过 [Windows 图标提取核对](../Output/Branding-20261108/compiled-icon-validation.txt)。

最终 [中文完整 UI](../Output/UIFixes-20261108/full-ui-after-audio.log)、[兼容显示交互](../Output/UIFixes-20261108/compatibility-interactions-modern/interaction-pass.txt)、[运行时](../Output/UIFixes-20261108/compatibility-runtime-complete/runtime.txt)及 [18 款下方图层](../Output/UIFixes-20261108/compatibility-runtime-complete/lower-layers.txt)均通过。旧回归中对 `/soundpack` 的断言已更新为现代 `/audio/channels`：五个并发调用只完成一次注册及通道同步，暖调用不重复同步，不发送旧接口或重复设备选择请求。仍保留预览实际像素、音频失败反馈、编辑位置、缺失素材处理及关闭注销的原有断言。
