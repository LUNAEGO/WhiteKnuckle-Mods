using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WKBioExperiment
{
    // 장신구와 장신구가 부여하는 전용 퍽을 런타임에 만들어 게임 데이터베이스에 등록한다.
    internal static class BioTrinketFactory
    {
        public const string TrinketName = "Trinket_BioExperimented";
        public const string PerkId = "Perk_BioExperimented";
        const string BuffId = "bioExperimented";
        const string DebuffId = "bioExperimented_debuff";

        static Trinket trinket;
        static Perk perk;
        static Sprite icon;
        static Image fallbackMeter;

        public static Trinket Trinket { get { return trinket; } }

        // 데이터베이스가 준비됐으면 장신구를 만들고, 게임모드 장신구 목록에 끼워 넣는다. 여러 번 불려도 안전.
        public static void EnsureRegistered()
        {
            var databases = Traverse.Create(typeof(CL_AssetManager)).Field("databases").GetValue<System.Collections.IList>();
            if (databases == null || databases.Count == 0) return;
            WKAssetDatabase db = Traverse.Create(databases[0]).Field("database").GetValue<WKAssetDatabase>();
            if (db == null) return;

            if (trinket == null) Build(db);

            if (db.perkAssets != null && !db.perkAssets.Contains(perk)) db.perkAssets.Add(perk);
            if (db.trinketAssets != null && !db.trinketAssets.Contains(trinket)) db.trinketAssets.Add(trinket);

            if (db.gamemodeAssets != null)
                foreach (M_Gamemode gm in db.gamemodeAssets)
                    AddToGamemode(gm);
        }

        public static void AddToGamemode(M_Gamemode gm)
        {
            if (gm == null || gm.availableTrinkets == null || trinket == null) return;
            List<Trinket> list = gm.availableTrinkets.trinkets;
            if (list != null && !list.Contains(trinket)) list.Add(trinket);
        }

        internal static string Title { get { return Plugin.L("Biologically Experimented", "생체 실험된"); } }

        static string Lore
        {
            get
            {
                return Plugin.L(
                    "Traces of those who willingly gave up their bodies to break past their limits. Drugs coursing through the veins push the body beyond its limits. As long as there's enough food to fuel the metabolism, that is.",
                    "한계를 돌파하기 위해 기꺼이 육체를 포기한 자들의 흔적입니다. 혈관을 타고 흐르는 약물이 신체를 한계 이상으로 가속시킵니다. 대사량을 감당할 식량만 충분하다면 말이죠.");
            }
        }

        // 설명문 아래 붙는 능력치 요약. 퍽 설명은 [ ]를 지워 버리므로 대괄호 대신 색 태그를 쓴다.
        static string Summary()
        {
            string f = Plugin.L(
                "<color=#7CFF7C>+ Boost</color>: stamina +{0:0}%, climb speed +{1:0}%, gravity {2:0}%, extra jump +{3}, inventory capacity +{4}\n" +
                "<color=#FF6B6B>- Weakness</color>: score {5:0}%\n" +
                "<color=#FFD454>! Side effect</color>: hunger constantly drains. Food restores {6:0}%, pills and injectors {7:0}%. " +
                "At {8:0}% hunger or below all mobility abilities shut down, and at 0% your vision fades (gray screen) while your health drains until death.\n" +
                "<color=#FFD454>! Leaderboards</color>: runs with this trinket are not submitted",
                "<color=#7CFF7C>+ 강화</color>: 스태미나 +{0:0}%, 오르기 속도 +{1:0}%, 중력 {2:0}%, 추가 점프 +{3}, 인벤토리 용량 +{4}\n" +
                "<color=#FF6B6B>- 약화</color>: 획득 점수 {5:0}%\n" +
                "<color=#FFD454>! 부작용</color>: 배고픔이 계속 줄어듭니다. 음식 {6:0}%, 알약·주사기 {7:0}% 회복. " +
                "배고픔 {8:0}% 이하에서 모든 기동성 능력이 차단되고, 0%에 도달하면 시야 상실(회색 화면)과 함께 체력이 닳아 사망합니다.\n" +
                "<color=#FFD454>! 리더보드</color>: 이 장신구를 낀 판은 기록이 제출되지 않습니다");
            return string.Format(f,
                Plugin.Stamina.Value * 100f, Plugin.Climb.Value * 100f, Plugin.Gravity.Value * 100f,
                Plugin.ExtraJumps.Value, Plugin.Pouches.Value, Plugin.ScoreMultiplier.Value * 100f,
                Plugin.FoodRecovery.Value * 100f, Plugin.MedicalRecovery.Value * 100f, Plugin.MobilityCutoff.Value * 100f);
        }

        // 장신구 & 속박 선택 화면용 짧은 설명 (설명 칸이 4줄 정도라 긴 설명은 잘린다. 전체 설명은 퍽 설명에 남김)
        static string PickerText()
        {
            string f = Plugin.L(
                "Traces of those who gave up their bodies to break their limits. Drugs drive the body on, if you can keep it fed. <color=#FFD454>(No leaderboards)</color>\n" +
                "<color=#7CFF7C>+ Boost</color>: stamina +{0:0}%, climb speed +{1:0}%, gravity {2:0}%, extra jump +{3}, inventory capacity +{4}\n" +
                "<color=#FF6B6B>- Weakness</color>: score {5:0}%\n" +
                "<color=#FFD454>! Side effect</color>: hunger constantly drains (food {6:0}%, pills/injectors {7:0}%). Mobility shuts down at {8:0}% or below, gray screen and health loss at 0%",
                "한계를 넘기 위해 육체를 포기한 자들의 흔적. 약물이 신체를 가속시킵니다, 식량만 충분하다면요. <color=#FFD454>(리더보드 제외)</color>\n" +
                "<color=#7CFF7C>+ 강화</color>: 스태미나 +{0:0}%, 오르기 속도 +{1:0}%, 중력 {2:0}%, 추가 점프 +{3}, 인벤토리 용량 +{4}\n" +
                "<color=#FF6B6B>- 약화</color>: 획득 점수 {5:0}%\n" +
                "<color=#FFD454>! 부작용</color>: 배고픔이 계속 줄어듦 (음식 {6:0}%, 알약·주사기 {7:0}% 회복). {8:0}% 이하에서 기동성 차단, 0%면 회색 화면과 함께 체력 감소");
            return string.Format(f,
                Plugin.Stamina.Value * 100f, Plugin.Climb.Value * 100f, Plugin.Gravity.Value * 100f,
                Plugin.ExtraJumps.Value, Plugin.Pouches.Value, Plugin.ScoreMultiplier.Value * 100f,
                Plugin.FoodRecovery.Value * 100f, Plugin.MedicalRecovery.Value * 100f, Plugin.MobilityCutoff.Value * 100f);
        }

        static void Build(WKAssetDatabase db)
        {
            icon = LoadIcon(db) ?? MakeIcon();
            string text = Lore + "\n" + Summary();

            perk = ScriptableObject.CreateInstance<Perk>();
            perk.name = PerkId;
            perk.hideFlags = HideFlags.DontUnloadUnusedAsset;
            perk.id = PerkId;
            perk.title = Title;
            perk.description = text;
            perk.flavorText = "";
            perk.perkType = Perk.PerkType.trinket;
            perk.tags = new List<string>();
            perk.flags = new List<string>();
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
            // 기동성 버프는 배고픔 모듈이 들고 있어야 0%에서 꺼낼 수 있으므로 퍽 자체 버프는 쓰지 않는다.
            perk.useBuff = false;
            perk.buff = new BuffContainer { id = BuffId + "_perk", desc = "", buffs = new List<BuffContainer.Buff>() };
            perk.useBaseBuff = false;
            perk.baseBuff = new BuffContainer { id = "", desc = "", buffs = new List<BuffContainer.Buff>() };
            perk.modules = new List<PerkModule> { MakeHungerModule(db) };

            trinket = ScriptableObject.CreateInstance<Trinket>();
            trinket.name = TrinketName;
            trinket.hideFlags = HideFlags.DontUnloadUnusedAsset;
            trinket.title = Title;
            trinket.description = PickerText();
            trinket.flavorText = "";
            trinket.isBinding = false;
            trinket.icon = icon;
            trinket.lockIcon = icon;
            trinket.cost = Plugin.Cost.Value;
            trinket.itemsToGrant = new List<Item_Object>();
            trinket.perksToGrant = new List<Perk> { perk };
            trinket.pouchesToGrant = Plugin.Pouches.Value;
            trinket.progressionUnlock = null; // 처음부터 해금
            trinket.scoreMultiplierBonus = Plugin.ScoreMultiplier.Value;
            trinket.scoreBonus = 0f;
            trinket.comingSoon = false;
            trinket.settingBlacklist = new List<string>();
            trinket.activateOrder = 0;

            Plugin.Log.LogInfo("장신구 생성 완료: " + TrinketName);
        }

        // 게임에 이미 있는 배고픔 모듈(PerkModule_HungerMeter)을 그대로 써서 UI·소리·음식 회복·굶주림 피해를 재사용한다.
        static PerkModule_HungerMeter MakeHungerModule(WKAssetDatabase db)
        {
            PerkModule_HungerMeter template = null;
            if (db.perkAssets != null)
            {
                foreach (Perk p in db.perkAssets)
                {
                    if (p == null || p.modules == null) continue;
                    foreach (PerkModule m in p.modules)
                    {
                        var h = m as PerkModule_HungerMeter;
                        if (h != null && h.hungerMeterAsset != null) { template = h; break; }
                    }
                    if (template != null)
                    {
                        Plugin.Log.LogInfo("배고픔 미터를 기존 퍽에서 가져옴: " + p.id);
                        break;
                    }
                }
            }

            var mod = new PerkModule_HungerMeter();
            mod.name = "BioHunger";
            if (template != null)
            {
                mod.hungerMeterAsset = template.hungerMeterAsset;
                mod.hungerTickAudio = template.hungerTickAudio;
                mod.consumeBuffIDs = new List<string> { "food" };
                mod.fullColor = template.fullColor;
                mod.emptyColor = template.emptyColor;
            }
            else
            {
                Plugin.Log.LogWarning("기존 배고픔 미터를 찾지 못해 대체 미터를 사용함");
                mod.hungerMeterAsset = FallbackMeter();
                mod.hungerTickAudio = new List<AudioClip>();
                mod.consumeBuffIDs = new List<string> { "food" };
            }
            mod.hungerMax = 1f;
            mod.hungerMeter = 1f;
            mod.hungerDecayRate = 1f / Mathf.Max(1f, Plugin.SecondsToEmpty.Value);
            mod.eatRecovery = Plugin.FoodRecovery.Value; // 실제 회복량은 Patch_HungerConsume에서 아이템 종류별로 처리

            // 기동성 버프: 배고픔이 차단선보다 높으면 배율 1, 이하가 되면 배율 0 (buffCurve는 1 - 배고픔비율로 평가됨)
            mod.buff = new BuffContainer
            {
                id = BuffId,
                desc = "",
                buffs = new List<BuffContainer.Buff>
                {
                    MakeBuff("addStamina", Plugin.Stamina.Value),
                    MakeBuff("addClimb", Plugin.Climb.Value),
                    MakeBuff("addExtraJumps", Plugin.ExtraJumps.Value),
                    MakeBuff("addGravity", Plugin.Gravity.Value),
                },
                loseOverTime = false,
                loseRate = 0f,
                buffTime = 1f,
                multiplier = 1f,
            };
            float off = 1f - Mathf.Clamp(Plugin.MobilityCutoff.Value, 0f, 0.99f);
            mod.buffCurve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(off - 0.001f, 1f, 0f, 0f),
                new Keyframe(off, 0f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
            // 원래 퍽의 부가 디버프는 쓰지 않는다
            mod.debuff = new BuffContainer
            {
                id = DebuffId,
                desc = "",
                buffs = new List<BuffContainer.Buff>(),
                loseOverTime = false,
                loseRate = 0f,
                buffTime = 1f,
                multiplier = 0f,
            };
            mod.debuffCurve = AnimationCurve.Constant(0f, 1f, 0f);
            return mod;
        }

        static BuffContainer.Buff MakeBuff(string id, float amount)
        {
            return new BuffContainer.Buff { id = id, maxAmount = amount, amount = amount };
        }

        // 기존 미터가 없을 때 쓰는 단순한 미터 (복제될 원본이라 비활성 부모 아래에 둔다)
        static Image FallbackMeter()
        {
            if (fallbackMeter != null) return fallbackMeter;
            var holder = new GameObject("WKBio_MeterTemplateHolder");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            var go = new GameObject("WKBio_HungerMeter", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(holder.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(32f, 32f);
            fallbackMeter = go.GetComponent<Image>();
            fallbackMeter.sprite = icon;
            fallbackMeter.preserveAspect = true;
            return fallbackMeter;
        }

        // 플러그인 폴더의 PNG를 아이콘으로 쓴다. 인벤토리(월드 공간)에 기존 장신구와 같은 크기로 보이도록
        // 기존 장신구 퍽 아이콘의 월드 크기에 맞춰 pixelsPerUnit을 정한다.
        static Sprite LoadIcon(WKAssetDatabase db)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(BioTrinketFactory).Assembly.Location);
                string path = System.IO.Path.Combine(dir, IconFile);
                if (!System.IO.File.Exists(path))
                {
                    Plugin.Log.LogWarning("아이콘 파일이 없어 기본 아이콘을 사용함: " + path);
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                // Unity 6의 LoadImage에는 ReadOnlySpan 오버로드가 섞여 있어 net472로는 직접 호출이 안 되므로 byte[] 버전을 리플렉션으로 부른다
                var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                if (!(bool)load.Invoke(null, new object[] { tex, System.IO.File.ReadAllBytes(path), false })) return null;
                tex.filterMode = FilterMode.Trilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.anisoLevel = 4;
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

                float worldSize = ReferenceIconWorldSize(db);
                float ppu = Mathf.Max(tex.width, tex.height) / worldSize;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                sprite.name = "BioExperimented_Icon";
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Plugin.Log.LogInfo(string.Format("아이콘 로드: {0}x{1}, 월드 크기 {2:0.###}", tex.width, tex.height, worldSize));
                return sprite;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("아이콘 로드 실패: " + e.Message);
                return null;
            }
        }

        const string IconFile = "Biologically Experimented.png";

        // 기존 장신구 퍽 아이콘들의 월드 크기(가로·세로 중 큰 쪽) 중앙값
        static float ReferenceIconWorldSize(WKAssetDatabase db)
        {
            var sizes = new List<float>();
            if (db.perkAssets != null)
            {
                foreach (Perk p in db.perkAssets)
                {
                    if (p == null || p.icon == null || p == perk || p.perkType != Perk.PerkType.trinket) continue;
                    Rect r = p.icon.rect;
                    sizes.Add(Mathf.Max(r.width, r.height) / p.icon.pixelsPerUnit);
                }
            }
            if (sizes.Count == 0) return 1f;
            sizes.Sort();
            return sizes[sizes.Count / 2];
        }

        // 32x32 픽셀 아이콘: 초록 실험액이 든 플라스크 (PNG가 없을 때 대체용)
        static Sprite MakeIcon()
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(0, 0, 0, 0);
            var outline = new Color(0.08f, 0.06f, 0.08f, 1f);
            var glass = new Color(0.78f, 0.86f, 0.88f, 1f);
            var liquid = new Color(0.45f, 0.95f, 0.35f, 1f);
            var liquidDark = new Color(0.22f, 0.6f, 0.2f, 1f);
            var bubble = new Color(0.85f, 1f, 0.8f, 1f);
            var vein = new Color(0.85f, 0.15f, 0.25f, 1f);

            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // 둥근 몸통(중심 16,11 반지름 10)과 목(14~17, 20~28)
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - 16f, dy = y + 0.5f - 11f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool body = d <= 10f;
                    bool neck = x >= 13 && x <= 18 && y >= 18 && y <= 28;
                    bool lip = x >= 11 && x <= 20 && y >= 27 && y <= 29;
                    if (!(body || neck || lip)) continue;
                    bool edge = (body && d > 9f) || (neck && !body && (x == 13 || x == 18)) || (lip && (y == 27 || y == 29 || x == 11 || x == 20));
                    Color c;
                    if (edge) c = outline;
                    else if (body && y <= 13) c = (y <= 5 || dx > 5f) ? liquidDark : liquid;
                    else c = glass;
                    px[y * S + x] = c;
                }
            }
            // 거품
            int[,] bubbles = { { 12, 8 }, { 18, 10 }, { 15, 5 }, { 20, 7 } };
            for (int i = 0; i < bubbles.GetLength(0); i++) px[bubbles[i, 1] * S + bubbles[i, 0]] = bubble;
            // 액체 위로 뻗은 붉은 혈관 (생체 실험 느낌)
            int[,] veins = { { 10, 14 }, { 9, 15 }, { 9, 16 }, { 22, 14 }, { 23, 15 }, { 22, 16 }, { 16, 14 }, { 16, 15 }, { 17, 16 } };
            for (int i = 0; i < veins.GetLength(0); i++) px[veins[i, 1] * S + veins[i, 0]] = vein;

            tex.SetPixels(px);
            tex.Apply();
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            sprite.name = "BioExperimented_Icon";
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
