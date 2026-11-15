using UnityEngine;
using UnityEngine.Audio;

public sealed class MainMenuAudio : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip ambienceClip;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.25f;
    [SerializeField, Min(0.01f)] private float startupFadeDuration = 1.5f;
    private AudioSource music, ambience;
    private float gain, targetGain = 1f, fadeDuration;
    private bool started;

    private void Awake()
    {
        music = CreateSource("MenuMusic", musicClip, musicGroup);
        ambience = CreateSource("MenuAmbience", ambienceClip, ambienceGroup);
        fadeDuration = startupFadeDuration;
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip, AudioMixerGroup group)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;
        source.clip = clip;
        source.outputAudioMixerGroup = group;
        return source;
    }

    private void Start() { started = true; FadeIn(startupFadeDuration); }
    private void OnEnable() { if (started) FadeIn(startupFadeDuration); }

    public void FadeIn(float seconds)
    {
        targetGain = 1f;
        fadeDuration = Mathf.Max(0.01f, seconds);
        if (music && music.clip && !music.isPlaying) music.Play();
        if (ambience && ambience.clip && !ambience.isPlaying) ambience.Play();
    }

    public void FadeOut(float seconds)
    {
        targetGain = 0f;
        fadeDuration = Mathf.Max(0.01f, seconds);
    }

    private void Update()
    {
        gain = Mathf.MoveTowards(gain, targetGain, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
        if (music) music.volume = musicVolume * gain;
        if (ambience) ambience.volume = ambienceVolume * gain;
    }

    private void OnDisable()
    {
        if (music) music.Stop();
        if (ambience) ambience.Stop();
        gain = 0f;
    }
    private void OnDestroy()
    {
        if (music) Destroy(music.gameObject);
        if (ambience) Destroy(ambience.gameObject);
    }
}
