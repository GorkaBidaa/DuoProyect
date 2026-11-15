using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeybindPanel : MonoBehaviour
{
    [Header("Keep this controller outside Panel Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform listRoot;
    [SerializeField] private KeybindRow rowPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private BindingIconSet bindingIcons;

    [Header("Keyboard / controller selector")]
    [SerializeField] private Button deviceSwitchButton;
    [SerializeField] private Image deviceIcon;
    [SerializeField] private TMP_Text deviceLabel;
    [SerializeField] private Sprite keyboardIcon;
    [SerializeField] private Sprite controllerIcon;

    private bool showingGamepad;
    private bool lastUsedGamepad;
    private readonly List<KeybindRow> rows = new List<KeybindRow>();
    private InputActionAsset runtimeActions;
    private InputActionMap gameplay;
    private InputActionRebindingExtensions.RebindingOperation operation;
    private GameInput inputOwner;
    private float previousTimeScale;
    private bool ownsPause;
    private bool isOpen;
    private int captureFinishedFrame = -1;
    private GameObject lastSelection;
    private EventSystem captureEventSystem;
    private bool previousNavigationEvents;
    private int navigationRestoreFrame;
    public bool IsOpen => isOpen;
    public bool IsRebinding => operation != null;
    public bool ConsumesBackThisFrame => IsRebinding || Time.frameCount <= captureFinishedFrame;
    public GameObject PanelRoot => panelRoot;

    private void Awake()
    {
        if (!panelRoot || !listRoot || !rowPrefab || !rowPrefab.IsConfigured || !statusText ||
            !inputActions || !scrollRect || scrollRect.content != listRoot ||
            !scrollRect.viewport || transform.IsChildOf(panelRoot.transform) ||
            !listRoot.IsChildOf(panelRoot.transform))
        {
            Debug.LogError("KeybindPanel needs its Controls page, Scroll View Content, configured row prefab, status text, Input Actions and ScrollRect. Keep the controller outside its panel.", this);
            enabled = false;
            return;
        }
        runtimeActions = Instantiate(inputActions);
        runtimeActions.Disable();
        gameplay = runtimeActions.FindActionMap("Gameplay", false);
        if (gameplay == null)
        {
            Debug.LogError("KeybindPanel's Input Actions asset needs a Gameplay map.", this);
            enabled = false;
            return;
        }
        panelRoot.SetActive(false);
        if (deviceSwitchButton) deviceSwitchButton.onClick.AddListener(ToggleDevice);
        RefreshDeviceSelector();
    }

    // Preserves the old standalone entry point. The settings controller uses OpenAsTab instead.
    public void Open()
    {
        if (!CanOpen()) return;
        inputOwner = GameInput.Instance;
        if (inputOwner && inputOwner.DialogueBlocked) return;
        previousTimeScale = Time.timeScale;
        ownsPause = true;
        if (inputOwner) inputOwner.SetSettingsBlocked(true);
        Time.timeScale = 0f;
        ShowRows();
    }

    public void OpenAsTab()
    {
        if (!CanOpen()) return;
        ownsPause = false;
        ShowRows();
    }

    private bool CanOpen() => Application.isPlaying && isActiveAndEnabled && !isOpen && runtimeActions && gameplay != null;

    private void ShowRows()
    {
        InputBindingSettings.Load(runtimeActions);
        isOpen = true;
        panelRoot.SetActive(true);
        RememberInputDevice();
        showingGamepad = lastUsedGamepad;
        BuildRows();
        ShowDeviceInstructions();
    }

    public void Close()
    {
        if (!isOpen) return;
        CancelRebind();
        isOpen = false;
        if (panelRoot) panelRoot.SetActive(false);
        if (ownsPause)
        {
            if (inputOwner) inputOwner.SetSettingsBlocked(false);
            Time.timeScale = previousTimeScale;
        }
        ownsPause = false;
        inputOwner = null;
    }

    private void BuildRows()
    {
        foreach (KeybindRow row in rows)
            if (row) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
        foreach (InputAction action in gameplay.actions)
        {
            // Alternatives for one action share a row; composite directions keep their own row.
            var actionRows = new Dictionary<string, KeybindRow>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (binding.isComposite || !MatchesSelectedDevice(binding)) continue;
                string group = binding.isPartOfComposite ? "part:" + binding.name : "action";
                if (!actionRows.TryGetValue(group, out KeybindRow row))
                {
                    string title = action.name;
                    if (binding.isPartOfComposite)
                        title += binding.name == "negative" ? " Left" : binding.name == "positive" ? " Right" : " " + binding.name;
                    row = Instantiate(rowPrefab, listRoot);
                    row.gameObject.SetActive(true);
                    row.Setup(title);
                    actionRows.Add(group, row);
                    rows.Add(row);
                }
                InputAction target = action;
                int bindingIndex = index;
                KeybindSlot slot = null;
                slot = row.AddBinding(Display(action, index), IconFor(action, index),
                    () => BeginRebind(target, bindingIndex, slot));
            }
        }
        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.verticalNormalizedPosition = 1f;
        lastSelection = null;
        RefreshDeviceSelector();
    }

    private static string Display(InputAction action, int index)
    {
        string display = action.GetBindingDisplayString(index);
        return string.IsNullOrEmpty(display) ? "Unbound" : display;
    }

    private Sprite IconFor(InputAction action, int index)
    {
        if (bindingIcons && bindingIcons.TryGetIcon(action, index, out Sprite icon)) return icon;
        return null;
    }

    private void RefreshBinding(KeybindSlot slot, InputAction action, int index)
    {
        if (slot) slot.SetBinding(Display(action, index), IconFor(action, index));
    }

    private static bool IsGamepad(InputBinding binding) =>
        !string.IsNullOrEmpty(binding.groups) && binding.groups.Contains("Gamepad");

    private bool MatchesSelectedDevice(InputBinding binding)
    {
        if (showingGamepad) return IsGamepad(binding);
        if (IsGamepad(binding)) return false;
        string path = binding.effectivePath ?? binding.path ?? "";
        return (!string.IsNullOrEmpty(binding.groups) && binding.groups.Contains("KeyboardMouse")) ||
            path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase);
    }

    public void ToggleDevice() => SelectDevice(!showingGamepad);
    public void ShowKeyboard() => SelectDevice(false);
    public void ShowController() => SelectDevice(true);

    private void SelectDevice(bool gamepad)
    {
        if (!isOpen || IsRebinding || Time.frameCount <= captureFinishedFrame) return;
        // Pointer/controller input must not replace this manual choice while the tab stays open.
        showingGamepad = gamepad;
        bool rowHadFocus = EventSystem.current && EventSystem.current.currentSelectedGameObject &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(listRoot);
        BuildRows();
        ShowDeviceInstructions();
        if (rowHadFocus && rows.Count > 0) rows[0].Select();
    }

    private void RefreshDeviceSelector()
    {
        if (deviceSwitchButton) deviceSwitchButton.interactable = !IsRebinding;
        Sprite icon = showingGamepad ? controllerIcon : keyboardIcon;
        if (deviceIcon)
        {
            deviceIcon.sprite = icon;
            deviceIcon.enabled = icon != null;
            deviceIcon.preserveAspect = true;
        }
        if (deviceLabel) deviceLabel.text = showingGamepad ? "Controller" : "Keyboard";
    }

    private void ShowDeviceInstructions()
    {
        statusText.text = showingGamepad && Gamepad.current == null
            ? "Controller bindings. Connect a controller to change a binding."
            : "Select a binding to change it. Changes save automatically. Escape cancels capture.";
    }

    private void RememberInputDevice()
    {
        // Run even while Controls is hidden, so opening from the main menu uses recent input.
        if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f)))
            lastUsedGamepad = false;
        Gamepad pad = Gamepad.current;
        if (pad == null) return;
        if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f)
            lastUsedGamepad = true;
        foreach (var control in pad.allControls)
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                lastUsedGamepad = true;
    }

    private void BeginRebind(InputAction action, int index, KeybindSlot slot)
    {
        if (!isOpen || operation != null || Time.frameCount <= captureFinishedFrame) return;
        InputBinding previous = action.bindings[index];
        bool gamepad = IsGamepad(previous);
        if (gamepad && Gamepad.current == null)
        {
            statusText.text = "Connect a controller to change this binding.";
            return;
        }
        RestoreUINavigation();
        captureEventSystem = EventSystem.current;
        if (captureEventSystem)
        {
            previousNavigationEvents = captureEventSystem.sendNavigationEvents;
            captureEventSystem.sendNavigationEvents = false;
        }
        foreach (KeybindRow item in rows) item.SetInteractable(false);
        statusText.text = "Listening for " + action.name + ". Press Escape or gamepad Start to cancel.";
        slot.SetBinding("Listening...");
        try
        {
            operation = action.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Gamepad>/start")
                .WithControlsExcluding("<Mouse>/leftButton")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnCancel(op =>
                {
                    FinishCapture();
                    RefreshBinding(slot, action, index);
                    if (statusText) statusText.text = "Cancelled. Previous binding kept.";
                })
                .OnComplete(op =>
                {
                    string conflict = FindConflict(action, index);
                    if (conflict != null)
                    {
                        action.RemoveBindingOverride(index);
                        if (previous.hasOverrides) action.ApplyBindingOverride(index, previous);
                    }
                    FinishCapture();
                    RefreshBinding(slot, action, index);
                    if (conflict != null) statusText.text = "Already used by " + conflict + ". Previous binding kept.";
                    else
                    {
                        InputBindingSettings.Save(runtimeActions);
                        statusText.text = "Saved: " + action.name + " - " + Display(action, index);
                    }
                });
            if (gamepad) operation.WithControlsHavingToMatchPath("<Gamepad>/*");
            else
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>/*");
                operation.WithControlsHavingToMatchPath("<Mouse>/*");
            }
            if (previous.isPartOfComposite) operation.WithExpectedControlType("Button");
            if (action.name != "CycleItem") operation.WithControlsExcluding("<Mouse>/scroll");
            if (action.name == "CycleItem") operation.WithExpectedControlType("Axis");
            RefreshDeviceSelector();
            operation.Start();
        }
        catch (Exception error)
        {
            FinishCapture();
            RefreshBinding(slot, action, index);
            statusText.text = "Could not start capture: " + error.Message;
        }
    }

    private string FindConflict(InputAction changed, int index)
    {
        string path = changed.bindings[index].effectivePath;
        foreach (InputAction action in gameplay.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action == changed && i == index) continue;
                if (action.bindings[i].isComposite || !string.Equals(path, action.bindings[i].effectivePath, StringComparison.OrdinalIgnoreCase)) continue;
                bool intentionalShare = string.Equals(path, "<Gamepad>/buttonNorth", StringComparison.OrdinalIgnoreCase) &&
                    ((changed.name == "UseItem" && action.name == "Interact") || (changed.name == "Interact" && action.name == "UseItem"));
                if (!intentionalShare) return action.name;
            }
        }
        return null;
    }

    private void Update()
    {
        RememberInputDevice();
        if (operation == null && captureEventSystem && Time.frameCount >= navigationRestoreFrame)
            RestoreUINavigation();
        if (!isOpen) return;
        // Keep UI submit/navigation from also activating another control while capturing.
        if (IsRebinding)
        {
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) CancelRebind();
            return;
        }
        if (ownsPause && !ConsumesBackThisFrame &&
            ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
             (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame ||
              Gamepad.current.startButton.wasPressedThisFrame)))) Close();
    }

    private void LateUpdate()
    {
        if (!isOpen || !EventSystem.current || !scrollRect || !scrollRect.content) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (!selected || selected == lastSelection) return;
        lastSelection = selected;
        if (!selected.transform.IsChildOf(listRoot)) return;
        RectTransform selectedRect = selected.transform as RectTransform;
        if (!selectedRect) return;
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = scrollRect.viewport;
        float scrollableHeight = scrollRect.content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 0f) return;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selectedRect);
        float correction = bounds.max.y > viewport.rect.yMax ? bounds.max.y - viewport.rect.yMax :
            bounds.min.y < viewport.rect.yMin ? bounds.min.y - viewport.rect.yMin : 0f;
        if (Mathf.Approximately(correction, 0f)) return;
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + correction / scrollableHeight);
    }

    private void FinishCapture()
    {
        captureFinishedFrame = Time.frameCount;
        var finished = operation;
        operation = null;
        finished?.Dispose();
        // A newly assigned Submit/Cancel key must not also activate the UI this frame.
        navigationRestoreFrame = Time.frameCount + 1;
        foreach (KeybindRow item in rows) if (item) item.SetInteractable(true);
        RefreshDeviceSelector();
    }

    private void RestoreUINavigation()
    {
        if (captureEventSystem) captureEventSystem.sendNavigationEvents = previousNavigationEvents;
        captureEventSystem = null;
    }

    public void CancelRebind() { if (operation != null) operation.Cancel(); }

    public void RestoreDefaults()
    {
        if (!isOpen || operation != null || !runtimeActions) return;
        InputBindingSettings.RestoreDefaults();
        InputBindingSettings.Load(runtimeActions);
        BuildRows();
        statusText.text = "Keyboard and controller defaults restored and saved.";
    }

    private void OnDisable()
    {
        Close();
        RestoreUINavigation();
    }
    private void OnDestroy()
    {
        CancelRebind();
        RestoreUINavigation();
        if (deviceSwitchButton) deviceSwitchButton.onClick.RemoveListener(ToggleDevice);
        if (runtimeActions) Destroy(runtimeActions);
    }
}
