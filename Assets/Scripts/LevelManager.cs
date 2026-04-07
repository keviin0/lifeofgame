using System;
using UnityEngine;

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
}
