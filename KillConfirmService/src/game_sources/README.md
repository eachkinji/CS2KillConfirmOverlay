# 游戏来源与 LOL 击杀反馈

游戏来源只有 `cs2`、`csgo_legacy`、`lol`；来源选择不修改素材风格、图标包、显示模式。

## 文件边界

| 职责 | 目录 |
| --- | --- |
| LOL 本地 HTTPS 采集、中文身份匹配、事件基线和测试 | `KillConfirmService/src/game_sources/lol/` |
| CS 来源切换时的状态重置 | `KillConfirmService/src/game_sources/counter_strike/` |
| 已有 CS GSI 解析和 Legacy 适配，保持独立 | `KillConfirmService/src/gsi/`、`KillConfirmService/src/csgo_legacy/` |
| 两端 CS 版本设置（从旧的通用 Games 目录迁入） | `Widget/Services/GameSources/CounterStrike/`、`CompatibilityHost/Renderer/Services/GameSources/CounterStrike/` |
| 统一来源服务客户端 / 选择控件 | `Widget/Services/GameSources/`、`Widget/Controls/GameSources/` |
| CS 图标 / LOL 图标（源文件与运行资源均分目录） | `SourceAssets/GameSources/{CounterStrike,LeagueOfLegends}/`、`Widget/Assets/GameSources/{CounterStrike,LeagueOfLegends}/` |
| LOL 隔离联调脚本 | `Tests/GameSources/LeagueOfLegends/` |
| 兼容显示的 CS / LOL 对局窗口识别 | `CompatibilityHost/Windows/GameSources/{CounterStrike,LeagueOfLegends}/` |

不要把 LOL 数据转换成虚假的 CS GSI payload，也不要把 LOL 加进 `GsiGameVersion`。LOL 不调用 CS 安装路径、CFG 或回合规则。素材风格中的 CF、Valorant 等仍然是表现层。

## 运行行为

- 桌面主页状态区和 UWP 收起按钮上方使用同一个圆形选择控件。点击另一来源先确认；当前来源直接忽略。高级设置中的旧来源入口已移除。
- 服务的 `/game-source/settings` 是权威状态，保存到数据目录下 `game-sources/selection.json`。旧 CS 偏好仅迁移一次；两端启动和刷新不能覆盖服务的已保存选择。
- 设置读取与数据状态响应均设置 `Cache-Control: no-store`，避免 Windows HTTP 缓存把界面退回旧来源。
- 选择变化等待当前来源处理结束，清空旧事件队列并推进 generation。事件携带 generation，两套消费者和 UWP 的异步动画预加载完成点都会拦截旧事件。
- LOL 固定访问 `https://127.0.0.1:2999/liveclientdata/allgamedata`。读取一个一致快照，取得当前玩家、玩家列表、事件与对局时间。正常间隔 350 ms，失败退避 2 s，超时 2 s，响应大小上限 4 MiB。
- 独立 HTTP 客户端仅允许上述固定本机地址，不走代理、不跟随重定向、不携带账号凭据。按 Riot 本地自签名接口的文档兼容证书，不改变系统 TLS 设置。
- 优先使用完整 Riot ID；国服旧 `summonerName` 也可匹配。短名称若能匹配多个玩家，则不触发。缺少可唯一识别的本机玩家（如观战）时不触发。
- 只以自己的新增 `ChampionKill` 触发一次 `Combat` 反馈；将对应 `Multikill.KillStreak` 的 2/3/4/5 档位写入同一个事件的 `kill_count`，用于当前素材包的双杀、三杀、四杀和五杀动画、音效与角色语音。没有有效连杀标记时按普通击杀（1）处理。
- `Multikill` 是当前击杀的补充信息，不是额外一次击杀。使用唯一玩家身份、事件时间和编号配对，并对事件编号及连杀标记去重；已经发出的击杀不会因迟到标记再播放一次。
- 未完成配对的击杀使用本机单调时钟等待 600ms，覆盖跨轮询到达的标记；优先按 1ms 时间容差配对，唯一候选才允许 100ms 的跨通知时间差。该容差是实现上的保守兼容策略，官方文档没有承诺事件到达顺序或延迟。超时或配对不明确时降级普通击杀，不猜测连杀档位。
- LOL 档位来自游戏本身，不将整局击杀总数当连杀，也不借用 CS 的回合、死亡或限时累计规则。队友击杀、助攻、野怪、防御塔不触发本机击杀反馈。
- 音频复用现有音效/角色语音两条独立通道，遵循所选包、关闭通道、音量、打断和素材槽位回退设置；不会自动切换到 LOL 专属素材。
- 发布动画和音频入队前复核来源 generation，入队期间保护来源状态，防止切到 CS 后旧 LOL 音频任务重新启动。
- 初次接入、断线恢复、来源切换、对局时间回退、事件编号重置或玩家变化都会重新建立基线，不补播历史击杀。
- 读取失败前已经确认、尚在短暂合并缓冲中的新增击杀，在来源仍一致时完成一次反馈，再重建基线；断线期间积累的事件不补播。
- LOL 下隐藏 CFG 操作，显示 LOL 数据状态；第一版不因本地接口断线或回大厅自动关闭小组件，以免把连接故障误判为游戏退出。
- 兼容显示的自动跟随和切出隐藏按当前来源识别进程。LOL 只跟随 `League of Legends.exe` 对局窗口，排除大厅、登录器和 WeGame；切换来源时立即作废旧窗口缓存。

## 验证与国服实测

单元测试：`cargo test --manifest-path KillConfirmService/Cargo.toml --bin cskillconfirm`。

构建服务后，用 PowerShell 7 执行隔离联调（省略 `-Service` 时使用 Debug 服务；`-Ui` 另需构建 Debug 桌面控制面板）：

```powershell
./Tests/GameSources/LeagueOfLegends/Test-Lol.ps1 -Service ./KillConfirmService/target/release/cskillconfirm.exe
./Tests/GameSources/Test-WindowSources.ps1
```

脚本占用本机 2999 端口作为 HTTPS 假对局，若端口被真实 LOL 占用会直接失败，不会关闭游戏。它启动独立端口、独立数据目录的真实服务；可选 UI 检查也使用独立配置。输出位于 `Output/LoLIntegration/`，不修改已安装用户配置。

联调保存 `journal-events.json`（实际服务发布的 1～5 档事件）和 `audio-evidence.json`。音频证据使用两个独立的有效静音 WAV 测试包、零音量，验证档位路由、解码和入队；不代表扬声器可听输出。兼容显示的 `--validate-lol-events <输出目录> <Widget素材根目录> <journal-events.json>` 可将这些事件交给现有客户端解析器和 Presenter，验证 CF 默认 `multi1..5` 资源及非空渲染帧。开发环境将 `KILLCONFIRM_CF_TEST_PACKS` 指向 `SourceAssets/DefaultPacks/crossfire`；正式载荷自带 `DefaultPacks/crossfire`。

国服验收：切 LOL → 进入人机/自定义对局 → 自己击杀敌方英雄应播放当前素材反馈一次；双杀到五杀使用对应档位。队友击杀、自己的助攻和击杀小兵不播放。中途切入不补播，切回 CS2/CSGO 后确认原 GSI 正常。模拟中文响应通过不能替代真实国服接口和游戏内显示验证。

接口依据：[Riot Game Client API](https://developer.riotgames.com/docs/lol#game-client-api)、[官方事件样例](https://static.developer.riotgames.com/docs/lol/liveclientdata_events.json)。
