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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float animSmoothing = 10f;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");

    private Rigidbody _rb;
    private SphereCollider _col;
    private Vector3 _inputDir;
    private Vector2 _animDir;
    private Vector2 _lastFacingDir = new Vector2(0f, -1f);
    private Box _heldBox;

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
        Vector3 move = new Vector3(_inputDir.x * moveSpeed, _inputDir.y * moveSpeed, 0f);
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