using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PermanentButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;

    private bool isPressed = false;
    private SpriteRenderer sr;

    private void Reset()
    {
        // Ensure the button collider acts as a trigger by default
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isPressed || doorToOpen == null || collision == null || collision.isTrigger)
            return;

        PlayerMovement playerMovement = collision.GetComponentInParent<PlayerMovement>();
        bool isPlayer = playerMovement != null || collision.CompareTag("Player");

        if (!isPlayer)
            return;

        // Prevent a dead player body from activating the button
        StaminaSystem stamina = collision.GetComponentInParent<StaminaSystem>();
        if (stamina != null && stamina.IsDead)
            return;

        isPressed = true;
        doorToOpen.SetActive(false);
        if (sr != null) sr.color = Color.gray;
    }
}