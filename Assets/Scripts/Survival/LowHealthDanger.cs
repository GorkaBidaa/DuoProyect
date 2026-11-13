using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth), typeof(StaminaSystem))]
public class LowHealthDanger : MonoBehaviour
{
    [Header("Screen effect")]
    [SerializeField] private Canvas gameplayCanvas;
    [Tooltip("Optional dedicated full-screen Image. A vignette Image is created if empty.")]
    [SerializeField] private Image vignette;
    [SerializeField, Range(0.05f, 0.9f)] private float lowHealthThreshold = 0.4f;
    [SerializeField, Range(0f, 0.5f)] private float maximumOpacity = 0.28f;
    [SerializeField] private Color dangerColor = new Color(0.5f, 0.015f, 0.025f, 1f);
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.6f;

    [Header("Audio - optional replacement clips")]
    [Tooltip("Use a short single heartbeat cycle. If empty, a placeholder heartbeat is generated.")]
    [SerializeField] private AudioClip heartbeatClip;
    [Tooltip("A looping danger ambience. If empty, a quiet placeholder drone is generated.")]
    [SerializeField] private AudioClip dangerAmbienceClip;
    [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.18f;
    [SerializeField, Range(40f, 180f)] private float startingBpm = 70f;
    [SerializeField, Range(40f, 180f)] private float criticalBpm = 135f;

    private PlayerHealth health;
    private StaminaSystem stamina;
    private AudioSource heartbeat, ambience;
    private AudioClip generatedHeartbeat, generatedAmbience;
    private Texture2D generatedTexture;
    private Sprite generatedSprite;
    private GameObject generatedOverlay;
    private float intensity, beatPhase = 1f;
    private bool audioPaused;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        stamina = GetComponent<StaminaSystem>();
        if (!vignette && !gameplayCanvas)
        {
            Debug.LogError("Assign LowHealthDanger's gameplay Canvas or a dedicated vignette Image.", this);
            enabled = false;
            return;
        }
        if (!vignette)
        {
            generatedOverlay = new GameObject("LowHealthVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            generatedOverlay.transform.SetParent(gameplayCanvas.transform, false);
            generatedOverlay.transform.SetAsFirstSibling();
            vignette = generatedOverlay.GetComponent<Image>();
            RectTransform rect = vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        vignette.raycastTarget = false;
        if (!vignette.sprite) vignette.sprite = CreateVignette();
        vignette.type = Image.Type.Simple;
        heartbeat = CreateAudio("LowHealthHeartbeat", false);
        ambience = CreateAudio("LowHealthAmbience", true);
        if (!heartbeatClip) heartbeatClip = generatedHeartbeat = CreateHeartbeat();
        if (!dangerAmbienceClip) dangerAmbienceClip = generatedAmbience = CreateDrone();
        heartbeat.clip = heartbeatClip;
        ambience.clip = dangerAmbienceClip;
        Clear();
    }

    private AudioSource CreateAudio(string name, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private void Update()
    {
        if (!health || !stamina || stamina.IsDead || health.CurrentHealth <= 0) { Clear(); return; }
        if (Time.timeScale <= 0f)
        {
            if (!audioPaused) { heartbeat.Pause(); ambience.Pause(); audioPaused = true; }
            if (vignette) vignette.enabled = false;
            return;
        }
        if (audioPaused) { heartbeat.UnPause(); ambience.UnPause(); audioPaused = false; }

        float target = Mathf.Clamp01((lowHealthThreshold - health.NormalizedHealth) / Mathf.Max(0.01f, lowHealthThreshold));
        intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime / Mathf.Max(0.05f, fadeDuration));
        if (intensity <= 0.001f) { Clear(); return; }

        float bpm = Mathf.Lerp(startingBpm, criticalBpm, intensity);
        beatPhase += Time.deltaTime * bpm / 60f;
        heartbeat.volume = heartbeatVolume * intensity;
        if (beatPhase >= 1f)
        {
            beatPhase %= 1f;
            heartbeat.Play();
        }
        ambience.volume = ambienceVolume * intensity;
        if (!ambience.isPlaying) ambience.Play();

        float pulse = Mathf.Clamp01(Mathf.Exp(-beatPhase * 15f) +
            0.55f * Mathf.Exp(-Mathf.Pow((beatPhase - 0.2f) * 22f, 2f)));
        float staminaPressure = stamina.maxStamina > 0f
            ? Mathf.Clamp01(1f - stamina.CurrentStamina / (stamina.maxStamina * 0.35f)) : 0f;
        Color color = dangerColor;
        // Soften the additional red tint when the existing stamina shadows are strongest.
        color.a = intensity * maximumOpacity * Mathf.Lerp(0.45f, 1f, pulse) * Mathf.Lerp(1f, 0.35f, staminaPressure);
        if (vignette) { vignette.color = color; vignette.enabled = true; }
    }

    private void Clear()
    {
        intensity = 0f;
        beatPhase = 1f;
        audioPaused = false;
        if (heartbeat) { heartbeat.Stop(); heartbeat.volume = 0f; }
        if (ambience) { ambience.Stop(); ambience.volume = 0f; }
        if (vignette) { vignette.color = Color.clear; vignette.enabled = false; }
    }

    private Sprite CreateVignette()
    {
        const int size = 128;
        generatedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedTexture.name = "LowHealthEdgeMask";
        generatedTexture.wrapMode = TextureWrapMode.Clamp;
        generatedTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x / (float)(size - 1)) * 2f - 1f);
                float ny = Mathf.Abs((y / (float)(size - 1)) * 2f - 1f);
                float edge = Mathf.Clamp01((Mathf.Max(nx, ny) - 0.52f) / 0.48f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, edge));
            }
        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply(false, true);
        generatedSprite = Sprite.Create(generatedTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return generatedSprite;
    }

    private static AudioClip CreateHeartbeat()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.36f)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            samples[i] = HeartPulse(t) + 0.65f * HeartPulse(t - 0.13f);
        }
        AudioClip clip = AudioClip.Create("Placeholder heartbeat", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float HeartPulse(float t)
    {
        if (t < 0f || t > 0.16f) return 0f;
        return Mathf.Sin(t * Mathf.PI / 0.16f) * Mathf.Exp(-t * 16f) * Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.85f;
    }

    private static AudioClip CreateDrone()
    {
        const int rate = 22050;
        var samples = new float[rate * 4];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float body = Mathf.Sin(2f * Mathf.PI * 43f * t) * 0.35f +
                Mathf.Sin(2f * Mathf.PI * 61f * t) * 0.2f + Mathf.Sin(2f * Mathf.PI * 86f * t) * 0.12f;
            samples[i] = body * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
        }
        AudioClip clip = AudioClip.Create("Placeholder danger drone", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        if (heartbeat) Destroy(heartbeat.gameObject);
        if (ambience) Destroy(ambience.gameObject);
        if (generatedOverlay) Destroy(generatedOverlay);
        else if (vignette && vignette.sprite == generatedSprite) vignette.sprite = null;
        if (generatedSprite) Destroy(generatedSprite);
        if (generatedTexture) Destroy(generatedTexture);
        if (generatedHeartbeat) Destroy(generatedHeartbeat);
        if (generatedAmbience) Destroy(generatedAmbience);
    }
}
