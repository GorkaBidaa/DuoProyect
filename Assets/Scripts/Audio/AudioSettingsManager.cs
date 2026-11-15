using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum GameAudioChannel { Master, SFX, Ambience, Music }

[DefaultExecutionOrder(-1000)]
public sealed class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [Header("One active manager per scene, outside panels that get hidden")]
    [SerializeField] private AudioMixer mixer;
    [Header("Exposed mixer parameter names — spelling must match")]
    [SerializeField] private string masterParameter = "MasterVolume";
    [SerializeField] private string sfxParameter = "SFXVolume";
    [SerializeField] private string ambienceParameter = "AmbienceVolume";
    [SerializeField] private string musicParameter = "MusicVolume";
    [Header("Restore Defaults values")]
    [SerializeField, Range(0f, 1f)] private float defaultMaster = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultSFX = 0.75f;
    [SerializeField, Range(0f, 1f)] private float defaultAmbience = 0.65f;
    [SerializeField, Range(0f, 1f)] private float defaultMusic = 0.6f;

    private static readonly string[] SaveKeys =
    {
        "DuoProyect.Audio.v1.Master", "DuoProyect.Audio.v1.SFX",
        "DuoProyect.Audio.v1.Ambience", "DuoProyect.Audio.v1.Music"
    };
    private readonly float[] levels = new float[4];
    private readonly HashSet<string> reportedParameters = new HashSet<string>();
    private bool initialized, started, pendingApply, dirty;
    private float saveAt;
    public event Action<GameAudioChannel, float> VolumeChanged;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("There is already an AudioSettingsManager. Keep one active GameAudio object per scene.", this);
            enabled = false;
            return;
        }
        if (!mixer)
        {
            Debug.LogError("AudioSettingsManager: assign the GameAudio AudioMixer asset to Mixer.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < levels.Length; i++)
        {
            float fallback = DefaultFor((GameAudioChannel)i);
            levels[i] = Sanitize(PlayerPrefs.GetFloat(SaveKeys[i], fallback), fallback);
        }
        initialized = true;
        Instance = this;
    }

    private void OnEnable()
    {
        if (!initialized) return;
        Instance = this;
        pendingApply = true;
    }

    private void Start()
    {
        // AudioMixer.SetFloat is applied from Start onward, never during Awake/OnEnable.
        started = true;
        ApplyAll();
    }

    private void Update()
    {
        if (pendingApply && started) ApplyAll();
        if (dirty && Time.unscaledTime >= saveAt) SaveNow();
    }

    public float GetVolume(GameAudioChannel channel)
    {
        int index = (int)channel;
        return index >= 0 && index < levels.Length ? levels[index] : 1f;
    }

    public void SetVolume(GameAudioChannel channel, float value)
    {
        int index = (int)channel;
        if (!initialized || index < 0 || index >= levels.Length) return;
        value = Sanitize(value, DefaultFor(channel));
        if (Mathf.Approximately(levels[index], value)) return;
        levels[index] = value;
        PlayerPrefs.SetFloat(SaveKeys[index], value);
        dirty = true;
        saveAt = Time.unscaledTime + 0.35f;
        if (started) Apply(channel);
        VolumeChanged?.Invoke(channel, value);
    }

    public void RestoreAudioDefaults()
    {
        if (!initialized) return;
        for (int i = 0; i < levels.Length; i++)
        {
            var channel = (GameAudioChannel)i;
            levels[i] = DefaultFor(channel);
            PlayerPrefs.SetFloat(SaveKeys[i], levels[i]);
            if (started) Apply(channel);
            VolumeChanged?.Invoke(channel, levels[i]);
        }
        dirty = true;
        SaveNow();
    }

    public void SaveNow()
    {
        if (!dirty) return;
        PlayerPrefs.Save();
        dirty = false;
    }

    private void ApplyAll()
    {
        for (int i = 0; i < levels.Length; i++) Apply((GameAudioChannel)i);
        pendingApply = false;
    }

    private void Apply(GameAudioChannel channel)
    {
        string parameter = channel == GameAudioChannel.Master ? masterParameter :
            channel == GameAudioChannel.SFX ? sfxParameter :
            channel == GameAudioChannel.Ambience ? ambienceParameter : musicParameter;
        float value = GetVolume(channel);
        float decibels = value <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(value));
        if (mixer.SetFloat(parameter, decibels)) reportedParameters.Remove(parameter);
        else if (reportedParameters.Add(parameter))
            Debug.LogError("Audio mixer parameter '" + parameter +
                "' was not found. Expose that group's Volume and give it this exact parameter name.", this);
    }

    private float DefaultFor(GameAudioChannel channel)
    {
        float value = channel == GameAudioChannel.Master ? defaultMaster :
            channel == GameAudioChannel.SFX ? defaultSFX :
            channel == GameAudioChannel.Ambience ? defaultAmbience : defaultMusic;
        return Sanitize(value, 1f);
    }

    private static float Sanitize(float value, float fallback)
        => float.IsNaN(value) ? fallback : Mathf.Clamp01(value);

    private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    private void OnApplicationQuit() => SaveNow();
    private void OnDisable()
    {
        SaveNow();
        if (Instance == this) Instance = null;
    }
}
