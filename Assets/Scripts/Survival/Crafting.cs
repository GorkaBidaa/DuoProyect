using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Crafting : MonoBehaviour
{
    //UI
    public GameObject craftingUI;
    public TextMeshProUGUI resourceRequirementText;

    //Crafting req
    public ResourceData requiredResource;
    public int requiredAmount;

    //Crafted result
    public ResourceData craftedItem;
    public int craftedAmount = 1;

    //player inventory reference
    private PlayerInventory playerInventory;

    //Interaction
    private Collider2D craftingCollider;

    private bool isCrafted = false;
    private bool isInCraftingArea = false;

    private void Awake()
    {
        if (craftingCollider == null)
        {
            craftingCollider = GetComponent<Collider2D>();
        }
    }

    void Start()
    {

    }


    void Update()
    {
        if (!isInCraftingArea || isCrafted)
            return;

        UpdateRequirementText();

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        bool interactPressed =
            (keyboard != null && keyboard.fKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);

        if (interactPressed)
            Craft();
    }

    private void UpdateRequirementText()
    {
        if (resourceRequirementText == null)
            return;

        if (requiredResource == null)
        {
            resourceRequirementText.text = "No required resource assigned.";
            return;
        }

        int ownedAmount = playerInventory != null
            ? playerInventory.GetResourceAmount(requiredResource)
            : 0;

        resourceRequirementText.text = $"{ownedAmount} / {requiredAmount} {requiredResource.resourceName}";
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && (!isCrafted))
        {

            playerInventory = collision.GetComponentInParent<PlayerInventory>();
            Debug.Log("Player entered crafting area.");
            UpdateRequirementText();
            craftingUI.SetActive(true);
            isInCraftingArea = true;

        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Debug.Log("Player exited crafting area.");
            isInCraftingArea = false;
            craftingUI.SetActive(false);
        }
    }

    public void Interact(InputAction.CallbackContext context)
    {

        if (!context.performed)
            return;


        if (isInCraftingArea && !isCrafted)
        {
            Craft();
        }
    }

    private void Craft()
    {

        if (playerInventory == null)
            return;



        if (!playerInventory.HasResource(requiredResource, requiredAmount))
        {
            Debug.Log("Not Enough resources to craft the item.");
            return;
        }
        playerInventory.RemoveResource(requiredResource, requiredAmount);

        playerInventory.AddResource(craftedItem, craftedAmount);

        isCrafted = true;

        craftingUI.SetActive(false);

        Debug.Log("Crafting successful! Crafted " + craftedAmount + " " + craftedItem.name);
    }


}


