using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-160)]
public class TutorialCompletion : MonoBehaviour
{
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [Header("Completion UI - keep this controller outside the panel")]
    [SerializeField] private StaminaSystem player;
    [SerializeField] private CanvasGroup completionPanel;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string title = "Tutorial Complete";
    [SerializeField, TextArea(2, 5)] private string message = "You crafted the special key and opened the gate.";
    [SerializeField, Min(0f)] private float afterGateDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float panelFadeSeconds = 0.35f;
    [SerializeField] private int panelSortOrder = 3000;
    [Header("Completion sound")]
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioClip completionClip;
    [SerializeField] private bool useTemporarySound = true;
    [SerializeField, Range(0f, 1f)] private float completionVolume = 0.55f;
    [Header("Return to title")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    private bool ready, ownsFreeze, panelReady, busy, previousAudioPause;
    private float previousTimeScale;
    private int readyFrame = -1;
    private GameInput inputOwner;
    private GameObject previousSelection;
    private AudioSource voice;
    private AudioClip temporaryClip;
    private AsyncOperation pendingLoad;
    private Canvas panelCanvas;
    private readonly List<GameObject> gateEffects = new List<GameObject>();
    public bool IsCompleted { get; private set; }
    public bool CanComplete => ready && isActiveAndEnabled && !IsCompleted && player && player.isActiveAndEnabled && !player.IsDead &&
        completionPanel && mainMenuButton &&
        panelCanvas && panelCanvas.enabled && panelCanvas.rootCanvas.enabled &&
        (!completionPanel.transform.parent || completionPanel.transform.parent.gameObject.activeInHierarchy) &&
        EventSystem.current && EventSystem.current.isActiveAndEnabled &&
        Time.timeScale > 0f && GameInput.Instance && GameInput.Instance.PromptsAllowed;

    private void Awake()
    {
        panelCanvas = completionPanel ? completionPanel.GetComponentInParent<Canvas>(true) : null;
        if (!player || !completionPanel || !mainMenuButton ||
            !panelCanvas ||
            player.gameObject.scene != gameObject.scene || completionPanel.gameObject.scene != gameObject.scene ||
            transform.IsChildOf(completionPanel.transform) ||
            !mainMenuButton.transform.IsChildOf(completionPanel.transform))
        {
            Debug.LogError("TutorialCompletion needs Player, Completion Panel CanvasGroup and its Main Menu button. Keep this controller active outside the hidden panel.", this);
            return;
        }
        completionPanel.alpha = 0f;
        completionPanel.gameObject.SetActive(false);
        mainMenuButton.interactable = false;
        if (statusText) statusText.gameObject.SetActive(false);
        var child = new GameObject("TutorialCompletionVoice");
        child.transform.SetParent(transform, false);
        voice = child.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.ignoreListenerPause = true;
        voice.outputAudioMixerGroup = sfxGroup;
        if (!completionClip && useTemporarySound) temporaryClip = TutorialFeedbackAudio.CreateCompletionSound();
        ready = true;
    }

    public bool Complete() => Complete(null);

    public bool Complete(SpecialKeyGate gate)
    {
        if (!CanComplete) return false;
        IsCompleted = true;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        inputOwner = GameInput.Instance;
        inputOwner.SetDialogueBlocked(true);
        ownsFreeze = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(ShowCompletion(gate));
        return true;
    }

    private IEnumerator ShowCompletion(SpecialKeyGate gate)
    {
        if (gate)
        {
            IEnumerator opening = gate.PlayOpeningFeedback(this);
            while (true)
            {
                object step;
                try
                {
                    if (!opening.MoveNext()) break;
                    step = opening.Current;
                }
                catch (Exception error)
                {
                    Debug.LogError("Gate feedback failed; continuing to completion UI: " + error.Message, this);
                    if (gate) gate.FinishOpeningVisuals();
                    break;
                }
                yield return step;
            }
        }
        if (afterGateDelay > 0f) yield return new WaitForSecondsRealtime(afterGateDelay);
        if (titleText) titleText.text = title;
        if (messageText) messageText.text = message;
        completionPanel.gameObject.SetActive(true);
        completionPanel.transform.SetAsLastSibling();
        Canvas canvas = completionPanel.GetComponentInParent<Canvas>();
        if (canvas) canvas.rootCanvas.sortingOrder = Mathf.Max(canvas.rootCanvas.sortingOrder, panelSortOrder);
        completionPanel.blocksRaycasts = true;
        completionPanel.interactable = true;
        completionPanel.alpha = 0f;
        AudioClip clip = completionClip ? completionClip : temporaryClip;
        if (voice && clip)
        {
            voice.clip = clip;
            voice.volume = completionVolume;
            voice.Play();
        }
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, panelFadeSeconds);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            completionPanel.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        completionPanel.alpha = 1f;
        // The interaction/Submit which unlocked the gate must not also press Return to Main Menu.
        while (SubmitHeld()) yield return null;
        yield return null;
        panelReady = true;
        readyFrame = Time.frameCount;
        mainMenuButton.interactable = true;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(mainMenuButton.gameObject);
        Debug.Log("Tutorial complete: the special key unlocked the gate.", this);
        onCompleted.Invoke();
    }

