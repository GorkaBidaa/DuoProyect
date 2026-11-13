using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class CursedForestHud : MonoBehaviour
{
    public Image healthFill;
    public Image staminaFill;
    public Image itemIcon;
    public TextMeshProUGUI itemCount;
    
    [Header("Health animation")]
    public Image healthGhost;
    [Tooltip("A dedicated wrapper around the health arc and its ghost, outside layout control.")]
    public RectTransform healthFlinchRoot;
    [Min(0.01f)] public float healthTweenDuration = 0.22f;
    [Min(0f)] public float ghostDelay = 0.3f;
    [Min(0.01f)] public float ghostTweenDuration = 0.6f;
    public Color damageFlashColor = Color.white;
    [Min(0f)] public float flinchPixels = 6f;
    private float targetHealth, shownHealth, ghostHealth, fromHealth, fromGhost, healthTime;
    private bool healthInitialized;
    private Color healthColor;
    private Vector2 restPosition;

    private void Awake()
    {
        Configure(healthFill, true);
        Configure(staminaFill, false);
        Configure(healthGhost, true);
        if (healthFill) healthColor = healthFill.color;
        if (healthFlinchRoot) restPosition = healthFlinchRoot.anchoredPosition;
        SetStamina(0f);
        SetItem(null, 0);
    }

    private static void Configure(Image image, bool clockwise)
    {
        if (!image) return;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Bottom;
        image.fillClockwise = clockwise;
        image.raycastTarget = false;
    }

    public void SetHealth(float value)
    {
        value = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        if (!healthInitialized || value > targetHealth)
        {
            // Initial values, healing and respawn must not leave a stale damage trail.
            targetHealth = shownHealth = ghostHealth = fromHealth = fromGhost = value;
            healthTime = 100f;
            healthInitialized = true;
            Apply(healthFill, value);
            Apply(healthGhost, value);
            RestoreHealthVisual();
            return;
        }
        if (Mathf.Approximately(value, targetHealth)) return;
        fromHealth = shownHealth;
        fromGhost = Mathf.Max(ghostHealth, shownHealth);
        targetHealth = value;
        healthTime = 0f;
    }

    private static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    private void Update()
    {
        if (!healthInitialized) return;
        healthTime += Time.deltaTime;
        shownHealth = Mathf.Lerp(fromHealth, targetHealth, Ease(healthTime / Mathf.Max(0.01f, healthTweenDuration)));
        ghostHealth = Mathf.Max(shownHealth, Mathf.Lerp(fromGhost, targetHealth,
            Ease((healthTime - ghostDelay) / Mathf.Max(0.01f, ghostTweenDuration))));
        Apply(healthFill, shownHealth);
        Apply(healthGhost, ghostHealth);
        float pulse = Mathf.Clamp01(1f - healthTime / 0.25f);
        if (healthFill) healthFill.color = Color.Lerp(healthColor, damageFlashColor, pulse);
        if (healthFlinchRoot) healthFlinchRoot.anchoredPosition = restPosition +
            Vector2.right * (Mathf.Sin(healthTime * 80f) * flinchPixels * pulse);
    }

    private void RestoreHealthVisual()
    {
        if (healthFill) healthFill.color = healthColor;
        if (healthFlinchRoot) healthFlinchRoot.anchoredPosition = restPosition;
    }

    private void OnDisable() => RestoreHealthVisual();
    public void SetStamina(float value) => Apply(staminaFill, value);

    private static void Apply(Image image, float normalized)
    {
        if (!image) return;
        float value = float.IsNaN(normalized) ? 0f : Mathf.Clamp01(normalized);
        image.enabled = value > 0f;
        // Both arcs start 18 degrees from bottom and span 132 degrees.
        image.fillAmount = value >= 1f ? 1f : value > 0f ? (18f + 132f * value) / 360f : 0f;
    }

    public void SetItem(Sprite sprite, int quantity)
    {
        if (itemIcon)
        {
            itemIcon.sprite = sprite;
            itemIcon.enabled = sprite != null;
        }
        if (itemCount) itemCount.text = sprite && quantity > 0 ? quantity.ToString() : string.Empty;
    }
}
