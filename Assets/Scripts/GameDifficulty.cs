using UnityEngine;

/// <summary>
/// Difficulty chosen from main-menu coins. Easy = 2 lives with hit feedback + iframes.
/// </summary>
public static class GameDifficulty
{
    public static bool IsEasyMode { get; private set; }

    /// <summary>True after any real death this run (easy second life or hard one-life). Reset when picking difficulty on the menu.</summary>
    public static bool CurrentRunHadDeath { get; private set; }

    public static int MaxLives => IsEasyMode ? 2 : 1;

    /// <summary>Easy: freeze simulation + cursor this long (real time; does not stop UI timers).</summary>
    public static float EasyHitPauseSeconds => 0.7f;

    /// <summary>Easy: after pause, this much iframe time while gameplay runs.</summary>
    public static float EasyHitIframesAfterPauseSeconds => 0.7f;

    public static void SetEasyMode()
    {
        IsEasyMode = true;
    }

    public static void SetHardMode()
    {
        IsEasyMode = false;
    }

    /// <summary>Call when the player chooses easy/hard on the menu so a new run starts with a clean death record.</summary>
    public static void BeginGameplayRunFromMenuPick()
    {
        CurrentRunHadDeath = false;
    }

    /// <summary>Call when the player loses all lives (actual death, not easy-mode first hit).</summary>
    public static void NotifyRunPlayerDied()
    {
        CurrentRunHadDeath = true;
    }

    /// <summary>Before any menu choice, treat as hard (1 life).</summary>
    public static void ResetToDefault()
    {
        IsEasyMode = false;
    }
}
