using UnityEngine;

/// <summary>
/// Dynamically updates any Renderer's sortingOrder based on world Y position.
/// Works on SpriteRenderer (fox/duck) and MeshRenderer (boxes) equally.
/// Objects lower on screen (smaller Y) render in front.
/// </summary>
public class YSort : MonoBehaviour
{
    [Tooltip("Multiply Y by this for finer sort resolution. " +
             "100 = objects must be 0.01 units apart to sort differently.")]
    [SerializeField] private int sortingPrecision = 100;

    [Tooltip("Constant offset on top of the Y-based order. " +
             "Use to nudge one object above another on the same row.")]
    [SerializeField] private int baseOffset = 0;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogWarning($"[YSort] No Renderer on {gameObject.name}.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        _renderer.sortingOrder =
            Mathf.RoundToInt(-transform.position.y * sortingPrecision) + baseOffset;
    }
}
