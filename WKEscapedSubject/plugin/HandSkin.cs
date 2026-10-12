using System.Collections.Generic;
using HarmonyLib;

namespace WKEscapedSubject
{
    // 변이 중·촉수 상태에서만 양손을 Infested(핏자국·부글거림) 코스메틱으로 바꾸고,
    // 끝나면 플레이어가 원래 쓰던 손 코스메틱으로 되돌린다
    internal static class HandSkin
    {
        const string CosmeticId = "Infested";
        static ENT_Player appliedTo;
        static readonly List<Cosmetic_HandItem>[] original = new List<Cosmetic_HandItem>[2];
        static Cosmetic_HandItem infested;

        static Cosmetic_HandItem Infested()
        {
            if (infested != null) return infested;
            var dict = Traverse.Create(typeof(CL_CosmeticManager)).Field("cosmeticHandDict").GetValue<Dictionary<string, Cosmetic_HandItem>>();
            if (dict != null) dict.TryGetValue(CosmeticId, out infested);
            if (infested == null) Plugin.Log.LogWarning("Infested 손 코스메틱을 찾지 못함");
            return infested;
        }

        public static void Set(ENT_Player player, bool on)
        {
            if (on)
            {
                if (appliedTo == player || player == null) return;
                if (appliedTo != null) Restore();
                Cosmetic_HandItem inf = Infested();
                if (inf == null) return;
                for (int i = 0; i < player.hands.Length && i < 2; i++)
                {
                    ENT_Player.Hand h = player.hands[i];
                    if (h.currentCosmetics == null) h.currentCosmetics = new List<Cosmetic_HandItem>();
                    original[i] = new List<Cosmetic_HandItem>(h.currentCosmetics);
                    foreach (var c in h.currentCosmetics) c.ClearCosmetic(); // 원래 손 그림으로 되돌린 뒤 교체
                    h.currentCosmetics.Clear();
                    h.currentCosmetics.Add(inf);
                }
                appliedTo = player;
            }
            else if (appliedTo != null)
            {
                Restore();
            }
        }

        static void Restore()
        {
            ENT_Player p = appliedTo;
            appliedTo = null;
            if (p == null) return; // 레벨이 바뀌어 플레이어가 사라졌으면 새 플레이어는 원래 손으로 시작함
            if (infested != null) infested.ClearCosmetic();
            for (int i = 0; i < p.hands.Length && i < 2; i++)
            {
                ENT_Player.Hand h = p.hands[i];
                if (h.currentCosmetics == null) continue;
                h.currentCosmetics.Clear();
                if (original[i] != null) h.currentCosmetics.AddRange(original[i]);
                original[i] = null;
            }
        }
    }
}
