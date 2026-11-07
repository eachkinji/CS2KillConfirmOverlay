# 三国杀 LZ4 无损帧导出

5.9.2.3 的源码同步保留本工具及其重建配置；两份 `sanguosha/killconfirm/lz4/` 离线预渲染输出属于本机实验数据，已加入 Git 忽略规则，也不随正式安装包分发。下述导出、同步流程为显式维护操作，正常构建不依赖已生成的大型缓存。

这是正式可维护的离线工具。仅处理配置选定的三国杀 SGSD / PNG；输入只读，输出必须位于源目录外。没有 FFmpeg、视频中间文件、播放窗口或显存测试脚手架。

它直接链接生产 `SanguoshaClip`、`SanguoshaFrameRenderer`、`SanguoshaLz4Source` / codec，以及桌面 Win2D 激活代码。原生 LZ4 由项目 `Native/Lz4/Lz4.targets` 从现有源码构建并附带许可证；不用额外下载 LZ4。

在仓库根目录编译与检查配置：

```powershell
dotnet build Tools/SanguoshaLz4/SanguoshaLz4.csproj -c Release -o Output/SanguoshaLz4Tool
./Output/SanguoshaLz4Tool/SanguoshaLz4Baker.exe validate Tools/SanguoshaLz4/Profiles/uwp-240dpi.json Output/SanguoshaExport-New
```

`validate` 不创建 GPU 设备、不解码图片、不写输出目录，只检查参数、输入 clip 存在与输出冲突。确认实际需要导出后，用同一命令把 `validate` 改成 `bake-set`。**`bake-set` 会使用 GPU，导出多片需与其他 GPU 性能实验串行运行。** 退出码为 0 且 `bake-set-index.json` 的 `Complete=true` 才表示整批完成。异常输出到 stderr；已创建输出目录时另存 `bake-set-failure.json`。

`SourceAssetRoot` 相对于配置文件目录解析；也支持绝对路径。`Clips` 可只列一片，也可列全部 11 片。每个 `Variants` 指定：

- `ProfileId`：与生产资源路径一致的标识。
- `Width` / `Height`：期望的**物理像素**大小；创建 target 后会断言真实像素大小匹配。
- `TargetDpi`：真实 CanvasRenderTarget DPI。非 96 DPI 时必须显式给出 `WidthDips` / `HeightDips`。
- `TransformKind=explicit`：`Matrix` 为 `M11,M12,M21,M22,M31,M32`，即 design 到 target **DIP** 的原始矩阵。不要预先乘 DPI 把真实 target 替换成 96 DPI 并假定等价。
- `TransformKind=desktop-default`：保持生产默认矩阵的完整浮点乘法链 `Scale(.6875f) * (Scale((float)(fit/.6875)) * Translate(center))`；此路径限生产桌面 96-DPI 物理 target，不能代数简化成单个 fit。

内置配置提供本机 240 DPI 桌面 `1375×773 @96dpi`、UWP `1100×619 DIP @240dpi →2750×1548px`，以及仅作参考的 `1600×900 @96dpi`。它们都是输入模板；完成整个宿主 wrapper 对照之前，不将新配置产生的文件视为已验收交付素材。

固定 `Seed=20261107` 使用原 `SanguoshaShake` 算法。抖动位移先左乘原矩阵，已烘入输出，播放器不可再重复施加。用户已接受预先固定轨迹。所有导出限定 source boost=0/0、appearance=1/1；其他颜色参数需相应精确变体或原画兜底，外部 opacity 不烘入。`ShakeMode=none` 仅用于明确无抖动的实验。

每片依照 SGSD 的 60 Hz 时间采样，保留生产相同的 tick 截断表达式；终止姿态不另占一个时间段。11 片共 1516 帧。每帧原 renderer 得到 BGRA8 premultiplied 字节后，使用生产 codec 压缩、解压并逐字节比对。写完再使用生产 indexed reader 重开 `.partial`，核对每一帧的 SHA-256；全部通过才改名为正式文件。

输出为 `lz4/{profileId}/{tier}.sgslz4`，保持 `KCLZ4F01` 容器契约。旁边 metadata 记录 DPI/DIPs/物理尺寸、矩阵、事件与固定轨迹、每帧 hash、输入 SGSD/PNG hash、文件大小及最终 hash。已有正式文件拒绝覆盖；失败的 `.partial` 留作诊断，重试请使用新输出目录。1600 参考不要自动复制进正式资源目录。

验证边界：本工具证明**声明的 target/DPI/矩阵/采样时间/颜色/seed 下，链接生产 renderer 的 BGRA 经压缩与落盘恢复逐字节一致**。它没有运行完整 UWP `CanvasControl` 或桌面 `DrawDesktopFrame` wrapper，没有覆盖宿主 viewport、颜色效果链、上传、DWM、off-grid 插值或任意高刷新率呈现。正式验收仍应在两端实际 wrapper 上逐帧对照；不能把压缩无损称为所有尺寸与刷新率输出逐像素等价。

## 正式 UWP 资源同步

`Sync-ActualUwpAssets.ps1` 是显式维护步骤，构建不会自动调用它。默认读取 `Output/SanguoshaLz4Integration-20261107/bake-uwp-actual-dpi`；要求完整 11 片、1516 帧、实际 240 DPI / 1100×619 DIP / `.6875` 矩阵及全部压解校验通过。先逐一检查源 manifest、metadata、大小和 SHA-256，再将 11 个容器与 11 个 metadata 同步到 SourceAssets 和 Widget 的对应 UWP profile。复制经过临时文件 hash 校验后原子替换，并再次核对目标 hash；不删除额外文件，也不触碰已安装用户数据、导入素材清单或其他游戏。

`-ValidateOnly` 或 `-WhatIf` 不复制，但仍会读取数 GiB 文件计算 SHA；应避开性能测试窗口。完整同步成功后才向 `SourceAssets/GameStyles/sanguosha/README.md` 追加带标记的 LZ4 说明，并写入 Output 同步报告。未完成导出或只有一片样例时会在任何目标写入前拒绝执行。
