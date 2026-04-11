using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Removes any extra buttons from PauseMenu canvases that aren't part of
/// the standard setup (i.e. anything that isn't RestartButton or MainMenuButton).
///
/// Run via  Tools → UI → Remove Extra Buttons from Pause Menu
///
/// The standard PauseMenuSetup creates exactly two buttons:
///   • RestartButton
///   • MainMenuButton
///
/// Any other button (e.g. a manually added "Choose Level" button) is listed
/// in a confirmation dialog before being deleted.
/// </summary>
public static class RemoveChooseLevelButton
{
    // Known button names that the setup script creates — leave these alone.
    private static readonly HashSet<string> KnownButtons = new HashSet<string>
    {
        "RestartButton",
        "MainMenuButton",
    };

    [MenuItem("Tools/UI/Remove Extra Buttons from Pause Menu")]
    public static void RemoveExtraPauseMenuButtons()
    {
        PauseMenu[] pauseMenus =
            Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include,
                                               FindObjectsSortMode.None);

        if (pauseMenus.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Remove Extra Buttons",
                "No PauseMenu component found in the active scene.",
                "OK");
            return;
        }

        // Collect every Button child that is NOT in the known set
        List<GameObject> extras = new List<GameObject>();

        foreach (PauseMenu pm in pauseMenus)
        {
            Button[] buttons = pm.GetComponentsInChildren<Button>(includeInactive: true);
            foreach (Button btn in buttons)
            {
                if (!KnownButtons.Contains(btn.gameObject.name))
                    extras.Add(btn.gameObject);
            }
        }

        if (extras.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Remove Extra Buttons",
                "No extra buttons found. The Pause Menu already only contains " +
                "RestartButton and MainMenuButton.",
                "OK");
            return;
        }

        // Build a readable list for the dialog
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("The following button(s) will be permanently removed:\n");
        foreach (GameObject go in extras)
            sb.AppendLine($"  • {go.name}");
        sb.AppendLine("\nProceed?");

        bool confirmed = EditorUtility.DisplayDialog(
            "Remove Extra Buttons from Pause Menu",
            sb.ToString(),
            "Remove", "Cancel");

        if (!confirmed) return;

        int count = 0;
        foreach (GameObject go in extras)
        {
            Undo.DestroyObjectImmediate(go);
            Debug.Log($"[RemoveChooseLevelButton] Removed '{go.name}' from Pause Menu.");
            count++;
        }

        Debug.Log($"[RemoveChooseLevelButton] Done — removed {count} button(s). " +
                  "Use Edit → Undo if you need to restore them.");
    }
}
