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
/// </summary>
[RequireComponent(typeof(Collider))]
public class Box : MonoBehaviour
{
    public bool IsHeld { get; private set; }

    private Collider _col;
    private Transform _carrier;
    private Vector3 _holdOffset;

    // Placement overlap protection
    private Collider _ignoredCollider;
    private float _ignoreMinDist;   // distance at which we re-enable collision

    void Awake()
    {
        _col = GetComponent<Collider>();
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

    public void PickUp(Transform carrier, Vector3 initialOffset)
    {
        // Clear any lingering ignore from a previous placement
        if (_ignoredCollider != null)
        {
            Physics.IgnoreCollision(_col, _ignoredCollider, false);
            _ignoredCollider = null;
        }

        IsHeld = true;
        _carrier = carrier;
        _holdOffset = initialOffset;

        if (_col != null) _col.enabled = false;
    }

    public void SetHoldOffset(Vector3 offset) => _holdOffset = offset;

    /// <summary>
    /// Detach the box and place it at <paramref name="worldPosition"/>.
    /// <paramref name="carrierCollider"/> is ignored until the carrier walks
    /// away, preventing the physics solver from pushing the box off-grid.
    /// </summary>
    public void PutDown(Vector3 worldPosition, Collider carrierCollider = null)
    {
        IsHeld = false;
        _carrier = null;

        transform.position = worldPosition;

        if (_col != null)
        {
            if (carrierCollider != null)
            {
                Physics.IgnoreCollision(_col, carrierCollider, true);
                _ignoredCollider = carrierCollider;

                // Sum of both collider radii + a small margin — once the carrier
                // is this far from the box centre, it's safe to collide again.
                float boxRadius = _col.bounds.extents.magnitude;
                float carrierRadius = carrierCollider.bounds.extents.magnitude;
                _ignoreMinDist = boxRadius + carrierRadius + 0.1f;
            }

            _col.enabled = true;
            Physics.SyncTransforms();
        }
    }

    public void PutDown() => PutDown(transform.position, null);
}