using System;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Patice.Domain;
using Patice.Practice;
using Process;
using UnityEngine;

namespace Patice.Game;

[HarmonyPatch]
public static class GameHooks
{
    private static PracticeDriver driver;
    internal static GamePlayback Playback { get; private set; }
    internal static bool PreventGameEnd => driver != null && driver.Controller.PreventGameEnd;
    private static bool entryHeld;
    private static GameProcess game;

    internal static void Close()
    {
        var closing = driver;
        try { if (closing != null) closing.Close(); }
        finally
        {
            if (closing != null) UnityEngine.Object.Destroy(closing);
            driver = null;
            Playback = null;
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
            GameDiagnostics.BeginTrack();
            MelonLogger.Msg("Practice input attached to gameplay.");
        }

        [HarmonyPatch(typeof(GameProcess), "OnRelease"), HarmonyPrefix]
        private static void Release()
        {
            game = null;
            entryHeld = false;
            Close();
        }

        [HarmonyPatch(typeof(GameProcess), "SetRelease"), HarmonyPrefix]
        private static void Finish()
        {
            // Release playback before the game stops its clock and finalizes scores.
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
            if (driver != null)
            {
                if (entry) driver.Controller.Entry();
                driver.Step();
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
            Playback = new GamePlayback(__instance, ____monitors[active], ____gameMovie, active);
            GameDiagnostics.MarkPractice();
            driver = ____monitors[active].gameObject.AddComponent<PracticeDriver>();
            driver.Initialize(active, new PracticeController(Playback, segments));
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

    [HarmonyPatch(typeof(GameMonitor), "IsAllJudged"), HarmonyPrefix]
    private static bool SegmentEnd(ref bool __result)
    {
        if (!PreventGameEnd) return true;
        __result = false;
        return false;
    }
}
