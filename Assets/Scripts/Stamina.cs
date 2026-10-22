using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class StaminaSystem : MonoBehaviour
{
    public float maxStamina = 100f;
    public float drainRate = 5f;
    public Slider staminaBar; // UI bar
    [SerializeField] private TMP_Text staminaText; // UI text

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

    //Method to update the stamina text in the UI
    private void LateUpdate()
    {
        if (staminaText == null)
            return;

        int current = Mathf.CeilToInt(
            Mathf.Clamp(currentStamina, 0f, maxStamina));
        int maximum = Mathf.CeilToInt(maxStamina);

        staminaText.text = $"{current} / {maximum}";
    }

    // MÉTODO QUE FALTABA PARA QUE FoodUse FUNCIONE 
    public bool TryRestoreStamina(float amount)
    {
        if (amount <= 0f || currentStamina >= maxStamina)
            return false;

        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);

        if (staminaBar != null)
            staminaBar.value = currentStamina;

        return true;
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

        if (staminaBar != null)
            staminaBar.value = currentStamina;
    }

    public void SetRespawnPoint(Vector2 newPosition)
    {
        respawnPosition = newPosition;
    }

    public void Die()
    {
        // Destroy any deployed Shadow
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

        //Reset health after respawn
        PlayerHealth health = GetComponent<PlayerHealth>();

        if (health != null)
            health.ResetAfterRespawn();
    }
}