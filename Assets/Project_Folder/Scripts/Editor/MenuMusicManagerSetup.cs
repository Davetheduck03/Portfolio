using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates a MenuMusicManager GameObject in the active scene.
/// Run via  Tools → UI → Create Menu Music Manager.
///
/// After running:
///   1. Assign your looping BGM clip to the  Menu Music Clip  field.
///   2. Assign the 'Music' AudioMixerGroup to  Output Mixer Group
///      so the track obeys the Music Volume slider.
///   3. Set  Menu Scene Indices  to the build indices of your menu scenes
///      (e.g. 0 for Main Menu, 2 for Level Select).
///      Leave it empty to play in every scene.
/// </summary>
public static class MenuMusicManagerSetup
{
    [MenuItem("Tools/UI/Create Menu Music Manager")]
    public static void CreateMenuMusicManager()
    {
        // Prevent duplicates
        if (Object.FindFirstObjectByType<MenuMusicManager>() != null)
        {
            EditorUtility.DisplayDialog(
                "Create Menu Music Manager",
                "A MenuMusicManager already exists in the scene.",
                "OK");
            return;
        }

        GameObject go = new GameObject("MenuMusicManager");
        Undo.RegisterCreatedObjectUndo(go, "Create Menu Music Manager");

        go.AddComponent<AudioSource>();
        go.AddComponent<MenuMusicManager>();

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        Debug.Log("[MenuMusicManagerSetup] Created. " +
                  "Assign your BGM clip and set Menu Scene Indices in the Inspector.");
    }
}
