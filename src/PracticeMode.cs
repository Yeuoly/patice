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
    private static GameProcess game;

    private static void Close()
    {
        var closing = ui;
        try { if (closing != null) closing.Close(); }
        finally
        {
            if (closing != null) UnityEngine.Object.Destroy(closing);
            ui = null;
            Player = null;
        }
    }

    [HarmonyPatch]
    public static class Lifecycle
    {
        [HarmonyPatch(typeof(GameProcess), "OnStart"), HarmonyPostfix]
        private static void Start(GameProcess __instance)
        {
            Plugin.InstallGameplayHooks();
            Close();
            game = __instance;
            entryHeld = false;
            ScoreGuard.BeginTrack();
            MelonLogger.Msg("Practice input attached to gameplay.");
        }

        [HarmonyPatch(typeof(GameProcess), "OnRelease"), HarmonyPrefix]
        private static void Release()
        {
            game = null;
            entryHeld = false;
            Close();
        }
    }

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
            var difficulty = (Difficulty)GameManager.SelectDifficultyID[active];
            var segments = Plugin.SegmentManagement.Get(GameManager.SelectMusicID[active], difficulty);
            if (segments.Length == 0) return;
            var duration = NotesManager.Instance().getPlayFinalMsec() - Playback.MusicStartDelayMs;
            foreach (var segment in segments)
                if (segment.endMs > duration)
                    throw new InvalidOperationException($"Segment {segment.id} exceeds the chart duration.");
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

    // Consume the Test edge after hardware polling and before GameMain checks it.
    // Keep the held state intact for the practice session's own edge detection.
    [HarmonyPatch(typeof(InputManager), "UpdateAmInput"), HarmonyPostfix]
    private static void EntryInput(bool[] ___SystemButtonDown)
    {
        if (game != null)
            ___SystemButtonDown[(int)InputManager.SystemButtonSetting.ButtonTest] = false;
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
