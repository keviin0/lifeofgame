using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game level editor for Game of Life levels. Uses the same cell-drawing
/// approach as <see cref="GameOfLifeSimulation"/>, but draws into a square,
/// resizable grid with visible outlines so the player can paint level data.
///
/// Tools: paint live cells, paint coins (collectibles), place a single cursor
/// start, and an eraser. Left-mouse paints with the selected tool; right-mouse
/// always erases.
///
/// All UI lives on the scene's Canvas; this script just wires the buttons up
/// to the editor logic.
/// </summary>
public class LevelEditor : MonoBehaviour
{
    public enum Tool { LiveCell, Coin, CursorStart, Eraser }

    [Header("Grid")]
    [Tooltip("Starting square grid dimension (N x N). Players can resize this live from the editor panel.")]
    [Min(1)][SerializeField] private int gridSize = 16;

    [Tooltip("Maximum allowed grid dimension. The base64 share format supports up to 255, but 64 is plenty for most levels.")]
    [Min(1)][SerializeField] private int maxGridSize = 64;

    [Tooltip("How much one tap of the grid +/- buttons changes the dimension.")]
    [Min(1)][SerializeField] private int gridSizeStep = 2;

    [Tooltip("Total world-space width/height the grid should span. Cell size is computed from this and the grid dimension.")]
    [SerializeField] private float targetGridWorldSize = 10f;

    [Header("Visual")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = new Color(0.2f, 0.2f, 0.5f, 0.5f);
    [SerializeField] private Color coinColor = new Color(1.0f, 0.85f, 0.2f);
    [SerializeField] private Color cursorStartColor = new Color(0.25f, 0.9f, 1f);
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.95f);

    [Range(0f, 0.3f)]
    [Tooltip("Fraction of the cell size used as spacing between cells, which becomes the visible grid outline.")]
    [SerializeField] private float outlineThickness = 0.06f;

    [Header("Editing")]
    [SerializeField] private Tool currentTool = Tool.LiveCell;

    [Header("UI - Grid Dimensions")]
    [SerializeField] private Button increaseGridSizeButton;
    [SerializeField] private Button decreaseGridSizeButton;
    [SerializeField] private TMP_Text gridDimensionsLabel;

    [Header("UI - Brushes")]
    [SerializeField] private Button aliveCellBrushButton;
    [SerializeField] private Button coinCellBrushButton;
    [SerializeField] private Button cursorBrushButton;
    [SerializeField] private Button eraseBrushButton;

    [Tooltip("Optional: scale applied to the currently selected brush button, so the active tool is obvious.")]
    [SerializeField] private float selectedBrushScale = 1.15f;

    [Header("UI - Share")]
    [SerializeField] private Button shareButton;

    [Header("UI - Test")]
    [SerializeField] private Button testButton;

    [Header("UI - Requirements")]
    [Tooltip("Container hidden once all requirements are satisfied; shown otherwise.")]
    [SerializeField] private GameObject conditionsTextContainer;

    [Tooltip("Text shown in green when at least one coin is placed, red otherwise.")]
    [SerializeField] private TMP_Text coinRequirementText;

    [Tooltip("Text shown in green when exactly one cursor start is placed, red otherwise.")]
    [SerializeField] private TMP_Text cursorRequirementText;

    [SerializeField] private Color requirementMetColor = new Color(0.25f, 0.85f, 0.35f);
    [SerializeField] private Color requirementUnmetColor = new Color(0.95f, 0.2f, 0.2f);

    [Header("UI - Share Confirmation")]
    [Tooltip("Optional message panel (e.g. \"link copied to clipboard!\") faded in briefly after a successful share.")]
    [SerializeField] private CanvasGroup messagePanelCanvasGroup;

    [Min(0f)][SerializeField] private float messageFadeInDuration = 0.2f;
    [Min(0f)][SerializeField] private float messageHoldDuration = 3f;
    [Min(0f)][SerializeField] private float messageFadeOutDuration = 0.4f;

    [Header("UI - Press Feedback")]
    [Tooltip("How small the share/test buttons shrink to when pressed (1 = no shrink).")]
    [Range(0.5f, 1f)][SerializeField] private float pressShrinkScale = 0.85f;

    [Tooltip("Seconds spent shrinking on press.")]
    [Min(0f)][SerializeField] private float pressShrinkDuration = 0.06f;

    [Tooltip("Seconds spent bouncing back to the original scale.")]
    [Min(0f)][SerializeField] private float pressRecoverDuration = 0.22f;

