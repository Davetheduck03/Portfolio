using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays a looping background track for a single level.
/// Place one of these on a GameObject in every level scene.
///
/// The track starts automatically on scene load and is destroyed with the
/// scene when you leave — no cleanup needed.
///
/// Volume is driven by the AudioMixer so it respects the player's
/// Music Volume slider.  Assign the same 'Music' mixer group you use
/// everywhere else.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class LevelMusicManager : MonoBehaviour
{
    [Header("Music")]
    [Tooltip("The BGM clip for this specific level. Loops automatically.")]
    [SerializeField] private AudioClip levelMusic;

    [Tooltip("Assign the 'Music' AudioMixerGroup so the track obeys " +
             "the Music Volume slider set by the player.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("Volume")]
    [Range(0f, 1f)]
    [Tooltip("Base volume for this track (before the mixer applies gain).")]
    [SerializeField] private float volume = 0.8f;

    private AudioSource _source;

    void Awake()
    {
        _source             = GetComponent<AudioSource>();
        _source.clip        = levelMusic;
        _source.loop        = true;
        _source.playOnAwake = false;
        _source.volume      = volume;

        if (outputMixerGroup != null)
            _source.outputAudioMixerGroup = outputMixerGroup;

        // If the persistent MenuMusicManager is still playing (e.g. the player
        // came from the level-select without a gap), silence it now.
        if (MenuMusicManager.Instance != null)
            MenuMusicManager.Instance.SetVolume(0f);
    }

    void Start()
    {
        if (levelMusic != null)
            _source.Play();
        else
            Debug.LogWarning("[LevelMusicManager] No level music clip assigned.", this);
    }

    void OnDestroy()
    {
        // Restore MenuMusicManager volume when this level unloads, so that
        // returning to the menu / level-select will have music again.
        if (MenuMusicManager.Instance != null)
        {
            float savedVol = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            MenuMusicManager.Instance.SetVolume(savedVol);
        }
    }
}
