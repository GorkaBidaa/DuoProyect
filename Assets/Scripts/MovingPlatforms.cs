using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;

    public Vector2 Velocity { get; private set; }

    private Vector2 a, b, target;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        a = pointA.position;
        b = pointB.position;
        target = b;
    }

    void FixedUpdate()
    {
        Vector2 current = rb.position;
        Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);

        Velocity = (next - current) / Time.fixedDeltaTime;
        rb.MovePosition(next);

        if (Vector2.Distance(next, target) < 0.01f)
            target = (target == a) ? b : a;
    }
}