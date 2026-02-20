using System;
using UnityEngine;

/// <summary>
/// Runs a Conway's Game of Life simulation on a grid. Each live cell has a collider for gameplay.
/// Call LoadLevel(preset) to set or change the grid; timestep is fixed for now.
/// </summary>
public class GameOfLifeSimulation : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Initial level to load. If null, use LevelManager or call LoadLevel() at runtime.")]
    [SerializeField] private GameOfLifeLevelPreset levelPreset;

    [Header("Grid layout")]
    [Tooltip("World size of one cell (square). With ortho size 5, use ~0.5 so the grid fills the camera (e.g. 20x20 = 10 units).")]
    [SerializeField] private float cellSize = 0.5f;

    [Tooltip("If true, grid is centered at world (0,0) when loading a level. Otherwise uses Grid Origin.")]
    [SerializeField] private bool centerGridOnLoad = true;

    [Tooltip("World position of the bottom-left corner of the grid — cell (0,0) starts here (used when Center Grid On Load is false).")]
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;

    [Header("Simulation")]
    [Tooltip("Time in seconds between Game of Life steps")]
    [SerializeField] private float stepInterval = 0.4f;

    [Header("Visual")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    // State
    private int _width;
    private int _height;
    private bool[,] _current;
    private bool[,] _next;
    private float _stepTimer;
    private GameOfLifeCellView[,] _cells;
    private bool _initialized;

    public int GridWidth => _width;
    public int GridHeight => _height;
    public float CellSize => cellSize;
    public Vector2 GridOrigin => gridOrigin;
    public bool IsInitialized => _initialized;

    /// <summary>
    /// World position for the center of cell (x, y).
    /// </summary>
    public Vector2 CellToWorld(int x, int y)
    {
        return gridOrigin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    }

    /// <summary>
    /// Grid cell that contains the given world position. Clamped to grid bounds.
    /// </summary>
    public Vector2Int WorldToCell(Vector2 world)
    {
        Vector2 local = world - gridOrigin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.y / cellSize);
        x = Mathf.Clamp(x, 0, _width - 1);
        y = Mathf.Clamp(y, 0, _height - 1);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// True if the grid cell (x,y) is alive and within bounds.
    /// </summary>
    public bool IsAliveAtGrid(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
        return _current[x, y];
    }

    /// <summary>
    /// True if the given world position is inside an alive cell.
    /// </summary>
    public bool IsAliveAtWorldPosition(Vector2 world)
    {
        Vector2Int c = WorldToCell(world);
        return IsAliveAtGrid(c.x, c.y);
    }

    private void Start()
    {
        if (levelPreset != null)
            LoadLevel(levelPreset);
        else
            LoadLevel(GetOrCreateDefaultPreset());
    }

    /// <summary>
    /// Fallback when no preset is assigned: in-memory default (small glider).
    /// </summary>
    private static GameOfLifeLevelPreset GetOrCreateDefaultPreset()
    {
        var p = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        p.gridWidth = 12;
        p.gridHeight = 12;
        p.initialLiveCells.Clear();
        // Glider
        p.initialLiveCells.Add(new Vector2Int(1, 0));
        p.initialLiveCells.Add(new Vector2Int(2, 1));
        p.initialLiveCells.Add(new Vector2Int(0, 2));
        p.initialLiveCells.Add(new Vector2Int(1, 2));
        p.initialLiveCells.Add(new Vector2Int(2, 2));
        return p;
    }

    private void Update()
    {
        if (!_initialized) return;
        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            _stepTimer += stepInterval;
            StepSimulation();
        }
    }

    /// <summary>
    /// Load or switch to a level preset. Rebuilds the grid and resets the simulation.
    /// </summary>
    public void LoadLevel(GameOfLifeLevelPreset preset)
    {
        if (preset == null) return;

        ClearGrid();
        _width = preset.gridWidth;
        _height = preset.gridHeight;
        _current = new bool[_width, _height];
        _next = new bool[_width, _height];

        if (centerGridOnLoad)
            gridOrigin = new Vector2(-_width * cellSize * 0.5f, -_height * cellSize * 0.5f);

        foreach (Vector2Int p in preset.GetInitialLiveCells())
        {
            if (p.x >= 0 && p.x < _width && p.y >= 0 && p.y < _height)
                _current[p.x, p.y] = true;
        }

        BuildCellViews();
        _stepTimer = stepInterval;
        _initialized = true;
    }

    private void ClearGrid()
    {
        if (_cells == null) return;
        for (int x = 0; x < _cells.GetLength(0); x++)
        {
            for (int y = 0; y < _cells.GetLength(1); y++)
            {
                if (_cells[x, y] != null && _cells[x, y].gameObject != null)
                    Destroy(_cells[x, y].gameObject);
            }
        }
        _cells = null;
        _initialized = false;
    }

    private void BuildCellViews()
    {
        _cells = new GameOfLifeCellView[_width, _height];
        Sprite cellSprite = CreateSquareSprite();

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                GameObject go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(
                    gridOrigin.x + x * cellSize,
                    gridOrigin.y + y * cellSize,
                    0f
                );
                go.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cellSprite;
                sr.sortingOrder = 0;
                sr.color = _current[x, y] ? aliveColor : deadColor;

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = Vector2.one;
                col.enabled = _current[x, y];

                var view = go.AddComponent<GameOfLifeCellView>();
                view.SetReferences(sr, col);
                _cells[x, y] = view;
            }
        }
    }

    private static Sprite _sharedSprite;

    private static Sprite CreateSquareSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _sharedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f // Pixels Per Unit
        );
        return _sharedSprite;
    }

    private void StepSimulation()
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                int n = CountNeighbors(x, y);
                bool alive = _current[x, y];
                _next[x, y] = alive ? (n == 2 || n == 3) : (n == 3);
            }
        }

        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _current[x, y] = _next[x, y];

        RefreshViews();
    }

    private int CountNeighbors(int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < _width && ny >= 0 && ny < _height && _current[nx, ny])
                    count++;
            }
        return count;
    }

    private void RefreshViews()
    {
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _cells[x, y].SetAlive(_current[x, y], aliveColor, deadColor);
    }

    private void OnDestroy()
    {
        ClearGrid();
    }
}
