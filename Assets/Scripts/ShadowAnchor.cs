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

    private GameObject currentShadow;
    private SpriteRenderer shadowSr;
    private bool ActiveShadow = false;
    private bool isTeleporting = false;
    private Coroutine teleportCoroutine;

    private float currentMaxLaunchVelocity = 0f;
    private float cachedMaxUpwardInertia = 0f;
    private float inertiaTimer = 0f;
    private float cooldownTimer = 0f;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private SpriteRenderer playerSr;
    private Key parsedKey;
    private bool keyValid;


    [Header("Shadow Dash Settings")]
    public Key shadowDashKey = Key.LeftShift;
    public float shadowDashSpeed = 18f;
    public float shadowDashDuration = 0.25f;
    public LayerMask shadowObstacleLayer; 

    private bool isShadowDashing = false;
    private Coroutine shadowDashCoroutine;
    private PlayerAnimatorController playerAnim;
    private Animator shadowAnimator;
    void Start()
    {
        playerAnim = GetComponent<PlayerAnimatorController>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        playerSr = GetComponent<SpriteRenderer>();
        CacheKey();
    }

    void OnValidate() { CacheKey(); }

    void OnDisable()
    {
        // Evita que el juego se quede congelado o el jugador invisible si se desactiva a mitad de teleport
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

        if (isTeleporting) return; // ignora input mientras dura el teleport

        TrackMomentum();

        if (ActiveShadow && currentShadow != null)
        {
            UpdateShadowColor();
        }

        if (ActiveShadow && currentShadow != null && !isShadowDashing && ReadShadowDashInput())
        {
            Vector2 dashDir = GetShadowDashDirection();
            shadowDashCoroutine = StartCoroutine(ShadowDashRoutine(dashDir));
        }

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
        if (gp != null && (gp.buttonWest.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame))
            return true;

        var kb = Keyboard.current;
        if (kb != null && keyValid && kb[parsedKey].wasPressedThisFrame)
            return true;

        return false;
    }

    // Una sola fuente de verdad para "se puede teletransportar?" (color y teleport usan lo mismo)
    private bool IsReachable()
    {
        if (currentShadow == null) return false;

        Vector2 toShadow = currentShadow.transform.position - transform.position;
        float dist = toShadow.magnitude;
        float effectiveForgiveRadius = Mathf.Max(RadiusForgive, maxTeleportRadius);

        if (dist <= maxTeleportRadius) return true;
        if (dist > effectiveForgiveRadius) return false;

        // Zona de perdon: solo si te estas moviendo hacia la sombra
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

        if (shadowSr != null && playerSr != null)
            shadowSr.flipX = playerSr.flipX;

        ActiveShadow = true;
    }

    private void Teleport()
    {
        if (currentShadow == null) { ActiveShadow = false; return; }

        cooldownTimer = abilityCooldown;

        if (IsReachable())
        {
            teleportCoroutine = StartCoroutine(TeleportRoutine(currentShadow.transform.position));
        }
        else
        {
            GameObject toDestroy = currentShadow;
            currentShadow = null;
            shadowSr = null;
            ActiveShadow = false;
            StartCoroutine(FailAndDestroy(toDestroy));
        }
    }

    private IEnumerator TeleportRoutine(Vector3 targetPosition)
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

            // Mover (usar rb.position mantiene la fisica sincronizada)
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.linearVelocity = Vector2.zero;
            }
            transform.position = targetPosition;

            if (currentShadow != null) Destroy(currentShadow);
            currentShadow = null;
            shadowSr = null;
            ActiveShadow = false;

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

                rb.linearVelocity = new Vector2(0f, appliedVelocityY);
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
        ActiveShadow = false;
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

    private Vector2 GetShadowDashDirection()
    {
        Vector2 dir = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1f;
        }

        var gp = Gamepad.current;
        if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > 0.1f)
        {
            dir = gp.leftStick.ReadValue();
        }

        if (dir.sqrMagnitude < 0.01f)
        {
            bool facingLeft = playerSr != null && playerSr.flipX;
            dir = facingLeft ? Vector2.left : Vector2.right;
        }

        return dir.normalized;
    }

    private IEnumerator ShadowDashRoutine(Vector2 direction)
    {
        isShadowDashing = true;

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
    }
}