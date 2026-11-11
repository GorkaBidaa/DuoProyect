using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Future NPC/rope components can implement this and register with the same UI.
public interface IInteractionPromptSource
{
    bool PromptAvailable { get; }
    Vector3 PromptPosition { get; }
    int PromptPriority { get; }
    string PromptAction { get; }
    string PromptLabel { get; }
}

public static class InteractionPromptRegistry
{
    private static readonly List<MonoBehaviour> sources = new List<MonoBehaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => sources.Clear();

    public static void Register(MonoBehaviour source)
    {
        if (source is IInteractionPromptSource && !sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(MonoBehaviour source) => sources.Remove(source);

    public static IInteractionPromptSource Select(Vector3 playerPosition)
    {
        IInteractionPromptSource best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            MonoBehaviour owner = sources[i];
            if (!owner) { sources.RemoveAt(i); continue; }
            if (!owner.isActiveAndEnabled) continue;
            var candidate = (IInteractionPromptSource)owner;
            if (!candidate.PromptAvailable) continue;
            float distance = (candidate.PromptPosition - playerPosition).sqrMagnitude;
            if (best == null || candidate.PromptPriority > best.PromptPriority ||
                (candidate.PromptPriority == best.PromptPriority && distance < bestDistance))
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    public static bool HasCraftingStation(CraftingMenu menu)
    {
        foreach (MonoBehaviour source in sources)
            if (source is Crafting station && station && station.PromptAvailable && station.Menu == menu)
                return true;
        return false;
    }
}

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text promptText;
    [Header("Follow player")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.12f;
    private Vector3 originalScale;
    private Canvas canvas;
    private RectTransform panelRect;

    private void Awake()
    {
        if (!player || !panel || !promptText)
        {
            Debug.LogError("Assign InteractionPromptUI's player, panel CanvasGroup and text.", this);
            enabled = false;
            return;
        }
        originalScale = panel.transform.localScale;
        Canvas ownerCanvas = panel.GetComponentInParent<Canvas>();
        canvas = ownerCanvas ? ownerCanvas.rootCanvas : null;
        panelRect = panel.transform as RectTransform;
        if (!worldCamera) worldCamera = Camera.main;
        if (!canvas || !panelRect || !(panelRect.parent is RectTransform) || !worldCamera ||
            (canvas.renderMode != RenderMode.ScreenSpaceOverlay && !canvas.worldCamera))
        {
            Debug.LogError("Prompt needs a Canvas, RectTransform parent and gameplay camera. Assign the Canvas camera when using Screen Space Camera or World Space.", this);
            enabled = false;
            return;
        }
        panel.interactable = false;
        panel.blocksRaycasts = false;
        promptText.raycastTarget = false;
        panel.alpha = 0f;
    }

    private void OnEnable() => Canvas.willRenderCanvases += FollowPlayer;

    private bool UpdatePosition()
    {
        if (!player || !worldCamera || !panelRect || !canvas) return false;
        Vector3 world = headAnchor ? headAnchor.position : player.position + headOffset;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z < worldCamera.nearClipPlane) return false;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
            panelRect.parent as RectTransform, screen, uiCamera, out Vector3 position)) return false;
        panelRect.position = position;
        return true;
    }

    private void FollowPlayer()
    {
        if (!UpdatePosition() && panel) panel.alpha = 0f;
    }

    private void LateUpdate()
    {
        GameInput input = GameInput.Instance;
        IInteractionPromptSource source = player && UpdatePosition() && input && input.PromptsAllowed
            ? InteractionPromptRegistry.Select(player.position) : null;
        if (source != null)
        {
            string label = input.GetBindingLabel(source.PromptAction);
            string text = label + " — " + source.PromptLabel;
            if (promptText.text != text) promptText.text = text;
        }
        panel.alpha = Mathf.MoveTowards(panel.alpha, source != null ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
        panel.transform.localScale = originalScale * Mathf.Lerp(0.96f, 1f, panel.alpha);
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= FollowPlayer;
        if (!panel) return;
        panel.alpha = 0f;
        // Awake might not have run yet on an inactive object.
        if (originalScale != Vector3.zero) panel.transform.localScale = originalScale;
    }
}
