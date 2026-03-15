using UnityEngine;

/// <summary>
/// Renders a visible tiled-floor grid overlay that matches the GridSystem's cell size.
///
/// QUICK SETUP — fit to your playable area:
///   1. Add a BoxCollider to your floor/room GameObject (size it to the inner playable area).
///   2. Drag that GameObject into the "Fit Target" field on this component.
///   3. The grid will auto-size and auto-center to that collider every time you hit Play
///      or change a value in the Inspector.
///
/// MANUAL SETUP (no collider):
///   Leave "Fit Target" empty and set Grid Width / Height manually (in cells).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridRenderer : MonoBehaviour
{
	[Header("Auto-Fit (recommended)")]
	[Tooltip("Drag in any GameObject that has a BoxCollider covering your inner playable area. " +
	         "The grid will snap to its exact bounds automatically.")]
	[SerializeField] private BoxCollider fitTarget;

	[Header("Manual Size (used when Fit Target is empty)")]
	[Tooltip("How many cells wide the grid quad spans.")]
	[SerializeField] private int gridWidth  = 20;
	[Tooltip("How many cells tall the grid quad spans.")]
	[SerializeField] private int gridHeight = 12;

	[Header("Visuals")]
	[Tooltip("Color of the grid lines. Alpha controls opacity.")]
	[SerializeField] private Color lineColor = new Color(0.25f, 0.25f, 0.45f, 0.30f);
	[Tooltip("Thickness of each cell border in texture pixels.")]
	[SerializeField, Range(1, 8)] private int borderPixels = 2;
	[Tooltip("Pixel resolution of one cell in the texture.")]
	[SerializeField, Range(16, 128)] private int cellPixels = 32;
	[Tooltip("Z offset so the grid sits in front of the background but behind characters.")]
	[SerializeField] private float zOffset = 0.1f;

	// ---------------------------------------------------------------
	//  Runtime state
	// ---------------------------------------------------------------

	private MeshRenderer _mr;
	private MeshFilter   _mf;
	private Material     _mat;

	// The actual world-space size used this build (may come from fitTarget or manual values)
	private float _builtWidth;
	private float _builtHeight;
	private int   _builtCellsX;
	private int   _builtCellsY;

	// ---------------------------------------------------------------
	//  Unity messages
	// ---------------------------------------------------------------

	void OnEnable()
	{
		_mr = GetComponent<MeshRenderer>();
		_mf = GetComponent<MeshFilter>();
		Rebuild();
	}

#if UNITY_EDITOR
	void OnValidate()
	{
		UnityEditor.EditorApplication.delayCall += () =>
		{
			if (this == null) return;
			_mr = GetComponent<MeshRenderer>();
			_mf = GetComponent<MeshFilter>();
			Rebuild();
		};
	}
#endif

	// ---------------------------------------------------------------
	//  Core rebuild
	// ---------------------------------------------------------------

	void Rebuild()
	{
		if (_mr == null || _mf == null) return;

		float cellSize = GridSystem.Instance != null ? GridSystem.Instance.cellSize : 1f;

		// ---- Determine world-space dimensions and centre ----
		Vector3 centre;

		if (fitTarget != null)
		{
			// Use the BoxCollider's world bounds to fit exactly
			Bounds b = fitTarget.bounds;

			_builtWidth  = b.size.x;
			_builtHeight = b.size.y;

			// Round to whole cells so the texture tiles cleanly
			_builtCellsX = Mathf.Max(1, Mathf.RoundToInt(_builtWidth  / cellSize));
			_builtCellsY = Mathf.Max(1, Mathf.RoundToInt(_builtHeight / cellSize));

			// Snap the actual mesh size to a whole number of cells
			_builtWidth  = _builtCellsX * cellSize;
			_builtHeight = _builtCellsY * cellSize;

			centre = new Vector3(b.center.x, b.center.y, b.center.z + zOffset);
		}
		else
		{
			// Manual size
			_builtCellsX = Mathf.Max(1, gridWidth);
			_builtCellsY = Mathf.Max(1, gridHeight);
			_builtWidth  = _builtCellsX * cellSize;
			_builtHeight = _builtCellsY * cellSize;

			Vector3 origin = GridSystem.Instance != null ? GridSystem.Instance.originOffset : Vector3.zero;
			centre = new Vector3(
				origin.x + _builtWidth  * 0.5f,
				origin.y + _builtHeight * 0.5f,
				origin.z + zOffset);
		}

		transform.position = centre;

		BuildQuadMesh(_builtWidth, _builtHeight);
		ApplyGridMaterial();
	}

	// ---------------------------------------------------------------
	//  Mesh
	// ---------------------------------------------------------------

	void BuildQuadMesh(float width, float height)
	{
		float hw = width  * 0.5f;
		float hh = height * 0.5f;

		var mesh = new Mesh { name = "GridQuad" };

		mesh.vertices = new Vector3[]
		{
			new Vector3(-hw, -hh, 0f),
			new Vector3( hw, -hh, 0f),
			new Vector3( hw,  hh, 0f),
			new Vector3(-hw,  hh, 0f),
		};

		mesh.uv = new Vector2[]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f),
		};

		mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
		mesh.RecalculateNormals();
		_mf.sharedMesh = mesh;
	}

	// ---------------------------------------------------------------
	//  Material & Texture
	// ---------------------------------------------------------------

	void ApplyGridMaterial()
	{
		Texture2D tex = BuildGridTexture();

		if (_mat == null)
			_mat = CreateTransparentMaterial();

		_mat.mainTexture      = tex;
		_mat.mainTextureScale = new Vector2(_builtCellsX, _builtCellsY);
		_mat.mainTextureOffset = Vector2.zero;

		_mr.sharedMaterial = _mat;
	}

	Texture2D BuildGridTexture()
	{
		int size = cellPixels;
		var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Point,
			wrapMode   = TextureWrapMode.Repeat,
		};

		Color clear = new Color(0f, 0f, 0f, 0f);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			bool border = x < borderPixels || y < borderPixels
			           || x >= size - borderPixels || y >= size - borderPixels;
			tex.SetPixel(x, y, border ? lineColor : clear);
		}

		tex.Apply();
		return tex;
	}

	static Material CreateTransparentMaterial()
	{
		Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
		if (urpUnlit != null)
		{
			var mat = new Material(urpUnlit);
			mat.SetFloat("_Surface",   1f);
			mat.SetFloat("_Blend",     0f);
			mat.SetFloat("_AlphaClip", 0f);
			mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			mat.renderQueue = 2999;
			return mat;
		}

		Shader sprites = Shader.Find("Sprites/Default");
		if (sprites != null) return new Material(sprites);

		return new Material(Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard"));
	}

	// ---------------------------------------------------------------
	//  Debug
	// ---------------------------------------------------------------

#if UNITY_EDITOR
	void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
		Gizmos.DrawWireCube(transform.position, new Vector3(_builtWidth, _builtHeight, 0.02f));
	}
#endif
}
