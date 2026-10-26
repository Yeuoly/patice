using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Patice.Practice;
using Process;

namespace Patice.Game;

public sealed class GamePlayback : IPlayback
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

    public GamePlayback(GameProcess process, GameMonitor monitor, MovieController movie, int monitorIndex)
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

    public void Restore(ExitBehavior behavior)
    {
        var rewind = behavior == ExitBehavior.ReturnToEntryPosition;
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

    internal void ApplyQueuedAudioRate()
    {
        if (mediaRatePending) ApplyMediaRate();
    }

    internal void MapChartTime(ref float current)
    {
        lastNativeTime = current;
        if (attached)
            current = (float)(chartAnchor + (current - nativeAnchor) * Rate);
        // The original timer still owns its stopwatch and previous time.
    }
}
