using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private bool busy;
    private AsyncOperation pendingLoad;

    private void Start()
    {
        if (loadingText) loadingText.gameObject.SetActive(false);
        if (playButton && playButton.IsActive() && EventSystem.current &&
            !EventSystem.current.currentSelectedGameObject)
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    public void Play()
    {
        if (busy) return;
        MenuUISounds.PlayClick();
        if (string.IsNullOrWhiteSpace(gameplayScene) || !Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError("Play cannot load '" + gameplayScene +
                "'. Add that scene to the enabled Scene List in File > Build Profiles.", this);
            return;
        }
        SetBusy(true);
        if (AudioSettingsManager.Instance) AudioSettingsManager.Instance.SaveNow();
        StartCoroutine(LoadGame());
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

    private IEnumerator LoadGame()
    {
        pendingLoad = TryStartLoad();
        if (pendingLoad == null)
        {
            SetBusy(false);
            if (menuAudio) menuAudio.FadeIn(0.3f);
            yield break;
        }
        pendingLoad.allowSceneActivation = false;
        if (loadingText) { loadingText.text = "Loading..."; loadingText.gameObject.SetActive(true); }
        while (pendingLoad.progress < 0.9f) yield return null;
        float duration = Mathf.Max(0.12f, transitionDuration);
        if (menuAudio) menuAudio.FadeOut(duration);
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pendingLoad.allowSceneActivation = true;
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

    private void OnDisable()
    {
        // Release an outstanding load if the menu is unexpectedly deactivated.
        if (pendingLoad != null && !pendingLoad.isDone) pendingLoad.allowSceneActivation = true;
    }
}
