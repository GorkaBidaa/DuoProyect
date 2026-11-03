using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class CursedForestHud : MonoBehaviour
{
    public Image healthFill;
    public Image staminaFill;
    public Image itemIcon;
    public TextMeshProUGUI itemCount;

    private void Awake()
    {
        Configure(healthFill, true);
        Configure(staminaFill, false);
        SetHealth(0f);
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

    public void SetHealth(float value) => Apply(healthFill, value);
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
