using UnityEngine;

/// <summary>
/// Keeps the SpriteRenderer child at the correct Z depth so it always renders
/// in front of 3D wall geometry in both camera modes, and corrects the X drift
/// that a perspective camera introduces whenever Z changes.
///
/// WHY X DRIFTS
///   Screen X = cameraSpaceX / cameraSpaceDepth * focalLength
///   When the sprite's world Z shifts (to stay in front of walls), its
///   camera-space depth changes, so the division produces a different screen X
///   even though nothing in the game logic moved the character left or right.
///   For a character at world X=5 moving from Z=0 to Z=-2.5, this drift is
///   clearly visible as a sideways pop.
///
/// THE CORRECTION
///   We want the sprite to project to the same screen X as the character root
///   (world Z=0).  Since the camera has no yaw, cameraSpaceX = worldX - camX,
///   and changing worldX does NOT affect cameraSpaceDepth.  The required local X
///   correction is therefore exact (no approximation):
///
///       localX = (charWorldX - camX) * (targetZ * camFwd.z) / charCamDepth
///
///   At charWorldX=0 (character on the camera centre axis) this is always 0,
///   so there is no correction needed or applied there.
///
/// PERSPECTIVE (TOP-DOWN) Z FORMULA
///   Higher world Y (top of map) → more-negative Z (closer to camera).
///   Lower world Y  (bottom)     → less-negative Z.
///
///       spriteZ = zAtBottom - yZSlope * worldY
///
/// Setup:
///   1. SpriteRenderer must be on a CHILD GameObject, not the root.
///   2. Attach this script to the CHARACTER ROOT (the one with the Rigidbody).
///   3. Assign Sprite Child and Cam in the Inspector.
/// </summary>
public class SpriteDepthOffset : MonoBehaviour
{
    [Header("Orthographic (2D) Mode")]
    [Tooltip("Fixed local Z offset in orthographic mode.\n" +
             "-0.6 puts the sprite just in front of 1-unit wall faces.")]
    [SerializeField] private float zOffsetOrtho = -0.6f;

    [Header("Perspective (Top-Down) Mode")]
    [Tooltip("Local Z when the character is at the bottom of the map (lowest world Y).\n" +
             "Less negative = further from camera.  E.g. -0.6.")]
    [SerializeField] private float zAtBottom = -0.6f;

    [Tooltip("How much more negative Z becomes per world unit the character moves UP the map.\n" +
             "E.g. slope 0.09 over 10 Y-units gives a -0.9 range:\n" +
             "zAtBottom=-0.6 → sprite reaches -1.5 at the top of the map.")]
    [SerializeField] private float yZSlope = 0.09f;

    [Tooltip("Local Z used when the character is inside a slotted box cell.\n" +
             "A positive value pushes the sprite further from the perspective camera\n" +
             "so it renders behind the box mesh (which sits at Z = 0).")]
    [SerializeField] private float zBehindObject = -1f;

    [Header("Target")]
    [Tooltip("The child GameObject whose local position will be adjusted.\n" +
             "Assign the sprite child directly. If left empty the script searches for\n" +
             "the first SpriteRenderer in any child as a fallback.")]
    [SerializeField] private Transform spriteChild;

    [Tooltip("The scene's main camera. Assign this directly to avoid silent failures.\n" +
             "If left empty the script tries Camera.main (requires MainCamera tag).")]
    [SerializeField] private Camera cam;

    // ---------------------------------------------------------------
    //  Private
    // ---------------------------------------------------------------

    private Transform _spriteTf;
    private Camera    _cam;
    private float     _baseLocalX; // original local X of the sprite child (before any correction)
    private bool      _forceBehind; // set by CharacterTopDown when inside a slotted-box cell

    // ---------------------------------------------------------------
    //  Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Called each frame by CharacterTopDown when the player is overlapping
    /// a slotted box cell.  Forces the sprite Z to <see cref="zBehindObject"/>
    /// so the sprite renders behind the box mesh in perspective mode.
    /// </summary>
    public void SetBehindObject(bool value) => _forceBehind = value;

    // ---------------------------------------------------------------
    //  Unity messages
    // ---------------------------------------------------------------

    private void Awake()
    {
        // Use the explicitly assigned child if provided.
        if (spriteChild != null)
        {
            if (spriteChild == transform)
            {
                Debug.LogWarning(
                    $"[SpriteDepthOffset] 'spriteChild' on '{name}' is set to the ROOT itself. " +
                    "Assign a child GameObject instead.", this);
                enabled = false;
                return;
            }
            _spriteTf = spriteChild;
        }
        else
        {
            // Fallback: find the first SpriteRenderer in any child.
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(includeInactive: true);

            if (sr == null)
            {
                Debug.LogWarning($"[SpriteDepthOffset] No SpriteRenderer found under {name}. " +
                                 "Assign the sprite child to the 'Sprite Child' field.", this);
                enabled = false;
                return;
            }

            if (sr.transform == transform)
            {
                Debug.LogWarning(
                    $"[SpriteDepthOffset] The SpriteRenderer on '{name}' is on the ROOT GameObject. " +
                    "Move it to a child so Z can be offset without affecting physics.", this);
                enabled = false;
                return;
            }

            _spriteTf = sr.transform;
        }

        // Store the sprite child's authored local X so the perspective correction
        // is applied on top of any intentional offset set in the editor.
        _baseLocalX = _spriteTf.localPosition.x;
    }

    private void LateUpdate()
    {
        if (_spriteTf == null) return;

        if (_cam == null) _cam = cam != null ? cam : Camera.main;
        if (_cam == null)
        {
            Debug.LogWarning($"[SpriteDepthOffset] No camera found on '{name}'. " +
                             "Assign the camera to the 'Cam' field in the Inspector.", this);
            enabled = false;
            return;
        }

        Vector3 lp = _spriteTf.localPosition;

        if (!_cam.orthographic)
        {
            // ── Z: push closer to camera at higher Y positions ────────────────
            float worldY = transform.position.y;
            float targetZ = _forceBehind
                ? zBehindObject
                : zAtBottom - yZSlope * worldY;

            // ── X: correct screen-space drift caused by the Z shift ───────────
            // Screen X = cameraSpaceX / cameraSpaceDepth.
            // Changing world Z changes cameraSpaceDepth, which changes screen X
            // for any object that isn't on the camera's centre axis.
            // We counter this by nudging local X so the projected screen X stays
            // exactly the same as if the sprite were at Z = 0.
            //
            // Derivation (exact for zero-yaw camera, camFwd.x == 0):
            //   localX = (charWorldX - camX) * (targetZ * camFwd.z) / charCamDepth
            float correctionX = 0f;
            Vector3 charWorld  = transform.position; // root is at world Z = 0
            Vector3 camPos     = _cam.transform.position;
            Vector3 camFwd     = _cam.transform.forward;
            float   charCamDepth = Vector3.Dot(charWorld - camPos, camFwd);

            if (Mathf.Abs(charCamDepth) > 0.001f)
                correctionX = (charWorld.x - camPos.x) * (targetZ * camFwd.z) / charCamDepth;

            lp.x = _baseLocalX + correctionX;
            lp.z = targetZ;
        }
        else
        {
            // Orthographic: flat Z offset, no X correction needed.
            lp.x = _baseLocalX;
            lp.z = zOffsetOrtho;
        }

        _spriteTf.localPosition = lp;
    }
}
