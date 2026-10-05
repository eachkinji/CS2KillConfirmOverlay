# 兼容显示

这是独立的桌面透明窗口宿主，适用于窗口和无边框全屏。在高级设置 → 兼容显示开启，无需安装或运行 Xbox Game Bar。独占全屏不在支持范围内。

## 文件隔离

- 本项目独立编译为 `KillConfirmCompatibility.exe`，使用 WPF 窗口和桌面 Win2D；不引用 Widget 项目或其渲染源码。
- Win2D 激活工厂绑定宿主自己的 DLL，避免安装包根目录注册的旧 UWP Win2D 被新宿主加载。原 Game Bar 进程的组件注册保持不变。
- `Renderer/` 是独立的渲染基线，包含现有 15 种风格及弹幕。后续兼容显示的渲染改动只在这里维护，不回写 Game Bar 渲染器。
- `Contracts/` 定义新配置；`Integration/` 是高级设置入口，仅编译到设置应用，不编译到桌面宿主。
- 位置、缩放、显示器及显示开关独立存放于包的 `LocalState/CompatibilityDisplay/display.json`。运行状态写入同目录的 `status.json`；并发更新使用锁和原子替换。
- 两种显示方式共用已有游戏事件、音频服务和用户选择的资源包。资源目录只读，兼容宿主不执行旧资源库的迁移或目录保存。
- 旧文件只接入设置入口、启动参数、模式互斥和打包；没有加入兼容渲染分支。安装集成单独放在 `Installer/Scripts/CompatibilityDisplay/`，服务启动桥接单独放在 `infrastructure/compatibility.rs`。

## 布局与运行

各风格分别保存准星、下方反馈、上方反馈（MW2019）、弹幕区域的位置和缩放。下方徽章跟随下方反馈。布局编辑支持拖动、滚轮缩放、方向键微调、Shift 加速、Esc 保存退出，并可在设置页输入百分比、隐藏元素、居中或重置。

普通模式使用置顶、透明、鼠标穿透且不抢焦点的窗口；编辑时启用鼠标输入。默认跟随 CS2/CSGO 客户区，切出游戏后隐藏；可选择显示器、关闭跟随和隐藏、切换 30/60 FPS。Ctrl+Alt+O 切换显示，Ctrl+Alt+L 切换布局编辑。关闭兼容显示会停止该宿主，恢复 Game Bar 显示。

Win2D 绘制透明图像，再提交到 WPF 透明窗口。仅更新发生变化的元素，渲染尺寸有上限；这一实现包含像素回读，仍需在游戏实机上确认不同显卡、DPI、多屏及反作弊环境的表现。没有注入或修改游戏进程。

## 构建与验证

`Build-CompatibilityHost.ps1` 自包含发布，不要求用户额外安装 .NET。快速及完整安装包均通过打包项目独立携带此宿主。

```powershell
./CompatibilityHost/Validation/Test-CompatibilityDisplay.ps1 -CrossfirePack '路径/穿越火线—原版—图标包.zip'
```

验证使用独立临时配置和资源副本，检查配置边界、并发更新、风格隔离、损坏配置、原生鼠标/焦点标志，并将 15 种风格的实际 Win2D 像素导出为 PNG。CF 资源仍按原来的独立资源包分发方式提供。

同一验证还检查弹幕像素、跨进程设置变化、认证连接、后台服务注册、切出游戏隐藏，以及关闭后的退出和注销。`Validation/Test-PackageIdentity.ps1` 使用独立临时包验证子进程继承包身份、资源访问和渲染 DLL 隔离，完成后移除测试包。
