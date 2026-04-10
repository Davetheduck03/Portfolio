using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Handles pause menu behaviour: toggling via Escape, freezing time,
/// and routing the audio/music sliders into an AudioMixer.
///
/// Setup:
///   1. Run Tools → UI → Create Pause Menu to generate the Canvas.
///   2. Assign the AudioMixer (Window → Audio → Audio Mixer).
///   3. In the AudioMixer, right-click the Master volume knob → Expose,
///      then rename the exposed parameter to "MasterVolume".
///      Do the same for a Music group: expose it as "MusicVolume".
///   4. The editor script auto-assigns all fields — nothing to wire by hand.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Canvas")]
    [Tooltip("Root GameObject of the pause menu canvas.")]
    [SerializeField] private GameObject pauseCanvas;

    [Header("Audio")]
    [Tooltip("Your project's AudioMixer asset.")]
    [SerializeField] private AudioMixer audioMixer;

    [Tooltip("Exposed parameter name on the AudioMixer for master/SFX volume.")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";

    [Tooltip("Exposed parameter name on the AudioMixer for music volume.")]
    [SerializeField] private string musicVolumeParam = "MusicVolume";

    [Header("Sliders")]
    [SerializeField] private Slider audioSlider;
    [SerializeField] private Slider musicSlider;

    // ----------------------------------------------------------------
    //  Private state
    // ----------------------------------------------------------------

    private bool _paused;

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Awake()
    {
        if (pauseCanvas != null)
            pauseCanvas.SetActive(false);
    }

    void Start()
    {
        // Initialise slider positions from the mixer's current values
        InitSlider(audioSlider, masterVolumeParam);
        InitSlider(musicSlider, musicVolumeParam);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetPaused(!_paused);
    }

    // ----------------------------------------------------------------
    //  Public API — wire buttons and sliders from the editor script
    // ----------------------------------------------------------------

    /// <summary>Restart button: unpause then reload the level.</summary>
    public void OnRestartPressed()
    {
        SetPaused(false);
        LevelManager lm = FindAnyObjectByType<LevelManager>();
        lm?.RestartLevel();
    }

    /// <summary>Main Menu button: unpause then go to menu.</summary>
    public void OnMainMenuPressed()
    {
        SetPaused(false);
        LevelManager lm = FindAnyObjectByType<LevelManager>();
        lm?.QuitToMenu();
    }

    /// <summary>
    /// Called by the Audio slider's OnValueChanged event.
    /// Slider value 0–1 is converted to decibels (-80 to 0 dB).
    /// </summary>
    public void OnAudioSliderChanged(float value)
    {
        SetMixerVolume(masterVolumeParam, value);
    }

    /// <summary>
    /// Called by the Music slider's OnValueChanged event.
    /// Slider value 0–1 is converted to decibels (-80 to 0 dB).
    /// </summary>
    public void OnMusicSliderChanged(float value)
    {
        SetMixerVolume(musicVolumeParam, value);
    }

    // ----------------------------------------------------------------
    //  Pause logic
    // ----------------------------------------------------------------

    private void SetPaused(bool paused)
    {
        _paused = paused;
        Time.timeScale = _paused ? 0f : 1f;

        if (pauseCanvas != null)
            pauseCanvas.SetActive(_paused);
    }

    // ----------------------------------------------------------------
    //  Audio helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// Reads the current dB value from the mixer and converts it back
    /// to a 0–1 slider position so the UI reflects the actual volume.
    /// </summary>
    private void InitSlider(Slider slider, string param)
    {
        if (slider == null || audioMixer == null) return;

        if (audioMixer.GetFloat(param, out float db))
            slider.value = Mathf.Pow(10f, db / 20f);   // dB → linear
        else
            slider.value = 1f;
    }

    /// <summary>
    /// Converts a linear 0–1 value to decibels and sends it to the mixer.
    /// Clamps the minimum to 0.001 to avoid log(0) = -infinity.
    /// </summary>
    private void SetMixerVolume(string param, float linearValue)
    {
        if (audioMixer == null) return;

        float db = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(param, db);
    }
}
