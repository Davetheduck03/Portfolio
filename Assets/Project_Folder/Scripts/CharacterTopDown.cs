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
    [Tooltip("How far in front of the character the box floats while carried.")]
    [SerializeField] private float holdDistance = 1.2f;

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
    private Box  _pushedBox;
    private bool _isPushing;

    // Placement indicator
    private GameObject _indicatorGO;
    private MeshRenderer _indicatorMR;

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

        float effectiveSpeed = _isPushing ? moveSpeed * pushSpeedMultiplier : moveSpeed;
        Vector3 move = new Vector3(_inputDir.x * effectiveSpeed, _inputDir.y * effectiveSpeed, 0f);
        _rb.linearVelocity = move;
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

        _indicatorGO.transform.position = GetSnappedPlacementPosition();
        _indicatorGO.transform.localScale = indicatorSize;
    }

    // ---------------------------------------------------------------
    //  Box pushing
    // ---------------------------------------------------------------

    private void TryPushBox()
    {
        // Can't push while carrying a box or standing still
        if (_heldBox != null || _inputDir.sqrMagnitude < 0.01f)
        {
            ReleasePush();
            return;
        }

        // Only push along the 4 cardinal directions — diagonals are rejected
        Vector3 cardinal = ToCardinal(_inputDir);
        if (cardinal == Vector3.zero)
        {
            ReleasePush();
            return;
        }

        // Probe just outside the sphere's edge in the cardinal direction
        Vector3 colCenter = transform.position + (Vector3)_col.center;
        Vector3 probeOrigin = colCenter + cardinal * (_col.radius + 0.05f);

        Collider[] hits = Physics.OverlapSphere(probeOrigin, 0.25f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        Box found = null;
        foreach (Collider hit in hits)
        {
            if (hit == _col) continue;
            Box b = hit.GetComponent<Box>();
            if (b != null && !b.IsHeld) { found = b; break; }
        }

        if (found != null)
        {
            _pushedBox = found;
            _isPushing = true;

            // Move the box first so it clears the way for the character this frame
            Vector3 delta = cardinal * (moveSpeed * pushSpeedMultiplier * Time.fixedDeltaTime);
            _pushedBox.transform.position += delta;
            Physics.SyncTransforms();
        }
        else
        {
            ReleasePush();
        }
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
        if (_isPushing && _pushedBox != null && GridSystem.Instance != null)
            _pushedBox.transform.position = GridSystem.Instance.SnapToGrid(_pushedBox.transform.position);

        _pushedBox = null;
        _isPushing = false;
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
        Vector3 initialOffset = faceDir * holdDistance;
        _heldBox.PickUp(transform, initialOffset);
    }

    private void PlaceBox()
    {
        _heldBox.PutDown(GetSnappedPlacementPosition(), _col);
        _heldBox = null;
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