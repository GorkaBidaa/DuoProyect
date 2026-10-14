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
    private bool isInSafeZone = false;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private ShadowAnchor anchor;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        anchor = GetComponent<ShadowAnchor>();
    }

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
        if (isInSafeZone)
        {
            RechargeStamina();
        }
        else
        {
            // Drain stamina continuously
            currentStamina -= drainRate * Time.deltaTime;
        }

        if (staminaBar != null)
            staminaBar.value = currentStamina;

        if (currentStamina <= 0f)
        {
            Die();
        }
    }

    public void SetSafeZone(bool state)
    {
        isInSafeZone = state;
        if (state)
        {
            RechargeStamina();
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
        // destroy any deployed Shadow
        if (anchor != null)
        {
            anchor.ForceDestroyAnchor();
        }

        if (playerMovement != null)
        {
            playerMovement.ResetMovementState();
        }

        // Teleport to the last saved respawn position
        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.linearVelocity = Vector2.zero;
        }
        transform.position = respawnPosition;
        currentStamina = maxStamina;
    }
}