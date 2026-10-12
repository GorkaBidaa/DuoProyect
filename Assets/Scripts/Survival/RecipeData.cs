using System;
using System.Text;
using UnityEngine;

public enum RecipeOutputType { Resource, Item }

[Serializable]
public class RecipeIngredient
{
    public ResourceData resource;
    [Min(1)] public int amount = 1;
}

[CreateAssetMenu(fileName = "New Recipe", menuName = "Crafting/Recipe")]
public class RecipeData : ScriptableObject
{
    public string recipeName;
    [TextArea] public string description;
    public RecipeIngredient[] ingredients;
    public RecipeOutputType outputType;
    public ResourceData resourceOutput;
    public ItemData itemOutput;
    [Min(1)] public int outputAmount = 1;

    public bool IsValid()
    {
        if (outputAmount <= 0 || ingredients == null || ingredients.Length == 0)
            return false;
        if (outputType == RecipeOutputType.Resource ? resourceOutput == null : itemOutput == null)
            return false;
        for (int i = 0; i < ingredients.Length; i++)
        {
            RecipeIngredient entry = ingredients[i];
            if (entry == null || entry.resource == null || entry.amount <= 0) return false;
            // One row per resource prevents accidentally undercounting duplicate ingredients.
            for (int j = 0; j < i; j++)
                if (ingredients[j].resource == entry.resource) return false;
        }
        return true;
    }

    public bool CanCraft(PlayerInventory inventory)
    {
        if (inventory == null || !IsValid()) return false;
        foreach (RecipeIngredient entry in ingredients)
            if (!inventory.HasResource(entry.resource, entry.amount)) return false;
        return true;
    }

    public string GetRequirements(PlayerInventory inventory)
    {
        if (!IsValid()) return "Recipe is not configured correctly.";
        StringBuilder text = new StringBuilder();
        foreach (RecipeIngredient entry in ingredients)
        {
            int owned = inventory.GetResourceAmount(entry.resource);
            int missing = Mathf.Max(0, entry.amount - owned);
            string status = missing == 0 ? "Ready" : $"Missing {missing}";
            text.AppendLine($"{entry.resource.resourceName}: {owned} / {entry.amount} — {status}");
        }
        return text.ToString();
    }
}
