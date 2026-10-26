using System;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor;
using Process;
using UnityEngine;

namespace Patice.Game;

[HarmonyPatch]
internal static class GameDiagnostics
{
    private static bool practiced;
    private static float nextEndReport;

    internal static void BeginTrack()
    {
        practiced = false;
        nextEndReport = 0;
    }

    internal static void MarkPractice() => practiced = true;

    [HarmonyPatch(typeof(GameProcess), "OnUpdate"), HarmonyPostfix]
    private static void ReportEndState(GameMonitor[] ____monitors, int ____sequence)
    {
        if (!practiced || Time.unscaledTime < nextEndReport) return;
        var position = NotesManager.GetCurrentMsec();
        var end = NotesManager.Instance().getPlayFinalMsec();
        if (____sequence < 5 && position < end) return;
        nextEndReport = Time.unscaledTime + 15;
        MelonLogger.Msg($"Practice end: sequence={____sequence}, time={position:F0}/{end:F0}, " +
            $"musicEnded={SoundManager.IsEndMusic()}, blockingEnd={GameHooks.PreventGameEnd}");
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

}
