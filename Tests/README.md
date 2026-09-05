# Tests

测试按运行环境放置：

- `CustomSequences/`：自定义序列格式、导入、编辑、持久化和导出的集成测试及辅助程序。
- `Regression/`：需要 Windows、PowerShell、MSBuild 或 Inno Setup 的 UI、打包、配置和安装器回归测试。
- `../KillConfirmService/src/**/tests.rs`：跟随 Rust 模块放置的单元测试。
- `../KillConfirmService/tests/`：Cargo 集成测试。

所有 PowerShell 测试均可从仓库根目录运行，例如：

```powershell
pwsh -NoProfile -File .\Tests\Regression\Test-GsiConfigConsistency.ps1
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
