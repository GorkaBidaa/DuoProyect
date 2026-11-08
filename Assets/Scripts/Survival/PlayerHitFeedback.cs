using UnityEngine;
using UnityEngine.UI;

public class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] playerSprites;
    [SerializeField] private Image screenFlash;
    [SerializeField] private AudioSource hitAudio;
    [SerializeField] private AudioClip hitClip;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.8f;
    [SerializeField, Range(0f, 0.5f)] private float screenOpacity = 0.18f;
    [SerializeField, Min(0.01f)] private float screenDuration = 0.18f;
    [SerializeField, Min(0.02f)] private float blinkInterval = 0.08f;
    private Color[] originalColors;
    private float startedAt, flashUntil;
    private bool playing;

    private void Awake()
    {
        if (playerSprites == null || playerSprites.Length == 0)
            playerSprites = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[playerSprites.Length];
        for (int i = 0; i < playerSprites.Length; i++)
            if (playerSprites[i]) originalColors[i] = playerSprites[i].color;
        if (screenFlash) screenFlash.raycastTarget = false;
        Clear();
    }

    public void Play(float invulnerabilityDuration)
    {
        startedAt = Time.time;
        flashUntil = Time.time + Mathf.Max(0.15f, invulnerabilityDuration);
        playing = true;
        if (hitAudio && hitClip) hitAudio.PlayOneShot(hitClip, hitVolume);
    }

    private void LateUpdate()
    {
        if (!playing) return;
        float elapsed = Time.time - startedAt;
        for (int i = 0; i < playerSprites.Length; i++)
        {
            if (!playerSprites[i]) continue;
            Color color = elapsed < 0.07f ? Color.white :
                Color.Lerp(originalColors[i], new Color(1f, 0.3f, 0.3f), Mathf.Clamp01(1f - elapsed / 0.2f));
            color.a = originalColors[i].a * ((int)(elapsed / Mathf.Max(0.02f, blinkInterval)) % 2 == 0 ? 1f : 0.35f);
            playerSprites[i].color = color;
        }
        if (screenFlash)
            screenFlash.color = new Color(1f, 0.1f, 0.1f,
                screenOpacity * Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, screenDuration)));
        if (Time.time >= flashUntil) Clear();
    }

    public void Clear()
    {
        playing = false;
        if (originalColors != null)
            for (int i = 0; i < playerSprites.Length; i++)
                if (playerSprites[i]) playerSprites[i].color = originalColors[i];
        if (screenFlash) screenFlash.color = Color.clear;
    }

    private void OnDisable() => Clear();
}
