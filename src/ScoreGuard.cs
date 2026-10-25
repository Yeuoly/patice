using HarmonyLib;
using MAI2.Util;
using Manager;
using Process;

namespace Patice;

// Practice invalidates record updates for this player's current track, not the whole session.
// Keep native judgment/result data intact instead of overwriting private score fields later.
[HarmonyPatch]
public static class ScoreGuard
{
    private static UserData owner;

    public static void MarkPractice(int monitor) => owner = Singleton<UserDataManager>.Instance.GetUserData(monitor);

    [HarmonyPatch(typeof(GameProcess), "OnStart"), HarmonyPrefix]
    private static void BeginTrack() => owner = null;

    [HarmonyPatch(typeof(UserData), "UpdateScore"), HarmonyPrefix]
    private static bool SaveRecord(UserData __instance) => !ReferenceEquals(__instance, owner);
}
