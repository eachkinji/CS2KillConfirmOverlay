Kill Confirm Overlay 安装载荷

- Install-KillConfirm.ps1：先安装普通主程序，再尝试可选 Game Bar MSIX 和环境配置。
- Scripts\Install：安装入口按职责加载的公共、AppX、依赖、Game Bar、Overlay 和 CS2 模块。
- Scripts\CompatibilityDisplay：独立的兼容显示安装模块；缺少 Game Bar 时准备兼容显示，已有偏好保持不变。
- Scripts\Setup：Inno Setup 安装页面的实时日志控件与进程输出接入。
- OverlayPackage：可选 Game Bar 小组件 MSIX 与签名证书。
- Prerequisites：离线依赖；安装前可逐项取消勾选，已满足版本要求的组件不会重复安装。

Standalone 包含普通控制面板、cskillconfirm、兼容显示和一份共享资源/运行库；安装后位于安装目录根部。

安装器自动检测并尝试安装 Xbox Game Bar 小组件，无需勾选。缺少防火墙服务、Game Bar 或 MSIX 安装失败时，跳过小组件，仍完成主程序安装。兼容显示支持窗口和无边框全屏；从开始菜单打开唯一的原控制面板，在主页选择兼容显示模式，并在素材与测试中调整屏幕与布局。

安装期间，进度条下方实时显示可滚动、可复制的安装日志；PowerShell 进程保持隐藏，完整诊断日志仍保存到文件。安装结束后，安装管理器会显示成功、提示或失败结果，并由用户决定是否打开诊断日志；安装脚本不会自动弹出日志窗口。

源码目录约定：Inno Setup 入口和 PowerShell 入口保留在 Installer 根目录；图片与图标放在 Assets，语言文件放在 Languages，安装逻辑放在 Scripts\Install。单个源码文件不得超过 500 行。

仅提供带离线依赖的新人安装包。控制面板、后台、兼容显示和内置素材为必装主体；Game Bar 小组件、Xbox Game Bar、各运行库、CS2 GSI 和桌面快捷方式可在安装前选择。
Windows 应用部署最多等待 120 秒；超时自动跳过剩余 Game Bar 部署并完成普通安装。系统可能仍处理已提交的请求，稍后可重新运行安装器重试选中的项目。
