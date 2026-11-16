using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-175)]
public class TreeDialogueController : MonoBehaviour
{
    [SerializeField] private StaminaSystem player;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private IntroTextAnimator textAnimator;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    private PlayerMovement movement;
    private Rigidbody2D playerBody;
    private Canvas panelCanvas;
    private TreeDialogueLine[] lines;
    private Action<bool> finished;
    private GameInput inputOwner;
    private GameObject previousSelection;
    private Coroutine revealRoutine;
    private int lineIndex, openedFrame, actionFrame = -1, closedFrame = -1;
    private float previousTimeScale;
    private bool previousAudioPause, ownsFreeze, inputArmed;
    public bool IsOpen => lines != null;
    public bool IsTyping => IsOpen && textAnimator && textAnimator.IsRevealing;
    public bool HasTypewriter => textAnimator && textAnimator.isActiveAndEnabled;
    public string CurrentSpeaker => IsOpen ?
        (string.IsNullOrWhiteSpace(lines[lineIndex].speaker) ? "Alderworn" : lines[lineIndex].speaker) : "";
    public StaminaSystem Player => player;
    public bool ConsumesInteractionThisFrame => closedFrame == Time.frameCount;
    public bool CanStart => isActiveAndEnabled && !IsOpen && !ConsumesInteractionThisFrame &&
        panel && panelCanvas && panelCanvas.enabled && panelCanvas.rootCanvas.enabled &&
        (!panel.transform.parent || panel.transform.parent.gameObject.activeInHierarchy) &&
        player && !player.IsDead && movement && movement.enabled && movement.isGrounded &&
        !movement.IsTeleporting && GameInput.Instance && GameInput.Instance.PromptsAllowed;

    private void Awake()
    {
        panelCanvas = panel ? panel.GetComponentInParent<Canvas>(true) : null;
        if (!player || !panel || !speakerText || !bodyText || !controlsText ||
            !panelCanvas || player.gameObject.scene != gameObject.scene || panel.scene != gameObject.scene ||
            transform.IsChildOf(panel.transform) || !bodyText.transform.IsChildOf(panel.transform) ||
            !speakerText.transform.IsChildOf(panel.transform) || !controlsText.transform.IsChildOf(panel.transform) ||
            (nextButton && !nextButton.transform.IsChildOf(panel.transform)) ||
            (closeButton && !closeButton.transform.IsChildOf(panel.transform)))
        {
            Debug.LogError("Tree dialogue needs scene Player, panel and its Speaker/Body/Controls TMP texts. Keep the controller active outside the panel.", this);
            enabled = false;
            return;
        }
        movement = player.GetComponent<PlayerMovement>();
        playerBody = player.GetComponent<Rigidbody2D>();
        if (!movement) { Debug.LogError("Dialogue Player needs PlayerMovement.", this); enabled = false; return; }
        if (!textAnimator) textAnimator = bodyText.GetComponent<IntroTextAnimator>();
        if (textAnimator && textAnimator.GetComponent<TMP_Text>() != bodyText)
        { Debug.LogError("Assign the IntroTextAnimator on the dialogue BodyText, not the Main Menu intro text.", this); enabled = false; return; }
        panel.SetActive(false);
    }

    public bool Begin(TreeDialogueData data, Action<bool> onFinished) =>
        data && data.IsPlayable && Begin(data.lines, onFinished);

    public bool Begin(TreeDialogueLine[] conversation, Action<bool> onFinished)
    {
        if (!CanStart || conversation == null || conversation.Length == 0) return false;
        foreach (var line in conversation) if (line == null || string.IsNullOrWhiteSpace(line.text)) return false;
        lines = conversation;
        finished = onFinished;
        lineIndex = 0;
        openedFrame = Time.frameCount;
        inputArmed = false;
        inputOwner = GameInput.Instance;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        inputOwner.SetDialogueBlocked(true);
        movement.StopForDialogue();
        if (playerBody) playerBody.linearVelocity = Vector2.zero;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ownsFreeze = true;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas) canvas.rootCanvas.sortingOrder = Mathf.Max(canvas.rootCanvas.sortingOrder, 1800);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (nextButton) nextButton.interactable = false;
        if (closeButton) closeButton.interactable = false;
        ShowLine();
        return true;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!player || !player.gameObject.activeInHierarchy || player.IsDead || !inputOwner || !inputOwner.isActiveAndEnabled)
        { Close(false); return; }
        controlsText.text = inputOwner.GetBindingLabel("Interact") + (IsTyping ? " - Reveal text    " : " - Continue    ") +
            inputOwner.GetBindingLabel("Back") + " - Close";
        if (Time.frameCount <= openedFrame) return;
        if (!inputArmed)
        {
            if (!ConfirmHeld())
            {
                inputArmed = true;
                if (nextButton) nextButton.interactable = true;
                if (closeButton) closeButton.interactable = true;
                if (nextButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }
            return;
        }
        if (inputOwner.Pressed("Back") || inputOwner.PausePressedThisFrame) { Cancel(); return; }
        if (inputOwner.Pressed("Interact")) Advance();
    }

    private bool ConfirmHeld()
    {
        if (inputOwner && (inputOwner.Held("Interact") || inputOwner.Held("Back"))) return true;
        var module = EventSystem.current ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module && module.submit != null && module.submit.action != null && module.submit.action.IsPressed()) return true;
        return (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed)) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    public void Advance()
    {
        if (!IsOpen || !inputArmed || Time.frameCount <= openedFrame || actionFrame == Time.frameCount) return;
        actionFrame = Time.frameCount;
        if (IsTyping) { textAnimator.RevealAll(); return; }
        lineIndex++;
        if (lineIndex >= lines.Length) Close(true);
        else ShowLine();
    }

    private void ShowLine()
    {
        StopReveal();
        speakerText.text = string.IsNullOrWhiteSpace(lines[lineIndex].speaker) ? "Alderworn" : lines[lineIndex].speaker;
        if (HasTypewriter)
        {
            textAnimator.PreparePage(lines[lineIndex].text);
            revealRoutine = StartCoroutine(textAnimator.RevealPreparedPage());
        }
        else
        {
            bodyText.maxVisibleCharacters = int.MaxValue;
            bodyText.text = lines[lineIndex].text;
        }
    }

    public void Cancel()
    {
        if (!IsOpen || !inputArmed || Time.frameCount <= openedFrame) return;
        MenuUISounds.PlayBack();
        Close(false);
    }

    public void CancelImmediately() => Close(false);

    private void StopReveal()
    {
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        if (textAnimator) textAnimator.CancelReveal();
    }

    private void Close(bool completed)
    {
        if (!IsOpen) return;
        StopReveal();
        lines = null;
        closedFrame = Time.frameCount;
        if (panel) panel.SetActive(false);
        if (ownsFreeze)
        {
            ownsFreeze = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }
        if (inputOwner)
        {
            inputOwner.SetDialogueBlocked(false);
            // Consume a closing Interact press so it cannot immediately open crafting or Talk again.
            if (inputOwner.isActiveAndEnabled) inputOwner.TryInteract();
            inputOwner.BlockThisFrame();
        }
        inputOwner = null;
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        Action<bool> callback = finished;
        finished = null;
        callback?.Invoke(completed);
    }

    private void OnDisable() => Close(false);
}
