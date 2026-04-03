using UnityEngine;

/// <summary>
/// A collectible key that can be picked up by either character.
/// When collected, fires OnKeyCollected with this key's ID so any
/// matching Door can react.
///
/// SETUP:
///   1. Attach to a GameObject that has a Collider with isTrigger = true.
///   2. Give it a unique keyID that matches the Door's requiredKeyID.
///   3. Optionally assign a collectSFX AudioClip and a Renderer for the
///      built-in bob/spin idle animation.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Key : MonoBehaviour
{
    // ------------------------------------------------------------------
    //  Inspector
    // ------------------------------------------------------------------

    [Tooltip("Unique identifier shared with the Door this key unlocks.")]
    [SerializeField] private string keyID = "Key_1";

    [Header("Idle Animation")]
    [Tooltip("How many units the key bobs up and down per cycle.")]
    [SerializeField] private float bobAmplitude = 0.15f;
    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float bobSpeed = 1.2f;
    [Tooltip("Degrees per second the key spins on its Y-axis.")]
    [SerializeField] private float spinSpeed = 90f;

    [Header("Collect Effect")]
    [Tooltip("How long the scale-down pop takes before the key is destroyed.")]
    [SerializeField] private float collectDuration = 0.25f;
    [SerializeField] private AudioClip collectSFX;

    // ------------------------------------------------------------------
    //  Events
    // ------------------------------------------------------------------

    /// <summary>
    /// Fired when any key is collected. The string argument is the key's ID.
    /// </summary>
    public static event System.Action<string> OnKeyCollected;

    // ------------------------------------------------------------------
    //  Private state
    // ------------------------------------------------------------------

    private bool    _collected;
    private float   _collectT;          // 0 → 1 during the collect pop
    private Vector3 _basePosition;
    private Vector3 _originalScale;
    private Collider _col;

    // ------------------------------------------------------------------

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;          // enforce trigger regardless of prefab setting

        _basePosition   = transform.position;
        _originalScale  = transform.localScale;
    }

    void Update()
    {
        if (_collected)
        {
            // Scale-down pop then destroy
            _collectT += Time.deltaTime / collectDuration;
            float s = Mathf.Lerp(1f, 0f, _collectT);
            transform.localScale = _originalScale * s;

            if (_collectT >= 1f)
                Destroy(gameObject);

            return;
        }

        // Idle bob + spin
        float bobY = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        transform.position = _basePosition + Vector3.up * bobY;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    // ------------------------------------------------------------------
    //  Trigger
    // ------------------------------------------------------------------

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        // Only characters can collect keys — ignore boxes, terrain, etc.
        if (other.GetComponent<BaseCharacter>() == null) return;

        Collect();
    }

    // ------------------------------------------------------------------

    private void Collect()
    {
        _collected = true;
        _col.enabled = false;

        if (collectSFX != null)
            AudioSource.PlayClipAtPoint(collectSFX, transform.position);

        OnKeyCollected?.Invoke(keyID);
    }
}
