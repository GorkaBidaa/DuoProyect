using System.Collections.Generic;
using System.Text;
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

    [Header("Upgrades - reuses the existing list, details and Craft button")]
    [SerializeField] private PlayerUpgrades playerUpgrades;
    [Tooltip("Optional upgrade-only copy of ItemSelectBTN. Uses Recipe Button Prefab when empty.")]
    [SerializeField] private UIRecipeButton upgradeButtonPrefab;
    [SerializeField] private TMP_Text craftButtonLabel;
    [Tooltip("Optional heading above the existing resource requirements list.")]
    [SerializeField] private TMP_Text requirementsTitle;

    [Header("HUD tracker")]
    [SerializeField] private PlayerHUD hud;
    [SerializeField] private GameObject pinnedPanel;

    [Header("Player")]
    public bool IsAtBase => atBase;

    private readonly List<GameObject> recipeRows = new();
    private readonly List<GameObject> requirementRows = new();

    // 0 = items, 1 = resources, 2 = upgrades.
    private int category;
    private bool atBase;
    private bool crafting;
    private RecipeData selectedRecipe;
    private RecipeData pinnedRecipe;
    private PlayerUpgradeData pinnedUpgrade;
    private int pinChangedFrame = -1;
    private PlayerUpgradeData selectedUpgrade;
    private PlayerUpgradeData feedbackUpgrade;
    private string upgradeFeedback;
    private float feedbackUntil;
    private Color warningDefaultColor;

    public PlayerInventory Inventory => inventory;
    public RecipeData SelectedRecipe => selectedRecipe;
    public RecipeData PinnedRecipe => pinnedRecipe;
    public PlayerUpgradeData SelectedUpgrade => selectedUpgrade;
    public PlayerUpgradeData PinnedUpgrade => pinnedUpgrade;
    public bool HasPinnedTarget => pinnedRecipe || pinnedUpgrade;
    public bool IsSelectedPinned => category == 2
        ? selectedUpgrade && selectedUpgrade == pinnedUpgrade
        : selectedRecipe && selectedRecipe == pinnedRecipe;
    public string PinnedTitle => pinnedUpgrade
        ? UpgradeTitle(pinnedUpgrade) + (pinnedUpgrade.IsUnlock ? "" :
            " - Level " + (playerUpgrades.GetLevel(pinnedUpgrade.kind) + 1))
        : pinnedRecipe ? pinnedRecipe.recipeName : "";
    public bool CanPinSelected => isActiveAndEnabled && IsOpen && atBase && !crafting &&
        Time.timeScale > 0f && (!GameInput.Instance || !GameInput.Instance.DialogueBlocked) &&
        (category == 2
            ? playerUpgrades && playerUpgrades.IsReady && selectedUpgrade && selectedUpgrade.IsValid() &&
              playerUpgrades.GetNextLevel(selectedUpgrade.kind) != null
            : selectedRecipe && selectedRecipe.IsValid());
    public event System.Action ViewChanged;
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

        if (!playerUpgrades) playerUpgrades = inventory.GetComponent<PlayerUpgrades>();
        if (!craftButtonLabel) craftButtonLabel = craftButton.GetComponentInChildren<TMP_Text>(true);
        warningDefaultColor = warningText.color;
        menuRoot.SetActive(false);
        pinnedPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryChanged += OnInventoryChanged;
        if (playerUpgrades) playerUpgrades.UpgradesChanged += OnUpgradesChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= OnInventoryChanged;
        if (playerUpgrades) playerUpgrades.UpgradesChanged -= OnUpgradesChanged;

        Close();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
        {
            ClearUpgradeFeedback();
            Refresh();
        }
        GameInput input = GameInput.Instance;
        if (input == null || input.DialogueBlocked) return;
        if (input.Pressed("Back"))
        {
            Close();
            return;
        }

        if (input.Pressed("PinRecipe"))
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

        GameInput.Instance?.BlockThisFrame();
        menuRoot.SetActive(true);
        BuildRecipeList();
    }

    public void Close()
    {
        if (IsOpen) GameInput.Instance?.BlockThisFrame();
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
        selectedUpgrade = null;
        ClearUpgradeFeedback();

        if (IsOpen)
            BuildRecipeList();
    }

    private void BuildRecipeList()
    {
        ClearRows(recipeRows);
        if (category == 2)
        {
            selectedRecipe = null;
            BuildUpgradeList();
            Refresh();
            return;
        }
        selectedUpgrade = null;
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

    private void BuildUpgradeList()
    {
        if (!playerUpgrades || playerUpgrades.Catalog == null)
        {
            selectedUpgrade = null;
            Debug.LogWarning("CraftingMenu Upgrades tab: assign Player Upgrades from the same player as Inventory.", this);
            return;
        }
        var visible = new List<PlayerUpgradeData>();
        foreach (PlayerUpgradeData upgrade in playerUpgrades.Catalog)
        {
            if (!upgrade || visible.Contains(upgrade)) continue;
            visible.Add(upgrade);
            UIRecipeButton prefab = upgradeButtonPrefab ? upgradeButtonPrefab : recipeButtonPrefab;
            UIRecipeButton row = Instantiate(prefab, recipeList);
            row.gameObject.SetActive(true);
            row.BindUpgrade(upgrade, playerUpgrades.GetLevel(upgrade.kind), () => SelectUpgrade(upgrade));
            recipeRows.Add(row.gameObject);
        }
        if (!visible.Contains(selectedUpgrade)) selectedUpgrade = visible.Count > 0 ? visible[0] : null;
    }

    private void SelectUpgrade(PlayerUpgradeData upgrade)
    {
        if (selectedUpgrade != upgrade) ClearUpgradeFeedback();
        selectedUpgrade = upgrade;
        Refresh();
    }

    private void OnUpgradesChanged()
    {
        if (crafting) return;
        // A pinned upgrade must advance/clear even when the crafting menu is closed.
        if (IsOpen && category == 2 && recipeRows.Count == 0) BuildRecipeList();
        else Refresh();
    }

    private void ClearUpgradeFeedback()
    {
        upgradeFeedback = null;
        feedbackUpgrade = null;
        feedbackUntil = 0f;
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
        warningText.color = warningDefaultColor;
        if (category == 2)
        {
            RefreshUpgradeDetails();
            return;
        }
        if (craftButtonLabel) craftButtonLabel.text = "CRAFT";
        if (requirementsTitle) requirementsTitle.text = "Required Resources";
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
        // Existing Craft button wiring stays valid on all three tabs.
        if (category == 2) { PurchaseSelectedUpgrade(); return; }
        RecipeData recipe = selectedRecipe;

        if (crafting || !atBase || !IsOpen || recipe == null)
            return;
        if (!recipe.CanCraft(inventory))
        {
            InteractionAudio.Play(InteractionCue.CraftDenied, inventory.transform.position);
            return;
        }

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
            InteractionAudio.Play(InteractionCue.CraftSuccess, inventory.transform.position);
        }
        finally
        {
            crafting = false;
            Refresh();
        }
    }

    private void RefreshUpgradeDetails()
    {
        if (craftButtonLabel) craftButtonLabel.text = "UPGRADE";
        if (requirementsTitle) requirementsTitle.text = "Upgrade Cost";
        craftButton.interactable = false;
        PlayerUpgradeData upgrade = selectedUpgrade;
        itemImage.sprite = upgrade ? upgrade.icon : null;
        itemImage.enabled = itemImage.sprite != null;
        if (!playerUpgrades || !playerUpgrades.IsReady || !upgrade)
        {
            itemTitle.text = "Upgrades";
            descriptionText.text = "Upgrades are currently unavailable.";
            warningText.text = "No upgrade available.";
            return;
        }
        itemTitle.text = string.IsNullOrEmpty(upgrade.upgradeName) ? upgrade.name : upgrade.upgradeName;
        if (!upgrade.IsValid())
        {
            descriptionText.text = upgrade.description;
            warningText.text = "This upgrade is unavailable.";
            return;
        }
        int level = playerUpgrades.GetLevel(upgrade.kind);
        int maximum = upgrade.levels.Length;
        PlayerUpgradeLevel next = playerUpgrades.GetNextLevel(upgrade.kind);
        bool complete = next == null;
        string progress = upgrade.IsUnlock ? complete ? "Unlocked" : "Locked" :
            "Level " + level + " / " + maximum;
        string effect = UpgradeEffect(upgrade, next);
        descriptionText.text = upgrade.description + "\n\n" + progress + "\n" + effect;

        foreach (GameObject row in recipeRows)
            if (row && row.TryGetComponent(out UIRecipeButton entry) && entry.BoundUpgrade)
                entry.RefreshUpgradeState(playerUpgrades.GetLevel(entry.BoundUpgrade.kind));

        if (complete)
        {
            if (craftButtonLabel) craftButtonLabel.text = upgrade.IsUnlock ? "UNLOCKED" : "MAX LEVEL";
            warningText.text = upgrade.IsUnlock ? "Ability already unlocked." : "Maximum level reached.";
        }
        else
        {
            foreach (RecipeIngredient cost in next.costs)
            {
                UIRequiredResource row = Instantiate(requirementPrefab, requirementsList);
                row.gameObject.SetActive(true);
                row.Bind(cost.resource, inventory.GetResourceAmount(cost.resource), cost.amount);
                requirementRows.Add(row.gameObject);
            }
            bool canPurchase = playerUpgrades.CanPurchase(upgrade.kind, out string reason);
            craftButton.interactable = atBase && canPurchase;
            warningText.text = !atBase ? "Return to a crafting station." : reason;
            if (craftButtonLabel && upgrade.IsUnlock) craftButtonLabel.text = "UNLOCK";
        }
        if (feedbackUpgrade == upgrade && !string.IsNullOrEmpty(upgradeFeedback) && Time.unscaledTime < feedbackUntil)
        {
            warningText.text = upgradeFeedback;
            warningText.color = new Color32(120, 210, 120, 255);
        }
    }

    private string UpgradeEffect(PlayerUpgradeData upgrade, PlayerUpgradeLevel next)
    {
        if (upgrade.IsUnlock)
            return next == null ? "This ability is available." :
                upgrade.kind == PlayerUpgradeKind.ShadowDash ? "Unlock Shadow Dash." : "Unlock Wall Jump.";
        float current = playerUpgrades.GetCurrentValue(upgrade.kind);
        string title = upgrade.kind == PlayerUpgradeKind.MaxHealth ? "Max health" :
            upgrade.kind == PlayerUpgradeKind.MaxStamina ? "Max stamina" : "Anchor range";
        return next == null ? title + ": " + current.ToString("0.##") :
            title + ": " + current.ToString("0.##") + " -> " + (current + next.amount).ToString("0.##");
    }

    public void PurchaseSelectedUpgrade()
    {
        if (crafting || !IsOpen || !atBase || category != 2 || !selectedUpgrade || !playerUpgrades) return;
        bool success = false;
        crafting = true;
        try
        {
            success = playerUpgrades.Purchase(selectedUpgrade.kind);
            if (success)
            {
                feedbackUpgrade = selectedUpgrade;
                upgradeFeedback = selectedUpgrade.IsUnlock ? selectedUpgrade.upgradeName + " unlocked!" :
                    selectedUpgrade.upgradeName + " upgraded to level " + playerUpgrades.GetLevel(selectedUpgrade.kind) + ".";
                feedbackUntil = Time.unscaledTime + 1.5f;
                MenuUISounds.PlayClick();
            }
            InteractionAudio.Play(success ? InteractionCue.CraftSuccess : InteractionCue.CraftDenied, inventory.transform.position);
        }
        finally
        {
            crafting = false;
            Refresh();
        }
    }

    public void PinSelected()
    {
        if (!CanPinSelected || pinChangedFrame == Time.frameCount)
            return;
        // A UI Submit and the Pin action may share a key. Toggle only once that frame.
        pinChangedFrame = Time.frameCount;
        GameInput.Instance?.BlockThisFrame();
        MenuUISounds.PlayClick();

        // Recipes and upgrades share one tracker. Pinning the same selection unpins it.
        if (category == 2)
        {
            pinnedUpgrade = IsSelectedPinned ? null : selectedUpgrade;
            pinnedRecipe = null;
        }
        else
        {
            pinnedRecipe = IsSelectedPinned ? null : selectedRecipe;
            pinnedUpgrade = null;
        }

        RefreshPinned();
    }

    public void Unpin()
    {
        pinnedRecipe = null;
        pinnedUpgrade = null;
        RefreshPinned();
    }

    private static string UpgradeTitle(PlayerUpgradeData upgrade) =>
        string.IsNullOrEmpty(upgrade.upgradeName) ? upgrade.name : upgrade.upgradeName;

    private string GetPinnedUpgradeRequirements()
    {
        PlayerUpgradeLevel next = playerUpgrades.GetNextLevel(pinnedUpgrade.kind);
        if (next == null) return "";
        var text = new StringBuilder();
        foreach (RecipeIngredient cost in next.costs)
        {
            int owned = inventory.GetResourceAmount(cost.resource);
            int missing = Mathf.Max(0, cost.amount - owned);
            string status = missing == 0 ? "Ready" : "Missing " + missing;
            text.AppendLine(cost.resource.resourceName + ": " + owned + " / " + cost.amount + " - " + status);
        }
        return text.ToString();
    }

    private void RefreshPinned()
    {
        // One-time unlocks and maxed upgrades no longer have a next-level cost to track.
        if (pinnedUpgrade && (!playerUpgrades || !pinnedUpgrade.IsValid() ||
            (playerUpgrades.IsReady && playerUpgrades.GetNextLevel(pinnedUpgrade.kind) == null)))
            pinnedUpgrade = null;

        if (pinnedPanel != null && hud != null)
        {
            bool show = pinnedRecipe || (pinnedUpgrade && playerUpgrades.IsReady);
            pinnedPanel.SetActive(show);
            if (show)
                hud.SetPinnedRecipe(PinnedTitle, pinnedUpgrade
                    ? GetPinnedUpgradeRequirements() : pinnedRecipe.GetRequirements(inventory));
        }
        foreach (GameObject row in recipeRows)
            if (row && row.TryGetComponent(out UIRecipeButton recipeButton))
                recipeButton.SetPinned((pinnedRecipe && recipeButton.BoundRecipe == pinnedRecipe) ||
                    (pinnedUpgrade && recipeButton.BoundUpgrade == pinnedUpgrade));
        ViewChanged?.Invoke();
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