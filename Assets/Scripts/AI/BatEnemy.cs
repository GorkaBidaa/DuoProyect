using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Seeker))]
public class BatEnemy : MonoBehaviour, ITrapTarget
{
    [Header("References")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private SpriteRenderer visuals;

    [Header("Patrol area (world units)")]
    [SerializeField] private Transform patrolCenter;
    [SerializeField] private Vector2 patrolSize = new Vector2(10f, 6f);
    [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
    [SerializeField] private Vector2 patrolPauseRange = new Vector2(0.5f, 1.5f);
    [SerializeField, Min(1f)] private float chaseAreaMultiplier = 2.5f;

    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float detectionRange = 12f;
    [SerializeField] private LayerMask solidLayers;

    [Header("Pathfinding")]
    [SerializeField, Min(0.1f)] private float approachSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 3f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.4f;
    [SerializeField, Min(0.01f)] private float waypointDistance = 0.12f;
    [SerializeField, Min(0.01f)] private float homeArrivalDistance = 0.2f;

    [Header("Attack")]
    [SerializeField, Min(0.1f)] private float attackRange = 3.5f;
    [SerializeField, Min(0.1f)] private float windupDuration = 0.8f;
    [SerializeField, Min(0f)] private float aimLockDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float swoopSpeed = 9f;
    [SerializeField, Min(0.1f)] private float swoopDuration = 1f;
    [SerializeField, Min(1)] private int damage = 25;

    [Header("Optional spike assistance")]
    [SerializeField] private bool spikeAssistEnabled;
    [SerializeField] private LayerMask spikeLayers;
    [SerializeField, Min(0.1f)] private float spikeSearchRadius = 4f;
    [SerializeField, Range(0f, 30f)] private float spikeAssistAngle = 12f;
    [SerializeField, Min(0f)] private float minimumDistanceBehindPlayer = 0.35f;

    [Header("Recovery")]
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.2f;

    [Header("Swoop cooldown and repositioning")]
    [SerializeField, Min(0f)] private float swoopCooldown = 3f;
    [SerializeField, Min(0.1f)] private float backAwayDistance = 2f;
    [SerializeField, Min(0.5f)] private float repositionDistance = 3f;
    [SerializeField, Min(0.1f)] private float repositionSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float minimumAttackPositionChange = 1.5f;
    [SerializeField, Min(0.5f)] private float repositionTimeout = 6f;

    [Header("Lure response")]
    [SerializeField, Min(0.1f)] private float investigateSpeed = 3f;
    [SerializeField, Min(0.1f)] private float lureArrivalDistance = 0.7f;

    [Header("Repellant response")]
    [SerializeField, Min(0.1f)] private float fleeSpeed = 5f;
    [SerializeField, Min(0.1f)] private float repellantClearance = 1.5f;

    [Header("Gloom reward")]
    [SerializeField] private MagnetResourcePickup gloomDropPrefab;
    [SerializeField] private ResourceData gloomResource;
    [SerializeField, Min(0)] private int gloomDropped = 2;

    private EnemyAttackContact attackContact;

    private enum State
    {
        Patrolling, Approaching, Windup, Swooping, Recovering, BackingAway, Repositioning, Returning, Investigating, Repelled
    }

    private State state;
    private Rigidbody2D body;
    private Seeker seeker;
    private Vector2 homePosition;
    private Vector2 patrolTarget;
    private Vector2 returnTarget;
    private Vector2 attackDirection;
    private Vector2 lastSwoopOrigin;
    private Vector2 combatTarget;
    private Color originalColor;
    private float stateTime;
    private float nextPathTime;
    private float patrolWait;
    private float nextSwoopTime;
    private float nextCombatTargetTime;
    private int navigationVersion;
    private int waypointIndex;
    private bool dead;
    public bool IsAlive => !dead && isActiveAndEnabled;
    private bool hasSwooped;
    private bool hasCombatTarget;
    private bool aimLocked;
    private readonly List<Collider2D> nearbySpikes = new List<Collider2D>();
    private bool hasPatrolTarget;
    private bool hasReturnTarget;
    private bool navigationFailed;
    private bool reportedPathError;
    private bool reportedReturnError;
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private EnemyLure activeLure;
    private readonly HashSet<EnemyLure> unreachableLures = new HashSet<EnemyLure>();
    private EnemyRepellantUse repellantSource;
    private Vector2 fleeTarget;
    private Vector2 fleeSourcePosition;
    private bool hasFleeTarget;
    private float nextFleeTargetTime;

    private Rect PatrolRect
    {
        get
        {
            Vector2 center = patrolCenter != null
                ? (Vector2)patrolCenter.position
                : Application.isPlaying ? homePosition : (Vector2)transform.position;
            Vector2 size = new Vector2(Mathf.Max(1f, patrolSize.x), Mathf.Max(1f, patrolSize.y));
            return new Rect(center - size * 0.5f, size);
        }
    }

    private Rect ChaseRect
    {
        get
        {
            Rect patrol = PatrolRect;
            Vector2 size = patrol.size * Mathf.Max(1f, chaseAreaMultiplier);
            return new Rect(patrol.center - size * 0.5f, size);
        }
    }

    private bool HasPlayer => player != null && player.isActiveAndEnabled;

    // Called by EnemySpawnPoint while the clone is inactive, before Awake runs.
    public void ConfigureSpawn(PlayerHealth target, Transform center, bool overrideArea, Vector2 area)
    {
        player = target;
        patrolCenter = center;
        if (overrideArea) patrolSize = new Vector2(Mathf.Max(0.1f, area.x), Mathf.Max(0.1f, area.y));
        enabled = true;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        seeker = GetComponent<Seeker>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        homePosition = body.position;

        if (visuals == null)
            visuals = GetComponentInChildren<SpriteRenderer>();
        if (visuals != null)
            originalColor = visuals.color;

        if (player == null || GetComponent<Collider2D>() == null)
        {
            Debug.LogError("BatEnemy needs a PlayerHealth reference and a Collider2D on its root.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (AstarPath.active == null || AstarPath.active.graphs == null ||
            AstarPath.active.graphs.Length == 0)
        {
            Debug.LogError("BatEnemy needs an active A* Pathfinder with a scanned 2D grid graph.", this);
            enabled = false;
            return;
        }

        ChangeState(PatrolRect.Contains(body.position) ? State.Patrolling : State.Returning);
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
        attackContact.CheckHit(damage, attackDirection);
        stateTime += Time.fixedDeltaTime;

        // Repellant has priority over bait, but a swoop already in flight finishes.
        if (!CheckForRepellant())
            CheckForLure();

        switch (state)
        {
            case State.Patrolling:
                TickPatrol();
                break;

            case State.Approaching:
                if (!CanPursuePlayer() || navigationFailed)
                    ChangeState(State.Returning);
                else if (Vector2.Distance(body.position, player.transform.position) <= attackRange && HasLineOfSight())
                    ChangeState(Time.time >= nextSwoopTime && IsNewAttackPosition(body.position)
                        ? State.Windup : State.Repositioning);
                else
                    FollowPath(player.transform.position, approachSpeed, false);
                break;

            case State.Windup:
                body.linearVelocity = Vector2.zero;
                if (!CanPursuePlayer())
                {
                    ChangeState(State.Returning);
                    break;
                }

                if (!aimLocked)
                {
                    // Keep following the player's position through most of the warning.
                    UpdateAttackAim();

                    float lockTime = Mathf.Max(0f, windupDuration - aimLockDuration);
                    if (stateTime >= lockTime)
                    {
                        aimLocked = true;
                        if (visuals != null)
                            visuals.color = new Color(1f, 0.5f, 0f);
                    }
                }

                if (stateTime >= windupDuration)
                    ChangeState(State.Swooping);
                break;

            case State.Swooping:
                // A committed attack finishes even if the player crosses the chase boundary.
                if (stateTime >= swoopDuration)
                    ChangeState(State.Recovering);
                else
                    body.linearVelocity = attackDirection * swoopSpeed;
                break;

            case State.Recovering:
                body.linearVelocity = Vector2.zero;
                if (!CanPursuePlayer())
                    ChangeState(State.Returning);
                else if (stateTime >= recoveryDuration)
                    ChangeState(State.BackingAway);
                break;

            case State.BackingAway:
                TickBackAway();
                break;

            case State.Repositioning:
                TickReposition();
                break;

            case State.Returning:
                TickReturn();
                break;

            case State.Investigating:
                TickInvestigate();
                break;

            case State.Repelled:
                TickRepelled();
                break;
        }
    }

    private bool CheckForRepellant()
    {
        if (state == State.Swooping)
            return false;

        EnemyRepellantUse source = null;
        if (ChaseRect.Contains(body.position))
        {
            // Keep backing off while this source is close, avoiding boundary oscillation.
            if (repellantSource != null &&
                repellantSource.CanAffect(body.position, repellantClearance + detectionRange))
                source = repellantSource;
            else
                source = EnemyRepellantUse.FindNearest(body.position);
        }

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
            hasFleeTarget = false;
            nextFleeTargetTime = 0f;
            ChangeState(State.Repelled);
        }
        return true;
    }

    private void TickRepelled()
    {
        if (repellantSource == null || !repellantSource.IsActive)
        {
            repellantSource = null;
            ChangeState(State.Returning);
            return;
        }

        float safeDistance = repellantSource.Radius + repellantClearance;
        if (Vector2.Distance(body.position, repellantSource.Position) >= safeDistance)
        {
            body.linearVelocity = Vector2.zero;
            hasFleeTarget = false;
            return;
        }

        if (navigationFailed || (hasFleeTarget &&
            (Vector2.Distance(body.position, fleeTarget) <= homeArrivalDistance ||
             (Vector2.Distance(fleeSourcePosition, repellantSource.Position) > 0.5f &&
              Vector2.Distance(fleeTarget, repellantSource.Position) < safeDistance + 0.2f))))
        {
            hasFleeTarget = false;
            ClearNavigation();
            nextFleeTargetTime = Time.time + repathInterval;
        }

        if (!hasFleeTarget)
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time < nextFleeTargetTime)
                return;

            nextFleeTargetTime = Time.time + repathInterval;
            if (!TryFindFleePoint(out fleeTarget))
                return;
            hasFleeTarget = true;
            fleeSourcePosition = repellantSource.Position;
            ClearNavigation();
        }

        FollowPath(fleeTarget, fleeSpeed, false, true);
    }

