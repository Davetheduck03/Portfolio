using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single entry point for level-wide events: showing the death screen,
/// restarting the level, and progressing to the next scene on completion.
///
/// Setup per level:
///   1. Add one LevelManager GameObject to the scene.
///   2. Build a Canvas with your death screen UI. Set it inactive by default.
///      Add a Restart button → LevelManager.RestartLevel
///      Add a Quit button   → LevelManager.QuitToMenu  (optional)
///      Drag the Canvas root into Death Screen Canvas.
///   3. Set Next Scene Index (build index of next level; -1 reloads this one).
///   4. Set Menu Scene Index (build index of your main menu; -1 to disable quit).
///   5. Wire every onPlayerKilled event (Enemy, SpikeBlock) → ShowDeathScreen.
///   6. Wire LevelEnd.OnLevelComplete → CompleteLevel.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Death Screen")]
    [Tooltip("Root GameObject of your death screen Canvas. " +
             "Leave it inactive in the scene — LevelManager activates it on death.")]
    [SerializeField] private GameObject deathScreenCanvas;

    [Header("Scene Progression")]
    [Tooltip("Build index of the scene to load when the level is completed.\n" +
             "Set to -1 to reload the current scene (handy during development).")]
    [SerializeField] private int nextSceneIndex = -1;

    [Tooltip("Build index of the main menu scene.\n" +
             "Set to -1 to disable the Quit button.")]
    [SerializeField] private int menuSceneIndex = -1;

    [Tooltip("Build index of the level select scene.\n" +
             "Set to -1 to disable the Choose Level button.")]
    [SerializeField] private int levelSelectSceneIndex = -1;

    // ----------------------------------------------------------------
    //  Private state
    // ----------------------------------------------------------------

    private static bool _loading; // guard against multiple LoadScene calls

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Awake()
    {
        _loading = false;

        // Make sure the death screen starts hidden
        if (deathScreenCanvas != null)
            deathScreenCanvas.SetActive(false);
    }

    // ----------------------------------------------------------------
    //  Public API — wire these to UnityEvents in the Inspector
    // ----------------------------------------------------------------

    /// <summary>
    /// Pauses the game and shows the death screen.
    /// Wire this to every onPlayerKilled event (Enemy, SpikeBlock, etc.).
    /// </summary>
    public void ShowDeathScreen()
    {
        if (_loading) return;

        Time.timeScale = 0f;

        if (deathScreenCanvas != null)
            deathScreenCanvas.SetActive(true);
        else
            // No canvas assigned — fall back to an immediate restart
            RestartLevel();
    }

    /// <summary>
    /// Resumes time and reloads the current scene from scratch, restoring
    /// every box, key, and door to its designed starting state.
    /// Wire the Restart button on your death screen Canvas to this.
    /// </summary>
    public void RestartLevel()
    {
        if (_loading) return;
        _loading = true;

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Resumes time and returns to the main menu.
    /// Wire the Quit button on your death screen Canvas to this.
    /// Does nothing if Menu Scene Index is -1.
    /// </summary>
    public void QuitToMenu()
    {
        if (_loading || menuSceneIndex < 0) return;
        _loading = true;

        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneIndex);
    }

    /// <summary>
    /// Loads the level select scene.
    /// Wire the Choose Level button on your death screen Canvas to this.
    /// Does nothing if Level Select Scene Index is -1.
    /// </summary>
    public void GoToLevelSelect()
    {
        if (_loading || levelSelectSceneIndex < 0) return;
        _loading = true;

        Time.timeScale = 1f;
        SceneManager.LoadScene(levelSelectSceneIndex);
    }

    /// <summary>
    /// Unlocks the next level then loads it.
    /// Wire this to LevelEnd.OnLevelComplete.
    /// </summary>
    public void CompleteLevel()
    {
        if (_loading) return;
        _loading = true;

        Time.timeScale = 1f;

        int target = nextSceneIndex >= 0
            ? nextSceneIndex
            : SceneManager.GetActiveScene().buildIndex;

        // Mark this level as completed and unlock the next one
        int current = SceneManager.GetActiveScene().buildIndex;
        LevelProgressManager.Instance?.MarkCompleted(current);
        LevelProgressManager.Instance?.UnlockLevel(target);

        SceneManager.LoadScene(target);
    }
}
