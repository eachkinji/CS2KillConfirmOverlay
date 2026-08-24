# Shared diagnostics, logging, and decoding helpers.
function Add-InstallResult {
    param(
        [ValidateSet("Success", "Warning", "Error")][string]$Status,
        [string]$Item,
        [string]$Detail = ""
    )

    $symbol = switch ($Status) {
        "Success" { "✅" }
        "Warning" { "⚠️" }
        default { "❌" }
    }
    $script:InstallResults.Add([pscustomobject]@{
        Status = $Status
        Item = $Item
        Detail = $Detail
    })
    Write-InstallLog ("{0} {1}{2}" -f $symbol, $Item, $(if ($Detail) { " - $Detail" } else { "" }))
}

function Get-ErrorReason {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if (-not $ErrorRecord) {
        return "未知错误"
    }
    $message = $ErrorRecord.Exception.Message
    $hresult = $ErrorRecord.Exception.HResult
    if ($hresult) {
        return ("{0} (HRESULT 0x{1:X8})" -f $message, ($hresult -band 0xffffffffL))
    }
    return $message
}

function Show-InstallSummary {
    $errorCount = @($InstallResults | Where-Object Status -eq "Error").Count
    $warningCount = @($InstallResults | Where-Object Status -eq "Warning").Count
    $successCount = @($InstallResults | Where-Object Status -eq "Success").Count
    $title = if ($errorCount -gt 0) {
        "Kill Confirm 安装诊断 - 有问题需要处理"
    }
    elseif ($warningCount -gt 0) {
        "Kill Confirm 安装诊断 - 已完成但有提示"
    }
    else {
        "Kill Confirm 安装诊断 - 全部成功"
    }

    $summaryLines = @(
        $title,
        "",
        "安装流程已经执行完毕，不会因为单项失败而跳过后续安装。",
        "成功 $successCount 项，提示 $warningCount 项，失败 $errorCount 项。",
        ""
    )
    foreach ($result in $InstallResults) {
        $symbol = switch ($result.Status) {
            "Success" { "✅" }
            "Warning" { "⚠️" }
            default { "❌" }
        }
        $summaryLines += ("{0} {1}" -f $symbol, $result.Item)
        if ($result.Detail) {
            $summaryLines += ("    原因/说明：{0}" -f $result.Detail)
        }
    }
    $summaryLines += ""
    $summaryLines += "完整日志：$LogPath"

    $logText = ""
    try {
        if (Test-Path -LiteralPath $LogPath) {
            $logText = Get-Content -LiteralPath $LogPath -Raw -ErrorAction Stop
        }
    }
    catch {
        $logText = "日志读取失败：$($_.Exception.Message)"
    }

    $reportText = (($summaryLines -join [Environment]::NewLine) +
        [Environment]::NewLine + [Environment]::NewLine +
        "================ 详细日志 ================" +
        [Environment]::NewLine + $logText)

    try {
        $utf8Bom = New-Object System.Text.UTF8Encoding($true)
        [System.IO.File]::WriteAllText($ResultPath, $reportText, $utf8Bom)

        $status = if ($errorCount -gt 0) {
            "Error"
        }
        elseif ($warningCount -gt 0) {
            "Warning"
        }
        else {
            "Success"
        }
        $statusText = @(
            "[Result]",
            "Status=$status",
            "SuccessCount=$successCount",
            "WarningCount=$warningCount",
            "ErrorCount=$errorCount",
            "ReportPath=$ResultPath",
            "LogPath=$LogPath"
        ) -join [Environment]::NewLine
        [System.IO.File]::WriteAllText($StatusPath, $statusText, [System.Text.Encoding]::ASCII)

        # The outer setup manager owns all user-facing completion prompts. The
        # script only writes result files and never opens Notepad by itself.
        Write-InstallLog "Installation result report: $ResultPath"
        Write-InstallLog "Installation status report: $StatusPath"
    }
    catch {
        Write-InstallLog "Failed to write the installation result report: $($_.Exception.Message)"
        Write-Host $reportText
    }
}

function ConvertFrom-Utf8Base64 {
    param([string]$Value)
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($Value))
}

function Write-InstallLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    try {
        Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8 -ErrorAction Stop
    }
    catch {
        Write-Host "[Log write failed: $($_.Exception.Message)]"
    }
    Write-Host $Message
}
