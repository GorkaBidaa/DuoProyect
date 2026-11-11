using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(-150)]
public class CursedTreeInteraction : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private TreeDialogueController controller;
    [SerializeField] private TreeDialogueData introduction;
    [SerializeField] private TreeDialogueData aboutTheForest;
    [SerializeField] private TreeDialogueData reservedThird;
    [SerializeField] private bool autoPlayIntroduction = true;
    [SerializeField] private bool thirdUnlocked;
    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private bool introCompleted, forestCompleted, autoAttemptedThisVisit;
    private bool ownsConversation;
    public bool PromptAvailable => isActiveAndEnabled && occupants.Count > 0 && controller &&
        !controller.IsOpen && NextDialogue != null;
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 150;
    public string PromptAction => "Interact";
    public string PromptLabel => "Talk";
    private TreeDialogueData NextDialogue => !introCompleted && introduction && introduction.IsPlayable
        ? introduction : thirdUnlocked && forestCompleted && reservedThird && reservedThird.IsPlayable
        ? reservedThird : aboutTheForest && aboutTheForest.IsPlayable ? aboutTheForest : null;

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;
    private void Awake()
    {
        if (!controller || !introduction || !introduction.IsPlayable || !aboutTheForest || !aboutTheForest.IsPlayable ||
            !GetComponent<Collider2D>().isTrigger)
        {
            Debug.LogError("Assign the Tree controller and two playable dialogues, and enable Is Trigger on its interaction collider.", this);
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
            if (ownsConversation) controller.Cancel();
        }
    }
    private void Update()
    {
        occupants.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
        if (occupants.Count == 0)
        {
            autoAttemptedThisVisit = false;
            if (ownsConversation && controller) controller.Cancel();
            return;
        }
        if (!PromptAvailable || !controller.CanStart) return;
        if (!ReferenceEquals(InteractionPromptRegistry.Select(controller.Player.transform.position), this)) return;
        if (autoPlayIntroduction && !introCompleted && !autoAttemptedThisVisit)
        {
            if (Play(introduction)) autoAttemptedThisVisit = true;
        }
        else if (GameInput.Instance && GameInput.Instance.TryInteract()) Play(NextDialogue);
    }
    private bool Play(TreeDialogueData data)
    {
        bool opened = controller.Begin(data, completed =>
        {
            ownsConversation = false;
            if (!completed) return;
            if (data == introduction) introCompleted = true;
            if (data == aboutTheForest) forestCompleted = true;
        });
        ownsConversation = opened;
        return opened;
    }
    public void SetThirdUnlocked(bool value) => thirdUnlocked = value;
    private void OnDisable()
    {
        InteractionPromptRegistry.Unregister(this);
        occupants.Clear();
        autoAttemptedThisVisit = false;
        if (ownsConversation && controller) controller.Cancel();
    }
}
