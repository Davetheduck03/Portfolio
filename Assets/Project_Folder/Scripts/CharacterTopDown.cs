using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class TopDownCharacter : BaseCharacter
{
	[Header("Movement")]
	[SerializeField] private float moveSpeed = 5f;

	[Header("Ground Check")]
	[SerializeField] private LayerMask groundLayer;
	[SerializeField] private float sphereCastRadius = 0.45f; // slightly under collider radius
	[SerializeField] private float sphereCastDistance = 0.2f;

	private Rigidbody _rb;
	private Vector3 _inputDir;
	private Vector3 _groundNormal = Vector3.up;

	void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_rb.useGravity = false;
		_rb.freezeRotation = true;
	}

	public override void HandleInput()
	{
		float x = Input.GetAxisRaw("Horizontal");
		float z = Input.GetAxisRaw("Vertical");
		_inputDir = new Vector3(x, 0f, z).normalized;
	}

	public override void Tick()
	{
		UpdateGroundNormal();

		Vector3 move = new Vector3(_inputDir.x * moveSpeed, 0f, _inputDir.z * moveSpeed);

		// Project movement along the surface so we follow slopes instead of fighting them
		if (_groundNormal != Vector3.up)
			move = Vector3.ProjectOnPlane(move, _groundNormal);

		// Preserve a small downward force to stay grounded on uneven terrain
		move.y = Mathf.Min(move.y, -0.5f);

		_rb.linearVelocity = move;
	}

	private void UpdateGroundNormal()
	{
		if (Physics.SphereCast(
				transform.position,
				sphereCastRadius,
				Vector3.down,
				out RaycastHit hit,
				sphereCastDistance,
				groundLayer))
		{
			_groundNormal = hit.normal;
		}
		else
		{
			_groundNormal = Vector3.up;
		}
	}

	public override void OnDeactivated()
	{
		base.OnDeactivated();
		_inputDir = Vector3.zero;
		_rb.linearVelocity = Vector3.zero;
	}
}