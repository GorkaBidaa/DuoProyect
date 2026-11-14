using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaSystem : MonoBehaviour
{
    [Header("Playtest / Debug")]
    [Tooltip("When enabled, the player cannot die from stamina depletion, health loss, spikes, or enemies")]
    public bool isImmortal = false;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    [Tooltip("Very slow passive stamina drain per second when idle (calibrated for ~100s total run with actions)")]
    public float drainRate = 0.35f;
    [Tooltip("Additional stamina drain per second while moving horizontally")]
    public float moveDrainRate = 0.25f;
    [Tooltip("Additional stamina drain per second while wall sliding")]
    public float wallSlideDrainRate = 0.35f;
    [Tooltip("Additional stamina drain per second while a shadow anchor is active in the world")]
    public float activeShadowDrainRate = 0.45f;

    [Header("Action Stamina Costs")]
    [Tooltip("Stamina consumed each time the player jumps or wall jumps")]
    public float jumpStaminaCost = 1.5f;
    [Tooltip("Stamina consumed when deploying the shadow anchor")]
    public float anchorDeployCost = 5.0f;
    [Tooltip("Stamina consumed when teleporting to the shadow anchor")]
    public float teleportStaminaCost = 8.0f;
    [Tooltip("Stamina consumed when triggering the shadow dash")]
    public float shadowDashStaminaCost = 6.0f;

    [Header("UI References")]
    public Slider staminaBar; // UI bar

    [SerializeField] private TMP_Text staminaText; // UI text
    [SerializeField] private PlayerHUD playerHud;

    [Header("Low Stamina Danger Effect")]
    [Tooltip("Normalized stamina percentage (0-1) below which the screen edge shadows appear")]
    [SerializeField, Range(0.05f, 0.8f)] private float lowStaminaThreshold = 0.35f;
    [Tooltip("Maximum opacity of the screen edge shadows when stamina reaches 0")]
    [SerializeField, Range(0.1f, 1f)] private float maxDangerAlpha = 0.88f;
    [Tooltip("Base pulsing speed of the danger shadows")]
    [SerializeField, Min(0.5f)] private float dangerPulseSpeed = 3.5f;
    [Tooltip("Pulse intensity added to the vignette opacity")]
    [SerializeField, Range(0f, 0.5f)] private float dangerPulseAmplitude = 0.18f;
    [Tooltip("Color of the low stamina danger vignette on the sides of the screen")]
    [SerializeField] private Color dangerShadowColor = new Color(0.04f, 0f, 0.08f, 1f);
    [Tooltip("Optional custom UI Image for the danger overlay. Auto-generated on Canvas if left empty")]
    [SerializeField] private Image dangerOverlayImage;

    private float currentStamina;
    private float currentDangerAlpha;
    private Texture2D generatedVignetteTexture;
    private Sprite generatedVignetteSprite;
    private Vector2 startPosition;
    private Vector2 respawnPosition;
    private bool isInSafeZone = false;

    // Cached integer values to avoid allocating a new UI string every frame in LateUpdate
    private int lastDisplayedCurrent = -1;
    private int lastDisplayedMax = -1;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private ShadowAnchor anchor;
    private PlayerAnimatorController animatorController;
    private PlayerHealth playerHealth;

    public bool IsInSafeZone => isInSafeZone;
    public bool IsDead { get; private set; }
    public bool IsRespawnArrival { get; private set; }
    public event System.Action Respawned;
    public float CurrentStamina => currentStamina;

    [Header("Animation")]
    [SerializeField] private float deathAnimDuration = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        anchor = GetComponent<ShadowAnchor>();
        animatorController = GetComponent<PlayerAnimatorController>();
        playerHealth = GetComponent<PlayerHealth>();

        // Upgrade legacy low serialized Inspector values so shadow abilities visibly drain the stamina bar
        if (Mathf.Approximately(anchorDeployCost, 1.2f)) anchorDeployCost = 5.0f;
        if (Mathf.Approximately(teleportStaminaCost, 2.3f)) teleportStaminaCost = 8.0f;
        if (Mathf.Approximately(shadowDashStaminaCost, 2.0f)) shadowDashStaminaCost = 6.0f;
    }

    public void Die()
    {
        if (IsDead || isImmortal)
            return;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        IsDead = true;
        IsRespawnArrival = true;

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
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.ResetAfterRespawn();

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
        Respawned?.Invoke();
        // Give trigger contacts time to recognize the teleport into the checkpoint.
        // During these steps base entry still restores safety, but never resources.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        IsRespawnArrival = false;
    }

    private void Start()
    {
        currentStamina = maxStamina;
        startPosition = transform.position;
        respawnPosition = startPosition;

        if (playerHud == null)
            playerHud = FindFirstObjectByType<PlayerHUD>();

        EnsureDangerOverlay();
        UpdateStaminaUI();
        UpdateDangerEffect(true);
    }

    private void Update()
    {
        // Don't drain stamina while dead
        if (IsDead)
        {
            UpdateDangerEffect(false);
            return;
        }

        if (isInSafeZone)
        {
            RechargeStamina();
        }
        else
        {
            // Drain stamina continuously at a slow base rate plus movement, wall-slide, and active shadow exertion
            float totalDrainRate = drainRate;

            if (playerMovement != null)
            {
                if (Mathf.Abs(playerMovement.HorizontalInput) > 0.01f)
                    totalDrainRate += moveDrainRate;

                if (playerMovement.IsWallSliding)
                    totalDrainRate += wallSlideDrainRate;
            }

            if (anchor != null && anchor.IsShadowActive)
            {
                totalDrainRate += activeShadowDrainRate;
            }

            currentStamina = Mathf.Max(0f, currentStamina - totalDrainRate * Time.deltaTime);
        }

        UpdateStaminaUI();
        UpdateDangerEffect(false);

        if (currentStamina <= 0f && !isImmortal)
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

        // Only allocate a new string when the displayed integer values change
        if (current != lastDisplayedCurrent || maximum != lastDisplayedMax)
        {
            lastDisplayedCurrent = current;
            lastDisplayedMax = maximum;
            staminaText.text = $"{current} / {maximum}";
        }
    }

    // Consumes an instant amount of stamina when performing actions or abilities
    public void ConsumeStamina(float amount)
    {
        if (IsDead || isInSafeZone || amount <= 0f)
            return;

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        UpdateStaminaUI();
        UpdateDangerEffect(false);

        if (currentStamina <= 0f && !isImmortal)
        {
            Die();
        }
    }

    public bool TryRestoreStamina(float amount)
    {
        if (IsDead || amount <= 0f || currentStamina >= maxStamina)
            return false;

        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
        UpdateStaminaUI();

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

    public void SetMaxStamina(float value)
    {
        float oldMaximum = maxStamina;
        maxStamina = Mathf.Max(1f, value);
        if (!IsDead) currentStamina = Mathf.Clamp(currentStamina + maxStamina - oldMaximum, 0f, maxStamina);
        UpdateStaminaUI();
        UpdateDangerEffect(false);
    }

    public void RechargeStamina()
    {
        currentStamina = maxStamina;
        UpdateStaminaUI();
    }

    public void SetRespawnPoint(Vector2 newPosition)
    {
        respawnPosition = newPosition;
    }

    private void UpdateStaminaUI()
    {
        if (staminaBar != null)
        {
            // Always synchronize Slider bounds with maxStamina so the bar scales properly
            staminaBar.minValue = 0f;
            staminaBar.maxValue = maxStamina;
            staminaBar.value = currentStamina;
        }

        if (playerHud != null)
            playerHud.SetStamina(currentStamina, maxStamina);
    }

    private void UpdateDangerEffect(bool instant)
    {
        if (dangerOverlayImage == null)
            return;

        float normalized = maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
        float targetAlpha = 0f;

        if (!isInSafeZone && normalized < lowStaminaThreshold)
        {
            // Severity scales from 0 (at threshold) to 1 (at 0 stamina)
            float severity = 1f - Mathf.Clamp01(normalized / Mathf.Max(0.001f, lowStaminaThreshold));
            float dynamicPulseSpeed = Mathf.Lerp(dangerPulseSpeed, dangerPulseSpeed * 2.2f, severity);
            float pulse = (Mathf.Sin(Time.unscaledTime * dynamicPulseSpeed) * 0.5f + 0.5f) * dangerPulseAmplitude * severity;
            targetAlpha = Mathf.Clamp01((severity * maxDangerAlpha) + pulse);
        }

        currentDangerAlpha = instant
            ? targetAlpha
            : Mathf.MoveTowards(currentDangerAlpha, targetAlpha, Time.unscaledDeltaTime * 3.5f);

        Color c = dangerShadowColor;
        c.a = currentDangerAlpha;
        dangerOverlayImage.color = c;
        dangerOverlayImage.enabled = currentDangerAlpha > 0.001f;
    }

    // Creates a full-screen edge shadow vignette automatically if none is assigned in the Inspector
    private void EnsureDangerOverlay()
    {
        if (dangerOverlayImage == null)
        {
            Canvas targetCanvas = staminaBar != null
                ? staminaBar.GetComponentInParent<Canvas>()
                : FindFirstObjectByType<Canvas>();

            if (targetCanvas == null)
            {
                GameObject canvasObj = new GameObject("LowStaminaDangerCanvas");
                targetCanvas = canvasObj.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 999;
                canvasObj.AddComponent<CanvasScaler>();
            }

            GameObject overlayObj = new GameObject("LowStaminaDangerVignette");
            overlayObj.transform.SetParent(targetCanvas.transform, false);
            overlayObj.transform.SetAsFirstSibling();

            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            dangerOverlayImage = overlayObj.AddComponent<Image>();
            dangerOverlayImage.raycastTarget = false;
        }

        if (dangerOverlayImage.sprite == null)
        {
            dangerOverlayImage.sprite = CreateVignetteSprite();
        }
    }

    // Generates a smooth procedural vignette texture with stronger shadows on the left and right edges
    private Sprite CreateVignetteSprite()
    {
        const int size = 256;
        generatedVignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y / (float)(size - 1)) * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float u = (x / (float)(size - 1)) * 2f - 1f;

                // Emphasize horizontal screen sides while framing corners smoothly
                float sideDist = Mathf.Abs(u);
                float radialDist = Mathf.Sqrt(u * u * 1.25f + v * v * 0.75f);
                float combined = Mathf.Max(sideDist * 0.95f, radialDist * 0.85f);

                float edgeFactor = Mathf.SmoothStep(0.42f, 1.0f, combined);
                float alpha = Mathf.Pow(edgeFactor, 1.75f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        generatedVignetteTexture.SetPixels(pixels);
        generatedVignetteTexture.Apply();

        generatedVignetteSprite = Sprite.Create(
            generatedVignetteTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f));

        return generatedVignetteSprite;
    }

    private void OnDestroy()
    {
        if (generatedVignetteSprite != null)
            Destroy(generatedVignetteSprite);
        if (generatedVignetteTexture != null)
            Destroy(generatedVignetteTexture);
    }
}