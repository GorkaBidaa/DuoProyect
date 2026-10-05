using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShadowAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public GameObject phantomPrefab;
    public float maxTeleportRadius = 8f;
    public float RadiusForgive = 9f;
    public KeyCode anchorKey = KeyCode.E;

    [Header("Momentum Settings")]
    public float UpMultiplier = 1f;
    public float velocityClampMax = 25f;

    [Header("Game Feel Settings")]
    public float inertiaCoyoteTime = 0.2f;
    public float hitstopDuration = 0.1f;

    private GameObject currentShadow;
    private bool ActiveShadow = false;
    private float currentMaxLaunchVelocity = 0f;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;

    private float cachedMaxUpwardInertia = 0f;
    private float inertiaTimer = 0f;

    public float abilityCooldown = 0.5f;
    private float cooldownTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        bool anchorPressed = false;

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (rb != null)
        {
            if (rb.linearVelocity.y > 0.1f)
            {
                if (rb.linearVelocity.y > cachedMaxUpwardInertia)
                {
                    cachedMaxUpwardInertia = rb.linearVelocity.y;
                }
                inertiaTimer = inertiaCoyoteTime;
            }
            else
            {
                inertiaTimer -= Time.deltaTime;
                if (inertiaTimer <= 0f)
                {
                    cachedMaxUpwardInertia = 0f;
                }
            }
        }

        if (playerMovement != null)
        {
            if (playerMovement.isGrounded)
            {
                currentMaxLaunchVelocity = 0f;
            }
            else if (rb != null && rb.linearVelocity.y > currentMaxLaunchVelocity)
            {
                currentMaxLaunchVelocity = rb.linearVelocity.y;
            }
        }

        var gp = Gamepad.current;
        if (gp != null && (gp.buttonWest.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame)) anchorPressed = true;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (System.Enum.TryParse<Key>(anchorKey.ToString(), out Key parsedKey))
            {
                var keyControl = kb[parsedKey];
                if (keyControl != null && keyControl.wasPressedThisFrame) anchorPressed = true;
            }
        }

        //Update shadow color dynamically if out of range
        if (ActiveShadow && currentShadow != null)
        {
            float currentDist = Vector2.Distance(transform.position, currentShadow.transform.position);
            bool isReachable = false;

            if (currentDist <= maxTeleportRadius)
            {
                isReachable = true;
            }
            else if (currentDist <= RadiusForgive)
            {
                Vector2 dirToShadow = (currentShadow.transform.position - transform.position).normalized;
                float currentDot = Vector2.Dot(rb.linearVelocity.normalized, dirToShadow);
                if (currentDot > 0.3f) isReachable = true;
            }

            SpriteRenderer shadowSr = currentShadow.GetComponent<SpriteRenderer>();
            if (shadowSr != null)
            {
                // Normal color if reachable, red/transparent if out of bounds
                if (isReachable) shadowSr.color = new Color32(127, 0, 247, 80);
                else shadowSr.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            }
        }

        if (anchorPressed)
        {
            if (!ActiveShadow)
            {
                if (cooldownTimer <= 0f)
                {
                    DeployAnchor();
                }
            }
            else
            {
                Teleport();
            }
        }
    }

    private void DeployAnchor()
    {
        currentShadow = Instantiate(phantomPrefab, transform.position, Quaternion.identity);
        ActiveShadow = true;
    }

    private void Teleport()
    {
        cooldownTimer = abilityCooldown;
        float distanceToAnchor = Vector2.Distance(transform.position, currentShadow.transform.position);

        bool canTeleport = false;

        if (distanceToAnchor <= maxTeleportRadius)
        {
            canTeleport = true;
        }
        else if (distanceToAnchor <= RadiusForgive)
        {
            Vector2 directionToShadow = (currentShadow.transform.position - transform.position).normalized;
            float dotProduct = Vector2.Dot(rb.linearVelocity.normalized, directionToShadow);

            if (dotProduct > 0.3f)
            {
                canTeleport = true;
            }
        }

        if (canTeleport)
        {
            StartCoroutine(TeleportRoutine(currentShadow.transform.position));
        }
        else
        {
            StartCoroutine(FailAndDestroy(currentShadow));
            ActiveShadow = false;
        }
    }

    private IEnumerator TeleportRoutine(Vector3 targetPosition)
    {
        SpriteRenderer playerSr = GetComponent<SpriteRenderer>();
        float fadeTime = 0.2f;

        if (playerMovement != null) playerMovement.SetTeleportingState(true);

        if (playerSr != null)
        {
            Color col = playerSr.color;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                col.a = Mathf.Lerp(1f, 0f, t / fadeTime);
                playerSr.color = col;
                yield return null;
            }
            col.a = 0f;
            playerSr.color = col;
        }

        transform.position = targetPosition;

        if (rb != null)
        {
            float appliedVelocityY = cachedMaxUpwardInertia;
            if (appliedVelocityY <= 0f) appliedVelocityY = 0f;
            else appliedVelocityY = appliedVelocityY * UpMultiplier;

            float dynamicMaxLimit = Mathf.Min(currentMaxLaunchVelocity * UpMultiplier, velocityClampMax);
            appliedVelocityY = Mathf.Clamp(appliedVelocityY, 0f, dynamicMaxLimit);

            rb.linearVelocity = new Vector2(0f, appliedVelocityY);
        }

        cachedMaxUpwardInertia = 0f;
        inertiaTimer = 0f;

        if (currentShadow != null)
        {
            Destroy(currentShadow);
            currentShadow = null;
        }
        ActiveShadow = false;

        if (playerSr != null)
        {
            Color col = playerSr.color;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                col.a = Mathf.Lerp(0f, 1f, t / fadeTime);
                playerSr.color = col;
                yield return null;
            }
            col.a = 1f;
            playerSr.color = col;
        }

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitstopDuration);
        Time.timeScale = 1f;

        if (playerMovement != null) playerMovement.SetTeleportingState(false);
    }

    private IEnumerator FailAndDestroy(GameObject shadowToDestroy)
    {
        SpriteRenderer sr = shadowToDestroy.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color32(255, 0, 0, 128);
        }

        yield return new WaitForSeconds(0.3f);

        if (shadowToDestroy != null)
        {
            Destroy(shadowToDestroy);
        }
    }

    public void ForceDestroyAnchor()
    {
        if (ActiveShadow && currentShadow != null)
        {
            Destroy(currentShadow);
            ActiveShadow = false;
        }
    }
}