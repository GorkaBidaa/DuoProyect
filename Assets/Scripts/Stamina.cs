using System.Collections;
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
    private PlayerAnimatorController animatorController;

    public bool IsInSafeZone => isInSafeZone;
    public bool IsDead { get; private set; }

    [Header("Animation")]
    [SerializeField] private float deathAnimDuration = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        anchor = GetComponent<ShadowAnchor>();
        animatorController = GetComponent<PlayerAnimatorController>();
    }

    public void Die()
    {
        if (IsDead)
            return;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        IsDead = true;

        // Destroy shadow anchor if exists
        if (anchor != null)
            anchor.ForceDestroyAnchor();

        // Reset and disable movement
        if (playerMovement != null)
        {
            playerMovement.ResetMovementState();
            playerMovement.enabled = false; // Block movement during animation
        }

        // Freeze physics while death animation plays
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        // Trigger death animation through the animator controller
        if (animatorController != null)
        {
            animatorController.TriggerDeath();
        }

        // Wait for death animation to complete
        yield return new WaitForSeconds(deathAnimDuration);

        // Respawn player
        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.linearVelocity = Vector2.zero;
        }
        transform.position = respawnPosition;
        Physics2D.SyncTransforms();

        // Reset stamina
        RechargeStamina();

        // Reset health
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
            health.ResetAfterRespawn();

        // Reset animator back to default state
        if (animatorController != null)
            animatorController.ResetAfterRespawn();

        // Re-enable movement and restore base gravity
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.ResetMovementState();
        }

        IsDead = false;
    }

    private void Start()
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

    private void Update()
    {
        // Don't drain stamina while dead
        if (IsDead)
            return;

        if (isInSafeZone)
        {
            RechargeStamina();
        }
        else
        {
            // Drain stamina continuously
            currentStamina = Mathf.Max(0f, currentStamina - drainRate * Time.deltaTime);
        }

        if (staminaBar != null)
            staminaBar.value = currentStamina;

        if (currentStamina <= 0f)
        {
            Die();
        }
    }

    // Method to update the stamina text in the UI
    private void LateUpdate()
    {
        if (staminaText == null)
            return;

        int current = Mathf.CeilToInt(
            Mathf.Clamp(currentStamina, 0f, maxStamina));
        int maximum = Mathf.CeilToInt(maxStamina);
        staminaText.text = $"{current} / {maximum}";
    }

    public bool TryRestoreStamina(float amount)
    {
        if (IsDead || amount <= 0f || currentStamina >= maxStamina)
            return false;

        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);

        if (staminaBar != null)
            staminaBar.value = currentStamina;

        return true;
    }

    public void SetSafeZone(bool state)
    {
        isInSafeZone = state;
        if (state && !IsDead)
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
}