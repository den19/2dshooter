using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Linq;

/// <summary>
/// Manages UI panels via PanelManager (Menu 3D style Animator flow).
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    static readonly string[] MainMenuPanelNames = { "MainMenu", "LevelSelect", "Credits" };

    [Header("Page Management")]
    [Tooltip("Panel Animators managed by the UI Manager (each root has Panel or MainMenu controller)")]
    public List<Animator> panels;
    [Tooltip("The index of the active panel")]
    public int currentPage = 0;
    [Tooltip("Panel opened on startup (index). -1 = use PanelManager.initiallyOpen only")]
    public int defaultPage = 0;

    [Header("Panel Manager")]
    [Tooltip("PanelManager on this object or a child (e.g. MenuManager)")]
    public PanelManager panelManager;

    [Header("Pause Settings")]
    [Tooltip("The index of the pause panel in the panels list")]
    public int pausePageIndex = 0;
    [Tooltip("Whether or not to allow pausing")]
    public bool allowPause = true;

    [Header("Input Actions & Controls")]
    public InputAction pauseAction;

    private bool isPaused = false;
    private List<UIelement> UIelements;

    [HideInInspector]
    public EventSystem eventSystem;

    private void OnEnable()
    {
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        pauseAction.Disable();
    }

    private void SetUpUIElements()
    {
        UIelements = FindObjectsOfType<UIelement>().ToList();
    }

    private void SetUpEventSystem()
    {
        eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("There is no event system in the scene but you are trying to use the UIManager.");
        }
    }

    private void EnsurePanelManager()
    {
        if (panelManager == null)
            panelManager = GetComponent<PanelManager>();
        if (panelManager == null)
            panelManager = GetComponentInChildren<PanelManager>();

        if (panelManager == null)
        {
            var canvas = GameObject.Find("MainMenuCanvas");
            if (canvas != null)
            {
                var menuManager = canvas.transform.Find("MenuManager");
                if (menuManager != null)
                    panelManager = menuManager.GetComponent<PanelManager>();
            }
        }

        if (panelManager == null)
            panelManager = FindAnyObjectByType<PanelManager>();
    }

    private bool PanelsListHasValidEntry()
    {
        if (panels == null || panels.Count == 0)
            return false;
        foreach (var panel in panels)
        {
            if (panel != null)
                return true;
        }
        return false;
    }

    private void EnsureMainMenuPanelsResolved()
    {
        if (PanelsListHasValidEntry())
            return;

        var canvas = GameObject.Find("MainMenuCanvas");
        if (canvas == null)
            return;

        if (panels == null)
            panels = new List<Animator>();
        else
            panels.Clear();

        foreach (var panelName in MainMenuPanelNames)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name != panelName)
                    continue;
                var anim = child.GetComponent<Animator>();
                if (anim != null)
                    panels.Add(anim);
                break;
            }
        }
    }

    public void TogglePause()
    {
        if (!allowPause || panelManager == null)
            return;

        if (isPaused)
        {
            panelManager.CloseCurrent();
            Time.timeScale = 1;
            isPaused = false;
        }
        else if (pausePageIndex >= 0 && pausePageIndex < panels.Count && panels[pausePageIndex] != null)
        {
            GoToPage(pausePageIndex);
            Time.timeScale = 0;
            isPaused = true;
        }
    }

    public void UpdateUI()
    {
        SetUpUIElements();
        foreach (UIelement uiElement in UIelements)
        {
            uiElement.UpdateUI();
        }
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);
    }

    private void Start()
    {
        EnsureMainMenuPanelsResolved();
        EnsurePanelManager();
        SetUpEventSystem();
        SetUpUIElements();
        UpdateUI();

        if (defaultPage >= 0 && defaultPage < panels.Count && panels[defaultPage] != null
            && panelManager != null && panelManager.initiallyOpen == null)
        {
            GoToPage(defaultPage);
        }
    }

    private bool isFirstTouchDown = false;
    private float firstTouchTime = 0f;
    private const float DOUBLE_TAP_INTERVAL = 0.3f;

    private void Update()
    {
        CheckPauseInput();
        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
                HandleTouch();
        }
    }

    void HandleTouch()
    {
        if (!isFirstTouchDown)
        {
            isFirstTouchDown = true;
            firstTouchTime = Time.unscaledTime;
        }
        else
        {
            if ((Time.unscaledTime - firstTouchTime) <= DOUBLE_TAP_INTERVAL)
                OnDoubleTap();
            ResetTouchState();
        }
    }

    void ResetTouchState()
    {
        isFirstTouchDown = false;
        firstTouchTime = 0f;
    }

    protected virtual void OnDoubleTap()
    {
        TogglePause();
    }

    private void CheckPauseInput()
    {
        if (pauseAction.triggered)
            TogglePause();
    }

    public void GoToPage(int pageIndex)
    {
        EnsureMainMenuPanelsResolved();
        if (pageIndex < 0 || pageIndex >= panels.Count || panels[pageIndex] == null)
            return;

        EnsurePanelManager();
        if (panelManager != null)
            panelManager.OpenPanel(panels[pageIndex]);
        else
            panels[pageIndex].gameObject.SetActive(true);

        currentPage = pageIndex;
    }

    public void GoToPageByName(string pageName)
    {
        EnsureMainMenuPanelsResolved();
        if (panels == null)
            return;

        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] != null && panels[i].gameObject.name == pageName)
            {
                GoToPage(i);
                return;
            }
        }
    }

    public void CloseCurrentPanel()
    {
        EnsurePanelManager();
        if (panelManager != null)
            panelManager.CloseCurrent();
    }

    /// <summary>
    /// Opens main menu panel after closing sub-panel (Level Select / Credits back flow).
    /// </summary>
    public void BackToMainMenuPanel()
    {
        CloseCurrentPanel();
        GoToPageByName("MainMenu");
    }

    public void SetActiveAllPages(bool activated)
    {
        if (panels == null)
            return;

        if (!activated)
        {
            EnsurePanelManager();
            if (panelManager != null)
                panelManager.CloseCurrent();
        }

        foreach (Animator panel in panels)
        {
            if (panel != null)
            {
                if (!activated)
                {
                    panel.SetBool("Open", false);
                    panel.gameObject.SetActive(false);
                }
                else
                    panel.gameObject.SetActive(true);
            }
        }
    }
}
