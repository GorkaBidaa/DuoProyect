using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIRecipeButton : MonoBehaviour
{
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private Image itemIcon;

    private Action onSelected;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Select);
    }

    public void Bind(RecipeData recipe, Action selected)
    {
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

    private void Select()
    {
        onSelected?.Invoke();
    }
}