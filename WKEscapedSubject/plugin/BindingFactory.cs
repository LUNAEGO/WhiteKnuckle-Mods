using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WKEscapedSubject
{
    // 속박(Trinket, isBinding)과 속박이 부여하는 퍽을 런타임에 만들어 게임 데이터베이스에 등록한다.
    internal static class BindingFactory
    {
        public const string BindingName = "Trinket_Binding_EscapedSubject";
        public const string PerkId = "Perk_Binding_EscapedSubject";
        public const string BioTrinketName = "Trinket_BioExperimented"; // 같이 쓸 수 없는 장신구
        // 둥지 입구가 "Rho 제단에 굼벵이를 바쳤는지" 확인하는 플래그. 켜 두면 어머니가 열기 지대로 보낸다
        const string HotZoneFlag = "session_sacrificed_grub";

        static Trinket binding;
        static Perk perk;

        public static void EnsureRegistered()
        {
            var databases = Traverse.Create(typeof(CL_AssetManager)).Field("databases").GetValue<System.Collections.IList>();
            if (databases == null || databases.Count == 0) return;
            WKAssetDatabase db = Traverse.Create(databases[0]).Field("database").GetValue<WKAssetDatabase>();
            if (db == null) return;

            if (binding == null) Build(db);

            if (db.perkAssets != null && !db.perkAssets.Contains(perk)) db.perkAssets.Add(perk);
            if (db.trinketAssets != null && !db.trinketAssets.Contains(binding)) db.trinketAssets.Add(binding);
            if (db.gamemodeAssets != null)
                foreach (M_Gamemode gm in db.gamemodeAssets) AddToGamemode(gm);
        }

        public static void AddToGamemode(M_Gamemode gm)
        {
            if (gm == null || gm.availableTrinkets == null || binding == null) return;
            List<Trinket> list = gm.availableTrinkets.bindings;
            if (list != null && !list.Contains(binding)) list.Add(binding);
        }

        internal static string Title { get { return Plugin.L("Escaped Test Subject", "탈출한 생체 실험자"); } }

        // 장신구 & 속박 선택 화면용 짧은 설명 (칸이 좁아 긴 설명은 잘린다. 자세한 수치는 퍽 설명에 남김)
        static string PickerText()
        {
            string f = Plugin.L(
                "A test subject mutated while escaping the lab. Weakened, but grotesque mutations awaken. <color=#FFD454>(No leaderboards)</color>\n" +
                "<color=#7CFF7C>+ Sticky Surface</color>: stay on the ground {0:0}s for the grub effect ({1:0}s once you grab a wall, no stamina drain for the first {2:0}s)\n" +
                "<color=#7CFF7C>+ Tentacle Mutation</color>: after eating a grub, pill, injector or Lemon Roach, reach with a bare hand for {3:0}s → {4} tentacle shots, stamina +{5:0}%\n" +
                "<color=#FF6B6B>- Weakness</color>: health {6:0}%, move speed {7:0}%, inventory {8:0}, gravity +{9:0}%, stamina {10:0}%  " +
                "<color=#FFD454>!</color> Hostile Mother, incompatible with 'Biologically Experimented'",
                "실험실을 탈출하며 변이된 실험체. 몸은 약해졌지만 기괴한 변이 능력이 발현됩니다. <color=#FFD454>(리더보드 제외)</color>\n" +
                "<color=#7CFF7C>+ 점착성 표면</color>: 지면에 {0:0}초 머무르면 굼벵이 효과 (벽을 짚고 {1:0}초, 처음 {2:0}초는 스태미나 유지)\n" +
                "<color=#7CFF7C>+ 촉수 변이</color>: 굼벵이·알약·주사기·Lemon 로치 섭취 후 맨손 {3:0}초 → 촉수 사출 {4}회, 스태미나 +{5:0}%\n" +
                "<color=#FF6B6B>- 약화</color>: 체력 {6:0}%, 이동속도 {7:0}%, 인벤토리 {8:0}, 중력 +{9:0}%, 스태미나 {10:0}%  " +
                "<color=#FFD454>!</color> 둥지의 어머니가 적대, '생체 실험된'과 함께 쓸 수 없음");
            return string.Format(f,
                Plugin.StickyGroundSeconds.Value, Plugin.StickyDuration.Value, Plugin.StickyNoDrain.Value,
                Plugin.MutateSeconds.Value, Plugin.Charges.Value, Plugin.TentacleStamina.Value * 100f,
                Plugin.Health.Value * 100f, Plugin.MoveSpeed.Value * 100f, Plugin.Capacity.Value,
                Plugin.Gravity.Value * 100f, Plugin.Stamina.Value * 100f);
        }

        // 게임 중 퍽 설명 (전체)
        static string PerkText()
        {
            string f = Plugin.L(
                "A test subject whose body mutated while escaping the lab. Heavier and weaker, but in extreme situations grotesque mutations awaken.\n" +
                "<color=#7CFF7C>+ Sticky Surface</color>: stay on the ground for {0:0}s to gain the grub effect. It lasts {1:0}s from the moment you grab a wall, and stamina doesn't drain for the first {7:0}s\n" +
                "<color=#7CFF7C>+ Tentacle Mutation</color>: after eating a grub, pill, injector or Lemon Roach, reach out with a bare hand for {2:0}s to turn both hands into tentacles. {3} tentacle shots (hold the bare-hand button {8:0.##}s, range {10:0}m). The tentacle stays while the button is held, and the shooting hand can grab bare wall near the anchor. Stamina +{11:0}% while in tentacle form\n" +
                "<color=#FF6B6B>- Weakness</color>: health {9:0}%, move speed {12:0}%, inventory capacity {4:0}, gravity +{5:0}%, stamina {6:0}%\n" +
                "<color=#FFD454>! Nest</color>: the Mother recognizes the escaped subject and turns hostile\n" +
                "<color=#FFD454>! Incompatible</color>: trinket 'Biologically Experimented'\n" +
                "<color=#FFD454>! Leaderboards</color>: runs with this binding are not submitted",
                "실험실을 탈출하는 과정에서 신체가 변이된 실험체. 몸은 무겁고 약해졌지만, 극한의 상황에서 기괴한 변이 능력이 발현됩니다.\n" +
                "<color=#7CFF7C>+ 점착성 표면</color>: 지면에 {0:0}초간 머무르면 굼벵이 효과 발현. 벽을 짚는 순간부터 {1:0}초 유지, 처음 {7:0}초는 스태미나가 줄지 않음\n" +
                "<color=#7CFF7C>+ 촉수 변이</color>: 굼벵이·알약·주사기·Lemon 로치 섭취 후 맨손을 {2:0}초간 뻗고 있으면 양손이 촉수로 변이, 촉수 사출 {3}회 (맨손 버튼 {8:0.##}초 유지, 사거리 {10:0}m). 버튼을 누르고 있는 동안 촉수가 유지되고, 박힌 곳 가까이에서는 쏜 손으로 맨벽도 잡을 수 있음. 촉수 상태 동안 스태미나 +{11:0}%\n" +
                "<color=#FF6B6B>- 약화</color>: 체력 {9:0}%, 이동속도 {12:0}%, 인벤토리 용량 {4:0}, 중력 +{5:0}%, 스태미나 {6:0}%\n" +
                "<color=#FFD454>! 둥지</color>: 어머니가 탈출한 실험체를 알아보고 적대합니다\n" +
                "<color=#FFD454>! 함께 쓸 수 없음</color>: 장신구 '생체 실험된'\n" +
                "<color=#FFD454>! 리더보드</color>: 이 속박을 낀 판은 기록이 제출되지 않습니다");
            return string.Format(f,
                Plugin.StickyGroundSeconds.Value, Plugin.StickyDuration.Value, Plugin.MutateSeconds.Value, Plugin.Charges.Value,
                Plugin.Capacity.Value, Plugin.Gravity.Value * 100f, Plugin.Stamina.Value * 100f,
                Plugin.StickyNoDrain.Value, Plugin.ShootHold.Value, Plugin.Health.Value * 100f,
                Plugin.TentacleRange.Value, Plugin.TentacleStamina.Value * 100f, Plugin.MoveSpeed.Value * 100f);
        }

        static void Build(WKAssetDatabase db)
        {
            Sprite icon = Assets.LoadIcon("binding_icon.png", db, Perk.PerkType.binding);
            string text = PerkText();

            perk = ScriptableObject.CreateInstance<Perk>();
            perk.name = PerkId;
            perk.hideFlags = HideFlags.DontUnloadUnusedAsset;
            perk.id = PerkId;
            perk.title = Title;
            perk.description = text;
            perk.flavorText = "";
            perk.perkType = Perk.PerkType.binding;
            perk.tags = new List<string> { "binding" };
            perk.flags = new List<string> { HotZoneFlag };
            perk.playerTag = new List<string>();
            perk.spawnPool = Perk.PerkPool.never;
            perk.spawnInEndless = false;
            perk.canStack = false;
            perk.stackMax = 1;
            perk.multiplierCurve = AnimationCurve.Constant(0f, 1f, 0f);
            perk.competitive = true;
            perk.removeOnCleanse = false;
            perk.unlockProgressionID = "";
            perk.icon = icon;
            perk.useBuff = true;
            perk.buff = new BuffContainer
            {
                id = PerkId,
                desc = "",
                buffs = new List<BuffContainer.Buff>
                {
                    MakeBuff("addGravity", Plugin.Gravity.Value),
                    MakeBuff("addStamina", Plugin.Stamina.Value),
                    MakeBuff("addCapacity", Plugin.Capacity.Value),
                    MakeBuff("addSpeed", Plugin.MoveSpeed.Value),
                },
                loseOverTime = false,
                loseRate = 0f,
                buffTime = 1f,
                multiplier = 1f,
            };
            perk.useBaseBuff = false;
            perk.baseBuff = new BuffContainer { id = "", desc = "", buffs = new List<BuffContainer.Buff>() };
            // 손은 평소에는 플레이어의 원래 손. 변이를 시작하면 Infested(핏자국·부글거림)로 바뀐다 (HandSkin.cs)
            perk.modules = new List<PerkModule>();

            binding = ScriptableObject.CreateInstance<Trinket>();
            binding.name = BindingName;
            binding.hideFlags = HideFlags.DontUnloadUnusedAsset;
            binding.title = Title;
            binding.description = PickerText();
            binding.flavorText = "";
            binding.isBinding = true;
            binding.icon = icon;
            binding.lockIcon = icon;
            binding.cost = Plugin.Cost.Value;
            binding.itemsToGrant = new List<Item_Object>();
            binding.perksToGrant = new List<Perk> { perk };
            binding.pouchesToGrant = 0;
            binding.progressionUnlock = null;
            binding.scoreMultiplierBonus = Plugin.ScoreBonus.Value;
            binding.scoreBonus = 0f;
            binding.comingSoon = false;
            binding.settingBlacklist = new List<string>();
            binding.activateOrder = 0;

            Plugin.Log.LogInfo("속박 생성 완료: " + BindingName);
        }

        static BuffContainer.Buff MakeBuff(string id, float amount)
        {
            return new BuffContainer.Buff { id = id, maxAmount = amount, amount = amount };
        }

        public static bool IsActive(ENT_Player p)
        {
            return p != null && p.HasPerk(PerkId);
        }
    }
}
