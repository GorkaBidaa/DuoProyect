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

    private float currentMaxLaunchVelocity = 0f;
    private float cachedMaxUpwardInertia = 0f;
    private float inertiaTimer = 0f;
    private float cooldownTimer = 0f;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private SpriteRenderer playerSr;
    private Key parsedKey;
    private bool keyValid;

    void Start()
    {
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
        keyValid = System.Enum.TryParse(anchorKey.ToString(), out parsedKey);
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        TrackMomentum();

        if (ActiveShadow && currentShadow != null && !isTeleporting)
            UpdateShadowColor();

        if (isTeleporting) return; // ignora input mientras dura el teleport

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
            inertiaTimer = inertiaCoyoteTime;
        }
        else
        {
            inertiaTimer -= Time.deltaTime;
            if (inertiaTimer <= 0f) cachedMaxUpwardInertia = 0f;
        }

        if (playerMovement != null)
        {
            if (playerMovement.isGrounded) currentMaxLaunchVelocity = 0f;
            else if (vy > currentMaxLaunchVelocity) currentMaxLaunchVelocity = vy;
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

        if (dist <= maxTeleportRadius) return true;
        if (dist > RadiusForgive) return false;

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
        if (phantomPrefab == null)
        {
            Debug.LogWarning("ShadowAnchor: falta phantomPrefab.");
            return;
        }
        currentShadow = Instantiate(phantomPrefab, transform.position, Quaternion.identity);
        shadowSr = currentShadow.GetComponent<SpriteRenderer>();
        ActiveShadow = true;
    }

    private void Teleport()
    {
        if (currentShadow == null) { ActiveShadow = false; return; }

        cooldownTimer = abilityCooldown;

        if (IsReachable())
        {
            StartCoroutine(TeleportRoutine(currentShadow.transform.position));
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

        try
        {
            if (playerMovement != null) playerMovement.SetTeleportingState(true);

            // Fade out
            yield return FadePlayer(1f, 0f);

            // Mover (usar rb.position mantiene la fisica sincronizada)
            if (rb != null) rb.position = targetPosition;
            transform.position = targetPosition;

            if (rb != null)
            {
                float appliedVelocityY = cachedMaxUpwardInertia > 0f
                    ? cachedMaxUpwardInertia * UpMultiplier
                    : 0f;

                float dynamicMaxLimit = Mathf.Min(currentMaxLaunchVelocity * UpMultiplier, velocityClampMax);
                appliedVelocityY = Mathf.Clamp(appliedVelocityY, 0f, dynamicMaxLimit);

                rb.linearVelocity = new Vector2(0f, appliedVelocityY);
            }

            cachedMaxUpwardInertia = 0f;
            inertiaTimer = 0f;

            if (currentShadow != null) Destroy(currentShadow);
            currentShadow = null;
            shadowSr = null;
            ActiveShadow = false;

            // Fade in
            yield return FadePlayer(0f, 1f);

            // Hitstop
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitstopDuration);
        }
        finally
        {
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            isTeleporting = false;
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
        if (currentShadow != null) Destroy(currentShadow);
        currentShadow = null;
        shadowSr = null;
        ActiveShadow = false;
    }
}