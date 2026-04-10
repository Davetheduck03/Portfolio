using UnityEngine;

/// <summary>
/// A collectible key. When any character's collider enters the trigger,
/// the key is consumed and OnKeyCollected is raised.
///
/// Setup:
///   1. Add to a GameObject with an IsTrigger collider.
///   2. Wire the OnKeyCollected UnityEvent (or subscribe in code) to a Door.Open().
///   3. Optionally assign a visual to hide on collection.
/// </summary>
public class Key : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Root visual to hide when collected. Defaults to this GameObject.")]
    [SerializeField] private GameObject visual;

    public event System.Action<Key> OnKeyCollected;

    private bool _collected;

    private void Awake()
    {
        if (visual == null) visual = gameObject;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        // Characters and enemies can collect keys
        bool isCharacter = other.GetComponent<BaseCharacter>() != null
                        || other.GetComponentInParent<BaseCharacter>() != null;
        bool isEnemy     = other.GetComponent<Enemy>() != null
                        || other.GetComponentInParent<Enemy>() != null;

        if (!isCharacter && !isEnemy) return;

        Collect();
    }

    private void Collect()
    {
        _collected = true;
        visual.SetActive(false);
        OnKeyCollected?.Invoke(this);

        // Destroy after a frame so subscribers can react
        Destroy(gameObject, 0.05f);
    }
}