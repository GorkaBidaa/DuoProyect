using System.Collections.Generic;
using TMPro;
using UnityEngine;

// One action/direction label, with as many alternative binding buttons as it needs.
public class KeybindRow : MonoBehaviour
{
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Transform bindingsRoot;
    [Tooltip("The existing ChangeBTN, with KeybindSlot attached, inside BindingsRoot. It becomes a hidden template in Play Mode.")]
    [SerializeField] private KeybindSlot slotTemplate;
    private readonly List<KeybindSlot> slots = new List<KeybindSlot>();
    public bool IsConfigured => actionLabel && bindingsRoot && slotTemplate &&
        slotTemplate.IsConfigured && bindingsRoot.IsChildOf(transform) &&
        bindingsRoot != transform && slotTemplate.transform.parent == bindingsRoot;

    private void Awake()
    {
        if (slotTemplate) slotTemplate.gameObject.SetActive(false);
    }

    public void Setup(string title)
    {
        actionLabel.text = title;
        slotTemplate.gameObject.SetActive(false);
        foreach (KeybindSlot slot in slots)
            if (slot) { slot.gameObject.SetActive(false); Destroy(slot.gameObject); }
        slots.Clear();
    }

    public KeybindSlot AddBinding(string binding, Sprite icon, UnityEngine.Events.UnityAction onChange)
    {
        KeybindSlot slot = Instantiate(slotTemplate, bindingsRoot, false);
        slot.name = "Binding_" + (slots.Count + 1);
        slot.Setup(binding, icon, onChange);
        slot.gameObject.SetActive(true);
        slots.Add(slot);
        return slot;
    }

    public void SetInteractable(bool value)
    {
        foreach (KeybindSlot slot in slots) if (slot) slot.SetInteractable(value);
    }

    public void Select()
    {
        if (slots.Count > 0 && slots[0]) slots[0].Select();
    }
}
