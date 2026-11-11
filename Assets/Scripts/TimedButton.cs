using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TimedButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;
    public float openTime = 3f;

    private bool isPressed = false;
    private SpriteRenderer sr;
    private Color originalColor;
    private Collider2D doorCollider;
    private ContactFilter2D solidFilter;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>(8);

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

        if (sr != null)
        {
            originalColor = sr.color;
        }

        if (doorToOpen != null)
        {
            doorCollider = doorToOpen.GetComponent<Collider2D>();
            if (doorCollider == null)
                doorCollider = doorToOpen.GetComponentInChildren<Collider2D>();
        }

        // Ignore trigger colliders when checking if the doorway is blocked
        solidFilter = new ContactFilter2D();
        solidFilter.useTriggers = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isPressed || collision == null || collision.isTrigger)
            return;

        bool isPlayer = collision.CompareTag("Player") ||
                        collision.GetComponentInParent<PlayerMovement>() != null;

        if (!isPlayer)
            return;

        // Prevent a dead player body from activating the button
        StaminaSystem stamina = collision.GetComponentInParent<StaminaSystem>();
        if (stamina != null && stamina.IsDead)
            return;

        StartCoroutine(PressButtonRoutine());
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressed = true;

        Bounds doorBounds = default;
        bool hasDoorCollider = false;

        if (doorCollider == null && doorToOpen != null)
        {
            doorCollider = doorToOpen.GetComponent<Collider2D>();
            if (doorCollider == null)
                doorCollider = doorToOpen.GetComponentInChildren<Collider2D>();
        }

        if (doorCollider != null)
        {
            doorBounds = doorCollider.bounds;
            hasDoorCollider = true;
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
        overlapResults.Clear();
        Physics2D.OverlapBox(bounds.center, bounds.size, 0f, solidFilter, overlapResults);

        for (int i = 0; i < overlapResults.Count; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit != null && !hit.isTrigger && (hit.CompareTag("Player") || hit.GetComponentInParent<PlayerMovement>() != null))
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        // Reset state if disabled while active so the button does not remain stuck
        if (isPressed)
        {
            if (doorToOpen != null)
                doorToOpen.SetActive(true);
            if (sr != null)
                sr.color = originalColor;
            isPressed = false;
        }
    }
}