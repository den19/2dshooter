#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds Level4 as an E-shaped asteroid corridor maze, wires Level3→Level4,
/// and adds Level 4 to the main-menu level select.
/// </summary>
public static class Level4Setup
{
    const string Level4Path = "Assets/_Scenes/Level4.unity";
    const string Level3Path = "Assets/_Scenes/Level3.unity";
    const string MainMenuPath = "Assets/_Scenes/MainMenu.unity";
    const string MainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu.prefab";
    const string CanvasInGameUIPath = "Assets/Prefabs/UI/CanvasInGameUI.prefab";

    const string ChaserPath = "Assets/Prefabs/Enemies/IndividualEnemies/Chasers/ChaserEnemy.prefab";
    const string DiagonalChaserPath = "Assets/Prefabs/Enemies/IndividualEnemies/Chasers/DiagonalChasing.prefab";
    const string StraightShooterPath = "Assets/Prefabs/Enemies/IndividualEnemies/StationaryShooters/StraightShooter.prefab";
    const string DiagonalShooterPath = "Assets/Prefabs/Enemies/IndividualEnemies/StationaryShooters/DiagonalShooter.prefab";

    // E corridor layout (open playable strokes of the letter).
    const float Cell = 6f;
    const float OriginX = 0f;
    const float OriginY = 0f;
    const float EWidth = 102f;
    const float EHeight = 138f;
    const float Corridor = 22f;
    const float SpineRight = 28f;
    const float MidBarRight = 88f;

    [MenuItem("Tools/2D Shooter/Setup Level4 (All)")]
    public static void SetupAll()
    {
        SetupLevel4Scene();
        SetupLevel3Transition();
        SetupMainMenuLevel4Button();
        SetupMainMenuPrefabLevel4Button();
        AssetDatabase.SaveAssets();
        Debug.Log("Level4 setup complete (scene, Level3 transition, main menu).");
    }

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

    [MenuItem("Tools/2D Shooter/Setup Level4 (E Layout)")]
    public static void SetupLevel4Scene()
    {
        var scene = EditorSceneManager.OpenScene(Level4Path);

        var wallTemplate = FindWallTemplate();
        ClearMazeAsteroids();
        ClearEnemies();

        var mazeRoot = EnsureChildRoot("EMaze");
        var enemiesRoot = EnsureChildRoot("Enemies");

        BuildEWalls(mazeRoot.transform, wallTemplate);
        int enemyCount = PlaceEnemies(enemiesRoot.transform);
        PlacePlayer(new Vector3(OriginX + EWidth - 8f, OriginY + Corridor * 0.5f, 0f));
        EnsureCanvasInGameUI();
        if (!Menu3DUISetup.FixInGamePauseMenuInOpenScene(Level4Path))
            Debug.LogWarning("Pause menu fix did not complete for Level4.");
        ConfigureGameManager(enemyCount);
        UpdateLevelLabels();
        ConfigureFinalVictory();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Level4 E layout saved with {enemyCount} enemies.");
    }

    [MenuItem("Tools/2D Shooter/Setup Level3 → Level4 Transition")]
    public static void SetupLevel3Transition()
    {
        var scene = EditorSceneManager.OpenScene(Level3Path);

        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text != null && tmp.text.Contains("You WON the GAME"))
            {
                tmp.text = "Level 3 <color=\"green\">Complete!</color>";
                EditorUtility.SetDirty(tmp);
            }

