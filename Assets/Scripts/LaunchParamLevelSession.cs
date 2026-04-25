using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static carrier for a level loaded from a `?level=...` launch parameter.
/// <see cref="GameManager"/> stages the Base64 payload on Awake; <see cref="LevelManager"/>
/// picks it up on Start and runs it as a single, timed level. When the level
/// completes, <see cref="LevelManager.LoadNextLevel"/> ends the session and
/// reloads the scene so the player lands on the normal main menu.
///
/// State is intentionally process-wide static so it survives scene reloads
/// (the launch-param URL would otherwise re-trigger every reload, looping the
/// custom level forever).
/// </summary>
public static class LaunchParamLevelSession
{
    /// <summary>True between a successful TryStageFromBase64 and End().</summary>
    public static bool IsActive { get; private set; }

    /// <summary>The staged custom-level preset, or null when no session is active.</summary>
    public static GameOfLifeLevelPreset Preset { get; private set; }

    private static bool _consumedThisProcess;

    /// <summary>
    /// Decode and stage a launch-param level for the current scene. Only honors
    /// the very first call per process; later calls (e.g. after returning to
    /// the main menu via a scene reload) are ignored so the same launch param
    /// doesn't loop forever.
    /// </summary>
    public static bool TryStageFromBase64(string base64)
    {
        if (_consumedThisProcess) return false;
        _consumedThisProcess = true;

        if (string.IsNullOrWhiteSpace(base64)) return false;

        var temp = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        if (!temp.TryFromBase64(base64, out string error))
        {
            Debug.LogWarning($"LaunchParamLevelSession: failed to decode launch-param level ({error}).");
            DestroyPreset(temp);
            return false;
        }

        temp.name = "LaunchParamLevel";
        temp.suppressLeaderboard = true;
        Preset = temp;
        IsActive = true;
        return true;
    }

    /// <summary>
    /// End the active session and load the active scene fresh, dropping the
    /// player back into the normal (non-launch-param) main-menu flow.
    /// No-op if no session is active.
    /// </summary>
    public static bool ReturnToMainMenu()
    {
        if (!IsActive) return false;
        End();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        return true;
    }

    /// <summary>Tear down the session and free the temporary preset.</summary>
    public static void End()
    {
        IsActive = false;
        DestroyPreset(Preset);
        Preset = null;
    }

    private static void DestroyPreset(GameOfLifeLevelPreset p)
    {
        if (p == null) return;
        if (Application.isPlaying) Object.Destroy(p);
        else Object.DestroyImmediate(p);
    }
}
