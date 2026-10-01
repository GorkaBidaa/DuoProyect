using System.Collections;
using UnityEngine;

public class TimedButton : MonoBehaviour
{
    [Header("Door")]
    public GameObject doorToOpen;
    public float openTime = 3f;

    private bool isPressed = false;

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

        // Disable door
        if (doorToOpen != null) doorToOpen.SetActive(false);

        // Change the button color
        GetComponent<SpriteRenderer>().color = Color.gray;

        // Wait
        yield return new WaitForSeconds(openTime);
        if (doorToOpen != null) {
            doorToOpen.SetActive(true);
        }
        ColorUtility.TryParseHtmlString("#FF00C4", out Color parsedColor);
        GetComponent<SpriteRenderer>().color = parsedColor;
        isPressed = false;
    }
}