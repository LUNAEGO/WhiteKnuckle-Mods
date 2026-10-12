// White Knuckle 신규 속박 - 탈출한 생체 실험자 (Escaped Test Subject)
// 몸은 무겁고 약해졌지만, 굼벵이·알약·주사기·Lemon 로치를 섭취한 뒤 맨손을 5초간 뻗고 있으면
// 양손이 촉수로 변이해 바나클 훅처럼 촉수를 쏠 수 있다.
// 이 속박을 낀 판은 온라인 리더보드에 제출하지 않는다 (LeaderboardBlock.cs)
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WKEscapedSubject
{
    [BepInPlugin(Guid, "WK Escaped Test Subject", "0.9.3")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "LUNAEGO.EscapedTestSubject";

        internal static ManualLogSource Log;
        internal static string AssetDir;

        // 표시 언어
        internal static ConfigEntry<string> Language;
        internal static bool Korean { get { return Language != null && Language.Value == "Korean"; } }
        internal static string L(string en, string ko) { return Korean ? ko : en; }

        // 속박
        internal static ConfigEntry<int> Cost;
        internal static ConfigEntry<float> ScoreBonus, Gravity, Stamina, Capacity, Health, MoveSpeed;
        // 점착성 표면
        internal static ConfigEntry<float> StickyGroundSeconds, StickyDuration, StickyNoDrain;
        // 촉수 변이
        internal static ConfigEntry<float> MutateSeconds, TentacleStamina, TentacleRange, PullForce, WallGrabDistance, AnchorGrabRadius;
        internal static ConfigEntry<int> Charges;
        internal static ConfigEntry<string> ChargeKeywords;
        internal static ConfigEntry<float> ShootHold, CounterScale;
        // 테스트
        internal static ConfigEntry<bool> DebugKeys;

        void Awake()
        {
            Log = Logger;
            AssetDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "Assets");

            Language = Config.Bind("General", "Language", "English",
                new ConfigDescription("Text language for the binding name and description / 속박 이름·설명 언어",
                    new AcceptableValueList<string>("English", "Korean")));

            Cost = Config.Bind("Binding", "Cost", 1, "Binding cost (a binding adds this much to the trinket budget)");
            ScoreBonus = Config.Bind("Binding", "ScoreMultiplierBonus", 0.35f, "Final score multiplier bonus (0.35 = +35%). Runs with this binding are never submitted to leaderboards");
            Gravity = Config.Bind("Binding", "Gravity", 0.2f, "Gravity change (0.2 = +20%)");
            Stamina = Config.Bind("Binding", "Stamina", -0.2f, "Stamina change (-0.2 = -20%)");
            Capacity = Config.Bind("Binding", "InventoryCapacity", -2f, "Inventory capacity change (-2 = 5 slots down to 3)");
            Health = Config.Bind("Binding", "MaxHealth", -0.2f, "Max health change (-0.2 = -20%)");
            MoveSpeed = Config.Bind("Binding", "MoveSpeed", -0.2f, "Move speed change (-0.2 = -20%)");

            StickyGroundSeconds = Config.Bind("Sticky", "GroundSeconds", 5f, "Seconds on the ground before Sticky Surface triggers");
            StickyDuration = Config.Bind("Sticky", "Duration", 7f, "Grub effect duration (seconds), counted from the first wall grab after it triggers");
            StickyNoDrain = Config.Bind("Sticky", "NoStaminaDrainSeconds", 5f, "Stamina does not drain for this long after the first wall grab");

            MutateSeconds = Config.Bind("Mutation", "HoldSeconds", 5f, "Seconds you must reach out with a bare hand to mutate");
            Charges = Config.Bind("Mutation", "Charges", 5, "Tentacle shots per mutation");
            ChargeKeywords = Config.Bind("Mutation", "ChargeItemKeywords", "Pill,Injector,Inoculator,Syringe,Adrenaline,Grub,Roach_Lemon",
                "Items whose name contains one of these words charge the mutation (comma separated, case-insensitive)");
            TentacleStamina = Config.Bind("Mutation", "TentacleStaminaBonus", 0.35f,
                "Temporary stamina bonus while in tentacle form (0.35 = +35%). Removed when all shots are used");
            TentacleRange = Config.Bind("Mutation", "TentacleRange", 30f, "Tentacle range (m). The Barnacle Hook has 20m");
            PullForce = Config.Bind("Mutation", "PullForceMultiplier", 1.10f, "Tentacle pull force multiplier relative to the Barnacle Hook (1.10 = +10%)");
            WallGrabDistance = Config.Bind("Mutation", "WallGrabDistance", 2.8f,
                "Within this distance (m) of the tentacle anchor, the shooting hand can grab bare walls while the button is held");
            AnchorGrabRadius = Config.Bind("Mutation", "AnchorGrabRadius", 1.4f,
                "Bare walls can only be grabbed within this radius (m) of the anchor (walls along the pull path can't be grabbed)");
            ShootHold = Config.Bind("Mutation", "ShootHoldSeconds", 0.75f,
                "In tentacle form, hold the bare-hand button this long to shoot (grabbing something cancels it)");
            CounterScale = Config.Bind("Visual", "CounterScale", 3f, "Scale of the remaining-shots counter at the bottom right");

            DebugKeys = Config.Bind("Debug", "EnableDebugKeys", false,
                "If true: F6 = charge mutation, F7 = instant tentacle form (for testing)");

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

            var go = new GameObject("WKEscapedSubject_Controller");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MutationController>();

            Log.LogInfo("Escaped Test Subject binding loaded (language: " + Language.Value + ")");
        }
    }
}
