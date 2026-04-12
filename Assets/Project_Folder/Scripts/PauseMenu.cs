using UnityEngine;
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
        // Position sliders from saved PlayerPrefs.
        // OnValueChanged fires → AudioManager applies the volume.
        InitSlider(audioSlider, AudioManager.MasterKey);
        InitSlider(musicSlider, AudioManager.MusicKey);
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
        => AudioManager.Instance?.SetMasterVolume(value);

    /// <summary>Called by the Music slider's OnValueChanged event.</summary>
    public void OnMusicSliderChanged(float value)
        => AudioManager.Instance?.SetMusicVolume(value);

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
    /// Loads the saved linear volume from PlayerPrefs (default 1) and
    /// applies it to the slider. The slider's OnValueChanged then fires
    /// automatically and pushes the value into the AudioMixer.
    /// </summary>
    private void InitSlider(Slider slider, string prefsKey)
    {
        if (slider == null) return;
        // Setting .value fires OnValueChanged → AudioManager applies the volume.
        slider.value = PlayerPrefs.GetFloat(prefsKey, 1f);
    }
}
