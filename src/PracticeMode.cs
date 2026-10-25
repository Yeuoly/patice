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
    private static bool practiced;
    private static float nextEndReport;

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
            practiced = false;
            nextEndReport = 0;
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
            Player = new Playback(__instance, ____monitors[active], ____gameMovie, active);
            practiced = true;
            ui = ____monitors[active].gameObject.AddComponent<PracticeModeUI>();
            ui.Initialize(active, segments, Player);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[Patice] {exception}");
            Close();
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnUpdate"), HarmonyPostfix]
    private static void ReportEndState(GameMonitor[] ____monitors, int ____sequence)
    {
        if (!practiced || Time.unscaledTime < nextEndReport) return;
        var position = NotesManager.GetCurrentMsec();
        var end = NotesManager.Instance().getPlayFinalMsec();
        if (____sequence < 5 && position < end) return;
        nextEndReport = Time.unscaledTime + 15;
        MelonLogger.Msg($"Practice end: sequence={____sequence}, time={position:F0}/{end:F0}, " +
            $"musicEnded={SoundManager.IsEndMusic()}, blockingEnd={ui != null && ui.PreventGameEnd}");
        for (var i = 0; i < ____monitors.Length; i++)
        {
            var score = Singleton<GamePlayManager>.Instance.GetGameScore(i, -1);
            MelonLogger.Msg($"Practice end P{i + 1}: judged={score.IsAllJudged()}, " +
                $"monitorEnded={____monitors[i].IsAllJudged()}, playEnded={____monitors[i].IsPlayEnd()}");
            if (score.IsAllJudged()) continue;
            var results = Traverse.Create(score).Field<Array>("_judgeResultList").Value;
            var notes = NotesManager.Instance(i).getReader().GetNoteList();
            var pending = 0;
            for (var index = 0; index < results.Length; index++)
            {
                if (Traverse.Create(results.GetValue(index)).Field<bool>("Judged").Value) continue;
                pending++;
                if (pending > 16) continue;
                var note = notes[index];
                MelonLogger.Msg($"Pending note {index}: type={note.type.getEnum()}, " +
                    $"start={note.time.msec}, end={note.end.msec}, used={note.isUsed}, judged={note.isJudged}");
            }
            MelonLogger.Msg($"Pending total: {pending}/{results.Length}");
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
        if (ui == null || !ui.PreventGameEnd) return true;
        __result = false;
        return false;
    }
}