            if (tmp.text == "GO TO MAIN MENU")
            {
                tmp.text = "Next Level";
                EditorUtility.SetDirty(tmp);
            }
        }

        RewireVictoryNextLevelButton("Level4");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Level3 victory now loads Level4.");
    }

    [MenuItem("Tools/2D Shooter/Setup MainMenu Level4 Button")]
    public static void SetupMainMenuLevel4Button()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath);
        var levelSelect = GameObject.Find("LevelSelect");
        if (levelSelect == null)
        {
            Debug.LogError("LevelSelect panel not found in MainMenu.");
            return;
        }

        EnsureLevelFourButton(levelSelect.transform, isPrefabContents: false);
        LayoutLevelSelectButtons(levelSelect.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainMenu LevelFourButton ready.");
    }

    static void SetupMainMenuPrefabLevel4Button()
    {
        var root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        var levelSelect = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "LevelSelect");
        if (levelSelect == null)
        {
            Debug.LogWarning("LevelSelect not found in MainMenu prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        EnsureLevelFourButton(levelSelect, isPrefabContents: true);
        LayoutLevelSelectButtons(levelSelect);
        PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("MainMenu prefab LevelFourButton ready.");
    }

    static void ClearMazeAsteroids()
    {
        var toDestroy = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("AsteroidWall_Object"))
            .Select(t => t.gameObject)
            .ToList();

        var existingMaze = GameObject.Find("EMaze");
        if (existingMaze != null)
            toDestroy.Add(existingMaze);

        foreach (var go in toDestroy)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    static void ClearEnemies()
    {
        var enemiesRoot = GameObject.Find("Enemies");
        if (enemiesRoot != null)
        {
            var children = new List<GameObject>();
            foreach (Transform child in enemiesRoot.transform)
                children.Add(child.gameObject);
            foreach (var child in children)
                Object.DestroyImmediate(child);
        }

        foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemy != null)
                Object.DestroyImmediate(enemy.gameObject);
        }
    }

    static GameObject EnsureChildRoot(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
            return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static void BuildEWalls(Transform parent, GameObject template)
    {
        if (template == null)
        {
            Debug.LogError("No AsteroidWall_Object template found and no asteroid prefab available.");
            return;
        }

        var wallCells = new HashSet<Vector2Int>();
        int minX = Mathf.FloorToInt(OriginX / Cell) - 1;
        int maxX = Mathf.CeilToInt((OriginX + EWidth) / Cell) + 1;
        int minY = Mathf.FloorToInt(OriginY / Cell) - 1;
        int maxY = Mathf.CeilToInt((OriginY + EHeight) / Cell) + 1;

        for (int gx = minX; gx <= maxX; gx++)
        {
            for (int gy = minY; gy <= maxY; gy++)
            {
                float wx = gx * Cell + Cell * 0.5f;
                float wy = gy * Cell + Cell * 0.5f;
                if (!IsInsideECorridor(wx, wy) && IsInsideEBounds(wx, wy))
                    wallCells.Add(new Vector2Int(gx, gy));
            }
        }

        // Outer frame so the E reads clearly against open space.
        for (int gx = minX; gx <= maxX; gx++)
        {
            wallCells.Add(new Vector2Int(gx, minY));
            wallCells.Add(new Vector2Int(gx, maxY));
        }
        for (int gy = minY; gy <= maxY; gy++)
        {
            wallCells.Add(new Vector2Int(minX, gy));
            wallCells.Add(new Vector2Int(maxX, gy));
        }

        int index = 0;
        foreach (var cell in wallCells)
        {
            var wall = Object.Instantiate(template, parent);
            wall.name = $"AsteroidWall_Object ({index++})";
            wall.transform.position = new Vector3(cell.x * Cell + Cell * 0.5f, cell.y * Cell + Cell * 0.5f, 0f);
            wall.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            wall.SetActive(true);
        }

        Object.DestroyImmediate(template);
    }

    static GameObject FindWallTemplate()
    {
        var existing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name.StartsWith("AsteroidWall_Object"));
        if (existing != null)
        {
            var clone = Object.Instantiate(existing.gameObject);
            clone.name = "AsteroidWall_Template";
            return clone;
        }

        // Fallback: build a simple static asteroid wall from Asteroid 1 art.
        var asteroidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment & Hazards/Asteroid 1.prefab");
        if (asteroidPrefab == null)
            return null;

        var go = Object.Instantiate(asteroidPrefab);
        go.name = "AsteroidWall_Template";
        go.transform.localScale = Vector3.one * 4.5f;

        foreach (var health in go.GetComponentsInChildren<Health>(true))
            Object.DestroyImmediate(health);
        foreach (var damage in go.GetComponentsInChildren<Damage>(true))
            Object.DestroyImmediate(damage);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;
        }

        return go;
    }

    static bool IsInsideEBounds(float x, float y)
    {
        return x >= OriginX - Cell && x <= OriginX + EWidth + Cell
            && y >= OriginY - Cell && y <= OriginY + EHeight + Cell;
    }

    /// <summary>
    /// Open corridor strokes of letter E: spine + top/middle/bottom bars.
    /// </summary>
    static bool IsInsideECorridor(float x, float y)
    {
        float left = OriginX + 4f;
        float spineRight = OriginX + SpineRight;
        float bottomTop = OriginY + Corridor;
        float midBottom = OriginY + (EHeight - Corridor) * 0.5f - Corridor * 0.5f;
        float midTop = midBottom + Corridor;
        float topBottom = OriginY + EHeight - Corridor;
        float topTop = OriginY + EHeight - 4f;
        float bottomRight = OriginX + EWidth - 4f;
        float midRight = OriginX + MidBarRight;
        float topRight = OriginX + EWidth - 4f;

        bool inSpine = x >= left && x <= spineRight && y >= OriginY + 4f && y <= topTop;
        bool inBottom = x >= left && x <= bottomRight && y >= OriginY + 4f && y <= bottomTop;
        bool inMiddle = x >= left && x <= midRight && y >= midBottom && y <= midTop;
        bool inTop = x >= left && x <= topRight && y >= topBottom && y <= topTop;
        return inSpine || inBottom || inMiddle || inTop;
    }

    static int PlaceEnemies(Transform parent)
    {
        var chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPath);
        var diagonalChaser = AssetDatabase.LoadAssetAtPath<GameObject>(DiagonalChaserPath);
        var straightShooter = AssetDatabase.LoadAssetAtPath<GameObject>(StraightShooterPath);
        var diagonalShooter = AssetDatabase.LoadAssetAtPath<GameObject>(DiagonalShooterPath);

        var placements = new List<EnemyPlacement>();

        // Bottom bar: start zone, lighter enemies.
        AddLine(placements, chaser, 11,
            new Vector3(OriginX + EWidth - 18f, OriginY + Corridor * 0.5f, 0f),
            new Vector3(OriginX + SpineRight + 6f, OriginY + Corridor * 0.5f, 0f),
            hp: 3, moveSpeed: 2.2f, followRange: 40f);

        // Spine low.
        AddLine(placements, diagonalChaser, 8,
            new Vector3(OriginX + SpineRight * 0.5f, OriginY + Corridor + 4f, 0f),
            new Vector3(OriginX + SpineRight * 0.5f, OriginY + (EHeight * 0.5f) - Corridor * 0.5f - 4f, 0f),
            hp: 4, moveSpeed: 2.4f, followRange: 50f);

        // Middle bar pocket.
        AddLine(placements, straightShooter, 6,
            new Vector3(OriginX + SpineRight + 8f, OriginY + EHeight * 0.5f, 0f),
            new Vector3(OriginX + MidBarRight - 8f, OriginY + EHeight * 0.5f, 0f),
            hp: 7, moveSpeed: 0f, followRange: 0f);
        AddLine(placements, chaser, 5,
            new Vector3(OriginX + SpineRight + 10f, OriginY + EHeight * 0.5f - 4f, 0f),
            new Vector3(OriginX + MidBarRight - 10f, OriginY + EHeight * 0.5f + 4f, 0f),
            hp: 6, moveSpeed: 2.6f, followRange: 55f);

        // Spine high.
        AddLine(placements, diagonalChaser, 8,
            new Vector3(OriginX + SpineRight * 0.5f, OriginY + EHeight * 0.5f + Corridor * 0.5f + 4f, 0f),
            new Vector3(OriginX + SpineRight * 0.5f, OriginY + EHeight - Corridor - 4f, 0f),
            hp: 8, moveSpeed: 2.8f, followRange: 60f);

        // Top bar finale.
        AddLine(placements, straightShooter, 6,
            new Vector3(OriginX + SpineRight + 8f, OriginY + EHeight - Corridor * 0.5f, 0f),
            new Vector3(OriginX + EWidth - 16f, OriginY + EHeight - Corridor * 0.5f, 0f),
            hp: 10, moveSpeed: 0f, followRange: 0f);
        AddLine(placements, diagonalShooter, 4,
            new Vector3(OriginX + SpineRight + 14f, OriginY + EHeight - Corridor * 0.5f + 3f, 0f),
            new Vector3(OriginX + EWidth - 20f, OriginY + EHeight - Corridor * 0.5f - 3f, 0f),
            hp: 12, moveSpeed: 0f, followRange: 0f);
        AddLine(placements, chaser, 4,
            new Vector3(OriginX + SpineRight + 20f, OriginY + EHeight - Corridor * 0.5f, 0f),
            new Vector3(OriginX + EWidth - 12f, OriginY + EHeight - Corridor * 0.5f, 0f),
            hp: 14, moveSpeed: 3f, followRange: 70f);

        int count = 0;
        foreach (var placement in placements)
        {
            if (placement.Prefab == null)
                continue;

            var enemyGo = (GameObject)PrefabUtility.InstantiatePrefab(placement.Prefab, parent);
            enemyGo.transform.position = placement.Position;
            enemyGo.name = $"{placement.Prefab.name}_{count}";

            var enemy = enemyGo.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.moveSpeed = placement.MoveSpeed;
                enemy.followRange = placement.FollowRange;
                if (GameManager.instance != null && GameManager.instance.player != null)
                    enemy.followTarget = GameManager.instance.player.transform;
                else
                {
                    var player = Object.FindFirstObjectByType<Controller>();
                    if (player != null)
                        enemy.followTarget = player.transform;
                }
            }

            var health = enemyGo.GetComponent<Health>();
            if (health != null)
            {
                health.defaultHealth = placement.Health;
                health.maximumHealth = placement.Health;
                health.currentHealth = placement.Health;
            }

            count++;
        }

        return count;
    }

    static void AddLine(
        List<EnemyPlacement> list,
        GameObject prefab,
        int count,
        Vector3 from,
        Vector3 to,
        int hp,
        float moveSpeed,
        float followRange)
    {
        if (count <= 0 || prefab == null)
            return;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            list.Add(new EnemyPlacement
            {
                Prefab = prefab,
                Position = Vector3.Lerp(from, to, t),
                Health = hp,
                MoveSpeed = moveSpeed,
                FollowRange = followRange
            });
        }
    }

    static void PlacePlayer(Vector3 position)
    {
        var player = Object.FindFirstObjectByType<Controller>();
        if (player == null)
        {
            Debug.LogWarning("Player not found in Level4.");
            return;
        }

        player.transform.position = position;
        EditorUtility.SetDirty(player.gameObject);
    }

    static void ConfigureGameManager(int enemyCount)
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("GameManager not found in Level4.");
            return;
        }

        gm.enemiesToDefeat = enemyCount;
        gm.gameIsWinnable = true;
        EditorUtility.SetDirty(gm);
    }

    static void EnsureCanvasInGameUI()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "CanvasInGameUI")
                return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasInGameUIPath);
        if (prefab == null)
        {
            Debug.LogError($"CanvasInGameUI prefab not found at {CanvasInGameUIPath}");
            return;
        }

        PrefabUtility.InstantiatePrefab(prefab);
        Debug.Log("Instantiated CanvasInGameUI for Level4.");
    }

    static void UpdateLevelLabels()
    {
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text == "Level 3" || tmp.gameObject.name.Contains("Level3Label"))
            {
                tmp.text = "Level 4";
                EditorUtility.SetDirty(tmp);
            }
        }

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "Level3Label")
            {
                t.name = "Level4Label";
                EditorUtility.SetDirty(t.gameObject);
            }
        }
    }

    static void ConfigureFinalVictory()
    {
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text != null && (tmp.text.Contains("Level 3") && tmp.text.Contains("Complete")))
            {
                tmp.text = "<color=\"yellow\">Congratulations!</color>\n\nYou WON the GAME!\n\n\nYou are a \n\n<color=\"red\">hardcore</color> \n\nsirvival spaceship player!";
                EditorUtility.SetDirty(tmp);
            }

            if (tmp.text == "Next Level")
            {
                tmp.text = "GO TO MAIN MENU";
                EditorUtility.SetDirty(tmp);
            }
        }

        RewireVictoryNextLevelButton("MainMenu");
    }

    static void RewireVictoryNextLevelButton(string levelName)
    {
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.gameObject.name != "Next Level Button")
                continue;

            var loadButton = button.GetComponent<LevelLoadButton>();
            if (loadButton == null)
                continue;

            ReplaceLoadLevelPersistentCall(button, loadButton, levelName);
        }
    }

    static void ReplaceLoadLevelPersistentCall(Button button, LevelLoadButton loadButton, string levelName)
    {
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);

        UnityEventTools.AddStringPersistentListener(button.onClick, loadButton.LoadLevelByName, levelName);
        EditorUtility.SetDirty(button);
    }

    static void EnsureLevelFourButton(Transform levelSelect, bool isPrefabContents)
    {
        var existing = levelSelect.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "LevelFourButton");
        if (existing != null)
        {
            WireLevelButton(existing.gameObject, "Level4", "Level 4");
            return;
        }

        var source = levelSelect.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "LevelThreeButton");
        if (source == null)
        {
            Debug.LogError("LevelThreeButton not found; cannot create LevelFourButton.");
            return;
        }

        var clone = Object.Instantiate(source.gameObject, source.parent);
        clone.name = "LevelFourButton";
        clone.SetActive(true);
        WireLevelButton(clone, "Level4", "Level 4");
    }

    static void WireLevelButton(GameObject buttonGo, string levelName, string label)
    {
        foreach (var text in buttonGo.GetComponentsInChildren<Text>(true))
        {
            text.text = label;
            EditorUtility.SetDirty(text);
        }
        foreach (var tmp in buttonGo.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            tmp.text = label;
            EditorUtility.SetDirty(tmp);
        }

        var button = buttonGo.GetComponent<Button>();
        var loadButton = buttonGo.GetComponent<LevelLoadButton>();
        var scoreReseter = buttonGo.GetComponent<ScoreReseter>();
        if (scoreReseter == null)
            scoreReseter = Object.FindFirstObjectByType<ScoreReseter>();

        if (button == null || loadButton == null)
            return;

        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);

        UnityEventTools.AddStringPersistentListener(button.onClick, loadButton.LoadLevelByName, levelName);
        if (scoreReseter != null)
            UnityEventTools.AddPersistentListener(button.onClick, scoreReseter.ResetScore);

        EditorUtility.SetDirty(button);
    }

    static void LayoutLevelSelectButtons(Transform levelSelect)
    {
        var names = new[] { "LevelOneButton", "LevelTwoButton", "LevelThreeButton", "LevelFourButton" };
        // Vertical list centered in Level Select panel.
        float startY = 150f;
        float step = -70f;

        for (int i = 0; i < names.Length; i++)
        {
            var t = levelSelect.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == names[i]);
            if (t == null)
                continue;

            var rt = t.GetComponent<RectTransform>();
            if (rt == null)
                continue;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, startY + step * i);
            if (rt.sizeDelta.x < 100f)
                rt.sizeDelta = new Vector2(550f, 70f);
            EditorUtility.SetDirty(rt);
        }
    }

    struct EnemyPlacement
    {
        public GameObject Prefab;
        public Vector3 Position;
        public int Health;
        public float MoveSpeed;
        public float FollowRange;
    }
}
#endif
