using UnityEngine;
using TMPro;

public class RangeUpgrade : MonoBehaviour
{
    public float newRadius = 20f;
    public TextMeshProUGUI messageText;
    public float messageDuration = 3f;

    void Start()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            ShadowAnchor anchor = collision.GetComponent<ShadowAnchor>();
            if (anchor != null)
            {
                anchor.maxTeleportRadius = newRadius;

                string msg = "Teleport radius upgraded to " + newRadius + "!";

                if (messageText != null)
                {
                    if (GetComponent<SpriteRenderer>() != null)
                        GetComponent<SpriteRenderer>().enabled = false;

                    if (GetComponent<Collider2D>() != null)
                        GetComponent<Collider2D>().enabled = false;

                    StopAllCoroutines();
                    StartCoroutine(ShowMessageRoutine(msg, messageDuration));
                }
                else
                {
                    Debug.Log(msg);
                    Destroy(gameObject); 
                }
            }
        }
    }

    private System.Collections.IEnumerator ShowMessageRoutine(string text, float duration)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        messageText.text = "";
        messageText.gameObject.SetActive(false);

        Destroy(gameObject);
    }
}