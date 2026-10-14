using System.Collections;
using UnityEngine;

public class PermanentButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;

    private bool isPressed = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !isPressed && doorToOpen != null)
        {
            isPressed = true;
            doorToOpen.SetActive(false);
            GetComponent<SpriteRenderer>().color = Color.gray;
        }
    }
}