using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    // ----------------------------------------------------------------
    //  Inspector
    // ----------------------------------------------------------------

    [Header("Side-Scroller — Patrol")]
    [SerializeField] private float patrolSpeed = 2f;

    [Tooltip("Length of the horizontal ray cast ahead to detect walls.")]
    [SerializeField] private float wallCheckDistance = 0.6f;
    [Tooltip("Layers that count as a wall (usually the same as Ground Layers).")]
    [SerializeField] private LayerMask wallLayers;

    [Tooltip("How far ahead of the enemy the ledge-probe ray is placed.\n" +
             "Roughly half the enemy's width works well.")]
    [SerializeField] private float ledgeCheckDistance = 0.5f;
    [Tooltip("How far downward the ledge-probe ray travels.\n" +
             "A bit longer than the ground-ray length avoids false positives on small steps.")]
    [SerializeField] private float ledgeCheckDepth = 0.6f;

    [Header("Side-Scroller — Chase")]
    [SerializeField] private float chaseSpeed     = 4f;
    [Tooltip("2-D distance at which the enemy switches from patrol to chase.")]
    [SerializeField] private float detectionRange = 6f;

    [Header("Side-Scroller — Gravity")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float     fallMultiplier = 2.5f;

    [Header("Side-Scroller — Ground Rays")]
    [Tooltip("Number of rays spread across the base of the collider.")]
    [SerializeField] private int   groundRayCount        = 3;
    [Tooltip("How far below the origin point each ray travels.")]
    [SerializeField] private float groundRayLength       = 0.12f;
    [Tooltip("Half the horizontal spread of the rays. " +
             "Set to roughly half your enemy collider's width.")]
    [SerializeField] private float groundRayHalfWidth    = 0.3f;
    [Tooltip("Pulls the outermost rays inward so walls beside the enemy " +
             "don't count as ground.")]
    [SerializeField] private float groundRayInset        = 0.05f;
    [Tooltip("Lifts the ray origins up from the very bottom of the collider.")]
    [SerializeField] private float groundRayOriginOffset = 0.1f;

    [Header("Top-Down")]
    [SerializeField] private float topDownSpeed = 3f;
    [Tooltip("Snap movement to the nearest of 8 angles (N/NE/E/SE/S/SW/W/NW).\n" +
             "Gives a retro grid-movement feel.  Leave off for smooth pursuit.")]
    [SerializeField] private bool snapTo8Directions = false;

    [Header("Key Seeking")]
    [Tooltip("Assign a Key for this enemy to seek when the player is out of range.\n" +
             "The enemy walks toward it at patrol speed; collection happens automatically\n" +
             "when the enemy's collider enters the key's trigger.")]
    [SerializeField] private Key keyTarget;

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
    private float     _patrolDir        = 1f;   // +1 = right, -1 = left
    private float     _patrolFlipCooldown;      // prevents rapid double-flip on narrow platforms
    private Key       _activeKeyTarget;         // runtime ref, cleared when key is collected
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
        // Key seeking — subscribe to clear the reference when the key is collected
        // by anyone (player, another enemy, etc.) so we stop targeting a dead object.
        if (keyTarget != null)
        {
            _activeKeyTarget = keyTarget;
            _activeKeyTarget.OnKeyCollected += OnKeyCollectedByAnyone;
        }

        if (CharacterManager.Instance == null) return;
        CharacterManager.Instance.OnCharacterSwitched += HandleCharacterSwitched;
        RefreshMode(CharacterManager.Instance.ActiveCharacter);
    }

    void OnDestroy()
    {
        if (_activeKeyTarget != null)
            _activeKeyTarget.OnKeyCollected -= OnKeyCollectedByAnyone;

        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterSwitched -= HandleCharacterSwitched;
    }

    private void OnKeyCollectedByAnyone(Key _) => _activeKeyTarget = null;

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

        // Drive blend tree with the signed direction — the animator has
        // separate left/right clips so flipX is not needed and would double-mirror.
        if (moveX != 0f)
            _lastFacing = new Vector2(Mathf.Sign(moveX), 0f);

        SetAnimatorDirection(_lastFacing);
    }

    private float GetSidescrollerMoveX()
    {
        BaseCharacter player = CharacterManager.Instance?.ActiveCharacter;

        // ── 1. Chase player ────────────────────────────────────────────
        if (player != null)
        {
            float dx   = player.transform.position.x - transform.position.x;
            float dy   = player.transform.position.y - transform.position.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist <= detectionRange)
            {
                float chaseDir = Mathf.Sign(dx);

                // If a wall or ledge is blocking the direct path, stop here —
                // this is already the closest accessible point to the player.
                if (WallInDirection(chaseDir) || (_isGrounded && LedgeInDirection(chaseDir)))
                    return 0f;

                return chaseDir * chaseSpeed;
            }
        }

        // ── 2. Seek key ────────────────────────────────────────────────
        if (_activeKeyTarget != null)
        {
            float dx      = _activeKeyTarget.transform.position.x - transform.position.x;
            float seekDir = Mathf.Sign(dx);

            // Only advance if the path is clear; stop (and wait) if blocked.
            if (Mathf.Abs(dx) > 0.05f
                && !WallInDirection(seekDir)
                && !(_isGrounded && LedgeInDirection(seekDir)))
                return seekDir * patrolSpeed;

            return 0f;
        }

        // ── 3. Patrol: flip on wall OR ledge ──────────────────────────
        if (_patrolFlipCooldown > 0f)
        {
            _patrolFlipCooldown -= Time.fixedDeltaTime;
        }
        else if (WallAhead() || (_isGrounded && LedgeAhead()))
        {
            _patrolDir          = -_patrolDir;
            _patrolFlipCooldown = 0.25f;
        }

        return _patrolDir * patrolSpeed;
    }

    // ── Direction-aware obstacle checks ──────────────────────────────────

    /// <summary>Returns true when a wall collider is within wallCheckDistance
    /// in the given horizontal direction (+1 right, -1 left).</summary>
    private bool WallInDirection(float dir)
    {
        return Physics.Raycast(
            transform.position,
            new Vector3(dir, 0f, 0f),
            wallCheckDistance,
            wallLayers,
            QueryTriggerInteraction.Ignore);
    }

    /// <summary>Returns true when there is no ground beneath the foot-level
    /// probe point displaced in the given direction — i.e. a ledge is ahead.</summary>
    private bool LedgeInDirection(float dir)
    {
        float   feetOffsetY = groundRayHalfWidth - groundRayOriginOffset;
        Vector3 probeOrigin = transform.position
                            + new Vector3(dir * ledgeCheckDistance, -feetOffsetY, 0f);

        return !Physics.Raycast(
            probeOrigin,
            Vector3.down,
            groundRayLength + ledgeCheckDepth,
            groundLayers,
            QueryTriggerInteraction.Ignore);
    }

    // Convenience wrappers used by patrol (always check in the current patrol direction).
    private bool WallAhead()  => WallInDirection(_patrolDir);
    private bool LedgeAhead() => LedgeInDirection(_patrolDir);

    // ── Ground check & gravity ────────────────────────────────────────

    private void UpdateGroundCheck()
    {
        Vector3 bottom     = transform.position + Vector3.down * (groundRayHalfWidth - groundRayOriginOffset);
        float   halfSpread = Mathf.Max(0f, groundRayHalfWidth - groundRayInset);
        int     count      = Mathf.Max(1, groundRayCount);

        _isGrounded = false;

        for (int i = 0; i < count; i++)
        {
            float   t      = count > 1 ? (float)i / (count - 1) : 0.5f;
            float   xOff   = Mathf.Lerp(-halfSpread, halfSpread, t);
            Vector3 origin = bottom + new Vector3(xOff, 0f, 0f);

            if (Physics.Raycast(origin, Vector3.down, groundRayLength,
                                groundLayers, QueryTriggerInteraction.Ignore))
            {
                _isGrounded = true;
                break;
            }
        }
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
        float dir = Application.isPlaying ? _patrolDir : 1f;

        // Wall-check ray (yellow, horizontal)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, new Vector3(dir, 0f, 0f) * wallCheckDistance);

        // Ledge-probe ray (cyan) — originates at foot level, same Y as ground rays
        float   feetOffsetY = groundRayHalfWidth - groundRayOriginOffset;
        Vector3 probeOrigin = transform.position
                            + new Vector3(dir * ledgeCheckDistance, -feetOffsetY, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(probeOrigin, 0.05f);
        Gizmos.DrawRay(probeOrigin, Vector3.down * (groundRayLength + ledgeCheckDepth));

        // Detection range
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Ground rays
        {
            Vector3 bottom     = transform.position + Vector3.down * (groundRayHalfWidth - groundRayOriginOffset);
            float   halfSpread = Mathf.Max(0f, groundRayHalfWidth - groundRayInset);
            int     count      = Mathf.Max(1, groundRayCount);

            for (int i = 0; i < count; i++)
            {
                float   t      = count > 1 ? (float)i / (count - 1) : 0.5f;
                float   xOff   = Mathf.Lerp(-halfSpread, halfSpread, t);
                Vector3 origin = bottom + new Vector3(xOff, 0f, 0f);

                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawLine(origin, origin + Vector3.down * groundRayLength);
                Gizmos.DrawWireSphere(origin, 0.02f);
            }
        }
    }
#endif
}
