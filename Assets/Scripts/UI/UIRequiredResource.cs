using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRequiredResource : MonoBehaviour
{
    [SerializeField] private Image resourceIcon;
    [SerializeField] private TMP_Text resourceName;
    [SerializeField] private TMP_Text requirementsText;
    [SerializeField] private GameObject warningImage;

    public void Bind(ResourceData resource, int owned, int required)
    {
        resourceIcon.sprite = resource.icon;
        resourceIcon.enabled = resource.icon != null;

        resourceName.text = resource.resourceName;
        requirementsText.text = $"{owned} / {required}";

        bool missing = owned < required;
        warningImage.SetActive(missing);

        requirementsText.color = missing
            ? new Color32(230, 100, 100, 255)
            : new Color32(120, 210, 120, 255);
    }
}