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
	[SerializeField] private float lowJumpMultiplier = 2.5f;

	[Header("Jump Hold")]
	[Tooltip("Extra upward force applied per second while the jump key is held.")]
	[SerializeField] private float jumpHoldForce = 35f;
	[Tooltip("How long (seconds) the hold boost can extend the jump.")]
	[SerializeField] private float jumpHoldMaxTime = 0.2f;

	[Header("Ground Check")]
	[Tooltip("All layers the character can stand on. Select multiple in the Inspector.")]
	[SerializeField] private LayerMask groundLayers;

	[Tooltip("Number of rays cast across the bottom of the collider. " +
	         "3 catches edges cleanly without being expensive.")]
	[SerializeField] private int groundRayCount = 3;

	[Tooltip("How far below the collider bottom each ray travels. " +
	         "Keep small — just enough to survive one physics step.")]
	[SerializeField] private float groundRayLength = 0.12f;

	[Tooltip("Pulls the outermost rays inward from the collider edge so they " +
	         "don't false-positive on a wall beside the character.")]
	[SerializeField] private float groundRayInset = 0.05f;

	[Tooltip("Lifts the ray origin up from the very bottom of the collider. " +
	         "Increase if rays start below the surface on uneven ground.")]
	[SerializeField] private float groundRayOriginOffset = 0.1f;

	[SerializeField] private float sphereCastRadius   = 0.45f;
	[SerializeField] private float sphereCastDistance = 0.2f;

	[Header("Animation")]
	[SerializeField] private Animator animator;
	[SerializeField] private SpriteRenderer spriteRenderer;

	[Header("Audio")]
	[Tooltip("One-shot sound played the moment the character leaves the ground on a jump.")]
	[SerializeField] private AudioClip jumpSound;

	private static readonly int Speed      = Animator.StringToHash("Speed");
	private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

	private Rigidbody _rb;
	private SphereCollider _col;

	private float _inputX;
	private bool  _jumpRequested;
	private bool  _jumpHeld;
	private bool  _isGrounded;
	private float _velocityY;
	private float _jumpHoldTimer;         // how long the hold boost has been active
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

		// Walk sound — only while grounded and actually moving
		if (_isGrounded && Mathf.Abs(_inputX) > 0.01f)
			StartWalkSound();
		else
			StopWalkSound();
	}

	// -------------------------------------------------------------------
	// Ground detection
	// -------------------------------------------------------------------

	private void UpdateGroundCheck()
	{
		// Cast N rays spread evenly across the collider's bottom edge.
		// Any single hit is enough to be considered grounded, so the character
		// stays stable even when only a small corner of the collider overlaps a tile.

		// Centre of the bottom of the sphere, raised by the origin offset.
		Vector3 bottom = transform.position + Vector3.down * (_col.radius - groundRayOriginOffset);

		// Half-width of the ray spread, inset slightly so side walls don't register.
		float halfSpread = Mathf.Max(0f, _col.radius - groundRayInset);

		_isGrounded = false;

		for (int i = 0; i < groundRayCount; i++)
		{
			// t goes 0 → 1 across the N rays; with a single ray it sits at centre.
			float t      = groundRayCount > 1 ? (float)i / (groundRayCount - 1) : 0.5f;
			float xOffset = Mathf.Lerp(-halfSpread, halfSpread, t);
			Vector3 origin = bottom + new Vector3(xOffset, 0f, 0f);

			if (Physics.Raycast(origin, Vector3.down, groundRayLength,
			                    groundLayers, QueryTriggerInteraction.Ignore))
			{
				_isGrounded = true;
				break;            // one hit is enough — no need to test the rest
			}
		}
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
		{
			_velocityY     = jumpForce;
			_jumpHoldTimer = 0f;          // reset hold window on each new jump
			PlayOneShot(jumpSound);       // ← play jump sound the moment we leave the ground
		}

		_jumpRequested = false;

		// While holding jump and still rising, apply a continuous upward boost
		// for up to jumpHoldMaxTime seconds. Releasing early cuts the boost off,
		// and the increased lowJumpMultiplier brings the character down faster.
		if (_jumpHeld && !_isGrounded && _velocityY > 0f
		    && _jumpHoldTimer < jumpHoldMaxTime)
		{
			_jumpHoldTimer += Time.deltaTime;
			_velocityY     += jumpHoldForce * Time.deltaTime;
		}
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
		_jumpHeld      = false;
		_velocityY     = 0f;
		_jumpHoldTimer = 0f;

		base.OnDeactivated(); // zeros velocity, isKinematic=true, disables Collider+Animator, dims sprite
	}

	// -------------------------------------------------------------------
	// Debug
	// -------------------------------------------------------------------

	void OnDrawGizmosSelected()
	{
		SphereCollider col = GetComponent<SphereCollider>();
		if (col == null) return;

		Vector3 bottom     = transform.position + Vector3.down * (col.radius - groundRayOriginOffset);
		float   halfSpread = Mathf.Max(0f, col.radius - groundRayInset);
		int     count      = Mathf.Max(1, groundRayCount);

		for (int i = 0; i < count; i++)
		{
			float   t      = count > 1 ? (float)i / (count - 1) : 0.5f;
			float   xOff   = Mathf.Lerp(-halfSpread, halfSpread, t);
			Vector3 origin = bottom + new Vector3(xOff, 0f, 0f);

			Gizmos.color = _isGrounded ? Color.green : Color.red;
			Gizmos.DrawLine(origin, origin + Vector3.down * groundRayLength);
			Gizmos.DrawWireSphere(origin, 0.02f);
		}

		Gizmos.color = Color.yellow;
		Gizmos.DrawRay(transform.position, _groundNormal * 0.5f);
	}
}