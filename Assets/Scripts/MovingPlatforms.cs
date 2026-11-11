using UnityEngine;

[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    public Vector2 Velocity { get; private set; }

    private Vector2 a, b, target;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Enforce kinematic body settings so the platform never falls due to gravity
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("Assign pointA and pointB on MovingPlatform.", this);
            enabled = false;
            return;
        }

        a = pointA.position;
        b = pointB.position;
        target = b;
    }

    void FixedUpdate()
    {
        if (Time.fixedDeltaTime <= 0f)
        {
            Velocity = Vector2.zero;
            return;
        }

        Vector2 current = rb.position;
        Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);

        Velocity = (next - current) / Time.fixedDeltaTime;
        rb.MovePosition(next);

        // Switch target endpoint when close enough using squared distance
        if ((next - target).sqrMagnitude < 0.0001f)
            target = (target == a) ? b : a;
    }

    private void OnDisable()
    {
        Velocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the platform path in the Scene view
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawWireSphere(pointA.position, 0.15f);
            Gizmos.DrawWireSphere(pointB.position, 0.15f);
        }
    }
}