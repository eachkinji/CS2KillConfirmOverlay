# 5.9.2.3 发布说明

本版统一控制面板的视觉规范，调整 UWP 底部操作区，并补充 LOL 连杀反馈。

## 界面优化

- 统一首页、设置页、游戏面板、素材卡片及创建弹窗的字号、间距、圆角、颜色和按钮样式；保留游戏主题色与固定图标侧栏。
- 改善窄窗口和中英文长文本布局；素材创建表单支持整体纵向滚动，创建、取消按钮保持可见。
- UWP 的 CS2 / CSGO / LOL 来源选择器居中放到收起按钮上方；展开、收起按钮改为更小的浅色胶囊，并按文字自适应尺寸。

## LOL 击杀与连杀

- 读取 LOL 官方 Live Client Data API，以自己的新增英雄击杀触发反馈；根据对应的 `Multikill.KillStreak` 使用双杀、三杀、四杀、五杀（2–5）档位，没有有效标记时按普通击杀处理。
- 连杀标记只补充当前击杀，不额外触发一次；重复或迟到标记不会重播已经发出的反馈。LOL 档位不使用 CS 的连杀累计规则。
- 复用当前素材包和现有音效、角色语音两条独立通道，遵循各通道的开关、音量、打断和槽位回退设置。
- 初次接入、断线恢复、切换来源或识别到新对局时重新建立基线，不补播历史击杀。断线前已观察到、仍在短暂合并缓冲中的新增击杀，会在来源未变时完成一次反馈。
- 来源切换清空旧事件和音频，阻止旧来源反馈继续进入新来源；切换 CS2 / CSGO / LOL 不改变素材风格、图标包或显示模式。

## 验证记录

| 检查 | 最终结果与证据 |
| --- | --- |
| UI 规范、中英文布局与素材库滚动 | 静态检查 91 个面板 XAML、92 个源文件/资源字典，0 问题。先前中英文隔离 UI、来源选择及素材库滚动检查通过；最后的 CF 语音弹窗滚动修正另经实际窗口复验。详细范围见 [UI 验证记录](control-panel-ui-validation.md)。 |
| 桌面、兼容显示与 UWP 最终构建 | x64 Release 均通过；正式打包未跳过 Rust。UWP 主包和英文资源包版本均为 5.9.2.3，MSIX 签名状态 Valid。[构建日志](../Output/Release-5.9.2.3-20261107-ui-lol/build.log)、[MSIX 校验](../Output/LoLIntegration/release-5.9.2.3/packaged/msix-provenance.json)。构建保留原有警告，不宣称零警告。 |
| LOL 单元测试与隔离 HTTPS 联调 | Rust 全量 169 项通过、0 失败、1 忽略（原生进程测试使用的子进程夹具）；其中 LOL 36 项通过。[日志](../Output/LoLIntegration/release-5.9.2.3/rust-tests-final.log)。Release 服务的本地自签名 HTTPS 联调通过；正式打包完成服务构建和签名后，对载荷中的服务再次联调通过，[记录](../Output/LoLIntegration/release-5.9.2.3/packaged/integration.log)、[载荷服务散列与签名](../Output/LoLIntegration/release-5.9.2.3/packaged/service-provenance.json)。 |
| 动画档位、双通道音频的验证范围 | 正式载荷中已签名的兼容显示程序读取其随附 CF 默认包，将载荷服务发布的 1～5 档事件通过现有客户端解析器、Presenter 和桌面 Win2D 渲染器，选中 `multi1..5` 五张不同主图并输出非空画面；[渲染报告](../Output/LoLIntegration/release-5.9.2.3/packaged/rendering/pass.txt)、[程序与素材散列](../Output/LoLIntegration/release-5.9.2.3/packaged/rendering/payload-provenance.json)。两个独立静音 WAV 包分别验证音效与角色语音的素材选择、解码/入队，13 次事件的两通道路由序列一致；[音频证据](../Output/LoLIntegration/smoke-profile-20261107-233236-43660/audio-evidence.json)。 |
| 来源切换与历史事件隔离 | 通过中文 Riot ID/旧名匹配、同轮及跨轮询连杀、重复/迟到通知、断线前 pending 刷新、恢复不补播、来源切换与服务重启等联调场景。600ms 到期与新杀、旧 marker 同轮到达的错配边界已修复并通过单元回归。 |
| 安装包版本、签名及 SHA-256 | `KillConfirmGameBar_Setup_5.9.2.3_有依赖-新人用.exe`，325,339,728 字节（310.27 MiB），单个 EXE、无分卷；文件及产品版本 5.9.2.3，Authenticode 状态 Valid，签名证书与 MSIX 相同。[产物校验](../Output/Release-5.9.2.3-20261107-ui-lol/artifact-verification.json)、[SHA-256](../Output/Release-5.9.2.3-20261107-ui-lol/SHA256.txt)。 |

本次验证尚不包含真实国服对局或实际 Game Bar 宿主运行。隔离测试中的事件档位与音频路由、入队结果，不等同于真实对局中的动画显示和可听输出验收。

已将实际测试过的程序和五档 CF 主图与 Inno 安装器输入逐项比较 SHA-256，10 项一致；[校验记录](../Output/LoLIntegration/release-5.9.2.3/packaged/installer-transfer-verification.json)。工作区包含接手前的未提交修改，另存 [源码工作树快照](../Output/LoLIntegration/release-5.9.2.3/source-provenance.json)，不能仅凭 Git HEAD 重建本包，也不能将快照内所有修改归为本轮。

维护依据：[UI 规范](control-panel-ui-standard.md)、[UI 验证记录](control-panel-ui-validation.md)、[游戏来源与 LOL 行为](../KillConfirmService/src/game_sources/README.md)。
