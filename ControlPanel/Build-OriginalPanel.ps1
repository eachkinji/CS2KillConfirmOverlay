# Compile the existing control-panel sources; Generated is never an independent UI.
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskWidget = Join-Path $taskRepository 'Widget'
$taskGenerated = Join-Path $PSScriptRoot 'Generated'
[xml]$taskProject = Get-Content -LiteralPath (Join-Path $taskWidget 'KillConfirmGameBar.csproj') -Raw
$taskNamespace = [Xml.XmlNamespaceManager]::new($taskProject.NameTable)
$taskNamespace.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
$taskWritten = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($taskItem in $taskProject.SelectNodes('//m:Compile|//m:Page', $taskNamespace)) {
    $taskInclude = $taskItem.GetAttribute('Include')
    if (!$taskInclude) { continue }
    $taskPattern = Join-Path $taskWidget $taskInclude
    if ($taskInclude.Contains('**')) {
        $taskBase = $taskPattern.Substring(0, $taskPattern.IndexOf('**')).TrimEnd('\','/')
        $taskFiles = Get-ChildItem -LiteralPath $taskBase -Recurse -File -Filter ('*' + [IO.Path]::GetExtension($taskInclude))
    } else { $taskBase = Split-Path $taskPattern -Parent; $taskFiles = @(Get-Item -LiteralPath $taskPattern) }
    foreach ($taskFile in $taskFiles) {
        $taskRelative = [IO.Path]::GetRelativePath($taskWidget, $taskFile.FullName).Replace('\','/')
        if ($taskRelative -in @('App.xaml.cs','Properties/AssemblyInfo.cs','Services/Runtime/SharedRuntime.cs','Services/Runtime/SharedResources.cs') -or $taskRelative.StartsWith('Pages/KillConfirmWidget/') -and !$taskRelative.EndsWith('.Styles.xaml') -or $taskRelative.StartsWith('Controls/Animations/') -or $taskRelative.EndsWith('DanmakuOverlay.xaml') -or $taskRelative.EndsWith('DanmakuOverlay.xaml.cs')) { continue }
        if ($taskRelative.StartsWith('../CompatibilityHost/Integration/')) {
            $taskSub = $taskRelative.Substring('../CompatibilityHost/Integration/'.Length)
            if ($taskSub.StartsWith('KillConfirmWidgetPage.')) { continue }
            $taskRelative = 'Features/CompatibilityDisplay/Integration/' + $taskSub
        } elseif ($taskRelative.StartsWith('../CompatibilityHost/Contracts/')) {
            $taskRelative = 'Features/CompatibilityDisplay/Contracts/' + $taskFile.Name
        }
        $taskText = [IO.File]::ReadAllText($taskFile.FullName)
        $taskText = $taskText.Replace('Windows.UI.Xaml', 'Microsoft.UI.Xaml')
        foreach ($taskXamlRoot in @('Pages','Controls','Features')) { $taskText=$taskText.Replace('ms-appx:///'+$taskXamlRoot+'/', 'ms-appx:///Generated/'+$taskXamlRoot+'/') }
        if ($taskFile.Extension -eq '.cs') {
            $taskText = $taskText.Replace('Windows.UI.Xaml', 'Microsoft.UI.Xaml')
            $taskText = $taskText.Replace('ApplicationData.Current', 'DesktopPlatform.Data').Replace('Package.Current', 'DesktopPlatform.Package')
            $taskText = $taskText.Replace('Windows.ApplicationModel.DesktopPlatform', 'DesktopPlatform')
            $taskText = $taskText.Replace('ApplicationDataContainer', 'DesktopLocalSettings')
            $taskText = $taskText.Replace('Windows.UI.Text.FontWeights', 'Microsoft.UI.Text.FontWeights')
            $taskText = $taskText.Replace('Windows.UI.Core.WindowSizeChangedEventArgs', 'Microsoft.UI.Xaml.WindowSizeChangedEventArgs')
            $taskText = $taskText.Replace('new MediaElement', 'new DesktopMediaElement')
            $taskText = $taskText.Replace('StorageApplicationPermissions.FutureAccessList.Clear();', '// Ordinary desktop file access has no UWP future-access grants.')
            $taskText = $taskText.Replace('CoreApplication.RequestRestartAsync', 'DesktopPlatform.RestartAsync')
            $taskText = $taskText.Replace('StorageFile.GetFileFromApplicationUriAsync', 'DesktopPlatform.AssetFileAsync')
            $taskText = $taskText.Replace('Dispatcher.RunAsync', 'DesktopPlatform.DispatchAsync').Replace('Dispatcher.HasThreadAccess', 'DispatcherQueue.HasThreadAccess')
            $taskText = $taskText.Replace('Window.Current', 'DesktopPlatform.Window').Replace('DisplayInformation.GetForCurrentView()', 'DesktopPlatform.Display')
            $taskText = [regex]::Replace($taskText, '(\w+)\.(PickSingleFileAsync|PickMultipleFilesAsync|PickSingleFolderAsync)\(\)', 'DesktopPlatform.$2($1)')
            $taskText = [regex]::Replace($taskText, '(\w+)\.ShowAsync\(\)', 'DesktopPlatform.ShowDialogAsync($1)')
            $taskText = [regex]::Replace($taskText, '(\w+)\.IsOpen = true;', 'DesktopPlatform.OpenPopup($1);')
            $taskText = $taskText.Replace('Windows.UI.Colors', 'Microsoft.UI.Colors')
            if($taskFile.Name -eq 'CompatibilityDisplayPanel.xaml.cs') {
                $taskText=$taskText.Replace('GameBarMode.IsChecked = !compatibility;', 'GameBarMode.IsChecked = !compatibility; GameBarMode.IsEnabled = DesktopPlatform.GameBarAvailable;')
                $taskText=$taskText.Replace('OpenGameBarButton.IsEnabled = !_switching', 'OpenGameBarButton.IsEnabled = DesktopPlatform.GameBarAvailable && !_switching')
                $taskText=$taskText.Replace('HomeView.ApplyLanguage(); ApplyModeGuide(', 'if (!DesktopPlatform.GameBarAvailable) GameBarModeHint.Text = zh ? "未安装可用的 Game Bar 小组件，可直接使用兼容显示。" : "Game Bar widget is unavailable. Desktop display works directly."; HomeView.ApplyLanguage(); ApplyModeGuide(')
            }
        } elseif ($taskText.Contains('FlyoutBase.AttachedFlyout')) {
            if (!$taskText.Contains('xmlns:primitives=')) { $taskText = $taskText.Replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:primitives="using:Microsoft.UI.Xaml.Controls.Primitives"') }
            $taskText = [regex]::Replace($taskText, '(?<!:)FlyoutBase\.AttachedFlyout', 'primitives:FlyoutBase.AttachedFlyout')
        }
        $taskDestination = Join-Path $taskGenerated $taskRelative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
        if (!(Test-Path -LiteralPath $taskDestination) -or [IO.File]::ReadAllText($taskDestination) -ne $taskText) { [IO.File]::WriteAllText($taskDestination, $taskText, [Text.UTF8Encoding]::new($false)) }
        $taskWritten.Add([IO.Path]::GetFullPath($taskDestination)) | Out-Null
    }
}
# Removing stale generated files prevents old panel code from surviving a source deletion.
if (Test-Path -LiteralPath $taskGenerated) {
    foreach ($taskStale in Get-ChildItem -LiteralPath $taskGenerated -Recurse -File) {
        if (!$taskWritten.Contains($taskStale.FullName)) { Remove-Item -LiteralPath $taskStale.FullName }
    }
}
Write-Host ('Original control-panel files adapted: ' + $taskWritten.Count)
