using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AudioSettingsRow : MonoBehaviour
{
    [SerializeField] private GameAudioChannel channel;
    [SerializeField] private Slider valueSlider;
    [SerializeField] private TMP_Text percentageText;
    [Tooltip("Optional. The label will use the selected channel's name.")]
    [SerializeField] private TMP_Text settingText;
    private AudioSettingsManager manager;
    private bool reportedMissingManager;

    private void OnEnable()
    {
        if (!valueSlider || !percentageText)
        {
            Debug.LogError("AudioSettingsRow '" + name +
                "': assign Value Slider and Percentage Text on this row.", this);
            enabled = false;
            return;
        }
        valueSlider.minValue = 0f;
        valueSlider.maxValue = 1f;
        valueSlider.wholeNumbers = false;
        valueSlider.onValueChanged.AddListener(OnSliderChanged);
        if (settingText) settingText.text = channel == GameAudioChannel.Master ? "Master Volume" : channel.ToString();
        TryBindManager();
    }

    private void Start()
    {
        TryBindManager();
        if (!manager && !reportedMissingManager)
        {
            reportedMissingManager = true;
            Debug.LogWarning("AudioSettingsRow '" + name +
                "' is waiting for an active GameAudio object with a configured AudioSettingsManager.", this);
        }
    }

    private void Update()
    {
        if (!manager || !manager.isActiveAndEnabled) TryBindManager();
    }

    private void TryBindManager()
    {
        AudioSettingsManager candidate = AudioSettingsManager.Instance;
        if (candidate && !candidate.isActiveAndEnabled) candidate = null;
        if (candidate == manager)
        {
            valueSlider.interactable = manager != null;
            return;
        }
        if (manager) manager.VolumeChanged -= OnVolumeChanged;
        manager = candidate;
        valueSlider.interactable = manager != null;
        if (!manager) return;
        manager.VolumeChanged += OnVolumeChanged;
        Refresh(manager.GetVolume(channel));
    }

    private void OnSliderChanged(float value)
    {
        if (manager) manager.SetVolume(channel, value);
        Refresh(value);
    }

    private void OnVolumeChanged(GameAudioChannel changed, float value)
    {
        if (changed == channel) Refresh(value);
    }

    private void Refresh(float value)
    {
        valueSlider.SetValueWithoutNotify(value);
        percentageText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnDisable()
    {
        if (valueSlider) valueSlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (manager) manager.VolumeChanged -= OnVolumeChanged;
        manager = null;
    }
}
