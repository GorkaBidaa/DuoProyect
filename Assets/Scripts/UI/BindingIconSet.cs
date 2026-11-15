using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "BindingIcons", menuName = "UI/Binding Icon Set")]
public sealed class BindingIconSet : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        [Tooltip("Input System control path, for example <Keyboard>/space or <Gamepad>/buttonSouth.")]
        public string controlPath;
        public Sprite sprite;
        public Entry(string path) { controlPath = path; }
    }

    [Tooltip("Default keyboard controls are prefilled. Add entries for any other keys you want illustrated.")]
    [SerializeField]
    private List<Entry> keyboardIcons = new List<Entry>
    {
        new Entry("<Keyboard>/a"), new Entry("<Keyboard>/d"),
        new Entry("<Keyboard>/w"), new Entry("<Keyboard>/f"),
        new Entry("<Keyboard>/i"), new Entry("<Keyboard>/p"),
        new Entry("<Keyboard>/e"), new Entry("<Keyboard>/space"),
        new Entry("<Keyboard>/escape"), new Entry("<Keyboard>/leftShift"),
        new Entry("<Keyboard>/leftArrow"), new Entry("<Keyboard>/rightArrow"),
        new Entry("<Keyboard>/upArrow"), new Entry("<Keyboard>/q")
    };

    [SerializeField]
    private List<Entry> mouseIcons = new List<Entry>
    {
        new Entry("<Mouse>/rightButton"), new Entry("<Mouse>/scroll/y"),
        new Entry("<Mouse>/leftButton"), new Entry("<Mouse>/middleButton"),
        new Entry("<Mouse>/forwardButton"), new Entry("<Mouse>/backButton")
    };

    [Tooltip("Assign a consistent controller artwork family, such as PlayStation or Xbox.")]
    [SerializeField]
    private List<Entry> controllerIcons = new List<Entry>
    {
        new Entry("<Gamepad>/buttonSouth"), new Entry("<Gamepad>/buttonNorth"),
        new Entry("<Gamepad>/buttonEast"), new Entry("<Gamepad>/buttonWest"),
        new Entry("<Gamepad>/select"), new Entry("<Gamepad>/start"),
        new Entry("<Gamepad>/leftTrigger"), new Entry("<Gamepad>/rightTrigger"),
        new Entry("<Gamepad>/leftShoulder"), new Entry("<Gamepad>/rightShoulder"),
        new Entry("<Gamepad>/leftStick/x"), new Entry("<Gamepad>/dpad/x"),
        new Entry("<Gamepad>/leftStick"), new Entry("<Gamepad>/rightStick"),
        new Entry("<Gamepad>/leftStickPress"), new Entry("<Gamepad>/rightStickPress"),
        new Entry("<Gamepad>/dpad"), new Entry("<Gamepad>/dpad/up"),
        new Entry("<Gamepad>/dpad/down"), new Entry("<Gamepad>/dpad/left"),
        new Entry("<Gamepad>/dpad/right")
    };

    [NonSerialized] private Dictionary<string, Sprite> icons;

    public bool TryGetIcon(InputAction action, int bindingIndex, out Sprite icon)
    {
        icon = null;
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return false;
        InputBinding binding = action.bindings[bindingIndex];
        if (binding.isComposite) return false;
        return TryGetIcon(binding.effectivePath, out icon);
    }

    public bool TryGetIcon(string controlPath, out Sprite icon)
    {
        icon = null;
        if (string.IsNullOrWhiteSpace(controlPath)) return false;
        EnsureLookup();
        string path = controlPath.Trim();
        // Exact layout-specific entries take precedence over generic controller artwork.
        if (Find(path, out icon)) return true;
        path = GenericDevicePath(path);
        if (Find(path, out icon)) return true;
        // Axis bindings can use the whole stick/d-pad/scroll glyph when no axis glyph is mapped.
        if (path.EndsWith("/x", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/y", StringComparison.OrdinalIgnoreCase))
            return Find(path.Substring(0, path.Length - 2), out icon);
        return false;
    }

    private bool Find(string path, out Sprite icon)
    {
        return icons.TryGetValue(path, out icon) && icon != null;
    }

    private static string GenericDevicePath(string path)
    {
        if (!path.StartsWith("<", StringComparison.Ordinal)) return path;
        int end = path.IndexOf('>');
        if (end <= 1) return path;
        string layout = path.Substring(1, end - 1);
        string family = InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad") ? "Gamepad" :
            InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard") ? "Keyboard" :
            InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse") ? "Mouse" : null;
        return family == null ? path : "<" + family + ">" + path.Substring(end + 1);
    }

    private void EnsureLookup()
    {
        if (icons != null) return;
        icons = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        AddEntries(keyboardIcons);
        AddEntries(mouseIcons);
        AddEntries(controllerIcons);
    }

    private void AddEntries(List<Entry> entries)
    {
        if (entries == null) return;
        foreach (Entry entry in entries)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.controlPath) && entry.sprite)
                icons[entry.controlPath.Trim()] = entry.sprite;
    }

    private void OnEnable() => icons = null;
    private void OnValidate() => icons = null;
}
