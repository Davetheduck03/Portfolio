#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click tool that:
///   1. Slices all Fox sprite sheets (256×256 grid)
///   2. Creates AnimationClips for each direction per animation state
///   3. Builds a state machine in Fox.controller with separate blend trees per state:
///        Idle  ──(Speed > 0.05)──▶  Walk
///        Walk  ──(Speed < 0.05)──▶  Idle
///
/// To add new states (Push, Attack, etc.) later:
///   - Add a new entry to the AnimStates array
///   - Add the sprite sheets to Fox_FullBundle/[AnimName]/
///   - Add the transition conditions to BuildStateMachine()
///   - Re-run the tool from Tools ▶ Project ▶ Setup Fox 8-Direction Blend Tree
/// </summary>
public static class FoxBlendTreeSetup
{
    // ---------------------------------------------------------------
    //  Paths
    // ---------------------------------------------------------------
    private const string ControllerPath = "Assets/Project_Folder/Anim/Fox/Fox.controller";
    private const string ClipOutputPath = "Assets/Project_Folder/Anim/Fox";
    private const string FoxBundlePath  = "Assets/Fox_FullBundle";
    private const string SpritePrefix   = "Fox";

    // ---------------------------------------------------------------
    //  Sprite sheet layout  (all sheets use 256×256 frames, 6 columns)
    //  Walk = 6×2 = 12 frames | Idle = 6×4 = 24 frames
    // ---------------------------------------------------------------
    private const int FrameSize = 256;
    private const int SheetCols = 6;

    // ---------------------------------------------------------------
    //  Animation states — add new rows here for future animations
    //
    //  rows       : number of rows in the sprite sheet (used for UV layout)
    //  frameCount : actual filled frames — may be less than rows×SheetCols
    //               if the last row isn't fully used
    // ---------------------------------------------------------------
    private static readonly (string name, int rows, int frameCount, float fps)[] AnimStates =
    {
        ("Idle", 4, 20, 12f),   // 6×4 sheet but only 20 frames filled (last row has 2)
        ("Walk", 2, 12, 12f),   // 6×2 sheet, all 12 frames filled
        // ("Push", 2, 12, 12f),   ← future example
    };

    // ---------------------------------------------------------------
    //  Animator parameters
    // ---------------------------------------------------------------
    private const string ParamMoveX = "MoveX";
    private const string ParamMoveY = "MoveY";
    private const string ParamSpeed = "Speed";

    // ---------------------------------------------------------------
    //  Direction mapping  (counter-clockwise from South, confirmed by sprites)
    //
    //  dir1 = S   dir2 = SW   dir3 = W   dir4 = NW
    //  dir5 = N   dir6 = NE   dir7 = E   dir8 = SE
    // ---------------------------------------------------------------
    private static readonly (int dir, Vector2 pos)[] Directions =
    {
        (1, new Vector2( 0f,           -1f)),           // S
        (2, new Vector2(-0.7071f,      -0.7071f)),      // SW
        (3, new Vector2(-1f,            0f)),            // W
        (4, new Vector2(-0.7071f,       0.7071f)),      // NW
        (5, new Vector2( 0f,            1f)),            // N
        (6, new Vector2( 0.7071f,       0.7071f)),      // NE
        (7, new Vector2( 1f,            0f)),            // E
        (8, new Vector2( 0.7071f,      -0.7071f)),      // SE
    };

    // ---------------------------------------------------------------
    [MenuItem("Tools/Project/Setup Fox 8-Direction Blend Tree")]
    public static void Run()
    {
        // --- Step 1: Slice all sprite sheets -----------------------
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var (animName, rows, frameCount, _) in AnimStates)
                for (int dir = 1; dir <= 8; dir++)
                    SliceSheet($"{FoxBundlePath}/{animName}/{SpritePrefix}_{animName}_dir{dir}.png",
                               animName, dir, rows, frameCount);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        // --- Step 2: Create AnimationClips per state ---------------
        // clips[stateIndex][directionIndex 0..7]
        var clips = new AnimationClip[AnimStates.Length][];

        for (int s = 0; s < AnimStates.Length; s++)
        {
            var (animName, _, frameCount, fps) = AnimStates[s];
            clips[s] = new AnimationClip[8];

            for (int i = 0; i < 8; i++)
                clips[s][i] = CreateClip(animName, Directions[i].dir, frameCount, fps);
        }

        AssetDatabase.Refresh();

        // --- Step 3: Build state machine ---------------------------
        BuildStateMachine(clips);

