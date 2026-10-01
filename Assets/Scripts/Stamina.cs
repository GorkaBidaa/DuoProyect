using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    public float maxStamina = 100f;
    public float drainRate = 5f;
    public Slider staminaBar; //UI bar

    private float currentStamina;
    private Vector2 startPosition;
    private Vector2 respawnPosition;

    void Start()
    {
        currentStamina = maxStamina;
        startPosition = transform.position;
        respawnPosition = startPosition;

        if (staminaBar != null)
        {
            staminaBar.maxValue = maxStamina;
            staminaBar.value = currentStamina;
        }
    }

    void Update()
    {
        // Drain stamina continuously
        currentStamina -= drainRate * Time.deltaTime;

        if (staminaBar != null)
            staminaBar.value = currentStamina;

        if (currentStamina <= 0)
        {
            Die();
        }
    }
    public void RechargeStamina()
    {
        currentStamina = maxStamina;
    }

    public void SetRespawnPoint(Vector2 newPosition)
    {
        respawnPosition = newPosition;
    }

    public void Die()
    {
        // Teleport to the last saved respawn position
        transform.position = respawnPosition;
        currentStamina = maxStamina;

        // destroy any deployed Shadow
        var anchor = GetComponent<ShadowAnchor>();
        if (anchor != null)
        {
            anchor.ForceDestroyAnchor();
        }

    }

}