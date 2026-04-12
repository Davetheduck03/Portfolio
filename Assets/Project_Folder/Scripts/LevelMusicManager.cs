using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that plays per-level background music.
///
/// How it works
/// ────────────
/// • This GameObject is DontDestroyOnLoad — it lives for the entire session.
/// • Each level scene contains a lightweight <see cref="LevelMusicSource"/>
///   component that declares which clip to play.
/// • On every scene load this manager checks whether the incoming clip is the
///   same as the one already playing.
///     ▸ Same clip  → keep playing (no restart — survives death/respawn).
///     ▸ New clip   → switch tracks.
///     ▸ No source  → stop (e.g. returning to the main menu).
///
/// Setup
/// ─────
///   1. Run  Tools → Audio → Create Level Music Manager  in your first scene.
///   2. Add a  LevelMusicSource  component to a GameObject in every level scene
///      and assign its AudioClip.
///   3. Optionally assign the Music AudioMixerGroup so the track obeys the
///      player's volume slider.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class LevelMusicManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Singleton
    // ----------------------------------------------------------------

    public static LevelMusicManager Instance { get; private set; }

    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Mixer (optional)")]
    [Tooltip("Assign the 'Music' AudioMixerGroup so the track obeys the " +
             "Music Volume slider.  Individual LevelMusicSource components " +
             "can override this per-scene.")]
    [SerializeField] private AudioMixerGroup defaultMixerGroup;

    // ----------------------------------------------------------------
    //  Private
    // ----------------------------------------------------------------

    private AudioSource _source;
    private AudioClip   _currentClip;       // clip that is currently playing (or null)
    private float       _trackVolume = 1f;  // per-clip natural volume from LevelMusicSource
    private float       _musicVolume = 1f;  // player's saved music slider value

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

        _source             = GetComponent<AudioSource>();
        _source.loop        = true;
        _source.playOnAwake = false;

        if (defaultMixerGroup != null)
            _source.outputAudioMixerGroup = defaultMixerGroup;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ----------------------------------------------------------------
    //  Scene events
    // ----------------------------------------------------------------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find the data component placed in this scene.
        LevelMusicSource src = FindFirstObjectByType<LevelMusicSource>();

        if (src == null || src.Clip == null)
        {
            // No level music for this scene — stop and let MenuMusicManager take over.
            StopMusic();
            return;
        }

        // ── Same clip already playing? Do nothing — music continues seamlessly. ──
        if (src.Clip == _currentClip && _source.isPlaying)
            return;

        // ── Different (or first) clip — switch tracks. ──
        _currentClip   = src.Clip;
        _trackVolume   = src.Volume;
        _source.clip   = _currentClip;

        // Scale the per-clip natural volume by the player's saved music preference.
        _musicVolume         = PlayerPrefs.GetFloat(AudioManager.MusicKey, 1f);
        _source.volume       = _trackVolume * _musicVolume;

        // Per-scene mixer group overrides the default if assigned.
        _source.outputAudioMixerGroup =
            src.MixerGroup != null ? src.MixerGroup : defaultMixerGroup;

        _source.Play();

        // Silence the persistent menu music without overwriting the player's
        // saved volume preference (SilenceForLevel only touches the AudioSource).
        if (MenuMusicManager.Instance != null)
            MenuMusicManager.Instance.SilenceForLevel();
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private void StopMusic()
    {
        if (!_source.isPlaying && _currentClip == null) return;

        _source.Stop();
        _currentClip = null;

        // Restore the menu music to the player's saved volume preference
        // without writing anything new to PlayerPrefs.
        if (MenuMusicManager.Instance != null)
            MenuMusicManager.Instance.RestoreVolume();
    }

    // ----------------------------------------------------------------
    //  Public API
    // ----------------------------------------------------------------

    /// <summary>
    /// Called by AudioManager whenever the music slider changes.
    /// Scales the current track's natural volume by the new player preference.
    /// </summary>
    public void ApplyMusicVolume(float linearVolume)
    {
        _musicVolume = Mathf.Clamp01(linearVolume);
        if (_source != null && _source.isPlaying)
            _source.volume = _trackVolume * _musicVolume;
    }
}
