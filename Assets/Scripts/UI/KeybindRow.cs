using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeybindRow : MonoBehaviour
{
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text bindingLabel;
    [SerializeField] private Button changeButton;
    public bool IsConfigured => actionLabel && bindingLabel && changeButton;

    public void Setup(string title, string binding, UnityEngine.Events.UnityAction onChange)
    {
        actionLabel.text = title;
        bindingLabel.text = binding;
        changeButton.onClick.RemoveAllListeners();
        changeButton.onClick.AddListener(onChange);
    }

    public void SetBinding(string binding) => bindingLabel.text = binding;
    public void SetInteractable(bool value) => changeButton.interactable = value;
    public void Select()
    {
        if (UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(changeButton.gameObject);
    }
}
