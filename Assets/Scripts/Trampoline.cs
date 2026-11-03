using UnityEngine;

public class Trampoline : MonoBehaviour
{
    [Header("Trampoline Settings")]
    public float bounceForce = 20f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerMovement player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player == null && collision.gameObject.CompareTag("Player"))
            player = collision.gameObject.GetComponent<PlayerMovement>();

        if (player != null)
        {
            player.Bounce(bounceForce);
        }
    }
}