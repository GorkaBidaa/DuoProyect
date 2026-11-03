using UnityEngine;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Status HUD")]
    [SerializeField] private CursedForestHud statusHud;

    // [Header("Quest")]
    // [SerializeField] private TextMeshProUGUI questTitle;
    // [SerializeField] private TextMeshProUGUI questObjective;

    [Header("Crafting")]
    [SerializeField] private TextMeshProUGUI recipeTitle;
    [SerializeField] private TextMeshProUGUI requirementsText;

    public void SetHealth(int current, int maximum)
    {
        if (statusHud == null) return;

        float normalized = maximum > 0
            ? (float)current / maximum
            : 0f;

        statusHud.SetHealth(normalized);
    }

    public void SetStamina(float current, float maximum)
    {
        if (statusHud == null) return;

        float normalized = maximum > 0f
            ? current / maximum
            : 0f;

        statusHud.SetStamina(normalized);
    }

    public void SetActiveItem(Sprite icon, int quantity)
    {
        if (statusHud != null)
            statusHud.SetItem(icon, quantity);
    }

    //public void SetQuest(string title, string objective)
    //{
    //    if (questTitle != null)
    //        questTitle.text = title;
//
  //      if (questObjective != null)
   //         questObjective.text = objective;
   // }

    public void SetPinnedRecipe(string title, string requirements)
    {
        if (recipeTitle != null)
            recipeTitle.text = title;

        if (requirementsText != null)
            requirementsText.text = requirements;
    }
}