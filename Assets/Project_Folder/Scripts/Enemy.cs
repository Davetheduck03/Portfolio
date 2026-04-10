using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Side-Scroller — Patrol")]
    [SerializeField] private float patrolSpeed      = 2f;
    [Tooltip("Length of the horizontal ray cast ahead to detect walls.\n" +
             "Should be roughly half the enemy's width + a small margin.")]
    [SerializeField] private float wallCheckDistance = 0.6f;
    [Tooltip("Layers that count as a wall for the direction-flip check.\n" +
             "Usually the same mask as the player's Ground Layers.")]
    [SerializeField] private LayerMask wallLayers;

    [Header("Side-Scroller — Chase")]
    [SerializeField] private float chaseSpeed     = 4f;
    [Tooltip("2-D distance at which the enemy switches from patrol to chase.")]
    [SerializeField] private float detectionRange = 6f;

    [Header("Side-Scroller — Gravity")]
    [Tooltip("Empty child transform placed at the base of the collider (same setup as the player).")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float     groundCheckRadius = 0.15f;
    [SerializeField] private float     fallMultiplier    = 2.5f;

    [Header("Top-Down")]
    [SerializeField] private float topDownSpeed = 3f;
    [Tooltip("Snap movement to the nearest of 8 angles (N/NE/E/SE/S/SW/W/NW).\n" +
             "Gives a retro grid-movement feel.  Leave off for smooth pursuit.")]
    [SerializeField] private bool snapTo8Directions = false;

    [Header("Kill")]
    [Tooltip("Fired once each time the enemy first touches the active player.\n" +
             "Wire to your respawn / game-over method here.")]
    [SerializeField] private UnityEvent onPlayerKilled;

    [Tooltip("Fired once when this enemy is killed (e.g. by a spike).\n" +
             "Optional — wire to a score counter, sound effect, etc.")]
    [SerializeField] private UnityEvent onEnemyKilled;

    [Header("Visuals")]
    [Tooltip("Optional — used to flip the sprite toward the direction of travel.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Animation")]
    [Tooltip("Animator driven by the MoveX / MoveY blend tree.\n" +
             "Assign the EnemyAnimator controller created via Tools > Enemy > Create Enemy Animator.")]
    [SerializeField] private Animator animator;

    // ----------------------------------------------------------------
    //  Private state
    // ----------------------------------------------------------------

    private enum Mode { Sidescroller, TopDown }

    private static readonly int ParamMoveX = Animator.StringToHash("MoveX");
    private static readonly int ParamMoveY = Animator.StringToHash("MoveY");

    private Rigidbody _rb;
    private Mode      _mode;
    private float     _velocityY;
    private bool      _isGrounded;
    private float     _patrolDir    = 1f;   // +1 = right, -1 = left
    private Vector2   _lastFacing   = Vector2.down; // preserves facing when stopped
    private bool      _contactKillFired;    // prevents the kill event firing every frame
    private bool      _dead;               // set on Die() so it only runs once

    // ----------------------------------------------------------------
    //  Unity messages
    // ----------------------------------------------------------------

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity     = false;
        _rb.freezeRotation = true;
        _rb.constraints    = RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotation;
    }

    void Start()
    {
        if (CharacterManager.Instance == null) return;

        CharacterManager.Instance.OnCharacterSwitched += HandleCharacterSwitched;
        RefreshMode(CharacterManager.Instance.ActiveCharacter);
    }

    void OnDestroy()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterSwitched -= HandleCharacterSwitched;
    }

    void FixedUpdate()
    {
        if (_mode == Mode.Sidescroller)
            TickSidescroller();
        else
            TickTopDown();
    }

    void OnCollisionEnter(Collision col)  => TryKillPlayer(col.gameObject);
    void OnCollisionExit (Collision col)
    {
        if (col.gameObject.GetComponent<BaseCharacter>() != null)
            _contactKillFired = false;
    }

    // ----------------------------------------------------------------
    //  Mode switching
    // ----------------------------------------------------------------

    private void HandleCharacterSwitched(BaseCharacter newChar) => RefreshMode(newChar);

    private void RefreshMode(BaseCharacter activeChar)
    {
        _mode = activeChar is SidescrollerCharacter ? Mode.Sidescroller : Mode.TopDown;

        if (_mode == Mode.TopDown)
        {
            _velocityY         = 0f;
            _rb.linearVelocity = Vector3.zero;
        }
    }

    // ----------------------------------------------------------------
    //  Side-scroller tick
    // ----------------------------------------------------------------

    private void TickSidescroller()
    {
        UpdateGroundCheck();
        ApplyGravity();

        float moveX = GetSidescrollerMoveX();

        _rb.linearVelocity = new Vector3(moveX, _velocityY, 0f);

        if (spriteRenderer != null && moveX != 0f)
            spriteRenderer.flipX = moveX < 0f;

        // Drive blend tree — side-scroller only moves horizontally
        if (moveX != 0f)
            _lastFacing = new Vector2(Mathf.Sign(moveX), 0f);

        SetAnimatorDirection(_lastFacing);
    }

    private float GetSidescrollerMoveX()
    {
        BaseCharacter player = CharacterManager.Instance?.ActiveCharacter;

        // ── Chase ──────────────────────────────────────────────────────
        if (player != null)
        {
            float dx   = player.transform.position.x - transform.position.x;
            float dy   = player.transform.position.y - transform.position.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist <= detectionRange)
                return Mathf.Sign(dx) * chaseSpeed;
        }

        // ── Patrol: wall-bounce ────────────────────────────────────────
        if (WallAhead())
            _patrolDir = -_patrolDir;

        return _patrolDir * patrolSpeed;
    }

    /// <summary>
    /// Casts a short horizontal ray in the current patrol direction.
    /// Returns true when a wall is close enough to warrant reversing.
    /// </summary>
    private bool WallAhead()
    {
        Vector3 origin = transform.position;
        Vector3 dir    = new Vector3(_patrolDir, 0f, 0f);

        return Physics.Raycast(
            origin,
            dir,
            wallCheckDistance,
            wallLayers,
            QueryTriggerInteraction.Ignore);
    }

    // ── Ground check & gravity ────────────────────────────────────────

    private void UpdateGroundCheck()
    {
        if (groundCheck == null) return;

        _isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void ApplyGravity()
    {
        if (_isGrounded && _velocityY <= 0f)
        {
            _velocityY = -0.5f;
            return;
        }

        float mult = _velocityY < 0f ? fallMultiplier : 1f;
        _velocityY -= GravityService.Gravity * mult * Time.fixedDeltaTime;
    }

    // ----------------------------------------------------------------
    //  Top-down tick
    // ----------------------------------------------------------------

    private void TickTopDown()
    {
        BaseCharacter player = CharacterManager.Instance?.ActiveCharacter;
        if (player == null) return;

        Vector2 toPlayer = new Vector2(
            player.transform.position.x - transform.position.x,
            player.transform.position.y - transform.position.y);

        if (toPlayer.sqrMagnitude < 0.001f) return;

        Vector2 dir = toPlayer.normalized;

        if (snapTo8Directions)
            dir = SnapTo8(dir);

        _rb.linearVelocity = new Vector3(dir.x * topDownSpeed, dir.y * topDownSpeed, 0f);

        // Drive blend tree with the movement direction
        _lastFacing = dir;
        SetAnimatorDirection(_lastFacing);
    }

    private static Vector2 SnapTo8(Vector2 dir)
    {
        float angle   = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        float rad     = snapped * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void SetAnimatorDirection(Vector2 dir)
    {
        if (animator == null) return;
        animator.SetFloat(ParamMoveX, dir.x);
        animator.SetFloat(ParamMoveY, dir.y);
    }

    // ----------------------------------------------------------------
    //  Kill / Death
    // ----------------------------------------------------------------

    private void TryKillPlayer(GameObject obj)
    {
        if (_contactKillFired) return;

        BaseCharacter character = obj.GetComponent<BaseCharacter>();
        if (character == null || !character.IsActive) return;

        _contactKillFired = true;
        onPlayerKilled.Invoke();
    }

    /// <summary>
    /// Call this to kill the enemy (e.g. from a SpikeBlock).
    /// Fires <see cref="onEnemyKilled"/> then destroys the GameObject.
    /// </summary>
    public void Die()
    {
        if (_dead) return;
        _dead = true;

        onEnemyKilled.Invoke();
        Destroy(gameObject);
    }

    // ----------------------------------------------------------------
    //  Editor helpers
    // ----------------------------------------------------------------

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Wall-check ray
        Gizmos.color = Color.yellow;
        Vector3 rayDir = new Vector3(_patrolDir, 0f, 0f);
        Gizmos.DrawRay(transform.position, rayDir * wallCheckDistance);

        // Detection range
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Ground check
        if (groundCheck != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
#endif
}
