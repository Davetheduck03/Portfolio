using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Lightweight data component — place one in every level scene.
/// Tells the persistent <see cref="LevelMusicManager"/> which clip to play
/// when this scene is loaded.
///
/// No AudioSource lives here; the LevelMusicManager owns the one AudioSource
/// that persists across scene loads so the track never restarts on death.
/// </summary>
public class LevelMusicSource : MonoBehaviour
{
    [Header("Music")]
    [Tooltip("The BGM clip for this level.")]
    [SerializeField] private AudioClip clip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Mixer (optional)")]
    [Tooltip("Overrides the LevelMusicManager's default mixer group for this scene only. " +
             "Leave empty to use the manager's default.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    // Read-only properties for LevelMusicManager
    public AudioClip       Clip       => clip;
    public float           Volume     => volume;
    public AudioMixerGroup MixerGroup => mixerGroup;
}
