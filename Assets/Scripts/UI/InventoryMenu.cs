using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private ActiveItemSelector itemSelector;
    [SerializeField] private CraftingMenu craftingMenu;

    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Transform listRoot;
    [SerializeField] private UIItemSlot slotPrefab;
    [SerializeField] private TMP_Text collectionTitle;

    [Header("Details")]
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailType;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text ownedAmount;

    private enum Category { Items, Resources, Relics }

    private Category category;
    private UnityEngine.Object selected;
    private readonly List<UIItemSlot> spawnedSlots = new();

    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    private void Awake()
    {
        if (inventory == null || menuRoot == null ||
            listRoot == null || slotPrefab == null ||
            collectionTitle == null || detailImage == null ||
            detailName == null || detailType == null ||
            detailDescription == null || ownedAmount == null)
        {
            Debug.LogError(
                "Assign all inventory menu and detail references.", this);
            enabled = false;
            return;
        }

        menuRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryChanged += Refresh;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= Refresh;

        Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;

        bool toggle =
            (keyboard != null && keyboard.iKey.wasPressedThisFrame) ||
            (pad != null && pad.selectButton.wasPressedThisFrame);

        bool back =
            (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame);

        // Process closing first so the same press cannot reopen the menu.
        if (IsOpen && (toggle || back))
        {
            Close();
            return;
        }

        if (toggle && !IsOpen)
            Open();
    }

    public void Open()
    {
        if (!enabled || Time.timeScale <= 0f ||
            (craftingMenu != null && craftingMenu.IsOpen))
            return;

        menuRoot.SetActive(true);

        if (itemSelector != null)
            itemSelector.InputBlocked = true;

        Refresh();
    }

    public void Close()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        if (itemSelector != null)
            itemSelector.InputBlocked = false;
    }

    public void ShowItems() => ChangeCategory(Category.Items);
    public void ShowResources() => ChangeCategory(Category.Resources);
    public void ShowRelics() => ChangeCategory(Category.Relics);

    private void ChangeCategory(Category value)
    {
        category = value;
        selected = null;
        Refresh();
    }

    private void Refresh()
    {
        if (!IsOpen)
            return;

        foreach (UIItemSlot slot in spawnedSlots)
        {
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        spawnedSlots.Clear();
        collectionTitle.text = category.ToString();

        List<UnityEngine.Object> entries = new();

        if (category == Category.Items)
        {
            foreach (ItemSlot slot in inventory.GetAvailableItems())
            {
                if (!entries.Contains(slot.item))
                    entries.Add(slot.item);
            }
        }
        else if (category == Category.Resources)
        {
            foreach (InventorySlot slot in inventory.GetAvailableResources())
            {
                if (!entries.Contains(slot.resource))
                    entries.Add(slot.resource);
            }
        }

        // Relics will have entries once their data system exists.
        foreach (UnityEngine.Object entry in entries)
        {
            Sprite icon;
            int amount;

            if (entry is ItemData item)
            {
                icon = item.icon;
                amount = inventory.GetItemAmount(item);
            }
            else
            {
                ResourceData resource = (ResourceData)entry;
                icon = resource.icon;
                amount = inventory.GetResourceAmount(resource);
            }

            UIItemSlot square = Instantiate(slotPrefab, listRoot);
            square.gameObject.SetActive(true);
            square.Bind(icon, amount, () => Select(entry));
            spawnedSlots.Add(square);
        }

        if (!entries.Contains(selected))
            selected = entries.Count > 0 ? entries[0] : null;

        Select(selected);
    }

    private void Select(UnityEngine.Object entry)
    {
        selected = entry;

        Sprite icon = null;
        string name = "Nothing here yet";
        string type = "";
        string description = "This category is empty.";
        int amount = 0;

        if (entry is ItemData item)
        {
            icon = item.icon;
            name = item.itemName;
            type = "Item";
            description = item.description;
            amount = inventory.GetItemAmount(item);
        }
        else if (entry is ResourceData resource)
        {
            icon = resource.icon;
            name = resource.resourceName;
            type = "Resource";
            description = resource.description;
            amount = inventory.GetResourceAmount(resource);
        }

        detailImage.sprite = icon;
        detailImage.enabled = icon != null;
        detailName.text = name;
        detailType.text = type;
        detailDescription.text = description;
        ownedAmount.text = entry != null ? amount.ToString() : "";
    }
}