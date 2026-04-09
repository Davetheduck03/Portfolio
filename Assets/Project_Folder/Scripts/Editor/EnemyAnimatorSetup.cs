using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Editor utility that builds the Enemy Animator Controller from scratch:
///
///   1. Loads the sliced sprites from each devil-walk-*.png sheet.
///   2. Bakes one looping AnimationClip per direction.
///   3. Creates an AnimatorController with a single 2D Freeform Directional
///      blend tree driven by MoveX / MoveY float parameters.
///
/// Run via  Tools ▸ Enemy ▸ Create Enemy Animator
/// </summary>
public static class EnemyAnimatorSetup
{
    // ----------------------------------------------------------------
    //  Config — adjust if your folder layout differs
    // ----------------------------------------------------------------

    private const string SpriteFolder     = "Assets/Art/Enemy";
    private const string OutputFolder     = "Assets/Project_Folder/Anim/Enemy";
    private const string ControllerName   = "EnemyAnimator";
    private const float  FrameRate        = 8f;

    // Path from the Animator component down to the SpriteRenderer.
    // Leave empty ("") if the SpriteRenderer lives on the same GameObject
    // as the Animator.  Use e.g. "Sprite" if it is on a child called "Sprite".
    private const string SpriteRendererPath = "";

    // ----------------------------------------------------------------
    //  Direction table  (clip name, blend-tree X, blend-tree Y)
    // ----------------------------------------------------------------

    private static readonly (string clip, float x, float y)[] s_Directions =
    {
        ( "devil-walk-down",        0f,      -1f      ),
        ( "devil-walk-down-left",  -0.707f,  -0.707f  ),
        ( "devil-walk-down-right",  0.707f,  -0.707f  ),
        ( "devil-walk-left",       -1f,       0f      ),
        ( "devil-walk-right",       1f,       0f      ),
        ( "devil-walk-up",          0f,       1f      ),
        ( "devil-walk-up-left",    -0.707f,   0.707f  ),
        ( "devil-walk-up-right",    0.707f,   0.707f  ),
    };

    // ----------------------------------------------------------------
    //  Menu entry
    // ----------------------------------------------------------------

    [MenuItem("Tools/Enemy/Create Enemy Animator")]
    public static void CreateEnemyAnimator()
    {
        EnsureFolder(OutputFolder);

        // 1. Bake one AnimationClip per direction ─────────────────────
        var clips = new AnimationClip[s_Directions.Length];
        for (int i = 0; i < s_Directions.Length; i++)
            clips[i] = BakeClip(s_Directions[i].clip);

        // 2. Build Animator Controller ─────────────────────────────────
        string controllerPath = $"{OutputFolder}/{ControllerName}.controller";

        // Overwrite any existing controller at that path
        AnimatorController existing =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController ctrl =
            AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);

        // 3. Blend tree ────────────────────────────────────────────────
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        BlendTree      tree;
        AnimatorState  moveState = ctrl.CreateBlendTreeInController("Move", out tree);

        tree.blendType        = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter   = "MoveX";
        tree.blendParameterY  = "MoveY";

        for (int i = 0; i < s_Directions.Length; i++)
        {
            if (clips[i] == null) continue;
            tree.AddChild(clips[i], new Vector2(s_Directions[i].x, s_Directions[i].y));
        }

        sm.defaultState = moveState;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyAnimatorSetup] Created {controllerPath}");
        Selection.activeObject = ctrl;
        EditorGUIUtility.PingObject(ctrl);
    }

    // ----------------------------------------------------------------
    //  Clip baking
    // ----------------------------------------------------------------

    private static AnimationClip BakeClip(string sheetName)
    {
        string texPath = $"{SpriteFolder}/{sheetName}.png";

        // Load all sub-assets (sprites) from the sheet, sort by name
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texPath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"[EnemyAnimatorSetup] No sprites in '{texPath}'. " +
                             "Make sure the texture is imported as Sprite (Multiple).");
            return null;
        }

        // Create clip
        var clip = new AnimationClip
        {
            name      = sheetName,
            frameRate = FrameRate,
            wrapMode  = WrapMode.Loop
        };

        // Keyframes — one per sprite frame, plus a copy of the last
        // frame at the very end so Unity knows the clip duration.
        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = SpriteRendererPath,
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = sprites[i] };

        // End-of-clip sentinel (same sprite as the last frame)
        keys[sprites.Length] = new ObjectReferenceKeyframe
        {
            time  = sprites.Length / FrameRate,
            value = sprites[sprites.Length - 1]
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        // Loop settings
        var loopSettings = AnimationUtility.GetAnimationClipSettings(clip);
        loopSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, loopSettings);

        // Save
        string clipPath = $"{OutputFolder}/{sheetName}.anim";
        AssetDatabase.DeleteAsset(clipPath); // remove stale asset if re-running
        AssetDatabase.CreateAsset(clip, clipPath);

        return clip;
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private static void EnsureFolder(string folderPath)
    {
        // Walk the path and create any missing intermediate folders
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
