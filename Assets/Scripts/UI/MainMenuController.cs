using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameplayScene = "BACKUPTutorial";
    [SerializeField] private SettingsPanelController settingsController;
    [SerializeField] private MainMenuAudio menuAudio;
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField, Min(0.12f)] private float transitionDuration = 0.4f;

    [Header("Full-screen loading overlay")]
    [SerializeField] private CanvasGroup loadingOverlay;
    [Tooltip("Raises a separate overlay Canvas above the menu. No effect on a shared Canvas's order.")]
    [SerializeField] private int overlaySortOrder = 1000;
    [SerializeField] private GameObject loadingContent;
    [SerializeField] private Slider loadingProgress;
    [SerializeField, Min(0f)] private float minimumLoadingSeconds = 0.6f;

    [Header("Opening monologue (before gameplay starts)")]
    [SerializeField] private bool showIntroduction = true;
    [SerializeField] private CanvasGroup introductionContent;
    [SerializeField] private TMP_Text introductionText;
    [SerializeField] private IntroTextAnimator introTextAnimator;
    [Tooltip("Off: press Continue after reading. On: use Page Hold Seconds after typing finishes.")]
    [SerializeField] private bool autoAdvancePages;
    [SerializeField] private TMP_Text skipHint;
    [SerializeField] private Button skipButton;
    [SerializeField, TextArea(2, 6)]
    private string[] introductionPages =
    {
        "The ship...\nI remember the flight, but not landing.",
        "I need to contact my crew.\nThe transmitter is damaged."
    };
    [SerializeField, Min(0.5f)] private float pageHoldSeconds = 4.5f;
    [SerializeField, Min(0.01f)] private float textFadeSeconds = 0.4f;

    private bool busy, introPlaying, skipArmed, skipRequested, usingGamepad;
    private float introOpenedAt;
    private bool pageCanReceiveInput, advanceRequested;
    private int introActionFrame = -1;
    private AsyncOperation pendingLoad;

    private void Start()
    {
        if (!introTextAnimator && introductionText) introTextAnimator = introductionText.GetComponent<IntroTextAnimator>();
        if (loadingOverlay && !transform.IsChildOf(loadingOverlay.transform))
            loadingOverlay.gameObject.SetActive(false);
        if (introductionContent && !transform.IsChildOf(introductionContent.transform))
            introductionContent.gameObject.SetActive(false);
        if (loadingText) loadingText.gameObject.SetActive(false);
        if (skipButton) skipButton.interactable = false;
        SelectPlay();
    }

    private void Update()
    {
        if (busy && pendingLoad != null && !pendingLoad.isDone)
        {
            float progress = Mathf.Clamp01(pendingLoad.progress / 0.9f);
            if (loadingProgress) loadingProgress.SetValueWithoutNotify(progress);
            if (loadingText) loadingText.text = "Loading... " + Mathf.RoundToInt(progress * 100f) + "%";
        }
        if (!introPlaying) return;
        UpdateSkipHint();
        // A held Play/Submit input must be released before it can skip the opening.
        if (!skipArmed)
        {
            if (Time.unscaledTime - introOpenedAt >= 0.25f && !SkipInputHeld())
            {
                skipArmed = true;
                if (skipButton) skipButton.interactable = true;
            }
            return;
        }
        if (SkipWholeIntroPressed()) SkipIntro();
        else if (ConfirmInputPressed()) AdvanceIntro();
    }

    public void Play()
    {
        if (busy || (settingsController && settingsController.IsOpen)) return;
        if (!ValidEntryUI()) return;
        if (string.IsNullOrWhiteSpace(gameplayScene) || !Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError("Play cannot load '" + gameplayScene +
                "'. Add it to the enabled Scene List in File > Build Profiles.", this);
            return;
        }
        UpdateSkipHint();
        MenuUISounds.PlayClick();
        SetBusy(true);
        if (AudioSettingsManager.Instance) AudioSettingsManager.Instance.SaveNow();
        loadingOverlay.gameObject.SetActive(true);
        loadingOverlay.alpha = 0f;
        loadingOverlay.blocksRaycasts = true;
        loadingOverlay.interactable = true;
        RaiseOverlayAboveMenu();
        loadingContent.SetActive(false);
        if (introductionContent) introductionContent.gameObject.SetActive(false);
        if (loadingText) { loadingText.text = "Loading... 0%"; loadingText.gameObject.SetActive(false); }
        if (loadingProgress)
        {
            loadingProgress.minValue = 0f;
            loadingProgress.maxValue = 1f;
            loadingProgress.wholeNumbers = false;
            loadingProgress.interactable = false;
            loadingProgress.SetValueWithoutNotify(0f);
        }
        StartCoroutine(LoadGame());
    }

    private bool ValidEntryUI()
    {
        bool valid = loadingOverlay && loadingContent &&
            !transform.IsChildOf(loadingOverlay.transform) &&
            loadingContent != loadingOverlay.gameObject &&
            loadingContent.transform.IsChildOf(loadingOverlay.transform);
        if (showIntroduction)
            valid = valid && introductionContent && introductionText && skipHint &&
                introductionContent.gameObject != loadingContent &&
                introductionContent != loadingOverlay &&
                introductionContent.transform.IsChildOf(loadingOverlay.transform) &&
                !introductionContent.transform.IsChildOf(loadingContent.transform) &&
                !loadingContent.transform.IsChildOf(introductionContent.transform) &&
                introductionText.transform.IsChildOf(introductionContent.transform) &&
                skipHint.transform.IsChildOf(introductionContent.transform);
        if (!valid)
            Debug.LogError("Assign the loading overlay and its separate LoadingContent/IntroductionContent children. Intro needs body and skip-hint text. Keep MainMenuController outside the overlay.", this);
        return valid;
    }

    public void OpenSettings()
    {
        if (busy) return;
        MenuUISounds.PlayClick();
        if (settingsController) settingsController.OpenSettings();
        else Debug.LogError("MainMenuController: assign Settings Controller.", this);
    }

    public void Exit()
    {
        if (busy) return;
        MenuUISounds.PlayClick();
        SetBusy(true);
        if (AudioSettingsManager.Instance) AudioSettingsManager.Instance.SaveNow();
        StartCoroutine(QuitAfterFade());
    }

    public void SkipIntro()
    {
        if (!introPlaying || !skipArmed || skipRequested) return;
        skipRequested = true;
        pageCanReceiveInput = false;
        if (introTextAnimator) introTextAnimator.CancelReveal();
        if (skipButton) skipButton.interactable = false;
    }

    public void AdvanceIntro()
    {
        if (!introPlaying || !skipArmed || skipRequested || !pageCanReceiveInput ||
            introActionFrame == Time.frameCount) return;
        introActionFrame = Time.frameCount;
        if (introTextAnimator && introTextAnimator.IsRevealing)
        {
            introTextAnimator.RevealAll();
            return;
        }
        advanceRequested = true;
        pageCanReceiveInput = false;
    }

    private void RaiseOverlayAboveMenu()
    {
        loadingOverlay.transform.SetAsLastSibling();
        Canvas overlayCanvas = loadingOverlay.GetComponentInParent<Canvas>();
        Canvas menuCanvas = playButton ? playButton.GetComponentInParent<Canvas>() : null;
        if (!overlayCanvas) return;
        Canvas overlayRoot = overlayCanvas.rootCanvas;
        Canvas menuRoot = menuCanvas ? menuCanvas.rootCanvas : null;
        if (overlayRoot != menuRoot)
        {
            // Sibling order cannot put one root Canvas above a different root Canvas.
            if (menuRoot) overlayRoot.sortingLayerID = menuRoot.sortingLayerID;
            int aboveMenu = menuRoot ? menuRoot.sortingOrder + 1 : overlaySortOrder;
            overlayRoot.sortingOrder = Mathf.Max(overlayRoot.sortingOrder, Mathf.Max(overlaySortOrder, aboveMenu));
        }
    }

    private IEnumerator LoadGame()
    {
        float duration = Mathf.Max(0.12f, transitionDuration);
        if (menuAudio) menuAudio.FadeOut(duration);
        float elapsed = 0f;
        // First fully cover the menu. Intro text appears only once the backdrop is opaque.
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingOverlay.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        loadingOverlay.alpha = 1f;

        if (showIntroduction && introductionPages != null)
        {
            loadingContent.SetActive(false);
            introductionContent.gameObject.SetActive(true);
            introductionContent.alpha = 0f;
            introductionContent.interactable = true;
            introductionContent.blocksRaycasts = true;
            introPlaying = true;
            skipRequested = skipArmed = false;
            introOpenedAt = Time.unscaledTime;
            if (skipButton) skipButton.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            UpdateSkipHint();
            foreach (string page in introductionPages)
            {
                if (skipRequested) break;
                if (string.IsNullOrWhiteSpace(page)) continue;
                advanceRequested = false;
                pageCanReceiveInput = false;
                bool animate = introTextAnimator && introTextAnimator.isActiveAndEnabled;
                if (animate) introTextAnimator.PreparePage(page);
                else { introductionText.maxVisibleCharacters = int.MaxValue; introductionText.text = page; }
                yield return FadeIntro(1f);
                if (skipRequested) break;
                pageCanReceiveInput = true;
                if (animate) yield return introTextAnimator.RevealPreparedPage();
                if (skipRequested) break;
                float until = Time.unscaledTime + Mathf.Max(0.5f, pageHoldSeconds);
                while (!skipRequested && !advanceRequested && (!autoAdvancePages || Time.unscaledTime < until))
                    yield return null;
                pageCanReceiveInput = false;
                if (introTextAnimator) introTextAnimator.CancelReveal();
                if (skipRequested) break;
                yield return FadeIntro(0f);
            }
            introPlaying = false;
            pageCanReceiveInput = false;
            if (introTextAnimator) introTextAnimator.CancelReveal();
            if (skipButton) skipButton.interactable = false;
            if (skipRequested)
            {
                // Finish the visual fade even though the monologue was skipped.
                yield return FadeIntro(0f, false);
            }
            introductionContent.gameObject.SetActive(false);
        }

        // Intro is finished/skipped. Keep the same opaque backdrop for the loading screen.
        loadingContent.SetActive(true);
        if (loadingText)
        {
            loadingText.text = "Loading... 0%";
            loadingText.gameObject.SetActive(true);
        }
        // Let the loading UI render before scene loading begins.
        yield return null;
        float openedAt = Time.unscaledTime;
        pendingLoad = TryStartLoad();
        if (pendingLoad == null)
        {
            loadingOverlay.gameObject.SetActive(false);
            SetBusy(false);
            if (menuAudio) menuAudio.FadeIn(0.3f);
            SelectPlay();
            yield break;
        }
        pendingLoad.allowSceneActivation = false;
        while (pendingLoad.progress < 0.9f || Time.unscaledTime - openedAt < Mathf.Max(0f, minimumLoadingSeconds))
            yield return null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pendingLoad.allowSceneActivation = true;
    }

    private IEnumerator FadeIntro(float target, bool interruptible = true)
    {
        float start = introductionContent.alpha;
        float duration = Mathf.Max(0.01f, textFadeSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (interruptible && skipRequested) yield break;
            elapsed += Time.unscaledDeltaTime;
            introductionContent.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        introductionContent.alpha = target;
    }

    private static bool SkipInputHeld()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
             keyboard.escapeKey.isPressed)) ||
            (pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed)) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    private static bool SkipWholeIntroPressed() =>
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

    private static bool ConfirmInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private void UpdateSkipHint()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) usingGamepad = false;
        else
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue().sqrMagnitude > 0.01f)
                usingGamepad = true;
            foreach (var control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                    usingGamepad = true;
        }
        Mouse mouse = Mouse.current;
        if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame)))
            usingGamepad = false;
        if (skipHint)
        {
            string confirm = usingGamepad ? "A / Cross" : "Space / Enter";
            string skip = usingGamepad ? "B / Circle" : "Esc";
            string action = introTextAnimator && introTextAnimator.IsRevealing ? "Reveal text" : "Next";
            skipHint.text = confirm + " - " + action + "    " + skip + " - Skip intro";
        }
    }

    private AsyncOperation TryStartLoad()
    {
        try { return SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single); }
        catch (Exception error)
        {
            Debug.LogError("Could not load gameplay: " + error.Message, this);
            return null;
        }
    }

    private IEnumerator QuitAfterFade()
    {
        float duration = Mathf.Max(0.12f, transitionDuration);
        if (menuAudio) menuAudio.FadeOut(duration);
        yield return new WaitForSecondsRealtime(duration);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetBusy(bool value)
    {
        busy = value;
        if (playButton) playButton.interactable = !value;
        if (settingsButton) settingsButton.interactable = !value;
        if (exitButton) exitButton.interactable = !value;
        if (!value && loadingText) loadingText.gameObject.SetActive(false);
    }

    private void SelectPlay()
    {
        if (playButton && playButton.IsActive() && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    private void OnDisable()
    {
        if (introTextAnimator) introTextAnimator.CancelReveal();
        // Unity cannot cancel an async scene load. Release it if this controller is unexpectedly removed.
        if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
    }
}
