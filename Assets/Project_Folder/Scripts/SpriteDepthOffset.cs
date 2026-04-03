using UnityEngine;

/// <summary>
/// Pushes the SpriteRenderer child forward in Z so it always sits in front
/// of 3D mesh geometry (walls, boxes, etc.) in a top-down 2.5D scene.
///
/// How it works:
///   A 3D box centred at Z=0 has its front face at Z ≈ -0.5.
///   A sprite also at Z=0 is therefore INSIDE the box — the depth buffer
///   lets the box face win.  Moving the sprite to Z = -0.6 (or any value
///   less than -0.5) puts it physically in front of every standard 1-unit
///   3D mesh, so it always draws on top regardless of sorting layers.
///
/// Setup:
///   1. Your SpriteRenderer MUST be on a CHILD GameObject, not the same
///      GameObject as the Rigidbody.  If it isn't, move it to a child first.
///   2. Attach this script to the CHARACTER ROOT (the one with the Rigidbody).
///   3. Adjust zOffset in the Inspector if needed (default −0.6 works for
///      any mesh with a half-extent of ≤ 0.5 in Z).
/// </summary>
public class SpriteDepthOffset : MonoBehaviour
{
    [Tooltip("Local Z offset applied to the SpriteRenderer child. " +
             "Negative = toward the camera in Unity's default orientation. " +
             "Must be more negative than -(mesh half-depth), e.g. -0.6 for 1-unit cubes.")]
    [SerializeField] private float zOffset = -0.6f;

    private void Awake()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        if (sr == null)
        {
            Debug.LogWarning($"[SpriteDepthOffset] No SpriteRenderer found under {name}.", this);
            return;
        }

        if (sr.transform == transform)
        {
            Debug.LogWarning(
                $"[SpriteDepthOffset] The SpriteRenderer on '{name}' is on the ROOT GameObject, " +
                "not a child.  Move the SpriteRenderer (and Animator) to a child GameObject " +
                "so this script can safely offset its Z without affecting physics.", this);
            return;
        }

        Vector3 localPos    = sr.transform.localPosition;
        localPos.z          = zOffset;
        sr.transform.localPosition = localPos;
    }
}
