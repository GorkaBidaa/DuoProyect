using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(-140)]
public class SpecialKeyGate : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ItemData specialKey;
    [SerializeField] private TutorialCompletion tutorialCompletion;
    [Tooltip("Solid colliders blocking passage. Keep the separate interaction trigger out of this array.")]
    [SerializeField] private Collider2D[] blockingColliders;
    [Tooltip("Optional closed-gate art to hide. Keep the gate root and interaction trigger active.")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private UnityEvent onUnlocked = new UnityEvent();
    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private StaminaSystem stamina;
    private bool unlocked, unlocking;
    public bool IsUnlocked => unlocked;
    public bool PromptAvailable => isActiveAndEnabled && !unlocked && !unlocking && occupants.Count > 0 &&
        playerInventory && playerInventory.isActiveAndEnabled && stamina && !stamina.IsDead;
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 200;
    public string PromptAction => "Interact";
    public string PromptLabel => playerInventory && specialKey && playerInventory.GetItemAmount(specialKey) > 0
        ? "Unlock Gate" : "Special Key required";

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;
    private void Awake()
    {
        Collider2D interactionTrigger = GetComponent<Collider2D>();
        stamina = playerInventory ? playerInventory.GetComponent<StaminaSystem>() : null;
        bool valid = playerInventory && stamina && specialKey && tutorialCompletion &&
            interactionTrigger.isTrigger && blockingColliders != null && blockingColliders.Length > 0;
        if (valid)
            foreach (Collider2D collider in blockingColliders)
                if (!collider || collider == interactionTrigger || collider.isTrigger) { valid = false; break; }
        if ((closedVisual && transform.IsChildOf(closedVisual.transform)) ||
            (openVisual && transform.IsChildOf(openVisual.transform)) ||
            (closedVisual && closedVisual == openVisual)) valid = false;
        if (!valid)
        {
            Debug.LogError("Gate needs player inventory, key asset, TutorialCompletion, a trigger, and separate solid blocking colliders. Visuals must not be the gate root or its parents.", this);
            enabled = false;
            return;
        }
        if (openVisual) openVisual.SetActive(false);
    }
    private void OnEnable() => InteractionPromptRegistry.Register(this);
    private void OnDisable()
    {
        InteractionPromptRegistry.Unregister(this);
        occupants.Clear();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == playerInventory) occupants.Add(other);
    }
    private void OnTriggerExit2D(Collider2D other) => occupants.Remove(other);
    private void Update()
    {
        occupants.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!PromptAvailable) return;
        GameInput input = GameInput.Instance;
        if (!input || !input.PromptsAllowed ||
            !ReferenceEquals(InteractionPromptRegistry.Select(playerInventory.transform.position), this)) return;
        if (input.TryInteract()) TryUnlock();
    }
    public bool TryUnlock()
    {
        GameInput input = GameInput.Instance;
        if (!PromptAvailable || !input || !input.PromptsAllowed) return false;
        if (playerInventory.GetItemAmount(specialKey) < 1)
        {
            Debug.Log("Craft the Special Key at a crafting station to open this gate.", this);
            return false;
        }
        // Lock before inventory events fire, preventing duplicate key consumption/re-entry.
        unlocking = true;
        if (!playerInventory.RemoveItem(specialKey, 1)) { unlocking = false; return false; }
        unlocked = true;
        unlocking = false;
        foreach (Collider2D collider in blockingColliders) if (collider) collider.enabled = false;
        if (closedVisual) closedVisual.SetActive(false);
        if (openVisual) openVisual.SetActive(true);
        input.BlockThisFrame();
        tutorialCompletion.Complete();
        onUnlocked.Invoke();
        return true;
    }
}
