using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton that plays a circular iris-wipe transition.
///
/// ── Quick usage ────────────────────────────────────────────────────
///
///   // Replace every SceneManager.LoadScene() call with:
///   SceneTransition.Instance.LoadScene(buildIndex);
///
///   // To reload (death / respawn):
///   SceneTransition.Instance.ReloadScene();
///
///   // Just wipe out (close the iris), then run your own code:
///   SceneTransition.Instance.WipeOut(() => { /* do stuff */ });
///
///   // Just wipe in (open the iris):
///   SceneTransition.Instance.WipeIn();
///
/// ── Setup ──────────────────────────────────────────────────────────
///   Run  Tools → UI → Create Scene Transition  in any scene.
///   The object is DontDestroyOnLoad — put it once in your first
///   scene (e.g. MainMenu) and it survives every scene load.
///
/// ── Requirements ───────────────────────────────────────────────────
///   Assets/Project_Folder/Shaders/CircularWipe.shader must exist.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class SceneTransition : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Singleton
    // ----------------------------------------------------------------

    public static SceneTransition Instance { get; private set; }

    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Timing")]
    [Tooltip("Total seconds for one full open or close animation.")]
    [SerializeField] private float wipeDuration = 0.45f;

    [Header("Curves  (x = time 0-1 / y = progress 0-1)")]
    [SerializeField] private AnimationCurve wipeInCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve wipeOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Appearance")]
    [Tooltip("Colour of the wipe overlay (normally black).")]
    [SerializeField] private Color wipeColor    = Color.black;
    [Tooltip("Softness of the iris edge in screen-space units (0 = hard).")]
    [SerializeField, Range(0.001f, 0.15f)]
    private float edgeSoftness = 0.03f;

    [Header("Audio")]
    [Tooltip("Sound that plays the moment the circle starts closing. " +
             "Plays with unscaled time so it works even when TimeScale is 0.")]
    [SerializeField] private AudioClip transitionSound;
    [Range(0f, 1f)]
    [SerializeField] private float transitionVolume = 1f;

    // ----------------------------------------------------------------
    //  Private state
    // ----------------------------------------------------------------

    private Material    _wipeMat;
    private Image       _wipeImage;
    private Coroutine   _activeRoutine;
    private AudioSource _audioSource;

    private static readonly int ProgressID  = Shader.PropertyToID("_Progress");
    private static readonly int SoftnessID  = Shader.PropertyToID("_EdgeSoftness");

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();

        // AudioSource for the wipe sound — auto-add if one isn't on the GameObject
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        // Use unscaled time so the sound fires correctly even during death pause
        _audioSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        // Open the iris when the very first scene appears.
        SetProgress(0f);
        _activeRoutine = StartCoroutine(AnimRoutine(0f, 1f, wipeInCurve, null));
    }

    // ----------------------------------------------------------------
    //  Public API
    // ----------------------------------------------------------------

    /// <summary>
    /// Close the iris → load the requested scene → open the iris.
    /// Replaces every SceneManager.LoadScene() in your project.
    /// </summary>
    public void LoadScene(int buildIndex)
    {
        StopActive();
        _activeRoutine = StartCoroutine(LoadRoutine(buildIndex));
    }

    /// <summary>Close iris → reload the current scene → open iris. Use for death/respawn.</summary>
    public void ReloadScene()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Open the iris (Progress 0→1).
    /// Useful after a manual scene-management call where you already handled the load.
    /// </summary>
    public void WipeIn(Action onComplete = null)
    {
        StopActive();
        _activeRoutine = StartCoroutine(AnimRoutine(0f, 1f, wipeInCurve, onComplete));
    }

    /// <summary>
    /// Close the iris (Progress 1→0).
    /// Useful for showing a death screen without immediately loading a scene.
    /// </summary>
    public void WipeOut(Action onComplete = null)
    {
        StopActive();
        PlayTransitionSound();
        _activeRoutine = StartCoroutine(AnimRoutine(1f, 0f, wipeOutCurve, onComplete));
    }

    // ----------------------------------------------------------------
    //  Coroutines
    // ----------------------------------------------------------------

    private IEnumerator LoadRoutine(int buildIndex)
    {
        // 1. Close  (play transition sound exactly when the iris starts shutting)
        PlayTransitionSound();
        yield return AnimRoutine(1f, 0f, wipeOutCurve, null);

        // 2. Load
        AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex);
        yield return op;

        // 3. Extra frame so new scene objects finish Awake/Start before we reveal them
        yield return null;

        // 4. Open
        yield return AnimRoutine(0f, 1f, wipeInCurve, null);
    }

    private IEnumerator AnimRoutine(float from, float to,
                                    AnimationCurve curve, Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < wipeDuration)
        {
            elapsed += Time.unscaledDeltaTime;   // unscaled so it works during pause
            float t = Mathf.Clamp01(elapsed / wipeDuration);
            SetProgress(Mathf.LerpUnclamped(from, to, curve.Evaluate(t)));
            yield return null;
        }

        SetProgress(to);
        onComplete?.Invoke();
    }

    // ----------------------------------------------------------------
    //  Overlay construction
    // ----------------------------------------------------------------

    private void BuildOverlay()
    {
        // ── Canvas on this GameObject ─────────────────────────────
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;   // always on top

        // ── Full-screen child image ───────────────────────────────
        GameObject imgGO = new GameObject("WipeOverlay");
        imgGO.transform.SetParent(transform, false);

        _wipeImage              = imgGO.AddComponent<Image>();
        _wipeImage.raycastTarget = false;   // never block clicks

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ── Material ─────────────────────────────────────────────
        Shader shader = Shader.Find("Custom/CircularWipe");

        if (shader == null)
        {
            Debug.LogError(
                "[SceneTransition] Shader 'Custom/CircularWipe' not found.\n" +
                "Make sure  Assets/Project_Folder/Shaders/CircularWipe.shader  exists " +
                "and has compiled without errors.");
            return;
        }

        _wipeMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _wipeMat.SetFloat(SoftnessID, edgeSoftness);

        _wipeImage.material = _wipeMat;
        _wipeImage.color    = wipeColor;    // drives the shader's _Color via vertex colour

        SetProgress(0f);  // start fully closed — Start() will open it
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    private void SetProgress(float value)
    {
        if (_wipeMat != null)
            _wipeMat.SetFloat(ProgressID, value);
    }

    private void PlayTransitionSound()
    {
        if (_audioSource != null && transitionSound != null)
            _audioSource.PlayOneShot(transitionSound, transitionVolume);
    }

    private void StopActive()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
    }
}
