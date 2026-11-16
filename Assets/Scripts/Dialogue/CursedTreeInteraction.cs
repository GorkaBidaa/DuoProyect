using System.Collections.Generic;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(-150)]
public class CursedTreeInteraction : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private TreeDialogueController controller;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private RecipeData keyRecipe;
    [Header("Short tutorial conversations")]
    [SerializeField] private TreeDialogueData introduction;
    [SerializeField] private TreeDialogueData gatheringHint;
    [SerializeField] private TreeDialogueData readyToCraft;
    [SerializeField] private TreeDialogueData keyReady;
    [SerializeField] private bool autoPlayIntroduction = true;
    [SerializeField] private bool showMissingMaterials = true;
    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private bool introCompleted, autoAttemptedThisVisit, ownsConversation;
    public bool PromptAvailable => isActiveAndEnabled && occupants.Count > 0 && controller &&
        controller.isActiveAndEnabled && !controller.IsOpen && !controller.ConsumesInteractionThisFrame;
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 150;
    public string PromptAction => "Interact";
    public string PromptLabel => "Talk to Alderworn";

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;
    private void Awake()
    {
        bool valid = controller && inventory && controller.Player &&
            inventory.gameObject == controller.Player.gameObject && keyRecipe && keyRecipe.IsValid() &&
            keyRecipe.outputType == RecipeOutputType.Item && keyRecipe.itemOutput &&
            introduction && introduction.IsPlayable && gatheringHint && gatheringHint.IsPlayable &&
            readyToCraft && readyToCraft.IsPlayable && keyReady && keyReady.IsPlayable &&
            GetComponent<Collider2D>().isTrigger;
        if (!valid)
        {
            Debug.LogError("Alderworn needs the dialogue controller, its Player inventory, the Magic Key recipe, four playable tutorial dialogues and an interaction trigger.", this);
            enabled = false;
        }
    }
    private void OnEnable() => InteractionPromptRegistry.Register(this);
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (controller && other.GetComponentInParent<StaminaSystem>() == controller.Player) occupants.Add(other);
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!occupants.Remove(other)) return;
        if (occupants.Count == 0)
        {
            autoAttemptedThisVisit = false;
            if (ownsConversation && controller) controller.CancelImmediately();
        }
    }
    private void Update()
    {
        occupants.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
        if (occupants.Count == 0)
        {
            autoAttemptedThisVisit = false;
            if (ownsConversation && controller) controller.CancelImmediately();
            return;
        }
        if (!PromptAvailable || !controller.CanStart) return;
        if (!ReferenceEquals(InteractionPromptRegistry.Select(controller.Player.transform.position), this)) return;
        if (autoPlayIntroduction && !introCompleted && !autoAttemptedThisVisit)
        {
            if (Talk()) autoAttemptedThisVisit = true;
        }
        else if (GameInput.Instance && GameInput.Instance.TryInteract()) Talk();
    }

    public bool Talk()
    {
        if (!PromptAvailable || !controller.CanStart) return false;
        bool welcome = !introCompleted;
        TreeDialogueData data = welcome ? introduction :
            inventory.GetItemAmount(keyRecipe.itemOutput) > 0 ? keyReady :
            keyRecipe.CanCraft(inventory) ? readyToCraft : gatheringHint;
        var lines = new List<TreeDialogueLine>(data.lines);
        if (!welcome && data == gatheringHint && showMissingMaterials)
        {
            var missing = new StringBuilder("Still needed for " + keyRecipe.recipeName + ": ");
            bool separator = false;
            foreach (var ingredient in keyRecipe.ingredients)
            {
                int amount = Mathf.Max(0, ingredient.amount - inventory.GetResourceAmount(ingredient.resource));
                if (amount == 0) continue;
                if (separator) missing.Append("; ");
                missing.Append(ingredient.resource.resourceName).Append(" x").Append(amount);
                separator = true;
            }
            if (separator) lines.Add(new TreeDialogueLine("Alderworn", missing.ToString() + "."));
        }
        bool opened = controller.Begin(lines.ToArray(), completed =>
        {
            ownsConversation = false;
            if (welcome && completed) introCompleted = true;
        });
        ownsConversation = opened;
        return opened;
    }

    private void OnDisable()
    {
        InteractionPromptRegistry.Unregister(this);
        occupants.Clear();
        autoAttemptedThisVisit = false;
        if (ownsConversation && controller) controller.CancelImmediately();
    }
}
