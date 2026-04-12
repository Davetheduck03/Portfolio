using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class TopDownCharacter : BaseCharacter
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Interaction")]
    [Tooltip("Radius in which the character can reach a box to pick it up.")]
    [SerializeField] private float pickupRange = 1.5f;
    [Tooltip("How far in front of the character to place the box when putting it down.")]
    [SerializeField] private float holdDistance = 1.2f;
    [Tooltip("Offset of the box root relative to the character while it is being carried.")]
    [SerializeField] private Vector3 carryOffset = Vector3.zero;

    [Header("Grid Placement")]
    [Tooltip("Size of the placement ghost cube — should match your box's scale.")]
    [SerializeField] private Vector3 indicatorSize = Vector3.one;
    [Tooltip("Optional: assign a semi-transparent material for the ghost. " +
             "If left empty a default green ghost is created automatically.")]
    [SerializeField] private Material indicatorMaterial;

    [Header("Box Pushing")]
    [Tooltip("Speed multiplier applied while pushing a box (0–1). " +
             "Both the character and box move at this fraction of moveSpeed " +
             "so they stay in sync.")]
    [SerializeField, Range(0.1f, 1f)] private float pushSpeedMultiplier = 0.6f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float animSmoothing = 10f;

    [Header("Audio")]
    [Tooltip("Sound played the moment a box is picked up.")]
    [SerializeField] private AudioClip pickupSound;
    [Tooltip("Sound played the moment a box is put down.")]
    [SerializeField] private AudioClip putDownSound;

    private static readonly int MoveX     = Animator.StringToHash("MoveX");
    private static readonly int MoveY     = Animator.StringToHash("MoveY");
    private static readonly int Speed     = Animator.StringToHash("Speed");
    private static readonly int IsPushing = Animator.StringToHash("IsPushing");

    private Rigidbody _rb;
    private SphereCollider _col;
    private Vector3 _inputDir;
    private Vector2 _animDir;
    private Vector2 _lastFacingDir = new Vector2(0f, -1f);
    private Box _heldBox;

    // Box pushing
    private readonly List<Box> _pushedChain = new List<Box>();
    private Vector3 _pushCardinal;
    private bool _isPushing;

    // Placement indicator
    private GameObject _indicatorGO;
    private MeshRenderer _indicatorMR;

    // Depth sorting — behind slotted boxes
    private SpriteDepthOffset _depthOffset;
    private Box[]             _allBoxes;

    // ---------------------------------------------------------------
    //  Unity messages
    // ---------------------------------------------------------------

    void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<SphereCollider>();
        _rb.useGravity = false;
        _rb.freezeRotation = true;
        _rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        _depthOffset = GetComponent<SpriteDepthOffset>();
        _allBoxes    = FindObjectsOfType<Box>(includeInactive: true);

        BuildPlacementIndicator();
    }

    // ---------------------------------------------------------------
    //  BaseCharacter overrides
    // ---------------------------------------------------------------

    public override void HandleInput()
    {
        float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
        _inputDir = new Vector3(x, y, 0f).normalized;

        if (animator != null)
        {
            bool moving = _inputDir.sqrMagnitude > 0.01f;

            if (moving)
                _lastFacingDir = new Vector2(_inputDir.x, _inputDir.y);

            _animDir = moving
                ? Vector2.Lerp(_animDir, _lastFacingDir, animSmoothing * Time.deltaTime)
                : _lastFacingDir;

            animator.SetFloat(MoveX, _animDir.x);
            animator.SetFloat(MoveY, _animDir.y);
            animator.SetFloat(Speed, moving ? 1f : 0f);
            animator.SetBool (IsPushing, _isPushing);
        }

        // ---- Box interaction ----
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (_heldBox != null) PlaceBox();
            else TryPickUpBox();
        }

        if (_heldBox != null)
        {
            Vector3 faceDir = new Vector3(_lastFacingDir.x, _lastFacingDir.y, 0f);
            _heldBox.SetHoldOffset(faceDir * holdDistance);
        }

        UpdatePlacementIndicator();

    }

    public override void Tick()
    {
        TryPushBox();
        UpdateBehindBoxDepth();

        float effectiveSpeed = _isPushing ? moveSpeed * pushSpeedMultiplier : moveSpeed;
        Vector3 move = new Vector3(_inputDir.x * effectiveSpeed, _inputDir.y * effectiveSpeed, 0f);
        _rb.linearVelocity = move;
    }

    /// <summary>
    /// Checks whether the player is standing inside any slotted box's XY cell.
    /// When true, SpriteDepthOffset is told to use <c>zBehindObject</c> so the
    /// sprite renders behind the (semi-transparent) slotted box mesh.
    /// </summary>
    private void UpdateBehindBoxDepth()
    {
        if (_depthOffset == null) return;

        float cellHalf = (GridSystem.Instance != null ? GridSystem.Instance.cellSize : 1f) * 0.5f;
        Vector3 pos = transform.position;

        bool behind = false;
        foreach (Box box in _allBoxes)
        {
            if (!box.IsSlotted) continue;
            Vector3 b = box.transform.position;
            if (Mathf.Abs(pos.x - b.x) < cellHalf &&
                Mathf.Abs(pos.y - b.y) < cellHalf)
            {
                behind = true;
                break;
            }
        }

        _depthOffset.SetBehindObject(behind);
    }

    public override void OnActivated()
    {
        base.OnActivated();
        _rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    public override void OnDeactivated()
    {
        if (_heldBox != null)
        {
            _heldBox.PutDown(GetSnappedPlacementPosition(), _col);
            _heldBox = null;
        }

        _inputDir = Vector3.zero;
        _animDir = Vector2.zero;

        ReleasePush();

        // Reset depth override so the sprite isn't stuck behind objects
        // when the player switches back to 2D mode.
        _depthOffset?.SetBehindObject(false);

        if (_indicatorGO != null) _indicatorGO.SetActive(false);

        base.OnDeactivated();
    }

    // ---------------------------------------------------------------
    //  Placement indicator
    // ---------------------------------------------------------------

    private Vector3 GetSnappedPlacementPosition()
    {
        Vector3 faceDir = new Vector3(_lastFacingDir.x, _lastFacingDir.y, 0f);
        Vector3 rawTarget = transform.position + faceDir * holdDistance;

        if (_heldBox != null)
            rawTarget.z = _heldBox.transform.position.z;

        return GridSystem.Instance != null
            ? GridSystem.Instance.SnapToGrid(rawTarget)
            : rawTarget;
    }

    private void UpdatePlacementIndicator()
    {
        if (_indicatorGO == null) return;

        bool holding = _heldBox != null;
        _indicatorGO.SetActive(holding);

        if (!holding) return;

        Vector3 pos = GetSnappedPlacementPosition();
        _indicatorGO.transform.position = new Vector3(pos.x, pos.y, -1f);
        _indicatorGO.transform.localScale = indicatorSize;

        // Tint red when the cell is blocked, green when valid.
        if (_indicatorMR != null)
        {
            _indicatorMR.material.color = IsValidPlacement(pos)
                ? new Color(0.2f, 1f, 0.3f, 0.35f)
                : new Color(1f, 0.15f, 0.15f, 0.35f);
        }
    }

    /// <summary>Returns true when <paramref name="pos"/> is inside the grid
    /// bounds, not occupied by a solid wall, and not occupied by a character.</summary>
    private bool IsValidPlacement(Vector3 pos)
    {
        if (GridSystem.Instance != null)
        {
            Vector2Int cell = GridSystem.Instance.WorldToCell(pos);
            if (cell.x < 0 || cell.x >= GridSystem.Instance.gridWidth ||
                cell.y < 0 || cell.y >= GridSystem.Instance.gridHeight)
                return false;
        }

        if (SolidWallAt(pos)) return false;

        // Prevent placing a box on top of any character (colliders may be disabled).
        float checkRadius = GridSystem.Instance != null ? GridSystem.Instance.cellSize * 0.5f : 0.5f;
        if (CharacterManager.Instance != null &&
            CharacterManager.Instance.IsOccupiedByCharacter(pos, checkRadius))
            return false;

        return true;
    }

    // ---------------------------------------------------------------
    //  Box pushing
    // ---------------------------------------------------------------

    private void TryPushBox()
    {
        // Can't push while carrying a box or standing still.
        if (_heldBox != null || _inputDir.sqrMagnitude < 0.01f)
        {
            ReleasePush();
            return;
        }

        // Only push along the 4 cardinal directions — diagonals are rejected.
        Vector3 cardinal = ToCardinal(_inputDir);
        if (cardinal == Vector3.zero)
        {
            ReleasePush();
            return;
        }

        float cellSize = GridSystem.Instance != null ? GridSystem.Instance.cellSize : 1f;

        // ── 1. Find the first box directly in front of the character ──
        Vector3 colCenter   = transform.position + (Vector3)_col.center;
        Vector3 probeOrigin = colCenter + cardinal * (_col.radius + 0.05f);

        Box first = BoxAt(probeOrigin);
        if (first == null)
        {
            ReleasePush();
            return;
        }

        // ── 2. Walk the chain ──
        _pushedChain.Clear();
        _pushedChain.Add(first);

        // Cap prevents infinite loops if boxes snap to the same cell.
        const int maxChain = 64;
        int chainCountBefore = _pushedChain.Count;
        while (_pushedChain.Count < maxChain)
        {
            Box     chainLast    = _pushedChain[_pushedChain.Count - 1];
            Vector3 chainLastPos = chainLast.transform.position;

            Box next = BoxAt(chainLastPos + cardinal * cellSize);
            if (next == null || _pushedChain.Contains(next)) break;

            next.transform.position = chainLastPos + cardinal * cellSize;
            _pushedChain.Add(next);
        }

        if (_pushedChain.Count > chainCountBefore)
            Physics.SyncTransforms();

        Box     leadBox     = _pushedChain[_pushedChain.Count - 1];
        Vector3 snappedLead = GridSystem.Instance != null
            ? GridSystem.Instance.SnapToGrid(leadBox.transform.position)
            : leadBox.transform.position;
        Vector3 frontTarget = snappedLead + cardinal * cellSize;

        // Out-of-bounds check
        if (GridSystem.Instance != null)
        {
            Vector2Int cell = GridSystem.Instance.WorldToCell(frontTarget);
            if (cell.x < 0 || cell.x >= GridSystem.Instance.gridWidth ||
                cell.y < 0 || cell.y >= GridSystem.Instance.gridHeight)
            {
                ReleasePush();
                return;
            }
        }

        // Wall check
        if (SolidWallAt(frontTarget))
        {
            ReleasePush();
            return;
        }

        // ── 4. Move every box in the chain by the same delta ──
        _pushCardinal = cardinal;
        _isPushing    = true;
        Vector3 delta = cardinal * (moveSpeed * pushSpeedMultiplier * Time.fixedDeltaTime);
        foreach (Box b in _pushedChain)
            b.transform.position += delta;

        Physics.SyncTransforms();
    }

    /// <summary>Returns a free (not held, not slotted) Box near <paramref name="pos"/>,
    /// or null if none found.</summary>
    private Box BoxAt(Vector3 pos)
    {
        Collider[] hits = Physics.OverlapSphere(pos, 0.25f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit == _col) continue;
            Box b = hit.GetComponent<Box>();
            if (b != null && !b.IsHeld && !b.IsSlotted) return b;
        }
        return null;
    }

    /// <summary>Returns true if there is a solid wall collider at <paramref name="pos"/>
    /// that should block pushing or placement. Boxes and hole blockers are NOT treated as walls.</summary>
    private bool SolidWallAt(Vector3 pos)
    {
        // OverlapBox with a large Z extent catches wall colliders at any depth,
        // and XY half-extents just under half a cell avoid false hits on adjacent cells.
        float half = GridSystem.Instance != null ? GridSystem.Instance.cellSize * 0.45f : 0.45f;
        Collider[] hits = Physics.OverlapBox(
            pos,
            new Vector3(half, half, 5f),
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == _col) continue;
            if (hit.GetComponent<Box>() != null) continue;          // it's a box — part of chain
            if (hit.GetComponentInParent<Hole>() != null) continue; // it's a hole blocker — passable
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the nearest cardinal direction (up/down/left/right) only when the
    /// input is clearly aligned to one axis.  Returns Vector3.zero for diagonals.
    /// </summary>
    private static Vector3 ToCardinal(Vector3 dir)
    {
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);

        // If both axes are significant the player is pressing a diagonal — reject it
        if (ax > 0.1f && ay > 0.1f) return Vector3.zero;

        if (ax >= ay)
            return new Vector3(Mathf.Sign(dir.x), 0f, 0f);
        else
            return new Vector3(0f, Mathf.Sign(dir.y), 0f);
    }

    private void ReleasePush()
    {
        if (_isPushing && _pushedChain.Count > 0)
        {
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.cellSize : 1f;
            SnapChainToGrid(_pushCardinal, cellSize);
        }

        _pushedChain.Clear();
        _isPushing = false;
    }

    /// <summary>
    /// Snaps every box in the pushed chain to the grid, working back from the
    /// lead box so they land in a perfectly-spaced line.
    /// </summary>
    private void SnapChainToGrid(Vector3 cardinal, float cellSize)
    {
        if (_pushedChain.Count == 0) return;

        int last = _pushedChain.Count - 1;

        // Snap the lead (front-most) box first.
        Vector3 frontSnapped = GridSystem.Instance != null
            ? GridSystem.Instance.SnapToGrid(_pushedChain[last].transform.position)
            : _pushedChain[last].transform.position;

        _pushedChain[last].transform.position = frontSnapped;

        // Place every box behind it exactly one cell-width back from the next.
        for (int i = last - 1; i >= 0; i--)
        {
            int stepsBack = last - i;
            _pushedChain[i].transform.position = frontSnapped - cardinal * (stepsBack * cellSize);
        }

        Physics.SyncTransforms();
    }

    // ---------------------------------------------------------------
    //  Box interaction
    // ---------------------------------------------------------------

    private void TryPickUpBox()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, pickupRange);

        Box closest = null;
        float closestSq = float.MaxValue;

        foreach (Collider col in nearby)
        {
            Box b = col.GetComponent<Box>();
            if (b == null || b.IsHeld) continue;

            float dsq = (b.transform.position - transform.position).sqrMagnitude;
            if (dsq < closestSq) { closestSq = dsq; closest = b; }
        }

        if (closest == null) return;

        _heldBox = closest;
        Vector3 faceDir = new Vector3(_lastFacingDir.x, _lastFacingDir.y, 0f);
        _heldBox.PickUp(transform, faceDir * holdDistance);
        PlayOneShot(pickupSound);
    }

    private void PlaceBox()
    {
        Vector3 target = GetSnappedPlacementPosition();
        if (!IsValidPlacement(target)) return;

        _heldBox.PutDown(target, _col);
        _heldBox = null;
        PlayOneShot(putDownSound);
    }

    // ---------------------------------------------------------------
    //  Indicator setup
    // ---------------------------------------------------------------

    private void BuildPlacementIndicator()
    {
        _indicatorGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _indicatorGO.name = "BoxPlacementIndicator";
        _indicatorGO.transform.SetParent(null);
        _indicatorGO.transform.localScale = indicatorSize;

        Destroy(_indicatorGO.GetComponent<Collider>());

        _indicatorMR = _indicatorGO.GetComponent<MeshRenderer>();

        if (indicatorMaterial != null)
        {
            _indicatorMR.material = indicatorMaterial;
        }
        else
        {
            _indicatorMR.material = CreateDefaultIndicatorMaterial();
        }

        _indicatorGO.SetActive(false);
    }

    private static Material CreateDefaultIndicatorMaterial()
    {
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpUnlit != null)
        {
            var mat = new Material(urpUnlit);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            return mat;
        }

        Shader fallback = Shader.Find("Sprites/Default");
        if (fallback != null)
        {
            var mat = new Material(fallback);
            mat.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            return mat;
        }

        return new Material(Shader.Find("Standard"))
        {
            color = new Color(0.2f, 1f, 0.3f, 0.35f)
        };
    }
}