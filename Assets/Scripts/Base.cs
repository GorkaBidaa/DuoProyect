using UnityEngine;

public class BaseStation : MonoBehaviour
{
    // save this base as checkpoint
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            StaminaSystem playerStamina = collision.GetComponent<StaminaSystem>();
            if (playerStamina != null)
            {                playerStamina.SetRespawnPoint(transform.position);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            StaminaSystem playerStamina = collision.GetComponent<StaminaSystem>();
            if (playerStamina != null)
            {
                playerStamina.RechargeStamina();
            }
        }
    }
}