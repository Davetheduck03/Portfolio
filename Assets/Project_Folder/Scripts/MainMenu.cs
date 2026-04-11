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
        LoadWithTransition(firstLevelIndex);
    }

    public void OnContinuePressed()
    {
        LoadWithTransition(GetLatestUnlockedLevel());
    }

    public void OnLevelSelectPressed()
    {
        LoadWithTransition(levelSelectSceneIndex);
    }

    private void LoadWithTransition(int buildIndex)
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadScene(buildIndex);
        else
            SceneManager.LoadScene(buildIndex);
    }

    public void OnSettingsPressed()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);

            // Force layout immediately — OnEnable fires before the layout pass,
            // leaving Handle Slide Area with zero width.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                settingsPanel.GetComponent<RectTransform>());

            // Ensure handleRect is wired (can become null after domain reloads)
            RepairSlider(masterSlider);
            RepairSlider(musicSlider);

            SyncSlider(masterSlider, masterVolumeParam);
            SyncSlider(musicSlider,  musicVolumeParam);
        }
        if (creditsPanel != null) creditsPanel.SetActive(false);
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

        // Use the manually configured list if provided
        if (allLevelIndices != null && allLevelIndices.Length > 0)
        {
            foreach (int idx in allLevelIndices)
            {
                if (LevelProgressManager.Instance.IsCompleted(idx)) return true;

                // Any level after the first being unlocked = progress was made
                if (idx != allLevelIndices[0] &&
                    LevelProgressManager.Instance.IsUnlocked(idx)) return true;
            }
            return false;
        }

        // Fallback: scan every scene in build settings
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            if (LevelProgressManager.Instance.IsCompleted(i)) return true;
            if (i != firstLevelIndex && LevelProgressManager.Instance.IsUnlocked(i)) return true;
        }

        return false;
    }

    /// Returns the build index of the last level the player has unlocked.
    private int GetLatestUnlockedLevel()
    {
        if (LevelProgressManager.Instance == null) return firstLevelIndex;

        // Use the manually configured list if provided
        if (allLevelIndices != null && allLevelIndices.Length > 0)
        {
            int latest = allLevelIndices[0];
            foreach (int idx in allLevelIndices)
            {
                if (LevelProgressManager.Instance.IsUnlocked(idx))
                    latest = idx;
            }
            return latest;
        }

        // Fallback: scan every scene in build settings from firstLevelIndex upward
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        int latestScene = firstLevelIndex;
        for (int i = firstLevelIndex; i < sceneCount; i++)
        {
            if (LevelProgressManager.Instance.IsUnlocked(i))
                latestScene = i;
        }
        return latestScene;
    }

    /// <summary>
    /// Converts a linear 0–1 value to decibels, sends it to the mixer,
    /// and saves it to PlayerPrefs so the setting is shared with PauseMenu.
    /// </summary>
    private void SetMixerVolume(string param, float linearValue)
    {
        // Always save — even if the mixer isn't assigned
        PlayerPrefs.SetFloat(param, linearValue);
        PlayerPrefs.Save();

        if (audioMixer == null) return;
        float db = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(param, db);
    }

    /// <summary>
    /// Loads saved volumes from PlayerPrefs on startup.
    /// Uses SetValueWithoutNotify so the sliders hold the right internal value
    /// without firing OnValueChanged (panel may be inactive; layout not ready yet).
    /// The AudioMixer is synced directly here instead.
    /// </summary>
    private void InitAudioSliders()
    {
        float master = PlayerPrefs.GetFloat(masterVolumeParam, 1f);
        float music  = PlayerPrefs.GetFloat(musicVolumeParam,  1f);

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (musicSlider  != null) musicSlider.SetValueWithoutNotify(music);

        // Apply to mixer directly since OnValueChanged won't fire
        ApplyToMixer(masterVolumeParam, master);
        ApplyToMixer(musicVolumeParam,  music);
    }

    /// <summary>
    /// If handleRect became null (domain reload / missing reference), finds it
    /// again by walking the slider's own hierarchy.  Without it, Unity silently
    /// skips handle movement inside UpdateVisuals() even though the fill works fine.
    /// </summary>
    private static void RepairSlider(Slider slider)
    {
        if (slider == null || slider.handleRect != null) return;

        Transform slideArea = slider.transform.Find("Handle Slide Area");
        if (slideArea == null) return;

        Transform handle = slideArea.Find("Handle");
        if (handle != null)
            slider.handleRect = handle.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Forces the slider to UpdateVisuals() with correct layout bounds.
    /// Nudging to a slightly different value then back guarantees that
    /// Slider.Set() sees a real change and repositions the handle.
    /// </summary>
    private void SyncSlider(Slider slider, string param)
    {
        if (slider == null) return;
        float v = PlayerPrefs.GetFloat(param, 1f);
        // Nudge away without notifying, then set the real value so
        // UpdateVisuals fires now that layout bounds are ready.
        slider.SetValueWithoutNotify(v < 1f ? v + 0.001f : v - 0.001f);
        slider.value = v;   // → UpdateVisuals() + OnValueChanged → mixer + PlayerPrefs
    }

    private void ApplyToMixer(string param, float linearValue)
    {
        if (audioMixer == null) return;
        float db = Mathf.Log10(Mathf.Max(linearValue, 0.001f)) * 20f;
        audioMixer.SetFloat(param, db);
    }
}
