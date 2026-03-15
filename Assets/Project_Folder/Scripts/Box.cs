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
	// ---------------------------------------------------------------
	//  State
	// ---------------------------------------------------------------

	/// <summary>True while the top-down character is carrying this box.</summary>
	public bool IsHeld { get; private set; }

	// ---------------------------------------------------------------
	//  Private fields
	// ---------------------------------------------------------------

	private Collider  _col;
	private Transform _carrier;
	private Vector3   _holdOffset;   // world-space offset from carrier; updated every frame

	// ---------------------------------------------------------------
	//  Unity messages
	// ---------------------------------------------------------------

	void Awake()
	{
		_col = GetComponent<Collider>();
	}

	void LateUpdate()
	{
		if (!IsHeld || _carrier == null) return;

		// Trail the carrier at the current hold offset (updated every frame by the carrier)
		transform.position = _carrier.position + _holdOffset;
	}

	// ---------------------------------------------------------------
	//  Public API — called by TopDownCharacter
	// ---------------------------------------------------------------

	/// <summary>
	/// Attach the box to a carrier. Disables the collider so it doesn't
	/// block the carrier's physics movement while being held.
	/// </summary>
	public void PickUp(Transform carrier, Vector3 initialOffset)
	{
		IsHeld      = true;
		_carrier    = carrier;
		_holdOffset = initialOffset;

		// Prevent the box collider from pushing the carrier around mid-carry
		if (_col != null) _col.enabled = false;
	}

	/// <summary>
	/// Update the offset each frame so the box stays in front of the
	/// carrier even as they turn.
	/// </summary>
	public void SetHoldOffset(Vector3 offset) => _holdOffset = offset;

	/// <summary>
	/// Detach the box and place it at <paramref name="worldPosition"/> (grid-snapped by the caller).
	/// Re-enables the collider so the 2D character can land on it again.
	/// </summary>
	public void PutDown(Vector3 worldPosition)
	{
		IsHeld   = false;
		_carrier = null;

		transform.position = worldPosition;

		if (_col != null)
		{
			_col.enabled = true;
			Physics.SyncTransforms(); // register the static collider at its new world position
		}
	}

	/// <summary>
	/// Detach the box at its current world position (no snap).
	/// </summary>
	public void PutDown() => PutDown(transform.position);
}
