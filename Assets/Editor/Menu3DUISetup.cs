#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utilities to apply Menu 3D style UI (PanelManager, Animator panels, SF backdrop).
/// Run via Tools/2D Shooter/Setup Menu 3D UI after opening the project in Unity.
/// </summary>
public static class Menu3DUISetup
{
    const string MainMenuControllerPath = "Assets/Art/UI/Menu3D/Animation/MainMenu/MainMenu.controller";
    const string PanelControllerPath = "Assets/Art/UI/Menu3D/Animation/Panel/Panel.controller";
    const string SfSceneElementsPath = "Assets/Art/UI/Menu3D/Prefabs/SF Scene Elements.prefab";
    const string SfTitlePath = "Assets/Art/UI/Menu3D/Prefabs/SF Title.prefab";
    const string SfWindowSpritePath = "Assets/Art/UI/Menu3D/Textures/SF UI/SF Window.psd";
    const string MainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu.prefab";

    [MenuItem("Tools/2D Shooter/Setup Menu 3D UI (All)")]
    public static void SetupAll()
    {
        SetupMainMenuScene();
        SetupMainMenuPrefab();
        SetupCanvasInGameUIPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Menu 3D UI setup complete.");
    }

    /// <summary>
    /// Entry point for Unity batchmode (-executeMethod Menu3DUISetup.SetupAllBatch).
    /// Waits for script compilation/domain reload before running setup.
    /// </summary>
    public static void SetupAllBatch()
    {
        EditorApplication.update += RunSetupAllWhenReady;
    }

    static void RunSetupAllWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= RunSetupAllWhenReady;
        try
        {
            SetupAll();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/2D Shooter/Setup Menu 3D UI (MainMenu Scene)")]
    public static void SetupMainMenuScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Scenes/MainMenu.unity");

        var canvas = GameObject.Find("MainMenuCanvas");
        if (canvas == null)
        {
            Debug.LogError("MainMenuCanvas not found.");
            return;
        }

        var uiManager = FindMainMenuUIManager(canvas.transform);
        if (uiManager == null)
        {
            Debug.LogError("UIManager not found in MainMenu scene.");
            return;
        }

        var guiCamera = EnsureGuiCamera();
        ConfigureRootCanvas(canvas, guiCamera);
        EnsureSfSceneElements();

        var mainPanel = FindPanelRoot(canvas.transform, "MainMenu");
        var levelSelect = FindPanelRoot(canvas.transform, "LevelSelect");
        var credits = FindPanelRoot(canvas.transform, "Credits");

        var mainAnim = SetupPanelRoot(mainPanel, LoadController(MainMenuControllerPath), true, useGameplayCamera: false);
        var lsAnim = SetupPanelRoot(levelSelect, LoadController(PanelControllerPath), false, useGameplayCamera: false);
        var crAnim = SetupPanelRoot(credits, LoadController(PanelControllerPath), false, useGameplayCamera: false);

        StyleSubPanelChrome(levelSelect, "LEVEL SELECT");
        StyleSubPanelChrome(credits, "CREDITS");
        EnableLevelSelectButtons(levelSelect);

        var panelManager = EnsurePanelManagerOnCanvas(canvas.transform, mainAnim);
        RemoveDuplicatePanelManager(uiManager, panelManager);

        var panelList = new List<Animator> { mainAnim, lsAnim, crAnim };
        if (!ConfigureUIManager(uiManager, panelManager, panelList, pausePageIndex: 0, allowPause: false, defaultPage: -1))
            return;

        RewireMainMenuButtons(mainPanel, levelSelect, credits, panelManager, mainAnim, lsAnim, crAnim);
        RemoveLegacyBackdrop(canvas.transform);

        if (levelSelect != null)
            SfMainMenuButtonStyler.ApplyToPanel(levelSelect.transform, SfMainMenuButtonStyler.LevelSelectButtonNames);
        if (credits != null)
            SfMainMenuButtonStyler.ApplyToPanel(credits.transform, SfMainMenuButtonStyler.CreditsButtonNames);
        SfMainMenuButtonStyler.ApplyToScene();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/2D Shooter/Setup Menu 3D UI (MainMenu Prefab)")]
    public static void SetupMainMenuPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        var canvas = root.transform.Find("MainMenuCanvas");
        if (canvas == null)
        {
            Debug.LogError("MainMenuCanvas not found in MainMenu prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var mainPanel = FindPanelRoot(canvas, "MainMenu");
        var levelSelect = FindPanelRoot(canvas, "LevelSelect");
        var credits = FindPanelRoot(canvas, "Credits");

        SetupPanelRoot(mainPanel, LoadController(MainMenuControllerPath), true, useGameplayCamera: false, guiCamera: null);
        SetupPanelRoot(levelSelect, LoadController(PanelControllerPath), false, useGameplayCamera: false, guiCamera: null);
        SetupPanelRoot(credits, LoadController(PanelControllerPath), false, useGameplayCamera: false, guiCamera: null);

        StyleSubPanelChrome(levelSelect, "LEVEL SELECT");
        StyleSubPanelChrome(credits, "CREDITS");
        EnableLevelSelectButtons(levelSelect);

        if (levelSelect != null)
            SfMainMenuButtonStyler.ApplyToPanel(levelSelect.transform, SfMainMenuButtonStyler.LevelSelectButtonNames);
        if (credits != null)
            SfMainMenuButtonStyler.ApplyToPanel(credits.transform, SfMainMenuButtonStyler.CreditsButtonNames);
        if (mainPanel != null)
            SfMainMenuButtonStyler.ApplyToPanel(mainPanel.transform, SfMainMenuButtonStyler.MainMenuButtonNames);

        PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("MainMenu prefab Menu 3D styling applied.");
    }

    static readonly string[] InGameLevelScenePaths =
    {
        "Assets/_Scenes/Level1.unity",
        "Assets/_Scenes/Level2.unity",
        "Assets/_Scenes/Level3.unity",
        "Assets/_Scenes/Level4.unity",
    };

    const string CanvasInGameUIPath = "Assets/Prefabs/UI/CanvasInGameUI.prefab";
    const float PausePanelWidth = InGameButtonLayout.ReferenceWidth;
    const float PausePanelHeight = InGameButtonLayout.DefaultPausePanelHeight;
    const float PauseBackdropWidth = 800f;
    const float PauseBackdropHeight = 1300f;

    [MenuItem("Tools/2D Shooter/Fix In-Game Pause Menu (Level Scenes)")]
    public static void FixInGamePauseMenus()
    {
        var activeScenePath = EditorSceneManager.GetActiveScene().path;

        foreach (var scenePath in InGameLevelScenePaths)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (FixInGamePauseMenuInOpenScene(scenePath))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Fixed in-game pause menu in {scenePath}.");
            }
        }

        if (!string.IsNullOrEmpty(activeScenePath) && System.IO.File.Exists(activeScenePath))
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        Debug.Log("In-game pause menu fix complete for Level1, Level2, Level3, and Level4.");
    }

