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
    private Bounds doorBounds;
    private bool hasDoorCollider = false;
    private ContactFilter2D solidFilter;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>(8);

    // Registra qué colliders del jugador están pisando el botón actualmente
    private readonly HashSet<Collider2D> collidersOnButton = new HashSet<Collider2D>();
    private Coroutine countdownRoutine;

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

        CacheDoorCollider();

        // Ignore trigger colliders when checking if the doorway is blocked
        solidFilter = new ContactFilter2D();
        solidFilter.useTriggers = false;
    }

    private void CacheDoorCollider()
    {
        if (doorCollider == null && doorToOpen != null)
        {
            doorCollider = doorToOpen.GetComponent<Collider2D>();
            if (doorCollider == null)
                doorCollider = doorToOpen.GetComponentInChildren<Collider2D>();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null || collision.isTrigger)
            return;

        if (!IsPlayerCollider(collision))
            return;

        // Prevent a dead player body from activating the button
        StaminaSystem stamina = collision.GetComponentInParent<StaminaSystem>();
        if (stamina != null && stamina.IsDead)
            return;

        collidersOnButton.Add(collision);

        // Si el jugador vuelve a pisar el botón antes de que termine el tiempo, se cancela la cuenta atrás
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        // Abre la puerta inmediatamente al pisar el botón
        if (!isPressed)
        {
            PressButton();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision == null)
            return;

        // Cuando el último collider del jugador sale del botón, comienza la cuenta atrás
        if (collidersOnButton.Remove(collision) && collidersOnButton.Count == 0 && isPressed)
        {
            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);

            countdownRoutine = StartCoroutine(ReleaseButtonRoutine());
        }
    }

    private void PressButton()
    {
        isPressed = true;

        CacheDoorCollider();
        if (doorCollider != null)
        {
            // Se guardan los límites antes de desactivar la puerta (al desactivarse, bounds pasa a ser cero)
            doorBounds = doorCollider.bounds;
            hasDoorCollider = true;
        }

        // Disable door
        if (doorToOpen != null)
            doorToOpen.SetActive(false);

        // Change the button color
        if (sr != null)
            sr.color = Color.gray;
    }

    private IEnumerator ReleaseButtonRoutine()
    {
        // El temporizador empieza a contar ahora que el jugador ha dejado de pisarlo
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

        if (sr != null)
            sr.color = originalColor;

        isPressed = false;
        countdownRoutine = null;
    }

    private bool IsPlayerCollider(Collider2D col)
    {
        return col.CompareTag("Player") || col.GetComponentInParent<PlayerMovement>() != null;
    }

    private bool IsPlayerBlockingDoor(Bounds bounds)
    {
        overlapResults.Clear();
        Physics2D.OverlapBox(bounds.center, bounds.size, 0f, solidFilter, overlapResults);

        for (int i = 0; i < overlapResults.Count; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit != null && !hit.isTrigger && IsPlayerCollider(hit))
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        collidersOnButton.Clear();
        countdownRoutine = null;

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