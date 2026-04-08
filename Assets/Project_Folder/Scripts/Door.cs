using System.Collections;
using UnityEngine;

/// <summary>
/// A "door" made of one or more blocks that crumble away when a linked
/// Key is collected.
///
/// Setup:
///   1. Add this component to an empty parent GameObject.
///   2. Assign all the block GameObjects you want to break in the Blocks array.
///      These should be your StoneBlock prefab instances or any GameObject with
///      a Collider you want disabled on open.
///   3. In the Inspector, drag the Key into the Linked Key field — OR call
///      Door.Open() directly from code / UnityEvent.
///
/// The blocks shrink via a coroutine, then are destroyed.
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Linked Key")]
    [Tooltip("Assign a Key in the scene. The door opens automatically when it is collected.")]
    [SerializeField] private Key linkedKey;

    [Header("Blocks")]
    [Tooltip("All GameObjects that make up this door. They will break apart when opened.")]
    [SerializeField] private GameObject[] blocks;

    [Header("Break Animation")]
    [Tooltip("How long each block takes to shrink to nothing.")]
    [SerializeField] private float breakDuration = 0.35f;

    [Tooltip("Stagger between each block starting its break animation.")]
    [SerializeField] private float staggerDelay = 0.06f;

    [Tooltip("Optional particle / audio prefab spawned at each block's position on break.")]
    [SerializeField] private GameObject breakFXPrefab;

    private bool _opened;

    private void Start()
    {
        if (linkedKey != null)
            linkedKey.OnKeyCollected += _ => Open();
    }

    private void OnDestroy()
    {
        if (linkedKey != null)
            linkedKey.OnKeyCollected -= _ => Open();
    }

    /// <summary>Call this from any source (UnityEvent, code, etc.) to break the door.</summary>
    public void Open()
    {
        if (_opened) return;
        _opened = true;
        StartCoroutine(BreakSequence());
    }

    private IEnumerator BreakSequence()
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i] != null)
                StartCoroutine(BreakBlock(blocks[i]));

            if (staggerDelay > 0f)
                yield return new WaitForSeconds(staggerDelay);
        }
    }

    private IEnumerator BreakBlock(GameObject block)
    {
        // Disable the collider immediately so characters can walk through
        Collider col = block.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Spawn FX at the block's world position
        if (breakFXPrefab != null)
            Instantiate(breakFXPrefab, block.transform.position, Quaternion.identity);

        // Shrink to zero over breakDuration
        Vector3 startScale = block.transform.localScale;
        float elapsed = 0f;

        while (elapsed < breakDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / breakDuration);

            // Ease in — blocks accelerate as they disappear
            float eased = t * t;
            block.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        Destroy(block);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (blocks == null) return;

        Gizmos.color = _opened
            ? new Color(0.2f, 0.9f, 0.2f, 0.4f)
            : new Color(0.9f, 0.2f, 0.2f, 0.4f);

        foreach (GameObject b in blocks)
        {
            if (b == null) continue;
            Collider col = b.GetComponent<Collider>();
            if (col != null)
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            else
                Gizmos.DrawCube(b.transform.position, Vector3.one);
        }

        // Draw a line from this door to its key
        if (linkedKey != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, linkedKey.transform.position);
        }
    }
#endif
}