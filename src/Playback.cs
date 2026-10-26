using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using HarmonyLib;
using DB;
using MAI2.Util;
using Manager;
using Monitor;
using Monitor.Game;
using Process;

namespace Patice;

[HarmonyPatch]
public sealed class Playback
{
    // SDEZ 1.70 GameProcess starts the note clock, then waits 91ms before StartMusic.
    public const float MusicStartDelayMs = 91;
    private readonly GameProcess process;
    private readonly GameMonitor monitor;
    private readonly MovieController movie;
    private readonly int monitorIndex;
    private readonly CriAtomExPlayer music;
    private readonly MovieMaterialMai2 moviePlayer;
    private double nativeAnchor, chartAnchor, lastNativeTime;
    private double entryPosition;
    private bool entryPaused, attached, mediaRatePending;
    private GameManager.AutoPlayMode entryAutoplay;
    public bool Previewing { get; private set; }
    public float Rate { get; private set; } = 1;
    public double EntryPositionMs => entryPosition;
    public double DurationMs => NotesManager.Instance().getPlayFinalMsec() - MusicStartDelayMs;
    public double PositionMs => NotesManager.GetCurrentMsec() - MusicStartDelayMs;
    public bool Paused => !(Traverse.Create(typeof(NotesManager)).Field<Stopwatch>("_stopwatch").Value.IsRunning);

    public Playback(GameProcess process, GameMonitor monitor, MovieController movie, int monitorIndex)
    {
        this.process = process;
        this.monitor = monitor;
        this.movie = movie;
        this.monitorIndex = monitorIndex;
        var players = Traverse.Create(Singleton<SoundCtrl>.Instance).Field<IDictionary>("_players").Value;
        music = Traverse.Create(players[2]).Field<CriAtomExPlayer>("Player").Value
            ?? throw new InvalidOperationException("Music player is unavailable.");
        moviePlayer = Traverse.Create(movie).Field<List<MovieMaterialMai2>>("_moviePlayers").Value[monitorIndex];
    }

    public void Capture()
    {
        NotesManager.UpdateTimer();
        entryPosition = PositionMs;
        entryPaused = Paused;
        entryAutoplay = GameManager.AutoPlay;
        Pause(true);
        nativeAnchor = chartAnchor = NotesManager.GetCurrentMsec();
        lastNativeTime = nativeAnchor;
        attached = true;
        ScoreGuard.MarkPractice(monitorIndex);
    }

    public void Preview(bool enabled)
    {
        Previewing = enabled;
        GameManager.AutoPlay = enabled ? GameManager.AutoPlayMode.Critical : GameManager.AutoPlayMode.None;
        SoundManager.PauseMusic(Paused);
    }

    public void Pause(bool paused)
    {
        NotesManager.Pause(paused);
        movie.Pause(monitorIndex, paused);
        SoundManager.PauseMusic(paused);
    }

    public void SetRate(float rate)
    {
        NotesManager.UpdateTimer();
        chartAnchor = NotesManager.GetCurrentMsec();
        nativeAnchor = lastNativeTime;
        Rate = rate;
        ApplyMediaRate();
    }

    public void Seek(double milliseconds)
    {
        var paused = Paused;
        Pause(true);
        try
        {
            var engineTime = (float)Math.Max(0, milliseconds) + MusicStartDelayMs;
            Singleton<GamePlayManager>.Instance.Initialize();
            movie.SetSeekFrame(monitorIndex, milliseconds);
            SoundManager.SeekMusic((int)milliseconds);
            monitor.Seek((int)milliseconds);
            nativeAnchor = chartAnchor = lastNativeTime = engineTime;
            NotesManager.StartPlay(engineTime);
            NotesManager.Pause(paused);
            process.UpdateNotes();
            mediaRatePending = true;
        }
        finally { Pause(paused); }
    }

    public void Restore(bool rewind)
    {
        if (!attached) return;
        try
        {
            Preview(false);
            if (rewind) Seek(entryPosition);
        }
        finally
        {
            Previewing = false;
            GameManager.AutoPlay = entryAutoplay;
            // Rebase the original clock at the current chart position before releasing it.
            var position = NotesManager.GetCurrentMsec();
            attached = false;
            Rate = 1;
            try { ApplyMediaRate(); }
            finally
            {
                NotesManager.StartPlay(position);
                NotesManager.Pause(true);
                NotesManager.UpdateTimer();
                Pause(rewind && entryPaused);
            }
        }
    }

    private void ApplyMediaRate()
    {
        music.SetPitch((float)(1200 * Math.Log(Rate, 2)));
        music.UpdateAll();
        moviePlayer?.player?.SetSpeed(Rate);
        mediaRatePending = false;
    }

    [HarmonyPatch(typeof(GameCtrl), "UpdateCtrl"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PreserveSkipResult(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        // The debug seek branch discards failure and marks the note used anyway.
        // Preserve the result, as the native track-skip branch already does.
        var result = generator.DeclareLocal(typeof(bool));
        var matcher = new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameCtrl), "SkipRegistNote")),
            new CodeMatch(OpCodes.Pop),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(OpCodes.Ldc_I4_1),
            new CodeMatch(instruction => instruction.opcode == OpCodes.Stfld &&
                Equals(instruction.operand, AccessTools.Field(typeof(NoteData), "isUsed"))));
        matcher.ThrowIfInvalid("Native seek note assignment was not found.");
        matcher.Advance(1).Set(OpCodes.Stloc, result);
        matcher.Advance(2).Set(OpCodes.Ldloc, result);
        return matcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(GameManager), "GetNoteSpeed"), HarmonyPrefix]
    private static void PreviewNoteSpeed(ref int __0)
    {
        if (PracticeMode.Player?.Previewing == true) __0 = (int)OptionNotespeedID.Speed_6_75;
    }

    [HarmonyPatch(typeof(GameManager), "GetTouchSpeed"), HarmonyPrefix]
    private static void PreviewTouchSpeed(ref int __0)
    {
        if (PracticeMode.Player?.Previewing == true) __0 = (int)OptionTouchspeedID.Speed6_75;
    }

    [HarmonyPatch(typeof(SoundManager), "PlayExecute"), HarmonyPostfix]
    private static void ApplyQueuedAudioRate()
    {
        var playback = PracticeMode.Player;
        if (playback?.mediaRatePending == true) playback.ApplyMediaRate();
    }

    [HarmonyPatch(typeof(NotesManager), "UpdateTimer"), HarmonyPostfix]
    private static void MapChartTime(ref float ____curMSec)
    {
        var playback = PracticeMode.Player;
        if (playback == null) return;
        playback.lastNativeTime = ____curMSec;
        if (playback.attached)
            ____curMSec = (float)(playback.chartAnchor + (____curMSec - playback.nativeAnchor) * playback.Rate);
        // The original UpdateTimer still owns its stopwatch, previous time and abandoned timer.
    }
}
