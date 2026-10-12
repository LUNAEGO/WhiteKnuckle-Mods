using System;
using System.Collections.Generic;
using HarmonyLib;

namespace WKBioExperiment
{
    // 이 장신구를 낀 판은 온라인 리더보드에 제출하지 않는다.
    // 게임은 판이 끝날 때(M_Gamemode.Finish) WK_Leaderboard_Core.disableLeaderboards를 보고 제출을 건너뛰므로,
    // 그 순간에만 켜고 끝나면 원래 값으로 되돌린다 (다른 모드가 켜 둔 값은 건드리지 않음)
    [HarmonyPatch(typeof(M_Gamemode), "Finish")]
    static class Patch_Finish_NoLeaderboard
    {
        static void Prefix(M_Gamemode __instance, out bool __state)
        {
            __state = CL_Leaderboard.WK_Leaderboard_Core.disableLeaderboards;
            if (!UsedThisRun(__instance)) return;
            CL_Leaderboard.WK_Leaderboard_Core.disableLeaderboards = true;
            Plugin.Log.LogInfo("Biologically Experimented was used this run: leaderboard submission skipped");
        }

        static Exception Finalizer(Exception __exception, bool __state)
        {
            CL_Leaderboard.WK_Leaderboard_Core.disableLeaderboards = __state;
            return __exception;
        }

        static bool UsedThisRun(M_Gamemode gm)
        {
            try
            {
                ENT_Player p = ENT_Player.GetPlayer();
                if (p != null && p.HasPerk(BioTrinketFactory.PerkId)) return true;
            }
            catch (Exception) { }
            try
            {
                // 판을 시작할 때 고른 장신구 목록 (플레이어가 이미 사라졌어도 확인 가능)
                List<string> list = StatManager.saveData.GetGamemodeTrinkets(gm.GetGamemodeName());
                if (list != null && list.Exists(n => n != null && n.Contains(BioTrinketFactory.TrinketName))) return true;
            }
            catch (Exception) { }
            return false;
        }
    }
}