    /// <summary>
    /// Entry point for Unity batchmode (-executeMethod Menu3DUISetup.FixInGamePauseMenusBatch).
    /// </summary>
    public static void FixInGamePauseMenusBatch()
    {
        EditorApplication.update += RunFixInGamePauseMenusWhenReady;
    }

    static void RunFixInGamePauseMenusWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= RunFixInGamePauseMenusWhenReady;
        try
        {
            FixInGamePauseMenus();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Repairs in-game pause menu wiring in the currently open level scene.
    /// Used by batch fix and Level4 setup to match Level2 pause behavior.
    /// </summary>
    public static bool FixInGamePauseMenuInOpenScene(string scenePath)
    {
        var canvasRoot = FindCanvasInGameUIRoot();
        if (canvasRoot == null)
        {
            Debug.LogWarning($"CanvasInGameUI not found in {scenePath}.");
            return false;
        }

        RemoveLegacyPausePanels(canvasRoot.transform);
        RemoveOrphanLegacyPauseUi(canvasRoot.transform);
        RestorePrefabPausePanel(canvasRoot);

        var pausePanel = FindMenu3DPausePanel(canvasRoot.transform);
        if (pausePanel == null)
        {
            Debug.LogError($"Menu 3D Pause Screen not found in {scenePath} after restore.");
            return false;
        }

        var pauseAnim = SetupPanelRoot(pausePanel, LoadController(PanelControllerPath), false, useGameplayCamera: true);
        EnsurePausePanelClosed(pausePanel);
        ApplyPausePanelSizing(pausePanel);
        EnsureUICameraBinder(canvasRoot);
        EnsureInGameButtonLayout(canvasRoot);

        var uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager != null)
            RewireUIManagerPausePanel(uiManager, pauseAnim, canvasRoot.transform);
        else
            Debug.LogWarning($"UIManager not found in {scenePath}.");

        NormalizeCanvasScaler(canvasRoot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvasRoot);

        var unpause = FindButtonByName(pausePanel, "Unpause");
        if (unpause != null && uiManager != null)
        {
            unpause.onClick.RemoveAllListeners();
            unpause.onClick.AddListener(uiManager.TogglePause);
        }

        SfMainMenuButtonStyler.ApplyToPausePanelInScene(pausePanel.transform);

        return true;
    }

