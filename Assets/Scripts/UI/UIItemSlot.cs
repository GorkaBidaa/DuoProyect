using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIItemSlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text amountText;

    private Action onSelected;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Select);
    }

    public void Bind(Sprite icon, int amount, Action selected)
    {
        itemIcon.sprite = icon;
        itemIcon.enabled = icon != null;
        amountText.text = $"x{amount}";
        onSelected = selected;
    }

    private void Select()
    {
        onSelected?.Invoke();
    }
}