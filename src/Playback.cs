using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
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
    private readonly List<MovieMaterialMai2> moviePlayers;
    private double nativeAnchor, chartAnchor, lastNativeTime;
    private double entryPosition;
    private bool entryPaused, attached, mediaRatePending;
    private GameManager.AutoPlayMode entryAutoplay;
    public bool Previewing { get; private set; }
    public float Rate { get; private set; } = 1;
    public double PositionMs => NotesManager.GetCurrentMsec() - MusicStartDelayMs;
    public bool Paused => !(Traverse.Create(typeof(NotesManager)).Field<Stopwatch>("_stopwatch").Value?.IsRunning ?? false);

    public Playback(GameProcess process, GameMonitor monitor, MovieController movie, int monitorIndex)
    {
        this.process = process;
        this.monitor = monitor;
        this.movie = movie;
        this.monitorIndex = monitorIndex;
        var players = Traverse.Create(Singleton<SoundCtrl>.Instance).Field<Dictionary<int, object>>("_players").Value;
        music = Traverse.Create(players[2]).Field<CriAtomExPlayer>("Player").Value
            ?? throw new InvalidOperationException("Music player is unavailable.");
        moviePlayers = Traverse.Create(movie).Field<List<MovieMaterialMai2>>("_moviePlayers").Value;
    }

    public void Capture()
    {
        if (attached) throw new InvalidOperationException("Playback is already owned by practice.");
        NotesManager.UpdateTimer();
        entryPosition = PositionMs;
        entryPaused = Paused;
        entryAutoplay = GameManager.AutoPlay;
        nativeAnchor = chartAnchor = NotesManager.GetCurrentMsec();
        lastNativeTime = nativeAnchor;
        attached = true;
        ScoreGuard.MarkPractice(monitorIndex);
    }

    public void Preview(bool enabled)
    {
        Previewing = enabled;
        GameManager.AutoPlay = enabled ? GameManager.AutoPlayMode.Critical : GameManager.AutoPlayMode.None;
        SoundManager.PauseMusic(Paused || Previewing);
    }

    public void Pause(bool paused)
    {
        NotesManager.Pause(paused);
        movie.Pause(monitorIndex, paused);
        SoundManager.PauseMusic(paused || Previewing);
    }

    public void SetRate(float rate)
    {
        if (rate <= 0 || float.IsNaN(rate) || float.IsInfinity(rate)) throw new ArgumentOutOfRangeException(nameof(rate));
        NotesManager.UpdateTimer();
        chartAnchor = NotesManager.GetCurrentMsec();
        nativeAnchor = lastNativeTime;
        Rate = rate;
        ApplyMediaRate();
    }

    public void Seek(double milliseconds)
    {
        if (!attached) throw new InvalidOperationException("Practice does not own playback.");
        var paused = Paused;
        Pause(true);
        try
        {
            var engineTime = (float)Math.Max(0, milliseconds) + MusicStartDelayMs;
            nativeAnchor = chartAnchor = lastNativeTime = engineTime;
            NotesManager.StartPlay(engineTime);
            NotesManager.Pause(true);
            NotesManager.UpdateTimer();
            // The native seek clears pooled notes, judgment flags and touch chains.
            // Reset only this player's current attempt; retain all other tracks and manager state.
            Singleton<GamePlayManager>.Instance.GetGameScore(monitorIndex, -1).Initialize(monitorIndex, false);
            monitor.Seek((int)milliseconds);
            SoundManager.SeekMusic((int)milliseconds);
            movie.SetSeekFrame(monitorIndex, milliseconds);
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
        foreach (var player in moviePlayers) player.player.SetSpeed(Rate);
        mediaRatePending = false;
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
