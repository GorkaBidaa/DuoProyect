using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    private enum Tab { Audio, Controls, Display }

    [Header("Keep this controller outside the panels below")]
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject audioPage;
    [SerializeField] private GameObject controlsPage;
    [SerializeField] private GameObject displayPage;
    [SerializeField] private KeybindPanel keybindPanel;
    [SerializeField] private Tab startingTab = Tab.Audio;

    [Header("Buttons")]
    [SerializeField] private Button restoreDefaultsButton;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private Button displayTabButton;

    [Header("Connect these when the corresponding settings are implemented")]
    [SerializeField] private UnityEvent onRestoreAudioDefaults = new UnityEvent();
    [SerializeField] private UnityEvent onRestoreDisplayDefaults = new UnityEvent();

    private Tab activeTab;
    private GameInput inputOwner;
    private GameObject previousSelection;
    private float previousTimeScale;
    private bool previousMainMenuActive;
    private bool isOpen;
    private int openedFrame;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        bool valid = settingsRoot && audioPage && controlsPage && keybindPanel &&
            restoreDefaultsButton && !transform.IsChildOf(settingsRoot.transform) &&
            !keybindPanel.transform.IsChildOf(settingsRoot.transform) &&
            audioPage != controlsPage && audioPage != settingsRoot && controlsPage != settingsRoot &&
            !audioPage.transform.IsChildOf(controlsPage.transform) &&
            !controlsPage.transform.IsChildOf(audioPage.transform) &&
            audioPage.transform.IsChildOf(settingsRoot.transform) &&
            controlsPage.transform.IsChildOf(settingsRoot.transform) &&
            keybindPanel.PanelRoot == controlsPage &&
            (!mainMenuPanel || (!settingsRoot.transform.IsChildOf(mainMenuPanel.transform) &&
             !transform.IsChildOf(mainMenuPanel.transform)));
        if (displayPage && (displayPage == settingsRoot || displayPage == audioPage ||
            displayPage == controlsPage || !displayPage.transform.IsChildOf(settingsRoot.transform) ||
            displayPage.transform.IsChildOf(audioPage.transform) ||
            displayPage.transform.IsChildOf(controlsPage.transform) ||
            audioPage.transform.IsChildOf(displayPage.transform) ||
            controlsPage.transform.IsChildOf(displayPage.transform))) valid = false;
        if (!valid)
        {
            Debug.LogError("Settings needs separate Audio/Controls pages inside Settings Root, a configured KeybindPanel on an active controller outside those panels, and Restore Defaults. Main Menu Panel must not contain Settings Root or the controller.", this);
            enabled = false;
            return;
        }
        settingsRoot.SetActive(false);
    }

    public void OpenSettings()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || isOpen) return;
        GameInput input = GameInput.Instance;
        if ((input && input.DialogueBlocked) || keybindPanel.IsOpen) return;
        previousTimeScale = Time.timeScale;
        previousMainMenuActive = mainMenuPanel && mainMenuPanel.activeSelf;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        inputOwner = input;
        if (inputOwner) inputOwner.SetSettingsBlocked(true);
        isOpen = true;
        openedFrame = Time.frameCount;
        Time.timeScale = 0f;
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        settingsRoot.SetActive(true);
        SelectTab(startingTab == Tab.Display && !displayPage ? Tab.Audio : startingTab);
    }

    public void CloseSettings()
    {
        if (!isOpen) return;
        // Capture is cancelled before changing panels or restoring gameplay input.
        keybindPanel.Close();
        isOpen = false;
        if (settingsRoot) settingsRoot.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(previousMainMenuActive);
        if (inputOwner) inputOwner.SetSettingsBlocked(false);
        inputOwner = null;
        Time.timeScale = previousTimeScale;
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy
                ? previousSelection : null);
    }

    public void ShowAudio() => SelectTab(Tab.Audio);
    public void ShowControls() => SelectTab(Tab.Controls);
    public void ShowDisplay() { if (displayPage) SelectTab(Tab.Display); }

    private void SelectTab(Tab tab)
    {
        if (!isOpen) return;
        keybindPanel.Close();
        activeTab = tab;
        audioPage.SetActive(tab == Tab.Audio);
        controlsPage.SetActive(tab == Tab.Controls);
        if (displayPage) displayPage.SetActive(tab == Tab.Display);
        if (tab == Tab.Controls) keybindPanel.OpenAsTab();
        RefreshRestoreButton();
        Button selected = tab == Tab.Audio ? audioTabButton :
            tab == Tab.Controls ? controlsTabButton : displayTabButton;
        if (selected && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }

    public void RestoreCurrentTabDefaults()
    {
        if (!isOpen || keybindPanel.IsRebinding) return;
        switch (activeTab)
        {
            case Tab.Controls: keybindPanel.RestoreDefaults(); break;
            case Tab.Audio: onRestoreAudioDefaults.Invoke(); break;
            case Tab.Display: onRestoreDisplayDefaults.Invoke(); break;
        }
        RefreshRestoreButton();
    }

    private void RefreshRestoreButton()
    {
        restoreDefaultsButton.interactable = !keybindPanel.IsRebinding &&
            (activeTab == Tab.Controls ? keybindPanel.IsOpen : activeTab == Tab.Audio
                ? onRestoreAudioDefaults.GetPersistentEventCount() > 0
                : onRestoreDisplayDefaults.GetPersistentEventCount() > 0);
    }

    private void Update()
    {
        if (!isOpen) return;
        RefreshRestoreButton();
        if (Time.frameCount <= openedFrame || keybindPanel.ConsumesBackThisFrame) return;
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame ||
             Gamepad.current.startButton.wasPressedThisFrame))) CloseSettings();
    }

    private void OnDisable() => CloseSettings();
}
