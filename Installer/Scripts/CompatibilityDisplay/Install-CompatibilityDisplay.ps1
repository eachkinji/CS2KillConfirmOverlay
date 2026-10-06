# The ordinary panel chooses compatibility automatically when the optional widget
# is unavailable. Do not create package data or overwrite saved display layouts.
function Initialize-CompatibilityDisplayFallback {
    Add-InstallResult -Status Success -Item '兼容显示' -Detail '普通主程序已支持兼容显示，从开始菜单打开控制面板即可使用；首次启动自动保留并导入旧配置'
}
