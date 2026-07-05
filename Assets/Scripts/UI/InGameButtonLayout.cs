using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sizes in-game UI buttons (pause, game over, victory) to fill the visible window width
/// with margins while keeping a fixed height. MainMenu is unaffected.
/// </summary>
[DisallowMultipleComponent]
public class InGameButtonLayout : MonoBehaviour
{
    public const float ReferenceWidth = 1080f;
    public const float ReferenceHeight = 1920f;
    public const float DefaultHorizontalMarginPercent = 0.05f;
    public const float DefaultFixedButtonHeight = 60f;
    public const float DefaultPauseButtonSpacing = 70f;
    public const float DefaultPausePanelHeight = 600f;

    public static readonly string[] PauseButtonNames =
    {
        " Unpause Button",
        "Main Menu Button (1)"
    };

    public static readonly string[] GameOverButtonNames =
    {
        "Main Menu Button"
    };

    public static readonly string[] VictoryButtonNames =
    {
        "Next Level Button"
    };

    [SerializeField] float horizontalMarginPercent = DefaultHorizontalMarginPercent;
    [SerializeField] float fixedButtonHeight = DefaultFixedButtonHeight;
    [SerializeField] float pauseButtonSpacing = DefaultPauseButtonSpacing;
    [SerializeField] float pausePanelHeight = DefaultPausePanelHeight;

    CanvasScaler canvasScaler;
    Vector2Int lastScreenSize = Vector2Int.zero;

    void Awake()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        ApplyLayout();
    }

    void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size != lastScreenSize)
            ApplyLayout();
    }

    public void ApplyLayout()
    {
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        var buttonSize = ComputeButtonSize(canvasScaler, horizontalMarginPercent, fixedButtonHeight);
        NormalizePausePanel(ComputeEffectiveReferenceWidth(canvasScaler));

        LayoutPauseButtons(buttonSize);
        LayoutPanelButtons("GameOverScreen", GameOverButtonNames, buttonSize, new Vector2(0f, -12f));
        LayoutPanelButtons("LevelVictoryScreen", VictoryButtonNames, buttonSize, new Vector2(0f, -12f));
    }

    public static float ComputeEffectiveReferenceWidth(CanvasScaler scaler)
    {
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return ReferenceWidth;

        var refRes = scaler.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f)
            return ReferenceWidth;

        var screenWidth = Mathf.Max(Screen.width, 1);
        var screenHeight = Mathf.Max(Screen.height, 1);
        var logWidth = Mathf.Log(screenWidth / refRes.x, 2f);
        var logHeight = Mathf.Log(screenHeight / refRes.y, 2f);
        var scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        if (scale <= 0f)
            return ReferenceWidth;

        return screenWidth / scale;
    }

    public static Vector2 ComputeButtonSize(CanvasScaler scaler, float marginPercent, float buttonHeight)
    {
        var refWidth = ComputeEffectiveReferenceWidth(scaler);
        var buttonWidth = refWidth * (1f - 2f * marginPercent);
        return new Vector2(buttonWidth, buttonHeight);
    }

    public static Vector2 ComputeDefaultEditorButtonSize()
    {
        var buttonWidth = ReferenceWidth * (1f - 2f * DefaultHorizontalMarginPercent);
        return new Vector2(buttonWidth, DefaultFixedButtonHeight);
    }

    public static void ApplyButtonRect(RectTransform rt, Vector2 size, Vector2 anchoredPosition)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
    }

    void NormalizePausePanel(float refWidth)
    {
        var pausePanel = FindPanel("Pause Screen");
        if (pausePanel == null)
            return;

        var panelRt = pausePanel.GetComponent<RectTransform>();
        if (panelRt != null)
            panelRt.sizeDelta = new Vector2(refWidth, pausePanelHeight);
    }

    void LayoutPauseButtons(Vector2 buttonSize)
    {
        var pausePanel = FindPanel("Pause Screen");
        if (pausePanel == null)
            return;

        var halfSpacing = pauseButtonSpacing * 0.5f;
        var unpause = FindNamedButton(pausePanel.transform, PauseButtonNames[0]);
        var mainMenu = FindNamedButton(pausePanel.transform, PauseButtonNames[1]);

        if (unpause != null)
            ApplyButtonRect(unpause, buttonSize, new Vector2(0f, halfSpacing));
        if (mainMenu != null)
            ApplyButtonRect(mainMenu, buttonSize, new Vector2(0f, -halfSpacing));
    }

    void LayoutPanelButtons(string panelName, string[] buttonNames, Vector2 buttonSize, Vector2 anchoredPosition)
    {
        var panel = FindPanel(panelName);
        if (panel == null)
            return;

        foreach (var buttonName in buttonNames)
        {
            var button = FindNamedButton(panel.transform, buttonName);
            if (button != null)
                ApplyButtonRect(button, buttonSize, anchoredPosition);
        }
    }

    Transform FindPanel(string panelName)
    {
        foreach (Transform child in transform)
        {
            if (child.name == panelName)
                return child;
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == panelName)
                return t;
        }

        return null;
    }

    static RectTransform FindNamedButton(Transform searchRoot, string buttonName)
    {
        foreach (var button in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == buttonName)
                return button.GetComponent<RectTransform>();
        }

        return null;
    }
}
