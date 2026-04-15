#if UNITY_WEBGL
using Wavedash;
using UnityEngine;

public static class WavedashUtils
{
    private static void SetStatFloat(string statName, float value, bool storeNow = true)
    {
        Wavedash.SDK.SetStatFloat(statName, value, storeNow);
        
        float time = Wavedash.SDK.GetStatFloat(statName);
        var user = Wavedash.SDK.GetUser();
        var playerName = user["username"];
        Debug.Log($"Stat {statName} set to {time} seconds for {playerName}");
    }

    private static void SetAchievement(string achievementName)
    {
        if (Wavedash.SDK.GetAchievement(achievementName)) return;
        Wavedash.SDK.SetAchievement(achievementName);
    }

    public static void GameComplete(bool isEasyMode, float time)
    {
        if (isEasyMode)
        {
            SetAchievement("PETRI_DISH_SURVIVOR");
            SetStatFloat("TIME_TO_COMPLETION_EASY", time);
        }
        else
        {
            SetAchievement("PETRI_DISH_WARRIOR");
            SetStatFloat("TIME_TO_COMPLETION_HARD", time);
            if (!GameDifficulty.CurrentRunHadDeath)
                SetAchievement("PETRI_DISH_CHAMPION");
        }
    }    
}
#endif
