#if UNITY_WEBGL
using Wavedash;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class WavedashUtils
{
    private static readonly string EASY_COMPLETION_TIMES_LEADERBOARD_NAME = "easy_completion_times";
    private static readonly string HARD_COMPLETION_TIMES_LEADERBOARD_NAME = "hard_completion_times";

    /// <summary>Fires after a completion time has been uploaded to a leaderboard.
    /// The bool indicates whether the updated leaderboard was the easy-mode one.</summary>
    public static event Action<bool> OnLeaderboardUpdated;

    private static void SetStatInt(string statName, int value, bool storeNow = true)
    {
        int currentValue = Wavedash.SDK.GetStatInt(statName);
        if (value < currentValue) return;
        Wavedash.SDK.SetStatInt(statName, value, storeNow);

        int time = Wavedash.SDK.GetStatInt(statName);
        var user = Wavedash.SDK.GetUser();
        var playerName = user["username"];
        Debug.Log($"Stat {statName} set to {time} milliseconds for {playerName}");
    }

    private static void SetAchievement(string achievementName)
    {
        if (Wavedash.SDK.GetAchievement(achievementName)) return;
        Wavedash.SDK.SetAchievement(achievementName);
    }

    private async static Task UploadToLeaderboard(string leaderboardName, int time)
    {
        var leaderboard = await Wavedash.SDK.GetOrCreateLeaderboard(
            leaderboardName,
            WavedashConstants.LeaderboardSortMethod.ASCENDING,
            WavedashConstants.LeaderboardDisplayType.TIME_SECONDS
        );

        if (leaderboard == null) return;

        var leaderboardId = leaderboard["id"].ToString();

        var result = await Wavedash.SDK.UploadLeaderboardScore(
            leaderboardId,
            time,
            keepBest: true
        );

        if (result != null)
            Debug.Log($"Rank on {leaderboardName}: {result["globalRank"]}");
    }

    private async static void SetModeLeaderboard(bool isEasyMode, int time)
    {
        await UploadToLeaderboard(GetTotalLeaderboardName(isEasyMode), time);
        OnLeaderboardUpdated?.Invoke(isEasyMode);
    }

    private async static void SetLevelLeaderboard(bool isEasyMode, string leaderboardKey, int time)
    {
        await UploadToLeaderboard(GetLevelLeaderboardName(isEasyMode, leaderboardKey), time);
        OnLeaderboardUpdated?.Invoke(isEasyMode);
    }

    public static void GameComplete(bool isEasyMode, int time)
    {
        if (isEasyMode)
        {
            SetAchievement("PETRI_DISH_SURVIVOR");
            SetStatInt("BEST_TIME_TO_COMPLETION_EASY", time);
            SetModeLeaderboard(true, time);
        }
        else
        {
            SetAchievement("PETRI_DISH_WARRIOR");
            SetStatInt("BEST_TIME_TO_COMPLETION_HARD", time);
            if (!GameDifficulty.CurrentRunHadDeath)
                SetAchievement("PETRI_DISH_CHAMPION");
            SetModeLeaderboard(false, time);
        }
    }

    public static void LevelComplete(bool isEasyMode, string leaderboardKey, int time)
    {
        if (string.IsNullOrWhiteSpace(leaderboardKey))
        {
            Debug.LogWarning("LevelComplete called with empty leaderboardKey; skipping upload.");
            return;
        }
        SetLevelLeaderboard(isEasyMode, leaderboardKey, time);
    }

    private static string GetTotalLeaderboardName(bool isEasyMode) =>
        isEasyMode ? EASY_COMPLETION_TIMES_LEADERBOARD_NAME : HARD_COMPLETION_TIMES_LEADERBOARD_NAME;

    private static string GetLevelLeaderboardName(bool isEasyMode, string leaderboardKey) =>
        $"{(isEasyMode ? "easy" : "hard")}_level_{leaderboardKey}_completion_times";

    public struct LeaderboardData
    {
        public List<Dictionary<string, object>> Top;
        public List<Dictionary<string, object>> Around;
        public Dictionary<string, object> Me;
        public string MyUserId;
    }

    public static Task<LeaderboardData> GetTotalLeaderboardFullData(
        bool isEasyMode, int topLimit, int aroundAhead, int aroundBehind) =>
        GetFullByName(GetTotalLeaderboardName(isEasyMode), topLimit, aroundAhead, aroundBehind);

    public static Task<LeaderboardData> GetLevelLeaderboardFullData(
        bool isEasyMode, string leaderboardKey, int topLimit, int aroundAhead, int aroundBehind) =>
        GetFullByName(GetLevelLeaderboardName(isEasyMode, leaderboardKey), topLimit, aroundAhead, aroundBehind);

    private async static Task<LeaderboardData> GetFullByName(
        string leaderboardName, int topLimit, int aroundAhead, int aroundBehind)
    {
        var leaderboard = await Wavedash.SDK.GetLeaderboard(leaderboardName);
        if (leaderboard == null) return default;

        var leaderboardId = leaderboard["id"].ToString();

        var topTask = Wavedash.SDK.ListLeaderboardEntries(leaderboardId, 0, topLimit);
        var aroundTask = Wavedash.SDK.ListLeaderboardEntriesAroundUser(leaderboardId, aroundAhead, aroundBehind);
        await Task.WhenAll(topTask, aroundTask);

        var top = topTask.Result;
        var around = aroundTask.Result;
        string myUserId = Wavedash.SDK.GetUserId();
        Dictionary<string, object> me = FindByUserId(around, myUserId) ?? FindByUserId(top, myUserId);

        return new LeaderboardData
        {
            Top = top,
            Around = around,
            Me = me,
            MyUserId = myUserId
        };
    }

    private static Dictionary<string, object> FindByUserId(List<Dictionary<string, object>> list, string userId)
    {
        if (list == null || string.IsNullOrEmpty(userId)) return null;
        foreach (var e in list)
        {
            if (e == null) continue;
            if (e.TryGetValue("userId", out var v) && v?.ToString() == userId)
                return e;
        }
        return null;
    }
}
#endif
