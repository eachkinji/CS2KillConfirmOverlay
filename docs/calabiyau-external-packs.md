# 卡拉彼丘独立图标包

从 5.0.1.34 起，161 个主题变体各自发布为独立 ZIP，不再包含在安装器中。5.0.1.39 的主程序内置六档默认场景（含连杀星星）、原生击杀准心及共用事件图标、文字和数字；基础资源和默认音效合计约 5.04 MiB。

选择卡拉彼丘，在图标包库点击“导入图标包”“批量导入图标包”，或拖入 ZIP。无需解压或安装其他主题。未导入的变体不会出现在列表中；下方与准心仍独立开关，其他游戏的准心不变。

每个 ZIP 包含 `manifest.json`、`pack_head.png`、`lower-1.json.gz` 至 `lower-6.json.gz` 和该变体实际引用的 `textures/*.png`。第六档用于超过五杀的兼容计数；五杀动画引用该可选档位，旧包仍回退第五档。粒子、网格、动态材质图块和原生精灵指令保持原来的修复版本。通用事件图标及准心由基础程序提供。

清单包含 `format_version: 1`、`package_kind: calabiyau_icon`、`game_style: calabiyau`、稳定 `id`、名称与每个文件的长度和 SHA-256。安装前验证校验值、动画及纹理引用；损坏的更新不会替换已有资源。新版六档 ZIP 需要 5.0.1.38 或更高版本。

5.0.1.35 起，素材库默认在安装盘根目录的 `KillConfirm素材包` 文件夹；安装器可选择，控制面板“高级设置 → 进阶设置 → 素材包存放位置”可更换并迁移。导入位置为素材库下的 `calabiyau/icon_packs/<id>_<清单指纹>/`。选择键保持原来的 `calabiyau_<变体编号>`。同一变体重新导入会替换旧版本，内容目录变化使 Game Bar 文件缓存更新。资源库支持删除及再次导出。旧版本的全内置条目被移除，默认包及已导入包保留；升级时清理旧安装目录中的全量素材，不删除用户导入包。

生成与验证：

Git 仓库只保存基础击杀素材及其依赖，并保留对应的 Widget 基础副本；正常构建无需本机游戏或研究目录。独立主题图标、角色语音、额外音效及其 ZIP 不提交到仓库。以下完整重建命令需要另外准备游戏导出资源和本地提取工具；干净检出可以直接构建主程序，或使用 `python Build-CalabiyauExternalPacks.py --base-only` 刷新基础副本。

```powershell
python Build-CalabiyauExternalPacks.py --stage-base
python Tests/Regression/Test-CalabiyauExternalPacks.py --packages Output/CalabiyauIconPacks --source SourceAssets/GameStyles/calabiyau
KillConfirmCompatibility.exe --validate-calabiyau <测试输出> <包含Assets的目录> <ZIP目录>
```

生成目录包含独立 ZIP、索引和导入说明。只想刷新基础素材时使用 `--base-only`。`stage_effects.py` 默认也只暂存基础资源；全量研究输出必须明确传入 `--research-full-runtime`。正式安装器资源检查会拒绝混入主题动画及 FX 图集。

5.0.1.35 的 161 个显示名和 ZIP 名来自游戏 Weapon 数据表的 Name.SourceString，例如“绯天锦火-兰汀”。内部编号保持稳定，已导入的旧清单仍可用。

迁移先复制并校验 SHA-256，更新目录引用和设置成功后才清理原素材。目标必须为空；不能选择磁盘根目录或素材目录的上下级。小型配置仍在 UserData，Game Bar 按需保留临时读取缓存，并在下次启动时清理至 256 MiB。

绘图回归覆盖 Game Bar 亮度/对比度路径的 CanvasCommandList 延迟绘制：每层独立 ColorMatrixEffect，避免前面的文字/材质变成最后一张红色数字贴图；另覆盖 6 连杀、12 连杀和独立击杀准心。控制面板已更名 KillConfirmControlPanel.exe，Game Bar 保持 KillConfirmGameBar.exe。

5.0.1.36 保留原生事件文案（击败、精准击败、近战击败、道具击败），按原纹理宽高比显示事件图标及数字，共用文字使用高分辨率栅格化和较细描边。加法发光与普通透明材质分别烘焙、按原绘制顺序合成，修复星星等区域出现的黑块；缩至零面积的图层不会进入 Direct2D 过滤。更新程序后，已有主题需要重新导入新版 ZIP 才能更新烘焙材质。

所有 161 个变体通过生产 Win2D 延迟绘制路径采样六档动画后，再从实际播放器截图生成列表预览。打包器会拒绝早于当前动画的旧预览。完整流程：

```powershell
KillConfirmCompatibility.exe --validate-calabiyau-library <隔离测试输出> <SourceAssets/GameStyles/calabiyau绝对路径>
python SourceAssets/GameStyles/calabiyau/build_previews.py <隔离测试输出>
python Build-CalabiyauExternalPacks.py --output Output/CalabiyauIconPacks38
python Tests/Regression/Test-CalabiyauExternalPacks.py --packages Output/CalabiyauIconPacks38 --source SourceAssets/GameStyles/calabiyau
```

安装与存储设置显示安装位置和素材位置。选择父目录时分别建议 `Kill Confirm Overlay`、`KillConfirm素材包` 子目录，并允许新建文件夹。安装器更换安装路径会沿用共享配置和更新快捷方式；更换素材路径会显示旧、新路径和迁移提示，校验成功才更新素材引用。

5.0.1.38 修正嵌套 Slate 控件的绘制层级，中央图标、边框和装饰按原始 LayerId 合成。官方展示中的一至五杀没有额外计数横条，因此移除旧实现从二杀开始叠加计数条的推断。六杀及以上保留可读的前景计数作为产品兼容行为；其门槛和前景位置尚未从原生六杀画面或控制器代码确认。

Game Bar 使用固定逻辑画布，避免窗口大小再次缩放卡拉彼丘动画。切换图标包时预加载所有档位；播放使用击杀事件时间补偿异步加载，防止声音已播而星星仍在等待纹理。现有多杀音轨与官方展示的星星出现阶段匹配，本版没有追加未经验证的独立音效。

程序更新不会覆盖外部素材库。旧图标包需要重新导入 38 版 ZIP 取得新层级；语音包重新导入 38 版 ZIP 后取得角色／皮肤头像，包 ID 和选择保持不变。

5.0.1.39 补齐默认包原本遗漏的连杀场景。普通击杀仍遵循原始 `Anim_Normal`，多杀使用 `Anim_MaxCount` 对应场景，并按原始星位及子动画依次出现。默认资源由安装器更新；已有 38 版主题图标包不因这项修复重新发布。基础打包检查校验六个默认场景的纹理闭包，拒绝混入外部主题图集。
