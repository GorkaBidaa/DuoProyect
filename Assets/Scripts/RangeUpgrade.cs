using UnityEngine;
using TMPro;

public class RangeUpgrade : MonoBehaviour
{
    public float newRadius = 20f;
    public TextMeshProUGUI messageText;
    public float messageDuration = 3f;
    public string customMessage = "";

    private SpriteRenderer sr;
    private Collider2D col;
    private bool isCollected = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        ShadowAnchor anchor = collision.GetComponentInParent<ShadowAnchor>();
        if (anchor == null && collision.CompareTag("Player"))
            anchor = collision.GetComponent<ShadowAnchor>();

        if (anchor != null)
        {
            isCollected = true;

            // Keep forgive margin proportional to the new radius and never downgrade
            float forgiveMargin = Mathf.Max(1f, anchor.RadiusForgive - anchor.maxTeleportRadius);
            float appliedRadius = Mathf.Max(anchor.maxTeleportRadius, newRadius);
            anchor.maxTeleportRadius = appliedRadius;
            anchor.RadiusForgive = appliedRadius + forgiveMargin;

            string msg = string.IsNullOrEmpty(customMessage)
                ? "Teleport radius upgraded to " + appliedRadius + "!"
                : customMessage;

            if (messageText != null)
            {
                if (sr != null) sr.enabled = false;
                if (col != null) col.enabled = false;

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

    private System.Collections.IEnumerator ShowMessageRoutine(string text, float duration)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        if (messageText.text == text)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }

        Destroy(gameObject);
    }
}