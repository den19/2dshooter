using UnityEngine;

/// <summary>
/// Assigns Camera.main to world-space UI canvases on level scenes (no separate GUI camera).
/// </summary>
[DisallowMultipleComponent]
public class UICameraBinder : MonoBehaviour
{
    void Awake()
    {
        var cam = Camera.main;
        if (cam == null)
            return;

        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = cam;
        }
    }
}
