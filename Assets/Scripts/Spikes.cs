using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        BatEnemy bat = collision.GetComponentInParent<BatEnemy>();

        if (bat != null)
        {
            bat.Die();
            return;
        }

        StaminaSystem playerStamina = collision.GetComponentInParent<StaminaSystem>();
        if (playerStamina == null && collision.CompareTag("Player"))
            playerStamina = collision.GetComponent<StaminaSystem>();

        if (playerStamina != null)
        {
            playerStamina.Die();
        }
    }
}