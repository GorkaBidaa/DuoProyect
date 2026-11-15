using UnityEngine;
using UnityEngine.Audio;

public enum MenuUICue { Click, Back, Silent }

[DefaultExecutionOrder(-500)]
public sealed class MenuUISounds : MonoBehaviour
{
    private static MenuUISounds instance;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip backClip;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
    [Tooltip("Optional. Also plays Back when Escape/controller Cancel closes settings.")]
    [SerializeField] private SettingsPanelController settingsController;
    private AudioSource source;
    private AudioClip temporaryClick;
    private int actionFrame = -1;
    private float nextHoverTime;
    private bool settingsWereOpen;

    private void Awake()
    {
        if (instance && instance != this)
        {
            Debug.LogError("Keep one active MenuUISounds per scene.", this);
            enabled = false;
            return;
        }
        instance = this;
        var child = new GameObject("UISoundVoice");
        child.transform.SetParent(transform, false);
        source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = sfxGroup;
        if (!clickClip) clickClip = temporaryClick = CreateTemporaryClick();
        if (!sfxGroup) Debug.LogWarning("MenuUISounds: assign the SFX mixer group so audio settings affect UI sounds.", this);
    }

    private void OnEnable() { if (source) instance = this; }

    public static void PlayClick() { if (instance && instance.isActiveAndEnabled) instance.PlayAction(false); }
    public static void PlayBack() { if (instance && instance.isActiveAndEnabled) instance.PlayAction(true); }
    public static void PlayHover()
    {
        if (!instance || !instance.isActiveAndEnabled || !instance.hoverClip ||
            Time.unscaledTime < instance.nextHoverTime || instance.actionFrame == Time.frameCount) return;
        instance.nextHoverTime = Time.unscaledTime + 0.08f;
        instance.Play(instance.hoverClip, 1f);
    }

    private void PlayAction(bool back)
    {
        // A menu function and its button feedback may request the same click in one frame.
        if (actionFrame == Time.frameCount) return;
        actionFrame = Time.frameCount;
        Play(back && backClip ? backClip : clickClip, back && !backClip ? 0.88f : 1f);
    }

    private void Play(AudioClip clip, float pitch)
    {
        if (!source || !clip) return;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        // A single voice replaces the previous tail instead of piling up overlapping clicks.
        source.Play();
    }

    private void LateUpdate()
    {
        bool open = settingsController && settingsController.IsOpen;
        if (settingsWereOpen && !open) PlayBack();
        settingsWereOpen = open;
    }

    private static AudioClip CreateTemporaryClick()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.065f)];
        var random = new System.Random(9431);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            samples[i] = Mathf.Min(1f, t * 1800f) * Mathf.Exp(-t * 95f) *
                (0.35f * Mathf.Sin(2f * Mathf.PI * 1450f * t) +
                 0.18f * Mathf.Sin(2f * Mathf.PI * 380f * t) + noise * 0.08f);
        }
        AudioClip clip = AudioClip.Create("TemporaryUIClick", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (source) source.Stop();
        if (instance == this) instance = null;
    }
    private void OnDestroy()
    {
        if (source) Destroy(source.gameObject);
        if (temporaryClick) Destroy(temporaryClick);
    }
}
