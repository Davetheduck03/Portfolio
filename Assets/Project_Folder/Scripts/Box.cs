using UnityEngine;

/// <summary>
/// An interactive box that:
///   - Acts as a solid platform the 2D character can jump onto.
///   - Can be picked up, carried, and placed by the top-down character (press E).
///   - Snaps to the GridSystem grid when placed — no gravity.
///
/// Setup in the Inspector / Editor:
///   1. Attach to a GameObject that has a Collider (no Rigidbody required).
///   2. Set the GameObject's Layer to match one of the layers in
///      SidescrollerCharacter's Ground Layers mask so the 2D character can land on it.
///   3. Assign the Mesh Root field to the child GameObject that holds the MeshRenderer.
///      If left empty the script auto-detects the first MeshRenderer child.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Box : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Child transform that owns the MeshRenderer. Auto-detected if not assigned.")]
    [SerializeField] private Transform meshRoot;

    [Tooltip("Local position of the mesh child while the box is being carried.")]
    [SerializeField] private Vector3 carryMeshOffset = new Vector3(0f, -1f, -3f);

    public bool IsHeld    { get; private set; }
    public bool IsSlotted { get; private set; }

    private Collider  _col;
    private Transform _carrier;
    private Vector3   _holdOffset;
    private Vector3   _meshRestLocalPos;   // original local position of the mesh child

    // Placement overlap protection
    private Collider _ignoredCollider;
    private float    _ignoreMinDist;

    void Awake()
    {
        _col = GetComponent<Collider>();

        // Auto-detect mesh child
        if (meshRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null) meshRoot = mr.transform;
        }

        if (meshRoot != null)
            _meshRestLocalPos = meshRoot.localPosition;
    }

    void LateUpdate()
    {
        if (IsHeld && _carrier != null)
        {
            transform.position = _carrier.position + _holdOffset;
            return;
        }

        // Once the carrier has walked far enough away, restore collision
        if (_ignoredCollider != null)
        {
            float dist = Vector3.Distance(transform.position, _ignoredCollider.transform.position);
            if (dist > _ignoreMinDist)
            {
                Physics.IgnoreCollision(_col, _ignoredCollider, false);
                _ignoredCollider = null;
            }
        }
    }

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Permanently locks the box into a hole. Once slotted it can never be
    /// picked up or pushed again.
    /// </summary>
    public void Slot()
    {
        IsSlotted = true;
        IsHeld    = false;
        _carrier  = null;

        if (_col != null) _col.enabled = false;
        enabled = false;
    }

    public void PickUp(Transform carrier, Vector3 initialOffset)
    {
        if (IsSlotted) return;

        // Clear any lingering ignore from a previous placement
        if (_ignoredCollider != null)
        {
            Physics.IgnoreCollision(_col, _ignoredCollider, false);
            _ignoredCollider = null;
        }

        IsHeld    = true;
        _carrier  = carrier;
        _holdOffset = initialOffset;

        if (_col != null) _col.enabled = false;

        // Offset the mesh child so it sits at the carry position visually.
        if (meshRoot != null)
            meshRoot.localPosition = carryMeshOffset;
    }

    public void SetHoldOffset(Vector3 offset) => _holdOffset = offset;

    /// <summary>
    /// Detach the box and place it at <paramref name="worldPosition"/>.
    /// <paramref name="carrierCollider"/> is ignored until the carrier walks
    /// away, preventing the physics solver from pushing the box off-grid.
    /// </summary>
    public void PutDown(Vector3 worldPosition, Collider carrierCollider = null)
    {
        IsHeld   = false;
        _carrier = null;

        transform.position = new Vector3(worldPosition.x, worldPosition.y, -1f);

        // Restore the mesh child to its original resting position.
        if (meshRoot != null)
            meshRoot.localPosition = _meshRestLocalPos;

        if (_col != null)
        {
            if (carrierCollider != null)
            {
                Physics.IgnoreCollision(_col, carrierCollider, true);
                _ignoredCollider = carrierCollider;

                float boxRadius    = _col.bounds.extents.magnitude;
                float carrierRadius = carrierCollider.bounds.extents.magnitude;
                _ignoreMinDist = boxRadius + carrierRadius + 0.1f;
            }

            _col.enabled = true;
            Physics.SyncTransforms();
        }
    }

    public void PutDown() => PutDown(transform.position, null);
}
