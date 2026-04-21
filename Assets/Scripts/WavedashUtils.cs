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

    public static Task<(List<Dictionary<string, object>> topEntries, Dictionary<string, object> myEntry)>
        GetTotalLeaderboardData(bool isEasyMode, int limit) =>
        GetLeaderboardDataByName(GetTotalLeaderboardName(isEasyMode), limit);

    public static Task<(List<Dictionary<string, object>> topEntries, Dictionary<string, object> myEntry)>
        GetLevelLeaderboardData(bool isEasyMode, string leaderboardKey, int limit) =>
        GetLeaderboardDataByName(GetLevelLeaderboardName(isEasyMode, leaderboardKey), limit);

    private async static Task<(List<Dictionary<string, object>> topEntries, Dictionary<string, object> myEntry)>
        GetLeaderboardDataByName(string leaderboardName, int limit)
    {
        var leaderboard = await Wavedash.SDK.GetLeaderboard(leaderboardName);
        if (leaderboard == null) return (null, null);

        var leaderboardId = leaderboard["id"].ToString();

        var topTask = Wavedash.SDK.ListLeaderboardEntries(leaderboardId, 0, limit);
        var mineTask = Wavedash.SDK.GetMyLeaderboardEntries(leaderboardId);
        await Task.WhenAll(topTask, mineTask);

        var mine = mineTask.Result;
        var myEntry = (mine != null && mine.Count > 0) ? mine[0] : null;
        return (topTask.Result, myEntry);
    }
}
#endif
