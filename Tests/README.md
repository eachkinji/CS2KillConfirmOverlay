# Tests

测试按运行环境放置：

- `CustomSequences/`：自定义序列格式、导入、编辑、持久化和导出的集成测试及辅助程序。
- `Regression/`：需要 Windows、PowerShell、MSBuild 或 Inno Setup 的 UI、打包、配置和安装器回归测试。
- `../KillConfirmService/src/**/tests.rs`：跟随 Rust 模块放置的单元测试。
- `../KillConfirmService/tests/`：Cargo 集成测试。

所有 PowerShell 测试均可从仓库根目录运行，例如：

```powershell
pwsh -NoProfile -File .\Tests\Regression\Test-GsiConfigConsistency.ps1
pwsh -NoProfile -File .\Tests\Regression\Test-LegacyProfileTransfer.ps1
pwsh -NoProfile -File .\Tests\Regression\Test-LegacyPanelUpgrade.ps1
pwsh -NoProfile -File .\CompatibilityHost\Validation\Test-OverwatchPreview.ps1
pwsh -NoProfile -File .\ControlPanel\Test-CloseBehavior.ps1 -Payload .\Output\StandaloneTest
pwsh -NoProfile -File .\Tests\CustomSequences\Test-CustomSequences.ps1
```

MSBuild 打包回归测试：

```powershell
MSBuild .\Tests\Regression\Test-CrossfirePackageResources.proj /t:Test /p:Configuration=Release /p:Platform=x64
```

Rust 测试：

```powershell
cargo test --manifest-path .\KillConfirmService\Cargo.toml
```

测试生成物统一写入仓库根目录的 `Output/`，该目录不会提交到 Git。

控制面板 UI 规范检查与隔离回归：

```powershell
python .\Tests\Regression\Test-PanelDesign.py
pwsh -NoProfile -File .\Tests\Regression\Test-PanelUi.ps1 -Language zh-CN
pwsh -NoProfile -File .\Tests\Regression\Test-PanelUi.ps1 -Language en-US
```

先构建 `ControlPanel/KillConfirmGameBar.csproj`。`Test-PanelUi.ps1` 默认使用 Debug
桌面输出，或通过 `-Payload` 指定构建目录；每项检查使用独立数据目录。
静态检查涵盖资源引用、可读字号和高度冲突，实际回归涵盖导航、游戏主题、
640px 换行布局、设置及音频通道。规范与验证范围见
[控制面板 UI 规范](../docs/control-panel-ui-standard.md)。
