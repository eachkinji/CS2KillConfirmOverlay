# 卡拉彼丘击杀反馈

5.0.1.39 的默认包使用 `killconfirm/default-lower-1.json.gz` 至 `default-lower-6.json.gz`，由原默认页的普通／连杀场景和星星子组件重建，恢复连杀星星、闪光、材质和粒子。基础素材按实际引用打包，包含默认音效约 5.04 MiB。161 个主题变体以独立 ZIP 发布，导入后分别注册为 `calabiyau_<编号>`；不随主程序打包。每个变体包含 1～5 杀原生场景；超过 5 杀使用第 6 个兼容场景，保留最高等级装饰与显示实际数量（最多 99）的前景计数层。下方反馈和原生击杀准心独立开关；其他游戏的准心分支不变。命中、助攻、死亡和经济事件不播放此反馈。

## 修复依据

- 游戏 DefaultEngine.ini 明确启用 `bExplicitCanvasChildZOrder=True`。绘制按 Slate LayerId 全局稳定排序，不能只递归输出控件树；相同 Z 的后续兄弟可能在先前子树的高层节点后绘制。
- 官方千代「花游舞夜」PV 的二、三、五杀逐帧均没有 `UW_Kill` 计数横条；Count 内部 CanvasPanel_6 默认透明，不能从二杀起擅自播放 Anim_ComboKill。第 6 个场景的前景计数是 Kill Confirm 超过原生五星范围的兼容行为，未声称已确认原生六杀控制逻辑。
- 动画按 `WidgetAnimation.AnimationBindings.AnimationGuid` 解析当前控件。`MovieScene.ObjectBindings.BindingName` 是可能过期的编辑器名称，不能作为控件绑定。
- 依赖按解析后的完整 `ObjectPath` 提取，保留 Unreal FName 的数字后缀，避免误用同名前缀的材质。
- `InGameKillFeedbackConfig` 第 12～15 行确定击败、精准击败、近战击败、道具击败分别使用动态 React9、React4、React11、React12。
- 按原始 UMG 布局、关键帧、可见性和激活轨道播放；星标使用原始子控件和 `StarConfig`、延迟参数；文字使用游戏字体生成的图像。
- Cascade 分布表驱动粒子出生、寿命、位置、速度、颜色、旋转、尺寸、序列帧及动态参数；SimpleMeshWidget 使用提取的 GLB 顶点、UV 和顶点色。
- 材质参数曲线和材质实例继承会进入 GPU 重建。图像控件保持原生绘制指令；材质、粒子和网格层烘焙成透明图块，并保持原来的绘制顺序。

## 重建与打包

本机游戏版本 832047，研究目录为 `F:/KillConfirm FIX/workshop/CalabiyauResearch`。CUE4Parse CLI 使用 `GAME_Strinova`。Python 需要 Pillow、NumPy、ModernGL 和可用的 OpenGL 环境。

这些提取与重建依赖仅供制作外部素材包使用，不是主程序构建依赖。仓库包含完整的基础播放资源及 Widget 副本；额外主题、角色语音、原始游戏资源和导出目录保持在本地，不随 Git 同步。音频再生成还需要仓库外的 wwiser、vgmstream 和扫描工具。

```powershell
python SourceAssets/GameStyles/calabiyau/extract_effects.py <研究目录>
python SourceAssets/GameStyles/calabiyau/bake_effects.py <研究目录>
python SourceAssets/GameStyles/calabiyau/stage_effects.py
python Build-CalabiyauExternalPacks.py --stage-base
```

可用 `--shards 4 --shard 0` 等参数分批生成；其他批次加 `--skip-dynamic`，避免重复写公共图像。`--resume` 跳过已经完整生成的变体。

`lower-1.json.gz` 至 `lower-6.json.gz` 为 60 FPS 指令。第 5 档可选 `overkill` 字段指向第 6 档；旧包没有该字段时仍兼容回退第 5 档。选择图标包时预热全部档位及贴图，首次多杀无需临时加载；播放使用服务的发布时刻加已耗加载时间补偿 UI 延迟。自 5.0.1.39 起，启用卡拉彼丘音频通道时，普通 GSI 击杀音轨在反馈事件发布后派发，与动画使用同一次发布作为时间起点；保留原生完整音轨的内部节奏，未宣称采样级音画同步。未变化的 64 像素图块会去重，周围保留相邻像素以供线性过滤。事件图标、文字和数字仍在运行时选择。每包的 `reconstructed.json` 记录源场景及粒子、材质、网格实际绘制数量。Widget 目录只保留基础资源；主题动画和引用贴图进入各自 ZIP。详见 `docs/calabiyau-external-packs.md`。

`build_packs.py` 保留为布局解析库；其旧批量入口有研究模式限制，不能用于发布，因为旧入口只处理静态图像。

## 验证与边界

`KillConfirmCompatibility.exe --validate-calabiyau <输出目录> <Widget目录> <ZIP目录>` 使用隔离配置测试导入、更新、损坏包拒绝、删除、主题选择、五档击杀、四种图标、连续播放、资源释放与重载、准心隔离，并导出原生渲染关键帧。

材质编辑器表达式和原游戏着色器字节码未直接执行。M_UI_Basic、M_UI_Diffuse、M_UI_CircleRoleMask 是根据导出参数重建的着色实现，粒子随机数也固定用于烘焙；不能把结构完整和移植前后画面一致当作与原游戏逐像素一致。动态角色头像使用资源默认值；没有伪造游戏中未提供的角色信息。原蓝图的事件回调字节码不执行，应用按 KillConfirm 事件维护击杀数量和播放状态。

默认星星依据原页 `StarConfig` 选择星位，控制器起播时间为 0.7 秒，后续间隔 0.1 秒；子动画的缩放、闪光峰值发生在起播之后。`Anim_Normal` 保留原资源隐藏星星的设置，连杀分支启用星容器。预加载与播放均按实际击杀数选择对应默认场景，避免所有杀数仅播放普通击杀主体。

本次不加入击杀信息栏或命中反馈。其他游戏资源保持原实现。
