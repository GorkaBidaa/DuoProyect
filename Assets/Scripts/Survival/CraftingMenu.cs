using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CraftingMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private InventoryMenu inventoryMenu;
    [SerializeField] private RecipeData[] recipes;

    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Transform recipeList;
    [SerializeField] private UIRecipeButton recipeButtonPrefab;
    [SerializeField] private Transform requirementsList;
    [SerializeField] private UIRequiredResource requirementPrefab;

    [Header("Details")]
    [SerializeField] private TMP_Text itemTitle;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private Button craftButton;

    [Header("HUD tracker")]
    [SerializeField] private PlayerHUD hud;
    [SerializeField] private GameObject pinnedPanel;

    private readonly List<GameObject> recipeRows = new();
    private readonly List<GameObject> requirementRows = new();

    // 0 = items, 1 = resources, 2 = upgrades.
    private int category;
    private bool atBase;
    private bool crafting;
    private RecipeData selectedRecipe;
    private RecipeData pinnedRecipe;

    public PlayerInventory Inventory => inventory;
    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    private void Awake()
    {
        if (inventory == null || menuRoot == null ||
            recipeList == null || recipeButtonPrefab == null ||
            requirementsList == null || requirementPrefab == null ||
            itemTitle == null || descriptionText == null ||
            itemImage == null || warningText == null ||
            craftButton == null || hud == null || pinnedPanel == null)
        {
            Debug.LogError("Assign all CraftingMenu references.", this);
            enabled = false;
            return;
        }

        menuRoot.SetActive(false);
        pinnedPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= OnInventoryChanged;

        Close();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;

        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame))
        {
            Close();
            return;
        }

        if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            PinSelected();
    }

    public void SetAtBase(bool value)
    {
        atBase = value;

        if (!atBase)
            Close();

        if (enabled)
            Refresh();
    }

    public void Open()
    {
        if (!enabled || !atBase || Time.timeScale <= 0f ||
            (inventoryMenu != null && inventoryMenu.IsOpen))
            return;

        menuRoot.SetActive(true);
        BuildRecipeList();
    }

    public void Close()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    public void ShowItems() => ChangeCategory(0);
    public void ShowResources() => ChangeCategory(1);
    public void ShowUpgrades() => ChangeCategory(2);

    private void ChangeCategory(int value)
    {
        category = value;
        selectedRecipe = null;

        if (IsOpen)
            BuildRecipeList();
    }

    private void BuildRecipeList()
    {
        ClearRows(recipeRows);
        List<RecipeData> visible = new();

        if (recipes != null)
        {
            foreach (RecipeData recipe in recipes)
            {
                if (recipe == null || visible.Contains(recipe))
                    continue;

                bool matches =
                    (category == 0 &&
                     recipe.outputType == RecipeOutputType.Item) ||
                    (category == 1 &&
                     recipe.outputType == RecipeOutputType.Resource);

                if (!matches)
                    continue;

                visible.Add(recipe);

                UIRecipeButton row =
                    Instantiate(recipeButtonPrefab, recipeList);

                row.gameObject.SetActive(true);
                row.Bind(recipe, () => SelectRecipe(recipe));
                recipeRows.Add(row.gameObject);
            }
        }

        if (!visible.Contains(selectedRecipe))
            selectedRecipe = visible.Count > 0 ? visible[0] : null;

        Refresh();
    }

    private void SelectRecipe(RecipeData recipe)
    {
        selectedRecipe = recipe;
        Refresh();
    }

    private void OnInventoryChanged()
    {
        if (!crafting)
            Refresh();
    }

    private void Refresh()
    {
        RefreshPinned();

        if (!IsOpen)
            return;

        ClearRows(requirementRows);
        RecipeData recipe = selectedRecipe;

        itemTitle.text = recipe != null
            ? recipe.recipeName : "No recipes";
        descriptionText.text = recipe != null
            ? $"{recipe.description}\n\nProduces: {recipe.outputAmount}"
            : "No recipes are available in this category.";

        Sprite icon = null;

        if (recipe != null)
        {
            if (recipe.outputType == RecipeOutputType.Item)
                icon = recipe.itemOutput != null
                    ? recipe.itemOutput.icon : null;
            else
                icon = recipe.resourceOutput != null
                    ? recipe.resourceOutput.icon : null;
        }

        itemImage.sprite = icon;
        itemImage.enabled = icon != null;

        bool valid = recipe != null && recipe.IsValid();
        bool canCraft = valid && recipe.CanCraft(inventory);
        craftButton.interactable = atBase && canCraft;

        warningText.text = recipe == null ? "No recipe selected."
            : !valid ? "This recipe is not configured correctly."
            : !atBase ? "Return to a crafting station."
            : !canCraft ? "Not enough resources."
            : "";

        if (!valid)
            return;

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            UIRequiredResource row =
                Instantiate(requirementPrefab, requirementsList);

            row.gameObject.SetActive(true);
            row.Bind(
                ingredient.resource,
                inventory.GetResourceAmount(ingredient.resource),
                ingredient.amount);

            requirementRows.Add(row.gameObject);
        }
    }

    public void CraftSelected()
    {
        RecipeData recipe = selectedRecipe;

        if (crafting || !atBase || !IsOpen ||
            recipe == null || !recipe.CanCraft(inventory))
            return;

        crafting = true;

        try
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                inventory.RemoveResource(
                    ingredient.resource, ingredient.amount);

            if (recipe.outputType == RecipeOutputType.Item)
                inventory.AddItem(recipe.itemOutput, recipe.outputAmount);
            else
                inventory.AddResource(
                    recipe.resourceOutput, recipe.outputAmount);
        }
        finally
        {
            crafting = false;
            Refresh();
        }
    }

    public void PinSelected()
    {
        if (!IsOpen || selectedRecipe == null ||
            !selectedRecipe.IsValid())
            return;

        // Pinning the same recipe again clears the tracker.
        pinnedRecipe = pinnedRecipe == selectedRecipe
            ? null : selectedRecipe;

        RefreshPinned();
    }

    public void Unpin()
    {
        pinnedRecipe = null;
        RefreshPinned();
    }

    private void RefreshPinned()
    {
        if (pinnedPanel == null || hud == null)
            return;

        bool show = pinnedRecipe != null;
        pinnedPanel.SetActive(show);

        if (show)
            hud.SetPinnedRecipe(
                pinnedRecipe.recipeName,
                pinnedRecipe.GetRequirements(inventory));
    }

    private static void ClearRows(List<GameObject> rows)
    {
        foreach (GameObject row in rows)
        {
            if (row == null)
                continue;

            row.SetActive(false);
            Destroy(row);
        }

        rows.Clear();
    }
}