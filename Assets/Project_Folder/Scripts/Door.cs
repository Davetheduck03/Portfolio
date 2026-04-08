using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A "door" made of blocks that crumble away when a linked Key is collected.
///
/// Block sources (both can be used together):
///   A) Tilemap — drag the Tilemap component in; every child GameObject
///      under it (your Lock_Wall prefabs) is collected automatically.
///   B) Blocks array — assign individual GameObjects directly, as before.
///
/// Setup:
///   1. Add this component to Lock_Door (or any empty parent).
///   2. Drag the Tilemap into the Tilemap Field.
///   3. Optionally assign extra blocks in the Blocks array.
///   4. Drag your Key into Linked Key.
/// </summary>
public class Door : MonoBehaviour
{
	[Header("Linked Key")]
	[Tooltip("The key that unlocks this door.")]
	[SerializeField] private Key linkedKey;

	[Header("Block Sources")]
	[Tooltip("Drag your Tilemap here. Every child GameObject becomes a door block.")]
	[SerializeField] private Tilemap tilemap;

	[Tooltip("Extra individual block GameObjects (used alongside or instead of the Tilemap).")]
	[SerializeField] private GameObject[] blocks;

	[Header("Break Animation")]
	[SerializeField] private float breakDuration = 0.35f;
	[SerializeField] private float staggerDelay = 0.06f;

	[Tooltip("Optional particle / audio prefab spawned at each block position.")]
	[SerializeField] private GameObject breakFXPrefab;

	// ---------------------------------------------------------------
	//  Private state
	// ---------------------------------------------------------------

	private bool _opened;

	// The resolved, combined list built in Start
	private readonly List<GameObject> _allBlocks = new List<GameObject>();

	// ---------------------------------------------------------------
	//  Unity messages
	// ---------------------------------------------------------------

	private void Start()
	{
		BuildBlockList();

		if (linkedKey != null)
			linkedKey.OnKeyCollected += _ => Open();
	}

	private void OnDestroy()
	{
		if (linkedKey != null)
			linkedKey.OnKeyCollected -= _ => Open();
	}

	// ---------------------------------------------------------------
	//  Block list construction
	// ---------------------------------------------------------------

	private void BuildBlockList()
	{
		_allBlocks.Clear();

		// ── A: Tilemap children ──────────────────────────────────────
		if (tilemap != null)
		{
			// Every direct child of the Tilemap transform is a Lock_Wall
			foreach (Transform child in tilemap.transform)
				_allBlocks.Add(child.gameObject);

			Debug.Log($"[Door] '{name}' collected {_allBlocks.Count} blocks from Tilemap '{tilemap.name}'.");
		}

		// ── B: Manually assigned blocks ──────────────────────────────
		if (blocks != null)
		{
			foreach (GameObject b in blocks)
			{
				if (b != null && !_allBlocks.Contains(b))
					_allBlocks.Add(b);
			}
		}

		if (_allBlocks.Count == 0)
			Debug.LogWarning($"[Door] '{name}' has no blocks assigned. " +
							 "Drag a Tilemap or add entries to the Blocks array.");
	}

	// ---------------------------------------------------------------
	//  Public API
	// ---------------------------------------------------------------

	public void Open()
	{
		if (_opened) return;
		_opened = true;
		StartCoroutine(BreakSequence());
	}

	// ---------------------------------------------------------------
	//  Break routines
	// ---------------------------------------------------------------

	private IEnumerator BreakSequence()
	{
		foreach (GameObject block in _allBlocks)
		{
			if (block != null)
				StartCoroutine(BreakBlock(block));

			if (staggerDelay > 0f)
				yield return new WaitForSeconds(staggerDelay);
		}
	}

	private IEnumerator BreakBlock(GameObject block)
	{
		// Disable collider immediately so characters can pass through
		Collider col = block.GetComponent<Collider>();
		if (col != null) col.enabled = false;

		if (breakFXPrefab != null)
			Instantiate(breakFXPrefab, block.transform.position, Quaternion.identity);

		Vector3 startScale = block.transform.localScale;
		float elapsed = 0f;

		while (elapsed < breakDuration)
		{
			elapsed += Time.deltaTime;
			float eased = Mathf.Clamp01(elapsed / breakDuration);
			eased *= eased; // ease-in
			block.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
			yield return null;
		}

		Destroy(block);
	}

	// ---------------------------------------------------------------
	//  Editor helpers
	// ---------------------------------------------------------------

#if UNITY_EDITOR
	/// <summary>
	/// Preview button in the Inspector — populates _allBlocks so the
	/// gizmo can draw without entering Play mode.
	/// </summary>
	[ContextMenu("Preview Block List")]
	private void PreviewBlockList() => BuildBlockList();

	private void OnDrawGizmosSelected()
	{
		// In edit mode, read directly from the Tilemap children
		IEnumerable<GameObject> source = Application.isPlaying
			? (IEnumerable<GameObject>)_allBlocks
			: TilemapChildrenEditMode();

		Gizmos.color = _opened
			? new Color(0.2f, 0.9f, 0.2f, 0.4f)
			: new Color(0.9f, 0.2f, 0.2f, 0.4f);

		foreach (GameObject b in source)
		{
			if (b == null) continue;
			Collider col = b.GetComponent<Collider>();
			Gizmos.DrawCube(
				col != null ? col.bounds.center : b.transform.position,
				col != null ? col.bounds.size : Vector3.one);
		}

		if (linkedKey != null)
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(transform.position, linkedKey.transform.position);
		}
	}

	private IEnumerable<GameObject> TilemapChildrenEditMode()
	{
		if (tilemap != null)
			foreach (Transform child in tilemap.transform)
				yield return child.gameObject;

		if (blocks != null)
			foreach (GameObject b in blocks)
				if (b != null) yield return b;
	}
#endif
}