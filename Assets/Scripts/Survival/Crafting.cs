using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Replaces the old single-recipe Crafting component on one base trigger.
[RequireComponent(typeof(Collider2D))]
public class Crafting : MonoBehaviour
{
    [SerializeField] private CraftingMenu menu;
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() != menu.Inventory) return;
        playerColliders.Add(other);
        menu.SetAtBase(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other)) return;
        if (playerColliders.Count == 0) menu.SetAtBase(false);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (menu != null) menu.SetAtBase(false);
    }

    private void Update()
    {
        if (playerColliders.Count == 0) return;
        bool interact = (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
        if (interact)
        {
            if (menu.IsOpen) menu.Close();
            else menu.Open();
        }
        if (menu.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            menu.Close();
    }
}
