using UnityEngine;

/// <summary>
/// Simple world-space grid that snaps XY positions to cell centres.
/// Add one instance to the scene (e.g. on an empty GameObject called "GridSystem").
/// </summary>
public class GridSystem : MonoBehaviour
{
	// ---------------------------------------------------------------
	//  Singleton
	// ---------------------------------------------------------------

	public static GridSystem Instance { get; private set; }

	// ---------------------------------------------------------------
	//  Inspector
	// ---------------------------------------------------------------

	[Tooltip("Width and height of each grid cell in world units. " +
	         "Set this to match the size of your blocks (default 1).")]
	[SerializeField] public float cellSize = 1f;

	[Tooltip("World-space origin offset for the grid. " +
	         "Use this if your blocks are centred on 0.5, 1.5, … instead of 0, 1, …")]
	[SerializeField] public Vector3 originOffset = Vector3.zero;

	[Header("Debug")]
	[Tooltip("Draw the grid in the Scene view.")]
	[SerializeField] private bool showGizmos    = true;
	[SerializeField] private int  gizmoExtent   = 20;    // half-size of the drawn grid
	[SerializeField] private Color gizmoColor   = new Color(0.3f, 0.8f, 1f, 0.25f);

	// ---------------------------------------------------------------
	//  Unity messages
	// ---------------------------------------------------------------

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("[GridSystem] Duplicate instance destroyed.", this);
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	// ---------------------------------------------------------------
	//  Public API
	// ---------------------------------------------------------------

	/// <summary>
	/// Returns the world-space centre of the grid cell that contains <paramref name="worldPos"/>.
	/// Z is preserved unchanged (the grid only operates in XY).
	/// </summary>
	public Vector3 SnapToGrid(Vector3 worldPos)
	{
		float x = Mathf.Round((worldPos.x - originOffset.x) / cellSize) * cellSize + originOffset.x;
		float y = Mathf.Round((worldPos.y - originOffset.y) / cellSize) * cellSize + originOffset.y;
		return new Vector3(x, y, worldPos.z);
	}

	/// <summary>
	/// Converts a world position to integer grid coordinates (no origin offset applied).
	/// </summary>
	public Vector2Int WorldToCell(Vector3 worldPos)
	{
		int gx = Mathf.RoundToInt((worldPos.x - originOffset.x) / cellSize);
		int gy = Mathf.RoundToInt((worldPos.y - originOffset.y) / cellSize);
		return new Vector2Int(gx, gy);
	}

	/// <summary>
	/// Converts integer grid coordinates back to a world-space centre position.
	/// </summary>
	public Vector3 CellToWorld(Vector2Int cell, float z = 0f)
	{
		return new Vector3(
			cell.x * cellSize + originOffset.x,
			cell.y * cellSize + originOffset.y,
			z);
	}

	// ---------------------------------------------------------------
	//  Gizmos
	// ---------------------------------------------------------------

#if UNITY_EDITOR
	void OnDrawGizmos()
	{
		if (!showGizmos) return;

		Gizmos.color = gizmoColor;

		float half  = gizmoExtent * cellSize;
		Vector3 org = originOffset;

		// Vertical lines
		for (int i = -gizmoExtent; i <= gizmoExtent; i++)
		{
			float x = org.x + i * cellSize;
			Gizmos.DrawLine(new Vector3(x, org.y - half, org.z),
			                new Vector3(x, org.y + half, org.z));
		}

		// Horizontal lines
		for (int i = -gizmoExtent; i <= gizmoExtent; i++)
		{
			float y = org.y + i * cellSize;
			Gizmos.DrawLine(new Vector3(org.x - half, y, org.z),
			                new Vector3(org.x + half, y, org.z));
		}
	}
#endif
}
