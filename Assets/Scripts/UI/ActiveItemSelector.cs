using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public class ActiveItemUseEvent : UnityEvent<ItemData> { }

public class ActiveItemSelector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private CraftingMenu craftingMenu;

    

    [Header("HUD")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Use request: connect actual item behavior later")]
    public ActiveItemUseEvent onUseRequested = new ActiveItemUseEvent();

    private readonly List<ItemData> availableItems = new List<ItemData>();
    private int selectedIndex;
    public bool InputBlocked { get; set; }
    public ItemData SelectedItem => availableItems.Count == 0
        ? null : availableItems[selectedIndex];

    private bool CanReadInput =>
    !InputBlocked &&
    Time.timeScale > 0f &&
    (craftingMenu == null || !craftingMenu.IsOpen);

    private void OnEnable()
    {
        if (inventory == null || itemIcon == null || itemNameText == null ||
            amountText == null || previousButton == null || nextButton == null)
        {
            Debug.LogError("Assign all inventory and HUD references on ActiveItemSelector.", this);
            enabled = false;
            return;
        }
        inventory.InventoryChanged += RefreshItems;
        previousButton.onClick.AddListener(Previous);
        nextButton.onClick.AddListener(Next);
        RefreshItems();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.InventoryChanged -= RefreshItems;
        if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
        if (nextButton != null) nextButton.onClick.RemoveListener(Next);
    }

    private void Update()
    {
        if (!CanReadInput) return;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (scroll > 0f || (pad != null && pad.rightShoulder.wasPressedThisFrame)) Next();
        else if (scroll < 0f || (pad != null && pad.leftShoulder.wasPressedThisFrame)) Previous();

        bool usePressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
            (pad != null && pad.buttonNorth.wasPressedThisFrame);
        if (usePressed) RequestUse();
    }

    public void Next() => Cycle(1);
    public void Previous() => Cycle(-1);

    private void Cycle(int direction)
    {
        if (!CanReadInput || availableItems.Count == 0) return;
        selectedIndex = (selectedIndex + direction + availableItems.Count) % availableItems.Count;
        RefreshDisplay();
    }

    private void RefreshItems()
    {
        ItemData previousItem = SelectedItem;
        int previousIndex = selectedIndex;
        availableItems.Clear();
        foreach (ItemSlot slot in inventory.GetAvailableItems())
            if (!availableItems.Contains(slot.item)) availableItems.Add(slot.item);
        int preservedIndex = availableItems.IndexOf(previousItem);
        selectedIndex = preservedIndex >= 0 ? preservedIndex :
            Mathf.Clamp(previousIndex, 0, Mathf.Max(0, availableItems.Count - 1));
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        ItemData item = SelectedItem;
        itemIcon.sprite = item != null ? item.icon : null;
        itemIcon.enabled = item != null && item.icon != null;
        itemNameText.text = item != null ? item.itemName : "No items";
        amountText.text = item != null ? $"x{inventory.GetItemAmount(item)}" : "";
        previousButton.interactable = availableItems.Count > 1;
        nextButton.interactable = availableItems.Count > 1;
    }

    public void RequestUse()
    {
        ItemData item = SelectedItem;
        if (!CanReadInput || item == null || inventory.GetItemAmount(item) <= 0) return;
        Debug.Log($"Use requested: {item.itemName}", this);
        onUseRequested.Invoke(item);
        // The item behavior must call RemoveItem only after a successful action.
    }
}
