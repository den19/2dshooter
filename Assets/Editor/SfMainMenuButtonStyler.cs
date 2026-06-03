#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Applies Unity UI Samples SF (sci-fi blue) button styling to main menu and sub-panel buttons.
/// </summary>
public static class SfMainMenuButtonStyler
{
    const string SfButtonSpritePath = "Assets/Art/UI/Menu3D/Textures/SF UI/SF Button.psd";
    const string SfButtonAnimatorPath = "Assets/Art/UI/Menu3D/Animation/SF Button.controller";
    const string JupiterFontPath = "Assets/Art/UI/Menu3D/Fonts/Jupiter/Jupiter.ttf";

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
        "LevelThreeButton"
    };

    public static readonly string[] CreditsButtonNames =
    {
        "MainMenuButton"
    };

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

    public static int ApplyToPanel(Transform panelRoot, string[] buttonNames)
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
            var buttonGo = FindNamedButton(panelRoot, buttonName);
            if (buttonGo == null)
                continue;

            ApplySfStyle(buttonGo, sprite, controller, font);
            count++;
        }

        return count;
    }

    static GameObject FindNamedButton(Transform searchRoot, string buttonName)
    {
        foreach (var button in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name != buttonName)
                continue;

            if (button.gameObject.GetComponent<Animator>() != null)
                continue;

            return button.gameObject;
        }

        return null;
    }

    public static void ApplySfStyle(GameObject buttonRoot, Sprite sfSprite, RuntimeAnimatorController controller, Font font)
    {
        var button = buttonRoot.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"No Button on {buttonRoot.name}");
            return;
        }

        var background = EnsureBackground(buttonRoot, sfSprite);
        var label = EnsureLabel(background != null ? background.transform : buttonRoot.transform, font);

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
            label.fontSize = 36;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;
        }

        if (buttonRoot.GetComponent<HighlightFix>() == null)
            buttonRoot.AddComponent<HighlightFix>();

        EditorUtility.SetDirty(buttonRoot);
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

        var text = buttonRoot.GetComponentInChildren<Text>();
        if (text != null && text.transform.parent == buttonRoot.transform)
            text.transform.SetParent(bgGo.transform, false);

        return img;
    }

    static Text EnsureLabel(Transform parent, Font font)
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
        if (font != null)
            text.font = font;
        return text;
    }
}
#endif
