#Requires -Version 7.0
# Exercise the actual UI-independent frame definition without launching Game Bar.
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$styleSource = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Styling/GameStyleService.cs')
$styleEnum = [regex]::Match($styleSource, '(?s)internal enum GameStyleMode\s*\{[^}]+\}').Value
if (-not $styleEnum) { throw 'GameStyleMode declaration not found.' }
$definition = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Services/Styling/KillFeedbackFrameDefinition.cs')
$markerPlayback = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/Animations/ModernWarfare2019/ModernWarfare2019Animation.Playback.cs')
$hostLayout = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Layout/KillConfirmWidgetPage.HostLayout.cs')
$dragVisuals = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Interaction/KillConfirmWidgetPage.DragVisuals.cs')
$widgetInput = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Pages/KillConfirmWidget/Interaction/KillConfirmWidgetPage.Input.cs')
$appearanceEditor = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/GameStyles/Shared/KillFeedbackAppearanceEditor.xaml.cs')
$crosshairOffsetEditor = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/GameStyles/Shared/CrosshairOffsetEditor.xaml')
$crosshairOffsetEditorCode = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/GameStyles/Shared/CrosshairOffsetEditor.xaml.cs')
$appearanceRow = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Widget/Controls/GameStyles/Shared/KillFeedbackAppearanceRow.xaml')
if ($markerPlayback -notmatch '(?s)if\s*\(killMarkOnly\)\s*\{\s*.*?_modernWarfare2019ImpactAngleDegrees\s*=\s*0\s*;\s*\}\s*else\s*\{\s*.*?_modernWarfare2019Random\.NextDouble') {
    throw 'Shared non-COD KillMark must stay axis-locked while full COD playback keeps its random impact angle.'
}
if ($hostLayout -match 'TryResizeWindowAsync|RefreshFixedWidgetLayout|SynchronizeHostPageLayout' -or
    $dragVisuals -match 'SynchronizeHostPageLayout|frame\.Width\s*=|frame\.Height\s*=') {
    throw 'The overlay must use the host-provided XAML layout without forcing window or Frame dimensions.'
}
if ($widgetInput -notmatch 'CenterWidgetWindowAsync\("crosshair-center"\)' -or
    $widgetInput -match 'RefreshFixedWidgetLayoutAndCenterAsync') {
    throw 'Crosshair centering must call only the native Game Bar center operation.'
}
if ($appearanceEditor -notmatch 'Dispatcher\.HasThreadAccess' -or
    $appearanceEditor -notmatch 'Dispatcher\.RunAsync\(CoreDispatcherPriority\.Normal' -or
    $appearanceEditor -notmatch 'KillFeedbackVisibilitySettingsStore\.Changed\s*-=' ) {
    throw 'Feedback appearance store callbacks must return to their owning UI dispatcher and release stale subscriptions.'
}
if ($crosshairOffsetEditor -notmatch 'x:Name="ResponsiveSettingsGrid"' -or
    $crosshairOffsetEditor -notmatch 'x:Name="ManualColumn"' -or
    $crosshairOffsetEditorCode -notmatch 'e\.NewSize\.Width\s*<\s*720\.0' -or
    $crosshairOffsetEditorCode -notmatch 'Grid\.SetRow\(ManualPanel,\s*compact\s*\?\s*1\s*:\s*0\)' -or
    $appearanceRow -notmatch 'x:Name="AdditionalSettingsHost"(?s).*?HorizontalContentAlignment="Stretch"') {
    throw 'Crosshair offset controls must stretch on wide settings pages and stack in the narrow advanced-effects flyout.'
}
$checks = @'
namespace KillConfirmGameBar.Services
{
    public static class FeedbackFrameRegressionChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new System.Exception(message);
        }

        public static string Run()
        {
            int testedFrames = 0;
            foreach (GameStyleMode style in System.Enum.GetValues(typeof(GameStyleMode)))
            {
                bool split = style == GameStyleMode.Overwatch || style == GameStyleMode.Apex
                    || style == GameStyleMode.ModernWarfare2019;
                var usedSlots = new System.Collections.Generic.HashSet<LegacyFeedbackPlacementSlot>();
                foreach (KillFeedbackLayer layer in System.Enum.GetValues(typeof(KillFeedbackLayer)))
                {
                    bool supported = layer != KillFeedbackLayer.Upper || style == GameStyleMode.ModernWarfare2019;
                    Check(KillFeedbackFrameDefinition.IsSupported(style, layer) == supported,
                        "Incorrect frame availability: " + style + "/" + layer);
                    if (!supported)
                    {
                        bool rejected = false;
                        try { KillFeedbackFrameDefinition.GetLegacyPlacementSlot(style, layer); }
                        catch (System.ArgumentOutOfRangeException) { rejected = true; }
                        Check(rejected, "Unsupported upper frame must not edit another frame's saved settings.");
                        continue;
                    }
                    var expected = layer == KillFeedbackLayer.Upper ? LegacyFeedbackPlacementSlot.Auxiliary
                        : layer == KillFeedbackLayer.Crosshair
                            ? (split ? LegacyFeedbackPlacementSlot.Primary : LegacyFeedbackPlacementSlot.Auxiliary)
                            : (split ? LegacyFeedbackPlacementSlot.LowerCard : LegacyFeedbackPlacementSlot.Primary);
                    var actual = KillFeedbackFrameDefinition.GetLegacyPlacementSlot(style, layer);
                    Check(actual == expected, "Old placement mapping changed: " + style + "/" + layer);
                    Check(usedSlots.Add(actual), "Two visible frames share saved placement: " + style);
                    if (actual == LegacyFeedbackPlacementSlot.Primary)
                        Check(KillFeedbackFrameDefinition.GetLegacyPrimaryLayer(style) == layer, "Primary transform mismatch");
                    if (actual == LegacyFeedbackPlacementSlot.Auxiliary)
                        Check(KillFeedbackFrameDefinition.GetLegacyAuxiliaryLayer(style) == layer, "Auxiliary transform mismatch");
                    testedFrames++;
                }
            }
            var allColors = new System.Collections.Generic.HashSet<uint>();
            foreach (KillFeedbackLayer layer in System.Enum.GetValues(typeof(KillFeedbackLayer)))
            {
                foreach (bool selected in new[] { false, true })
                {
                    uint color = KillFeedbackFrameDefinition.GetColorArgb(layer, selected);
                    Check(allColors.Add(color), "Frames or selection states have indistinguishable colors.");
                    Check((color >> 24) == 255, "Frame color must remain opaque.");
                }
            }
            return "PASS: " + testedFrames + " frame mappings across all " + System.Enum.GetValues(typeof(GameStyleMode)).Length + " styles, unsupported-frame rejection, independent saved placements and six distinct colors.";
        }
    }
}
'@
if (-not ('KillConfirmGameBar.Services.FeedbackFrameRegressionChecks' -as [type])) {
    Add-Type -TypeDefinition ($definition + "`nnamespace KillConfirmGameBar.Services {`n" + $styleEnum + "`n}`n" + $checks)
}
[KillConfirmGameBar.Services.FeedbackFrameRegressionChecks]::Run()
