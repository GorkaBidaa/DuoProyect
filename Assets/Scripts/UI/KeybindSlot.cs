using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One independently rebindable control inside an action row.
public sealed class KeybindSlot : MonoBehaviour
{
    [SerializeField] private Button changeButton;
    [SerializeField] private TMP_Text bindingLabel;
    [SerializeField] private Image bindingIcon;
    [SerializeField] private bool showTextWithIcon;
    private UnityEngine.Events.UnityAction bindingCallback;
    public bool IsConfigured => changeButton && bindingLabel;

    public void Setup(string binding, Sprite icon, UnityEngine.Events.UnityAction onChange)
    {
        SetBinding(binding, icon);
        // Preserve feedback/audio callbacks; only replace this slot's binding callback.
        if (bindingCallback != null) changeButton.onClick.RemoveListener(bindingCallback);
        bindingCallback = onChange;
        changeButton.onClick.AddListener(bindingCallback);
    }

    public void SetBinding(string binding) => SetBinding(binding, null);

    public void SetBinding(string binding, Sprite icon)
    {
        bool hasIcon = bindingIcon && icon;
        if (bindingIcon)
        {
            bindingIcon.sprite = icon;
            bindingIcon.enabled = hasIcon;
            bindingIcon.preserveAspect = true;
            bindingIcon.raycastTarget = false;
        }
        bindingLabel.text = binding;
        bindingLabel.enabled = !hasIcon || showTextWithIcon;
    }

    public void SetInteractable(bool value)
    {
        if (changeButton) changeButton.interactable = value;
    }

    public void Select()
    {
        if (changeButton && UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(changeButton.gameObject);
    }
}
