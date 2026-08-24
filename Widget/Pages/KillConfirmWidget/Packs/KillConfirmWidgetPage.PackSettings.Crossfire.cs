namespace KillConfirmGameBar
{
    public sealed partial class KillConfirmWidgetPage
    {
        private static bool SupportsBuiltInCodeIconPack(string iconPack)
        {
            switch ((iconPack ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "default":
                case "vip":
                case "angelic_beast":
                case "anniversary_10":
                case "anniversary_15":
                case "cfpl":
                case "rankmach_2019_1":
                case "rankmach_2019_2":
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizeCrossfireVoicePackAlias(string preset)
        {
            switch ((preset ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "cf":
                case "crossfire":
                    return "crossfire_swat_gr";
                case "cffhd":
                case "cf_fhd":
                case "crossfire_fhd":
                case "crossfire_v_fhd":
                    return "crossfire_flying_tiger_gr";
                case "kkgr":
                case "knifegr":
                case "knifekill_gr":
                    return "crossfire_women_gr";
                case "kkbl":
                case "knifebl":
                case "knifekill_bl":
                    return "crossfire_women_bl";
                default:
                    return null;
            }
        }
    }
}
