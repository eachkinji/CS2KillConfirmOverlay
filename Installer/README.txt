Kill Confirm Overlay 安装载荷

- Install-KillConfirm.ps1：执行依赖检查、MSIX 安装和环境配置。
- Scripts\Install：安装入口按职责加载的公共、AppX、依赖、Game Bar、Overlay 和 CS2 模块。
- Scripts\CompatibilityDisplay：独立的兼容显示安装模块；缺少 Game Bar 时准备兼容显示，已有偏好保持不变。
- Scripts\Setup：Inno Setup 安装页面的实时日志控件与进程输出接入。
- OverlayPackage：主程序 MSIX 与签名证书。
- Prerequisites：仅“有依赖”安装包包含的离线依赖。

Xbox Game Bar 为可选组件。兼容显示支持窗口和无边框全屏；从开始菜单打开本程序，在高级设置 → 兼容显示中开启功能并调整元素布局。

安装期间，进度条下方实时显示可滚动、可复制的安装日志；PowerShell 进程保持隐藏，完整诊断日志仍保存到文件。安装结束后，安装管理器会显示成功、提示或失败结果，并由用户决定是否打开诊断日志；安装脚本不会自动弹出日志窗口。

源码目录约定：Inno Setup 入口和 PowerShell 入口保留在 Installer 根目录；图片与图标放在 Assets，语言文件放在 Languages，安装逻辑放在 Scripts\Install。单个源码文件不得超过 500 行。
