using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central volume control for music and SFX via <see cref="AudioMixer"/> exposed parameters.
/// Persists settings with PlayerPrefs for future options menus.
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    private const string PrefMasterVolume = "Audio.MasterVolume";
    private const string PrefMusicVolume = "Audio.MusicVolume";
    private const string PrefSfxVolume = "Audio.SfxVolume";

    private const string ParamMasterVolume = "MasterVolume";
    private const string ParamMusicVolume = "MusicVolume";
    private const string ParamSfxVolume = "SfxVolume";

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Defaults (0–1)")]
    [SerializeField] private float defaultMasterVolume = 1f;
    [SerializeField] private float defaultMusicVolume = 0.2f;
    [SerializeField] private float defaultSfxVolume = 1f;

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("AudioSettings");
        if (prefab != null)
        {
            Instantiate(prefab);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(PrefMasterVolume, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(PrefMusicVolume, defaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(PrefSfxVolume, defaultSfxVolume);

        ApplyAllVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetMasterVolume(float volume01)
    {
        MasterVolume = Mathf.Clamp01(volume01);
        SetMixerVolume(ParamMasterVolume, MasterVolume);
        PlayerPrefs.SetFloat(PrefMasterVolume, MasterVolume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float volume01)
    {
        MusicVolume = Mathf.Clamp01(volume01);
        SetMixerVolume(ParamMusicVolume, MusicVolume);
        PlayerPrefs.SetFloat(PrefMusicVolume, MusicVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume01)
    {
        SfxVolume = Mathf.Clamp01(volume01);
        SetMixerVolume(ParamSfxVolume, SfxVolume);
        PlayerPrefs.SetFloat(PrefSfxVolume, SfxVolume);
        PlayerPrefs.Save();
    }

    public void ApplyAllVolumes()
    {
        SetMixerVolume(ParamMasterVolume, MasterVolume);
        SetMixerVolume(ParamMusicVolume, MusicVolume);
        SetMixerVolume(ParamSfxVolume, SfxVolume);
    }

    private void SetMixerVolume(string parameterName, float linearVolume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioSettingsManager: AudioMixer is not assigned.");
            return;
        }

        if (!audioMixer.SetFloat(parameterName, LinearToDecibels(linearVolume)))
        {
            Debug.LogWarning($"AudioSettingsManager: Could not set mixer parameter '{parameterName}'. Expose it on Master.mixer.");
        }
    }

    private static float LinearToDecibels(float linearVolume)
    {
        return linearVolume > 0.0001f ? Mathf.Log10(linearVolume) * 20f : -80f;
    }
}
