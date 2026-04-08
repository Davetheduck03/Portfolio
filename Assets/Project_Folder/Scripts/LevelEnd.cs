using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level end portal. Both characters must enter simultaneously.
///
/// Setup:
///   1. Add this script to your portal GameObject.
///   2. Attach a trigger Collider (Box or Circle) to the same GameObject.
///   3. Assign the Animator (which plays your portal spritesheet animations).
///   4. Assign the SpriteRenderer so the script can tint it.
///   5. Create three Animator states and set the string names below:
///        Idle       — BluePortal / OrangePortal looping animation
///        OneReady   — same or faster loop, tinted yellow
///        Complete   — fastest spin or a dedicated "sucking in" animation
///   6. Wire OnLevelComplete to SceneManager.LoadScene etc.
/// </summary>
public class LevelEnd : MonoBehaviour
{
	// ---------------------------------------------------------------
	//  Inspector
	// ---------------------------------------------------------------

	[Header("Portal Components")]
	[Tooltip("Animator on the portal sprite GameObject.")]
	[SerializeField] private Animator portalAnimator;

	[Tooltip("SpriteRenderer on the portal — used for tinting.")]
	[SerializeField] private SpriteRenderer portalRenderer;

	[Header("Animator State Names")]
	[Tooltip("State to play while waiting for characters.")]
	[SerializeField] private string idleStateName = "PortalIdle";
	[Tooltip("State to play when one character is inside.")]
	[SerializeField] private string oneReadyStateName = "PortalOneReady";
	[Tooltip("State to play the moment both characters are inside.")]
	[SerializeField] private string completeStateName = "PortalComplete";

	[Header("Portal Colours")]
	[SerializeField] private Color idleColour = Color.white;
	[SerializeField] private Color oneReadyColour = new Color(1f, 0.9f, 0.2f, 1f);   // yellow tint
	[SerializeField] private Color bothReadyColour = new Color(0.4f, 1f, 0.5f, 1f);   // green tint

	[Header("Idle Pulse")]
	[Tooltip("The portal bobs up and down on the Y axis while idle.")]
	[SerializeField] private float bobAmount = 0.08f;
	[SerializeField] private float bobSpeed = 1.4f;

	[Header("Completion")]
	[Tooltip("Seconds between both characters arriving and the suck-in starting.")]
	[SerializeField] private float completionDelay = 0.8f;
	[Tooltip("How long each character takes to fly into the portal and vanish.")]
	[SerializeField] private float suckInDuration = 0.7f;

	[Tooltip("Fired once after both characters are absorbed.")]
	public UnityEngine.Events.UnityEvent OnLevelComplete;

	// ---------------------------------------------------------------
	//  Private state
	// ---------------------------------------------------------------

	private readonly HashSet<BaseCharacter> _inside = new HashSet<BaseCharacter>();
	private bool _completed;
	private int _requiredCount;

	private Vector3 _portalOrigin;         // world position at Start
	private Color _currentColour;
	private Color _targetColour;

	// ---------------------------------------------------------------
	//  Unity messages
	// ---------------------------------------------------------------

	private void Start()
	{
		_requiredCount = CharacterManager.Instance != null
			? CharacterManager.Instance.CharacterCount
			: 1;

		_portalOrigin = transform.position;
		_currentColour = idleColour;
		_targetColour = idleColour;

		if (portalRenderer != null)
			portalRenderer.color = idleColour;

		PlayState(idleStateName);
	}

	private void Update()
	{
		if (_completed) return;

		RunIdleBob();
		LerpColour();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (_completed) return;

		BaseCharacter c = other.GetComponent<BaseCharacter>()
					   ?? other.GetComponentInParent<BaseCharacter>();
		if (c == null) return;

		_inside.Add(c);
		RefreshState();

		if (_inside.Count >= _requiredCount)
			StartCoroutine(CompleteRoutine());
	}

	private void OnTriggerExit(Collider other)
	{
		if (_completed) return;

		BaseCharacter c = other.GetComponent<BaseCharacter>()
					   ?? other.GetComponentInParent<BaseCharacter>();
		if (c == null) return;

		_inside.Remove(c);
		RefreshState();
	}

