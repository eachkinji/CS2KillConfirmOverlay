#Requires -Version 7.0
# Exercise the actual UI-independent frame definition without launching Game Bar.
$ErrorActionPreference = 'Stop'
$styleSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Services/Styling/GameStyleService.cs')
$styleEnum = [regex]::Match($styleSource, '(?s)internal enum GameStyleMode\s*\{[^}]+\}').Value
if (-not $styleEnum) { throw 'GameStyleMode declaration not found.' }
$definition = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'Widget/Services/Styling/KillFeedbackFrameDefinition.cs')
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
            return "PASS: " + testedFrames + " frame mappings across all 14 styles, unsupported-frame rejection, independent saved placements and six distinct colors.";
        }
    }
}
'@
if (-not ('KillConfirmGameBar.Services.FeedbackFrameRegressionChecks' -as [type])) {
    Add-Type -TypeDefinition ($definition + "`nnamespace KillConfirmGameBar.Services {`n" + $styleEnum + "`n}`n" + $checks)
}
[KillConfirmGameBar.Services.FeedbackFrameRegressionChecks]::Run()
