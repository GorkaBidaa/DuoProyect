using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShadowAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public GameObject phantomPrefab;
    public float maxTeleportRadius = 8f;
    public float RadiusForgive = 9f;
    [Tooltip("Dot minimo entre tu velocidad y la direccion a la sombra para contar como 'moviéndote hacia ella'")]
    public float forgiveDotThreshold = 0.3f;
    [Tooltip("Velocidad minima para que cuente como movimiento hacia la sombra")]
    public float forgiveMinSpeed = 0.5f;
    public KeyCode anchorKey = KeyCode.E;

    [Header("Momentum Settings")]
    public float UpMultiplier = 1f;
    public float velocityClampMax = 25f;

    [Header("Game Feel Settings")]
    public float inertiaCoyoteTime = 0.2f;
    public float hitstopDuration = 0.1f;
    public float fadeTime = 0.2f;
    public float abilityCooldown = 0.5f;

    [Header("Shadow Dash Settings")]
    [SerializeField] private bool shadowDashUnlocked = false;
    public Key shadowDashKey = Key.LeftShift;
    public Key alternateShadowDashKey = Key.R;
    public float shadowDashSpeed = 18f;
    public float shadowDashDuration = 0.25f;
    [Tooltip("Impulso horizontal que recibe el jugador si se teletransporta cortando el dash de la sombra")]
    public float shadowDashMomentumBoost = 14f;
    public LayerMask shadowObstacleLayer;

    [Header("Optional Menu References (Auto-found if left empty)")]
    [SerializeField] private InventoryMenu inventoryMenu;
    [SerializeField] private CraftingMenu craftingMenu;

    public bool ShadowDashUnlocked => shadowDashUnlocked;
    public bool IsShadowActive => ActiveShadow && currentShadow != null;

    private GameObject currentShadow;
    private SpriteRenderer shadowSr;
    private Animator shadowAnimator;
    private bool ActiveShadow = false;
    private bool isTeleporting = false;
    private Coroutine teleportCoroutine;

    private bool isShadowDashing = false;
    private bool hasShadowDashed = false;
    private Vector2 shadowFacingDirection = Vector2.right;
    private Coroutine shadowDashCoroutine;

    private float currentMaxLaunchVelocity = 0f;
    private float cachedMaxUpwardInertia = 0f;
    private float inertiaTimer = 0f;
    private float cooldownTimer = 0f;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private PlayerAnimatorController playerAnim;
    private StaminaSystem stamina;
    private SpriteRenderer playerSr;
    private Key parsedKey;
    private bool keyValid;

    // Preallocated raycast buffer so shadow dash collision checks do not allocate memory
    private readonly RaycastHit2D[] dashHitsBuffer = new RaycastHit2D[8];

    private bool IsInputBlocked =>
        (GameInput.Instance != null && GameInput.Instance.GameplayBlocked) ||
        Time.timeScale <= 0f ||
        (stamina != null && stamina.IsDead) ||
        (inventoryMenu != null && inventoryMenu.IsOpen) ||
        (craftingMenu != null && craftingMenu.IsOpen);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnim = GetComponent<PlayerAnimatorController>();
        if (playerAnim == null) playerAnim = GetComponentInChildren<PlayerAnimatorController>();

        playerSr = GetComponent<SpriteRenderer>();
        if (playerSr == null) playerSr = GetComponentInChildren<SpriteRenderer>();

        ResolveStaminaReference();
        CacheKey();
    }

    void Start()
    {
        ResolveStaminaReference();

        if (inventoryMenu == null)
            inventoryMenu = FindFirstObjectByType<InventoryMenu>();
        if (craftingMenu == null)
            craftingMenu = FindFirstObjectByType<CraftingMenu>();

        if (shadowObstacleLayer.value == 0 && playerMovement != null)
            shadowObstacleLayer = playerMovement.groundLayer;

        CacheKey();
    }

    private void ResolveStaminaReference()
    {
        if (stamina == null)
            stamina = GetComponent<StaminaSystem>();
        if (stamina == null)
            stamina = GetComponentInParent<StaminaSystem>();
        if (stamina == null)
            stamina = FindFirstObjectByType<StaminaSystem>();
    }

    void OnValidate()
    {
        CacheKey();
    }

    void OnDisable()
    {
        StopShadowDash();
        RestoreTeleportStateIfInterrupted();
    }

    private void RestoreTeleportStateIfInterrupted()
    {
        if (!isTeleporting) return;

        Time.timeScale = 1f;
        isTeleporting = false;
        teleportCoroutine = null;

        if (playerSr != null)
        {
            Color c = playerSr.color;
            c.a = 1f;
            playerSr.color = c;
        }

        if (playerMovement != null)
            playerMovement.SetTeleportingState(false);
    }

    public void UnlockShadowDash()
    {
        shadowDashUnlocked = true;
    }

    private void CacheKey()
    {
        if (System.Enum.TryParse(anchorKey.ToString(), true, out parsedKey))
        {
            keyValid = parsedKey != Key.None;
            return;
        }

        // Fallback for KeyCode names that differ in the new Input System
        parsedKey = anchorKey switch
        {
            KeyCode.Return => Key.Enter,
            KeyCode.LeftControl => Key.LeftCtrl,
            KeyCode.RightControl => Key.RightCtrl,
            KeyCode.LeftCommand => Key.LeftMeta,
            KeyCode.RightCommand => Key.RightMeta,
            KeyCode.Alpha0 => Key.Digit0,
            KeyCode.Alpha1 => Key.Digit1,
            KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3,
            KeyCode.Alpha4 => Key.Digit4,
            KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6,
            KeyCode.Alpha7 => Key.Digit7,
            KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,
            _ => Key.None
        };
        keyValid = parsedKey != Key.None;
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (isTeleporting)
            return; // Ignore input while teleporting

        TrackMomentum();

        if (ActiveShadow)
        {
            if (currentShadow != null)
            {
                UpdateShadowColor();
            }
            else
            {
                ActiveShadow = false;
                hasShadowDashed = false;
                isShadowDashing = false;
            }
        }

        if (IsInputBlocked)
            return;

        // Pressing R2 / the anchor key has priority for deploying the anchor or interrupting the dash with teleport
        if (ReadAnchorInput())
        {
            if (!ActiveShadow)
            {
                if (cooldownTimer <= 0f)
                    DeployAnchor();
            }
            else
            {
                Teleport();
            }
            return;
        }

        // The player can dash only if the ability is unlocked, a shadow is active, and no dash has been used yet
        if (shadowDashUnlocked && ActiveShadow && currentShadow != null && !isShadowDashing && !hasShadowDashed && ReadShadowDashInput())
        {
            shadowDashCoroutine = StartCoroutine(ShadowDashRoutine(shadowFacingDirection));
        }
    }

    private void TrackMomentum()
    {
        if (rb == null) return;

        float vy = rb.linearVelocity.y;

        if (vy > 0.1f)
        {
            if (vy > cachedMaxUpwardInertia) cachedMaxUpwardInertia = vy;
            if (vy > currentMaxLaunchVelocity) currentMaxLaunchVelocity = vy;
            inertiaTimer = inertiaCoyoteTime;
        }
        else
        {
            if (playerMovement != null && playerMovement.isGrounded && vy <= 0.05f)
            {
                currentMaxLaunchVelocity = 0f;
            }

            inertiaTimer -= Time.deltaTime;
            if (inertiaTimer <= 0f)
            {
                cachedMaxUpwardInertia = 0f;
            }
        }
    }

    private bool ReadAnchorInput()
    {
        if (GameInput.Instance != null && !GameInput.Instance.GameplayBlocked && GameInput.Instance.Pressed("Anchor"))
            return true;

        var gp = Gamepad.current;
        if (gp != null && gp.rightTrigger.wasPressedThisFrame)
            return true;

        var kb = Keyboard.current;
        if (kb != null && keyValid && kb[parsedKey].wasPressedThisFrame)
            return true;

        return false;
    }

    private bool ReadShadowDashInput()
    {
        if (GameInput.Instance != null && !GameInput.Instance.GameplayBlocked && GameInput.Instance.Pressed("ShadowDash"))
            return true;

        var gp = Gamepad.current;
        if (gp != null && gp.leftTrigger.wasPressedThisFrame)
            return true;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (shadowDashKey != Key.None && kb[shadowDashKey].wasPressedThisFrame)
                return true;
            if (alternateShadowDashKey != Key.None && kb[alternateShadowDashKey].wasPressedThisFrame)
                return true;
        }

        return false;
    }

    // Single source of truth for "can teleport?" (the color and teleport use the same check)
    private bool IsReachable()
    {
        if (currentShadow == null) return false;

        Vector2 toShadow = currentShadow.transform.position - transform.position;
        float dist = toShadow.magnitude;
        float effectiveForgiveRadius = Mathf.Max(RadiusForgive, maxTeleportRadius);

        if (dist <= maxTeleportRadius) return true;
        if (dist > effectiveForgiveRadius) return false;

        // Forgiveness zone: only when moving toward the shadow
        if (rb == null) return false;
        Vector2 vel = rb.linearVelocity;
        if (vel.magnitude < forgiveMinSpeed) return false;

        float dot = Vector2.Dot(vel.normalized, toShadow.normalized);
        return dot > forgiveDotThreshold;
    }

    private void UpdateShadowColor()
    {
        if (shadowSr == null) return;
        shadowSr.color = IsReachable()
            ? new Color32(127, 0, 247, 200)
            : new Color(1f, 0.3f, 0.3f, 0.5f);
    }

    private bool ResolvePlayerFacingLeft()
    {
        if (playerAnim != null)
            return playerAnim.FacingLeft;
        if (playerMovement != null && Mathf.Abs(playerMovement.HorizontalInput) > 0.01f)
            return playerMovement.HorizontalInput < 0f;
        if (playerSr != null)
            return playerSr.flipX;
        return transform.lossyScale.x < 0f;
    }

    private void DeployAnchor()
    {
        if (phantomPrefab == null)
        {
            Debug.LogWarning("[ShadowAnchor] No hay 'phantomPrefab' asignado en el Inspector.", this);
            return;
        }

        if (playerAnim != null)
            playerAnim.TriggerCastShadow();

        if (currentShadow != null)
            Destroy(currentShadow);

        currentShadow = Instantiate(phantomPrefab, transform.position, Quaternion.identity);
        shadowSr = currentShadow.GetComponent<SpriteRenderer>();
        if (shadowSr == null) shadowSr = currentShadow.GetComponentInChildren<SpriteRenderer>();

        shadowAnimator = currentShadow.GetComponent<Animator>();
        if (shadowAnimator == null) shadowAnimator = currentShadow.GetComponentInChildren<Animator>();

        bool facingLeft = ResolvePlayerFacingLeft();

        if (shadowSr != null)
            shadowSr.flipX = facingLeft;

        // Store the fixed horizontal direction when deploying the shadow
        shadowFacingDirection = facingLeft ? Vector2.left : Vector2.right;
        hasShadowDashed = false;
        isShadowDashing = false;
        ActiveShadow = true;
        UpdateShadowColor();

        // Consume stamina when deploying the shadow anchor
        ResolveStaminaReference();
        if (stamina != null)
            stamina.ConsumeStamina(stamina.anchorDeployCost);
    }

    private void StopShadowDash()
    {
        if (shadowDashCoroutine != null)
        {
            StopCoroutine(shadowDashCoroutine);
            shadowDashCoroutine = null;
        }

        if (shadowAnimator != null && currentShadow != null)
            shadowAnimator.SetBool("IsDashing", false);

        isShadowDashing = false;
    }

    private void Teleport()
    {
        if (currentShadow == null)
        {
            ActiveShadow = false;
            return;
        }

        // Check whether the player was dashing at this exact moment before stopping it
        bool interruptedDash = isShadowDashing;
        StopShadowDash();

        cooldownTimer = abilityCooldown;
        ResolveStaminaReference();

        if (IsReachable())
        {
            // Consume stamina on a successful teleport
            if (stamina != null)
            {
                stamina.ConsumeStamina(stamina.teleportStaminaCost);
                if (stamina.IsDead)
                {
                    ForceDestroyAnchor();
                    return;
                }
            }

            float horizontalBoost = interruptedDash
                ? shadowFacingDirection.x * shadowDashMomentumBoost
                : 0f;

            if (teleportCoroutine != null)
                StopCoroutine(teleportCoroutine);

            teleportCoroutine = StartCoroutine(
                TeleportRoutine(currentShadow.transform.position, horizontalBoost));
        }
        else
        {
            // Consume half teleport stamina cost when attempting to teleport out of range
            if (stamina != null)
                stamina.ConsumeStamina(stamina.teleportStaminaCost * 0.5f);

            GameObject toDestroy = currentShadow;
            currentShadow = null;
            shadowSr = null;
            shadowAnimator = null;
            ActiveShadow = false;
            hasShadowDashed = false;
            StartCoroutine(FailAndDestroy(toDestroy));
        }
    }

    private IEnumerator TeleportRoutine(Vector3 targetPosition, float horizontalLaunchVelocity)
    {
        isTeleporting = true;
        float prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

        // Snapshot momentum before fade starts
        float savedUpwardInertia = cachedMaxUpwardInertia;
        float savedMaxLaunchVel = Mathf.Max(currentMaxLaunchVelocity, savedUpwardInertia);
        cachedMaxUpwardInertia = 0f;
        inertiaTimer = 0f;

        if (playerMovement != null)
            playerMovement.SetTeleportingState(true);

        // Fade out
        yield return FadePlayer(1f, 0f);

        // Abort safely if the player died during fade out
        if (stamina != null && stamina.IsDead)
        {
            if (currentShadow != null) Destroy(currentShadow);
            currentShadow = null;
            shadowSr = null;
            shadowAnimator = null;
            ActiveShadow = false;
            hasShadowDashed = false;
            isShadowDashing = false;
            RestoreTeleportStateIfInterrupted();
            yield break;
        }

        // If the shadow was still valid at the moment of arrival, update targetPosition in case it moved
        if (currentShadow != null)
        {
            targetPosition = currentShadow.transform.position;
            Destroy(currentShadow);
        }

        currentShadow = null;
        shadowSr = null;
        shadowAnimator = null;
        ActiveShadow = false;
        hasShadowDashed = false;
        isShadowDashing = false;

        // Move (using rb.position keeps physics synchronized)
        if (rb != null)
        {
            rb.position = targetPosition;
            rb.linearVelocity = Vector2.zero;
        }
        transform.position = targetPosition;

        // Fade in
        yield return FadePlayer(0f, 1f);

        // Hitstop
        if (hitstopDuration > 0f)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitstopDuration);
        }

        // Restore timeScale and exit teleport state BEFORE applying the launch velocity
        Time.timeScale = prevTimeScale;
        isTeleporting = false;
        teleportCoroutine = null;

        if (playerSr != null)
        {
            Color c = playerSr.color;
            c.a = 1f;
            playerSr.color = c;
        }

        if (playerMovement != null)
            playerMovement.SetTeleportingState(false);

        // Apply launch velocity now that normal physics and gravity are active again
        if (rb != null && (stamina == null || !stamina.IsDead))
        {
            float appliedVelocityY = savedUpwardInertia > 0f
                ? savedUpwardInertia * UpMultiplier
                : 0f;

            float dynamicMaxLimit = Mathf.Min(savedMaxLaunchVel * UpMultiplier, velocityClampMax);
            appliedVelocityY = Mathf.Clamp(appliedVelocityY, 0f, dynamicMaxLimit);

            float finalVelX = Mathf.Abs(horizontalLaunchVelocity) > 0.01f
                ? horizontalLaunchVelocity
                : rb.linearVelocity.x;

            rb.WakeUp();
            rb.linearVelocity = new Vector2(finalVelX, appliedVelocityY);
        }
    }

    private IEnumerator FadePlayer(float from, float to)
    {
        if (playerSr == null) yield break;

        Color col = playerSr.color;
        if (fadeTime <= 0.001f)
        {
            col.a = to;
            playerSr.color = col;
            yield break;
        }

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            col.a = Mathf.Lerp(from, to, t / fadeTime);
            playerSr.color = col;
            yield return null;
        }

        col.a = to;
        playerSr.color = col;
    }

    private IEnumerator FailAndDestroy(GameObject shadowToDestroy)
    {
        if (shadowToDestroy == null) yield break;

        SpriteRenderer sr = shadowToDestroy.GetComponent<SpriteRenderer>();
        if (sr == null) sr = shadowToDestroy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color32(255, 0, 0, 200);

        yield return new WaitForSeconds(0.3f);

        if (shadowToDestroy != null) Destroy(shadowToDestroy);
    }

    public void ForceDestroyAnchor()
    {
        StopShadowDash();

        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
            teleportCoroutine = null;
        }

        RestoreTeleportStateIfInterrupted();

        cachedMaxUpwardInertia = 0f;
        currentMaxLaunchVelocity = 0f;
        inertiaTimer = 0f;

        if (currentShadow != null) Destroy(currentShadow);
        currentShadow = null;
        shadowSr = null;
        shadowAnimator = null;
        ActiveShadow = false;
        hasShadowDashed = false;
    }

    private IEnumerator ShadowDashRoutine(Vector2 direction)
    {
        if (currentShadow == null) yield break;

        isShadowDashing = true;
        hasShadowDashed = true;

        // Consume stamina when triggering the shadow dash
        ResolveStaminaReference();
        if (stamina != null)
        {
            stamina.ConsumeStamina(stamina.shadowDashStaminaCost);
            if (stamina.IsDead)
            {
                ForceDestroyAnchor();
                yield break;
            }
        }

        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;

        if (shadowSr != null && Mathf.Abs(direction.x) > 0.01f)
            shadowSr.flipX = direction.x < 0f;

        if (shadowAnimator != null)
            shadowAnimator.SetBool("IsDashing", true);

        LayerMask effectiveObstacleMask = shadowObstacleLayer.value != 0
            ? shadowObstacleLayer
            : (playerMovement != null ? playerMovement.groundLayer : Physics2D.DefaultRaycastLayers);

        const float wallSkinWidth = 0.2f;
        float elapsed = 0f;

        while (elapsed < shadowDashDuration && currentShadow != null)
        {
            float step = shadowDashSpeed * Time.deltaTime;

            // Cast from the visual center of the shadow so a bottom-aligned pivot doesn't scrape the floor
            Vector2 rayOrigin = shadowSr != null
                ? (Vector2)shadowSr.bounds.center
                : (Vector2)currentShadow.transform.position;

            int hitCount = Physics2D.RaycastNonAlloc(
                rayOrigin,
                direction,
                dashHitsBuffer,
                step + wallSkinWidth,
                effectiveObstacleMask);

            // Find the closest solid non-trigger wall obstacle opposing the dash direction
            float closestHitDistance = float.PositiveInfinity;
            bool hitSolidWall = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = dashHitsBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger)
                    continue;

                Transform hitTransform = hit.collider.transform;
                // Ignore both the player and the shadow's own colliders
                if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(currentShadow.transform))
                    continue;

                // Only stop on surfaces that actually oppose the horizontal dash direction (ignore flat floor/ceiling)
                if (Vector2.Dot(hit.normal, direction) > -0.25f)
                    continue;

                if (hit.distance < closestHitDistance)
                {
                    closestHitDistance = hit.distance;
                    hitSolidWall = true;
                }
            }

            if (hitSolidWall)
            {
                float safeAdvance = Mathf.Max(0f, closestHitDistance - wallSkinWidth);
                currentShadow.transform.position += (Vector3)(direction * safeAdvance);
                break;
            }

            currentShadow.transform.position += (Vector3)(direction * step);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (shadowAnimator != null && currentShadow != null)
            shadowAnimator.SetBool("IsDashing", false);

        isShadowDashing = false;
        shadowDashCoroutine = null;
    }
}