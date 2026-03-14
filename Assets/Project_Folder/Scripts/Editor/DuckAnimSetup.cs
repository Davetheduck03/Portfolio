#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click tool that:
///   1. Slices all Duck sprite sheets (48×48 frames, single row each)
///   2. Creates AnimationClips for Idle, Walk, and Jump
///   3. Builds a state machine in Duck.controller:
///
///        ┌──────────────────────────────────────────────────────┐
///        │  Idle ──(Speed > 0.1)──▶ Walk                        │
///        │  Walk ──(Speed < 0.1)──▶ Idle                        │
///        │  Idle/Walk ──(!IsGrounded)──▶ Jump                   │
///        │  Jump ──(IsGrounded + Speed > 0.1)──▶ Walk           │
///        │  Jump ──(IsGrounded + Speed < 0.1)──▶ Idle           │
///        └──────────────────────────────────────────────────────┘
///
/// To add new states (Attack, Death, etc.) later:
///   - Add an entry to AnimStates
///   - Drop the sprite sheet in Assets/Fat Duckl/ with the matching name
///   - Add transition conditions in BuildStateMachine()
///   - Re-run Tools ▶ Project ▶ Setup Duck Animations
/// </summary>
public static class DuckAnimSetup
{
    // ---------------------------------------------------------------
    //  Paths
    // ---------------------------------------------------------------
    private const string ControllerPath = "Assets/Project_Folder/Anim/Duck/Duck.controller";
    private const string ClipOutputPath = "Assets/Project_Folder/Anim/Duck";
    private const string SpritePath     = "Assets/Fat Duckl";
    private const string SpritePrefix   = "chubby-duck";

    // ---------------------------------------------------------------
    //  Sprite sheet layout — all sheets are a single row of 48×48 frames
    // ---------------------------------------------------------------
    private const int FrameSize = 48;

    // ---------------------------------------------------------------
    //  Animation states
    //
    //  frameCount : number of frames in the sheet  (width / FrameSize)
    //  fps        : playback speed
    //  loop       : whether the clip loops
    // ---------------------------------------------------------------
    private static readonly (string name, int frameCount, float fps, bool loop)[] AnimStates =
    {
        ("idle", 6,  10f, true),
        ("walk", 9,  12f, true),
        ("jump", 8,  10f, false),
        // ("attack", 6, 12f, false),   ← future example
    };

    // ---------------------------------------------------------------
    //  Animator parameters
    // ---------------------------------------------------------------
    private const string ParamSpeed      = "Speed";
    private const string ParamIsGrounded = "IsGrounded";

    // ---------------------------------------------------------------
    [MenuItem("Tools/Project/Setup Duck Animations")]
    public static void Run()
    {
        // --- Step 1: Slice sprite sheets ---------------------------
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var (name, frameCount, _, _) in AnimStates)
                SliceSheet($"{SpritePath}/{SpritePrefix}-{name}-sheet.png", name, frameCount);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        // --- Step 2: Create AnimationClips -------------------------
        var clips = new AnimationClip[AnimStates.Length];

        for (int i = 0; i < AnimStates.Length; i++)
        {
            var (name, frameCount, fps, loop) = AnimStates[i];
            clips[i] = CreateClip(name, frameCount, fps, loop);
        }

        AssetDatabase.Refresh();

        // --- Step 3: Build controller ------------------------------
        BuildStateMachine(clips);

        Debug.Log("[DuckAnim] Setup complete. Open Animator window on Duck.controller to review.");
    }

    // ---------------------------------------------------------------
    //  Sprite slicing  (single row, left-to-right)
    // ---------------------------------------------------------------
    private static void SliceSheet(string assetPath, string animName, int frameCount)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[DuckAnim] Sprite not found: {assetPath}");
            return;
        }

        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode       = FilterMode.Point;
        importer.mipmapEnabled    = false;

        var metadata = new SpriteMetaData[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            metadata[i] = new SpriteMetaData
            {
                name      = $"duck_{animName}_{i:D2}",
                rect      = new Rect(i * FrameSize, 0, FrameSize, FrameSize),
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
    private static AnimationClip CreateClip(string animName, int frameCount, float fps, bool loop)
    {
        string spritePath = $"{SpritePath}/{SpritePrefix}-{animName}-sheet.png";

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"[DuckAnim] No sprites at {spritePath}");
            return null;
        }

        string clipName = $"Duck_{animName}";
        string clipPath = $"{ClipOutputPath}/{clipName}.anim";

        AssetDatabase.DeleteAsset(clipPath);

        var clip      = new AnimationClip { name = clipName };
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;

        // Build keyframe array
        int keyCount = loop ? sprites.Length + 1 : sprites.Length;
        var keys     = new ObjectReferenceKeyframe[keyCount];

        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

        // For looping clips, repeat the first frame at the end for a seamless loop
        if (loop)
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[0] };

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    // ---------------------------------------------------------------
    //  State machine construction
    // ---------------------------------------------------------------
    private static void BuildStateMachine(AnimationClip[] clips)
    {
        // Create a fresh controller (overwrites any existing one)
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Parameters
        controller.AddParameter(ParamSpeed,      AnimatorControllerParameterType.Float);
        controller.AddParameter(ParamIsGrounded, AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;

        // Clips are ordered to match AnimStates: [0]=idle, [1]=walk, [2]=jump
        AnimatorState idleState = CreateClipState(sm, "Idle", clips[0], new Vector3(250, 50));
        AnimatorState walkState = CreateClipState(sm, "Walk", clips[1], new Vector3(500, 50));
        AnimatorState jumpState = CreateClipState(sm, "Jump", clips[2], new Vector3(375, 200));

        sm.defaultState = idleState;

        // Idle → Walk
        AddFloatTransition(idleState, walkState, ParamSpeed, AnimatorConditionMode.Greater, 0.1f);

        // Walk → Idle
        AddFloatTransition(walkState, idleState, ParamSpeed, AnimatorConditionMode.Less, 0.1f);

        // Idle → Jump
        AddBoolTransition(idleState, jumpState, ParamIsGrounded, false);

        // Walk → Jump
        AddBoolTransition(walkState, jumpState, ParamIsGrounded, false);

        // Jump → Walk  (landed and still moving)
        var jumpToWalk = jumpState.AddTransition(walkState);
        jumpToWalk.hasExitTime = false;
        jumpToWalk.duration    = 0f;
        jumpToWalk.AddCondition(AnimatorConditionMode.If,      0,   ParamIsGrounded);
        jumpToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, ParamSpeed);

        // Jump → Idle  (landed and stopped)
        var jumpToIdle = jumpState.AddTransition(idleState);
        jumpToIdle.hasExitTime = false;
        jumpToIdle.duration    = 0f;
        jumpToIdle.AddCondition(AnimatorConditionMode.If,   0,   ParamIsGrounded);
        jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, ParamSpeed);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------
    private static AnimatorState CreateClipState(AnimatorStateMachine sm, string name,
                                                  AnimationClip clip, Vector3 position)
    {
        var state    = sm.AddState(name, position);
        state.motion = clip;
        return state;
    }

    private static void AddFloatTransition(AnimatorState from, AnimatorState to,
                                            string param, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = 0f;
        t.AddCondition(mode, threshold, param);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to,
                                           string param, bool value)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = 0f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }
}
#endif
