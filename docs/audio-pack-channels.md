# 图标包、音效包与语音包

自 5.0.1.37 起，界面区分三种素材：图标包负责画面；音效包负责图标配套声音；语音包负责角色台词。每个游戏分别保存选择、音量、联动设置和角色语音打断设置。

## 旧配置

旧 CF 声音选择进入语音通道，音效通道默认“无”。其他游戏的旧声音选择进入音效通道，语音通道默认“无”。现有包 ID、文件路径及音量保留；旧 catalog 的 `VoicePacks` / `VoicePackItem` 内部名称保留，新增类型元数据完成分类，不要求用户重新导入。

新的 `AudioChannels.v2.<game>` 设置只迁移一次，显式“无”也是有效配置。旧设置保留作兼容镜像。新建包分别放入素材目录下的 `<game>/sound_effect_packs` 和 `<game>/voice_packs`。

## 包格式

声音清单在现有 `audio.slots` 格式上增加顶层 `audio_kind`：`sound_effect` 或 `character_voice`（接受 `sfx`、`voice` 别名）。`game_style` 指定游戏；`display_name` 指定正常显示名称。

通用编辑器创建的包还写入 `audio_routing: "generic_event"`，按通用击杀、爆头、刀杀、手雷等事件槽位播放。原生素材包不写这一标记，继续遵循各游戏自己的档位和组合规则，避免把 CF 或卡拉彼丘的原生槽位套到用户新建包上。

音效包可提供 `associated_icon_keys` 数组，也可与图标共享 `association_id`。联动开启时，图标与音效双向选择已安装的配套包；多配色共享一个音效时优先保留当前配色。语音包不参与图标联动。

卡拉彼丘的角色清单另提供 `audio.slot_delays_ms`、`audio.slot_cooldowns_ms`，按 `kill_1` 等档位保留游戏原始延迟和冷却。角色语音与图标音效使用独立播放组，打断其中一路不会停止另一路。

## 播放接口

`GET /audio/channels` 返回两路当前选择。`POST /audio/channels` 一次提交两路，全部校验成功后才切换；缺省或 null 的通道关闭：

```json
{
  "sound_effect": {
    "preset": "calabiyau",
    "volume_percent": 100,
    "enabled": true,
    "interrupt_previous": true
  },
  "character_voice": null
}
```

外部包附带 `custom_path` 与 `display_name`。总音量仍作用于两路，各路音量乘在总音量上。`/test/<count>?audio=true&audio_channel=sound_effect|character_voice|all` 可选择测试通道。旧 `/soundpack` 接口继续表示单路旧模式，不能在新双路配置后再次调用它，否则会恢复单路状态。

## 卡拉彼丘素材

`Build-CalabiyauAudioPacks.py` 从已核对的本地研究输出生成模块：34 套主题音效覆盖 161 个图标变体，另提供基础音效包；24 名角色及其皮肤共 50 个中文语音包。基础程序只内置五档默认击杀音效，其余按需导入。

角色声音沿游戏触发表、RoleVoice、中文 AkEvent、Wwise 音频库逐级核对，只映射普通击杀及二至六杀。普通击杀在候选台词中随机选择；不混入需要特定角色、队伍或技能条件的彩蛋。主题音效取各主题 MultiAudioList 的一至五杀整段，已包含主体及装饰的声音分层；不重复追加星星音效。

角色名称来自 ST_RoleName，皮肤名称来自 RoleSkin。语音导出选择普通干声分支，保留 Wwise 静态增益，再统一留出防削波余量。离线资源不模拟游戏运行时的动态滤波、空间化和 RTPC。原始触发映射和导出校验见交付目录的 `角色语音来源.json` 及研究目录的 `audio/character-validation.json`。