    private bool SubmitHeld()
    {
        var module = EventSystem.current ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (module && module.submit != null && module.submit.action != null && module.submit.action.IsPressed()) return true;
        var interact = inputOwner ? inputOwner.GameplayActions?.FindAction("Interact", false) : null;
        if (interact != null && interact.IsPressed()) return true;
        Keyboard keyboard = Keyboard.current;
        return (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed)) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    public void ReturnToMainMenu()
    {
        if (!panelReady || busy || Time.frameCount <= readyFrame) return;
        if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            SetStatus("Main Menu is unavailable. Check the Build Profiles scene list.");
            Debug.LogError("TutorialCompletion: MainMenu must be enabled in Build Profiles and Main Menu Scene must match its name.", this);
            return;
        }
        if (AudioSettingsManager.Instance) AudioSettingsManager.Instance.SaveNow();
        PlayerPrefs.Save();
        MenuUISounds.PlayClick();
        busy = true;
        mainMenuButton.interactable = false;
        SetStatus("Loading...");
        try { pendingLoad = SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single); }
        catch (Exception error)
        {
            Debug.LogError("Could not load Main Menu: " + error.Message, this);
            pendingLoad = null;
        }
        if (pendingLoad == null)
        {
            busy = false;
            mainMenuButton.interactable = true;
            SetStatus("Could not load Main Menu. Try again.");
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(mainMenuButton.gameObject);
            return;
        }
        pendingLoad.allowSceneActivation = false;
        StartCoroutine(ActivateMenu());
    }

    private IEnumerator ActivateMenu()
    {
        while (pendingLoad.progress < 0.9f) yield return null;
        pendingLoad.allowSceneActivation = true;
    }

    private void SetStatus(string text)
    {
        if (statusText) { statusText.text = text; statusText.gameObject.SetActive(true); }
    }

    public void TrackGateEffect(GameObject effect)
    {
        if (effect) gateEffects.Add(effect);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (voice) voice.Stop();
        foreach (var effect in gateEffects) if (effect) Destroy(effect);
        gateEffects.Clear();
        if (ownsFreeze)
        {
            ownsFreeze = false;
            if (inputOwner) inputOwner.SetDialogueBlocked(false);
            Time.timeScale = busy ? 1f : previousTimeScale;
            AudioListener.pause = busy ? false : previousAudioPause;
            if (!busy && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        if (completionPanel && !transform.IsChildOf(completionPanel.transform)) completionPanel.gameObject.SetActive(false);
        if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
    }

    private void OnDestroy()
    {
        if (voice) Destroy(voice.gameObject);
        if (temporaryClip) Destroy(temporaryClip);
    }
}

internal static class TutorialFeedbackAudio
{
    public static AudioClip CreateGateSound()
    {
        const int rate = 22050;
        const float duration = 0.24f;
        var samples = new float[(int)(rate * duration)];
        var random = new System.Random(831);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float a = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 28f);
            float second = t - 0.09f;
            float b = second < 0f ? 0f : Mathf.Min(1f, second / 0.003f) * Mathf.Exp(-second * 35f);
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            samples[i] = Mathf.Clamp((a + b * 0.7f) *
                (0.25f * noise + 0.4f * Mathf.Sin(2f * Mathf.PI * 110f * t)), -1f, 1f);
        }
        return Create("TemporaryGateUnlock", samples, rate);
    }

    public static AudioClip CreateCompletionSound()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.85f)];
        float[] notes = { 392f, 494f, 587f };
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float value = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float local = t - n * 0.12f;
                if (local < 0f) continue;
                float envelope = Mathf.Min(1f, local / 0.008f) * Mathf.Exp(-local * 8f);
                value += 0.25f * envelope * Mathf.Sin(2f * Mathf.PI * notes[n] * local);
            }
            samples[i] = value * Mathf.Clamp01((0.85f - t) / 0.08f);
        }
        return Create("TemporaryTutorialComplete", samples, rate);
    }

    private static AudioClip Create(string name, float[] samples, int rate)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
