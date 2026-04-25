using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Static carrier used by the level editor's "Test" button to ship a
/// just-edited level into the gameplay scene. The editor stores the level as
/// a Base64 share code; the gameplay scene's <see cref="LevelManager"/> picks
/// it up on Start, builds a temporary <see cref="GameOfLifeLevelPreset"/>,
/// and loads it into the simulation in place of the normal level list.
///
/// While <see cref="IsActive"/> is true the gameplay scene is treated as a
/// throwaway test run (no level progression, ESC returns to the editor).
/// </summary>
public static class LevelEditorTestSession
{
    /// <summary>True between Begin() and End(): a test run is currently active.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Name of the scene to return to when the test ends.</summary>
    public static string ReturnSceneName { get; private set; } = "Level Editor Scene";

    private static string _pendingBase64;
    private static string _restoreBase64;

    /// <summary>
    /// Stage a level for the next gameplay scene load. The stored payload is
    /// consumed exactly once by <see cref="TryConsumePreset"/>. A second copy
    /// is held for <see cref="TryConsumeRestore"/> so the editor scene can
    /// repaint the user's in-progress level when the test ends.
    /// </summary>
    public static void Begin(string base64, string returnSceneName)
    {
        _pendingBase64 = base64;
        _restoreBase64 = base64;
        if (!string.IsNullOrEmpty(returnSceneName))
            ReturnSceneName = returnSceneName;
        IsActive = true;
    }

    /// <summary>
    /// Build a fresh preset from the staged Base64 payload. Returns false if
    /// nothing is staged or the payload doesn't decode. The payload is cleared
    /// either way so it never leaks into a subsequent scene load.
    /// </summary>
    public static bool TryConsumePreset(out GameOfLifeLevelPreset preset)
    {
        preset = null;
        string code = _pendingBase64;
        _pendingBase64 = null;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var temp = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        if (!temp.TryFromBase64(code, out _))
        {
            if (Application.isPlaying) Object.Destroy(temp);
            else Object.DestroyImmediate(temp);
            return false;
        }

        temp.name = "LevelEditorTestPreset";
        temp.suppressLeaderboard = true;
        preset = temp;
        return true;
    }

    /// <summary>
    /// Pop the most recently tested level so the editor can re-import it
    /// when its scene loads. Cleared after the first call so subsequent
    /// editor opens (e.g. coming from the main menu) start clean.
    /// </summary>
    public static bool TryConsumeRestore(out string base64)
    {
        base64 = _restoreBase64;
        _restoreBase64 = null;
        return !string.IsNullOrWhiteSpace(base64);
    }

    /// <summary>
    /// Mark the test run as finished. Call when returning to the editor (or
    /// at the start of a non-test gameplay run) so leftover state doesn't
    /// affect future scene loads. The pending-restore payload is intentionally
    /// preserved so the editor can re-import the just-tested level on its
    /// next load.
    /// </summary>
    public static void End()
    {
        IsActive = false;
        _pendingBase64 = null;
    }

    /// <summary>
    /// Convenience: end the test session and load the editor scene. Used by
    /// any code path that wants to bail out of a test run (level completed,
    /// ESC pressed, etc.). No-op if there's no active test session.
    /// </summary>
    public static bool ReturnToEditor()
    {
        if (!IsActive) return false;
        string returnTo = ReturnSceneName;
        End();
        if (string.IsNullOrEmpty(returnTo)) return false;
        SceneManager.LoadScene(returnTo);
        return true;
    }
}
