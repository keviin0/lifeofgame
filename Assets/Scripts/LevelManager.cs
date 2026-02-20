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
    /// </summary>
    public void LoadLevelByIndex(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length) return;
        currentLevelIndex = index;
        if (simulation != null && levels[index] != null)
            simulation.LoadLevel(levels[index]);
    }

    /// <summary>
    /// Load the next level in the list. Wraps to 0 after the last level.
    /// </summary>
    public void LoadNextLevel()
    {
        if (levels == null || levels.Length == 0) return;
        currentLevelIndex = (currentLevelIndex + 1) % levels.Length;
        LoadLevelByIndex(currentLevelIndex);
    }

    /// <summary>
    /// Load a specific preset (e.g. from a trigger or UI). Does not change currentLevelIndex.
    /// </summary>
    public void LoadPreset(GameOfLifeLevelPreset preset)
    {
        if (simulation != null && preset != null)
            simulation.LoadLevel(preset);
    }

    public int CurrentLevelIndex => currentLevelIndex;
    public int LevelCount => levels != null ? levels.Length : 0;
}
