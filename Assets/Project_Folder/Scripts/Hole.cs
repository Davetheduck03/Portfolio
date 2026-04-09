using UnityEngine;

/// <summary>
/// A pit tile in the top-down view that blocks the player until a Box fills it.
///
/// ── How it works ──────────────────────────────────────────────────────────────
///   • Every FixedUpdate, an OverlapBox check scans the hole's cell for any
///     resting Box. No Rigidbody or trigger collider is needed on the Hole —
///     the scan works regardless of the Box's physics setup.
///   • A separate solid BLOCKER collider (child GameObject) physically stops
///     the top-down character from crossing while the hole is empty.
///   • When a Box is detected, it is snapped to the hole centre, dimmed, and
///     permanently slotted. The blocker is then disabled so the player can cross.
///
/// ── Prefab setup ──────────────────────────────────────────────────────────────
///   1. Create a GameObject, add this script. No collider needed on the root.
///   2. Add a CHILD GameObject called "Blocker":
///        • Add a BoxCollider (non-trigger), sized to the grid cell (e.g. 1×1×0.5).
///        • Assign it to the Blocker Collider field below.
///   3. Optionally add a child GameObject for EmptyVisual (the pit graphic).
///   4. Place the Hole at a grid-cell centre in the scene.
///
/// ── Layer advice ──────────────────────────────────────────────────────────────
///   Put the Blocker on the same layer as your solid walls so the top-down
///   Rigidbody collides with it. Set Sidescroller Layer so the blocker ignores
///   the 2D character completely.
/// </summary>
public class Hole : MonoBehaviour
{
    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Header("References")]
    [Tooltip("Solid (non-trigger) collider that physically stops the top-down " +
             "player while the hole is empty. Usually a BoxCollider child.")]
    [SerializeField] private Collider blockerCollider;

    [Tooltip("Layer the 2D side-scroller character lives on. " +
             "The blocker ignores this layer so the 2D character always passes through freely.")]
    [SerializeField] private int sidescrollerLayer = 0;

    [Header("Detection")]
    [Tooltip("How far the overlap box extends on each axis. Should match half your grid cell size.")]
    [SerializeField] private Vector3 detectHalfExtents = new Vector3(0.45f, 0.45f, 2f);

    [Header("Visuals")]
    [Tooltip("Shown when the hole is empty (the pit graphic).")]
    [SerializeField] private GameObject emptyVisual;

    [Tooltip("Opacity of the box mesh when it fills the hole (0 = invisible, 1 = fully opaque).")]
    [SerializeField, Range(0f, 1f)] private float filledBoxAlpha = 0.2f;

    // ---------------------------------------------------------------
    //  Public state
    // ---------------------------------------------------------------

    /// <summary>True once a box has been slotted into this hole.</summary>
    public bool IsFilled { get; private set; }

    // ---------------------------------------------------------------
    //  Unity messages
    // ---------------------------------------------------------------

    void Awake()
    {
        // Make the blocker invisible to the 2D character's layer so it can
        // always pass through holes freely, regardless of fill state.
        if (blockerCollider != null)
            Physics.IgnoreLayerCollision(blockerCollider.gameObject.layer, sidescrollerLayer, true);

        ApplyFillState();
    }

    void FixedUpdate()
    {
        if (IsFilled) return;

        // Scan the hole cell for any resting Box — no Rigidbody/trigger needed.
        Collider[] hits = Physics.OverlapBox(
            transform.position,
            detectHalfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            Box box = hit.GetComponent<Box>();
            if (box == null || box.IsHeld || box.IsSlotted) continue;

            AcceptBox(box);
            break;
        }
    }

    // ---------------------------------------------------------------
    //  Core logic
    // ---------------------------------------------------------------

    /// <summary>
    /// Merges <paramref name="box"/> into this hole: snaps it to the cell
    /// centre at Z = 1, dims its renderer, and permanently locks it.
    /// </summary>
    private void AcceptBox(Box box)
    {
        box.transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            -0.5f);

        // ── 2. Dim the 3D mesh so it reads as flush with the floor. ──
        var mr = box.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            foreach (Material mat in mr.materials)
            {
                Color c = mat.color;
                c.a = filledBoxAlpha;
                mat.color = c;
            }
        }

        // ── 3. Permanently slot the box — it can never be picked up again. ──
        box.Slot();

        // ── 4. Mark hole as filled and update the blocker + visuals. ──
        SetFilled(true);
    }

    /// <summary>
    /// Fills or unfills the hole programmatically (useful for puzzle resets).
    /// </summary>
    public void SetFilled(bool filled)
    {
        IsFilled = filled;
        ApplyFillState();
    }

    private void ApplyFillState()
    {
        // Blocker active only while the hole is empty.
        if (blockerCollider != null)
            blockerCollider.enabled = !IsFilled;

        if (emptyVisual != null) emptyVisual.SetActive(!IsFilled);
    }

    // ---------------------------------------------------------------
    //  Editor helpers
    // ---------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Red = empty (blocking), green = filled (passable).
        Gizmos.color = IsFilled
            ? new Color(0.2f, 0.9f, 0.2f, 0.45f)
            : new Color(0.9f, 0.15f, 0.15f, 0.45f);

        Gizmos.DrawCube(transform.position, detectHalfExtents * 2f);
        Gizmos.DrawWireCube(transform.position, detectHalfExtents * 2f);
    }
#endif
}
