using System;
using DB;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using Process;
using UnityEngine;

namespace Patice;

[HarmonyPatch]
public static class ScoreGuard
{
    public static int Monitor;
    private static bool used;
    private static UserScore saved;
    private static int musicId;
    private static int difficulty;

    public static void MarkPractice()
    {
        if (used) return;
        musicId = GameManager.SelectMusicID[Monitor];
        difficulty = GameManager.SelectDifficultyID[Monitor];
        var scores = Singleton<UserDataManager>.Instance.GetUserData(Monitor).ScoreDic[difficulty];
        saved = scores.TryGetValue(musicId, out var score) ? JsonUtility.FromJson<UserScore>(JsonUtility.ToJson(score)) : null;
        used = true;
    }

    [HarmonyPatch(typeof(GameProcess), "OnStart"), HarmonyPostfix]
    private static void Reset() { used = false; saved = null; }

    [HarmonyPatch(typeof(UserData), "UpdateScore"), HarmonyPrefix]
    private static bool SaveScore() => !used;

    [HarmonyPatch(typeof(ResultProcess), "OnStart"), HarmonyPostfix]
    private static void Result()
    {
        if (!used) return;
        var scores = Singleton<UserDataManager>.Instance.GetUserData(Monitor).ScoreDic[difficulty];
        if (saved == null) scores.Remove(musicId);
        else scores[musicId] = saved;
        var score = Singleton<GamePlayManager>.Instance.GetGameScore(Monitor, (int)GameManager.MusicTrackNumber - 1);
        var fields = Traverse.Create(score);
        fields.Property<decimal>("Achivement").Value = 0m;
        fields.Property<PlayComboflagID>("ComboType").Value = PlayComboflagID.None;
        fields.Property<PlayComboflagID>("NowComboType").Value = PlayComboflagID.None;
        score.SyncType = PlaySyncflagID.None;
        score.IsClear = false;
        foreach (var name in new[] { "DxScore", "MaxCombo", "MaxChain", "Fast", "Late" }) fields.Property<uint>(name).Value = 0;
        var judgments = fields.Field<uint[,]>("_resultList").Value;
        Array.Clear(judgments, 0, judgments.Length);
        used = false;
        saved = null;
    }
}
