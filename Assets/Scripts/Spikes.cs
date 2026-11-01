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
        if (collision.CompareTag("Player"))
        {
            StaminaSystem playerStamina = collision.GetComponent<StaminaSystem>();

            if (playerStamina != null)
            {
                playerStamina.Die();
            }
        }
    }
}