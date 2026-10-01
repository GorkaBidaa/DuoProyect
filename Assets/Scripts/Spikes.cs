using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
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