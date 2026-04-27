using System;
using System.Collections;
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
    [Tooltip("Total world-space width/height the grid should span. Cell size is computed automatically from this and the level's dimensions.")]
    [SerializeField] private float targetGridWorldSize = 10f;

    [Tooltip("If true, grid is centered at world (0,0) when loading a level. Otherwise uses Grid Origin.")]
    [SerializeField] private bool centerGridOnLoad = true;

    [Tooltip("World position of the bottom-left corner of the grid — cell (0,0) starts here (used when Center Grid On Load is false).")]
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;

    [Header("Simulation")]
    [Tooltip("Time in seconds between Game of Life steps")]
    [SerializeField] private float stepInterval = 0.4f;

    [Tooltip("If true, the simulation starts running immediately on load. If false, it waits for an explicit StartSimulation call (e.g. from the cursor).")]
    [SerializeField] private bool autoStart = false;

    [Header("Visual")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = new Color(0.2f, 0.2f, 0.5f, 0.5f);

    [Header("Cursor")]
    [Tooltip("Optional cursor controller that will be positioned at the level's cursor start cell, if defined.")]
    [SerializeField] private CursorController cursorController;

    [Header("Main menu")]
    [Tooltip("After picking an easy/hard coin, load the next level in LevelManager.")]
    [SerializeField] private bool difficultyCoinsLoadNextLevel = true;

    [Header("Transitions")]
    [Tooltip("Optional intermission shown between levels. If set, the player must click to continue after the fade to black.")]
    [SerializeField] private LevelIntermission levelIntermission;

    // State
    private float cellSize = 0.5f;
    private int _width;
    private int _height;
    private bool[,] _current;
    private bool[,] _next;
    private float _stepTimer;
    private float _stepCount;
    private GameOfLifeCellView[,] _cells;
    private System.Collections.Generic.List<GameObject> _spawnedCollectibles = new System.Collections.Generic.List<GameObject>();
    private System.Collections.Generic.List<GameObject> _spawnedDifficultyCoins = new System.Collections.Generic.List<GameObject>();
    private bool _initialized;
    private bool _running;
    private bool _inTransition;
    private int _remainingCollectibles;
    private LevelManager _levelManager;
    private float _baseStepInterval;

    public int GridWidth => _width;
    public int GridHeight => _height;
    public float CellSize => cellSize;
    public Vector2 GridOrigin => gridOrigin;
    public bool IsInitialized => _initialized;
    public bool IsRunning => _running;

    /// <summary>
    /// Current time in seconds between simulation steps. Smaller = faster.
    /// </summary>
    public float StepInterval
    {
        get => stepInterval;
        set => stepInterval = Mathf.Max(0.01f, value);
    }

    /// <summary>
    /// Make the simulation run faster by multiplying its speed.
    /// For example, multiplier 1.5 makes it 1.5x faster (smaller step interval).
    /// </summary>
    public void MultiplyStepSpeed(float speedMultiplier)
    {
        if (speedMultiplier <= 0f) return;
        StepInterval = StepInterval / speedMultiplier;
        // Keep timer in range so the new interval takes effect cleanly.
        _stepTimer = Mathf.Min(_stepTimer, StepInterval);
    }

    /// <summary>
    /// World position for cell (x, y) matching how cells are built in BuildCellViews.
    /// </summary>
    public Vector2 CellToWorld(int x, int y)
    {
        return gridOrigin + new Vector2(x * cellSize, y * cellSize);
    }

    /// <summary>
    /// Fired each time the simulation starts running (e.g. after the player's first click).
    /// </summary>
    public event Action OnSimulationStarted;

    /// <summary>
    /// Start running the simulation (if initialized). Used by cursor click.
    /// </summary>
    public void StartSimulation()
    {
        if (!_initialized) return;
        _running = true;
        _stepTimer = stepInterval;
        _stepCount = 0;
        OnSimulationStarted?.Invoke();
    }

    /// <summary>
    /// Stop/pause the simulation.
    /// </summary>
    public void StopSimulation()
    {
        _running = false;
    }

    /// <summary>
    /// Resume stepping after an easy-mode hit pause. Does not fire <see cref="OnSimulationStarted"/> (timer UI keeps running).
    /// </summary>
    public void ResumeSimulationAfterHit()
    {
        if (!_initialized) return;
        _running = true;
        _stepTimer = stepInterval;
    }

    /// <summary>
    /// Fired when the player dies (before the death transition begins).
    /// </summary>
    public event Action OnPlayerDeath;

    /// <summary>
    /// Called when the player dies (e.g. cursor hits a live cell).
    /// Plays the same transition but reloads the current level instead of advancing.
    /// </summary>
    public void OnPlayerDied()
    {
        if (_inTransition) return;
        GameDifficulty.NotifyRunPlayerDied();
        OnPlayerDeath?.Invoke();
        StartCoroutine(LevelDeathRoutine());
    }

    /// <summary>
    /// Fired when all collectibles have been collected and the level objective is complete.
    /// </summary>
    public event Action OnObjectiveCompleted;

    public void OnCollectibleCollected()
    {
        if (_remainingCollectibles <= 0) return;
        _remainingCollectibles--;
        if (_remainingCollectibles == 0)
        {
            OnObjectiveCompleted?.Invoke();
            RequestAdvanceToNextLevelWithTransition();
        }
    }

    /// <summary>
    /// Same row wipe → next level (black build) → reveal as when the last collectible is taken.
    /// Used for difficulty-coin advance so the menu gets the same transition as completing a level.
    /// </summary>
    public void RequestAdvanceToNextLevelWithTransition()
    {
        if (_inTransition) return;
        _inTransition = true;
        StartCoroutine(LevelAdvanceTransitionCoroutine());
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

    private void Awake()
    {
        // Remember the inspector-configured base interval so we can reset
        // between levels after collectibles speed it up.
        _baseStepInterval = stepInterval;
    }

    private void Start()
    {
        // LevelManager also loads in its Start(); avoid double LoadLevel and race on script order.
        var lm = FindFirstObjectByType<LevelManager>();
        if (lm != null && lm.LevelCount > 0)
            StartCoroutine(DeferredBootstrapWithLevelManager());
        else
            BootstrapLoadAndMaybeAutoStart();
    }

    /// <summary>
    /// Load from this component's preset when no LevelManager drives levels.
    /// </summary>
    private void BootstrapLoadAndMaybeAutoStart()
    {
        if (levelPreset != null)
            LoadLevel(levelPreset);
        else
            LoadLevel(GetOrCreateDefaultPreset());

        if (autoStart || cursorController == null)
            StartSimulation();
    }

    private IEnumerator DeferredBootstrapWithLevelManager()
    {
        yield return null;
        if (!_initialized)
            BootstrapLoadAndMaybeAutoStart();
        else if (autoStart || cursorController == null)
            StartSimulation();
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
        if (!_initialized || !_running) return;
        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            if (_stepCount % 4 == 0)
            {
                AudioManager.Instance.PlayHighSound();
            }
            else
            {
                if (stepInterval < 0.08f)
                {
                    if (_stepCount % 2 != 0)
                    {
                        AudioManager.Instance.PlayLowSound();
                    }
                }
                else
                {
                    AudioManager.Instance.PlayLowSound();
                }
            }
            _stepCount++;
            _stepTimer += stepInterval;
            StepSimulation();
        }
    }

    /// <summary>
    /// Load or switch to a level preset. Rebuilds the grid and resets the simulation.
    /// If startBlack is true, the newly built grid is rendered fully black so that
    /// a reveal transition can be performed afterwards.
    /// </summary>
    public void LoadLevel(GameOfLifeLevelPreset preset, bool startBlack = false)
    {
        if (preset == null) return;

        ClearGrid();

        // Reset timestep back to the base value for this level.
        StepInterval = _baseStepInterval;
        _width = preset.gridWidth;
        _height = preset.gridHeight;
        cellSize = targetGridWorldSize / Mathf.Max(_width, _height);
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
        SpawnCollectibles(preset);
        SpawnDifficultyCoins(preset);
        PositionCursorStart(preset);
        FitCameraToGrid();

        if (cursorController != null)
        {
            var starts = preset.GetCursorStartCells();
            if (starts == null || starts.Count == 0)
                cursorController.PrepareForNewLevel();
        }

        if (startBlack && _cells != null)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cellView = _cells[x, y];
                    if (cellView != null)
                        cellView.SetColorOnly(Color.black);
                }
            }

            SetOverlayPickupsVisible(false);
        }

        _stepTimer = stepInterval;
        _initialized = true;
        _running = false;

        AudioManager.Instance.ResetCollectionPitch();
    }

    private void ClearGrid()
    {
        if (_cells != null)
        {
            for (int x = 0; x < _cells.GetLength(0); x++)
            {
                for (int y = 0; y < _cells.GetLength(1); y++)
                {
                    if (_cells[x, y] != null && _cells[x, y].gameObject != null)
                        Destroy(_cells[x, y].gameObject);
                }
            }
            _cells = null;
        }

        if (_spawnedCollectibles != null)
        {
            for (int i = 0; i < _spawnedCollectibles.Count; i++)
            {
                if (_spawnedCollectibles[i] != null)
                    Destroy(_spawnedCollectibles[i]);
            }
            _spawnedCollectibles.Clear();
        }

        if (_spawnedDifficultyCoins != null)
        {
            for (int i = 0; i < _spawnedDifficultyCoins.Count; i++)
            {
                if (_spawnedDifficultyCoins[i] != null)
                    Destroy(_spawnedDifficultyCoins[i]);
            }
            _spawnedDifficultyCoins.Clear();
        }

        _initialized = false;
        _running = false;
        // Do not clear _inTransition here — LoadLevel runs mid wipe/reveal; clearing it allowed a second
        // transition (e.g. collectible triggers while sprites were hidden) and broke later level advances.
        _remainingCollectibles = 0;
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

    private void SpawnCollectibles(GameOfLifeLevelPreset preset)
    {
        var positions = preset.GetCollectibleCells();
        _remainingCollectibles = positions != null ? positions.Count : 0;
        if (positions == null || positions.Count == 0) return;

        Sprite sprite = CreateSquareSprite();

        foreach (var cell in positions)
        {
            if (cell.x < 0 || cell.x >= _width || cell.y < 0 || cell.y >= _height)
                continue;

            Vector2 worldPos = CellToWorld(cell.x, cell.y);

            GameObject go = new GameObject($"Collectible_{cell.x}_{cell.y}");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(worldPos.x, worldPos.y, 0f);
            go.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1.0f, 0.85f, 0.2f); // match editor's yellow collectible color
            sr.sortingOrder = 1; // render above normal cells
            // If we're in the middle of a transition, keep collectibles hidden until it finishes.
            if (_inTransition)
                sr.enabled = false;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<CollectibleCell>();

            _spawnedCollectibles.Add(go);
        }
    }

    private void SpawnDifficultyCoins(GameOfLifeLevelPreset preset)
    {
        Sprite sprite = CreateSquareSprite();

        void SpawnOne(Vector2Int cell, DifficultySelectCell.DifficultyChoice choice, Color color)
        {
            if (cell.x < 0 || cell.x >= _width || cell.y < 0 || cell.y >= _height)
                return;

            Vector2 worldPos = CellToWorld(cell.x, cell.y);
            GameObject go = new GameObject($"DifficultyCoin_{choice}_{cell.x}_{cell.y}");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(worldPos.x, worldPos.y, 0f);
            go.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 1;
            if (_inTransition)
                sr.enabled = false;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            var d = go.AddComponent<DifficultySelectCell>();
            d.Init(choice, difficultyCoinsLoadNextLevel);

            _spawnedDifficultyCoins.Add(go);
        }

        var easy = preset.GetEasyModeCoinCells();
        if (easy != null)
        {
            foreach (var c in easy)
                SpawnOne(c, DifficultySelectCell.DifficultyChoice.Easy, new Color(1.0f, 0.85f, 0.2f));
        }

        var hard = preset.GetHardModeCoinCells();
        if (hard != null)
        {
            foreach (var c in hard)
                SpawnOne(c, DifficultySelectCell.DifficultyChoice.Hard, new Color(1.0f, 0.85f, 0.2f));
        }

        var editor = preset.GetLevelEditorCoinCells();
        if (editor != null)
        {
            foreach (var c in editor)
                SpawnOne(c, DifficultySelectCell.DifficultyChoice.LevelEditor, new Color(1.0f, 0.85f, 0.2f));
        }
    }

    private void SetOverlayPickupsVisible(bool visible)
    {
        if (_spawnedCollectibles != null)
        {
            for (int i = 0; i < _spawnedCollectibles.Count; i++)
            {
                var go = _spawnedCollectibles[i];
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.enabled = visible;
                var col = go.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = visible;
            }
        }

        if (_spawnedDifficultyCoins != null)
        {
            for (int i = 0; i < _spawnedDifficultyCoins.Count; i++)
            {
                var go = _spawnedDifficultyCoins[i];
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.enabled = visible;
                var col = go.GetComponent<Collider2D>();
                if (col != null)
                    col.enabled = visible;
            }
        }
    }

    private IEnumerator LevelAdvanceTransitionCoroutine()
    {
        StopSimulation();

        // Small pause before the wipe.
        yield return new WaitForSeconds(0.5f);

        SetOverlayPickupsVisible(false);

        if (_cells != null)
        {
            // Fade current level to black row by row from top (highest y) to bottom.
            for (int y = _height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cellView = _cells[x, y];
                    if (cellView != null)
                        cellView.SetColorOnly(Color.black);
                }
                yield return new WaitForSeconds(0.01f);
            }
        }

        if (_levelManager == null)
            _levelManager = FindFirstObjectByType<LevelManager>();

        if (levelIntermission != null && _levelManager != null && _levelManager.IsPlayableLevel(_levelManager.CurrentLevelIndex))
        {
            int completedLevelNumber = _levelManager.CurrentLevelIndex - _levelManager.FirstPlayableLevelIndex + 1;
            bool isFinalLevel = _levelManager.IsLastPlayableLevel(_levelManager.CurrentLevelIndex);
            var completedPreset = _levelManager.CurrentLevel;
            string leaderboardKey = completedPreset != null ? completedPreset.LeaderboardKey : null;
            yield return levelIntermission.ShowAndWaitForClick(completedLevelNumber, leaderboardKey, isFinalLevel);
        }

        if (_levelManager != null)
        {
            // Load the next level with its grid already built but rendered black,
            // so the reveal below uses the correct new dimensions.
            _levelManager.LoadNextLevel(true);
            // Wait a frame so the new level can finish loading/building its grid.
            yield return null;
        }

        // Now reveal the NEW level from black, bottom to top, using the new grid size.
        if (_cells != null)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    bool alive = _current[x, y];
                    var cellView = _cells[x, y];
                    if (cellView != null)
                        cellView.SetAlive(alive, aliveColor, deadColor);
                }
                yield return new WaitForSeconds(0.01f);
            }
        }

        SetOverlayPickupsVisible(true);

        _inTransition = false;
    }

    private IEnumerator LevelDeathRoutine()
    {
        if (_inTransition) yield break;
        _inTransition = true;

        StopSimulation();

        // Small pause before the wipe.
        yield return new WaitForSeconds(0.8f);

        SetOverlayPickupsVisible(false);

        if (_cells != null)
        {
            // Fade current level to black row by row from top (highest y) to bottom.
            for (int y = _height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cellView = _cells[x, y];
                    if (cellView != null)
                        cellView.SetColorOnly(Color.black);
                }
                yield return new WaitForSeconds(0.01f);
            }
        }

        // Reload the current level.
        if (_levelManager == null)
            _levelManager = FindFirstObjectByType<LevelManager>();

        if (_levelManager != null)
        {
            int index = _levelManager.CurrentLevelIndex;
            // Reload the same level, with its new grid size (if edited) built black.
            _levelManager.LoadLevelByIndex(index, true);
            // Wait a frame so the new level can finish loading/building its grid.
            yield return null;
        }

        // Reveal the reloaded level from black, bottom to top.
        if (_cells != null)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    bool alive = _current[x, y];
                    var cellView = _cells[x, y];
                    if (cellView != null)
                        cellView.SetAlive(alive, aliveColor, deadColor);
                }
                yield return new WaitForSeconds(0.01f);
            }
        }

        SetOverlayPickupsVisible(true);

        _inTransition = false;
    }

    private void PositionCursorStart(GameOfLifeLevelPreset preset)
    {
        if (cursorController == null) return;
        var starts = preset.GetCursorStartCells();
        if (starts == null || starts.Count == 0) return;

        var cell = starts[0];
        if (cell.x < 0 || cell.x >= _width || cell.y < 0 || cell.y >= _height)
            return;

        Vector2 worldPos = CellToWorld(cell.x, cell.y);
        cursorController.PlaceAtStart(worldPos);
    }

    /// <summary>
    /// Adjust the main orthographic camera so the entire grid fits on screen
    /// regardless of level dimensions (e.g. 20x20, 33x33, 42x42, etc.).
    /// </summary>
    private void FitCameraToGrid()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // World-space size of the grid.
        float gridWorldWidth = _width * cellSize;
        float gridWorldHeight = _height * cellSize;

        // Bottom-left corner and center of the grid in world space.
        Vector2 min = gridOrigin;
        Vector2 center = min + new Vector2(gridWorldWidth * 0.5f, gridWorldHeight * 0.5f);

        // Center the camera on the grid while preserving its Z.
        Vector3 camPos = cam.transform.position;
        cam.transform.position = new Vector3(center.x, center.y, camPos.z);

        // Compute the orthographic size needed to fit width & height, with padding.
        float aspect = (float)Screen.width / Screen.height;
        float sizeByHeight = gridWorldHeight * 0.5f;
        float sizeByWidth = gridWorldWidth * 0.5f / Mathf.Max(aspect, 0.0001f);

        const float paddingFactor = 1.05f;
        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth) * paddingFactor;
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
