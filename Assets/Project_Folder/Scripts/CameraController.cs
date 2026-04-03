using UnityEngine;

/// <summary>
/// Switches the main camera between two fixed world positions when the active character changes:
///   - Orthographic  : covers the full 2D level
///   - Perspective   : angled top-down view covering the full top-down level
///
/// Attach to the Main Camera. Both positions are set in world space so the camera
/// never follows any character — it always shows the whole level.
///
/// Transition improvements over the original:
///   • AnimationCurve for fully inspector-tunable easing (replaces hardcoded SmoothStep).
///   • TopDown → 2D  : stays in perspective until orthoFlipPoint (~88 % through), then
///     snaps to orthographic — the camera is nearly at its destination so the pop is minimal.
///   • 2D → TopDown  : switches to perspective immediately and starts with an FOV that
///     matches the on-screen size of the ortho view, then converges to perspFOV as the
///     camera swoops into position (smooth cinematic arc rather than a mid-flight snap).
/// </summary>
public class CameraController : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Camera cam;

	// ------------------------------------------------------------------
	//  2-D / Orthographic mode
	// ------------------------------------------------------------------
	[Header("2D Mode (Orthographic)")]
	[Tooltip("Fixed world-space position of the camera in 2D mode.")]
	[SerializeField] private Vector3 orthoPosition = new Vector3(0f, 0f, -10f);
	[SerializeField] private float orthoSize = 11f;

	// ------------------------------------------------------------------
	//  Top-Down / Perspective mode
	// ------------------------------------------------------------------
	[Header("Top-Down Mode (Perspective)")]
	[Tooltip("Fixed world-space position of the camera in top-down mode.")]
	[SerializeField] private Vector3 perspPosition = new Vector3(0f, -17f, -38f);
	[SerializeField] private float perspFOV = 35f;
	[Tooltip("Euler angles for the perspective camera (tilt gives a Toodee-and-Topdee feel).")]
	[SerializeField] private Vector3 perspEuler = new Vector3(-25f, 0f, 0f);

	// ------------------------------------------------------------------
	//  Transition
	// ------------------------------------------------------------------
	[Header("Transition")]
	[SerializeField] private float transitionDuration = 1f;

	[Tooltip("Maps normalised time [0,1] to a blend factor [0,1]. " +
	         "EaseInOut is the default. Shape the curve in the Inspector for extra polish " +
	         "(e.g. a slight overshoot at the end for a more dynamic feel).")]
	[SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[Tooltip("Normalised time at which the camera flips from Perspective → Orthographic " +
	         "when entering 2D mode. Higher = later = less noticeable snap because the " +
	         "camera has almost reached its 2D destination before switching projection.")]
	[SerializeField, Range(0.5f, 0.99f)] private float orthoFlipPoint = 0.88f;

	// ------------------------------------------------------------------
	//  Private state
	// ------------------------------------------------------------------
	private bool       _isTopDown;
	private float      _transitionT = 1f;   // 1 = idle
	private Vector3    _fromPosition;
	private Quaternion _fromRotation;
	private float      _fromFOV;            // starting FOV for the perspective leg of the transition
	private float      _toFOV;             // ending FOV for the perspective leg of the transition
	private bool       _goingToTopDown;     // direction of the in-progress transition

	// ------------------------------------------------------------------
	void Start()
	{
		if (cam == null) cam = Camera.main;

		CharacterManager.Instance.OnCharacterSwitched += HandleCharacterSwitched;

		_isTopDown = CharacterManager.Instance.ActiveCharacter is TopDownCharacter;
		SnapToCurrentMode();
	}

	void OnDestroy()
	{
		if (CharacterManager.Instance != null)
			CharacterManager.Instance.OnCharacterSwitched -= HandleCharacterSwitched;
	}

	// ------------------------------------------------------------------
	//  Event callback
	// ------------------------------------------------------------------
	private void HandleCharacterSwitched(BaseCharacter newCharacter)
	{
		bool switchingToTopDown = newCharacter is TopDownCharacter;
		if (switchingToTopDown == _isTopDown) return;

		_isTopDown = switchingToTopDown;
		BeginTransition();
	}

	// ------------------------------------------------------------------
	//  Transition
	// ------------------------------------------------------------------
	private void BeginTransition()
	{
		_goingToTopDown = _isTopDown;
		_fromPosition   = cam.transform.position;
		_fromRotation   = cam.transform.rotation;

		// Run the whole lerp in perspective so position and rotation animate without
		// the camera popping between two fundamentally different projection matrices.
		if (_goingToTopDown && cam.orthographic)
		{
			// 2D → TopDown: compute a starting FOV that approximates the on-screen
			// half-height of the ortho view so the cut to perspective is barely visible.
			float depth = Mathf.Max(Mathf.Abs(cam.transform.position.z), 0.1f);
			_fromFOV = 2f * Mathf.Rad2Deg * Mathf.Atan(cam.orthographicSize / depth);
			_toFOV   = perspFOV;
		}
		else
		{
			// TopDown → 2D: start from the current FOV, and compute what FOV perspective
			// would need at the 2D position to match orthoSize. Lerping toward this keeps
			// the apparent scene coverage constant so the camera doesn't zoom in as it
			// approaches, and the snap to orthographic at orthoFlipPoint is imperceptible.
			_fromFOV = cam.fieldOfView;
			float depth2D = Mathf.Max(Mathf.Abs(orthoPosition.z), 0.1f);
			_toFOV   = 2f * Mathf.Rad2Deg * Mathf.Atan(orthoSize / depth2D);
		}

		cam.orthographic = false;
		cam.fieldOfView  = _fromFOV;
		_transitionT     = 0f;
	}

	private void SnapToCurrentMode()
	{
		if (_isTopDown)
		{
			cam.orthographic       = false;
			cam.fieldOfView        = perspFOV;
			cam.transform.rotation = Quaternion.Euler(perspEuler);
			cam.transform.position = perspPosition;
		}
		else
		{
			cam.orthographic      = true;
			cam.orthographicSize  = orthoSize;
			cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
			cam.transform.position = orthoPosition;
		}

		_transitionT = 1f;
	}

	// ------------------------------------------------------------------
	//  Per-frame update  (only needed during transitions)
	// ------------------------------------------------------------------
	void LateUpdate()
	{
		if (_transitionT >= 1f) return;

		_transitionT = Mathf.MoveTowards(_transitionT, 1f, Time.deltaTime / transitionDuration);
		float t = easeCurve.Evaluate(_transitionT);

		// Destination position and rotation depend on direction.
		Vector3    destPos = _goingToTopDown ? perspPosition : orthoPosition;
		Quaternion destRot = _goingToTopDown
			? Quaternion.Euler(perspEuler)
			: Quaternion.Euler(0f, 0f, 0f);

		cam.transform.position = Vector3.Lerp(_fromPosition, destPos, t);
		cam.transform.rotation = Quaternion.Slerp(_fromRotation, destRot, t);

		if (_goingToTopDown)
		{
			// 2D → TopDown: narrow the FOV from the ortho-equivalent value down to
			// perspFOV as the camera swoops into the top-down position.
			cam.fieldOfView = Mathf.Lerp(_fromFOV, _toFOV, t);
		}
		else
		{
			// TopDown → 2D: widen the FOV toward the ortho-equivalent value at the
			// 2D position so scene coverage stays constant as the camera moves closer.
			// Flip to orthographic near the end — the projection matrices match so
			// the snap is imperceptible.
			cam.fieldOfView = Mathf.Lerp(_fromFOV, _toFOV, t);
			if (_transitionT >= orthoFlipPoint && !cam.orthographic)
			{
				cam.orthographic     = true;
				cam.orthographicSize = orthoSize;
			}
		}
	}
}
