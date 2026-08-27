<#
.SYNOPSIS
    Verifies that every independently compiled/packaged CS2 GSI template renders
    the same text for the default service port.
#>

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$DefaultServiceUri = "http://127.0.0.1:10087/"
$ServicePortToken = "__KILLCONFIRM_PORT__"
$WidgetUriToken = "__WIDGET_GSI_SERVICE_URI__"

function Normalize-GsiText {
    param([AllowEmptyString()][string]$Text)

    if ($null -eq $Text) {
        $Text = ""
    }
    return $Text.Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")
}

function ConvertFrom-EscapedSourceLiteral {
    param([string]$Text)

    return [System.Text.RegularExpressions.Regex]::Unescape($Text)
}

function Get-WidgetGsiTemplate {
    $path = Join-Path $Root "Widget\Pages\KillConfirmWidget\KillConfirmWidgetPage.xaml.cs"
    $source = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $block = [regex]::Match(
        $source,
        '(?s)private const string GsiConfigTextTemplate\s*=\s*(?<body>.*?);')
    if (-not $block.Success) {
        throw "Unable to read the widget GSI CFG template: $path"
    }

    $body = $block.Groups["body"].Value.Replace(
        "GsiServiceUriToken",
        ('"{0}"' -f $WidgetUriToken))
    $result = New-Object System.Text.StringBuilder
    foreach ($literal in [regex]::Matches($body, '"(?<value>(?:\\.|[^"\\])*)"')) {
        [void]$result.Append((ConvertFrom-EscapedSourceLiteral $literal.Groups["value"].Value))
    }

    return $result.ToString().Replace($WidgetUriToken, $DefaultServiceUri)
}

function Get-ServiceGsiTemplate {
    $path = Join-Path $Root "KillConfirmService\src\api\requests.rs"
    $source = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $literal = [regex]::Match(
        $source,
        'const GSI_CONFIG_TEXT_TEMPLATE: &str = "(?<value>(?:\\.|[^"\\])*)";')
    if (-not $literal.Success) {
        throw "Unable to read the service GSI CFG template: $path"
    }

    return (ConvertFrom-EscapedSourceLiteral $literal.Groups["value"].Value).Replace(
        $ServicePortToken,
        "10087")
}

$installerModule = Join-Path $Root "Installer\Scripts\Install\Cs2.ps1"
. $installerModule

$templates = [ordered]@{
    "canonical sample" = Get-Content -LiteralPath (Join-Path $Root "KillConfirmService\gsi\gamestate_integration_killconfirm.cfg") -Raw -Encoding UTF8
    "installer PowerShell" = New-Cs2GsiConfigText -ServicePort 10087
    "widget" = Get-WidgetGsiTemplate
    "service" = Get-ServiceGsiTemplate
}

$expected = Normalize-GsiText $templates["canonical sample"]
foreach ($entry in $templates.GetEnumerator()) {
    $actual = Normalize-GsiText $entry.Value
    if (-not [string]::Equals($actual, $expected, [System.StringComparison]::Ordinal)) {
        throw "GSI CFG template mismatch: $($entry.Key) differs from the canonical sample. Synchronize every template before packaging."
    }
}

Write-Host " [OK] GSI CFG templates match (installer / widget / service / sample)" -ForegroundColor Green
