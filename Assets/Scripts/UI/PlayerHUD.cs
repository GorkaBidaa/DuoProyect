using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    //[SerializeField] private Slider secondLifeSlider;
    //[SerializeField] private TextMeshProUGUI secondLifeText;
    [SerializeField] private TextMeshProUGUI questTitle;
    [SerializeField] private TextMeshProUGUI questObjective;
    [SerializeField] private TextMeshProUGUI recipeTitle;
    [SerializeField] private TextMeshProUGUI requirementsText;

    public void SetHealth(int current, int maximum)
    {
        maximum = Mathf.Max(1, maximum); 
        current = Mathf.Clamp(current, 0, maximum);

        healthSlider.minValue = 0;
        healthSlider.maxValue = maximum;
        healthSlider.value = current;

        healthText.text = $"{current} / {maximum}";
    }

   // public void SetSecondLife(int current, int maximum)
    //{
     //   secondLifeText.text = $"Second life: {current}/{maximum}";
    //}

    public void SetQuest(string title, string objective)
    {
        questTitle.text = title;
        questObjective.text = objective;
    }

    public void SetPinnedRecipe(string title, string requirements)
    {
        recipeTitle.text = title;
        requirementsText.text = requirements;
    }
}
