using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision) => HandleSpikeContact(collision);
    private void OnTriggerStay2D(Collider2D collision) => HandleSpikeContact(collision);

    private void HandleSpikeContact(Collider2D collision)
    {
        if (collision == null)
            return;

        BatEnemy bat = collision.GetComponentInParent<BatEnemy>();

        if (bat != null)
        {
            bat.Die();
            return;
        }

        // Also support any other enemy implementing ITrapTarget
        ITrapTarget trapTarget = collision.GetComponentInParent<ITrapTarget>();
        if (trapTarget != null && trapTarget.IsAlive)
        {
            trapTarget.Die();
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