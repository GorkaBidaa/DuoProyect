using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// attach to manager
public class CraftingMenu : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private RecipeData[] recipes;
    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private TMP_Dropdown recipeDropdown;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private Button craftButton;
    [SerializeField] private Button pinButton;
    [Header("HUD tracker: outside menuRoot")]
    [SerializeField] private GameObject pinnedPanel;
    [SerializeField] private PlayerHUD hud;

    public PlayerInventory Inventory => inventory;
    public bool IsOpen => menuRoot.activeSelf;
    private bool atBase;
    private RecipeData pinnedRecipe;
    private RecipeData SelectedRecipe => recipes != null && recipeDropdown.value < recipes.Length
        ? recipes[recipeDropdown.value] : null;

    [SerializeField] private GameObject hudRoot;

    private void Awake()
    {
        menuRoot.SetActive(false);
        pinnedPanel.SetActive(false);
        recipeDropdown.ClearOptions();
        List<string> names = new List<string>();
        if (recipes != null)
            foreach (RecipeData recipe in recipes)
                names.Add(recipe != null ? recipe.recipeName : "Unassigned recipe");
        recipeDropdown.AddOptions(names);
    }

    private void OnEnable()
    {
        inventory.InventoryChanged += Refresh;
        recipeDropdown.onValueChanged.AddListener(OnSelectionChanged);
        Refresh();
    }

    private void OnDisable()
    {
        inventory.InventoryChanged -= Refresh;
        recipeDropdown.onValueChanged.RemoveListener(OnSelectionChanged);
    }

    private void OnSelectionChanged(int index) => Refresh();

    public void SetAtBase(bool value)
    {
        atBase = value;
        if (!atBase) Close();
        Refresh();
    }

    public void Open()
    {
        if (!atBase)
            return;

        menuRoot.SetActive(true);
        hudRoot.SetActive(false);

        Refresh();
        recipeDropdown.Select();
    }

    public void Close()
    {
        menuRoot.SetActive(false);
        hudRoot.SetActive(true);
    }


    public void CraftSelected()
    {
        RecipeData recipe = SelectedRecipe;
        if (!atBase || !IsOpen || recipe == null || !recipe.CanCraft(inventory)) return;
        foreach (RecipeIngredient entry in recipe.ingredients)
            inventory.RemoveResource(entry.resource, entry.amount);
        if (recipe.outputType == RecipeOutputType.Resource)
            inventory.AddResource(recipe.resourceOutput, recipe.outputAmount);
        else
            inventory.AddItem(recipe.itemOutput, recipe.outputAmount);
        Refresh();
    }

    public void PinSelected()
    {
        if (!IsOpen || SelectedRecipe == null || !SelectedRecipe.IsValid()) return;
        pinnedRecipe = SelectedRecipe;
        Refresh();
    }

    public void Unpin()
    {
        pinnedRecipe = null;
        Refresh();
    }

    private void Refresh()
    {
        RecipeData selected = SelectedRecipe;
        detailsText.text = selected == null ? "Select a recipe." :
            $"{selected.recipeName}\n{selected.description}\n\n" +
            selected.GetRequirements(inventory) + $"\nProduces: {selected.outputAmount}";
        craftButton.interactable = atBase && selected != null && selected.CanCraft(inventory);
        pinButton.interactable = selected != null && selected.IsValid();
        pinnedPanel.SetActive(pinnedRecipe != null);
        if (pinnedRecipe != null)
            hud.SetPinnedRecipe(pinnedRecipe.recipeName, pinnedRecipe.GetRequirements(inventory));
    }
}
