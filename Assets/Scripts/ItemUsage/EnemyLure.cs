using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class EnemyLure : MonoBehaviour
{
    [Header("Lure")]
    [SerializeField, Min(0.1f)] private float duration = 10f;
    [SerializeField, Min(0.1f)] private float attractionRange = 8f;

    [Header("Throw and landing")]
    [SerializeField] private LayerMask landingLayers;
    [SerializeField, Min(0.1f)] private float gravityScale = 1.5f;
    [SerializeField, Min(0.1f)] private float maximumFlightTime = 6f;
    [SerializeField, Range(0.1f, 1f)] private float minimumGroundNormal = 0.5f;

    private static readonly List<EnemyLure> activeLures = new List<EnemyLure>();
    private Rigidbody2D body;
    private CircleCollider2D shape;
    private float expiresAt;
    private float flightExpiresAt;
    private bool initialized;
    private bool landed;

    public Vector2 Position => transform.position;
    public bool IsConfigured => landingLayers.value != 0 &&
        GetComponent<Rigidbody2D>() != null && GetComponent<CircleCollider2D>() != null;

    public bool IsAvailable => initialized && landed && isActiveAndEnabled &&
        body != null && body.IsTouchingLayers(landingLayers) && Time.time < expiresAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => activeLures.Clear();

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<CircleCollider2D>();
    }

    private void OnEnable()
    {
        if (!activeLures.Contains(this))
            activeLures.Add(this);
    }

    private void OnDisable() => activeLures.Remove(this);

    public bool Launch(Vector2 velocity, Collider2D[] ownerColliders)
    {
        if (initialized || !IsConfigured)
            return false;

        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (shape == null)
            shape = GetComponent<CircleCollider2D>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.gravityScale = gravityScale;
        body.linearDamping = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        shape.isTrigger = false;

        if (ownerColliders != null)
        {
            foreach (Collider2D owner in ownerColliders)
            {
                if (owner != null && owner.enabled && owner.gameObject.activeInHierarchy)
                    Physics2D.IgnoreCollision(shape, owner, true);
            }
        }

        initialized = true;
        landed = false;
        flightExpiresAt = Time.time + Mathf.Max(0.1f, maximumFlightTime);
        body.linearVelocity = velocity;
        return true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        if ((!landed && Time.time >= flightExpiresAt) || (landed && Time.time >= expiresAt))
            Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckLanding(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckLanding(collision);

    private void CheckLanding(Collision2D collision)
    {
        if (!initialized || landed ||
            (landingLayers.value & (1 << collision.collider.gameObject.layer)) == 0)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < minimumGroundNormal)
                continue;

            landed = true;
            InteractionAudio.Play(InteractionCue.LureLand, transform.position);
            // The distraction's full duration begins on landing, not on throwing.
            expiresAt = Time.time + Mathf.Max(0.1f, duration);
            body.linearVelocity = Vector2.zero;
            break;
        }
    }

    public bool CanAttract(Vector2 enemyPosition)
    {
        return IsAvailable &&
            (Position - enemyPosition).sqrMagnitude <= attractionRange * attractionRange;
    }

    public static EnemyLure FindNearest(Vector2 enemyPosition, Rect allowedArea, ISet<EnemyLure> ignored)
    {
        EnemyLure nearest = null;
        float bestDistance = float.PositiveInfinity;

        foreach (EnemyLure lure in activeLures)
        {
            if (lure == null || !lure.CanAttract(enemyPosition) ||
                !allowedArea.Contains(lure.Position) ||
                (ignored != null && ignored.Contains(lure)))
                continue;

            float distance = (lure.Position - enemyPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                nearest = lure;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attractionRange);
    }
}
