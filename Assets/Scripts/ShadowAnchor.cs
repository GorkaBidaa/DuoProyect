using System.Collections;
using System.Runtime.ConstrainedExecution;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShadowAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public GameObject phantomPrefab; //shadow prefab
    public float maxTeleportRadius = 8f; //Maximum allowed teleport distance
    public KeyCode anchorKey = KeyCode.E; //Key to use the ability
    public float UpMultiplier = 1.5f; 

    private GameObject currentShadow; 
    private bool ActiveShadow = false;

    private Rigidbody2D rb;
    // Stored upward inertia captured at button press
    private float storedUpwardInertia = 0f;
    public float abilityCooldown = 0.5f;
    private float cooldownTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        bool anchorPressed = false;
        // Cooldown timer decrement
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

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

        if (anchorPressed)
        {
            if (!ActiveShadow)
            {
                // Only allow deploying if cooldown has expired
                if (cooldownTimer <= 0f)
                {
                    DeployAnchor();
                    // cooldown starts on teleport, not on deploy
                }
            }
            else
            {
                // Capture current upward velocity right when the player requests teleport
                if (rb != null)
                {
                    storedUpwardInertia = rb.linearVelocity.y;
                }
                else
                {
                    storedUpwardInertia = 0f;
                }

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
        //Distance between player and phantom
        float distanceToAnchor = Vector2.Distance(transform.position, currentShadow.transform.position);

        if (distanceToAnchor <= maxTeleportRadius)
        {
            // Start animation
            StartCoroutine(TeleportRoutine(currentShadow.transform.position));
        }
        else
        {
            //Destroy the anchor on failure
            StartCoroutine(FailAndDestroy(currentShadow));
            ActiveShadow = false;
        }
    }

    private IEnumerator TeleportRoutine(Vector3 targetPosition)
    {
        SpriteRenderer playerSr = GetComponent<SpriteRenderer>();
        float fadeTime = 0.2f;

        // Fade out
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

        // Move player to target
        transform.position = targetPosition;

        // Apply the stored upward inertia captured at button press
        if (rb != null)
        {
            float appliedVelocityY = storedUpwardInertia;
            if (appliedVelocityY <= 0f) appliedVelocityY = 0f;
            else appliedVelocityY = appliedVelocityY * UpMultiplier;
            rb.linearVelocity = new Vector2(0f, appliedVelocityY);
        }

        // Reset stored inertia after applying
        storedUpwardInertia = 0f;

        // Destroy phantom now that we've moved
        if (currentShadow != null)
        {
            Destroy(currentShadow);
            currentShadow = null;
        }
        ActiveShadow = false;

        // Fade in
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
    }
    private  IEnumerator FailAndDestroy(GameObject shadowToDestroy)
    {
        //Get the Sprite
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