	// ---------------------------------------------------------------
	//  Idle bob
	// ---------------------------------------------------------------

	private void RunIdleBob()
	{
		float yOffset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobAmount;
		transform.position = _portalOrigin + new Vector3(0f, yOffset, 0f);
	}

	// ---------------------------------------------------------------
	//  State / colour
	// ---------------------------------------------------------------

	private void RefreshState()
	{
		if (_inside.Count == 0)
		{
			_targetColour = idleColour;
			PlayState(idleStateName);
		}
		else if (_inside.Count < _requiredCount)
		{
			_targetColour = oneReadyColour;
			PlayState(oneReadyStateName);
		}
		else
		{
			_targetColour = bothReadyColour;
			PlayState(completeStateName);
		}
	}

	private void LerpColour()
	{
		_currentColour = Color.Lerp(_currentColour, _targetColour, Time.deltaTime * 6f);
		if (portalRenderer != null)
			portalRenderer.color = _currentColour;
	}

	private void PlayState(string stateName)
	{
		if (portalAnimator == null || string.IsNullOrEmpty(stateName)) return;
		portalAnimator.Play(stateName);
	}

	// ---------------------------------------------------------------
	//  Completion sequence
	// ---------------------------------------------------------------

	private IEnumerator CompleteRoutine()
	{
		// Wait, bail if someone leaves
		float elapsed = 0f;
		while (elapsed < completionDelay)
		{
			if (_inside.Count < _requiredCount) yield break;
			elapsed += Time.deltaTime;
			yield return null;
		}

		if (_inside.Count < _requiredCount) yield break;

		_completed = true;

		// Suck all characters in simultaneously
		var routines = new List<Coroutine>();
		foreach (BaseCharacter c in _inside)
			routines.Add(StartCoroutine(SuckIn(c)));

		foreach (Coroutine co in routines)
			yield return co;

		yield return new WaitForSeconds(0.25f);
		OnLevelComplete.Invoke();
	}

	/// <summary>
	/// Flies a character toward the portal centre, spins and shrinks them to nothing.
	/// </summary>
	private IEnumerator SuckIn(BaseCharacter character)
	{
		// Stop physics fighting the animation
		Rigidbody rb = character.GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.isKinematic = true;
		}

		Collider col = character.GetComponent<Collider>();
		if (col != null) col.enabled = false;

		// Stop any running animator on the character
		Animator anim = character.GetComponentInChildren<Animator>();
		if (anim != null) anim.enabled = false;

		Vector3 startPos = character.transform.position;
		Vector3 targetPos = transform.position;
		Vector3 startScale = character.transform.localScale;

		// Offset target slightly so both characters don't perfectly overlap
		Vector3 offset = (startPos.x < targetPos.x ? Vector3.right : Vector3.left) * 0.1f;
		targetPos += offset;

		float t = 0f;
		while (t < 1f)
		{
			t += Time.deltaTime / suckInDuration;
			float eased = t * t * t;           // cubic ease-in — snappy pull

			character.transform.position = Vector3.Lerp(startPos, targetPos, eased);
			character.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);

			// Spin faster as they approach the centre
			float spinSpeed = Mathf.Lerp(180f, 1080f, t);
			character.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

			yield return null;
		}

		character.gameObject.SetActive(false);
	}

	// ---------------------------------------------------------------
	//  Public queries (for HUD)
	// ---------------------------------------------------------------

	public int CharactersInside => _inside.Count;
	public int CharactersRequired => _requiredCount;

	// ---------------------------------------------------------------
	//  Gizmos
	// ---------------------------------------------------------------

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		Collider col = GetComponent<Collider>();

		Gizmos.color = _completed
			? new Color(0.2f, 0.9f, 0.2f, 0.35f)
			: _inside.Count > 0
				? new Color(1f, 0.85f, 0.1f, 0.35f)
				: new Color(0.35f, 0.45f, 1f, 0.35f);

		if (col != null)
			Gizmos.DrawCube(col.bounds.center, col.bounds.size);
		else
			Gizmos.DrawSphere(transform.position, 0.8f);

		UnityEditor.Handles.Label(
			transform.position + Vector3.up * 1.2f,
			$"Portal  {_inside.Count} / {_requiredCount}");
	}
#endif
}