#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Applies Unity UI Samples SF (sci-fi blue) button styling to main menu, sub-panel, and in-game UI buttons.
/// </summary>
public static class SfMainMenuButtonStyler
{
    const string SfButtonSpritePath = "Assets/Art/UI/Menu3D/Textures/SF UI/SF Button.psd";
    const string SfButtonAnimatorPath = "Assets/Art/UI/Menu3D/Animation/SF Button.controller";
    const string JupiterFontPath = "Assets/Art/UI/Menu3D/Fonts/Jupiter/Jupiter.ttf";

    const string CanvasInGameUIPath = "Assets/Prefabs/UI/CanvasInGameUI.prefab";
    const string GameOverScreenPath = "Assets/Prefabs/UI/UIPages/GameOverScreen.prefab";
    const string LevelVictoryScreenPath = "Assets/Prefabs/UI/UIPages/LevelVictoryScreen.prefab";

    const int InGameFontSize = 28;
    const float PauseButtonSpacing = InGameButtonLayout.DefaultPauseButtonSpacing;
    static Vector2 InGameButtonSize => InGameButtonLayout.ComputeDefaultEditorButtonSize();
    const string PendingBatchKey = "SfMainMenuButtonStyler_PendingInGameBatch";

    [InitializeOnLoadMethod]
    static void ResumePendingInGameBatch()
    {
        if (!SessionState.GetBool(PendingBatchKey, false))
            return;
        EditorApplication.update -= RunApplyInGameWhenReady;
        EditorApplication.update += RunApplyInGameWhenReady;
    }

    public static readonly string[] MainMenuButtonNames =
    {
        "NewGameButton",
        "LevelSelect",
        "Credits",
        "ExitGameButton"
    };

    public static readonly string[] LevelSelectButtonNames =
    {
        "MainMenuButton",
        "LevelOneButton",
        "LevelTwoButton",
        "LevelThreeButton",
        "LevelFourButton"
    };

    public static readonly string[] CreditsButtonNames =
    {
        "MainMenuButton"
    };

    public static string[] PauseButtonNames => InGameButtonLayout.PauseButtonNames;
    public static string[] GameOverButtonNames => InGameButtonLayout.GameOverButtonNames;
    public static string[] VictoryButtonNames => InGameButtonLayout.VictoryButtonNames;

