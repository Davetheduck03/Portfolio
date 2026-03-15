using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SidescrollerCharacter : BaseCharacter
{
	[Header("Movement")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float jumpForce = 12f;

	[Header("Manual Gravity")]
	[SerializeField] private float fallMultiplier = 2f;
	[SerializeField] private float lowJumpMultiplier = 1.5f;

	[Header("Ground Check")]
	[SerializeField] private Transform groundCheck;
	[Tooltip("All layers the character can stand on. Select multiple in the Inspector.")]
	[SerializeField] private LayerMask groundLayers;
	[SerializeField] private float groundCheckRadius = 0.1f;  // small, tight to contact point
	[SerializeField] private float sphereCastRadius = 0.45f; // slightly under collider radius
	[SerializeField] private float sphereCastDistance = 0.2f;

	[Header("Animation")]
	[SerializeField] private Animator animator;
	[SerializeField] private SpriteRenderer spriteRenderer;

	private static readonly int Speed      = Animator.StringToHash("Speed");
	private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

	private Rigidbody _rb;
	private SphereCollider _col;

	private float _inputX;
	private bool _jumpRequested;
	private bool _jumpHeld;
	private bool _isGrounded;
	private float _velocityY;
	private Vector3 _groundNormal = Vector3.up;

	void Awake()
	{
		base.Awake();

		_rb  = GetComponent<Rigidbody>();
		_col = GetComponent<SphereCollider>();

		_rb.useGravity     = false;
		_rb.freezeRotation = true;

		// Lock Z so the character stays on the 2D plane
		_rb.constraints = RigidbodyConstraints.FreezePositionZ
		                | RigidbodyConstraints.FreezeRotationX
		                | RigidbodyConstraints.FreezeRotationY
		                | RigidbodyConstraints.FreezeRotationZ;
	}

	public override void HandleInput()
	{
		_inputX = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);

		if (Keyboard.current.spaceKey.wasPressedThisFrame)
			_jumpRequested = true;

		_jumpHeld = Keyboard.current.spaceKey.isPressed;
	}

	public override void Tick()
	{
		UpdateGroundCheck();
		UpdateGroundNormal();
		ApplyGravity();
		ApplyJump();

		Vector3 move = new Vector3(_inputX * moveSpeed, _velocityY, 0f);

		// Project onto slope surface so movement follows inclines
		if (_isGrounded && _groundNormal != Vector3.up)
			move = Vector3.ProjectOnPlane(move, _groundNormal);

		_rb.linearVelocity = move;

		UpdateAnimator();
	}

	private void UpdateAnimator()
	{
		if (animator == null) return;

		animator.SetFloat(Speed,      Mathf.Abs(_inputX));
		animator.SetBool (IsGrounded, _isGrounded);

		// Flip sprite to face the direction of movement
		if (spriteRenderer != null && _inputX != 0f)
			spriteRenderer.flipX = _inputX < 0f;
	}

	// -------------------------------------------------------------------
	// Ground detection
	// -------------------------------------------------------------------

	private void UpdateGroundCheck()
	{
		// groundCheck is an empty child GameObject placed at the bottom of the sphere.
		// Position it at: local Y = -(sphereCollider.radius)
		_isGrounded = Physics.CheckSphere(
			groundCheck.position,
			groundCheckRadius,
			groundLayers,
			QueryTriggerInteraction.Ignore);
	}

	private void UpdateGroundNormal()
	{
		if (Physics.SphereCast(
				transform.position,
				sphereCastRadius,
				Vector3.down,
				out RaycastHit hit,
				sphereCastDistance,
				groundLayers))
		{
			// Only treat as valid ground if the surface faces mostly upward
			_groundNormal = hit.normal.y > 0.7f ? hit.normal : Vector3.up;
		}
		else
		{
			_groundNormal = Vector3.up;
		}
	}

	// -------------------------------------------------------------------
	// Physics
	// -------------------------------------------------------------------

	private void ApplyGravity()
	{
		if (_isGrounded && _velocityY < 0f)
		{
			// Small constant downward force — keeps CheckSphere reliably true
			// on uneven geometry without zeroing velocity entirely
			_velocityY = -0.5f;
			return;
		}

		float multiplier = 1f;

		if (_velocityY < 0f)
			multiplier = fallMultiplier;           // falling: pull down harder
		else if (_velocityY > 0f && !_jumpHeld)
			multiplier = lowJumpMultiplier;        // released early: short hop

		_velocityY -= GravityService.Gravity * multiplier * Time.fixedDeltaTime;
	}

	private void ApplyJump()
	{
		if (_jumpRequested && _isGrounded)
			_velocityY = jumpForce;

		_jumpRequested = false;
	}

	// -------------------------------------------------------------------
	// Switch
	// -------------------------------------------------------------------

	public override void OnActivated()
	{
		base.OnActivated(); // re-enables Rigidbody, Collider, Animator; restores opacity

		// Restore the sidescroller plane constraint (X/Y free, Z locked, no rotation).
		_rb.constraints = RigidbodyConstraints.FreezePositionZ
		                | RigidbodyConstraints.FreezeRotationX
		                | RigidbodyConstraints.FreezeRotationY
		                | RigidbodyConstraints.FreezeRotationZ;
	}

	public override void OnDeactivated()
	{
		// Clear input and vertical momentum before base zeroes linearVelocity.
		_inputX        = 0f;
		_jumpRequested = false;
		_velocityY     = 0f;

		base.OnDeactivated(); // zeros velocity, isKinematic=true, disables Collider+Animator, dims sprite
	}

	// -------------------------------------------------------------------
	// Debug
	// -------------------------------------------------------------------

	void OnDrawGizmosSelected()
	{
		if (groundCheck == null) return;

		Gizmos.color = _isGrounded ? Color.green : Color.red;
		Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

		Gizmos.color = Color.yellow;
		Gizmos.DrawRay(transform.position, _groundNormal * 0.5f);
	}
}