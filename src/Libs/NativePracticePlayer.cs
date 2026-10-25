using System;
using System.IO;
using Patice.Domain;
using Manager;
using UnityEngine;

namespace Patice.Libs;

public sealed class NativePracticePlayer : IPracticePlayer
{
    private double position;
    private float rate;
    private bool paused;
    private GameManager.AutoPlayMode autoplay;
    public static bool PreviewActive { get; private set; }
    public double PositionMs => PracticeMode.CurrentPlayMsec;

    public void Capture()
    {
        position = PositionMs;
        rate = PracticeMode.speed;
        paused = Playback.Paused;
        autoplay = GameManager.AutoPlay;
        ScoreGuard.MarkPractice();
    }

    public void Preview(bool enabled)
    {
        PreviewActive = enabled;
        GameManager.AutoPlay = enabled ? GameManager.AutoPlayMode.Critical : GameManager.AutoPlayMode.None;
        if (enabled) SoundManager.PauseMusic(true);
    }

    public void Pause(bool value)
    {
        Playback.Pause(value);
        if (PreviewActive) SoundManager.PauseMusic(true);
    }

    public void Seek(double milliseconds)
    {
        PracticeMode.CurrentPlayMsec = milliseconds;
        if (PreviewActive) SoundManager.PauseMusic(true);
    }

    public void SetRate(float value)
    {
        PracticeMode.speed = value;
        PracticeMode.SetSpeed();
    }

    public void Restore(bool rewind)
    {
        PreviewActive = false;
        GameManager.AutoPlay = autoplay;
        try { SetRate(rate); }
        finally
        {
            // Synchronize the paused preview audio before returning to normal play.
            try { if (rewind) Seek(position); }
            finally { Pause(rewind && paused); }
        }
    }

    public static SegmentDocument Load(int monitor)
    {
        var musicId = GameManager.SelectMusicID[monitor];
        var difficulty = GameManager.SelectDifficultyID[monitor];
        var difficulties = new[] { "basic", "advanced", "expert", "master", "remaster", "utage" };
        if (difficulty < 0 || difficulty >= difficulties.Length) throw new InvalidDataException("Unknown chart difficulty.");
        var identity = new ChartIdentity { musicId = musicId, difficulty = difficulties[difficulty] };
        var path = Path.Combine(Environment.CurrentDirectory, $"UserData/PracticeSegments/{musicId}/{identity.difficulty}.json");
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("Segment document exceeds 256 KiB.");
        var document = JsonUtility.FromJson<SegmentDocument>(File.ReadAllText(path));
        if (document == null) throw new InvalidDataException("Empty segment document.");
        document.Validate(identity, NotesManager.Instance().getPlayFinalMsec() - 91);
        return document;
    }
}
