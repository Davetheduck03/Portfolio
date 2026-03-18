using UnityEngine;

/// <summary>
/// World-space grid that snaps XY positions to cell centres.
/// Owns all grid layout data: cell size, origin, and dimensions.
/// GridRenderer reads from this so everything stays in sync.
/// </summary>
public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    // ---------------------------------------------------------------
    //  Inspector
    // ---------------------------------------------------------------

    [Tooltip("Width and height of each grid cell in world units.")]
    [SerializeField] public float cellSize = 1f;

    [Tooltip("World-space origin of the grid (bottom-left corner of cell 0,0).")]
    [SerializeField] public Vector3 originOffset = Vector3.zero;

    [Tooltip("When true the origin is a cell CORNER — centres sit at " +
             "origin + (n+0.5)*cellSize. When false the origin already " +
             "marks a cell centre.")]
    [SerializeField] public bool originIsCorner = true;

    [Header("Grid Size")]
    [Tooltip("How many cells wide the grid is.")]
    [SerializeField] public int gridWidth = 20;
    [Tooltip("How many cells tall the grid is.")]
    [SerializeField] public int gridHeight = 12;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private int gizmoExtent = 20;
    [SerializeField] private Color gizmoColor = new Color(0.3f, 0.8f, 1f, 0.25f);

    // ---------------------------------------------------------------

    private float HalfCell => originIsCorner ? cellSize * 0.5f : 0f;

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
    /// Returns the world-space centre of the grid cell that contains
    /// <paramref name="worldPos"/>. Z is preserved unchanged.
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        float half = HalfCell;

        float x = Mathf.Floor((worldPos.x - originOffset.x - half) / cellSize + 0.5f)
                  * cellSize + originOffset.x + half;
        float y = Mathf.Floor((worldPos.y - originOffset.y - half) / cellSize + 0.5f)
                  * cellSize + originOffset.y + half;

        return new Vector3(x, y, worldPos.z);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        float half = HalfCell;
        int gx = Mathf.FloorToInt((worldPos.x - originOffset.x - half) / cellSize + 0.5f);
        int gy = Mathf.FloorToInt((worldPos.y - originOffset.y - half) / cellSize + 0.5f);
        return new Vector2Int(gx, gy);
    }

    public Vector3 CellToWorld(Vector2Int cell, float z = 0f)
    {
        float half = HalfCell;
        return new Vector3(
            cell.x * cellSize + originOffset.x + half,
            cell.y * cellSize + originOffset.y + half,
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

        float ox = originOffset.x;
        float oy = originOffset.y;
        float oz = originOffset.z;
        float w = gridWidth * cellSize;
        float h = gridHeight * cellSize;

        // Cell-edge lines within the defined grid area
        for (int i = 0; i <= gridWidth; i++)
        {
            float x = ox + i * cellSize;
            Gizmos.DrawLine(new Vector3(x, oy, oz), new Vector3(x, oy + h, oz));
        }

        for (int i = 0; i <= gridHeight; i++)
        {
            float y = oy + i * cellSize;
            Gizmos.DrawLine(new Vector3(ox, y, oz), new Vector3(ox + w, y, oz));
        }

        // Cell centre dots
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 2f);
        float hc = HalfCell;
        for (int cx = 0; cx < gridWidth; cx++)
            for (int cy = 0; cy < gridHeight; cy++)
            {
                Vector3 centre = new Vector3(
                    ox + cx * cellSize + hc,
                    oy + cy * cellSize + hc,
                    oz);
                Gizmos.DrawWireSphere(centre, cellSize * 0.08f);
            }
    }
#endif
}