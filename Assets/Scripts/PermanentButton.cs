using UnityEngine;

public class PermanentButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;

    private bool isPressed = false;
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isPressed || doorToOpen == null)
            return;

        bool isPlayer = collision.CompareTag("Player") ||
                        collision.GetComponentInParent<PlayerMovement>() != null;

        if (isPlayer)
        {
            isPressed = true;
            doorToOpen.SetActive(false);
            if (sr != null) sr.color = Color.gray;
        }
    }
}