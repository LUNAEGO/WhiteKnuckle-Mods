using HarmonyLib;

namespace WKBioExperiment
{
    // 게임이 에셋 데이터베이스를 처음 만들 때 장신구를 등록
    [HarmonyPatch(typeof(CL_AssetManager), "InitializeDatabases")]
    static class Patch_InitializeDatabases
    {
        static void Postfix() { BioTrinketFactory.EnsureRegistered(); }
    }

    // 장신구 선택 화면을 채우기 직전에 현재 게임모드 목록에도 들어가 있는지 확인
    [HarmonyPatch(typeof(UI_TrinketPicker), "ReloadTrinkets")]
    static class Patch_ReloadTrinkets
    {
        static void Prefix(UI_TrinketPicker __instance)
        {
            BioTrinketFactory.EnsureRegistered();
            BioTrinketFactory.AddToGamemode(Traverse.Create(__instance).Field("currentGamemode").GetValue<M_Gamemode>());
        }
    }

    // 알약·주사기(HandItem_Buff) 사용 감지. 사용 도중 OnEat이 불리면 그쪽에서 회복을 처리하고,
    // 아이템이 OnEat을 부르지 않는 경우에는 사용이 끝난 뒤 여기서 회복한다.
    [HarmonyPatch(typeof(HandItem_Buff), "StartBuff")]
    static class Patch_MedicalUse
    {
        internal static bool Pending;

        static void Prefix(HandItem_Buff __instance)
        {
            Pending = HungerRecovery.IsMedical(__instance);
        }

        static void Postfix()
        {
            if (Pending)
            {
                Pending = false;
                HungerRecovery.Recover(HungerRecovery.Find(), Plugin.MedicalRecovery.Value);
            }
        }
    }

    // 이 장신구의 배고픔 모듈은 음식 50%, 알약·주사기 100%처럼 종류별로 회복량을 다르게 준다
    [HarmonyPatch(typeof(PerkModule_HungerMeter), "OnConsume")]
    static class Patch_HungerConsume
    {
        static bool Prefix(PerkModule_HungerMeter __instance, string type)
        {
            if (!HungerRecovery.IsOurs(__instance)) return true;
            float amount = 0f;
            if (Patch_MedicalUse.Pending)
            {
                Patch_MedicalUse.Pending = false;
                amount = Plugin.MedicalRecovery.Value;
            }
            else if (__instance.consumeBuffIDs != null && __instance.consumeBuffIDs.Contains(type))
            {
                amount = Plugin.FoodRecovery.Value;
            }
            HungerRecovery.Recover(__instance, amount);
            return false;
        }
    }

    // 저장된 장신구 이름으로 장신구를 찾아 발동하기 전에 등록 보장 (선택 화면을 거치지 않는 재시작 대비)
    [HarmonyPatch(typeof(M_Gamemode), "StartFreshGamemode")]
    static class Patch_StartFreshGamemode
    {
        static void Prefix() { BioTrinketFactory.EnsureRegistered(); }
    }
}
