using System;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Patice.Domain;
using Patice.Libs;
using Process;
using UnityEngine;

namespace Patice;

[HarmonyPatch]
public static class PracticeMode
{
    public static PracticeModeUI ui;
    public static Playback Player { get; private set; }
    private static bool entryHeld;

    private static void Close()
    {
        var closing = ui;
        try { if (closing != null) closing.Close(); }
        finally
        {
            if (closing != null) UnityEngine.Object.Destroy(closing);
            ui = null;
            Player = null;
            entryHeld = false;
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart"), HarmonyPrefix]
    private static void Start() => Close();

    [HarmonyPatch(typeof(GameProcess), "OnRelease"), HarmonyPrefix]
    private static void Release() => Close();

    [HarmonyPatch(typeof(GameProcess), "OnUpdate"), HarmonyPrefix]
    private static void Update(GameProcess __instance, GameMonitor[] ____monitors, MovieController ____gameMovie, int ____sequence)
    {
        if (____sequence != 4) return;
        try
        {
            var held = InputManager.GetSystemInputPush(InputManager.SystemButtonSetting.ButtonTest);
            var entry = held && !entryHeld;
            entryHeld = held;
            if (ui != null)
            {
                if (entry) ui.Entry();
                ui.Step();
                return;
            }
            if (!entry) return;
            if (Traverse.Create(__instance).Method("IsPartyPlay").GetValue<bool>())
                throw new InvalidOperationException("Practice requires local play; a linked game shares progress with other machines.");
            var active = -1;
            for (int i = 0; i < ____monitors.Length; i++)
            {
                if (!Singleton<UserDataManager>.Instance.GetUserData(i).IsEntry) continue;
                if (active >= 0) throw new InvalidOperationException("Practice requires one active player: the game has one shared playback clock.");
                active = i;
            }
            if (active < 0) return;
            var segments = SegmentDocument.Load(active).segments;
            Player = new Playback(__instance, ____monitors[active], ____gameMovie, active);
            ui = ____monitors[active].gameObject.AddComponent<PracticeModeUI>();
            ui.Initialize(active, segments, Player);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[Patice] {exception}");
            Close();
        }
    }

    [HarmonyPatch(typeof(InputManager), "GetSystemInputDown"), HarmonyPrefix]
    private static bool EntryInput(InputManager.SystemButtonSetting button, ref bool __result)
    {
        if (!GameManager.IsInGame || button != InputManager.SystemButtonSetting.ButtonTest) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(SoundManager), "Play"), HarmonyPrefix]
    private static bool PreviewSound(int __0, int __1, int __2) =>
        Player?.Previewing != true || __1 == 2 || (__0 == 0 && __2 == (int)Mai2.Mai2Cue.Cue.SE_SYS_CURSOR);

    [HarmonyPatch(typeof(GameMonitor), "IsAllJudged"), HarmonyPrefix]
    private static bool SegmentEnd(ref bool __result)
    {
        if (ui == null || !ui.PreventGameEnd) return true;
        __result = false;
        return false;
    }
}
