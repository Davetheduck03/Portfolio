using UnityEngine;

/// <summary>
/// Switches the main camera between two fixed world positions when Tab is pressed:
///   - Orthographic  : covers the full 2D level
///   - Perspective   : angled top-down view covering the full top-down level
///
/// Attach to the Main Camera. Both positions are set in world space so the camera
/// never follows any character — it always shows the whole level.
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
	[SerializeField] private float orthoSize = 5f;

	// ------------------------------------------------------------------
	//  Top-Down / Perspective mode
	// ------------------------------------------------------------------
	[Header("Top-Down Mode (Perspective)")]
	[Tooltip("Fixed world-space position of the camera in top-down mode.")]
	[SerializeField] private Vector3 perspPosition = new Vector3(0f, 7f, -6f);
	[SerializeField] private float perspFOV = 55f;
	[Tooltip("Euler angles for the perspective camera (tilt ~50° gives a Toodee-and-Topdee feel).")]
	[SerializeField] private Vector3 perspEuler = new Vector3(50f, 0f, 0f);

	// ------------------------------------------------------------------
	//  Transition
	// ------------------------------------------------------------------
	[Header("Transition")]
	[SerializeField] private float transitionDuration = 0.7f;

	// ------------------------------------------------------------------
	//  Private state
	// ------------------------------------------------------------------
	private bool _isTopDown;

	private float      _transitionT = 1f;   // 1 = idle
	private Vector3    _fromPosition;
	private Quaternion _fromRotation;
	private float      _fromOrthoSize;
	private float      _fromFOV;
	private bool       _switchToOrthoAt50;

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
		_fromPosition  = cam.transform.position;
		_fromRotation  = cam.transform.rotation;
		_fromOrthoSize = cam.orthographicSize;
		_fromFOV       = cam.fieldOfView;

		_switchToOrthoAt50 = !_isTopDown;   // going to 2D → flip to ortho at midpoint

		// Keep perspective during the lerp so position/FOV animate smoothly
		cam.orthographic = false;

		_transitionT = 0f;
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
		float t = Mathf.SmoothStep(0f, 1f, _transitionT);

		// Flip projection type at the midpoint
		if (_transitionT >= 0.5f)
			cam.orthographic = _switchToOrthoAt50;

		// Lerp to the fixed destination position
		Vector3    destPos = _isTopDown ? perspPosition : orthoPosition;
		Quaternion destRot = _isTopDown ? Quaternion.Euler(perspEuler) : Quaternion.Euler(0f, 0f, 0f);

		cam.transform.position = Vector3.Lerp(_fromPosition, destPos, t);
		cam.transform.rotation = Quaternion.Slerp(_fromRotation, destRot, t);

		if (cam.orthographic)
			cam.orthographicSize = Mathf.Lerp(_fromOrthoSize, orthoSize, t);
		else
			cam.fieldOfView = Mathf.Lerp(_fromFOV, perspFOV, t);
	}
}
