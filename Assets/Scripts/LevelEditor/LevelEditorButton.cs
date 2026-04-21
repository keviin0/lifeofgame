using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a small "LEVEL EDITOR" button in the bottom-right corner of the screen.
/// Loads the LevelEditorScene when clicked.
/// </summary>
public class LevelEditorButton : MonoBehaviour
{
    private const string LEVEL_EDITOR_SCENE_NAME = "LevelEditorScene";
    private const float BUTTON_WIDTH = 400f;
    private const float BUTTON_HEIGHT = 100f;
    private const float MARGIN = 40f;
    private const int FONT_SIZE = 36;

    [Header("Font")]
    [Tooltip("TMP font asset (Silkscreen-Regular SDF).")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Colors")]
    [SerializeField] private Color buttonColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject _canvasObj;
    private GameOfLifeSimulation _simulation;

    private void Start()
    {
        BuildButton();

        _simulation = FindFirstObjectByType<GameOfLifeSimulation>();
        if (_simulation != null)
            _simulation.OnSimulationStarted += HideButton;

        UpdateVisibility();
    }

    private void OnDestroy()
    {
        if (_simulation != null)
            _simulation.OnSimulationStarted -= HideButton;
    }

    /// <summary>
    /// Hide the button when the simulation starts running or when testing from the editor.
    /// </summary>
    private void UpdateVisibility()
    {
        bool isTesting = LevelEditorBridge.CameFromEditor;
        bool isRunning = _simulation != null && _simulation.IsRunning;
        if (_canvasObj != null)
            _canvasObj.SetActive(!isTesting && !isRunning);
    }

    private void HideButton()
    {
        if (_canvasObj != null)
            _canvasObj.SetActive(false);
    }

    private void BuildButton()
    {
        // Canvas
        _canvasObj = new GameObject("LevelEditorButtonCanvas");
        _canvasObj.transform.SetParent(transform);

        var canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObj.AddComponent<GraphicRaycaster>();

        // Button
        GameObject btnObj = new GameObject("EditorButton");
        btnObj.transform.SetParent(_canvasObj.transform, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0.5f);
        btnRect.anchorMax = new Vector2(1f, 0.5f);
        btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.anchoredPosition = new Vector2(-MARGIN, 0f);
        btnRect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = buttonColor;

        var button = btnObj.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonColor * 1.4f;
        colors.pressedColor = buttonColor * 0.6f;
        button.colors = colors;
        button.targetGraphic = btnImage;
        button.onClick.AddListener(OnButtonClicked);

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "LEVEL EDITOR";
        tmp.fontSize = FONT_SIZE;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
    }

    private void OnButtonClicked()
    {
        SceneManager.LoadScene(LEVEL_EDITOR_SCENE_NAME);
    }
}
