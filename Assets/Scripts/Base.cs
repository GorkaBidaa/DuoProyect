using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BaseStation : MonoBehaviour
{
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private StaminaSystem cachedPlayerStamina;
    private PlayerHealth cachedPlayerHealth;

    private void Reset()
    {
        // Ensure the checkpoint collider is configured as a trigger by default
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    // Save this base as checkpoint
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null)
            return;

        StaminaSystem playerStamina = collision.GetComponentInParent<StaminaSystem>();
        if (playerStamina == null && collision.CompareTag("Player"))
            playerStamina = collision.GetComponent<StaminaSystem>();

        if (playerStamina != null)
        {
            cachedPlayerStamina = playerStamina;
            if (cachedPlayerHealth == null)
                cachedPlayerHealth = playerStamina.GetComponent<PlayerHealth>();

            bool wasEmpty = playerColliders.Count == 0;
            playerColliders.Add(collision);

            // Only apply checkpoint and safe zone state once when the first player collider enters
            if (wasEmpty)
            {
                playerStamina.SetRespawnPoint(transform.position);
                playerStamina.SetSafeZone(true);

                // Restore full health when resting at a base station
                if (cachedPlayerHealth != null && !playerStamina.IsDead)
                    cachedPlayerHealth.ResetAfterRespawn();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!playerColliders.Remove(collision))
            return;

        // Clean up any destroyed or disabled colliders that might remain in the set
        playerColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

        if (playerColliders.Count == 0 && cachedPlayerStamina != null)
        {
            cachedPlayerStamina.SetSafeZone(false);
        }
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (cachedPlayerStamina != null)
            cachedPlayerStamina.SetSafeZone(false);
    }
}