    private bool TryFindFleePoint(out Vector2 point)
    {
        point = default;
        if (repellantSource == null || AstarPath.active == null)
            return false;

        GraphNode startNode = AstarPath.active.GetNearest(WorldPoint(body.position), NNConstraint.Default).node;
        if (startNode == null)
            return false;

        Rect area = ChaseRect;
        PatrolNodeConstraint constraint = new PatrolNodeConstraint(area)
        {
            constrainArea = true,
            area = (int)startNode.Area,
            constrainDistance = false
        };
        Vector2 sourcePosition = repellantSource.Position;
        Vector2 away = body.position - sourcePosition;
        float startingDistance = away.magnitude;
        away = startingDistance > 0.01f ? away.normalized : Vector2.up;
        float desiredDistance = repellantSource.Radius + repellantClearance + 0.5f;
        float bestDistance = startingDistance + 0.3f;
        bool found = false;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = attempt == 0 ? 0f : Random.Range(-100f, 100f);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                away.x * Mathf.Cos(radians) - away.y * Mathf.Sin(radians),
                away.x * Mathf.Sin(radians) + away.y * Mathf.Cos(radians));
            Vector2 candidate = sourcePosition + direction * desiredDistance;
            GraphNode node = AstarPath.active.GetNearest(WorldPoint(candidate), constraint).node;
            if (node == null || !constraint.Suitable(node))
                continue;

