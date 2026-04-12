using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays a looping background track in the menu / level-select scenes and
/// silences itself in gameplay scenes.  Persists across scene loads.
///
/// Setup:
///   1. Run  Tools → UI → Create Menu Music Manager  to add the GameObject,
///      or just add this component to any persistent GameObject manually.
///   2. Assign your BGM AudioClip to  Menu Music Clip.
///   3. Optionally assign your AudioMixer Music group to  Output Mixer Group
///      so the track obeys the Music Volume slider.
///   4. List every scene build-index where music should play in
///      Menu Scene Indices  (e.g. 0 = Main Menu, 2 = Level Select).
///      Leave the list empty to play in ALL scenes.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MenuMusicManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Singleton
    // ----------------------------------------------------------------

    public static MenuMusicManager Instance { get; private set; }

    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Music")]
    [Tooltip("The clip that loops in menu / level-select scenes.")]
    [SerializeField] private AudioClip menuMusicClip;

    [Tooltip("Assign the 'Music' AudioMixerGroup so this track obeys the " +
             "Music Volume slider set in PauseMenu / MainMenu.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Tooltip("Build indices of scenes where music should play. " +
             "Leave empty to play in EVERY scene.")]
    [SerializeField] private int[] menuSceneIndices;

    [Header("Volume")]
    [Range(0f, 1f)]
    [Tooltip("Starting volume before any PlayerPrefs setting is applied.")]
    [SerializeField] private float defaultVolume = 0.8f;

    // ----------------------------------------------------------------
    //  Private
    // ----------------------------------------------------------------

    private AudioSource _source;
    private bool        _silenced;          // true while a level track is playing
    private const string MusicVolumeKey = "MusicVolume";   // matches AudioManager key

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Awake()
    {
        // Only one instance lives across scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source             = GetComponent<AudioSource>();
        _source.clip        = menuMusicClip;
        _source.loop        = true;
        _source.playOnAwake = false;
        _source.volume      = PlayerPrefs.GetFloat(MusicVolumeKey, defaultVolume);

        if (outputMixerGroup != null)
            _source.outputAudioMixerGroup = outputMixerGroup;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Play immediately if the starting scene is a menu scene
        UpdatePlayback(SceneManager.GetActiveScene().buildIndex);
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
        UpdatePlayback(scene.buildIndex);
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private void UpdatePlayback(int buildIndex)
    {
        if (ShouldPlayInScene(buildIndex))
        {
            if (!_source.isPlaying)
            {
                _source.clip = menuMusicClip;
                _source.Play();
            }
        }
        else
        {
            if (_source.isPlaying)
                _source.Stop();
        }
    }

    private bool ShouldPlayInScene(int buildIndex)
    {
        if (menuMusicClip == null) return false;

        // Empty array = play everywhere
        if (menuSceneIndices == null || menuSceneIndices.Length == 0)
            return true;

        foreach (int idx in menuSceneIndices)
            if (idx == buildIndex) return true;

        return false;
    }

    // ----------------------------------------------------------------
    //  Public API
    // ----------------------------------------------------------------

    /// <summary>
    /// Applies the music volume to the AudioSource.
    /// Called by AudioManager whenever the music slider changes.
    /// Has no effect while a level track has silenced this manager.
    /// </summary>
    public void ApplyMusicVolume(float linearVolume)
    {
        if (_silenced || _source == null) return;
        _source.volume = Mathf.Clamp01(linearVolume);
    }

    /// <summary>
    /// Temporarily silences the AudioSource without touching PlayerPrefs.
    /// Called by LevelMusicManager when a level track takes over.
    /// </summary>
    internal void SilenceForLevel()
    {
        _silenced = true;
        if (_source != null) _source.volume = 0f;
    }

    /// <summary>
    /// Restores the AudioSource volume from the player's saved preference.
    /// Called by LevelMusicManager when returning to a non-level scene.
    /// </summary>
    internal void RestoreVolume()
    {
        _silenced = false;
        if (_source != null)
            _source.volume = Mathf.Clamp01(
                PlayerPrefs.GetFloat(MusicVolumeKey, defaultVolume));
    }
}
