using UnityEngine;

/// <summary>
/// Difficulty chosen from main-menu coins. Easy = 2 lives with hit feedback + iframes.
/// </summary>
public static class GameDifficulty
{
    public static bool IsEasyMode { get; private set; }

    public static int MaxLives => IsEasyMode ? 2 : 1;

    /// <summary>Easy: freeze simulation + cursor this long (real time; does not stop UI timers).</summary>
    public static float EasyHitPauseSeconds => 0.5f;

    /// <summary>Easy: after pause, this much iframe time while gameplay runs.</summary>
    public static float EasyHitIframesAfterPauseSeconds => 0.5f;

    public static void SetEasyMode()
    {
        IsEasyMode = true;
    }

    public static void SetHardMode()
    {
        IsEasyMode = false;
    }

    /// <summary>Before any menu choice, treat as hard (1 life).</summary>
    public static void ResetToDefault()
    {
        IsEasyMode = false;
    }
}
