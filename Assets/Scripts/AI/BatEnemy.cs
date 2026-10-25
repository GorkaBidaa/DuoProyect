using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BatEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth player;
    [SerializeField] private SpriteRenderer visuals;

    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float detectionRange = 7f;
    [SerializeField] private LayerMask solidLayers;

    [Header("Attack")]
    [SerializeField, Min(0.1f)] private float windupDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float swoopSpeed = 9f;
    [SerializeField, Min(0.1f)] private float swoopDuration = 1f;
    [SerializeField, Min(1)] private int damage = 25;

    [Header("Recovery")]
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 3f;

    private enum State { Waiting, Windup, Swooping, Recovering, Returning }

    private State state;
    private Rigidbody2D body;
    private Vector2 homePosition;
    private Vector2 attackDirection;
    private Color originalColor;
    private float stateTime;
    private bool dead;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        homePosition = body.position;

        if (visuals == null)
            visuals = GetComponentInChildren<SpriteRenderer>();

        if (visuals != null)
            originalColor = visuals.color;

        if (player == null)
        {
            Debug.LogError("Assign the PlayerHealth reference on BatEnemy.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (dead)
            return;

        if (player == null || !player.isActiveAndEnabled)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        stateTime += Time.fixedDeltaTime;

        switch (state)
        {
            case State.Waiting:
                body.linearVelocity = Vector2.zero;

                if (CanSeePlayer())
                    ChangeState(State.Windup);
                break;

            case State.Windup:
                body.linearVelocity = Vector2.zero;

                if (stateTime >= windupDuration)
                    ChangeState(State.Swooping);
                break;

            case State.Swooping:
                if (stateTime >= swoopDuration)
                {
                    ChangeState(State.Recovering);
                    break;
                }

                body.linearVelocity = attackDirection * swoopSpeed;
                break;

            case State.Recovering:
                body.linearVelocity = Vector2.zero;

                if (stateTime >= recoveryDuration)
                    ChangeState(State.Returning);
                break;

            case State.Returning:
                Vector2 toHome = homePosition - body.position;

                if (toHome.sqrMagnitude <= 0.01f)
                {
                    ChangeState(State.Waiting);
                    break;
                }

                // Avoid overshooting the home position.
                float speed = Mathf.Min(
                    returnSpeed,
                    toHome.magnitude / Time.fixedDeltaTime);

                body.linearVelocity = toHome.normalized * speed;
                break;
        }
    }

    private bool CanSeePlayer()
    {
        Vector2 target = player.transform.position;

        if ((target - body.position).sqrMagnitude >
            detectionRange * detectionRange)
            return false;

        RaycastHit2D obstruction =
            Physics2D.Linecast(body.position, target, solidLayers);

        return obstruction.collider == null;
    }

    private void ChangeState(State next)
    {
        state = next;
        stateTime = 0f;
        body.linearVelocity = Vector2.zero;

        if (next == State.Windup)
        {
            // Aim once. Dodging afterward makes the bat miss.
            Vector2 target = player.transform.position;
            attackDirection = (target - body.position).normalized;

            if (attackDirection.sqrMagnitude < 0.01f)
                attackDirection = Vector2.right;
        }

        if (visuals != null)
        {
            visuals.color = next == State.Windup ? Color.yellow
                : next == State.Swooping ? Color.red
                : originalColor;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void HandleCollision(Collision2D collision)
    {
        if (dead || state != State.Swooping)
            return;

        PlayerHealth hitPlayer =
            collision.collider.GetComponentInParent<PlayerHealth>();

        if (hitPlayer != null && hitPlayer == player)
        {
            // End the attack before applying damage: one hit per swoop.
            ChangeState(State.Recovering);
            hitPlayer.TakeDamage(damage);
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
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}