using System.Collections;
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
        if (collision.CompareTag("Player") && !isPressed && doorToOpen != null)
        {
            isPressed = true;
            doorToOpen.SetActive(false);
            if (sr != null) sr.color = Color.gray;
        }
    }
}