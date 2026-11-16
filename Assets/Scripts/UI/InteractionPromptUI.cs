using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [Tooltip("Action label: Craft, Attach rope, Talk, etc.")]
    [SerializeField] private TMP_Text promptText;
    [Header("Binding display")]
    [SerializeField] private BindingIconSet bindingIcons;
    [SerializeField] private Image bindingIcon;
    [SerializeField] private TMP_Text bindingFallbackText;
    [Header("Follow player")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.12f;
    [SerializeField] private bool scaleOnShow = true;
    [SerializeField, Range(0f, 0.2f)] private float scaleAmount = 0.04f;

    private Vector3 originalScale;
    private Canvas canvas;
    private RectTransform panelRect;
    private GameInput input;
    private string lastAction;
    private bool lastGamepad;
    private bool dirtyBinding = true;
    private bool showingIcon;
    private string bindingLabel = "Unbound";

    private void Awake()
    {
        if (!player || !panel || !promptText)
        {
            Debug.LogError("Assign InteractionPromptUI's player, panel CanvasGroup and text.", this);
            enabled = false;
            return;
        }
        if (bindingFallbackText == promptText)
        {
            Debug.LogWarning("Use separate TMP objects for Action Label and Binding Fallback Text. Falling back to combined text.", this);
            bindingFallbackText = null;
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
        if (bindingIcon)
        {
            bindingIcon.raycastTarget = false;
            bindingIcon.preserveAspect = true;
            bindingIcon.enabled = false;
        }
        if (bindingFallbackText) bindingFallbackText.raycastTarget = false;
        panel.alpha = 0f;
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += FollowPlayer;
        dirtyBinding = true;
    }

    private void ConnectInput()
    {
        GameInput current = GameInput.Instance;
        if (current == input) return;
        if (input) input.BindingsChanged -= InvalidateBinding;
        input = current;
        if (input) input.BindingsChanged += InvalidateBinding;
        dirtyBinding = true;
    }

    private void InvalidateBinding() => dirtyBinding = true;

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
        ConnectInput();
        IInteractionPromptSource source = player && UpdatePosition() && input && input.PromptsAllowed
            ? InteractionPromptRegistry.Select(player.position) : null;
        if (source != null)
        {
            if (dirtyBinding || lastAction != source.PromptAction || lastGamepad != input.UsingGamepad)
                RefreshBinding(source.PromptAction);
            bool separateBinding = showingIcon || bindingFallbackText;
            string text = separateBinding ? source.PromptLabel : bindingLabel + " - " + source.PromptLabel;
            if (promptText.text != text) promptText.text = text;
        }
        panel.alpha = Mathf.MoveTowards(panel.alpha, source != null ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
        panel.transform.localScale = scaleOnShow
            ? originalScale * Mathf.Lerp(1f - scaleAmount, 1f, panel.alpha) : originalScale;
    }

    private void RefreshBinding(string actionName)
    {
        dirtyBinding = false;
        lastAction = actionName;
        lastGamepad = input.UsingGamepad;
        InputAction action = input.GameplayActions != null && !string.IsNullOrEmpty(actionName)
            ? input.GameplayActions.FindAction(actionName, false) : null;
        int index = FindBinding(action, lastGamepad);
        Sprite icon = null;
        bindingLabel = "Unbound";
        if (index >= 0)
        {
            string label = action.GetBindingDisplayString(index);
            if (!string.IsNullOrEmpty(label)) bindingLabel = label;
            if (bindingIcons) bindingIcons.TryGetIcon(action, index, out icon);
        }
        showingIcon = bindingIcon && icon;
        if (bindingIcon)
        {
            bindingIcon.sprite = icon;
            bindingIcon.enabled = showingIcon;
        }
        if (bindingFallbackText)
        {
            bindingFallbackText.text = bindingLabel;
            bindingFallbackText.enabled = !showingIcon;
        }
    }

    private static int FindBinding(InputAction action, bool gamepad)
    {
        if (action == null) return -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            string path = binding.effectivePath;
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(path)) continue;
            int end = path.IndexOf('>');
            string layout = path.StartsWith("<") && end > 1 ? path.Substring(1, end - 1) : null;
            bool isPad = layout != null &&
                (layout.Equals("Gamepad", System.StringComparison.OrdinalIgnoreCase) ||
                 InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad"));
            bool isKeyboardMouse = layout != null &&
                (layout.Equals("Keyboard", System.StringComparison.OrdinalIgnoreCase) ||
                 layout.Equals("Mouse", System.StringComparison.OrdinalIgnoreCase) ||
                 InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard") ||
                 InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse"));
            if (gamepad ? isPad : isKeyboardMouse) return i;
        }
        return -1;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= FollowPlayer;
        if (input) input.BindingsChanged -= InvalidateBinding;
        input = null;
        dirtyBinding = true;
        if (!panel) return;
        panel.alpha = 0f;
        if (originalScale != Vector3.zero) panel.transform.localScale = originalScale;
    }
}
