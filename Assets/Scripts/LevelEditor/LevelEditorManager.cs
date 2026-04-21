using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Manages the level editor grid using UGUI Image elements for guaranteed rendering.
/// Handles mouse painting and converts the grid into a GameOfLifeLevelPreset.
/// </summary>
public class LevelEditorManager : MonoBehaviour
{
    private const int GRID_SIZE = 33;
    private const float GRID_SCREEN_FRACTION = 0.72f;

    [Header("Cell Colors")]
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color wallColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color collectibleColor = new Color(1.0f, 0.75f, 0.0f, 1f);
    [SerializeField] private Color cursorStartColor = new Color(0.1f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color gridLineColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    private LevelEditorCellType[,] _grid;
    private Image[,] _cellImages;
    private Canvas _gridCanvas;
    private RectTransform _gridPanel;
    private LevelEditorCellType _currentBrush = LevelEditorCellType.Wall;
    private Vector2Int _lastHoveredCell = new Vector2Int(-1, -1);
    private bool _isPainting;
    private bool _isErasing;

    /// <summary>
    /// Current grid width.
    /// </summary>
    public int GridWidth => GRID_SIZE;

    /// <summary>
    /// Current grid height.
    /// </summary>
    public int GridHeight => GRID_SIZE;

    /// <summary>
    /// Current brush type for painting.
    /// </summary>
    public LevelEditorCellType CurrentBrush => _currentBrush;

    /// <summary>
    /// Read-only access to the grid state.
    /// </summary>
    public LevelEditorCellType[,] Grid => _grid;

    private void Awake()
    {
        BuildGridUI();
        RestoreSavedGrid();
    }

    private void Update()
    {
        HandleMouseInput();
    }

    /// <summary>
    /// Set the active brush type for painting cells.
    /// </summary>
    public void SetBrush(LevelEditorCellType brush)
    {
        _currentBrush = brush;
    }

    /// <summary>
    /// Clear the entire grid back to empty.
    /// </summary>
    public void ClearAllCells()
    {
        if (_grid == null) return;

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                _grid[x, y] = LevelEditorCellType.Empty;
                _cellImages[x, y].color = emptyColor;
            }
        }
    }

    /// <summary>
    /// Validate the current grid and return the result.
    /// </summary>
    public LevelValidator.ValidationResult ValidateGrid()
    {
        return LevelValidator.Validate(GRID_SIZE, GRID_SIZE, _grid);
    }

    /// <summary>
    /// Build a runtime GameOfLifeLevelPreset from the current grid state.
    /// Returns null if validation fails.
    /// </summary>
    public GameOfLifeLevelPreset BuildPreset()
    {
        var validation = ValidateGrid();
        if (!validation.IsValid)
            return null;

        var preset = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        preset.gridWidth = GRID_SIZE;
        preset.gridHeight = GRID_SIZE;
        preset.initialLiveCells = new List<Vector2Int>();
        preset.collectibleCells = new List<Vector2Int>();
        preset.cursorStartCells = new List<Vector2Int>();

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                switch (_grid[x, y])
                {
                    case LevelEditorCellType.Wall:
                        preset.initialLiveCells.Add(pos);
                        break;
                    case LevelEditorCellType.Collectible:
                        preset.collectibleCells.Add(pos);
                        break;
                    case LevelEditorCellType.CursorStart:
                        preset.cursorStartCells.Add(pos);
                        break;
                }
            }
        }

        return preset;
    }

    /// <summary>
    /// Export the current grid as a Base64 string. Returns null if validation fails.
    /// </summary>
    public string ExportToBase64()
    {
        var preset = BuildPreset();
        return preset != null ? preset.ToBase64() : null;
    }

    /// <summary>
    /// Import a level from a Base64 string. Returns false if the string is invalid.
    /// </summary>
    public bool ImportFromBase64(string base64)
    {
        var tempPreset = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        if (!tempPreset.TryFromBase64(base64, out string error))
        {
            Debug.LogWarning($"LevelEditorManager: Import failed — {error}");
            Object.Destroy(tempPreset);
            return false;
        }

        ClearAllCells();

        foreach (var cell in tempPreset.initialLiveCells)
            SetCell(cell.x, cell.y, LevelEditorCellType.Wall);

        foreach (var cell in tempPreset.collectibleCells)
            SetCell(cell.x, cell.y, LevelEditorCellType.Collectible);

        foreach (var cell in tempPreset.cursorStartCells)
            SetCell(cell.x, cell.y, LevelEditorCellType.CursorStart);

        Object.Destroy(tempPreset);
        return true;
    }

    private void SetCell(int x, int y, LevelEditorCellType type)
    {
        if (!IsInBounds(x, y)) return;
        _grid[x, y] = type;
        _cellImages[x, y].color = GetColorForType(type);
    }

    private void BuildGridUI()
    {
        _grid = new LevelEditorCellType[GRID_SIZE, GRID_SIZE];
        _cellImages = new Image[GRID_SIZE, GRID_SIZE];

        // Canvas for the grid (separate from the UI panel canvas)
        GameObject canvasObj = new GameObject("GridCanvas");
        canvasObj.transform.SetParent(transform, false);
        _gridCanvas = canvasObj.AddComponent<Canvas>();
        _gridCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _gridCanvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // White background covering the left portion of the screen
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = new Vector2(GRID_SCREEN_FRACTION + 0.03f, 1f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.white;

        // Grid panel — centered in the left portion, square
        GameObject gridObj = new GameObject("GridPanel");
        gridObj.transform.SetParent(canvasObj.transform, false);
        _gridPanel = gridObj.AddComponent<RectTransform>();

        // Anchor the grid to the center of the left area
        float midX = GRID_SCREEN_FRACTION * 0.5f;
        _gridPanel.anchorMin = new Vector2(midX, 0.5f);
        _gridPanel.anchorMax = new Vector2(midX, 0.5f);
        _gridPanel.pivot = new Vector2(0.5f, 0.5f);

        // Size the grid as a square that fits in the available space with padding
        float availableHeight = 1080f * 0.9f;
        float availableWidth = 1920f * GRID_SCREEN_FRACTION * 0.9f;
        float gridPixelSize = Mathf.Min(availableHeight, availableWidth);
        _gridPanel.sizeDelta = new Vector2(gridPixelSize, gridPixelSize);

        // Grid background (shows as grid lines)
        var gridBgImage = gridObj.AddComponent<Image>();
        gridBgImage.color = gridLineColor;

        // Grid layout for cells
        var gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        float cellPixelSize = Mathf.Floor(gridPixelSize / GRID_SIZE);
        float spacing = Mathf.Max(1f, cellPixelSize * 0.04f);
        float actualCellSize = (gridPixelSize - spacing * (GRID_SIZE - 1)) / GRID_SIZE;

        gridLayout.cellSize = new Vector2(actualCellSize, actualCellSize);
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.startCorner = GridLayoutGroup.Corner.LowerLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = GRID_SIZE;
        gridLayout.padding = new RectOffset(0, 0, 0, 0);

        // Create cells (column-major: x is column, y is row from bottom)
        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                GameObject cellObj = new GameObject($"C_{x}_{y}");
                cellObj.transform.SetParent(gridObj.transform, false);

                var cellImage = cellObj.AddComponent<Image>();
                cellImage.color = emptyColor;
                cellImage.raycastTarget = false;

                _cellImages[x, y] = cellImage;
            }
        }

        Debug.Log($"LevelEditorManager: Built {GRID_SIZE}x{GRID_SIZE} UI grid, cellSize={actualCellSize:F1}px, spacing={spacing:F1}px");
    }

    private void HandleMouseInput()
    {
        if (_grid == null || _gridPanel == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseScreen = mouse.position.ReadValue();
        Vector2Int cell = ScreenToCell(mouseScreen);

        // Hover highlight
        if (cell != _lastHoveredCell)
        {
            if (IsInBounds(_lastHoveredCell.x, _lastHoveredCell.y))
                _cellImages[_lastHoveredCell.x, _lastHoveredCell.y].color = GetColorForType(_grid[_lastHoveredCell.x, _lastHoveredCell.y]);

            if (IsInBounds(cell.x, cell.y))
            {
                Color baseColor = GetColorForType(_grid[cell.x, cell.y]);
                _cellImages[cell.x, cell.y].color = Color.Lerp(baseColor, Color.gray, 0.3f);
            }

            _lastHoveredCell = cell;
        }

        // Left click: paint
        if (mouse.leftButton.wasPressedThisFrame)
        {
            _isPainting = true;
            _isErasing = false;
            PaintCell(cell);
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            _isErasing = true;
            _isPainting = false;
            EraseCell(cell);
        }

        if (_isPainting && mouse.leftButton.isPressed)
            PaintCell(cell);
        else if (_isPainting && mouse.leftButton.wasReleasedThisFrame)
            _isPainting = false;

        if (_isErasing && mouse.rightButton.isPressed)
            EraseCell(cell);
        else if (_isErasing && mouse.rightButton.wasReleasedThisFrame)
            _isErasing = false;
    }

    private void PaintCell(Vector2Int cell)
    {
        if (!IsInBounds(cell.x, cell.y)) return;
        _grid[cell.x, cell.y] = _currentBrush;
        _cellImages[cell.x, cell.y].color = GetColorForType(_currentBrush);
    }

    private void EraseCell(Vector2Int cell)
    {
        if (!IsInBounds(cell.x, cell.y)) return;
        _grid[cell.x, cell.y] = LevelEditorCellType.Empty;
        _cellImages[cell.x, cell.y].color = emptyColor;
    }

    private Color GetColorForType(LevelEditorCellType type)
    {
        return type switch
        {
            LevelEditorCellType.Wall => wallColor,
            LevelEditorCellType.Collectible => collectibleColor,
            LevelEditorCellType.CursorStart => cursorStartColor,
            _ => emptyColor,
        };
    }

    private Vector2Int ScreenToCell(Vector2 screenPos)
    {
        // Convert screen position to grid panel local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridPanel, screenPos, null, out Vector2 localPoint);

        // Grid panel has pivot at center, so offset to bottom-left origin
        float halfSize = _gridPanel.rect.width * 0.5f;
        float normalizedX = (localPoint.x + halfSize) / _gridPanel.rect.width;
        float normalizedY = (localPoint.y + halfSize) / _gridPanel.rect.height;

        int x = Mathf.FloorToInt(normalizedX * GRID_SIZE);
        int y = Mathf.FloorToInt(normalizedY * GRID_SIZE);

        return new Vector2Int(x, y);
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < GRID_SIZE && y >= 0 && y < GRID_SIZE;
    }

    /// <summary>
    /// Save the current grid state to the bridge so it survives scene reloads.
    /// </summary>
    public void SaveGridState()
    {
        if (_grid == null) return;

        int[,] saved = new int[GRID_SIZE, GRID_SIZE];
        for (int x = 0; x < GRID_SIZE; x++)
            for (int y = 0; y < GRID_SIZE; y++)
                saved[x, y] = (int)_grid[x, y];

        LevelEditorBridge.SavedGrid = saved;
    }

    private void RestoreSavedGrid()
    {
        int[,] saved = LevelEditorBridge.SavedGrid;
        if (saved == null) return;
        if (saved.GetLength(0) != GRID_SIZE || saved.GetLength(1) != GRID_SIZE) return;

        for (int x = 0; x < GRID_SIZE; x++)
        {
            for (int y = 0; y < GRID_SIZE; y++)
            {
                LevelEditorCellType type = (LevelEditorCellType)saved[x, y];
                _grid[x, y] = type;
                _cellImages[x, y].color = GetColorForType(type);
            }
        }

        Debug.Log("LevelEditorManager: Restored grid from saved state.");
    }
}
