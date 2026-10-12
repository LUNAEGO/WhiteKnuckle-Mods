using System;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WKBioExperiment
{
    internal static class HungerRecovery
    {
        // 현재 플레이어가 가진 이 장신구의 배고픔 모듈 (없으면 null)
        public static PerkModule_HungerMeter Find()
        {
            try
            {
                ENT_Player player = ENT_Player.GetPlayer();
                if (player == null) return null;
                Perk perk = player.GetPerk(BioTrinketFactory.PerkId);
                if (perk == null || perk.modules == null) return null;
                foreach (PerkModule m in perk.modules)
                {
                    var h = m as PerkModule_HungerMeter;
                    if (h != null) return h;
                }
            }
            catch (Exception) { }
            return null;
        }

        public static bool IsOurs(PerkModule_HungerMeter m)
        {
            Perk p = Traverse.Create(m).Field("perk").GetValue<Perk>();
            return p != null && p.id == BioTrinketFactory.PerkId;
        }

        public static bool IsMedical(HandItem_Buff h)
        {
            string names = h.gameObject.name;
            if (h.item != null) names += "|" + h.item.prefabName + "|" + h.item.itemName;
            foreach (string k in Plugin.MedicalKeywords.Value.Split(','))
            {
                string key = k.Trim();
                if (key.Length > 0 && names.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static void Recover(PerkModule_HungerMeter m, float amount)
        {
            if (m == null || amount <= 0f) return;
            m.hungerMeter = Mathf.Clamp(m.hungerMeter + amount * m.hungerMax, 0f, m.hungerMax);
            // 게임 기본 OnConsume과 같은 미터 흔들림 연출
            Image meter = Traverse.Create(m).Field("hungerMeterObject").GetValue<Image>();
            if (meter != null)
            {
                DOTween.Complete(meter.transform);
                meter.transform.DOPunchScale(Vector3.one * 0.15f, 2f, 5, 0.1f);
                meter.transform.DOPunchRotation(Vector3.forward * 10f, 1f, 5);
            }
        }
    }
}
