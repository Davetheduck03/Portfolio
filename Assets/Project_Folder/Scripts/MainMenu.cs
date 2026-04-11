using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Main Menu controller.
///
/// • If no progress exists:  shows START  (loads the first level).
/// • If any progress exists: shows CONTINUE (loads the latest unlocked level).
/// • LEVEL SELECT  → level select scene.
/// • SETTINGS      → slides in the settings panel (audio / music sliders).
/// • CREDITS       → slides in the credits panel.
/// • QUIT          → exits the application (stops play mode in the editor).
///
/// Setup:
///   Run Tools → UI → Create Main Menu to build the canvas automatically.
///   Fill in the All Level Indices array with your level build indices IN ORDER
///   so Continue knows which level to resume from.
/// </summary>
public class MainMenu : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Scene indices
    // ----------------------------------------------------------------

    [Header("Scene Indices")]
    [Tooltip("Build index of the very first level (used by the Start button).")]
    [SerializeField] private int firstLevelIndex = 3;

    [Tooltip("Build index of the Level Select scene.")]
    [SerializeField] private int levelSelectSceneIndex = 2;

    [Tooltip("All level build indices IN PLAY ORDER. " +
             "Used to find the latest unlocked level for the Continue button.")]
    [SerializeField] private int[] allLevelIndices;

    // ----------------------------------------------------------------
    //  Button references
    // ----------------------------------------------------------------

    [Header("Buttons")]
    [Tooltip("Shown only when the player has no saved progress.")]
    [SerializeField] private GameObject startButton;

    [Tooltip("Shown only when the player has saved progress.")]
    [SerializeField] private GameObject continueButton;

    // ----------------------------------------------------------------
    //  Panels
    // ----------------------------------------------------------------

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    // ----------------------------------------------------------------
    //  Settings audio
    // ----------------------------------------------------------------

    [Header("Settings — Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam  = "MusicVolume";
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Start()
    {
        // Ensure LevelProgressManager exists (it may not have been created
        // yet if this is the first scene the player opens).
        if (LevelProgressManager.Instance == null)
        {
            GameObject pm = new GameObject("LevelProgressManager");
            pm.AddComponent<LevelProgressManager>();
        }

        // Unlock the very first level so the Start button always works.
        LevelProgressManager.Instance?.UnlockLevel(firstLevelIndex);

        RefreshStartContinue();
        InitAudioSliders();

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel  != null) creditsPanel.SetActive(false);
    }

    // ----------------------------------------------------------------
    //  Button callbacks
    // ----------------------------------------------------------------

    public void OnStartPressed()
    {
        SceneManager.LoadScene(firstLevelIndex);
    }

    public void OnContinuePressed()
    {
        SceneManager.LoadScene(GetLatestUnlockedLevel());
    }

    public void OnLevelSelectPressed()
    {
        SceneManager.LoadScene(levelSelectSceneIndex);
    }

    public void OnSettingsPressed()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (creditsPanel  != null) creditsPanel.SetActive(false);
    }

    public void OnSettingsClose()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnCreditsPressed()
    {
        if (creditsPanel  != null) creditsPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnCreditsClose()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----------------------------------------------------------------
    //  Audio slider callbacks
    // ----------------------------------------------------------------

    public void OnMasterSliderChanged(float value)
        => SetMixerVolume(masterVolumeParam, value);

    public void OnMusicSliderChanged(float value)
        => SetMixerVolume(musicVolumeParam, value);

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    /// Shows Start if no progress, Continue if any progress exists.
    private void RefreshStartContinue()
    {
        bool hasProgress = HasAnyProgress();

        if (startButton    != null) startButton.SetActive(!hasProgress);
        if (continueButton != null) continueButton.SetActive(hasProgress);
    }

    /// Returns true if the player has completed at least one level
    /// or has unlocked a level beyond the first.
    private bool HasAnyProgress()
    {
        if (LevelProgressManager.Instance == null) return false;
        if (allLevelIndices == null || allLevelIndices.Length == 0) return false;

        foreach (int idx in allLevelIndices)
        {
            if (LevelProgressManager.Instance.IsCompleted(idx)) return true;

            // Any level after the first being unlocked = progress was made
            if (idx != allLevelIndices[0] &&
                LevelProgressManager.Instance.IsUnlocked(idx)) return true;
        }

        return false;
    }

    /// Returns the build index of the last level the player has unlocked.
    private int GetLatestUnlockedLevel()
    {
        if (LevelProgressManager.Instance == null ||
            allLevelIndices == null ||
            allLevelIndices.Length == 0)
            return firstLevelIndex;

        int latest = allLevelIndices[0];
        foreach (int idx in allLevelIndices)
        {
            if (LevelProgressManager.Instance.IsUnlocked(idx))
                latest = idx;
        }

        return latest;
    }

    private void SetMixerVolume(string param, float linearValue)
    {
        if (audioMixer == null) return;
        float db = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(param, db);
    }

    private void InitAudioSliders()
    {
        if (audioMixer == null) return;
        if (masterSlider != null) masterSlider.value = GetLinearVolume(masterVolumeParam);
        if (musicSlider  != null) musicSlider.value  = GetLinearVolume(musicVolumeParam);
    }

    private float GetLinearVolume(string param)
    {
        if (audioMixer.GetFloat(param, out float db))
            return Mathf.Pow(10f, db / 20f);
        return 1f;
    }
}
