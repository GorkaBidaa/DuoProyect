using UnityEngine;

[DisallowMultipleComponent]
public class EnvironmentalGlow : MonoBehaviour
{
    public enum GlowPreset
    {
        Custom,
        IceSpikeOrange,
        ForestHazardRed,
        ShadowAnchorPurple,
        CheckpointSafeGreen,
        InteractiveGold
    }

    [Header("Glow Appearance")]
    [SerializeField] private GlowPreset preset = GlowPreset.IceSpikeOrange;
    [SerializeField] private Color customColor = new Color(1f, 0.55f, 0.1f, 1f);
    [Tooltip("Size of the light in world units (increase X to cover long spike pits).")]
    [SerializeField] private Vector2 glowSize = new Vector2(3.5f, 3.5f);
    [SerializeField, Range(0.05f, 1f)] private float baseAlpha = 0.42f;
    [SerializeField] private int sortingOrderOffset = 1;

    [Header("Subtle Pulse Animation")]
    [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;
    [SerializeField, Range(0f, 0.3f)] private float pulseAmplitude = 0.08f;
    [SerializeField, Range(0f, 0.25f)] private float scalePulse = 0.05f;

    [Header("Optional Sync (For Shadow Anchor Prefab)")]
    [Tooltip("If enabled, copies the RGB color from the parent SpriteRenderer (e.g. Purple when in range, Red when out of range).")]
    [SerializeField] private bool syncWithParentSpriteColor = false;

    private static Sprite sharedGlowSprite;
    private SpriteRenderer parentSprite;
    private SpriteRenderer glowRenderer;
    private Transform glowTransform;
    private float randomPhase;

    private void Awake()
    {
        if (syncWithParentSpriteColor)
            parentSprite = GetComponentInParent<SpriteRenderer>();

        EnsureGlowSprite();

        GameObject child = new GameObject("GlowLight2D");
        child.transform.SetParent(transform, false);
        glowTransform = child.transform;

        glowRenderer = child.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = sharedGlowSprite;

        if (parentSprite != null)
        {
            glowRenderer.sortingLayerID = parentSprite.sortingLayerID;
            glowRenderer.sortingOrder = parentSprite.sortingOrder + sortingOrderOffset;
        }
        else
        {
            glowRenderer.sortingOrder = sortingOrderOffset;
        }

        randomPhase = Random.Range(0f, Mathf.PI * 2f);
        ApplyVisuals(0f);
    }

    private void LateUpdate()
    {
        if (!glowRenderer) return;
        ApplyVisuals(Time.time * pulseSpeed + randomPhase);
    }

    private void ApplyVisuals(float phase)
    {
        Color c = GetBaseColor();
        float wave = Mathf.Sin(phase);
        c.a = Mathf.Clamp01(baseAlpha + wave * pulseAmplitude);
        glowRenderer.color = c;

        float scaleMod = 1f + wave * scalePulse;
        glowTransform.localScale = new Vector3(glowSize.x * scaleMod, glowSize.y * scaleMod, 1f);
    }

    private Color GetBaseColor()
    {
        if (syncWithParentSpriteColor && parentSprite != null)
            return parentSprite.color;

        return preset switch
        {
            GlowPreset.IceSpikeOrange => new Color(1f, 0.48f, 0.08f, 1f),
            GlowPreset.ForestHazardRed => new Color(1f, 0.18f, 0.22f, 1f),
            GlowPreset.ShadowAnchorPurple => new Color32(155, 40, 255, 255),
            GlowPreset.CheckpointSafeGreen => new Color(0.35f, 1f, 0.55f, 1f),
            GlowPreset.InteractiveGold => new Color(1f, 0.85f, 0.25f, 1f),
            _ => customColor
        };
    }

    private static void EnsureGlowSprite()
    {
        if (sharedGlowSprite != null) return;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float dy = (y / (float)(size - 1)) * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (float)(size - 1)) * 2f - 1f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - dist);
                // Smooth quadratic falloff so edges blend invisibly into dark backgrounds
                float alpha = falloff * falloff * (3f - 2f * falloff);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * falloff);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        sharedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = GetBaseColor();
        Gizmos.DrawWireCube(transform.position, new Vector3(glowSize.x, glowSize.y, 0.05f));
    }
}