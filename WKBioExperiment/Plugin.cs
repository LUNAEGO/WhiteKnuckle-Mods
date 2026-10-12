// White Knuckle 신규 장신구 - 생체 실험된 (Biologically Experimented)
// 압도적인 기동성을 주는 대신, 배고픔을 관리하지 못하면 기동성을 잃고 굶어 죽는 장신구.
// 게임 파일은 건드리지 않고 런타임에 장신구/퍽 에셋을 만들어 게임 데이터베이스에 끼워 넣는다.
// 이 장신구를 낀 판은 온라인 리더보드에 제출하지 않는다 (Patches.cs)
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WKBioExperiment
{
    [BepInPlugin(Guid, "WK Biologically Experimented", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "LUNAEGO.BiologicallyExperimented";

        internal static ManualLogSource Log;

        // 표시 언어
        internal static ConfigEntry<string> Language;
        internal static bool Korean { get { return Language != null && Language.Value == "Korean"; } }
        internal static string L(string en, string ko) { return Korean ? ko : en; }

        // 장신구
        internal static ConfigEntry<int> Cost;
        internal static ConfigEntry<int> Pouches;
        internal static ConfigEntry<float> ScoreMultiplier;
        // 버프
        internal static ConfigEntry<float> Stamina, Climb, Gravity;
        internal static ConfigEntry<int> ExtraJumps;
        // 배고픔
        internal static ConfigEntry<float> SecondsToEmpty, FoodRecovery, MedicalRecovery, MobilityCutoff, StarveDamage;
        internal static ConfigEntry<string> MedicalKeywords;
        // 연출
        internal static ConfigEntry<float> GrayStart;
        internal static ConfigEntry<string> GrayMode;
        // 테스트
        internal static ConfigEntry<bool> DebugKeys;

        void Awake()
        {
            Log = Logger;

            Language = Config.Bind("General", "Language", "English",
                new ConfigDescription("Text language for the trinket name and description / 장신구 이름·설명 언어",
                    new AcceptableValueList<string>("English", "Korean")));

            Cost = Config.Bind("Trinket", "Cost", 1, "Trinket slot cost");
            Pouches = Config.Bind("Trinket", "Pouches", 2, "Extra pouches (inventory slots) granted");
            ScoreMultiplier = Config.Bind("Trinket", "ScoreMultiplierBonus", -0.35f, "Final score multiplier change (-0.35 = -35%). Runs with this trinket are never submitted to leaderboards");

            Stamina = Config.Bind("Buff", "Stamina", 0.3f, "Stamina bonus (0.3 = +30%)");
            Climb = Config.Bind("Buff", "ClimbSpeed", 0.15f, "Climb speed bonus (0.15 = +15%)");
            ExtraJumps = Config.Bind("Buff", "ExtraJumps", 1, "Extra jumps");
            Gravity = Config.Bind("Buff", "Gravity", -0.2f, "Gravity change (-0.2 = -20%)");

            SecondsToEmpty = Config.Bind("Hunger", "SecondsToEmpty", 270f,
                "Seconds for hunger to go from full to 0%. 270 = 4 min 30 s");
            FoodRecovery = Config.Bind("Hunger", "FoodRecovery", 0.5f, "Hunger restored per food item (0.5 = 50%)");
            MedicalRecovery = Config.Bind("Hunger", "MedicalRecovery", 1f, "Hunger restored by pills and injectors (1.0 = 100%)");
            MedicalKeywords = Config.Bind("Hunger", "MedicalItemKeywords", "Pill,Injector,Inoculator,Syringe,Adrenaline",
                "Items whose name contains one of these words count as pills/injectors (comma separated, case-insensitive)");
            MobilityCutoff = Config.Bind("Hunger", "MobilityCutoffPercent", 0.15f,
                "At or below this hunger, mobility buffs (stamina, climb, extra jump, gravity) turn off. They come back above it");
            StarveDamage = Config.Bind("Hunger", "StarveDamagePerSecond", 0f,
                "Extra damage per second at 0% hunger. 0 = only the game's own starvation damage (0.3 every second)");

            GrayStart = Config.Bind("Visual", "GrayStartPercent", 0.5f, "The screen starts turning gray below this hunger");
            GrayMode = Config.Bind("Visual", "GrayMode", "Auto",
                new ConfigDescription("Auto = desaturate with post-processing if available, otherwise a translucent overlay / PostProcess / Overlay / Off",
                    new AcceptableValueList<string>("Auto", "PostProcess", "Overlay", "Off")));

            DebugKeys = Config.Bind("Debug", "EnableDebugKeys", false,
                "If true: F8 = hunger 0%, F9 = hunger full (for testing)");

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

            var go = new GameObject("WKBioExperiment_Controller");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<BioHungerController>();

            Log.LogInfo("Biologically Experimented trinket loaded (language: " + Language.Value + ")");
        }
    }
}
