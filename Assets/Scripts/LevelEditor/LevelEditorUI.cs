using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and drives the level editor UI panel on the right side of the screen.
/// All UI is created at runtime so the editor scene only needs this component and a LevelEditorManager.
/// </summary>
public class LevelEditorUI : MonoBehaviour
{
    private const float PANEL_WIDTH_FRACTION = 0.25f;
    private const float BUTTON_HEIGHT = 40f;
    private const float SECTION_SPACING = 16f;
    private const float ELEMENT_SPACING = 6f;
    private const int TITLE_FONT_SIZE = 28;
    private const int LABEL_FONT_SIZE = 18;
    private const int BUTTON_FONT_SIZE = 16;
    private const int ERROR_FONT_SIZE = 14;

    [Header("References")]
    [SerializeField] private LevelEditorManager editorManager;

    [Header("Font")]
    [Tooltip("TMP font asset to use (Silkscreen-Regular SDF).")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Colors")]
    [SerializeField] private Color panelBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
    [SerializeField] private Color buttonNormalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color buttonSelectedColor = new Color(0.4f, 0.4f, 0.6f, 1f);
    [SerializeField] private Color errorTextColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color validTextColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Header("Scene Navigation")]
    [Tooltip("Name of the main game scene to load when testing a level.")]
    [SerializeField] private string gameSceneName = "Kevin's Scene";

    private TextMeshProUGUI _validationText;
    private TMP_InputField _importField;
    private TMP_InputField _exportField;
    private Button _playButton;
    private List<(Button button, LevelEditorCellType type)> _brushButtons = new List<(Button, LevelEditorCellType)>();

    private void Start()
    {
        if (editorManager == null)
            editorManager = FindFirstObjectByType<LevelEditorManager>();

        BuildUI();
        UpdateBrushSelection();
        RunValidation();
    }

    private void Update()
    {
        RunValidation();
    }

    private void BuildUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("LevelEditorCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Panel background (right side)
        GameObject panelObj = CreateUIObject("EditorPanel", canvasObj.transform);
        var panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f - PANEL_WIDTH_FRACTION, 0f);
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = panelBackgroundColor;

