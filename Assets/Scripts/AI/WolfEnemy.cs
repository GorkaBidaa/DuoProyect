using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class WolfEnemy : MonoBehaviour, ITrapTarget
{
    [Header("References")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private SpriteRenderer visuals;
    [SerializeField] private LayerMask solidLayers;

    [Header("Patrol and detection")]
    [SerializeField] private Transform patrolCenter;
    [SerializeField, Min(1f)] private float patrolWidth = 10f;
    [SerializeField, Min(1f)] private float chaseAreaMultiplier = 2.5f;
    [SerializeField, Min(0.1f)] private float detectionRange = 10f;
    [SerializeField, Min(0.1f)] private float maximumTrackingHeight = 4f;
    [SerializeField, Min(0f)] private float patrolPause = 0.6f;

    [Header("Ground movement")]
    [SerializeField, Min(0.1f)] private float patrolSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float chaseSpeed = 5f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 3f;
    [SerializeField, Min(0.1f)] private float gravityScale = 2f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.12f;
    [SerializeField, Min(0.01f)] private float ledgeLookAhead = 0.08f;
    [SerializeField, Min(0.01f)] private float maximumSafeDrop = 0.4f;

    [Header("Lunge")]
    [SerializeField, Min(0.1f)] private float attackRange = 1.8f;
    [SerializeField, Min(0.1f)] private float attackHeightTolerance = 1f;
    [SerializeField, Min(0.1f)] private float windupDuration = 0.65f;
    [SerializeField, Min(0f)] private float aimLockDuration = 0.15f;
    [SerializeField, Min(0.1f)] private float lungeSpeed = 12f;
    [SerializeField, Min(0.1f)] private float lungeDuration = 0.5f;
    [SerializeField, Min(1)] private int damage = 25;
    [SerializeField, Min(0f)] private float attackCooldown = 2.5f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float backAwayDistance = 2f;

    [Header("Item responses")]
    [SerializeField, Min(0.1f)] private float investigateSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float lureArrivalDistance = 0.6f;
    [SerializeField, Min(0.1f)] private float maximumLureHeightDifference = 1.25f;
    [SerializeField, Min(0.1f)] private float fleeSpeed = 6f;
    [SerializeField, Min(0.1f)] private float repellantClearance = 1.5f;

    [Header("Gloom reward")]
    [SerializeField] private MagnetResourcePickup gloomDropPrefab;
    [SerializeField] private ResourceData gloomResource;
    [SerializeField, Min(0)] private int gloomDropped = 3;

    [Header("Animation")]
    [SerializeField] private EnemyAnimationDriver animationDriver;
    [SerializeField] private bool useDebugStateTint;
    private EnemyAttackContact attackContact;

    private enum State
    {
        Patrolling, Chasing, Windup, Lunging, Recovering,
        BackingAway, WaitingCooldown, Returning, Investigating, Repelled
    }

    private State state;
    private Rigidbody2D body;
    private CapsuleCollider2D shape;
    private StaminaSystem playerStamina;
    private ContactFilter2D groundFilter;
    private Vector2 homePosition;
    private Color originalColor;
    private float stateTime;
    private float patrolWait;
    private float nextAttackTime;
    private float retreatTargetX;
    private int patrolDirection = 1;
    private int lungeDirection = 1;
    private bool aimLocked;
    private bool grounded;
    private bool dead;
    private EnemyLure activeLure;
    private EnemyRepellantUse repellantSource;
    private readonly HashSet<EnemyLure> unreachableLures = new HashSet<EnemyLure>();
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[1];

    public bool IsAlive => !dead && isActiveAndEnabled;
    private bool HasPlayer => player != null && player.isActiveAndEnabled &&
        (playerStamina == null || !playerStamina.IsDead);
    private float CenterX => patrolCenter != null ? patrolCenter.position.x : homePosition.x;
    private float PatrolMin => CenterX - patrolWidth * 0.5f;
    private float PatrolMax => CenterX + patrolWidth * 0.5f;
    private float ChaseMin => CenterX - patrolWidth * Mathf.Max(1f, chaseAreaMultiplier) * 0.5f;
    private float ChaseMax => CenterX + patrolWidth * Mathf.Max(1f, chaseAreaMultiplier) * 0.5f;
    private bool InsideChase(float x) => x >= ChaseMin && x <= ChaseMax;

    // Called by EnemySpawnPoint while the clone is inactive, before Awake runs.
    public void ConfigureSpawn(PlayerHealth target, Transform center, bool overrideArea, Vector2 area)
    {
        player = target;
        patrolCenter = center;
        if (overrideArea) patrolWidth = Mathf.Max(0.1f, area.x);
        enabled = true;
    }

    private void Awake()
    {
        EnemyAudio audio = GetComponent<EnemyAudio>();
        if (audio != null && player != null) audio.SetListener(player.transform);
        body = GetComponent<Rigidbody2D>();
        if (!animationDriver) animationDriver = GetComponentInChildren<EnemyAnimationDriver>(true);
        shape = GetComponent<CapsuleCollider2D>();
        homePosition = body.position;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;
        body.linearDamping = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        shape.isTrigger = false;
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(solidLayers);
        groundFilter.useTriggers = false;

        if (visuals == null)
            visuals = GetComponentInChildren<SpriteRenderer>();
        if (visuals != null)
            originalColor = visuals.color;
        if (player != null)
            playerStamina = player.GetComponentInParent<StaminaSystem>();

        if (player == null || solidLayers.value == 0)
        {
            Debug.LogError("Assign WolfEnemy's Player and solid terrain layers.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (dead)
            return;

        if (attackContact == null)
        {
            attackContact = gameObject.AddComponent<EnemyAttackContact>();
            attackContact.Setup(player, visuals);
        }
        attackContact.CheckHit(damage, new Vector2(lungeDirection, 0f));
        stateTime += Time.fixedDeltaTime;
        grounded = CheckGrounded();
        if (!CheckRepellant())
            CheckLure();

        switch (state)
        {
            case State.Patrolling:
                TickPatrol();
                break;
            case State.Chasing:
                TickChase();
                break;
            case State.Windup:
                StopHorizontal();
                if (!CanPursuePlayer())
                    ChangeState(State.Returning);
                else
                {
                    if (!aimLocked)
                    {
                        lungeDirection = DirectionTo(player.transform.position.x);
                        Face(lungeDirection);
                        if (stateTime >= Mathf.Max(0f, windupDuration - aimLockDuration))
                        {
                            aimLocked = true;
                            if (useDebugStateTint && visuals != null)
                                visuals.color = new Color(1f, 0.5f, 0f);
                        }
                    }
                    if (stateTime >= windupDuration && grounded)
                        ChangeState(State.Lunging);
                }
                break;
            case State.Lunging:
                if (stateTime >= lungeDuration || !TryWalk(lungeDirection, lungeSpeed))
                    ChangeState(State.Recovering);
                break;
            case State.Recovering:
                StopHorizontal();
                if (!CanPursuePlayer())
                    ChangeState(State.Returning);
                else if (stateTime >= recoveryDuration)
                    ChangeState(State.BackingAway);
                break;
            case State.BackingAway:
                if (!CanPursuePlayer())
                    ChangeState(State.Returning);
                else if (Mathf.Abs(retreatTargetX - body.position.x) <= 0.15f ||
                    !TryWalk(DirectionTo(retreatTargetX), returnSpeed))
                    ChangeState(State.WaitingCooldown);
                break;
            case State.WaitingCooldown:
                StopHorizontal();
                if (!CanPursuePlayer())
                    ChangeState(State.Returning);
                else if (Time.time >= nextAttackTime)
                    ChangeState(State.Chasing);
                break;
            case State.Returning:
                if (body.position.x >= PatrolMin && body.position.x <= PatrolMax)
                    ChangeState(State.Patrolling);
                else
                    TryWalk(DirectionTo(CenterX), returnSpeed);
                break;
            case State.Investigating:
                TickInvestigate();
                break;
            case State.Repelled:
                TickRepelled();
                break;
        }
        RefreshAnimation();
    }

    private void TickPatrol()
    {
        if (body.position.x < PatrolMin || body.position.x > PatrolMax)
        {
            ChangeState(State.Returning);
            return;
        }
        if (CanPursuePlayer() && Vector2.Distance(body.position, player.transform.position) <= detectionRange && HasLineOfSight())
        {
            ChangeState(State.Chasing);
            return;
        }
        if (!grounded)
        {
            StopHorizontal();
            return;
        }
        if (patrolWait > 0f)
        {
            patrolWait -= Time.fixedDeltaTime;
            StopHorizontal();
            return;
        }

        float target = patrolDirection > 0 ? PatrolMax : PatrolMin;
        if (Mathf.Abs(target - body.position.x) <= 0.15f || !TryWalk(patrolDirection, patrolSpeed))
        {
            patrolDirection = -patrolDirection;
            patrolWait = patrolPause;
            StopHorizontal();
        }
    }

    private void TickChase()
    {
        if (!CanPursuePlayer())
        {
            ChangeState(State.Returning);
            return;
        }
        float delta = player.transform.position.x - body.position.x;
        bool canAttack = Mathf.Abs(delta) <= attackRange &&
            Mathf.Abs(player.transform.position.y - body.position.y) <= attackHeightTolerance && HasLineOfSight();
        if (grounded && canAttack)
        {
            ChangeState(Time.time >= nextAttackTime ? State.Windup : State.WaitingCooldown);
            return;
        }
        if (Mathf.Abs(delta) > 0.15f)
            TryWalk(delta > 0f ? 1 : -1, chaseSpeed);
        else
            StopHorizontal();
    }

    private bool CheckGrounded()
    {
        int count = shape.Cast(Vector2.down, groundFilter, castHits, groundProbeDistance, true);
        for (int i = 0; i < count; i++)
            if (castHits[i].normal.y >= 0.5f)
                return true;
        return false;
    }

    private bool TryWalk(int direction, float speed)
    {
        float nextX = body.position.x + direction * speed * Time.fixedDeltaTime;
        bool movingBackInside = !InsideChase(body.position.x) &&
            Mathf.Abs(nextX - CenterX) < Mathf.Abs(body.position.x - CenterX);
        if (!grounded || (!InsideChase(nextX) && !movingBackInside))
        {
            StopHorizontal();
            return false;
        }
        float step = speed * Time.fixedDeltaTime;
        int count = shape.Cast(Vector2.right * direction, groundFilter, castHits, step + 0.04f, true);
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].normal.x * direction < -0.5f && castHits[i].normal.y < 0.7f)
            {
                StopHorizontal();
                return false;
            }
        }

        Bounds bounds = shape.bounds;
        Vector2 probe = new Vector2(
            bounds.center.x + direction * (bounds.extents.x + step + ledgeLookAhead),
            bounds.min.y + 0.25f);
        if (Physics2D.Raycast(probe, Vector2.down, groundFilter, groundHits, 0.25f + maximumSafeDrop) == 0 ||
            groundHits[0].normal.y < 0.5f)
        {
            StopHorizontal();
            return false;
        }

        Face(direction);
        body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
        return true;
    }

    private int DirectionTo(float x)
    {
        float delta = x - body.position.x;
        return Mathf.Abs(delta) > 0.01f ? (delta > 0f ? 1 : -1) : lungeDirection;
    }

    private void StopHorizontal()
    {
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void Face(int direction)
    {
        if (visuals != null)
            visuals.flipX = direction < 0;
    }

    private bool CanPursuePlayer()
    {
        return HasPlayer && !EnemyRepellantUse.Protects(player.transform) &&
            InsideChase(body.position.x) && InsideChase(player.transform.position.x) &&
            Mathf.Abs(player.transform.position.y - body.position.y) <= maximumTrackingHeight;
    }

    private bool HasLineOfSight()
    {
        return HasPlayer && Physics2D.Linecast(body.position, player.transform.position, solidLayers).collider == null;
    }

    private bool CheckRepellant()
    {
        if (state == State.Lunging)
            return false;
        EnemyRepellantUse source = repellantSource != null &&
            repellantSource.CanAffect(body.position, repellantClearance + detectionRange)
            ? repellantSource : EnemyRepellantUse.FindNearest(body.position);
        if (source == null)
        {
            if (state == State.Repelled)
            {
                repellantSource = null;
                ChangeState(State.Returning);
            }
            return false;
        }
        if (state != State.Repelled || source != repellantSource)
        {
            repellantSource = source;
            activeLure = null;
            ChangeState(State.Repelled);
        }
        return true;
    }

    private void TickRepelled()
    {
        if (repellantSource == null || !repellantSource.IsActive)
        {
            ChangeState(State.Returning);
            return;
        }
        if (Vector2.Distance(body.position, repellantSource.Position) >= repellantSource.Radius + repellantClearance)
            StopHorizontal();
        else
        {
            int away = body.position.x >= repellantSource.Position.x ? 1 : -1;
            TryWalk(away, fleeSpeed);
        }
    }

    private bool ValidLure(EnemyLure lure)
    {
        return lure != null && lure.CanAttract(body.position) && InsideChase(lure.Position.x) &&
            Mathf.Abs(lure.Position.y - body.position.y) <= maximumLureHeightDifference && !unreachableLures.Contains(lure);
    }

    private void CheckLure()
    {
        if (state == State.Windup || state == State.Lunging || state == State.Recovering || state == State.Repelled)
            return;
        unreachableLures.RemoveWhere(lure => lure == null || !lure.IsAvailable);
        Rect allowed = new Rect(ChaseMin, body.position.y - maximumLureHeightDifference,
            ChaseMax - ChaseMin, maximumLureHeightDifference * 2f);
        EnemyLure chosen = ValidLure(activeLure) ? activeLure : EnemyLure.FindNearest(body.position, allowed, unreachableLures);
        if (chosen == null)
        {
            if (state == State.Investigating)
            {
                activeLure = null;
                ChangeState(State.Returning);
            }
            return;
        }
        if (state != State.Investigating || chosen != activeLure)
        {
            activeLure = chosen;
            ChangeState(State.Investigating);
        }
    }

    private void TickInvestigate()
    {
        if (!ValidLure(activeLure))
        {
            activeLure = null;
            ChangeState(State.Returning);
            return;
        }
        if (Mathf.Abs(activeLure.Position.x - body.position.x) <= lureArrivalDistance)
        {
            StopHorizontal();
            return;
        }
        if (!grounded)
        {
            StopHorizontal();
            return;
        }
        if (!TryWalk(DirectionTo(activeLure.Position.x), investigateSpeed))
        {
            unreachableLures.Add(activeLure);
            activeLure = null;
            ChangeState(State.Returning);
        }
    }

    private void ChangeState(State next)
    {
        EnemyAudio audio = GetComponent<EnemyAudio>();
        if (audio != null)
        {
            if (player != null) audio.SetListener(player.transform);
            if (state == State.Windup && next != State.Windup && next != State.Lunging) audio.StopCue();
            audio.SetQuietWings(next == State.Windup || next == State.Lunging);
            if (next != state)
            {
                if (next == State.Chasing &&
                    (state == State.Patrolling || state == State.Returning || state == State.Investigating)) audio.Detection();
                if (next == State.Windup) audio.Windup();
                if (next == State.Lunging) audio.Attack();
            }
        }
        if (state == State.Lunging && next == State.Recovering)
            nextAttackTime = Time.time + attackCooldown;
        if (next == State.Lunging && attackContact != null) attackContact.BeginAttack();
        if (state == State.Lunging && next != State.Lunging && attackContact != null) attackContact.EndAttack();
        state = next;
        stateTime = 0f;
        StopHorizontal();

        if (next == State.Windup)
        {
            aimLocked = false;
            lungeDirection = DirectionTo(player.transform.position.x);
            Face(lungeDirection);
        }
        if (next == State.BackingAway && HasPlayer)
        {
            int away = Mathf.Abs(body.position.x - player.transform.position.x) > 0.01f
                ? (body.position.x > player.transform.position.x ? 1 : -1) : -lungeDirection;
            retreatTargetX = Mathf.Clamp(body.position.x + away * backAwayDistance, ChaseMin, ChaseMax);
        }
        if (visuals != null)
            visuals.color = useDebugStateTint
                ? (next == State.Windup ? Color.yellow : next == State.Lunging ? Color.red : originalColor)
                : originalColor;
        RefreshAnimation();
    }

    private void RefreshAnimation()
    {
        if (!animationDriver || dead) return;
        EnemyAnimationPhase phase = state == State.Windup ? EnemyAnimationPhase.Windup :
            state == State.Lunging ? EnemyAnimationPhase.Attack :
            state == State.Recovering ? EnemyAnimationPhase.Recover : EnemyAnimationPhase.Locomotion;
        float phaseDuration = state == State.Windup ? windupDuration :
            state == State.Lunging ? lungeDuration : recoveryDuration;
        animationDriver.SetPhase(phase, stateTime, phaseDuration);
        if (state == State.Windup || state == State.Lunging)
            animationDriver.Face(new Vector2(lungeDirection, 0f));
    }

    private void OnCollisionEnter2D(Collision2D collision) => TryDamagePlayer(collision);
    private void OnCollisionStay2D(Collision2D collision) => TryDamagePlayer(collision);

    private void TryDamagePlayer(Collision2D collision)
    {
        if (dead || state != State.Lunging || !HasPlayer)
            return;
        PlayerHealth hit = collision.collider.GetComponentInParent<PlayerHealth>();
        if (hit == player)
        {
            // Player contact is handled by the swept attack sensor; keep lunging.
        }
    }

    public void Die()
    {
        if (dead)
            return;
        dead = true;
        EnemyAudio audio = GetComponent<EnemyAudio>();
        if (audio != null)
        {
            if (player != null) audio.SetListener(player.transform);
            audio.Death();
        }
        if (audio != null)
        {
            audio.StopCue();
            audio.SetQuietWings(true);
            audio.StopFootsteps();
        }
        if (attackContact != null)
        {
            attackContact.EndAttack();
            attackContact.StopAllCoroutines();
            attackContact.enabled = false;
        }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        if (visuals) visuals.color = originalColor;
        DropGloom();
        float deathDuration = animationDriver ? animationDriver.PlayDeath() : 0f;
        if (deathDuration > 0f) StartCoroutine(FinishDeath(deathDuration));
        else RemoveDeadEnemy();
    }

    private IEnumerator FinishDeath(float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveDeadEnemy();
    }

    private void RemoveDeadEnemy()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void DropGloom()
    {
        if (gloomDropped <= 0)
            return;
        PlayerInventory receiver = player != null ? player.GetComponentInParent<PlayerInventory>() : null;
        if (gloomDropPrefab == null || gloomResource == null || receiver == null)
        {
            Debug.LogWarning("Assign WolfEnemy's Gloom pickup prefab, Gloom resource and player inventory.", this);
            return;
        }
        MagnetResourcePickup drop = Instantiate(gloomDropPrefab, transform.position, Quaternion.identity);
        if (drop.Initialize(receiver, gloomResource, gloomDropped))
            drop.gameObject.SetActive(true);
    }

    private void OnDisable() => StopHorizontal();

    private void OnDrawGizmosSelected()
    {
        Vector2 center = patrolCenter != null ? (Vector2)patrolCenter.position
            : Application.isPlaying ? homePosition : (Vector2)transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(center.x - patrolWidth * 0.5f, center.y, transform.position.z),
            new Vector3(center.x + patrolWidth * 0.5f, center.y, transform.position.z));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z),
            new Vector3(patrolWidth * chaseAreaMultiplier, maximumTrackingHeight * 2f, 0f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