    [Tooltip("Scene name loaded when the Test button is pressed. Must contain a GameOfLifeSimulation (and ideally a LevelManager). Must be in Build Settings.")]
    [SerializeField] private string testSceneName = "Kevin's Scene";

    [Header("UI - Main Menu")]
    [SerializeField] private Button mainMenuButton;

    [Tooltip("Scene name loaded when the Main Menu button is pressed. Must be in Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "Kevin's Scene";

    // Cell state values (match the 2-bit encoding in GameOfLifeLevelPreset.ToBase64).
    private const int Empty = 0;
    private const int Alive = 1;
    private const int Coin = 2;
    private const int Cursor = 3;

    // Per-button press-bounce coroutine handles, so re-pressing the same
    // button mid-animation cleanly restarts instead of overlapping tweens.
    private readonly Dictionary<Button, Coroutine> _pressBounceRoutines = new();

    // Active share-confirmation message tween, if any.
    private Coroutine _messageRoutine;

    // Runtime state
    private int _size;
    private float _cellSize;
    private Vector2 _origin;
    private int[,] _data;
    private SpriteRenderer[,] _fills;
    private Vector2Int? _cursorStart;
    private GameObject _root;
    private Camera _cam;

    private static Sprite _sharedSprite;

    private void Start()
    {
        _cam = Camera.main;
        _data = new int[gridSize, gridSize];
        UnityEngine.Cursor.visible = true;

        // The message panel is left visible in the editor for layout purposes;
        // hide it at runtime until a share triggers the fade-in.
        if (messagePanelCanvasGroup != null)
            messagePanelCanvasGroup.alpha = 0f;

        WireUI();

        // Returning from a test run: re-import the level the player tested so
        // the editor doesn't reset to a blank grid. Falls through to a normal
        // Rebuild() if there's nothing staged or the import fails.
        if (LevelEditorTestSession.TryConsumeRestore(out string restoreCode))
        {
            if (!ImportCode(restoreCode, out string importError))
            {
                Debug.LogWarning($"[LevelEditor] Could not restore tested level: {importError}");
                Rebuild();
            }
        }
        else
        {
            Rebuild();
        }

        UpdateBrushSelectionVisual();
    }

    private void OnDestroy()
    {
        DestroyGrid();
    }

    private void WireUI()
    {
        if (increaseGridSizeButton != null)
            increaseGridSizeButton.onClick.AddListener(() => ChangeGridSize(+gridSizeStep));
        if (decreaseGridSizeButton != null)
            decreaseGridSizeButton.onClick.AddListener(() => ChangeGridSize(-gridSizeStep));

        if (aliveCellBrushButton != null)
            aliveCellBrushButton.onClick.AddListener(() => SelectTool(Tool.LiveCell));
        if (coinCellBrushButton != null)
            coinCellBrushButton.onClick.AddListener(() => SelectTool(Tool.Coin));
        if (cursorBrushButton != null)
            cursorBrushButton.onClick.AddListener(() => SelectTool(Tool.CursorStart));
        if (eraseBrushButton != null)
            eraseBrushButton.onClick.AddListener(() => SelectTool(Tool.Eraser));

        if (shareButton != null)
            shareButton.onClick.AddListener(() => { PlayPressBounce(shareButton); OnSharePressed(); });

        if (testButton != null)
            testButton.onClick.AddListener(() => { PlayPressBounce(testButton); OnTestPressed(); });

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuPressed);
    }

    private void OnTestPressed()
    {
        if (string.IsNullOrEmpty(testSceneName))
        {
            Debug.LogWarning("[LevelEditor] Test scene name is empty.");
            return;
        }

        if (!IsLevelPlayable(out string reason))
        {
            Debug.LogWarning($"[LevelEditor] Cannot test: {reason}");
            return;
        }

        // Defer the scene load until the press-bounce on the Test button has
        // had a chance to play; otherwise the editor scene unloads before any
        // of the animation is visible.
        StartCoroutine(LoadTestSceneAfterPressBounce());
    }

    private IEnumerator LoadTestSceneAfterPressBounce()
    {
        yield return new WaitForSecondsRealtime(pressShrinkDuration + pressRecoverDuration);

        string code = ExportCode();
        LevelEditorTestSession.Begin(code, SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(testSceneName);
    }

    private void OnMainMenuPressed()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("[LevelEditor] Main menu scene name is empty.");
            return;
        }
        LevelEditorTestSession.End();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _fills == null) return;

        bool leftDown = Input.GetMouseButton(0);
        bool rightDown = Input.GetMouseButton(1);
        if (!leftDown && !rightDown) return;

