using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    [Header("Trampoline Settings")]
    public float bounceForce = 20f;

    // Support both solid collisions and trigger colliders
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
            TryBounce(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBounce(other);
    }

    private void TryBounce(Collider2D collider)
    {
        if (collider == null || collider.isTrigger)
            return;

        PlayerMovement player = collider.GetComponentInParent<PlayerMovement>();
        if (player == null && collider.gameObject.CompareTag("Player"))
            player = collider.gameObject.GetComponent<PlayerMovement>();

        if (player == null || player.IsTeleporting)
            return;

        // Do not bounce the player if they are already dead
        StaminaSystem stamina = player.GetComponent<StaminaSystem>();
        if (stamina != null && stamina.IsDead)
            return;

        player.Bounce(bounceForce);
    }
}