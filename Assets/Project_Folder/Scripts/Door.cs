using UnityEngine;

/// <summary>
/// A door that slides open when the matching Key is collected by either character.
///
/// SETUP:
///   1. Attach to the door GameObject (which should have a Collider for blocking).
///   2. Set requiredKeyID to match the Key's keyID.
///   3. Set openOffset to define where the door slides to when opened
///      (e.g. Vector3(0, 2, 0) slides it upward out of the way).
///   4. Tune openDuration and openCurve in the Inspector for the feel you want.
///
/// The door's Collider is disabled once it finishes opening so characters
/// can pass through without needing to destroy the GameObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Door : MonoBehaviour
{
    // ------------------------------------------------------------------
    //  Inspector
    // ------------------------------------------------------------------

    [Tooltip("Must match the keyID on the Key that unlocks this door.")]
    [SerializeField] private string requiredKeyID = "Key_1";

    [Header("Open Animation")]
    [Tooltip("World-space offset the door slides to when opened. " +
             "(0, 2, 0) slides it upward; (0, -2, 0) sinks it into the floor.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 2f, 0f);

    [Tooltip("Seconds the slide animation takes.")]
    [SerializeField] private float openDuration = 0.8f;

    [Tooltip("Maps normalised time [0,1] to blend factor [0,1]. " +
             "Shape in the Inspector — a slight ease-out gives a satisfying thud.")]
    [SerializeField] private AnimationCurve openCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioClip openSFX;

    // ------------------------------------------------------------------
    //  Private state
    // ------------------------------------------------------------------

    private bool    _opening;
    private float   _openT;         // 0 → 1 during the open animation
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private Collider _col;

    // ------------------------------------------------------------------

    void Awake()
    {
        _col            = GetComponent<Collider>();
        _closedPosition = transform.position;
        _openPosition   = _closedPosition + openOffset;
    }

    void OnEnable()
    {
        Key.OnKeyCollected += HandleKeyCollected;
    }

    void OnDisable()
    {
        Key.OnKeyCollected -= HandleKeyCollected;
    }

    // ------------------------------------------------------------------
    //  Key event
    // ------------------------------------------------------------------

    private void HandleKeyCollected(string keyID)
    {
        if (_opening) return;
        if (keyID != requiredKeyID) return;

        _opening = true;

        if (openSFX != null)
            AudioSource.PlayClipAtPoint(openSFX, transform.position);
    }

    // ------------------------------------------------------------------
    //  Animation
    // ------------------------------------------------------------------

    void Update()
    {
        if (!_opening || _openT >= 1f) return;

        _openT = Mathf.MoveTowards(_openT, 1f, Time.deltaTime / openDuration);
        float t = openCurve.Evaluate(_openT);
        transform.position = Vector3.Lerp(_closedPosition, _openPosition, t);

        // Disable the collider mid-way so the character isn't blocked even if
        // they reach the door before the animation fully completes.
        if (_openT >= 0.5f && _col.enabled)
            _col.enabled = false;
    }

    // ------------------------------------------------------------------
    //  Editor gizmo — shows the open destination in the Scene view
    // ------------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(transform.position + openOffset, transform.localScale);
        Gizmos.DrawLine(transform.position, transform.position + openOffset);
    }
#endif
}