            Vector2 resolved = (Vector2)(Vector3)node.position;
            float distance = Vector2.Distance(resolved, sourcePosition);
            if (distance <= bestDistance || Vector2.Distance(resolved, body.position) <= homeArrivalDistance * 2f)
                continue;

            point = resolved;
            bestDistance = distance;
            found = true;
            if (distance >= desiredDistance - homeArrivalDistance)
                return true;
        }
        return found;
    }

    private void CheckForLure()
    {
        // A distraction does not cancel an attack that is already committed.
        if (state == State.Windup || state == State.Swooping || state == State.Recovering || state == State.Repelled)
            return;

        unreachableLures.RemoveWhere(lure => lure == null || !lure.IsAvailable);
        Rect area = ChaseRect;
        EnemyLure chosen = null;

        if (area.Contains(body.position))
        {
            if (activeLure != null && activeLure.CanAttract(body.position) &&
                area.Contains(activeLure.Position) && !unreachableLures.Contains(activeLure))
                chosen = activeLure;
            else
                chosen = EnemyLure.FindNearest(body.position, area, unreachableLures);
        }

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
        if (activeLure == null || !activeLure.IsAvailable)
        {
            activeLure = null;
            ChangeState(State.Returning);
            return;
        }

        if (navigationFailed)
        {
            unreachableLures.Add(activeLure);
            activeLure = null;
            ChangeState(State.Returning);
            return;
        }

        if (Vector2.Distance(body.position, activeLure.Position) <= lureArrivalDistance)
            body.linearVelocity = Vector2.zero;
        else
            FollowPath(activeLure.Position, investigateSpeed, false, true);
    }

    private bool IsNewAttackPosition(Vector2 position)
    {
        return !hasSwooped ||
            Vector2.Distance(position, lastSwoopOrigin) >= minimumAttackPositionChange;
    }

    private void TickBackAway()
    {
        if (!CanPursuePlayer())
        {
            ChangeState(State.Returning);
            return;
        }

        if (navigationFailed || stateTime >= repositionTimeout)
        {
            ChangeState(State.Repositioning);
            return;
        }

        if (!hasCombatTarget)
        {
            if (!TryFindCombatPoint(true, out combatTarget))
            {
                // A wall or boundary may prevent backing straight away. Try a flank instead.
                ChangeState(State.Repositioning);
                return;
            }
            hasCombatTarget = true;
            ClearNavigation();
        }

        if (Vector2.Distance(body.position, combatTarget) <= homeArrivalDistance)
            ChangeState(State.Repositioning);
        else
            FollowPath(combatTarget, repositionSpeed, false, true);
    }

    private void TickReposition()
    {
        if (!CanPursuePlayer())
        {
            ChangeState(State.Returning);
            return;
        }

        if (hasCombatTarget && !navigationFailed &&
            Vector2.Distance(body.position, combatTarget) <= homeArrivalDistance)
        {
            body.linearVelocity = Vector2.zero;
            // Waiting at a reached position is allowed for arbitrarily long cooldowns.
            if (Time.time < nextSwoopTime)
                return;

            bool ready = IsNewAttackPosition(body.position) &&
                Vector2.Distance(body.position, player.transform.position) <= attackRange && HasLineOfSight();
            ChangeState(ready ? State.Windup : State.Approaching);
            return;
        }

        if (stateTime >= repositionTimeout)
        {
            // Do not force an attack from the old spot when the room has no usable new position.
            ChangeState(State.Returning);
            return;
        }

        if (navigationFailed)
        {
            hasCombatTarget = false;
            ClearNavigation();
            nextCombatTargetTime = Time.time + repathInterval;
        }

        if (!hasCombatTarget)
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time < nextCombatTargetTime)
                return;

            nextCombatTargetTime = Time.time + repathInterval;
            if (!TryFindCombatPoint(false, out combatTarget))
                return;

            hasCombatTarget = true;
            ClearNavigation();
        }

        FollowPath(combatTarget, repositionSpeed, false, true);
    }

    private bool TryFindCombatPoint(bool backingAway, out Vector2 point)
    {
        point = default;
        if (!HasPlayer || AstarPath.active == null)
            return false;

        GraphNode startNode = AstarPath.active.GetNearest(WorldPoint(body.position), NNConstraint.Default).node;
        if (startNode == null)
            return false;

        Rect area = ChaseRect;
        PatrolNodeConstraint constraint = new PatrolNodeConstraint(area)
        {
            constrainArea = true,
            area = (int)startNode.Area,
            constrainDistance = false
        };

        Vector2 playerPosition = player.transform.position;
        Vector2 away = body.position - playerPosition;
        float startingPlayerDistance = away.magnitude;
        away = startingPlayerDistance > 0.01f ? away.normalized : -attackDirection;
        if (away.sqrMagnitude < 0.01f)
            away = Vector2.up;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = backingAway
                ? (attempt == 0 ? 0f : Random.Range(-70f, 70f))
                : Random.Range(50f, 140f) * (Random.value < 0.5f ? -1f : 1f);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                away.x * Mathf.Cos(radians) - away.y * Mathf.Sin(radians),
                away.x * Mathf.Sin(radians) + away.y * Mathf.Cos(radians));
            Vector2 candidate = backingAway
                ? body.position + direction * backAwayDistance
                : playerPosition + direction * repositionDistance * Random.Range(0.85f, 1.15f);

            if (!area.Contains(candidate))
                continue;

            GraphNode node = AstarPath.active.GetNearest(WorldPoint(candidate), constraint).node;
            if (node == null || !constraint.Suitable(node))
                continue;

            Vector2 resolved = (Vector2)(Vector3)node.position;
            if (Vector2.Distance(resolved, candidate) > 1f ||
                Vector2.Distance(resolved, body.position) < Mathf.Max(homeArrivalDistance * 2f, 0.5f))
                continue;

            if (backingAway)
            {
                if (Vector2.Distance(resolved, playerPosition) < startingPlayerDistance + backAwayDistance * 0.35f)
                    continue;
            }
            else
            {
                if ((hasSwooped && Vector2.Distance(resolved, lastSwoopOrigin) < minimumAttackPositionChange + homeArrivalDistance) ||
                    Vector2.Distance(resolved, playerPosition) < repositionDistance * 0.65f ||
                    Physics2D.Linecast(resolved, playerPosition, solidLayers).collider != null)
                    continue;
            }

            point = resolved;
            return true;
        }

        return false;
    }

    private void TickPatrol()
    {
        if (!PatrolRect.Contains(body.position))
        {
            ChangeState(State.Returning);
            return;
        }

        if (CanPursuePlayer() &&
            Vector2.Distance(body.position, player.transform.position) <= detectionRange && HasLineOfSight())
        {
            ChangeState(State.Approaching);
            return;
        }

        if (hasPatrolTarget && (navigationFailed ||
            Vector2.Distance(body.position, patrolTarget) <= homeArrivalDistance))
        {
            BeginPatrolPause();
            return;
        }

        if (!hasPatrolTarget)
        {
            body.linearVelocity = Vector2.zero;
            patrolWait -= Time.fixedDeltaTime;
            if (patrolWait > 0f)
                return;

            if (!TryFindPatrolPoint(false, out patrolTarget))
            {
                BeginPatrolPause();
                return;
            }

            hasPatrolTarget = true;
            ClearNavigation();
        }

        FollowPath(patrolTarget, patrolSpeed, true);
    }

    private void TickReturn()
    {
        if (PatrolRect.Contains(body.position))
        {
            ChangeState(State.Patrolling);
            return;
        }

        if (!hasReturnTarget || navigationFailed)
        {
            bool preferHome = !navigationFailed;
            ClearNavigation();
            hasReturnTarget = TryFindPatrolPoint(preferHome, out returnTarget);

            if (!hasReturnTarget)
            {
                body.linearVelocity = Vector2.zero;
                if (!reportedReturnError)
                    Debug.LogWarning("Bat has no reachable grid nodes inside its patrol area. Check grid coverage and clearance.", this);
                reportedReturnError = true;
                return;
            }

            reportedReturnError = false;
        }

        // Returning may cross the outer area; only normal patrol paths are restricted.
        FollowPath(returnTarget, returnSpeed, false);
    }

    private void BeginPatrolPause()
    {
        hasPatrolTarget = false;
        float minimum = Mathf.Max(0f, patrolPauseRange.x);
        patrolWait = Random.Range(minimum, Mathf.Max(minimum, patrolPauseRange.y));
        body.linearVelocity = Vector2.zero;
        ClearNavigation();
    }

    private bool CanPursuePlayer()
    {
        return HasPlayer && !EnemyRepellantUse.Protects(player.transform) && ChaseRect.Contains(body.position) &&
            ChaseRect.Contains((Vector2)player.transform.position);
    }

    private bool HasLineOfSight()
    {
        return HasPlayer &&
            Physics2D.Linecast(body.position, player.transform.position, solidLayers).collider == null;
    }

    private bool TryFindPatrolPoint(bool preferHome, out Vector2 point)
    {
        point = default;
        if (AstarPath.active == null)
            return false;

        GraphNode startNode = AstarPath.active.GetNearest(WorldPoint(body.position), NNConstraint.Default).node;
        if (startNode == null)
            return false;

        Rect area = PatrolRect;
        PatrolNodeConstraint constraint = new PatrolNodeConstraint(area)
        {
            constrainArea = true,
            area = (int)startNode.Area,
            constrainDistance = false
        };

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 candidate = preferHome && attempt == 0 ? homePosition : new Vector2(
                Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));

            GraphNode node = AstarPath.active.GetNearest(WorldPoint(candidate), constraint).node;
            if (node == null || !constraint.Suitable(node))
                continue;

            point = (Vector2)(Vector3)node.position;
            if (preferHome || Vector2.Distance(body.position, point) > homeArrivalDistance * 2f)
                return true;
        }

        return false;
    }

    private Vector3 WorldPoint(Vector2 point) => new Vector3(point.x, point.y, transform.position.z);

    private void FollowPath(Vector2 destination, float speed, bool restrictToPatrol, bool restrictToChase = false)
    {
        if (Time.time >= nextPathTime && seeker.IsDone() && AstarPath.active != null)
        {
            nextPathTime = Time.time + repathInterval;
            int version = navigationVersion;
            ABPath path = ABPath.Construct(WorldPoint(body.position), WorldPoint(destination));

            if (restrictToPatrol || restrictToChase)
            {
                Rect area = restrictToPatrol ? PatrolRect : ChaseRect;
                path.nnConstraint = new PatrolNodeConstraint(area);
                path.traversalProvider = new PatrolTraversal(area);
            }

            seeker.StartPath(path, result => ReceivePath(result, version));
        }

        while (waypointIndex < waypoints.Count &&
            Vector2.Distance(body.position, waypoints[waypointIndex]) <= waypointDistance)
        {
            waypointIndex++;
        }

        if (waypointIndex >= waypoints.Count)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = (Vector2)waypoints[waypointIndex] - body.position;
        float stepSpeed = Mathf.Min(speed, direction.magnitude / Time.fixedDeltaTime);
        body.linearVelocity = direction.normalized * stepSpeed;
    }

    private void ReceivePath(Pathfinding.Path path, int version)
    {
        if (this == null || !isActiveAndEnabled || dead || version != navigationVersion)
            return;

        waypoints.Clear();
        waypointIndex = 0;
        navigationFailed = path.error || path.vectorPath == null || path.vectorPath.Count == 0;

        if (navigationFailed)
        {
            // A blocked patrol leg is retried with a different destination.
            if (state != State.Patrolling && !reportedPathError)
                Debug.LogWarning("Bat could not find a route. Check grid coverage and connected nodes. " + path.errorLog, this);
            reportedPathError = true;
            return;
        }

        waypoints.AddRange(path.vectorPath);
        waypointIndex = waypoints.Count > 1 ? 1 : 0;
        reportedPathError = false;
    }

    private void UpdateAttackAim()
    {
        if (!HasPlayer)
            return;

        Vector2 playerPosition = player.transform.position;
        Vector2 toPlayer = playerPosition - body.position;
        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f
            ? toPlayer.normalized : Vector2.right;

        if (spikeAssistEnabled && spikeLayers.value != 0 &&
            TryFindSpikeAim(playerPosition, direction, out Vector2 spikePosition))
        {
            direction = (spikePosition - body.position).normalized;
        }

        attackDirection = direction;
    }

    private bool TryFindSpikeAim(Vector2 playerPosition, Vector2 playerDirection, out Vector2 target)
    {
        target = default;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(spikeLayers);
        filter.useTriggers = true;
        nearbySpikes.Clear();
        Physics2D.OverlapCircle(playerPosition, spikeSearchRadius, filter, nearbySpikes);

        float bestScore = float.PositiveInfinity;
        float maximumDistance = Mathf.Max(0f, swoopSpeed * swoopDuration - 0.1f);
        bool found = false;

        foreach (Collider2D spike in nearbySpikes)
        {
            if (spike == null || !spike.enabled || !spike.gameObject.activeInHierarchy)
                continue;

            SpikeTrap trap = spike.GetComponentInParent<SpikeTrap>();
            if (trap == null || !trap.isActiveAndEnabled)
                continue;

            Vector2 spikePosition = spike.bounds.center;
            Vector2 behindPlayer = spikePosition - playerPosition;
            float behindDistance = Vector2.Dot(behindPlayer, playerDirection);
            if (behindDistance < minimumDistanceBehindPlayer ||
                behindPlayer.sqrMagnitude > spikeSearchRadius * spikeSearchRadius)
                continue;

            Vector2 toSpike = spikePosition - body.position;
            float distance = toSpike.magnitude;
            if (distance <= 0.01f || distance > maximumDistance)
                continue;

            float angle = Vector2.Angle(playerDirection, toSpike);
            if (angle > spikeAssistAngle)
                continue;

            // Assistance cannot aim through a solid wall.
            if (Physics2D.Linecast(body.position, spikePosition, solidLayers).collider != null)
                continue;

            float score = angle + behindDistance * 0.2f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            target = spikePosition;
            found = true;
        }

        return found;
    }

    private void ChangeState(State next)
    {
        if (state == State.Swooping && next == State.Recovering)
            nextSwoopTime = Time.time + swoopCooldown;

        if (next == State.Swooping && attackContact != null) attackContact.BeginAttack();
        if (state == State.Swooping && next != State.Swooping && attackContact != null) attackContact.EndAttack();
        state = next;
        stateTime = 0f;
        body.linearVelocity = Vector2.zero;
        hasReturnTarget = false;
        hasCombatTarget = false;
        nextCombatTargetTime = 0f;
        ClearNavigation();

        if (next == State.Patrolling)
        {
            BeginPatrolPause();
        }

        if (next == State.Swooping)
        {
            lastSwoopOrigin = body.position;
            hasSwooped = true;
        }

        if (next == State.Windup)
        {
            aimLocked = false;
            UpdateAttackAim();
        }

        if (visuals != null)
        {
            visuals.color = next == State.Windup ? Color.yellow
                : next == State.Swooping ? Color.red
                : originalColor;
        }
    }

    private void ClearNavigation()
    {
        navigationVersion++;
        waypoints.Clear();
        waypointIndex = 0;
        nextPathTime = 0f;
        navigationFailed = false;
    }

    private void OnCollisionEnter2D(Collision2D collision) => HandleCollision(collision);
    private void OnCollisionStay2D(Collision2D collision) => HandleCollision(collision);

    private void HandleCollision(Collision2D collision)
    {
        if (dead || state != State.Swooping)
            return;

        PlayerHealth hitPlayer = collision.collider.GetComponentInParent<PlayerHealth>();
        if (hitPlayer != null && hitPlayer == player)
        {
            // Player contact is handled by the swept attack sensor; keep swooping.
            return;
        }

        int hitLayer = collision.collider.gameObject.layer;
        if ((solidLayers.value & (1 << hitLayer)) != 0)
            ChangeState(State.Recovering);
    }

    public void Die()
    {
        if (dead)
            return;
        dead = true;
        body.linearVelocity = Vector2.zero;
        DropGloom();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void DropGloom()
    {
        if (gloomDropped <= 0)
            return;

        PlayerInventory receiver = player != null
            ? player.GetComponentInParent<PlayerInventory>() : null;

        if (gloomDropPrefab == null || gloomResource == null || receiver == null)
        {
            Debug.LogWarning("Bat Gloom reward needs its pickup prefab, Gloom resource and the player's inventory.", this);
            return;
        }

        // Spawn independently of the Bat, so destroying the Bat cannot destroy its reward.
        MagnetResourcePickup drop = Instantiate(gloomDropPrefab, transform.position, Quaternion.identity);
        if (drop.Initialize(receiver, gloomResource, gloomDropped))
            drop.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        ClearNavigation();
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Rect patrol = PatrolRect;
        Rect chase = ChaseRect;
        float z = transform.position.z;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(patrol.center.x, patrol.center.y, z), new Vector3(patrol.width, patrol.height, 0f));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(new Vector3(chase.center.x, chase.center.y, z), new Vector3(chase.width, chase.height, 0f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (Application.isPlaying && (state == State.Windup || state == State.Swooping))
        {
            Gizmos.color = aimLocked ? new Color(1f, 0.5f, 0f) : Color.yellow;
            Gizmos.DrawLine(transform.position,
                transform.position + (Vector3)(attackDirection * swoopSpeed * swoopDuration));
        }

        if (Application.isPlaying && hasCombatTarget &&
            (state == State.BackingAway || state == State.Repositioning))
        {
            Gizmos.color = Color.cyan;
            Vector3 target = new Vector3(combatTarget.x, combatTarget.y, z);
            Gizmos.DrawWireSphere(target, 0.2f);
            Gizmos.DrawLine(transform.position, target);
        }
    }

    // Both patrol destinations AND their connecting routes stay in the rectangle.
    private sealed class PatrolNodeConstraint : NNConstraint
    {
        private readonly Rect bounds;
        public PatrolNodeConstraint(Rect bounds) { this.bounds = bounds; }
        public override bool Suitable(GraphNode node)
        {
            return base.Suitable(node) && bounds.Contains((Vector2)(Vector3)node.position);
        }
    }

    private sealed class PatrolTraversal : ITraversalProvider
    {
        private readonly Rect bounds;
        public PatrolTraversal(Rect bounds) { this.bounds = bounds; }
        public bool CanTraverse(Pathfinding.Path path, GraphNode node)
        {
            return DefaultITraversalProvider.CanTraverse(path, node) &&
                bounds.Contains((Vector2)(Vector3)node.position);
        }
        public uint GetTraversalCost(Pathfinding.Path path, GraphNode node)
        {
            return DefaultITraversalProvider.GetTraversalCost(path, node);
        }
    }
}
