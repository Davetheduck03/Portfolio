using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Creates a SceneTransition GameObject in the active scene.
/// Run via  Tools → UI → Create Scene Transition.
///
/// Only needs to be placed in your very first scene (e.g. MainMenu).
/// The DontDestroyOnLoad singleton survives all subsequent scene loads.
/// </summary>
public static class SceneTransitionSetup
{
    [MenuItem("Tools/UI/Create Scene Transition")]
    public static void Create()
    {
        // Prevent duplicates
        if (Object.FindFirstObjectByType<SceneTransition>() != null)
        {
            EditorUtility.DisplayDialog(
                "Scene Transition",
                "A SceneTransition already exists in this scene.\n\n" +
                "Only one is needed — it persists across all scenes automatically.",
                "OK");
            return;
        }

        GameObject go = new GameObject("SceneTransition");
        Undo.RegisterCreatedObjectUndo(go, "Create SceneTransition");

        // Canvas is required by SceneTransition (via RequireComponent)
        go.AddComponent<Canvas>();
        go.AddComponent<SceneTransition>();

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        Debug.Log(
            "[SceneTransitionSetup] SceneTransition created.\n\n" +
            "• Place it in your first scene (e.g. MainMenu) — it persists automatically.\n" +
            "• Replace SceneManager.LoadScene() calls with  SceneTransition.Instance.LoadScene(index)\n" +
            "• For death/respawn use  SceneTransition.Instance.ReloadScene()\n" +
            "• Tweak Wipe Duration, Curves, and Edge Softness in the Inspector.");
    }
}
