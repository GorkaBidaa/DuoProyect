using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class CraftingPinUI : MonoBehaviour
{
    [SerializeField] private CraftingMenu craftingMenu;
    [SerializeField] private BindingIconSet bindingIcons;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Image bindingIcon;
    [SerializeField] private TMP_Text bindingFallbackText;
    [Tooltip("Optional separate badge near the selected recipe, showing PINNED.")]
    [SerializeField] private GameObject selectedPinnedMarker;
    [Tooltip("Optional text showing the recipe currently tracked by the HUD.")]
    [SerializeField] private TMP_Text pinnedStatusText;

    private Button button;
    private GameInput input;
    private bool displayGamepad, lastCanPin;

    private void Awake()
    {
        button = GetComponent<Button>();
        // The controller is outside the visual canvas in this project. An explicit reference
        // is preferred; this fallback also lets the visual prefab work with one scene controller.
        if (!craftingMenu) craftingMenu = FindFirstObjectByType<CraftingMenu>();
        if (!craftingMenu || !actionLabel || !bindingFallbackText)
        {
            Debug.LogError("CraftingPinUI: assign Crafting Menu, Action Label and Binding Fallback Text on the Pin button.", this);
            button.interactable = false;
            enabled = false;
            return;
        }
        if (selectedPinnedMarker && transform.IsChildOf(selectedPinnedMarker.transform))
        {
            Debug.LogWarning("Selected Pinned Marker must be a separate badge, not the Pin button or its parent.", this);
            selectedPinnedMarker = null;
        }
    }

    private void OnEnable()
    {
        if (!craftingMenu || !button) return;
        craftingMenu.ViewChanged += Refresh;
        button.onClick.AddListener(OnClick);
        ConnectInput();
        Refresh();
    }

    private void OnClick()
    {
        if (craftingMenu && craftingMenu.CanPinSelected) craftingMenu.PinSelected();
    }

    private void ConnectInput()
    {
        GameInput current = GameInput.Instance;
        if (current == input) return;
        if (input) input.BindingsChanged -= Refresh;
        input = current;
        displayGamepad = input && input.UsingGamepad;
        if (input) input.BindingsChanged += Refresh;
        Refresh();
    }

    private void LateUpdate()
    {
        ConnectInput();
        bool previousDevice = displayGamepad;
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f ||
                pad.rightStick.ReadValue().sqrMagnitude > 0.25f ||
                pad.dpad.ReadValue().sqrMagnitude > 0.01f) displayGamepad = true;
            foreach (var control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)
                    displayGamepad = true;
        }
        if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f ||
             Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f))) displayGamepad = false;
        if (previousDevice != displayGamepad || lastCanPin != craftingMenu.CanPinSelected) Refresh();
    }

    private void Refresh()
    {
        if (!button || !craftingMenu || !actionLabel || !bindingFallbackText) return;
        lastCanPin = craftingMenu.CanPinSelected;
        bool selectedIsPinned = craftingMenu.SelectedRecipe &&
            craftingMenu.SelectedRecipe == craftingMenu.PinnedRecipe;
        button.interactable = lastCanPin;
        actionLabel.text = selectedIsPinned ? "Unpin" : "Pin";
        if (selectedPinnedMarker) selectedPinnedMarker.SetActive(selectedIsPinned);
        if (pinnedStatusText)
            pinnedStatusText.text = craftingMenu.PinnedRecipe
                ? "Pinned: " + craftingMenu.PinnedRecipe.recipeName : "No recipe pinned";

        InputAction action = input ? input.Action("PinRecipe") : null;
        int bindingIndex = FindBinding(action);
        Sprite icon = null;
        string label = "Unbound";
        if (bindingIndex >= 0)
        {
            string display = action.GetBindingDisplayString(bindingIndex);
            if (!string.IsNullOrEmpty(display)) label = display;
            if (bindingIcons) bindingIcons.TryGetIcon(action, bindingIndex, out icon);
        }
        bool showIcon = bindingIcon && icon;
        if (bindingIcon)
        {
            bindingIcon.sprite = icon;
            bindingIcon.enabled = showIcon;
            bindingIcon.preserveAspect = true;
            bindingIcon.raycastTarget = false;
        }
        bindingFallbackText.text = label;
        bindingFallbackText.enabled = !showIcon;
    }

    private int FindBinding(InputAction action)
    {
        if (action == null) return -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
            string groups = binding.groups ?? "";
            string path = binding.effectivePath;
            bool gamepad = groups.Contains("Gamepad") || path.StartsWith("<Gamepad>", System.StringComparison.OrdinalIgnoreCase);
            bool keyboardMouse = groups.Contains("KeyboardMouse") ||
                path.StartsWith("<Keyboard>", System.StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("<Mouse>", System.StringComparison.OrdinalIgnoreCase);
            if (displayGamepad ? gamepad : keyboardMouse) return i;
        }
        return -1;
    }

    private void OnDisable()
    {
        if (button) button.onClick.RemoveListener(OnClick);
        if (craftingMenu) craftingMenu.ViewChanged -= Refresh;
        if (input) input.BindingsChanged -= Refresh;
        input = null;
    }
}
