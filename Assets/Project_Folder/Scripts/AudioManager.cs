using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton — the single source of truth for Master and Music volume.
///
///   Master  →  AudioListener.volume   (scales EVERY AudioSource in the game, zero setup)
///   Music   →  pushed directly to MenuMusicManager + LevelMusicManager AudioSources
///
/// Both values are saved to PlayerPrefs so they survive between sessions.
///
/// Setup
/// ─────
///   Add this component once to your first scene (e.g. MainMenu).
///   It survives every scene load via DontDestroyOnLoad.
///
/// Slider wiring (MainMenu / PauseMenu)
/// ─────────────────────────────────────
///   Master slider OnValueChanged  →  AudioManager.Instance.SetMasterVolume
///   Music  slider OnValueChanged  →  AudioManager.Instance.SetMusicVolume
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Singleton
    // ----------------------------------------------------------------

    public static AudioManager Instance { get; private set; }

    // ----------------------------------------------------------------
    //  PlayerPrefs keys — must match the strings used by MainMenu / PauseMenu
    // ----------------------------------------------------------------

    public const string MasterKey = "MasterVolume";
    public const string MusicKey  = "MusicVolume";

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioListener.volume is the global scale for every AudioSource — apply immediately.
        AudioListener.volume = PlayerPrefs.GetFloat(MasterKey, 1f);

        // Re-apply on every scene load so managers that start in a new scene
        // receive the saved volume.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioListener.volume = PlayerPrefs.GetFloat(MasterKey, 1f);

        // Give scene objects one frame to finish Awake / Start before we push music volume.
        StartCoroutine(ApplyMusicNextFrame());
    }

    private IEnumerator ApplyMusicNextFrame()
    {
        yield return null;
        PushMusicVolume(PlayerPrefs.GetFloat(MusicKey, 1f));
    }

    // ----------------------------------------------------------------
    //  Public API  — call these from slider OnValueChanged events
    // ----------------------------------------------------------------

    /// <summary>
    /// Sets the master volume (0–1).
    /// Writes through to AudioListener.volume which scales every AudioSource
    /// in the project with no per-source wiring required.
    /// </summary>
    public void SetMasterVolume(float v)
    {
        v = Mathf.Clamp01(v);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(MasterKey, v);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Sets the music volume (0–1).
    /// Pushes the value directly to MenuMusicManager and LevelMusicManager.
    /// </summary>
    public void SetMusicVolume(float v)
    {
        v = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(MusicKey, v);
        PlayerPrefs.Save();
        PushMusicVolume(v);
    }

    // ----------------------------------------------------------------
    //  Private
    // ----------------------------------------------------------------

    private void PushMusicVolume(float v)
    {
        MenuMusicManager.Instance?.ApplyMusicVolume(v);
        LevelMusicManager.Instance?.ApplyMusicVolume(v);
    }
}
