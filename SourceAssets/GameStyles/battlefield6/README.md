# Battlefield 6

独立的战地 6 内置素材。来源和许可见 THIRD-PARTY-NOTICES.txt。

- `killconfirm/textures/`：上游原始 2048 × 2048 图标，保留高清源。
- `audio-source/`：上游原始 OGG，仅作来源存档。
- `soundpacks/battlefield6/`：运行时 WAV 音效与 manifest，由构建脚本自动暂存。
- 图标发布镜像：`Widget/Assets/GameStyles/battlefield6/`，供 UWP 和兼容显示读取。
- 动画：两端各自 `Controls/Animations/Battlefield6/`，只通过公共调度器接入。

基础击杀、爆头、近战、爆炸、助攻，以及本应用实际提供的目标/回合奖励可以驱动此风格。
Minecraft 的伤害值、载具摧毁、治疗、占点和独立荣誉事件不会从普通击杀数据推测；
相应原图仍完整保留，供未来增加真实事件协议后使用。