    [MenuItem("Tools/2D Shooter/Apply SF Buttons (Main Menu)")]
    public static void ApplyMainMenuFromMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/MainMenu.unity");
        var count = ApplyToScene();
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Applied SF button style to {count} main menu button(s).");
        }
        else
            Debug.LogWarning("No main menu buttons found. Open MainMenu scene with MainMenu panel.");
    }

    [MenuItem("Tools/2D Shooter/Apply SF Buttons (Sub-Panels)")]
    public static void ApplySubPanelsFromMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/MainMenu.unity");
        var count = ApplyToSubPanelsInScene();
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Applied SF button style to {count} sub-panel button(s).");
        }
        else
            Debug.LogWarning("No sub-panel buttons found in MainMenu scene.");
    }

    [MenuItem("Tools/2D Shooter/Apply SF Buttons (In-Game UI)")]
    public static void ApplyInGameFromMenu()
    {
        var count = ApplyToInGamePrefabs();
        if (count > 0)
            Debug.Log($"Applied SF button style to {count} in-game button(s).");
        else
            Debug.LogWarning("No in-game buttons found in UI prefabs.");
    }

    /// <summary>
    /// Entry point for Unity batchmode (-executeMethod SfMainMenuButtonStyler.ApplyInGameButtonsBatch).
    /// </summary>
    public static void ApplyInGameButtonsBatch()
    {
        SessionState.SetBool(PendingBatchKey, true);
        EditorApplication.update -= RunApplyInGameWhenReady;
        EditorApplication.update += RunApplyInGameWhenReady;
    }

    static void RunApplyInGameWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= RunApplyInGameWhenReady;
        SessionState.SetBool(PendingBatchKey, false);
        try
        {
            ApplyInGameFromMenu();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    public static int ApplyToScene()
    {
        var mainPanel = FindCanvasPanelRoot("MainMenu");
        if (mainPanel == null)
        {
            Debug.LogWarning("MainMenu panel not found on MainMenuCanvas.");
            return 0;
        }

        return ApplyToPanel(mainPanel.transform, MainMenuButtonNames);
    }

    public static int ApplyToSubPanelsInScene()
    {
        var count = 0;
        var levelSelect = FindCanvasPanelRoot("LevelSelect");
        if (levelSelect != null)
            count += ApplyToPanel(levelSelect.transform, LevelSelectButtonNames);

        var credits = FindCanvasPanelRoot("Credits");
        if (credits != null)
            count += ApplyToPanel(credits.transform, CreditsButtonNames);

        return count;
    }

    public static int ApplyToInGamePrefabs()
    {
        var inGameSize = InGameButtonSize;
        var count = 0;

        count += ApplyToPrefab(CanvasInGameUIPath, root =>
        {
            var pause = FindPanelRoot(root.transform, "Pause Screen");
            if (pause == null)
                return 0;

            var styled = ApplyToPanel(pause.transform, PauseButtonNames, InGameFontSize, inGameSize, force: true);
            LayoutPauseButtons(pause.transform);
            return styled;
        });

        count += ApplyToGameOverAndVictoryPrefabs(inGameSize);
        AssetDatabase.SaveAssets();
        return count;
    }

    public static int ApplyToGameOverAndVictoryPrefabs(Vector2? inGameSize = null)
    {
        var size = inGameSize ?? InGameButtonSize;
        var count = 0;

        count += ApplyToPrefab(GameOverScreenPath, root =>
            ApplyToPanel(root.transform, GameOverButtonNames, InGameFontSize, size, force: true));

        count += ApplyToPrefab(LevelVictoryScreenPath, root =>
            ApplyToPanel(root.transform, VictoryButtonNames, InGameFontSize, size, force: true));

        return count;
    }

    public static int ApplyToPausePanelInScene(Transform pausePanelRoot)
    {
        if (pausePanelRoot == null)
            return 0;

        var inGameSize = InGameButtonSize;
        var count = ApplyToPanel(pausePanelRoot, PauseButtonNames, InGameFontSize, inGameSize, force: true);
        LayoutPauseButtons(pausePanelRoot);
        return count;
    }

    static int ApplyToPrefab(string prefabPath, System.Func<GameObject, int> apply)
    {
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Could not load prefab '{prefabPath}'. Restore it from version control or fix YAML corruption. {ex.Message}");
            return 0;
        }

        var count = apply(root);
        if (count > 0)
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return count;
    }

    static GameObject FindCanvasPanelRoot(string panelName)
    {
        var canvas = GameObject.Find("MainMenuCanvas");
        if (canvas == null)
            return null;

        foreach (Transform child in canvas.transform)
        {
            if (child.name != panelName)
                continue;
            if (child.GetComponent<Animator>() == null)
                continue;
            return child.gameObject;
        }

        return null;
    }

    static GameObject FindPanelRoot(Transform canvas, string name)
    {
        foreach (Transform child in canvas)
        {
            if (child.name == name)
                return child.gameObject;
        }

        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t.gameObject;
        }

        return null;
    }

    public static int ApplyToPanel(Transform panelRoot, string[] buttonNames)
    {
        return ApplyToPanel(panelRoot, buttonNames, 36, null, force: false);
    }

    public static int ApplyToPanel(Transform panelRoot, string[] buttonNames, int fontSize, Vector2? sizeDelta, bool force)
    {
        if (panelRoot == null || buttonNames == null || buttonNames.Length == 0)
            return 0;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SfButtonSpritePath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SfButtonAnimatorPath);
        var font = AssetDatabase.LoadAssetAtPath<Font>(JupiterFontPath);

        if (sprite == null || controller == null)
        {
            Debug.LogError("Missing SF assets. Ensure Assets/Art/UI/Menu3D is imported.");
            return 0;
        }

        int count = 0;
        foreach (var buttonName in buttonNames)
        {
            var buttonGo = FindNamedButton(panelRoot, buttonName, force);
            if (buttonGo == null)
                continue;

            ApplySfStyle(buttonGo, sprite, controller, font, fontSize, sizeDelta);
            count++;
        }

        return count;
    }

    static GameObject FindNamedButton(Transform searchRoot, string buttonName, bool force = false)
    {
        foreach (var button in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name != buttonName)
                continue;

            if (!force && button.gameObject.GetComponent<Animator>() != null)
                continue;

            return button.gameObject;
        }

        return null;
    }

    public static void ApplySfStyle(GameObject buttonRoot, Sprite sfSprite, RuntimeAnimatorController controller, Font font)
    {
        ApplySfStyle(buttonRoot, sfSprite, controller, font, 36, null);
    }

    public static void ApplySfStyle(GameObject buttonRoot, Sprite sfSprite, RuntimeAnimatorController controller, Font font,
        int fontSize, Vector2? sizeDelta)
    {
        var button = buttonRoot.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"No Button on {buttonRoot.name}");
            return;
        }

        var labelText = ExtractButtonLabel(buttonRoot);

        var background = EnsureBackground(buttonRoot, sfSprite);
        var label = EnsureLabel(background != null ? background.transform : buttonRoot.transform, font, labelText);

        var image = background != null ? background : buttonRoot.GetComponent<Image>();
        if (image == null)
            image = buttonRoot.AddComponent<Image>();

        image.sprite = sfSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0f, 0.5490196f, 1f, 1f);
        image.raycastTarget = true;

        var rootImage = buttonRoot.GetComponent<Image>();
        if (rootImage != null && rootImage != image)
            Object.DestroyImmediate(rootImage, true);

        button.targetGraphic = image;
        button.transition = Selectable.Transition.Animation;
        button.spriteState = new SpriteState();

        var animator = buttonRoot.GetComponent<Animator>();
        if (animator == null)
            animator = buttonRoot.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        if (label != null)
        {
            label.color = Color.white;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;
        }

        if (sizeDelta.HasValue)
            ApplyButtonLayout(buttonRoot.GetComponent<RectTransform>(), sizeDelta.Value, Vector2.zero);

        if (buttonRoot.GetComponent<HighlightFix>() == null)
            buttonRoot.AddComponent<HighlightFix>();

        EditorUtility.SetDirty(buttonRoot);
    }

    static void LayoutPauseButtons(Transform pausePanelRoot)
    {
        var halfSpacing = PauseButtonSpacing * 0.5f;
        var unpause = FindNamedButton(pausePanelRoot, PauseButtonNames[0], force: true);
        var mainMenu = FindNamedButton(pausePanelRoot, PauseButtonNames[1], force: true);

        if (unpause != null)
            ApplyButtonLayout(unpause.GetComponent<RectTransform>(), InGameButtonSize, new Vector2(0f, halfSpacing));
        if (mainMenu != null)
            ApplyButtonLayout(mainMenu.GetComponent<RectTransform>(), InGameButtonSize, new Vector2(0f, -halfSpacing));
    }

    public static void ApplyButtonLayout(RectTransform rt, Vector2 size, Vector2 anchoredPosition)
    {
        if (rt == null)
            return;

        InGameButtonLayout.ApplyButtonRect(rt, size, anchoredPosition);
        EditorUtility.SetDirty(rt);
    }

    static string ExtractButtonLabel(GameObject buttonRoot)
    {
        foreach (var tmp in buttonRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var labelText = tmp.text;
            Object.DestroyImmediate(tmp.gameObject, true);
            return labelText;
        }

        var existing = buttonRoot.GetComponentInChildren<Text>(true);
        return existing != null ? existing.text : null;
    }

    static Image EnsureBackground(GameObject buttonRoot, Sprite sfSprite)
    {
        var bgTransform = buttonRoot.transform.Find("Background");
        GameObject bgGo;
        if (bgTransform != null)
            bgGo = bgTransform.gameObject;
        else
        {
            bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(buttonRoot.transform, false);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localPosition = new Vector3(0, 0, -15f);
        }

        var img = bgGo.GetComponent<Image>();
        img.sprite = sfSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0.5490196f, 1f, 1f);

        foreach (var text in buttonRoot.GetComponentsInChildren<Text>(true))
        {
            if (text.transform.parent == buttonRoot.transform)
                text.transform.SetParent(bgGo.transform, false);
        }

        return img;
    }

    static Text EnsureLabel(Transform parent, Font font, string preservedText = null)
    {
        var labelTransform = parent.Find("Label") ?? parent.Find("Text");
        GameObject labelGo;
        if (labelTransform != null)
        {
            labelGo = labelTransform.gameObject;
            if (labelGo.name == "Text")
                labelGo.name = "Label";
        }
        else
        {
            labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(parent, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        var text = labelGo.GetComponent<Text>();
        if (text == null)
            text = labelGo.AddComponent<Text>();

        if (!string.IsNullOrEmpty(preservedText))
            text.text = preservedText;

        if (font != null)
            text.font = font;
        return text;
    }
}
#endif
