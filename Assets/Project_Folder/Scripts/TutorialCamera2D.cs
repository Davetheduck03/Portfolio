using UnityEngine;

/// <summary>
/// Smooth-following orthographic camera for the side-scroller tutorial.
///
/// • Follows the target horizontally with a tight lag so the player always
///   feels centred but movement reads smoothly.
/// • Follows vertically with a dead-zone so small jumps don't jostle the view.
/// • Bounds are set in world-space level edges — the camera automatically
///   compensates for its own half-extents, so the level border is never shown.
///
/// Setup:
///   1. Attach to your tutorial scene's Main Camera.
///   2. Disable (or remove) CameraController on the same camera if present.
///   3. Set Target to the SidescrollerCharacter GameObject.
///   4. Drag the cyan box gizmo corners in the Scene view to frame your level
///      (or type values into Min Bounds / Max Bounds in the Inspector).
/// </summary>
[RequireComponent(typeof(Camera))]
public class TutorialCamera2D : MonoBehaviour
{
    // ------------------------------------------------------------------
    //  Inspector
    // ------------------------------------------------------------------

    [Header("Target")]
    [Tooltip("The player to follow. Leave empty to auto-find SidescrollerCharacter.")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [Tooltip("How quickly the camera catches up horizontally. 6–8 feels snappy but smooth.")]
    [SerializeField] private float horizontalSmooth = 7f;

    [Tooltip("How quickly the camera catches up vertically. " +
             "Lower than horizontal so jumps don't jolt the view.")]
    [SerializeField] private float verticalSmooth = 4f;

    [Tooltip("Shift the tracking point upward so the player sits slightly below " +
             "centre — gives them more look-ahead space above.")]
    [SerializeField] private float verticalOffset = 1f;

    [Tooltip("The camera ignores vertical drift smaller than this. " +
             "Prevents the view from bobbing on short hops.")]
    [SerializeField] private float verticalDeadzone = 0.8f;

    [Header("Bounds  (world-space level edges)")]
    [Tooltip("Left and bottom edge of the playable level in world space.")]
    [SerializeField] private Vector2 minBounds = new Vector2(-5f,  -2f);

    [Tooltip("Right and top edge of the playable level in world space.")]
    [SerializeField] private Vector2 maxBounds = new Vector2( 40f,  12f);

    [Header("Kill Depth")]
    [Tooltip("If the player falls below this Y position they are killed instantly. " +
             "Should sit a little below minBounds.y so the camera has already " +
             "hit its floor clamp before the kill triggers.")]
    [SerializeField] private float killDepth = -6f;

    [Tooltip("Assign the LevelManager so the kill can trigger the death screen. " +
             "Leave empty to auto-find one in the scene.")]
    [SerializeField] private LevelManager levelManager;

    // ------------------------------------------------------------------
    //  Private
    // ------------------------------------------------------------------

    private Camera _cam;
    private float  _smoothY;   // independently smoothed Y so the dead-zone works correctly

    // ------------------------------------------------------------------
    //  Unity messages
    // ------------------------------------------------------------------

    void Awake()
    {
        _cam = GetComponent<Camera>();

        // Auto-find the side-scroller character if no target was assigned.
        if (target == null)
        {
            SidescrollerCharacter sc = FindFirstObjectByType<SidescrollerCharacter>();
            if (sc != null) target = sc.transform;
        }

        // Seed the smoothed Y so there's no initial snap on the first frame.
        if (target != null)
            _smoothY = target.position.y + verticalOffset;

        if (levelManager == null)
            levelManager = FindFirstObjectByType<LevelManager>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ── Kill depth ─────────────────────────────────────────────────
        if (target.position.y < killDepth)
        {
            if (levelManager != null)
                levelManager.ShowDeathScreen();
            else
                Debug.LogWarning("[TutorialCamera2D] Kill depth reached but no LevelManager found.");
            return;
        }

        // ── Desired position ───────────────────────────────────────────
        float desiredX = target.position.x;
        float desiredY = target.position.y + verticalOffset;

        // Only chase Y when the player moves outside the dead-zone.
        if (Mathf.Abs(desiredY - _smoothY) > verticalDeadzone)
            _smoothY = Mathf.Lerp(_smoothY, desiredY, verticalSmooth * Time.deltaTime);

        float x = Mathf.Lerp(transform.position.x, desiredX,  horizontalSmooth * Time.deltaTime);
        float y = _smoothY;

        // ── Clamp to bounds ────────────────────────────────────────────
        // Half-extents of the visible area.  The camera centre must stay
        // at least this far inside the level edges so nothing bleeds through.
        float halfH = _cam.orthographic
            ? _cam.orthographicSize
            : Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(transform.position.z);

        float halfW = halfH * _cam.aspect;

        float clampMinX = minBounds.x + halfW;
        float clampMaxX = maxBounds.x - halfW;
        float clampMinY = minBounds.y + halfH;
        float clampMaxY = maxBounds.y - halfH;

        // Guard against a level that's narrower than the camera view.
        if (clampMinX > clampMaxX) x = (minBounds.x + maxBounds.x) * 0.5f;
        else                       x = Mathf.Clamp(x, clampMinX, clampMaxX);

        if (clampMinY > clampMaxY) y = (minBounds.y + maxBounds.y) * 0.5f;
        else                       y = Mathf.Clamp(y, clampMinY, clampMaxY);

        transform.position = new Vector3(x, y, transform.position.z);
    }

    // ------------------------------------------------------------------
    //  Editor gizmo
    // ------------------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        // Filled cyan box shows the allowed level area.
        Gizmos.color = new Color(0f, 1f, 1f, 0.08f);
        Vector3 centre = new Vector3(
            (minBounds.x + maxBounds.x) * 0.5f,
            (minBounds.y + maxBounds.y) * 0.5f,
            0f);
        Vector3 size = new Vector3(
            maxBounds.x - minBounds.x,
            maxBounds.y - minBounds.y,
            0f);
        Gizmos.DrawCube(centre, size);

        // Solid outline.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(centre, size);

        // Corner labels via handles so they're easy to read in the scene view.
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(
            new Vector3(minBounds.x, minBounds.y, 0f), " min");
        UnityEditor.Handles.Label(
            new Vector3(maxBounds.x, maxBounds.y, 0f), " max");

        // Kill depth — red horizontal line spanning the full level width.
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(minBounds.x, killDepth, 0f),
            new Vector3(maxBounds.x, killDepth, 0f));
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.Label(
            new Vector3(minBounds.x, killDepth, 0f), " kill depth");
#endif
    }
}