        // Vertical layout
        var layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 20, 20);
        layout.spacing = ELEMENT_SPACING;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Exit button at the top
        CreateButton("EXIT TO GAME", panelRect, OnExitClicked);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Title
        CreateLabel("LEVEL EDITOR", panelRect, TITLE_FONT_SIZE);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Brush tools
        CreateLabel("BRUSH", panelRect, LABEL_FONT_SIZE);
        CreateBrushButton("WALL", LevelEditorCellType.Wall, new Color(0.1f, 0.1f, 0.1f), panelRect);
        CreateBrushButton("COLLECTIBLE", LevelEditorCellType.Collectible, new Color(1f, 0.75f, 0f), panelRect);
        CreateBrushButton("CURSOR START", LevelEditorCellType.CursorStart, new Color(0.1f, 0.8f, 0.3f), panelRect);
        CreateBrushButton("ERASER", LevelEditorCellType.Empty, new Color(0.5f, 0.5f, 0.5f), panelRect);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Actions
        CreateLabel("ACTIONS", panelRect, LABEL_FONT_SIZE);
        _playButton = CreateButton("TEST LEVEL", panelRect, OnPlayClicked);
        CreateButton("CLEAR", panelRect, OnClearClicked);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Export
        CreateLabel("EXPORT", panelRect, LABEL_FONT_SIZE);
        _exportField = CreateInputField("", panelRect, true);
        CreateButton("COPY TO CLIPBOARD", panelRect, OnExportClicked);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Import
        CreateLabel("IMPORT", panelRect, LABEL_FONT_SIZE);
        _importField = CreateInputField("Paste Base64...", panelRect, false);
        CreateButton("LOAD", panelRect, OnImportClicked);
        CreateSpacer(panelRect, SECTION_SPACING);

        // Validation display
        CreateLabel("STATUS", panelRect, LABEL_FONT_SIZE);
        _validationText = CreateLabel("", panelRect, ERROR_FONT_SIZE);
        _validationText.alignment = TextAlignmentOptions.TopLeft;
        _validationText.textWrappingMode = TextWrappingModes.Normal;
        _validationText.enableWordWrapping = true;

        // Hint
        CreateSpacer(panelRect, SECTION_SPACING);
        var hint = CreateLabel("Left click: paint\nRight click: erase", panelRect, ERROR_FONT_SIZE);
        hint.color = new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    private void RunValidation()
    {
        if (editorManager == null || _validationText == null) return;

        var result = editorManager.ValidateGrid();
        if (result.IsValid)
        {
            _validationText.text = "VALID";
            _validationText.color = validTextColor;
        }
        else
        {
            _validationText.text = string.Join("\n", result.Errors);
            _validationText.color = errorTextColor;
        }

        if (_playButton != null)
            _playButton.interactable = result.IsValid;
    }

    private void UpdateBrushSelection()
    {
        if (editorManager == null) return;

        foreach (var (button, type) in _brushButtons)
        {
            var colors = button.colors;
            colors.normalColor = type == editorManager.CurrentBrush ? buttonSelectedColor : buttonNormalColor;
            colors.highlightedColor = type == editorManager.CurrentBrush ? buttonSelectedColor : buttonNormalColor * 1.2f;
            button.colors = colors;
        }
    }

    private void OnBrushSelected(LevelEditorCellType brushType)
    {
        if (editorManager != null)
            editorManager.SetBrush(brushType);
        UpdateBrushSelection();
    }

    private void OnPlayClicked()
    {
        if (editorManager == null) return;

        var preset = editorManager.BuildPreset();
        if (preset == null) return;

        // Save the grid so it can be restored when returning from testing.
        editorManager.SaveGridState();

        LevelEditorBridge.PendingPreset = preset;
        LevelEditorBridge.CameFromEditor = true;
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnClearClicked()
    {
        if (editorManager != null)
            editorManager.ClearAllCells();
    }

    private void OnExitClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnExportClicked()
    {
        if (editorManager == null) return;

        string base64 = editorManager.ExportToBase64();
        if (!string.IsNullOrEmpty(base64))
        {
            if (_exportField != null)
                _exportField.text = base64;
            GUIUtility.systemCopyBuffer = base64;
        }
    }

    private void OnImportClicked()
    {
        if (editorManager == null || _importField == null) return;

        string base64 = _importField.text;
        if (string.IsNullOrWhiteSpace(base64)) return;

        editorManager.ImportFromBase64(base64);
    }

    // --- UI Factory Methods ---

    private TextMeshProUGUI CreateLabel(string text, RectTransform parent, int fontSize)
    {
        GameObject obj = CreateUIObject("Label", parent);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, fontSize + 8f);

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null) tmp.font = font;

        return tmp;
    }

    private Button CreateButton(string label, RectTransform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = CreateUIObject("Button_" + label, parent);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, BUTTON_HEIGHT);

        var image = obj.AddComponent<Image>();
        image.color = buttonNormalColor;

        var button = obj.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonNormalColor;
        colors.highlightedColor = buttonNormalColor * 1.3f;
        colors.pressedColor = buttonNormalColor * 0.7f;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textObj = CreateUIObject("Text", obj.transform);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = BUTTON_FONT_SIZE;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;

        return button;
    }

    private void CreateBrushButton(string label, LevelEditorCellType brushType, Color indicatorColor, RectTransform parent)
    {
        GameObject obj = CreateUIObject("BrushBtn_" + label, parent);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, BUTTON_HEIGHT);

        var image = obj.AddComponent<Image>();
        image.color = buttonNormalColor;

        var button = obj.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonNormalColor;
        colors.highlightedColor = buttonNormalColor * 1.3f;
        colors.pressedColor = buttonNormalColor * 0.7f;
        button.colors = colors;
        button.targetGraphic = image;

        LevelEditorCellType capturedType = brushType;
        button.onClick.AddListener(() => OnBrushSelected(capturedType));

        // Color indicator on the left
        GameObject indicatorObj = CreateUIObject("Indicator", obj.transform);
        var indRect = indicatorObj.GetComponent<RectTransform>();
        indRect.anchorMin = new Vector2(0f, 0.15f);
        indRect.anchorMax = new Vector2(0f, 0.85f);
        indRect.offsetMin = new Vector2(8f, 0f);
        indRect.offsetMax = new Vector2(28f, 0f);
        var indImage = indicatorObj.AddComponent<Image>();
        indImage.color = indicatorColor;

        GameObject textObj = CreateUIObject("Text", obj.transform);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(36f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = BUTTON_FONT_SIZE;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        if (font != null) tmp.font = font;

        _brushButtons.Add((button, brushType));
    }

    private TMP_InputField CreateInputField(string placeholder, RectTransform parent, bool readOnly)
    {
        GameObject obj = CreateUIObject("InputField", parent);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 36f);

        var bgImage = obj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        GameObject textAreaObj = CreateUIObject("TextArea", obj.transform);
        var textAreaRect = textAreaObj.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(8f, 2f);
        textAreaRect.offsetMax = new Vector2(-8f, -2f);
        textAreaObj.AddComponent<RectMask2D>();

        GameObject placeholderObj = CreateUIObject("Placeholder", textAreaObj.transform);
        var phRect = placeholderObj.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        var phText = placeholderObj.AddComponent<TextMeshProUGUI>();
        phText.text = placeholder;
        phText.fontSize = ERROR_FONT_SIZE;
        phText.fontStyle = FontStyles.Italic;
        phText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        if (font != null) phText.font = font;

        GameObject textObj = CreateUIObject("Text", textAreaObj.transform);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.fontSize = ERROR_FONT_SIZE;
        tmpText.color = Color.white;
        if (font != null) tmpText.font = font;

        var inputField = obj.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = tmpText;
        inputField.placeholder = phText;
        inputField.readOnly = readOnly;
        inputField.fontAsset = font;

        return inputField;
    }

    private void CreateSpacer(RectTransform parent, float height)
    {
        GameObject obj = CreateUIObject("Spacer", parent);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }
}
