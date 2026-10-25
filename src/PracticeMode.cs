using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AquaMai.Mods.Fix;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.UX.PracticeMode.Libs;
using HarmonyLib;
using Manager;
using Monitor;
using Monitor.Game;
using Process;
using UnityEngine;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using MelonLoader;
using MAI2.Util;
using AquaMai.Mods.UX.PracticeMode.Domain;

namespace AquaMai.Mods.UX.PracticeMode;

[ConfigCollapseNamespace]
[ConfigSection(
    en: "Practice Mode.",
    name: "练习模式")]
[EnableGameVersion(23000)]
public class PracticeMode
{
    [ConfigEntry(
        name: "按键",
        en: "Key to show Practice Mode UI.",
        zh: "显示练习模式 UI 的按键")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.Test;

    [ConfigEntry]
    public static readonly bool longPress = false;

    [ConfigEntry(
        name: "仅自由模式可用",
        en: "Only allow Practice Mode in Freedom Mode while time remains.",
        zh: "仅在自由模式且时间未耗尽时允许使用练习模式")]
    public static readonly bool onlyInFreedomMode = false;

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

    public static void TogglePause()
    {
        DebugFeature.Pause = !DebugFeature.Pause;
        if (!DebugFeature.Pause)
        {
            Seek(0);
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
            catch (NullReferenceException) {} // 忽略即可，因为SBGA的代码在ResetOptionSpeed内部没有做null检查，单刷的时候对2P那侧这个函数必定会抛NPE
        }
    }

    public static void SetSpeed()
    {
        DontRuinMyAccount.triggerForPracticeMode();
        
        player?.SetPitch((float)(1200 * Math.Log(speed, 2)));
        // player.SetDspTimeStretchRatio(1 / speed);
        player?.UpdateAll();
        
        movie?.ForEach(m => m.player?.SetSpeed(speed));
        GameCtrlResetOptionSpeed();
    }

    private static IEnumerator SetSpeedCoroutineInner()
    {
        yield return null;
        SetSpeed();
    }

    public static void SetSpeedCoroutine()
    {
        SharedInstances.GameMainObject.StartCoroutine(SetSpeedCoroutineInner());
    }

    public static void Seek(int addMsec)
    {
        // Debug feature 里面那个 timer 不能感知变速
        // 为了和魔改版本统一，polyfill 里面不修这个
        // 这里重新实现一个能感知变速的 Seek
        var msec = CurrentPlayMsec + addMsec;
        if (msec < 0)
        {
            msec = 0;
        }

        CurrentPlayMsec = msec;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "ForceNoteCollect")]
    private static void ForceNoteCollect(NotesManager ___NoteMng)
    {
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
            DebugFeature.CurrentPlayMsec = value;
            SetSpeedCoroutine();
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
    }

    [HarmonyPatch(typeof(GameProcess), "OnRelease")]
    [HarmonyPrefix]
    public static void GameProcessPostRelease()
    {
        ClearPracticeEffects();
        speed = 1;
        ui = null;
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
            var entry = KeyListener.GetKeyDownOrLongPress(key, longPress);
            if (ui != null)
            {
                if (entry) ui.Entry();
                ui.Step();
                if (onlyInFreedomMode && GameManager.GetFreedomModeMSec() <= 0) ClearPracticeEffects();
                return;
            }
            if (!entry || (onlyInFreedomMode && (!GameManager.IsFreedomMode || GameManager.GetFreedomModeMSec() <= 0))) return;
            var active = -1;
            for (var i = 0; i < 2; i++)
            {
                if (!Singleton<UserDataManager>.Instance.GetUserData(i).IsEntry) continue;
                // Native playback and autoplay are global. Do not seek another player's game.
                if (active >= 0) { MelonLogger.Warning("[PracticeMode] Segment practice requires one active player."); return; }
                active = i;
            }
            if (active < 0) return;
            if (!AquaMai.Core.ConfigLoader.Config.GetSectionState(typeof(DontRuinMyAccount)).Enabled || !DontRuinMyAccount.forPracticeMode)
                throw new InvalidOperationException("DontRuinMyAccount and ForPracticeMode must be enabled for segment practice.");
            var document = NativePracticePlayer.Load(active);
            ui = ____monitors[active].gameObject.AddComponent<PracticeModeUI>();
            ui.Initialize(active, document.segments);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[PracticeMode] {exception}");
            ClearPracticeEffects();
        }
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
        if (isInAdvDemo || Shim.IsKaleidxScopeMode)
        {
            return true;
        }

        if (startGap != -1f)
        {
            ____curMSec = startGap;
            startGap = -1f;
        }
        ____curMSecPre = ____curMSec;
        if (____isPlaying && ____stopwatch != null && !DebugFeature.Pause)
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
        // var pool = new CriAtomExStandardVoicePool(1, 8, 96000, true, 2);
        // pool.AttachDspTimeStretch();
        // player.SetVoicePoolIdentifier(pool.identifier);

        // debug
        // var wrapper1 = ____players[7];
        // var player1 = (CriAtomExPlayer)wrapper1.GetType().GetField("Player").GetValue(wrapper1);
        // var pool = new CriAtomExStandardVoicePool(1, 8, 96000, true, 2);
        // pool.AttachDspTimeStretch();
        // player1.SetVoicePoolIdentifier(pool.identifier);
        // player1.SetDspTimeStretchRatio(2);
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
