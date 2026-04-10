using UnityEngine;

/// <summary>
/// Debug utility for managing level unlock state during development.
///
/// Usage:
///   • Add this component to any GameObject in any scene.
///   • Assign the build indices of all your levels to the All Level Indices array.
///   • Right-click the component header in the Inspector to run commands, OR
///     press the keyboard shortcuts during Play mode.
///
/// Shortcuts (Play mode only):
///   Ctrl + Shift + U  — Unlock all levels
///   Ctrl + Shift + R  — Reset progress (lock all except first)
///
/// IMPORTANT: Remove or disable this component before shipping.
/// </summary>
public class DebugLevelUnlock : MonoBehaviour
{
    [Tooltip("Build indices of every level in your game, in play order.\n" +
             "Must match the entries in your LevelSelectMenu.")]
    [SerializeField] private int[] allLevelIndices;

    // ----------------------------------------------------------------
    //  Keyboard shortcuts (Play mode)
    // ----------------------------------------------------------------

    void Update()
    {
        bool ctrl  = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift);

        if (ctrl && shift && Input.GetKeyDown(KeyCode.U))
            UnlockAllLevels();

        if (ctrl && shift && Input.GetKeyDown(KeyCode.R))
            ResetProgress();
    }

    // ----------------------------------------------------------------
    //  Context menu commands (Inspector right-click, works in Edit mode too)
    // ----------------------------------------------------------------

    [ContextMenu("Unlock All Levels")]
    public void UnlockAllLevels()
    {
        if (allLevelIndices == null || allLevelIndices.Length == 0)
        {
            Debug.LogWarning("[DebugLevelUnlock] All Level Indices is empty. " +
                             "Fill in the build indices of your levels.");
            return;
        }

        if (LevelProgressManager.Instance != null)
        {
            LevelProgressManager.Instance.UnlockAll(allLevelIndices);
        }
        else
        {
            // No runtime instance — write directly to PlayerPrefs
            // (useful when running from the Inspector outside Play mode)
            foreach (int idx in allLevelIndices)
                PlayerPrefs.SetInt("Level_Unlocked_" + idx, 1);
            PlayerPrefs.Save();
            Debug.Log($"[DebugLevelUnlock] Unlocked {allLevelIndices.Length} levels via PlayerPrefs.");
        }

        // If a LevelSelectMenu is in the scene, refresh it immediately
        FindAnyObjectByType<LevelSelectMenu>()?.Refresh();
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        if (allLevelIndices == null || allLevelIndices.Length == 0)
        {
            Debug.LogWarning("[DebugLevelUnlock] All Level Indices is empty.");
            return;
        }

        if (LevelProgressManager.Instance != null)
        {
            LevelProgressManager.Instance.ResetProgress(allLevelIndices);
        }
        else
        {
            foreach (int idx in allLevelIndices)
                PlayerPrefs.DeleteKey("Level_Unlocked_" + idx);

            if (allLevelIndices.Length > 0)
                PlayerPrefs.SetInt("Level_Unlocked_" + allLevelIndices[0], 1);

            PlayerPrefs.Save();
            Debug.Log("[DebugLevelUnlock] Progress reset via PlayerPrefs.");
        }

        FindAnyObjectByType<LevelSelectMenu>()?.Refresh();
    }

    [ContextMenu("Print Unlock State")]
    public void PrintUnlockState()
    {
        if (allLevelIndices == null || allLevelIndices.Length == 0)
        {
            Debug.LogWarning("[DebugLevelUnlock] All Level Indices is empty.");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[DebugLevelUnlock] Current unlock state:");

        foreach (int idx in allLevelIndices)
        {
            bool unlocked = PlayerPrefs.GetInt("Level_Unlocked_" + idx, 0) == 1;
            sb.AppendLine($"  Scene {idx}: {(unlocked ? "UNLOCKED" : "locked")}");
        }

        Debug.Log(sb.ToString());
    }
}
