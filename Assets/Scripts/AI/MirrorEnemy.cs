using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MirrorEnemy : MonoBehaviour
{
    [Header("References (Auto-found if empty)")]
    [SerializeField] private StaminaSystem playerStamina;
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private SpriteRenderer visuals;
    [SerializeField] private Animator animator;

    [Header("Detection Ranges")]
    [Tooltip("horizontal Distance to wake up")]
    [SerializeField, Min(1f)] private float wakeUpRange = 9f;
    [Tooltip("horizontal Distance to lock onto the player")]
    [SerializeField, Min(0.5f)] private float lockRange = 5f;
    [Tooltip("maximum vertical distance")]
    [SerializeField, Min(1f)] private float maxVerticalRange = 7f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask solidLayers;

    [Header("Catch-Up Jump & Return Physics")]
    [Tooltip("vertical speed when catching up")]
    [SerializeField, Min(5f)] private float minCatchUpSpeed = 16f;
    [Tooltip("maximum speed allowed to intercept your height")]
    [SerializeField, Min(10f)] private float maxCatchUpSpeed = 28f;
    [Tooltip("Y distance at which it's considered that it has caught up with you and blocks at 1:1")]
    [SerializeField, Min(0.01f)] private float syncTolerance = 0.08f;
    [Tooltip("gravity when returning to its position if you move away")]
    [SerializeField, Min(1f)] private float returnGravity = 35f;

    private enum State
    {
        Idle,
        CatchingUp,   // jumping to catch up with the player, but not yet at the same Y
        LockedMirror, // copying the player's Y exactly, as long as the player is within range
        Returning     // returning to its original position when the player moves away
    }

    private State state = State.Idle;
    private Rigidbody2D rb;
    private float homeX;
    private float homeY;
    private float returnVelocityY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        homeX = rb.position.x;
        homeY = rb.position.y;

        if (visuals == null)
            visuals = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (playerStamina == null)
            playerStamina = FindFirstObjectByType<StaminaSystem>();

        if (playerStamina != null && playerRb == null)
            playerRb = playerStamina.GetComponent<Rigidbody2D>();

        if (playerStamina == null || playerRb == null)
        {
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (playerStamina == null || playerRb == null)
            return;

        if (playerStamina.IsDead)
        {
            if (state != State.Idle && state != State.Returning)
                EnterState(State.Returning);

            TickReturning();
            UpdateVisuals();
            return;
        }

        Vector2 playerPos = playerRb.position;
        float absDistX = Mathf.Abs(playerPos.x - homeX);
        float absDistY = Mathf.Abs(playerPos.y - homeY);

        bool inZone = absDistX <= wakeUpRange &&
                      absDistY <= maxVerticalRange &&
                      (!requireLineOfSight || HasLineOfSight(playerPos));

        if (!inZone)
        {
            if (state == State.CatchingUp || state == State.LockedMirror)
                EnterState(State.Returning);
        }
        else
        {
            if (state == State.Idle || state == State.Returning)
            {
                // if the player is within the lock range, go directly to LockedMirror, otherwise start CatchingUp
                float yDiff = Mathf.Abs(playerPos.y - rb.position.y);
                EnterState(yDiff <= syncTolerance ? State.LockedMirror : State.CatchingUp);
            }
        }

        switch (state)
        {
            case State.Idle:
                rb.MovePosition(new Vector2(homeX, homeY));
                rb.linearVelocity = Vector2.zero;
                break;

            case State.CatchingUp:
                TickCatchingUp(playerPos, absDistX);
                break;

            case State.LockedMirror:
                TickLockedMirror(playerPos);
                break;

            case State.Returning:
                TickReturning();
                break;
        }

        UpdateVisuals();
    }

    private void EnterState(State next)
    {
        state = next;
        if (next == State.Returning)
        {
            returnVelocityY = rb.linearVelocity.y;
        }
    }

    private void TickCatchingUp(Vector2 playerPos, float absDistX)
    {
        float currentY = rb.position.y;
        float targetY = playerPos.y;
        float diffY = targetY - currentY;

        if (Mathf.Abs(diffY) <= syncTolerance)
        {
            MoveVerticallyClamped(targetY);
            rb.linearVelocity = new Vector2(0f, playerRb.linearVelocity.y);
            EnterState(State.LockedMirror);
            return;
        }

        // calculate the time available to reach the player based on horizontal distance and their approach speed
        float distToLock = Mathf.Max(0f, absDistX - lockRange);
        float playerApproachSpeed = Mathf.Max(Mathf.Abs(playerRb.linearVelocity.x), 8f);
        float timeAvailable = Mathf.Max(Time.fixedDeltaTime, distToLock / playerApproachSpeed);

        // if the player is moving in the same vertical direction, add their vertical speed to the required speed to catch up
        float playerVelBonus = Mathf.Sign(diffY) == Mathf.Sign(playerRb.linearVelocity.y)
            ? Mathf.Abs(playerRb.linearVelocity.y)
            : 0f;

        float requiredSpeed = (Mathf.Abs(diffY) / timeAvailable) + playerVelBonus;
        float catchUpSpeed = Mathf.Clamp(requiredSpeed, minCatchUpSpeed, maxCatchUpSpeed);

        float nextY = Mathf.MoveTowards(currentY, targetY, catchUpSpeed * Time.fixedDeltaTime);
        float actualY = MoveVerticallyClamped(nextY);
        rb.linearVelocity = new Vector2(0f, (actualY - currentY) / Time.fixedDeltaTime);

        if (Mathf.Abs(targetY - actualY) <= syncTolerance)
        {
            EnterState(State.LockedMirror);
        }
    }

    private void TickLockedMirror(Vector2 playerPos)
    {
        float currentY = rb.position.y;
        float actualY = MoveVerticallyClamped(playerPos.y);
        rb.linearVelocity = new Vector2(0f, (actualY - currentY) / Time.fixedDeltaTime);
    }

    private void TickReturning()
    {
        float currentY = rb.position.y;
        if (Mathf.Abs(currentY - homeY) <= 0.02f)
        {
            rb.MovePosition(new Vector2(homeX, homeY));
            rb.linearVelocity = Vector2.zero;
            returnVelocityY = 0f;
            EnterState(State.Idle);
            return;
        }

        float dir = Mathf.Sign(homeY - currentY);
        returnVelocityY = Mathf.MoveTowards(returnVelocityY, dir * maxCatchUpSpeed, returnGravity * Time.fixedDeltaTime);
        float nextY = currentY + returnVelocityY * Time.fixedDeltaTime;


        if ((dir > 0f && nextY >= homeY) || (dir < 0f && nextY <= homeY))
        {
            rb.MovePosition(new Vector2(homeX, homeY));
            rb.linearVelocity = Vector2.zero;
            returnVelocityY = 0f;
            EnterState(State.Idle);
            return;
        }

        float actualY = MoveVerticallyClamped(nextY);
        // if it hit a ceiling or floor, stop the vertical velocity so it doesn't keep trying to go through the wall
        if (Mathf.Abs(actualY - nextY) > 0.001f)
            returnVelocityY = 0f;

        rb.linearVelocity = new Vector2(0f, (actualY - currentY) / Time.fixedDeltaTime);
    }

    // moves vertically towards targetY, but stops if it hits a wall or floor. Returns the actual Y position after moving.
    private float MoveVerticallyClamped(float targetY)
    {
        float currentY = rb.position.y;
        float deltaY = targetY - currentY;

        if (Mathf.Abs(deltaY) > 0.0001f && solidLayers.value != 0)
        {
            Vector2 dir = deltaY > 0f ? Vector2.up : Vector2.down;
            float dist = Mathf.Abs(deltaY);
            RaycastHit2D hit = Physics2D.BoxCast(
                rb.position,
                GetComponent<Collider2D>().bounds.size * 0.9f,
                0f,
                dir,
                dist,
                solidLayers);

            if (hit.collider != null)
            {
                targetY = currentY + dir.y * Mathf.Max(0f, hit.distance - 0.02f);
            }
        }

        rb.MovePosition(new Vector2(homeX, targetY));
        return targetY;
    }

    private bool HasLineOfSight(Vector2 playerPos)
    {
        if (solidLayers.value == 0)
            return true;

        return Physics2D.Linecast(rb.position, playerPos, solidLayers).collider == null;
    }

    private void UpdateVisuals()
    {
        if (playerRb != null && visuals != null)
        {
            float dx = playerRb.position.x - rb.position.x;
            if (Mathf.Abs(dx) > 0.05f)
                visuals.flipX = dx < 0f;
        }

        if (animator != null)
        {
            animator.SetFloat("VelocityY", rb.linearVelocity.y);
            animator.SetBool("IsGrounded", Mathf.Abs(rb.position.y - homeY) <= 0.05f);
        }
    }

    // Die in contanct
    private void OnCollisionEnter2D(Collision2D collision) => TryKillPlayer(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryKillPlayer(other);

    private void TryKillPlayer(Collider2D col)
    {
        StaminaSystem hitStamina = col.GetComponentInParent<StaminaSystem>();
        if (hitStamina == null && col.CompareTag("Player"))
            hitStamina = col.GetComponent<StaminaSystem>();

        if (hitStamina != null)
        {
            hitStamina.Die();
        }
    }



}