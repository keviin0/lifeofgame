using UnityEngine;

/// <summary>
/// Static bridge to pass data between the level editor and game scenes.
/// Stores the preset for testing and the editor grid state for restoration.
/// </summary>
public static class LevelEditorBridge
{
    /// <summary>
    /// A preset built by the level editor, consumed by the game scene to load the custom level.
    /// </summary>
    public static GameOfLifeLevelPreset PendingPreset { get; set; }

    /// <summary>
    /// True when the game scene was launched from the level editor.
    /// </summary>
    public static bool CameFromEditor { get; set; }

    /// <summary>
    /// Saved editor grid state so the map can be restored after testing.
    /// Each element is the int cast of LevelEditorCellType.
    /// </summary>
    public static int[,] SavedGrid { get; set; }

    /// <summary>
    /// Consume the pending preset (returns it and clears the field).
    /// </summary>
    public static GameOfLifeLevelPreset ConsumePendingPreset()
    {
        var preset = PendingPreset;
        PendingPreset = null;
        return preset;
    }
}
