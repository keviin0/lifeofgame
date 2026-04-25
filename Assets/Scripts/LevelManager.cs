using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the list of level presets and drives which level the GameOfLifeSimulation runs.
/// Assign levels in the inspector; the simulation loads the first level on start.
/// Call LoadNextLevel() or LoadLevelByIndex() to transition.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [SerializeField] private GameOfLifeSimulation simulation;
    [Tooltip("Level presets in order. First is loaded on start.")]
    [SerializeField] private GameOfLifeLevelPreset[] levels;
    [Tooltip("Current level index (0-based). Used when transitioning.")]
    [SerializeField] private int currentLevelIndex;
    [Tooltip("Levels with index below this are treated as menu/intro and are excluded from the run timer and intermission.")]
    [SerializeField] private int firstPlayableLevelIndex = 1;
    /// <summary>
    /// Fired whenever a level is loaded or reloaded. Passes the new level index.
    /// </summary>
    public event Action<int> OnLevelLoaded;

    /// <summary>
    /// Fired when the current level is completed (before transitioning to the next).
    /// Passes the completed level index.
    /// </summary>
    public event Action<int> OnLevelCompleted;

    private void Start()
    {
        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();

        // Level-editor test run: skip the configured level list entirely and
        // load the user's in-progress level. Completing the level (or pressing
        // ESC, handled by GameManager) sends the player back to the editor.
        if (LevelEditorTestSession.TryConsumePreset(out var testPreset))
        {
            if (simulation != null)
                simulation.LoadLevel(testPreset);
            else
                Debug.LogWarning("LevelManager: test session active but no GameOfLifeSimulation found in scene.");
            OnLevelLoaded?.Invoke(currentLevelIndex);
            return;
        }

        // Any non-test load clears stale test state.
        LevelEditorTestSession.End();

        // Launch-param level: a single, timed run of a custom level decoded
        // from the URL. Loaded at the first playable index so the run timer
        // and level intermission both fire normally; LoadNextLevel returns
        // the player to the main menu.
        if (LaunchParamLevelSession.IsActive && LaunchParamLevelSession.Preset != null)
        {
            currentLevelIndex = firstPlayableLevelIndex;
            if (simulation != null)
                simulation.LoadLevel(LaunchParamLevelSession.Preset);
            else
                Debug.LogWarning("LevelManager: launch-param level active but no GameOfLifeSimulation found in scene.");
            OnLevelLoaded?.Invoke(currentLevelIndex);
            return;
        }

        if (levels != null && levels.Length > 0)
        {
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levels.Length - 1);
            LoadLevelByIndex(currentLevelIndex);
        }
        else if (simulation != null && simulation.IsInitialized == false)
        {
            Debug.LogWarning("LevelManager: No levels assigned. Assign level presets or set a preset on GameOfLifeSimulation.");
        }
    }

    /// <summary>
    /// Load the level at the given index. Does nothing if index is out of range.
    /// If startBlack is true, the simulation builds the grid but renders it fully
    /// black so a reveal transition can be run afterwards.
    /// </summary>
    public void LoadLevelByIndex(int index, bool startBlack = false)
    {
        // During a test/launch-param run, the configured `levels` list isn't
        // what's playing, so reloading by index (e.g. from the death routine)
        // would warp the player into an unrelated real level. Exit the session
        // back to its origin instead.
        if (LevelEditorTestSession.ReturnToEditor()) return;
        if (LaunchParamLevelSession.ReturnToMainMenu()) return;

        if (levels == null || index < 0 || index >= levels.Length) return;
        currentLevelIndex = index;
        if (simulation != null && levels[index] != null)
            simulation.LoadLevel(levels[index], startBlack);
        OnLevelLoaded?.Invoke(currentLevelIndex);
    }

    /// <summary>
    /// Load the next level in the list. Wraps to 0 after the last level.
    /// If startBlack is true, the simulation builds the grid but renders it fully
    /// black so a reveal transition can be run afterwards.
    /// </summary>
    public void LoadNextLevel(bool startBlack = false)
    {
        // In a test run the "next level" is the editor we came from.
        // The level-complete transition has already played in the simulation,
        // so we just hop scenes here.
        if (LevelEditorTestSession.ReturnToEditor())
            return;

        // Launch-param run: same idea, but the "next level" is the main menu.
        if (LaunchParamLevelSession.ReturnToMainMenu())
            return;

        if (levels == null || levels.Length == 0) return;
        OnLevelCompleted?.Invoke(currentLevelIndex);
        currentLevelIndex = (currentLevelIndex + 1) % levels.Length;
        LoadLevelByIndex(currentLevelIndex, startBlack);
    }

    /// <summary>
    /// Load a specific preset (e.g. from a trigger or UI). Does not change currentLevelIndex.
    /// If startBlack is true, the simulation builds the grid but renders it fully
    /// black so a reveal transition can be run afterwards.
    /// </summary>
    public void LoadPreset(GameOfLifeLevelPreset preset, bool startBlack = false)
    {
        if (simulation != null && preset != null)
            simulation.LoadLevel(preset, startBlack);
    }

    public int CurrentLevelIndex => currentLevelIndex;
    public int LevelCount => levels != null ? levels.Length : 0;
    public int FirstPlayableLevelIndex => firstPlayableLevelIndex;
    public bool IsPlayableLevel(int index) => index >= firstPlayableLevelIndex;
    public bool IsLastPlayableLevel(int index) => levels != null && index == levels.Length - 1;

    public GameOfLifeLevelPreset GetLevelAt(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length) return null;
        return levels[index];
    }

    public GameOfLifeLevelPreset CurrentLevel
    {
        get
        {
            if (LaunchParamLevelSession.IsActive && LaunchParamLevelSession.Preset != null)
                return LaunchParamLevelSession.Preset;
            return GetLevelAt(currentLevelIndex);
        }
    }
}
