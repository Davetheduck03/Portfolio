using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour
{
	public bool IsActive { get; private set; }

	[Header("Inactive State")]
	[Tooltip("Sprite opacity while this character is inactive.")]
	[SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.75f;

	// ── Audio ─────────────────────────────────────────────────────────────
	[Header("Audio")]
	[Tooltip("AudioSource used for one-shot sounds (jump, pickup, etc.). " +
	         "Auto-found via GetComponent if left empty.")]
	[SerializeField] protected AudioSource characterAudio;

	// Cached so we never call GetComponent in hot paths
	private Rigidbody      _baseRb;
	private Collider       _baseCol;
	private Animator       _baseAnim;
	private SpriteRenderer _baseSprite;
	private MeshRenderer[] _baseMeshRenderers;

	protected virtual void Awake()
	{
		_baseRb             = GetComponent<Rigidbody>();
		_baseCol            = GetComponent<Collider>();
		_baseAnim           = GetComponentInChildren<Animator>();
		_baseSprite         = GetComponentInChildren<SpriteRenderer>();
		_baseMeshRenderers  = GetComponentsInChildren<MeshRenderer>();

		// Auto-find the SFX AudioSource if not wired in the Inspector.
		if (characterAudio == null)
			characterAudio = GetComponent<AudioSource>();
	}

	/// <summary>Called when this character becomes the active one.</summary>
	public virtual void OnActivated()
	{
		IsActive = true;

		// Re-enable physics and rendering
		if (_baseRb  != null) _baseRb.isKinematic  = false;
		if (_baseCol != null) _baseCol.enabled      = true;
		if (_baseAnim != null) _baseAnim.enabled    = true;

		// Restore full opacity
		SetSpriteAlpha(1f);
	}

	/// <summary>Called when this character is swapped out.</summary>
	public virtual void OnDeactivated()
	{
		IsActive = false;

		// Stop all physics motion before going kinematic
		if (_baseRb != null)
		{
			_baseRb.linearVelocity = Vector3.zero;
			_baseRb.isKinematic    = true;
		}

		if (_baseCol  != null) _baseCol.enabled   = false;
		if (_baseAnim != null) _baseAnim.enabled  = false;

		// Dim the sprite so it reads as inactive
		SetSpriteAlpha(inactiveAlpha);

	}

	// ── Audio helpers (called by subclasses) ──────────────────────────

	/// <summary>Plays a non-looping one-shot on the character AudioSource.</summary>
	protected void PlayOneShot(AudioClip clip)
	{
		if (characterAudio != null && clip != null)
			characterAudio.PlayOneShot(clip);
	}

	/// <summary>
	/// Immediately stops all audio on this character.
	/// Called by LevelManager when the death screen is shown (TimeScale = 0).
	/// </summary>
	public void StopAllAudio()
	{
		if (characterAudio != null) characterAudio.Stop();
	}

	private void SetSpriteAlpha(float alpha)
	{
		// SpriteRenderer (duck)
		if (_baseSprite != null)
		{
			Color c = _baseSprite.color;
			c.a = alpha;
			_baseSprite.color = c;
		}

		// MeshRenderer children (fox)
		foreach (MeshRenderer mr in _baseMeshRenderers)
		{
			foreach (Material mat in mr.materials)
			{
				Color c = mat.color;
				c.a = alpha;
				mat.color = c;
			}
		}
	}

	public abstract void HandleInput();
	public abstract void Tick(); // physics/movement update
}