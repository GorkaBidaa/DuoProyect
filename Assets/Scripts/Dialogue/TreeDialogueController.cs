using System;
using TMPro;
using UnityEngine;

public class TreeDialogueController : MonoBehaviour
{
    [SerializeField] private StaminaSystem player;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text controlsText;
    private PlayerMovement movement;
    private TreeDialogueData dialogue;
    private Action<bool> finished;
    private GameInput inputOwner;
    private bool movementWasEnabled;
    private int lineIndex, openedFrame;
    public bool IsOpen => dialogue != null;
    public StaminaSystem Player => player;
    public bool CanStart => isActiveAndEnabled && !IsOpen && player && !player.IsDead &&
        movement && movement.enabled && movement.isGrounded && !movement.IsTeleporting &&
        GameInput.Instance && GameInput.Instance.PromptsAllowed;

    private void Awake()
    {
        if (!player || !panel || !speakerText || !bodyText || !controlsText ||
            transform.IsChildOf(panel.transform))
        {
            Debug.LogError("Assign dialogue player/panel/text fields. Keep the controller outside the panel it hides.", this);
            enabled = false;
            return;
        }
        movement = player.GetComponent<PlayerMovement>();
        if (!movement) { Debug.LogError("Dialogue player needs PlayerMovement.", this); enabled = false; return; }
        panel.SetActive(false);
    }

    public bool Begin(TreeDialogueData data, Action<bool> onFinished)
    {
        if (!CanStart || !data || !data.IsPlayable) return false;
        dialogue = data;
        finished = onFinished;
        lineIndex = 0;
        openedFrame = Time.frameCount;
        inputOwner = GameInput.Instance;
        inputOwner.SetDialogueBlocked(true);
        movementWasEnabled = movement.enabled;
        movement.StopForDialogue();
        movement.enabled = false;
        panel.SetActive(true);
        ShowLine();
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!player || !player.gameObject.activeInHierarchy || player.IsDead || !inputOwner || !inputOwner.isActiveAndEnabled)
        { Close(false); return; }
        controlsText.text = inputOwner.GetBindingLabel("Interact") + " - Continue    " +
            inputOwner.GetBindingLabel("Back") + " - Close";
        if (Time.frameCount == openedFrame || Time.timeScale <= 0f) return;
        if (inputOwner.Pressed("Back")) { Close(false); return; }
        if (inputOwner.Pressed("Interact")) Advance();
    }

    public void Advance()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        lineIndex++;
        if (lineIndex >= dialogue.lines.Length) Close(true);
        else ShowLine();
    }

    private void ShowLine()
    {
        speakerText.text = dialogue.lines[lineIndex].speaker;
        bodyText.text = dialogue.lines[lineIndex].text;
    }

    public void Cancel() => Close(false);

    private void Close(bool completed)
    {
        if (!IsOpen) return;
        dialogue = null;
        if (panel) panel.SetActive(false);
        if (inputOwner) inputOwner.SetDialogueBlocked(false);
        inputOwner = null;
        if (movement && player && !player.IsDead && movementWasEnabled)
        {
            movement.ResetMovementState();
            movement.enabled = true;
        }
        Action<bool> callback = finished;
        finished = null;
        callback?.Invoke(completed);
    }

    private void OnDisable() => Close(false);
}
