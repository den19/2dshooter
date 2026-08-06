using TMPro;
using UnityEngine;

/// <summary>
/// Appends the app build version to MainMenuTitle on the same TMP item,
/// separated by a space, with smaller/softer rich-text styling for readability.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class MainMenuTitleVersion : MonoBehaviour
{
    [Tooltip("Base title shown before the version. If empty, uses the current TMP text.")]
    [SerializeField] string titleBase = "AsurviL";

    [Tooltip("Version size relative to the title font size (TMP rich-text %).")]
    [SerializeField] [Range(30, 90)] int versionSizePercent = 42;

    [Tooltip("Version alpha (00–FF) so the build number stays secondary to the title.")]
    [SerializeField] [Range(0x40, 0xFF)] int versionAlpha = 0xB0;

    TextMeshProUGUI titleText;

    void Awake()
    {
        titleText = GetComponent<TextMeshProUGUI>();
        Apply();
    }

    void Apply()
    {
        if (titleText == null)
            return;

        var baseTitle = string.IsNullOrWhiteSpace(titleBase) ? titleText.text : titleBase.Trim();
        var version = Application.version;
        if (string.IsNullOrWhiteSpace(version))
        {
            titleText.text = baseTitle;
            return;
        }

        // Same TMP item: "AsurviL 1.2.12" with a quieter build number after a space.
        titleText.text =
            $"{baseTitle} <size={versionSizePercent}%><alpha=#{versionAlpha:X2}>{version.Trim()}</alpha></size>";
    }
}
