using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIRecipeButton : MonoBehaviour
{
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private Image itemIcon;
    [Tooltip("Optional separate child badge displaying PIN/PINNED.")]
    [SerializeField] private GameObject pinnedMarker;
    public RecipeData BoundRecipe { get; private set; }
    [Tooltip("Optional second line used for upgrade level / unlock state.")]
    [SerializeField] private TMP_Text upgradeStateText;
    public PlayerUpgradeData BoundUpgrade { get; private set; }

    private Action onSelected;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Select);
    }

    public void Bind(RecipeData recipe, Action selected)
    {
        BoundRecipe = recipe;
        BoundUpgrade = null;
        if (upgradeStateText && upgradeStateText != itemName) upgradeStateText.gameObject.SetActive(false);
        SetPinned(false);
        itemName.text = recipe.recipeName;

        Sprite icon = null;

        if (recipe.outputType == RecipeOutputType.Item &&
            recipe.itemOutput != null)
        {
            icon = recipe.itemOutput.icon;
        }
        else if (recipe.outputType == RecipeOutputType.Resource &&
                 recipe.resourceOutput != null)
        {
            icon = recipe.resourceOutput.icon;
        }

        itemIcon.sprite = icon;
        itemIcon.enabled = icon != null;
        onSelected = selected;
    }

    public void BindUpgrade(PlayerUpgradeData upgrade, int level, Action selected)
    {
        BoundRecipe = null;
        BoundUpgrade = upgrade;
        SetPinned(false);
        itemIcon.sprite = upgrade.icon;
        itemIcon.enabled = upgrade.icon != null;
        onSelected = selected;
        RefreshUpgradeState(level);
    }

    public void RefreshUpgradeState(int level)
    {
        if (!BoundUpgrade) return;
        string title = string.IsNullOrEmpty(BoundUpgrade.upgradeName) ? BoundUpgrade.name : BoundUpgrade.upgradeName;
        int maximum = BoundUpgrade.levels != null ? BoundUpgrade.levels.Length : 0;
        string state = BoundUpgrade.IsUnlock ? level > 0 ? "Unlocked" : "Locked" :
            level >= maximum ? "Max Level" : "Level " + level + "/" + maximum;
        if (upgradeStateText && upgradeStateText != itemName)
        {
            itemName.text = title;
            upgradeStateText.text = state;
            upgradeStateText.gameObject.SetActive(true);
        }
        else itemName.text = title + " (" + state + ")";
    }

    public void SetPinned(bool value)
    {
        // Never hide the entire row if its root/ancestor was accidentally assigned as the badge.
        if (pinnedMarker && !transform.IsChildOf(pinnedMarker.transform))
            pinnedMarker.SetActive(value);
    }

    private void Select()
    {
        onSelected?.Invoke();
    }
}