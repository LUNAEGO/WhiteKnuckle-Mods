using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WKEscapedSubject
{
    [HarmonyPatch(typeof(CL_AssetManager), "InitializeDatabases")]
    static class Patch_InitializeDatabases
    {
        static void Postfix() { BindingFactory.EnsureRegistered(); }
    }

    [HarmonyPatch(typeof(UI_TrinketPicker), "ReloadTrinkets")]
    static class Patch_ReloadTrinkets
    {
        static void Prefix(UI_TrinketPicker __instance)
        {
            BindingFactory.EnsureRegistered();
            BindingFactory.AddToGamemode(Traverse.Create(__instance).Field("currentGamemode").GetValue<M_Gamemode>());
        }

        // 속박이 9개를 넘으면 칸 밖으로 밀려나므로 3×3 영역 안에 맞춘다
        static void Postfix(UI_TrinketPicker __instance) { PickerLayout.FitBindings(__instance); }
    }

    // 긴 설명이 잘리지 않게 글자 크기 자동 조절
    [HarmonyPatch(typeof(UI_TrinketPicker), "ShowTrinketDescription")]
    static class Patch_ShowTrinketDescription
    {
        static void Prefix(UI_TrinketPicker __instance, Trinket t) { PickerLayout.OnShowDescription(__instance, t); }
    }

    // '생체 실험된' 장신구와 같이 고를 수 없다: 한쪽을 고르면 다른 쪽 선택을 해제
    [HarmonyPatch(typeof(UI_TrinketPicker), "SelectTrinket")]
    static class Patch_SelectTrinket
    {
        static void Prefix(UI_TrinketPicker __instance, Trinket t)
        {
            if (t == null || __instance.selectedTrinkets == null) return;
            string other = t.name == BindingFactory.BindingName ? BindingFactory.BioTrinketName
                         : t.name == BindingFactory.BioTrinketName ? BindingFactory.BindingName : null;
            if (other == null || __instance.selectedTrinkets.Contains(t)) return; // 해제하는 경우는 그대로
            int removed = __instance.selectedTrinkets.RemoveAll(x => x != null && x.name == other);
            if (removed > 0) Plugin.Log.LogInfo("함께 쓸 수 없어 선택 해제: " + other);
        }
    }

    // 시작 직전에 저장된 선택 목록에 둘 다 있으면 장신구 쪽을 뺀다 (선택 화면을 거치지 않은 경우 대비)
    [HarmonyPatch(typeof(M_Gamemode), "StartFreshGamemode")]
    static class Patch_StartFreshGamemode
    {
        static void Prefix(M_Gamemode __instance)
        {
            BindingFactory.EnsureRegistered();
            MutationController.ResetRun();
            try
            {
                string mode = __instance.GetGamemodeName();
                List<string> list = StatManager.saveData.GetGamemodeTrinkets(mode);
                if (list == null) return;
                bool hasBinding = list.Exists(n => n != null && n.Contains(BindingFactory.BindingName));
                if (hasBinding && list.RemoveAll(n => n != null && n.Contains(BindingFactory.BioTrinketName)) > 0)
                {
                    StatManager.saveData.SetGamemodeTrinkets(mode, list);
                    Plugin.Log.LogInfo("속박과 함께 쓸 수 없어 '생체 실험된'을 이번 판에서 제외");
                }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("선택 목록 확인 실패: " + e.Message); }
        }
    }

    // 촉수 때문에 켠 '어디든 잡기'는 촉수를 쏜 손으로, 촉수가 박힌 지점 근처에서만 통하게 한다 (다른 손이나 끌려가는 길의 다른 벽은 못 잡음).
    // 굼벵이 효과(grabAnything)가 따로 걸려 있으면 원래대로 어디든 잡을 수 있다
    [HarmonyPatch(typeof(ENT_Player), "GrabAnywhere")]
    static class Patch_GrabAnywhere
    {
        static bool Prefix(ENT_Player __instance, ENT_Player.Hand curhand, ref InteractHit interactHit, ref bool __result)
        {
            TentacleGrapple g = TentacleGrapple.Active;
            if (g == null || !g.WallGrabOn || __instance.curBuffs.GetBuff("grabAnything") > 0f) return true;
            // 촉수를 쏜 손으로, 박힌 지점 근처만 잡을 수 있다
            if (System.Array.IndexOf(__instance.hands, curhand) == g.WallGrabHand && Vector3.Distance(interactHit.point, g.AnchorPoint) <= Plugin.AnchorGrabRadius.Value) return true;
            // 원래 함수의 '잡을 수 없음' 처리와 같게
            __instance.grabAnywhereFakeHandholds[curhand.id].gameObject.SetActive(false);
            __result = false;
            return false;
        }
    }

    // 굼벵이·알약·주사기·Lemon 로치 섭취 → 변이 충전
    [HarmonyPatch(typeof(HandItem_Buff), "StartBuff")]
    static class Patch_StartBuff
    {
        static void Prefix(HandItem_Buff __instance) { MutationController.OnConsume(__instance); }
    }
}
