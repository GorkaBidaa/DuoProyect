using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-190)]
public class GameplayPauseMenu : MonoBehaviour
{
    [SerializeField] private SettingsPanelController settingsController;
    [SerializeField] private KeybindPanel keybindPanel;
    [SerializeField] private InventoryMenu inventoryMenu;
    [SerializeField] private CraftingMenu craftingMenu;
    [SerializeField] private StaminaSystem player;
    [Header("Buttons inside Settings")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [Header("Return to title")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private TMP_Text loadingText;

    private GameInput inputOwner;
    private GameObject previousSelection;
    private float previousTimeScale;
    private bool previousAudioPause;
    private bool busy, settingsOwned;
    private int openedFrame = -1;
    private AsyncOperation pendingLoad;
    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (!settingsController || !settingsController.SettingsRoot || !keybindPanel ||
            !inventoryMenu || !craftingMenu || !player || !resumeButton || !mainMenuButton ||
            transform.IsChildOf(settingsController.SettingsRoot.transform))
        {
            Debug.LogError("GameplayPauseMenu needs Settings, KeybindPanel, inventory/crafting, Player, Resume and Main Menu buttons. Keep the controller outside Settings Root.", this);
            enabled = false;
            return;
        }
        if (loadingText) loadingText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (settingsController) settingsController.Closed += OnSettingsClosed;
    }

    private void Update()
    {
        // Settings owns Back/Pause while open, including rebind cancellation.
        if (busy || IsPaused || !player || player.IsDead) return;
        GameInput input = GameInput.Instance;
        if (!input || settingsController.IsOpen || settingsController.ConsumesBackThisFrame ||
            keybindPanel.IsOpen || keybindPanel.ConsumesBackThisFrame ||
            !input.PausePressedThisFrame || Time.frameCount <= openedFrame) return;
        if (input.DialogueBlocked || Time.timeScale <= 0f) return;
        if (inventoryMenu.IsOpen)
        {
            inventoryMenu.Close();
            input.BlockThisFrame();
            return;
        }
        if (craftingMenu.IsOpen)
        {
            craftingMenu.Close();
            input.BlockThisFrame();
            return;
        }
        Pause();
    }

    public void Pause()
    {
        GameInput input = GameInput.Instance;
        if (!isActiveAndEnabled || IsPaused || busy || !input || !player || player.IsDead ||
            input.DialogueBlocked || Time.timeScale <= 0f || inventoryMenu.IsOpen ||
            craftingMenu.IsOpen || settingsController.IsOpen || keybindPanel.IsOpen ||
            settingsController.ConsumesBackThisFrame || keybindPanel.ConsumesBackThisFrame) return;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        inputOwner = input;
        IsPaused = true;
        openedFrame = Time.frameCount;
        inputOwner.SetPauseBlocked(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        settingsController.OpenSettings();
        settingsOwned = settingsController.IsOpen;
        if (!settingsOwned)
        {
            ReleasePause(false);
            Debug.LogWarning("Pause could not open Settings. Check SettingsPanelController's references and Console errors.", this);
            return;
        }
        MenuUISounds.PlayClick();
        Select(resumeButton);
    }

    public void Resume()
    {
        if (!IsPaused || busy || Time.frameCount <= openedFrame || keybindPanel.IsRebinding ||
            settingsController.ConsumesBackThisFrame) return;
        MenuUISounds.PlayBack();
        CloseOwnedSettings();
        ReleasePause(false);
    }

    // Compatibility for an existing On Click event; a separate Options button is unnecessary.
    public void OpenOptions() => Pause();

    private void OnSettingsClosed()
    {
        if (!settingsOwned || !IsPaused || busy) return;
        settingsOwned = false;
        ReleasePause(false);
    }

    private void CloseOwnedSettings()
    {
        bool close = settingsOwned;
        settingsOwned = false;
        if (close && settingsController && settingsController.IsOpen)
            settingsController.CloseSettings();
    }

    public void ReturnToMainMenu()
    {
        if (!IsPaused || busy || Time.frameCount <= openedFrame || keybindPanel.IsRebinding ||
            settingsController.ConsumesBackThisFrame) return;
        if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError("Add MainMenu to the enabled Scene List in File > Build Profiles, and check Main Menu Scene.", this);
            return;
        }
        if (AudioSettingsManager.Instance) AudioSettingsManager.Instance.SaveNow();
        PlayerPrefs.Save();
        MenuUISounds.PlayClick();
        busy = true;
        settingsController.CloseRequestsBlocked = true;
        SetButtons(false);
        if (loadingText)
        {
            loadingText.text = "Loading...";
            loadingText.gameObject.SetActive(true);
        }
        try
        {
            pendingLoad = SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
        }
        catch (Exception error)
        {
            Debug.LogError("Could not return to Main Menu: " + error.Message, this);
            pendingLoad = null;
        }
        if (pendingLoad == null)
        {
            busy = false;
            settingsController.CloseRequestsBlocked = false;
            SetButtons(true);
            if (loadingText) loadingText.gameObject.SetActive(false);
            Select(resumeButton);
            return;
        }
        pendingLoad.allowSceneActivation = false;
        StartCoroutine(ActivateMainMenu());
    }

    private IEnumerator ActivateMainMenu()
    {
        while (pendingLoad.progress < 0.9f) yield return null;
        pendingLoad.allowSceneActivation = true;
    }

    private void SetButtons(bool interactable)
    {
        if (resumeButton) resumeButton.interactable = interactable;
        if (mainMenuButton) mainMenuButton.interactable = interactable;
    }

    private static void Select(Button button)
    {
        if (button && button.IsActive() && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void ReleasePause(bool leavingScene)
    {
        if (!IsPaused) return;
        IsPaused = false;
        if (inputOwner) inputOwner.SetPauseBlocked(false);
        inputOwner = null;
        Time.timeScale = leavingScene ? 1f : previousTimeScale;
        AudioListener.pause = leavingScene ? false : previousAudioPause;
        if (!leavingScene && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy
                ? previousSelection : null);
        previousSelection = null;
    }

    private void OnDisable()
    {
        if (settingsController)
        {
            settingsController.Closed -= OnSettingsClosed;
            if (IsPaused) settingsController.CloseRequestsBlocked = false;
        }
        CloseOwnedSettings();
        ReleasePause(busy);
        if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
    }
}
