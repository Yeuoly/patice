using System;
using System.Linq;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Process;

namespace Patice;

// Direct game playback. No dependency on another mod's debug implementation.
[HarmonyPatch]
public static class Playback
{
    private static GameProcess process;
    private static GameMonitor[] monitors;
    private static MovieController movie;
    public static bool Paused { get; private set; }

    [HarmonyPatch(typeof(GameProcess), "OnStart"), HarmonyPostfix]
    private static void Start(GameProcess __instance, GameMonitor[] ____monitors, MovieController ____gameMovie)
    {
        process = __instance;
        monitors = ____monitors;
        movie = ____gameMovie;
        Paused = false;
    }

    public static void Pause(bool paused)
    {
        Paused = paused;
        SoundManager.PauseMusic(paused);
        Movie("Pause", paused);
        NotesManager.Pause(paused);
    }

    public static void Seek(double milliseconds)
    {
        if (process == null || monitors == null) throw new InvalidOperationException("No active game.");
        var time = Math.Max(0, milliseconds);
        var paused = Paused;
        Pause(true);
        try
        {
            Singleton<GamePlayManager>.Instance.Initialize();
            Movie("SetSeekFrame", time);
            SoundManager.SeekMusic((int)time);
            foreach (var monitor in monitors) monitor.Seek((int)time);
            NotesManager.StartPlay((float)time + 91);
            process.UpdateNotes();
        }
        finally { Pause(paused); }
        PracticeMode.SetSpeed();
    }

    private static void Movie(string methodName, object value)
    {
        if (movie == null) return;
        var method = movie.GetType().GetMethods().Single(m => m.Name == methodName);
        if (method.GetParameters().Length == 1) method.Invoke(movie, new[] { value });
        else for (int i = 0; i < monitors.Length; i++) method.Invoke(movie, new[] { (object)i, value });
    }
}