        // Don't paint when the pointer is over any uGUI element (buttons, panels, etc).
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 mw = _cam.ScreenToWorldPoint(Input.mousePosition);
        int x = Mathf.FloorToInt((mw.x - _origin.x) / _cellSize);
        int y = Mathf.FloorToInt((mw.y - _origin.y) / _cellSize);
        if (x < 0 || y < 0 || x >= _size || y >= _size) return;

        Tool tool = rightDown ? Tool.Eraser : currentTool;
        ApplyTool(x, y, tool);
    }

    /// <summary>
    /// Tally of cells that matter for the "is this level playable?" check.
    /// </summary>
    private struct CellCounts
    {
        public int coins;
        public int cursors;
        public bool CoinRequirementMet => coins >= 1;
        public bool CursorRequirementMet => cursors == 1;
        public bool AllMet => CoinRequirementMet && CursorRequirementMet;
    }

    private CellCounts CountCells()
    {
        var c = new CellCounts();
        if (_data == null) return c;
        for (int x = 0; x < _size; x++)
        {
            for (int y = 0; y < _size; y++)
            {
                int v = _data[x, y];
                if (v == Coin) c.coins++;
                else if (v == Cursor) c.cursors++;
            }
        }
        return c;
    }

    /// <summary>
    /// A level is "playable" only if it has at least one coin and exactly one
    /// cursor start. Other content (live cells, grid size) is unconstrained.
    /// </summary>
    private bool IsLevelPlayable(out string reason)
    {
        if (_data == null)
        {
            reason = "grid not initialized";
            return false;
        }

        var c = CountCells();
        if (!c.CursorRequirementMet)
        {
            reason = c.cursors == 0
                ? "place exactly one cursor start cell"
                : $"only one cursor start cell allowed (found {c.cursors})";
            return false;
        }
        if (!c.CoinRequirementMet)
        {
            reason = "place at least one coin cell";
            return false;
        }

        reason = null;
        return true;
    }

    private void UpdateActionButtonsInteractable()
    {
        var c = CountCells();
        bool playable = c.AllMet;

        // When requirements are met, swap the requirements panel out for the
        // action buttons (and vice-versa). The buttons are also marked
        // non-interactable as a belt-and-braces guard if they're ever shown.
        if (testButton != null)
        {
            testButton.gameObject.SetActive(playable);
            testButton.interactable = playable;
        }
        if (shareButton != null)
        {
            shareButton.gameObject.SetActive(playable);
            shareButton.interactable = playable;
        }
        if (conditionsTextContainer != null)
            conditionsTextContainer.SetActive(!playable);

        if (coinRequirementText != null)
            coinRequirementText.color = c.CoinRequirementMet ? requirementMetColor : requirementUnmetColor;
        if (cursorRequirementText != null)
            cursorRequirementText.color = c.CursorRequirementMet ? requirementMetColor : requirementUnmetColor;
    }

    private void ApplyTool(int x, int y, Tool tool)
    {
        int desired = tool switch
        {
            Tool.LiveCell => Alive,
            Tool.Coin => Coin,
            Tool.CursorStart => Cursor,
            Tool.Eraser => Empty,
            _ => Empty,
        };

        // Only one cursor start allowed: clear the previous one before setting a new one.
        if (desired == Cursor && _cursorStart.HasValue && (_cursorStart.Value.x != x || _cursorStart.Value.y != y))
        {
            Vector2Int prev = _cursorStart.Value;
            _data[prev.x, prev.y] = Empty;
            PaintCell(prev.x, prev.y);
            _cursorStart = null;
        }

        if (_data[x, y] == desired) return;

        _data[x, y] = desired;
        if (desired == Cursor)
            _cursorStart = new Vector2Int(x, y);
        else if (_cursorStart.HasValue && _cursorStart.Value.x == x && _cursorStart.Value.y == y)
            _cursorStart = null;

        PaintCell(x, y);
        UpdateActionButtonsInteractable();
    }

    private void SelectTool(Tool t)
    {
        currentTool = t;
        UpdateBrushSelectionVisual();
    }

    private void UpdateBrushSelectionVisual()
    {
        SetBrushSelected(aliveCellBrushButton, currentTool == Tool.LiveCell);
        SetBrushSelected(coinCellBrushButton, currentTool == Tool.Coin);
        SetBrushSelected(cursorBrushButton, currentTool == Tool.CursorStart);
        SetBrushSelected(eraseBrushButton, currentTool == Tool.Eraser);
    }

    private void SetBrushSelected(Button button, bool selected)
    {
        if (button == null) return;
        float s = selected ? selectedBrushScale : 1f;
        button.transform.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>
    /// Quick "shrink + spring back" tactile feedback for a button press.
    /// </summary>
    private void PlayPressBounce(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy) return;
        if (_pressBounceRoutines.TryGetValue(button, out var running) && running != null)
            StopCoroutine(running);
        _pressBounceRoutines[button] = StartCoroutine(PressBounceCoroutine(button.transform));
    }

    private IEnumerator PressBounceCoroutine(Transform t)
    {
        Vector3 baseScale = Vector3.one;

        // Shrink quickly...
        float elapsed = 0f;
        while (elapsed < pressShrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = pressShrinkDuration > 0f ? Mathf.Clamp01(elapsed / pressShrinkDuration) : 1f;
            float s = Mathf.Lerp(1f, pressShrinkScale, k);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // ...then bounce back with a slight overshoot for the spring feel.
        elapsed = 0f;
        while (elapsed < pressRecoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = pressRecoverDuration > 0f ? Mathf.Clamp01(elapsed / pressRecoverDuration) : 1f;
            float s = Mathf.Lerp(pressShrinkScale, 1f, EaseOutBack(k));
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        t.localScale = baseScale;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t1 = t - 1f;
        return 1f + c3 * t1 * t1 * t1 + c1 * t1 * t1;
    }

    private void ChangeGridSize(int delta)
    {
        int next = Mathf.Clamp(gridSize + delta, 1, maxGridSize);
        if (next == gridSize) return;
        gridSize = next;
        Rebuild();
    }

    /// <summary>
    /// Destroy the current grid and rebuild it from <see cref="gridSize"/>.
    /// Preserves any painted data that overlaps the new dimensions.
    /// </summary>
    private void Rebuild()
    {
        DestroyGrid();

        _size = Mathf.Max(1, gridSize);
        _cellSize = targetGridWorldSize / _size;
        _origin = new Vector2(-_size * _cellSize * 0.5f, -_size * _cellSize * 0.5f);

        if (_data == null || _data.GetLength(0) != _size || _data.GetLength(1) != _size)
        {
            int[,] old = _data;
            int oldSize = old != null ? old.GetLength(0) : 0;
            _data = new int[_size, _size];
            int copy = Mathf.Min(oldSize, _size);
            for (int x = 0; x < copy; x++)
                for (int y = 0; y < copy; y++)
                    _data[x, y] = old[x, y];

            if (_cursorStart.HasValue && (_cursorStart.Value.x >= _size || _cursorStart.Value.y >= _size))
                _cursorStart = null;
        }

        _root = new GameObject("LevelEditorGrid");
        _root.transform.SetParent(transform, false);

        // Outline: one solid rectangle covering the whole grid area. Cells are drawn
        // on top slightly smaller than their slot so the spacing reveals the outline color.
        var bg = new GameObject("GridOutline");
        bg.transform.SetParent(_root.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        bg.transform.localScale = new Vector3(_size * _cellSize, _size * _cellSize, 1f);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = GetSharedSprite();
        bgSr.color = outlineColor;
        bgSr.sortingOrder = -1;

        _fills = new SpriteRenderer[_size, _size];
        float fillScale = Mathf.Clamp01(1f - outlineThickness);

        for (int x = 0; x < _size; x++)
        {
            for (int y = 0; y < _size; y++)
            {
                var go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = new Vector3(
                    _origin.x + (x + 0.5f) * _cellSize,
                    _origin.y + (y + 0.5f) * _cellSize,
                    0f);
                go.transform.localScale = new Vector3(_cellSize * fillScale, _cellSize * fillScale, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetSharedSprite();
                sr.sortingOrder = 0;
                _fills[x, y] = sr;
                PaintCell(x, y);
            }
        }

        FitCameraToGrid();
        UpdateGridDimensionsLabel();
        UpdateActionButtonsInteractable();
    }

    private void DestroyGrid()
    {
        if (_root != null)
        {
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
            _root = null;
        }
        _fills = null;
    }

    private void PaintCell(int x, int y)
    {
        if (_fills == null) return;
        var sr = _fills[x, y];
        if (sr == null) return;
        int v = _data[x, y];
        sr.color = v switch
        {
            Alive => aliveColor,
            Coin => coinColor,
            Cursor => cursorStartColor,
            _ => deadColor,
        };
        sr.sortingOrder = v == Empty ? 0 : 1;
    }

    private void UpdateGridDimensionsLabel()
    {
        if (gridDimensionsLabel == null) return;
        gridDimensionsLabel.text = $"{_size}\nx\n{_size}";
    }

    private void OnSharePressed()
    {
        if (!IsLevelPlayable(out string reason))
        {
            Debug.LogWarning($"[LevelEditor] Cannot share: {reason}");
            return;
        }

        string code = ExportCode();
        WebGLClipboard.Copy(code);
        Debug.Log($"[LevelEditor] Share code copied ({_size}x{_size}): {code}");
        ShowShareMessage();
    }

    /// <summary>
    /// Fade the share-confirmation message panel in, hold for a few seconds,
    /// then fade it back out. Restarts cleanly if the player shares again
    /// while the previous message is still visible.
    /// </summary>
    private void ShowShareMessage()
    {
        if (messagePanelCanvasGroup == null) return;
        if (_messageRoutine != null) StopCoroutine(_messageRoutine);
        _messageRoutine = StartCoroutine(ShowShareMessageRoutine());
    }

    private IEnumerator ShowShareMessageRoutine()
    {
        yield return TweenCanvasGroupAlpha(messagePanelCanvasGroup, 0f, 1f, messageFadeInDuration);
        yield return new WaitForSecondsRealtime(messageHoldDuration);
        yield return TweenCanvasGroupAlpha(messagePanelCanvasGroup, 1f, 0f, messageFadeOutDuration);
        _messageRoutine = null;
    }

    private static IEnumerator TweenCanvasGroupAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        if (duration <= 0f) { cg.alpha = to; yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        cg.alpha = to;
    }

    /// <summary>
    /// Build a <see cref="GameOfLifeLevelPreset"/>-compatible share code from the current grid.
    /// </summary>
    public string ExportCode()
    {
        var temp = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        try
        {
            temp.gridWidth = _size;
            temp.gridHeight = _size;
            temp.initialLiveCells.Clear();
            temp.collectibleCells.Clear();
            temp.cursorStartCells.Clear();

            for (int x = 0; x < _size; x++)
            {
                for (int y = 0; y < _size; y++)
                {
                    switch (_data[x, y])
                    {
                        case Alive: temp.initialLiveCells.Add(new Vector2Int(x, y)); break;
                        case Coin: temp.collectibleCells.Add(new Vector2Int(x, y)); break;
                        case Cursor: temp.cursorStartCells.Add(new Vector2Int(x, y)); break;
                    }
                }
            }

            return temp.ToBase64();
        }
        finally
        {
            if (Application.isPlaying) Destroy(temp);
            else DestroyImmediate(temp);
        }
    }

    /// <summary>
    /// Replace the grid with the contents of a share code. Non-square codes
    /// are accepted but padded out to a square (using the largest side) since
    /// the editor only supports square dimensions.
    /// </summary>
    public bool ImportCode(string code, out string error)
    {
        var temp = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        try
        {
            if (!temp.TryFromBase64(code, out error))
                return false;

            int size = Mathf.Clamp(Mathf.Max(temp.gridWidth, temp.gridHeight), 1, maxGridSize);
            gridSize = size;
            _data = new int[size, size];
            _cursorStart = null;

            foreach (var c in temp.initialLiveCells)
                if (InBounds(c, size)) _data[c.x, c.y] = Alive;
            foreach (var c in temp.collectibleCells)
                if (InBounds(c, size)) _data[c.x, c.y] = Coin;
            foreach (var c in temp.cursorStartCells)
            {
                if (!InBounds(c, size)) continue;
                _data[c.x, c.y] = Cursor;
                _cursorStart = c;
                break;
            }

            Rebuild();
            return true;
        }
        finally
        {
            if (Application.isPlaying) Destroy(temp);
            else DestroyImmediate(temp);
        }
    }

    private static bool InBounds(Vector2Int c, int size)
        => c.x >= 0 && c.y >= 0 && c.x < size && c.y < size;

    /// <summary>
    /// Fit the main orthographic camera around the grid, mirroring the
    /// behaviour of <see cref="GameOfLifeSimulation.FitCameraToGrid"/>.
    /// </summary>
    private void FitCameraToGrid()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float gridWorld = _size * _cellSize;
        Vector2 center = _origin + new Vector2(gridWorld, gridWorld) * 0.5f;
        Vector3 camPos = cam.transform.position;
        cam.transform.position = new Vector3(center.x, center.y, camPos.z);

        float aspect = (float)Screen.width / Screen.height;
        float sizeByHeight = gridWorld * 0.5f;
        float sizeByWidth = gridWorld * 0.5f / Mathf.Max(aspect, 0.0001f);

        const float paddingFactor = 1.1f;
        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth) * paddingFactor;
    }

    private static Sprite GetSharedSprite()
    {
        if (_sharedSprite != null) return _sharedSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _sharedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _sharedSprite;
    }
}
