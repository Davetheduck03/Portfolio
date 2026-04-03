using UnityEngine;

/// <summary>
/// Assigns a Sorting Layer to this object's Renderer at startup.
/// Add this to any prefab whose MeshRenderer needs to participate in
/// the sprite sorting stack (walls, floors, props, etc.).
/// No material changes — purely a Renderer property.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SetSortingLayer : MonoBehaviour
{
    [Tooltip("Must exactly match a Sorting Layer name defined in " +
             "Edit > Project Settings > Tags and Layers > Sorting Layers.")]
    [SerializeField] private string layerName = "World";

    private void Awake()
    {
        GetComponent<Renderer>().sortingLayerName = layerName;
    }
}
