using System.Collections;
using UnityEngine;

public class TimedButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;
    public float openTime = 3f;

    private bool isPressed = false;
    private SpriteRenderer sr;
    private Color originalColor;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalColor = sr.color;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isPressed)
        {
            StartCoroutine(PressButtonRoutine());
        }
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressed = true;

        Bounds doorBounds = default;
        bool hasDoorCollider = false;

        if (doorToOpen != null)
        {
            Collider2D doorCol = doorToOpen.GetComponent<Collider2D>();
            if (doorCol != null)
            {
                doorBounds = doorCol.bounds;
                hasDoorCollider = true;
            }
        }

        // Disable door
        if (doorToOpen != null) doorToOpen.SetActive(false);

        // Change the button color
        if (sr != null) sr.color = Color.gray;

        // Wait
        yield return new WaitForSeconds(openTime);

        // Wait until player clears the doorway
        while (hasDoorCollider && IsPlayerBlockingDoor(doorBounds))
        {
            yield return null;
        }

        if (doorToOpen != null)
        {
            doorToOpen.SetActive(true);
        }

        if (sr != null) sr.color = originalColor;
        isPressed = false;
    }

    private bool IsPlayerBlockingDoor(Bounds bounds)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].CompareTag("Player"))
                return true;
        }
        return false;
    }
}