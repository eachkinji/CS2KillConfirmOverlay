using System;
using System.IO;

namespace KillConfirmCompatibility.Contracts
{
    public enum PackImportContent { Unknown, Icon, Audio }

    // Shared by ZIP preflight and the audio importer. A missing declaration is
    // not evidence of a game; raw user assets remain valid manual imports.
    public sealed class PackImportClassification
    {
        public string Game { get; private set; }
        public string AudioKind { get; private set; }
        public PackImportContent Content { get; private set; }

        public static PackImportClassification Read(string gameStyle, string legacyGame,
            string packageKind, string legacyKind, string audioKind, bool hasAudio,
            bool hasAudioFiles = false, bool hasImageFiles = false)
        {
            string package = Normalize(packageKind), kind = Normalize(legacyKind);
            string inferredGame = "", inferredAudio = "";
            PackImportContent content = PackImportContent.Unknown;
            switch (package)
            {
                case "valorant_voice": inferredGame = "valorant"; inferredAudio = "sound_effect"; content = PackImportContent.Audio; break;
                case "crossfire_voice": inferredGame = "crossfire"; inferredAudio = "character_voice"; content = PackImportContent.Audio; break;
                case "valorant_icon": inferredGame = "valorant"; content = PackImportContent.Icon; break;
                case "crossfire_icon": inferredGame = "crossfire"; content = PackImportContent.Icon; break;
                case "calabiyau_icon": inferredGame = "calabiyau"; content = PackImportContent.Icon; break;
                case "titanfall2_icon": inferredGame = "titanfall2"; content = PackImportContent.Icon; break;
                case "cod2022_icon": inferredGame = "cod2022"; content = PackImportContent.Icon; break;
                case "leagueoflegends_icon": inferredGame = "leagueoflegends"; content = PackImportContent.Icon; break;
                case "rdr2_icon": inferredGame = "rdr2"; content = PackImportContent.Icon; break;
                case "audio": content = PackImportContent.Audio; break;
                case "icon": content = PackImportContent.Icon; break;
            }
            string game = NormalizeGame(gameStyle), legacy = NormalizeGame(legacyGame);
            if (game.Length == 0) game = legacy;
            if ((legacy.Length > 0 && game != legacy) || (inferredGame.Length > 0 && game.Length > 0 && game != inferredGame))
                throw new InvalidDataException("素材包的游戏声明互相冲突 / Conflicting pack game declarations.");
            if (game.Length == 0) game = inferredGame;

            string declaredAudio = NormalizeAudioKind(audioKind);
            if (!string.IsNullOrWhiteSpace(audioKind) && declaredAudio.Length == 0)
                throw new InvalidDataException("无法识别素材包的音频类型 / Unknown audio kind.");
            PackImportContent legacyContent = kind == "icon" ? PackImportContent.Icon
                : kind == "audio" || kind == "voice" || kind == "sound_effect" || kind == "character_voice" ? PackImportContent.Audio : PackImportContent.Unknown;
            if (legacyContent != PackImportContent.Unknown && content != PackImportContent.Unknown && legacyContent != content)
                throw new InvalidDataException("素材包的类型声明互相冲突 / Conflicting pack content declarations.");
            if (content == PackImportContent.Unknown) content = legacyContent;
            if (declaredAudio.Length > 0 || hasAudio)
            {
                if (content == PackImportContent.Icon)
                    throw new InvalidDataException("图标包不能包含音频包声明 / Conflicting icon and audio declarations.");
                content = PackImportContent.Audio;
            }
            if (content == PackImportContent.Unknown)
            {
                if (hasAudioFiles && !hasImageFiles) content = PackImportContent.Audio;
                else if (hasImageFiles && !hasAudioFiles) content = PackImportContent.Icon;
            }
            return new PackImportClassification { Game = game, Content = content,
                AudioKind = declaredAudio.Length > 0 ? declaredAudio : inferredAudio };
        }

        public void Validate(string requestedGame, bool audioPage)
        {
            if (Game.Length > 0 && Game != NormalizeGame(requestedGame))
                throw new InvalidDataException("素材包所属游戏与当前页面不一致，请切换到对应游戏后导入 / This pack belongs to another game.");
            if (audioPage && Content == PackImportContent.Icon)
                throw new InvalidDataException("这是图标包，请在图标包库导入 / Import this icon pack in the icon library.");
            if (!audioPage && Content == PackImportContent.Audio)
                throw new InvalidDataException("这是音频包，请在音效包或语音包库导入 / Import this audio pack in an audio library.");
        }

        public static string NormalizeAudioKind(string value)
        {
            switch (Normalize(value))
            {
                case "sound_effect": case "sfx": return "sound_effect";
                case "character_voice": case "voice": return "character_voice";
                default: return "";
            }
        }

        private static string NormalizeGame(string value)
        {
            // Preserve GameStyleService's documented aliases without its
            // unknown-key fallback to Crossfire: an unknown game stays unknown.
            switch (Normalize(value))
            {
                case "ow": return "overwatch";
                case "modernwarfare": case "mw2019": case "mw19": return "modernwarfare2019";
                case "cod19": case "mw2022": case "mw22": case "mwii": case "modernwarfare2": case "modernwarfare2022": case "现代战争2022": case "使命召唤19": return "cod2022";
                case "apexlegends": case "apex_legends": return "apex";
                case "titanfall_2": case "titanfall": case "tf2": case "泰坦陨落": case "泰坦陨落2": return "titanfall2";
                case "bf1": return "battlefield1";
                case "bf5": return "battlefield5";
                case "bf4": return "battlefield4";
                case "bf2042": case "2042": return "battlefield2042";
                case "battlefield_6": case "bf6": return "battlefield6";
                case "delta": case "df": return "deltaforce";
                case "strinova": case "卡拉彼丘": return "calabiyau";
                case "豆包": return "doubao";
                case "大狗叫": return "dagoujiao";
                case "三国杀": case "sgs": return "sanguosha";
                case "lol": case "league_of_legends": case "英雄联盟": return "leagueoflegends";
                case "reddeadredemption2": case "reddead2": case "red_dead_redemption_2": case "荒野大镖客2": case "荒野大镖客": case "大表哥2": return "rdr2";
                default: return Normalize(value);
            }
        }

        private static string Normalize(string value) => (value ?? "").Trim().ToLowerInvariant();
    }
}
