using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Patice.Libs;
using HarmonyLib;
using Manager;
using Monitor;
using Monitor.Game;
using Process;
using UnityEngine;
using MelonLoader;
using MAI2.Util;
using Patice.Domain;

namespace Patice;

[HarmonyPatch]
public class PracticeMode
{
    private static bool entryHeld;
    public static float speed = 1;
    private static CriAtomExPlayer player;
    private static List<MovieMaterialMai2> movie;
    private static GameCtrl[] gameCtrl = new GameCtrl[2];
    public static bool keepNoteSpeed = false;

    private static void ClearPracticeEffects()
    {
        if (ui == null) return;
        var closing = ui;
        try { closing.Close(); }
        finally
        {
            UnityEngine.Object.Destroy(closing);
            ui = null;
        }
    }

    public static void GameCtrlResetOptionSpeed()
    {
        foreach (var g in gameCtrl)
        {
            try
            {
                g?.ResetOptionSpeed();
            }
            catch (NullReferenceException) { } // Inactive monitors have no initialized note-speed options.
        }
    }

    public static void SetSpeed()
    {
        ScoreGuard.MarkPractice();
        
        player?.SetPitch((float)(1200 * Math.Log(speed, 2)));
        // player.SetDspTimeStretchRatio(1 / speed);
        player?.UpdateAll();
        
        movie?.ForEach(m => m.player?.SetSpeed(speed));
        GameCtrlResetOptionSpeed();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "ForceNoteCollect")]
    private static void ForceNoteCollect(NotesManager ___NoteMng)
    {
        if (ui == null) return;
        foreach (NoteData note in ___NoteMng.getReader().GetNoteList())
        {
            if (note != null && note.type.isConnectSlide())
            {
                note.isJudged = true;
            }
        }
    }

    public static double CurrentPlayMsec
    {
        get => NotesManager.GetCurrentMsec() - 91;
        set
        {
            Playback.Seek(value);
        }
    }

    public static PracticeModeUI ui;


    [HarmonyPatch]
    public class PatchNoteSpeed
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameManager), "GetNoteSpeed");
            yield return AccessTools.Method(typeof(GameManager), "GetTouchSpeed");
        }

        public static void Postfix(ref float __result)
        {
            if (!keepNoteSpeed) return;
            __result /= speed;
        }
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    [HarmonyPostfix]
    public static void GameProcessPostStart()
    {
        ClearPracticeEffects();
        speed = 1;
        ui = null;
        entryHeld = false;
    }

    [HarmonyPatch(typeof(GameProcess), "OnRelease")]
    [HarmonyPrefix]
    public static void GameProcessPostRelease()
    {
        ClearPracticeEffects();
        speed = 1;
        ui = null;
        entryHeld = false;
    }

    [HarmonyPatch(typeof(GameCtrl), "Initialize")]
    [HarmonyPostfix]
    public static void GameCtrlPostInitialize(GameCtrl __instance)
    {
        gameCtrl[__instance.MonitorIndex] =__instance;
    }

    [HarmonyPatch(typeof(GameProcess), "OnUpdate")]
    [HarmonyPrefix]
    public static void GameProcessPreUpdate(GameMonitor[] ____monitors, int ____sequence)
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
            var active = -1;
            for (var i = 0; i < 2; i++)
            {
                if (!Singleton<UserDataManager>.Instance.GetUserData(i).IsEntry) continue;
                // Native playback and autoplay are global. Do not seek another player's game.
                if (active >= 0) { MelonLogger.Warning("[PracticeMode] Segment practice requires one active player."); return; }
                active = i;
            }
            if (active < 0) return;
            var document = NativePracticePlayer.Load(active);
            ScoreGuard.Monitor = active;
            ui = ____monitors[active].gameObject.AddComponent<PracticeModeUI>();
            ui.Initialize(active, document.segments);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[PracticeMode] {exception}");
            ClearPracticeEffects();
        }
    }

    [HarmonyPatch(typeof(InputManager), "GetSystemInputDown")]
    [HarmonyPrefix]
    public static bool ReserveEntry(InputManager.SystemButtonSetting button, ref bool __result)
    {
        if (!GameManager.IsInGame || button != InputManager.SystemButtonSetting.ButtonTest) return true;
        __result = false;
        return false;
    }

    // Filter the lowest common enqueue call: notes, holds, slides and voices use different wrappers.
    // Returning here preserves each wrapper's PlayerID and the native stop/release semantics.
    [HarmonyPatch(typeof(SoundManager), "Play")]
    [HarmonyPrefix]
    public static bool PreviewSound(int __0, int __1, int __2) =>
        !NativePracticePlayer.PreviewActive || __1 == 2 || (__0 == 0 && __2 == (int)Mai2.Mai2Cue.Cue.SE_SYS_CURSOR);

    [HarmonyPatch(typeof(GameMonitor), "IsAllJudged")]
    [HarmonyPrefix]
    public static bool PreviewEnd(ref bool __result)
    {
        if (ui == null || !ui.PreventGameEnd) return true;
        __result = false;
        return false;
    }

    private static float startGap = -1f;

    [HarmonyPatch(typeof(NotesManager), "StartPlay")]
    [HarmonyPostfix]
    public static void NotesManagerPostUpdateTimer(float msecStartGap)
    {
#if DEBUG
        MelonLogger.Msg($"[PracticeMode] NotesManager.StartPlay msecStartGap={msecStartGap}");
#endif
        startGap = msecStartGap;
    }

    private static bool isInAdvDemo = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDemoProcess), nameof(AdvDemoProcess.OnStart))]
    public static void AdvDemoProcessOnStart()
    {
        isInAdvDemo = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDemoProcess), nameof(AdvDemoProcess.OnRelease))]
    public static void AdvDemoProcessOnRelease()
    {
        isInAdvDemo = false;
    }


    [HarmonyPatch(typeof(NotesManager), "UpdateTimer")]
    [HarmonyPrefix]
    public static bool NotesManagerPostUpdateTimer(bool ____isPlaying, Stopwatch ____stopwatch, ref float ____curMSec, ref float ____curMSecPre, float ____msecStartGap)
    {
        if (isInAdvDemo || ui == null)
        {
            return true;
        }

        if (startGap != -1f)
        {
            ____curMSec = startGap;
            startGap = -1f;
        }
        ____curMSecPre = ____curMSec;
        if (____isPlaying && ____stopwatch != null && !Playback.Paused)
        {
            var num = (double)____stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000.0 * speed;
            ____curMSec += (float)num;
            ____stopwatch.Reset();
            ____stopwatch.Start();
        }

        return false;
    }

    [HarmonyPatch(typeof(SoundCtrl), "Initialize")]
    [HarmonyPostfix]
    public static void SoundCtrlPostInitialize(SoundCtrl.InitParam param, Dictionary<int, object> ____players)
    {
        var wrapper = ____players[2];
        player = (CriAtomExPlayer)wrapper.GetType().GetField("Player").GetValue(wrapper);

    }

    [HarmonyPatch(typeof(MovieController), "Awake")]
    [HarmonyPostfix]
    public static void MovieControllerPostAwake(object ____moviePlayers)
    {
        // 1.55以上，_moviePlayers是List<MovieMaterialMai2>；1.50以下，___moviePlayers是MovieMaterialMai2。
        // 所以这里用object承接，再根据object的类型做具体处理
        if (____moviePlayers is List<MovieMaterialMai2> mList) movie = mList;
        else if (____moviePlayers is MovieMaterialMai2 m) movie = new([m]);
        else MelonLogger.Error("[PracticeMode] MovieControllerPostAwake: [BUG] 收到的____moviePlayers参数不正确");
    }
}
