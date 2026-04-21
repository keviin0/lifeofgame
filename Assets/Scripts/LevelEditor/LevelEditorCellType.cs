/// <summary>
/// Cell types matching the 2-bit encoding used in GameOfLifeLevelPreset Base64 format.
/// 0 = Empty, 1 = Wall (alive), 2 = Collectible, 3 = CursorStart.
/// </summary>
public enum LevelEditorCellType
{
    Empty = 0,
    Wall = 1,
    Collectible = 2,
    CursorStart = 3
}
