using System.Collections.Generic;
using UnityEngine;

public class BaseStation : MonoBehaviour
{
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private StaminaSystem cachedPlayerStamina;

    // Save this base as checkpoint
    private void OnTriggerEnter2D(Collider2D collision)
    {
        StaminaSystem playerStamina = collision.GetComponentInParent<StaminaSystem>();
        if (playerStamina == null && collision.CompareTag("Player"))
            playerStamina = collision.GetComponent<StaminaSystem>();

        if (playerStamina != null)
        {
            cachedPlayerStamina = playerStamina;
            bool firstEntry = playerColliders.Count == 0;
            playerColliders.Add(collision);
            playerStamina.SetRespawnPoint(transform.position);
            playerStamina.SetSafeZone(true);
            if (firstEntry && WorldResetCoordinator.Instance != null)
                WorldResetCoordinator.Instance.PlayerEnteredBase(playerStamina);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!playerColliders.Remove(collision))
            return;

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