using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining a single level: grid size and initial live cells.
/// Create via Assets > Create > Game Of Life > Level Preset.
/// </summary>
[CreateAssetMenu(fileName = "New Level", menuName = "Game Of Life/Level Preset")]
public class GameOfLifeLevelPreset : ScriptableObject
{
    [Tooltip("Grid width (number of cells)")]
    public int gridWidth = 16;

    [Tooltip("Grid height (number of cells)")]
    public int gridHeight = 16;

    [Tooltip("Initial live cell positions in grid coordinates (0,0 = bottom-left). Add entries in inspector or use the helper below.")]
    public List<Vector2Int> initialLiveCells = new List<Vector2Int>();

    /// <summary>
    /// Optional: set initial state from a simple pattern string.
    /// '1' or '#' = alive, anything else = dead. One row per line.
    /// Example 3x3 glider:
    /// ".1.\n1.1\n.11"
    /// </summary>
    public void SetFromPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;
        initialLiveCells.Clear();
        string[] rows = pattern.Trim().Split('\n');
        int h = rows.Length;
        int w = 0;
        foreach (string row in rows)
            w = Mathf.Max(w, row.Length);
        gridHeight = h;
        gridWidth = w;
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];
            for (int x = 0; x < row.Length && x < w; x++)
            {
                char c = row[x];
                if (c == '1' || c == '#')
                    initialLiveCells.Add(new Vector2Int(x, y));
            }
        }
    }

    /// <summary>
    /// Returns a copy of the initial live cells list (read-only usage).
    /// </summary>
    public IReadOnlyList<Vector2Int> GetInitialLiveCells()
    {
        return initialLiveCells;
    }
}
