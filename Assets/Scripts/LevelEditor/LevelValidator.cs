using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates a level editor grid state before allowing play-testing or export.
/// Each cell in the grid uses a CellType enum matching the 2-bit encoding in GameOfLifeLevelPreset.
/// </summary>
public static class LevelValidator
{
    private const int MIN_GRID_SIZE = 13;
    private const int MAX_GRID_SIZE = 57;

    /// <summary>
    /// Result of a level validation check.
    /// </summary>
    public readonly struct ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(bool isValid, List<string> errors)
        {
            IsValid = isValid;
            Errors = errors;
        }
    }

    /// <summary>
    /// Validate a grid of cell types for playability.
    /// </summary>
    public static ValidationResult Validate(int gridWidth, int gridHeight, LevelEditorCellType[,] grid)
    {
        var errors = new List<string>();

        if (gridWidth < MIN_GRID_SIZE || gridHeight < MIN_GRID_SIZE)
            errors.Add($"Grid too small (min {MIN_GRID_SIZE}x{MIN_GRID_SIZE}).");

        if (gridWidth > MAX_GRID_SIZE || gridHeight > MAX_GRID_SIZE)
            errors.Add($"Grid too large (max {MAX_GRID_SIZE}x{MAX_GRID_SIZE}).");

        if (grid == null)
        {
            errors.Add("Grid data is null.");
            return new ValidationResult(false, errors);
        }

        int collectibleCount = 0;
        int cursorStartCount = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                switch (grid[x, y])
                {
                    case LevelEditorCellType.Collectible:
                        collectibleCount++;
                        break;
                    case LevelEditorCellType.CursorStart:
                        cursorStartCount++;
                        break;
                }
            }
        }

        if (collectibleCount == 0)
            errors.Add("Need at least 1 collectible.");

        if (cursorStartCount == 0)
            errors.Add("Need at least 1 cursor start.");

        return new ValidationResult(errors.Count == 0, errors);
    }
}
