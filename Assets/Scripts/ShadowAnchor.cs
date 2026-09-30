using System.Collections;
using System.Runtime.ConstrainedExecution;
using UnityEngine;
using UnityEngine.InputSystem;

public class PhantomAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public GameObject phantomPrefab; //shadow prefab
    public float maxTeleportRadius = 8f; //Maximum allowed teleport distance
    public KeyCode anchorKey = KeyCode.E; //Key to use the ability

    private GameObject currentShadow; 
    private bool ActiveShadow = false;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        bool anchorPressed = false;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) anchorPressed = true;

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
                DeployAnchor();
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
        //Distance between player and phantom
        float distanceToAnchor = Vector2.Distance(transform.position, currentShadow.transform.position);

        if (distanceToAnchor <= maxTeleportRadius)
        {
            //Teleport
            transform.position = currentShadow.transform.position;

            if (rb != null)
            {
                // Reset all the velocity except for the Y velocity if jumping
                float currentVelocityY = rb.linearVelocity.y;

                if (currentVelocityY < 0)
                {
                    currentVelocityY = 0f;
                }
                rb.linearVelocity = new Vector2(0f, currentVelocityY);
            }

            //Destroy the phantom and reset
            Destroy(currentShadow);
            ActiveShadow = false;
        }
        else
        {
            //Destroy the anchor on failure
            StartCoroutine(FailAndDestroy(currentShadow));
            ActiveShadow = false;
        }
    }
    private IEnumerator FailAndDestroy(GameObject shadowToDestroy)
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
}