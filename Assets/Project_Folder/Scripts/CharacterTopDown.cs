using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class TopDownCharacter : BaseCharacter
{
	[Header("Movement")]
	[SerializeField] private float moveSpeed = 5f;

	[Header("Animation")]
	[SerializeField] private Animator animator;
	[SerializeField] private float animSmoothing = 10f;

	private static readonly int MoveX = Animator.StringToHash("MoveX");
	private static readonly int MoveY = Animator.StringToHash("MoveY");
	private static readonly int Speed = Animator.StringToHash("Speed");

	private Rigidbody _rb;
	private Vector3 _inputDir;
	private Vector2 _animDir;
	private Vector2 _lastFacingDir = new Vector2(0f, -1f);  // default face South

	void Awake()
	{
		_rb = GetComponent<Rigidbody>();
		_rb.useGravity = false;
		_rb.freezeRotation = true;
		_rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
	}

	public override void HandleInput()
	{
		float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
		float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
		_inputDir = new Vector3(x, y, 0f).normalized;

		if (animator == null) return;

		bool moving = _inputDir.sqrMagnitude > 0.01f;

		if (moving)
		{
			// Remember the last direction so idle faces the right way
			_lastFacingDir = new Vector2(_inputDir.x, _inputDir.y);

			// Smooth blend params toward the new input direction
			_animDir = Vector2.Lerp(_animDir, _lastFacingDir, animSmoothing * Time.deltaTime);
		}
		else
		{
			// Snap blend params to the last facing direction so the idle
			// blend tree picks the matching directional idle clip
			_animDir = _lastFacingDir;
		}

		animator.SetFloat(MoveX, _animDir.x);
		animator.SetFloat(MoveY, _animDir.y);
		animator.SetFloat(Speed, moving ? 1f : 0f);
	}

	public override void Tick()
	{
		Vector3 move = new Vector3(_inputDir.x * moveSpeed, _inputDir.y * moveSpeed, 0f);
		_rb.linearVelocity = move;
	}

	public override void OnDeactivated()
	{
		base.OnDeactivated();
		_inputDir = Vector3.zero;
		_animDir  = Vector2.zero;
		_rb.linearVelocity = Vector3.zero;

		if (animator != null)
		{
			animator.SetFloat(MoveX, _lastFacingDir.x);
			animator.SetFloat(MoveY, _lastFacingDir.y);
			animator.SetFloat(Speed, 0f);
		}
	}
}