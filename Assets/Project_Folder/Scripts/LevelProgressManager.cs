using UnityEngine;

/// <summary>
/// Persistent singleton that tracks which levels are unlocked and completed.
/// Survives scene loads so progress is always accessible.
/// Data is stored in PlayerPrefs and persists between sessions.
///
/// Key format:
///   "Level_Unlocked_{buildIndex}"  = 1  → level is playable
///   "Level_Complete_{buildIndex}"  = 1  → level has been finished
/// </summary>
public class LevelProgressManager : MonoBehaviour
{
    public static LevelProgressManager Instance { get; private set; }

    private const string UnlockPrefix   = "Level_Unlocked_";
    private const string CompletePrefix = "Level_Complete_";

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
    }

    // ----------------------------------------------------------------
    //  Unlock API
    // ----------------------------------------------------------------

    /// <summary>Marks a level as unlocked and saves to disk.</summary>
    public void UnlockLevel(int buildIndex)
    {
        PlayerPrefs.SetInt(UnlockPrefix + buildIndex, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Returns true if the level has been unlocked.</summary>
    public bool IsUnlocked(int buildIndex)
        => PlayerPrefs.GetInt(UnlockPrefix + buildIndex, 0) == 1;

    // ----------------------------------------------------------------
    //  Completion API
    // ----------------------------------------------------------------

    /// <summary>Marks a level as completed and saves to disk.</summary>
    public void MarkCompleted(int buildIndex)
    {
        PlayerPrefs.SetInt(CompletePrefix + buildIndex, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Returns true if the level has been completed at least once.</summary>
    public bool IsCompleted(int buildIndex)
        => PlayerPrefs.GetInt(CompletePrefix + buildIndex, 0) == 1;

    /// <summary>
    /// Returns how many levels in the supplied build-index list have been completed.
    /// Used by chapter cards to show progress (e.g. "3 / 5").
    /// </summary>
    public int CountCompleted(int[] buildIndices)
    {
        int count = 0;
        foreach (int idx in buildIndices)
            if (IsCompleted(idx)) count++;
        return count;
    }

    // ----------------------------------------------------------------
    //  Bulk operations (debug tool)
    // ----------------------------------------------------------------

    /// <summary>Unlocks and marks every level in the list as completed.</summary>
    public void UnlockAll(int[] buildIndices)
    {
        foreach (int idx in buildIndices)
        {
            PlayerPrefs.SetInt(UnlockPrefix   + idx, 1);
            PlayerPrefs.SetInt(CompletePrefix + idx, 1);
        }
        PlayerPrefs.Save();
        Debug.Log($"[LevelProgressManager] Unlocked & completed {buildIndices.Length} levels.");
    }

    /// <summary>Clears all progress except the first level stays unlocked.</summary>
    public void ResetProgress(int[] buildIndices)
    {
        foreach (int idx in buildIndices)
        {
            PlayerPrefs.DeleteKey(UnlockPrefix   + idx);
            PlayerPrefs.DeleteKey(CompletePrefix + idx);
        }

        if (buildIndices.Length > 0)
            UnlockLevel(buildIndices[0]);

        PlayerPrefs.Save();
        Debug.Log("[LevelProgressManager] Progress reset. Only the first level is unlocked.");
    }
}
