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
    public float shadowDashSpeed = 18f;
    public float shadowDashDuration = 0.25f;
    [Tooltip("Impulso horizontal que recibe el jugador si se teletransporta cortando el dash de la sombra")]
    public float shadowDashMomentumBoost = 14f;
    public LayerMask shadowObstacleLayer;

    public bool ShadowDashUnlocked => shadowDashUnlocked;

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
    private SpriteRenderer playerSr;
    private Key parsedKey;
    private bool keyValid;

    void Start()
    {
        playerAnim = GetComponent<PlayerAnimatorController>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerSr = GetComponent<SpriteRenderer>();
        if (playerSr == null) playerSr = GetComponentInChildren<SpriteRenderer>();
        CacheKey();
    }

    void OnValidate() { CacheKey(); }

    void OnDisable()
    {
        StopShadowDash();

        // Prevent the game from freezing or the player from remaining invisible if disabled during teleport
        if (isTeleporting)
        {
            Time.timeScale = 1f;
            isTeleporting = false;
            if (playerSr != null)
            {
                Color c = playerSr.color; c.a = 1f; playerSr.color = c;
            }
            if (playerMovement != null) playerMovement.SetTeleportingState(false);
        }
    }

    public void UnlockShadowDash()
    {
        shadowDashUnlocked = true;
    }

    private void CacheKey()
    {
        if (System.Enum.TryParse(anchorKey.ToString(), true, out parsedKey))
        {
            keyValid = true;
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
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (isTeleporting) return; // Ignore input while teleporting

        TrackMomentum();

        if (ActiveShadow && currentShadow != null)
        {
            UpdateShadowColor();
        }

        // Pressing R2 / the anchor key has priority for deploying the anchor or interrupting the dash with teleport
        if (ReadAnchorInput())
        {
            if (!ActiveShadow)
            {
                if (cooldownTimer <= 0f) DeployAnchor();
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
            inertiaTimer -= Time.deltaTime;
            if (inertiaTimer <= 0f) cachedMaxUpwardInertia = 0f;

            if (playerMovement != null && playerMovement.isGrounded)
            {
                currentMaxLaunchVelocity = 0f;
            }
        }
    }

    private bool ReadAnchorInput()
    {
        var gp = Gamepad.current;
        if (gp != null && (gp.rightTrigger.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame))
            return true;

        var kb = Keyboard.current;
        if (kb != null && keyValid && kb[parsedKey].wasPressedThisFrame)
            return true;

        return false;
    }

    private bool ReadShadowDashInput()
    {
        var gp = Gamepad.current;
        if (gp != null && gp.leftTrigger.wasPressedThisFrame)
            return true;

        var kb = Keyboard.current;
        if (kb != null && kb[shadowDashKey].wasPressedThisFrame)
            return true;

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
            ? new Color32(127, 0, 247, 80)
            : new Color(1f, 0.3f, 0.3f, 0.5f);
    }

    private void DeployAnchor()
    {
        if (playerAnim == null)
            Debug.LogError("ShadowAnchor: playerAnim is NULL");
        if (phantomPrefab == null)
        {
            Debug.LogWarning("ShadowAnchor: there is no phantomPrefab.");
            return;
        }

        if (playerAnim != null) playerAnim.TriggerCastShadow();

        currentShadow = Instantiate(phantomPrefab, transform.position, Quaternion.identity);
        shadowSr = currentShadow.GetComponent<SpriteRenderer>();
        shadowAnimator = currentShadow.GetComponent<Animator>();

        bool facingLeft = playerAnim != null
            ? playerAnim.FacingLeft
            : (playerSr != null && playerSr.flipX);

        if (shadowSr != null)
            shadowSr.flipX = facingLeft;

        // Store the fixed horizontal direction when deploying the shadow
        shadowFacingDirection = facingLeft ? Vector2.left : Vector2.right;
        hasShadowDashed = false;
        isShadowDashing = false;
        ActiveShadow = true;
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

        if (IsReachable())
        {
            float horizontalBoost = interruptedDash
                ? shadowFacingDirection.x * shadowDashMomentumBoost
                : 0f;

            teleportCoroutine = StartCoroutine(
                TeleportRoutine(currentShadow.transform.position, horizontalBoost));
        }
        else
        {
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
        float prevTimeScale = Time.timeScale;

        // Snapshot momentum before fade starts
        float savedUpwardInertia = cachedMaxUpwardInertia;
        float savedMaxLaunchVel = currentMaxLaunchVelocity;
        cachedMaxUpwardInertia = 0f;
        inertiaTimer = 0f;

        try
        {
            if (playerMovement != null) playerMovement.SetTeleportingState(true);

            // Fade out
            yield return FadePlayer(1f, 0f);

            // Move (using rb.position keeps physics synchronized)
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.linearVelocity = Vector2.zero;
            }
            transform.position = targetPosition;

            if (currentShadow != null) Destroy(currentShadow);
            currentShadow = null;
            shadowSr = null;
            shadowAnimator = null;
            ActiveShadow = false;
            hasShadowDashed = false;

            // Fade in
            yield return FadePlayer(0f, 1f);

            // Hitstop
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitstopDuration);

            // Apply launch velocity after fade and hitstop
            if (rb != null)
            {
                float appliedVelocityY = savedUpwardInertia > 0f
                    ? savedUpwardInertia * UpMultiplier
                    : 0f;

                float dynamicMaxLimit = Mathf.Min(savedMaxLaunchVel * UpMultiplier, velocityClampMax);
                appliedVelocityY = Mathf.Clamp(appliedVelocityY, 0f, dynamicMaxLimit);

                rb.linearVelocity = new Vector2(horizontalLaunchVelocity, appliedVelocityY);
            }
        }
        finally
        {
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            isTeleporting = false;
            teleportCoroutine = null;
            if (playerMovement != null) playerMovement.SetTeleportingState(false);
        }
    }

    private IEnumerator FadePlayer(float from, float to)
    {
        if (playerSr == null) yield break;

        Color col = playerSr.color;
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
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
        if (sr != null) sr.color = new Color32(255, 0, 0, 128);

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

        if (isTeleporting)
        {
            Time.timeScale = 1f;
            isTeleporting = false;
            if (playerSr != null)
            {
                Color c = playerSr.color;
                c.a = 1f;
                playerSr.color = c;
            }
            if (playerMovement != null) playerMovement.SetTeleportingState(false);
        }

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
        isShadowDashing = true;
        hasShadowDashed = true;

        if (shadowSr != null && Mathf.Abs(direction.x) > 0.01f)
            shadowSr.flipX = direction.x < 0f;

        if (shadowAnimator != null)
            shadowAnimator.SetBool("IsDashing", true);

        float elapsed = 0f;
        while (elapsed < shadowDashDuration && currentShadow != null)
        {
            float step = shadowDashSpeed * Time.deltaTime;
            RaycastHit2D hit = Physics2D.Raycast(currentShadow.transform.position, direction, step + 0.2f, shadowObstacleLayer);
            if (hit.collider != null)
                break;

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