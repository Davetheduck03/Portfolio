using UnityEngine;

/// <summary>
/// Draws a thin-line grid overlay using GL.Lines — no material asset required.
/// Reads everything (cellSize, origin, width, height) from GridSystem.
///
/// SETUP:
///   1. Attach to any GameObject (Main Camera works fine).
///   2. Configure all grid layout on the GridSystem component.
///   3. Tweak lineColor here in the Inspector.
/// </summary>
public class GridRenderer : MonoBehaviour
{
    [Header("Line Appearance")]
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.12f);

    [Tooltip("Z depth for the lines. Sit between background and sprites.")]
    [SerializeField] private float zDepth = 0f;

    private Material _glMat;

    void OnRenderObject()
    {
        if (GridSystem.Instance == null) return;
        if (_glMat == null) CreateGLMaterial();

        float cellSize = GridSystem.Instance.cellSize;
        Vector3 origin = GridSystem.Instance.originOffset;
        int cellsX = Mathf.Max(1, GridSystem.Instance.gridWidth);
        int cellsY = Mathf.Max(1, GridSystem.Instance.gridHeight);

        float minX = origin.x;
        float minY = origin.y;
        float maxX = origin.x + cellsX * cellSize;
        float maxY = origin.y + cellsY * cellSize;

        _glMat.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(lineColor);

        for (float x = minX; x <= maxX + 0.001f; x += cellSize)
        {
            GL.Vertex3(x, minY, zDepth);
            GL.Vertex3(x, maxY, zDepth);
        }

        for (float y = minY; y <= maxY + 0.001f; y += cellSize)
        {
            GL.Vertex3(minX, y, zDepth);
            GL.Vertex3(maxX, y, zDepth);
        }

        GL.End();
        GL.PopMatrix();
    }

    void OnDisable()
    {
        if (_glMat != null)
        {
            if (Application.isPlaying) Destroy(_glMat);
            else DestroyImmediate(_glMat);
        }
    }

    private void CreateGLMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");

        _glMat = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMat.SetInt("_ZWrite", 0);
    }
}