        Debug.Log("[FoxBlendTree] Setup complete. Open the Animator window on Fox.controller to review.");
    }

    // ---------------------------------------------------------------
    //  Sprite slicing
    // ---------------------------------------------------------------
    private static void SliceSheet(string assetPath, string animName, int dir, int rows, int frameCount)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[FoxBlendTree] Sprite not found: {assetPath}");
            return;
        }

        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode       = FilterMode.Point;
        importer.mipmapEnabled    = false;

        // Only slice the actual filled frames — skip empty cells at the end
        var metadata = new SpriteMetaData[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            int col      = i % SheetCols;
            int rowIdx   = i / SheetCols;
            int yFlipped = (rows - 1 - rowIdx) * FrameSize;  // Unity y-axis is bottom-up

            metadata[i] = new SpriteMetaData
            {
                name      = $"{SpritePrefix}_{animName}_dir{dir}_{i:D2}",
                rect      = new Rect(col * FrameSize, yFlipped, FrameSize, FrameSize),
                alignment = (int)SpriteAlignment.Center,
                pivot     = new Vector2(0.5f, 0.5f),
            };
        }

        importer.spritesheet = metadata;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------
    //  AnimationClip creation
    // ---------------------------------------------------------------
    private static AnimationClip CreateClip(string animName, int dir, int frameCount, float fps)
    {
        string spritePath = $"{FoxBundlePath}/{animName}/{SpritePrefix}_{animName}_dir{dir}.png";

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"[FoxBlendTree] No sprites at {spritePath}");
            return null;
        }

        string clipName = $"Fox_{animName}_dir{dir}";
        string clipPath = $"{ClipOutputPath}/{clipName}.anim";

        AssetDatabase.DeleteAsset(clipPath);

        var clip      = new AnimationClip { name = clipName };
        clip.wrapMode = WrapMode.Loop;

        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

        // Repeat first frame at the end for a seamless loop
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[0] };

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    // ---------------------------------------------------------------
    //  State machine + blend tree construction
    // ---------------------------------------------------------------
    private static void BuildStateMachine(AnimationClip[][] clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FoxBlendTree] Controller not found at {ControllerPath}");
            return;
        }

        // Parameters
        TryAddParam(controller, ParamMoveX, AnimatorControllerParameterType.Float);
        TryAddParam(controller, ParamMoveY, AnimatorControllerParameterType.Float);
        TryAddParam(controller, ParamSpeed, AnimatorControllerParameterType.Float);

        // Clear existing states for a clean rebuild
        var sm = controller.layers[0].stateMachine;
        foreach (var cs in sm.states)
            sm.RemoveState(cs.state);

        // Build one blend tree state per animation
        var states = new AnimatorState[AnimStates.Length];
        for (int s = 0; s < AnimStates.Length; s++)
            states[s] = CreateBlendTreeState(controller, AnimStates[s].name, clips[s]);

        // Default state = Idle (index 0)
        sm.defaultState = states[0];

        // ---- Transitions ----------------------------------------
        // Idle (index 0) → Walk (index 1)
        AddTransition(states[0], states[1], ParamSpeed, AnimatorConditionMode.Greater, 0.05f);

        // Walk (index 1) → Idle (index 0)
        AddTransition(states[1], states[0], ParamSpeed, AnimatorConditionMode.Less, 0.05f);

        // Future states:  add transitions here as you add new states to AnimStates.
        // Example for Push (index 2) when you add it:
        //   AddTransition(states[1], states[2], "IsPushing", AnimatorConditionMode.If, 0);
        //   AddTransition(states[2], states[1], "IsPushing", AnimatorConditionMode.IfNot, 0);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }

    private static AnimatorState CreateBlendTreeState(AnimatorController controller,
                                                       string stateName,
                                                       AnimationClip[] dirClips)
    {
        BlendTree tree;
        AnimatorState state = controller.CreateBlendTreeInController(stateName, out tree);

        tree.blendType       = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter  = ParamMoveX;
        tree.blendParameterY = ParamMoveY;

        // Center (dir1 = South-facing) used as the rest pose when MoveX/MoveY = (0,0)
        tree.AddChild(dirClips[0], Vector2.zero);

        // 8 directional clips
        for (int i = 0; i < 8; i++)
            tree.AddChild(dirClips[i], Directions[i].pos);

        return state;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to,
                                      string param, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.hasExitTime  = false;
        t.duration     = 0.1f;
        t.AddCondition(mode, threshold, param);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------
    private static void TryAddParam(AnimatorController ctrl, string name,
                                    AnimatorControllerParameterType type)
    {
        if (ctrl.parameters.Any(p => p.name == name)) return;
        ctrl.AddParameter(name, type);
    }
}
#endif
