using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeybindPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform listRoot;
    [SerializeField] private KeybindRow rowPrefab;
    [SerializeField] private TMP_Text statusText;
    private readonly List<KeybindRow> rows = new List<KeybindRow>();
    private GameInput input;
    private InputActionRebindingExtensions.RebindingOperation operation;
    private InputAction captureAction;
    private bool captureWasEnabled;
    private float previousTimeScale;
    private bool isOpen;
    private int captureFinishedFrame = -1;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (!panelRoot || !listRoot || !rowPrefab || !rowPrefab.IsConfigured || !statusText ||
            transform.IsChildOf(panelRoot.transform))
        {
            Debug.LogError("Assign KeybindPanel's panel, list, configured row prefab and status text. Keep this controller outside its panel.", this);
            enabled = false;
            return;
        }
        panelRoot.SetActive(false);
    }

    public void Open()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || isOpen) return;
        input = GameInput.Instance;
        if (!input || input.DialogueBlocked)
        {
            Debug.LogWarning("Controls cannot open while input is unavailable or another input-blocking screen is active.", this);
            return;
        }
        isOpen = true;
        previousTimeScale = Time.timeScale;
        input.SetSettingsBlocked(true);
        Time.timeScale = 0f;
        panelRoot.SetActive(true);
        BuildRows();
        statusText.text = "Changes save automatically. Escape cancels capture. Mouse left-click and gamepad Start are reserved for UI/cancel.";
    }

    public void Close()
    {
        if (!isOpen) return;
        CancelRebind();
        isOpen = false;
        if (panelRoot) panelRoot.SetActive(false);
        if (input) input.SetSettingsBlocked(false);
        Time.timeScale = previousTimeScale;
    }

    private void BuildRows()
    {
        foreach (KeybindRow row in rows)
            if (row) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
        foreach (InputAction action in input.GameplayActions.actions)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (action.bindings[index].isComposite) continue;
                InputAction target = action;
                int bindingIndex = index;
                var binding = action.bindings[index];
                string group = IsGamepad(binding) ? "Gamepad" : "Keyboard / Mouse";
                string title = action.name;
                if (binding.isPartOfComposite)
                    title += binding.name == "negative" ? " Left" : binding.name == "positive" ? " Right" : " " + binding.name;
                KeybindRow row = Instantiate(rowPrefab, listRoot);
                row.gameObject.SetActive(true);
                row.Setup(title + " - " + group, Display(action, index), () => BeginRebind(target, bindingIndex, row));
                rows.Add(row);
            }
        }
        if (input.UsingGamepad && rows.Count > 0) rows[0].Select();
    }

    private static string Display(InputAction action, int index)
    {
        string display = action.GetBindingDisplayString(index);
        return string.IsNullOrEmpty(display) ? "Unbound" : display;
    }

    private static bool IsGamepad(InputBinding binding) =>
        !string.IsNullOrEmpty(binding.groups) && binding.groups.Contains("Gamepad");

    private void BeginRebind(InputAction action, int index, KeybindRow row)
    {
        if (!isOpen || operation != null || Time.frameCount <= captureFinishedFrame) return;
        InputBinding previous = action.bindings[index];
        bool gamepad = IsGamepad(previous);
        captureAction = action;
        captureWasEnabled = action.enabled;
        action.Disable();
        foreach (KeybindRow item in rows) item.SetInteractable(false);
        statusText.text = "Listening for " + action.name + ". Press Escape or gamepad Start to cancel.";
        row.SetBinding("Listening...");
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
                    row.SetBinding(Display(action, index));
                    statusText.text = "Cancelled. Previous binding kept.";
                })
                .OnComplete(op =>
                {
                    string conflict = FindConflict(action, index);
                    if (conflict != null)
                    {
                        action.RemoveBindingOverride(index);
                        action.ApplyBindingOverride(index, previous);
                    }
                    FinishCapture();
                    row.SetBinding(Display(action, index));
                    if (conflict != null) statusText.text = "Already used by " + conflict + ". Previous binding kept.";
                    else { input.SaveBindings(); statusText.text = "Saved: " + action.name + " - " + Display(action, index); }
                });
            if (gamepad) operation.WithControlsHavingToMatchPath("<Gamepad>/*");
            else
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>/*");
                // Keyboard/mouse share one binding group. Explicitly include mouse controls.
                operation.WithControlsHavingToMatchPath("<Mouse>/*");
            }
            if (previous.isPartOfComposite) operation.WithExpectedControlType("Button");
            if (action.name != "CycleItem") operation.WithControlsExcluding("<Mouse>/scroll");
            if (action.name == "CycleItem") operation.WithExpectedControlType("Axis");
            operation.Start();
        }
        catch (Exception e)
        {
            FinishCapture();
            row.SetBinding(Display(action, index));
            statusText.text = "Could not start capture: " + e.Message;
        }
    }

    private string FindConflict(InputAction changed, int index)
    {
        string path = changed.bindings[index].effectivePath;
        foreach (InputAction action in input.GameplayActions.actions)
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
        if (!isOpen || operation == null) return;
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) CancelRebind();
    }

    private void FinishCapture()
    {
        captureFinishedFrame = Time.frameCount;
        var finished = operation;
        operation = null;
        finished?.Dispose();
        if (captureAction != null && captureWasEnabled && input && input.isActiveAndEnabled) captureAction.Enable();
        captureAction = null;
        foreach (KeybindRow item in rows) if (item) item.SetInteractable(true);
    }

    public void CancelRebind() { if (operation != null) operation.Cancel(); }

    public void RestoreDefaults()
    {
        if (!isOpen || operation != null || !input) return;
        input.RestoreDefaults();
        BuildRows();
        statusText.text = "Default bindings restored and saved.";
    }

    private void OnDisable() => Close();
}