    static GameObject FindCanvasInGameUIRoot()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.gameObject.name == "CanvasInGameUI")
                return canvas.gameObject;
        }

        return GameObject.Find("CanvasInGameUI");
    }

    public static bool IsMenu3DPausePanel(Transform t)
    {
        if (t == null || t.name != "Pause Screen")
            return false;
        if (t.Find("Window") == null)
            return false;
        var canvas = t.GetComponent<Canvas>();
        return canvas != null && canvas.renderMode == RenderMode.WorldSpace;
    }

    public static Animator FindMenu3DPauseAnimator()
    {
        var canvasRoot = FindCanvasInGameUIRoot();
        if (canvasRoot == null)
            return null;

        var pausePanel = FindMenu3DPausePanel(canvasRoot.transform);
        return pausePanel != null ? pausePanel.GetComponent<Animator>() : null;
    }

    static GameObject FindMenu3DPausePanel(Transform canvasRoot)
    {
        foreach (Transform child in canvasRoot)
        {
            if (IsMenu3DPausePanel(child))
                return child.gameObject;
        }

        foreach (var t in canvasRoot.GetComponentsInChildren<Transform>(true))
        {
            if (IsMenu3DPausePanel(t))
                return t.gameObject;
        }

        return null;
    }

    static void RemoveLegacyPausePanels(Transform canvasRoot)
    {
        var toDestroy = new List<GameObject>();
        foreach (Transform child in canvasRoot)
        {
            if (child.name == "Pause Screen" && !IsMenu3DPausePanel(child))
                toDestroy.Add(child.gameObject);
        }

        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);
    }

    static void RemoveOrphanLegacyPauseUi(Transform canvasRoot)
    {
        var toDestroy = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.transform.parent != null)
                continue;
            if (go.transform.IsChildOf(canvasRoot))
                continue;
            if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) != null)
                continue;

            if (IsOrphanLegacyPauseTitle(go) || IsOrphanLegacyUnpauseButton(go))
                toDestroy.Add(go);
        }

        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);
    }

    static bool IsOrphanLegacyPauseTitle(GameObject go)
    {
        if (go.name != "Title")
            return false;

        var tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
        return tmp != null && tmp.text == "Paused";
    }

    static bool IsOrphanLegacyUnpauseButton(GameObject go)
    {
        if (go.name.Contains("Unpause") && go.GetComponent<Button>() != null)
            return true;
        return go.name == "Main Menu Button (1)" && go.GetComponent<Button>() != null;
    }

    static void RestorePrefabPausePanel(GameObject canvasInstanceRoot)
    {
        var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(canvasInstanceRoot);
        if (prefabRoot == null)
            prefabRoot = canvasInstanceRoot;

        if (FindMenu3DPausePanel(prefabRoot.transform) != null)
            return;

        var assetPause = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasInGameUIPath);
        if (assetPause == null)
            return;

        var assetPausePanel = FindPanelRoot(assetPause.transform, "Pause Screen");
        if (assetPausePanel == null)
            return;

        PrefabUtility.RevertRemovedGameObject(prefabRoot, assetPausePanel, InteractionMode.AutomatedAction);
    }

    static void ApplyPausePanelSizing(GameObject pausePanel)
    {
        var panelRt = pausePanel.GetComponent<RectTransform>();
        if (panelRt != null)
            panelRt.sizeDelta = new Vector2(PausePanelWidth, PausePanelHeight);

        var backdropRt = FindPauseBackdropRect(pausePanel.transform);
        if (backdropRt != null && backdropRt != panelRt)
            backdropRt.sizeDelta = new Vector2(PauseBackdropWidth, PauseBackdropHeight);

        EditorUtility.SetDirty(pausePanel);
    }

    static void EnsurePausePanelClosed(GameObject pausePanel)
    {
        if (pausePanel == null)
            return;

        pausePanel.SetActive(false);
        var pauseAnimator = pausePanel.GetComponent<Animator>();
        if (pauseAnimator != null)
            pauseAnimator.SetBool("Open", false);
        EditorUtility.SetDirty(pausePanel);
    }

    static RectTransform FindPauseBackdropRect(Transform pausePanel)
    {
        var window = pausePanel.Find("Window");
        if (window != null)
        {
            var backdrop = window.Find("UIBackdrop");
            if (backdrop != null)
                return backdrop.GetComponent<RectTransform>();

            foreach (Transform child in window)
            {
                if (child.name == "Title")
                    continue;
                var rt = child.GetComponent<RectTransform>();
                if (rt != null && child.GetComponent<Image>() != null)
                    return rt;
            }
        }

        if (pausePanel.GetComponent<Image>() != null)
            return pausePanel.GetComponent<RectTransform>();

        return null;
    }

    static void RewireUIManagerPausePanel(UIManager uiManager, Animator pauseAnim, Transform canvasRoot)
    {
        var panels = uiManager.panels != null ? new List<Animator>(uiManager.panels) : new List<Animator>();
        while (panels.Count < 3)
            panels.Add(null);

        if (panels[1] == null)
            panels[1] = FindPanelAnimator(canvasRoot, "GameOverScreen");
        if (panels[2] == null)
            panels[2] = FindPanelAnimator(canvasRoot, "LevelVictoryScreen");

        panels[0] = pauseAnim;
        if (panels.Count > 3)
            panels.RemoveRange(3, panels.Count - 3);

        var so = new SerializedObject(uiManager);
        var panelsProp = so.FindProperty("panels");
        panelsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
            panelsProp.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
        so.FindProperty("pausePageIndex").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        uiManager.panels = panels;
        uiManager.pausePageIndex = 0;
        EditorUtility.SetDirty(uiManager);
    }

    static Animator FindPanelAnimator(Transform canvasRoot, string panelName)
    {
        foreach (Transform child in canvasRoot)
        {
            if (child.name != panelName)
                continue;
            var anim = child.GetComponent<Animator>();
            if (anim != null)
                return anim;
        }

        foreach (var anim in canvasRoot.GetComponentsInChildren<Animator>(true))
        {
            if (anim.gameObject.name == panelName)
                return anim;
        }

        return null;
    }

    static void NormalizeCanvasScaler(GameObject canvasRoot)
    {
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        if (scaler == null)
            return;
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);
    }

    [MenuItem("Tools/2D Shooter/Setup Menu 3D UI (CanvasInGameUI Prefab)")]
    public static void SetupCanvasInGameUIPrefab()
    {
        var path = "Assets/Prefabs/UI/CanvasInGameUI.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var canvas = root;
        var uiManager = Object.FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            var go = new GameObject("UIManager");
            go.transform.SetParent(root.transform.parent, false);
            uiManager = go.AddComponent<UIManager>();
        }

        RemoveGuiCameraFromHierarchy(root.transform);
        ConfigureInGameRootCanvas(canvas);
        EnsureUICameraBinder(canvas);
        EnsureInGameButtonLayout(canvas);

        var pause = FindPanelRoot(canvas.transform, "Pause Screen");
        var gameOver = FindPanelRoot(canvas.transform, "GameOverScreen");
        var victory = FindPanelRoot(canvas.transform, "LevelVictoryScreen");

        var pauseAnim = SetupPanelRoot(pause, LoadController(PanelControllerPath), false, useGameplayCamera: true);
        var goAnim = SetupPanelRoot(gameOver, LoadController(PanelControllerPath), false, useGameplayCamera: true);
        var vicAnim = SetupPanelRoot(victory, LoadController(PanelControllerPath), false, useGameplayCamera: true);

        var panelManager = EnsurePanelManager(canvas.transform, null);
        ConfigureUIManager(uiManager, panelManager, new List<Animator> { pauseAnim, goAnim, vicAnim }, pausePageIndex: 0, allowPause: true, defaultPage: -1);

        var unpause = FindButtonByName(pause, "Unpause");
        if (unpause != null)
        {
            unpause.onClick.RemoveAllListeners();
            unpause.onClick.AddListener(uiManager.TogglePause);
        }

        if (pause != null)
            SfMainMenuButtonStyler.ApplyToPausePanelInScene(pause.transform);
        SfMainMenuButtonStyler.ApplyToGameOverAndVictoryPrefabs();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static RuntimeAnimatorController LoadController(string path)
    {
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }

    static GameObject FindPanelRoot(Transform canvas, string name)
    {
        foreach (Transform child in canvas)
        {
            if (child.name == name)
                return child.gameObject;
        }
        return null;
    }

    static Animator SetupPanelRoot(GameObject panelRoot, RuntimeAnimatorController controller, bool active,
        bool useGameplayCamera = false, Camera guiCamera = null)
    {
        if (panelRoot == null)
            return null;

        var window = panelRoot.transform.Find("Window");
        if (window == null)
        {
            var windowGo = new GameObject("Window", typeof(RectTransform));
            window = windowGo.transform;
            window.SetParent(panelRoot.transform, false);

            var rt = window.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0, active ? 0 : -15f, 0);
            rt.localPosition = new Vector3(0, 0, 100);

            var toMove = new List<Transform>();
            foreach (Transform child in panelRoot.transform)
            {
                if (child != window)
                    toMove.Add(child);
            }
            foreach (var child in toMove)
                child.SetParent(window, true);

            if (window.GetComponent<TiltWindow>() == null)
                window.gameObject.AddComponent<TiltWindow>();
        }

        foreach (var comp in panelRoot.GetComponents<Component>())
        {
            if (comp != null && comp.GetType().Name == "UIPage")
                Object.DestroyImmediate(comp, true);
        }

        var anim = panelRoot.GetComponent<Animator>();
        if (anim == null)
            anim = panelRoot.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        var canvas = panelRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (useGameplayCamera)
            canvas.worldCamera = null;
        else
        {
            if (guiCamera == null)
                guiCamera = GameObject.Find("GUI Camera")?.GetComponent<Camera>();
            canvas.worldCamera = guiCamera;
        }

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            panelRoot.AddComponent<GraphicRaycaster>();

        panelRoot.SetActive(active);
        return anim;
    }

    static UIManager FindMainMenuUIManager(Transform canvas)
    {
        var uiGo = GameObject.Find("UIManager");
        if (uiGo != null)
        {
            var ui = uiGo.GetComponent<UIManager>();
            if (ui != null)
                return ui;
        }

        var onCanvas = canvas.GetComponentInChildren<UIManager>(true);
        if (onCanvas != null)
            return onCanvas;

        return Object.FindAnyObjectByType<UIManager>();
    }

    static PanelManager EnsurePanelManagerOnCanvas(Transform canvas, Animator initiallyOpen)
    {
        PanelManager pm = null;
        var menuManager = canvas.Find("MenuManager");
        if (menuManager != null)
            pm = menuManager.GetComponent<PanelManager>();

        if (pm == null)
        {
            var go = new GameObject("MenuManager");
            go.transform.SetParent(canvas, false);
            pm = go.AddComponent<PanelManager>();
        }

        pm.initiallyOpen = initiallyOpen;
        EditorUtility.SetDirty(pm);
        return pm;
    }

    static void RemoveDuplicatePanelManager(UIManager uiManager, PanelManager canonical)
    {
        if (uiManager == null || canonical == null)
            return;

        var onUi = uiManager.GetComponent<PanelManager>();
        if (onUi != null && onUi != canonical)
            Object.DestroyImmediate(onUi, true);
    }

    static PanelManager EnsurePanelManager(Transform canvas, Animator initiallyOpen)
    {
        return EnsurePanelManagerOnCanvas(canvas, initiallyOpen);
    }

    static bool ConfigureUIManager(UIManager ui, PanelManager pm, List<Animator> panels, int pausePageIndex, bool allowPause, int defaultPage)
    {
        if (ui == null)
        {
            Debug.LogError("ConfigureUIManager: UIManager is null.");
            return false;
        }

        foreach (var anim in panels)
        {
            if (anim == null)
            {
                Debug.LogError("ConfigureUIManager: A panel Animator reference is null. Run setup after all panel roots exist on MainMenuCanvas.");
                return false;
            }
        }

        var so = new SerializedObject(ui);
        so.FindProperty("panelManager").objectReferenceValue = pm;
        var panelsProp = so.FindProperty("panels");
        panelsProp.arraySize = panels.Count;
        for (int i = 0; i < panels.Count; i++)
            panelsProp.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
        so.FindProperty("pausePageIndex").intValue = pausePageIndex;
        so.FindProperty("allowPause").boolValue = allowPause;
        so.FindProperty("defaultPage").intValue = defaultPage;
        so.ApplyModifiedPropertiesWithoutUndo();

        ui.panelManager = pm;
        ui.panels = panels;
        ui.pausePageIndex = pausePageIndex;
        ui.allowPause = allowPause;
        ui.defaultPage = defaultPage;
        EditorUtility.SetDirty(ui);

        Debug.Log($"UIManager configured with {panels.Count} panel(s) and PanelManager on '{pm.gameObject.name}'.");
        return true;
    }

    static Camera EnsureGuiCamera()
    {
        var existing = GameObject.Find("GUI Camera");
        if (existing != null)
            return existing.GetComponent<Camera>();

        var camGo = new GameObject("GUI Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Depth;
        cam.cullingMask = 1 << 5;
        cam.orthographic = true;
        cam.depth = 0;
        cam.fieldOfView = 40f;
        return cam;
    }

    static Camera EnsureGuiCameraInPrefab(Transform root)
    {
        var t = root.Find("GUI Camera");
        if (t != null)
            return t.GetComponent<Camera>();

        var camGo = new GameObject("GUI Camera");
        camGo.transform.SetParent(root.parent != null ? root.parent : root, false);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Depth;
        cam.cullingMask = 1 << 5;
        cam.orthographic = true;
        cam.depth = 0;
        return cam;
    }

    static void ConfigureRootCanvas(GameObject canvas, Camera guiCamera)
    {
        var c = canvas.GetComponent<Canvas>();
        if (c == null)
            return;
        c.renderMode = RenderMode.ScreenSpaceCamera;
        c.worldCamera = guiCamera;
        c.planeDistance = 100f;
    }

    static void ConfigureInGameRootCanvas(GameObject canvas)
    {
        var c = canvas.GetComponent<Canvas>();
        if (c == null)
            return;
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.worldCamera = null;
    }

    static void EnsureInGameButtonLayout(GameObject canvas)
    {
        if (canvas.GetComponent<InGameButtonLayout>() == null)
            canvas.AddComponent<InGameButtonLayout>();
        EditorUtility.SetDirty(canvas);
    }

    static void EnsureUICameraBinder(GameObject canvas)
    {
        if (canvas.GetComponent<UICameraBinder>() == null)
            canvas.AddComponent<UICameraBinder>();
    }

    static void RemoveGuiCameraFromHierarchy(Transform root)
    {
        var gui = root.Find("GUI Camera");
        if (gui != null)
            Object.DestroyImmediate(gui.gameObject, true);

        foreach (Transform child in root)
        {
            if (child.name == "GUI Camera")
                Object.DestroyImmediate(child.gameObject, true);
        }

        var sf = root.Find("SF Scene Elements");
        if (sf != null)
            Object.DestroyImmediate(sf.gameObject, true);
        if (root.parent != null)
        {
            sf = root.parent.Find("SF Scene Elements");
            if (sf != null && sf.parent == root.parent)
                Object.DestroyImmediate(sf.gameObject, true);
        }
    }

    static void EnsureSfSceneElements()
    {
        if (GameObject.Find("SF Scene Elements") != null)
            return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfSceneElementsPath);
        if (prefab != null)
            PrefabUtility.InstantiatePrefab(prefab);
    }

    static void EnsureSfSceneElementsInHierarchy(Transform root)
    {
        if (Object.FindObjectOfType<ParticleSystem>() != null && GameObject.Find("SF Scene Elements") != null)
            return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfSceneElementsPath);
        if (prefab == null)
            return;
        var parent = root.parent != null ? root.parent : root;
        if (parent.Find("SF Scene Elements") == null)
            PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    static void RewireMainMenuButtons(GameObject main, GameObject levelSelect, GameObject credits,
        PanelManager pm, Animator mainAnim, Animator lsAnim, Animator crAnim)
    {
        WireOpenPanel(FindButtonByName(main, "LevelSelect"), pm, lsAnim);
        WireOpenPanel(FindButtonByName(main, "Credits"), pm, crAnim);

        WireBack(FindButtonByName(levelSelect, "MainMenuButton"), pm, mainAnim);
        WireBack(FindButtonByName(credits, "MainMenuButton"), pm, mainAnim);
    }

    static void WireOpenPanel(Button btn, PanelManager pm, Animator target)
    {
        if (btn == null || pm == null || target == null)
            return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => pm.OpenPanel(target));
    }

    static void WireBack(Button btn, PanelManager pm, Animator mainAnim)
    {
        if (btn == null || pm == null || mainAnim == null)
            return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            pm.CloseCurrent();
            pm.OpenPanel(mainAnim);
        });
    }

    static Button FindButtonByName(GameObject root, string name)
    {
        if (root == null)
            return null;
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name != name)
                continue;
            if (btn.gameObject.GetComponent<Animator>() != null)
                continue;
            return btn;
        }
        return null;
    }

    static void RemoveLegacyBackdrop(Transform canvas)
    {
        var backdrop = canvas.Find("UIBackdrop");
        if (backdrop != null)
            Object.DestroyImmediate(backdrop.gameObject);
    }

    static Transform GetPanelContentRoot(Transform panelRoot)
    {
        if (panelRoot == null)
            return null;
        var window = panelRoot.Find("Window");
        return window != null ? window : panelRoot;
    }

    static Sprite LoadSfWindowSprite()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(SfWindowSpritePath);
        foreach (var asset in assets)
        {
            if (asset is Sprite sprite)
                return sprite;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SfWindowSpritePath);
    }

    static void StyleSubPanelChrome(GameObject panelRoot, string titleText)
    {
        if (panelRoot == null)
            return;

        var contentRoot = GetPanelContentRoot(panelRoot.transform);
        var windowSprite = LoadSfWindowSprite();
        if (windowSprite == null)
        {
            Debug.LogWarning("SF Window sprite not found. Check Menu3D textures.");
            return;
        }

        var backdrop = contentRoot.Find("UIBackdrop");
        if (backdrop != null)
        {
            var rt = backdrop.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
                EditorUtility.SetDirty(rt);
            }

            var img = backdrop.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = windowSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                EditorUtility.SetDirty(backdrop.gameObject);
            }
        }

        EnsureSfTitle(contentRoot, titleText);
    }

    static void EnsureSfTitle(Transform contentRoot, string titleText)
    {
        if (contentRoot == null || string.IsNullOrEmpty(titleText))
            return;

        var existingTitle = contentRoot.Find("SF Title");
        if (existingTitle != null)
        {
            SetSfTitleLabel(existingTitle, titleText);
            return;
        }

        Transform legacyTitle = null;
        foreach (Transform child in contentRoot)
        {
            if (child.name != "Text")
                continue;
            if (child.GetComponent<Button>() != null)
                continue;
            if (child.GetComponent<Text>() == null)
                continue;
            legacyTitle = child;
            break;
        }

        var titlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SfTitlePath);
        if (titlePrefab == null)
        {
            Debug.LogWarning("SF Title prefab not found.");
            return;
        }

        Transform parent = contentRoot;
        RectTransform sourceRt = null;
        if (legacyTitle != null)
        {
            sourceRt = legacyTitle.GetComponent<RectTransform>();
            parent = legacyTitle.parent;
            Object.DestroyImmediate(legacyTitle.gameObject);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(titlePrefab, parent);
        instance.name = "SF Title";

        var rt = instance.GetComponent<RectTransform>();
        if (sourceRt != null)
            CopyRectTransform(sourceRt, rt);
        else
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -68f);
            rt.sizeDelta = new Vector2(400f, 80f);
        }

        SetSfTitleLabel(instance.transform, titleText);
        instance.transform.SetAsFirstSibling();
        EditorUtility.SetDirty(instance);
    }

    static void SetSfTitleLabel(Transform sfTitleRoot, string titleText)
    {
        var label = sfTitleRoot.Find("TitleLabel")?.GetComponent<Text>();
        if (label != null)
            label.text = titleText;
    }

    static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    static void EnableLevelSelectButtons(GameObject levelSelectPanel)
    {
        if (levelSelectPanel == null)
            return;

        foreach (var name in new[] { "LevelTwoButton", "LevelThreeButton", "LevelFourButton" })
        {
            foreach (var t in levelSelectPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != name)
                    continue;
                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
            }
        }
    }
}
#endif

