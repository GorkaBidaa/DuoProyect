using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private MenuUICue clickSound = MenuUICue.Click;
    [Tooltip("Optional visual child to scale. If empty, scales this button.")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.035f;
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.96f;
    [SerializeField, Min(1f)] private float animationSpeed = 18f;
    private Button button;
    private Vector3 restScale;
    private bool hovered, selected, pressed;
    private bool initialized;
    private float clickPulseUntil;

    // A selected object remains in the EventSystem so controller navigation can resume.
    // Its selected visual only applies after actual keyboard/controller input.
    private static bool navigationMode;
    private static int checkedInputFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInputMode()
    {
        navigationMode = false;
        checkedInputFrame = -1;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        if (!visualRoot) visualRoot = transform as RectTransform;
        if (visualRoot) restScale = visualRoot.localScale;
        initialized = true;
        button.onClick.AddListener(OnClick);
    }

    private bool Available => button && button.IsActive() && button.IsInteractable();

    private void OnClick()
    {
        if (clickSound == MenuUICue.Back) MenuUISounds.PlayBack();
        else if (clickSound == MenuUICue.Click) MenuUISounds.PlayClick();
        clickPulseUntil = Time.unscaledTime + 0.09f;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        navigationMode = false;
        hovered = true;
        if (Available) MenuUISounds.PlayHover();
    }
    public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData data)
    {
        navigationMode = false;
        if (data.button == PointerEventData.InputButton.Left && Available) pressed = true;
    }
    public void OnPointerUp(PointerEventData data) { pressed = false; }
    public void OnSelect(BaseEventData data)
    {
        selected = true;
        UpdateInputMode();
        if (Available && navigationMode) MenuUISounds.PlayHover();
    }
    public void OnDeselect(BaseEventData data) { selected = false; pressed = false; }

    private static void UpdateInputMode()
    {
        if (checkedInputFrame == Time.frameCount) return;
        checkedInputFrame = Time.frameCount;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame ||
            keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.rightArrowKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame ||
            keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
            keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
            keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            navigationMode = true;
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue().sqrMagnitude > 0.01f)
                navigationMode = true;
            foreach (var control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)
                    navigationMode = true;
        }
        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.01f ||
            mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.scroll.ReadValue().sqrMagnitude > 0.01f)) navigationMode = false;
    }

    private void Update()
    {
        if (!visualRoot) return;
        UpdateInputMode();
        // The pointer can remain over a button after switching to controller navigation.
        bool highlighted = navigationMode ? selected : hovered;
        float target = !Available ? 1f : pressed || Time.unscaledTime < clickPulseUntil
            ? pressedScale : highlighted ? hoverScale : 1f;
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, restScale * target,
            1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime));
    }

    private void OnDisable()
    {
        hovered = selected = pressed = false;
        clickPulseUntil = 0f;
        if (initialized && visualRoot) visualRoot.localScale = restScale;
    }
    private void OnDestroy() { if (button) button.onClick.RemoveListener(OnClick); }
}
