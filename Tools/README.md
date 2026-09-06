# 开发工具

日常构建入口保留在仓库根目录：`Build-QuickPackage.ps1` 构建应用包并可本地安装，`Build-FullPackage.ps1` 生成完整 EXE 安装包，供 GitHub Actions 调用。

以下命令从仓库根目录运行：

- `python Tools/Crossfire/Build-CrossfireExternalPacks.py --help`：将旧 CF 资源归档转换为独立图标包和语音包。
- `python Tools/Crossfire/Build-CrossfirePacks.py --help`：将分类目录中的 CF 图标套装批量打包。
- `pwsh -File Tools/Danmaku/Start-DanmakuAnnotationGui.ps1`：启动弹幕标注审核界面，需要 PowerShell 7 和 Python。

CF 素材工具的输入文件保存在仓库外部，使用方法见 [CF 图标包说明](../docs/crossfire-icon-packs.md)。弹幕工具说明见 [标注说明](../Widget/Danmaku/Annotation/README.md)。

GSI 配置的唯一示例保存在 `KillConfirmService/gsi/gamestate_integration_killconfirm.cfg`。
