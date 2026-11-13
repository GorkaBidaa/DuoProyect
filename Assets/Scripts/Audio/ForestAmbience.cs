using UnityEngine;
using UnityEngine.Audio;

public class ForestAmbience : MonoBehaviour
{
    [SerializeField] private StaminaSystem player;
    [SerializeField] private AudioClip forestLoop;
    [SerializeField] private AudioClip[] occasionalSounds;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float occasionalVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float baseVolumeMultiplier = 0.65f;
    [SerializeField] private Vector2 soundInterval = new Vector2(12f, 25f);
    [SerializeField, Min(0.05f)] private float fadeDuration = 2f;
    private AudioSource bed, accent;
    private AudioClip placeholderWind, placeholderCreak, previousClip;
    private float nextSound;
    private bool paused;

    private void Awake()
    {
        bed = MakeSource("ForestBed", true);
        accent = MakeSource("DistantForestSounds", false);
        if (!forestLoop) forestLoop = placeholderWind = ForestAudioPlaceholders.Wind();
        placeholderCreak = ForestAudioPlaceholders.Creak();
        bed.clip = forestLoop;
    }
    private AudioSource MakeSource(string name, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.volume = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }
    private void OnEnable() => ScheduleSound();
    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            if (!paused) { bed.Pause(); accent.Pause(); paused = true; }
            return;
        }
        if (paused) { bed.UnPause(); accent.UnPause(); paused = false; }
        bool safe = player && player.IsInSafeZone;
        float target = ambienceVolume * (safe ? baseVolumeMultiplier : 1f);
        bed.volume = Mathf.MoveTowards(bed.volume, target, Mathf.Max(0.01f, ambienceVolume) * Time.deltaTime / Mathf.Max(0.05f, fadeDuration));
        if (!bed.isPlaying) bed.Play();
        if (Time.time < nextSound || accent.isPlaying) return;
        ScheduleSound();
        AudioClip chosen = null;
        if (occasionalSounds != null && occasionalSounds.Length > 0)
        {
            int start = Random.Range(0, occasionalSounds.Length);
            for (int i = 0; i < occasionalSounds.Length; i++)
            {
                var candidate = occasionalSounds[(start + i) % occasionalSounds.Length];
                if (candidate && candidate != previousClip) { chosen = candidate; break; }
            }
            if (!chosen) foreach (var candidate in occasionalSounds) if (candidate) { chosen = candidate; break; }
        }
        if (!chosen) chosen = placeholderCreak;
        previousClip = chosen;
        accent.clip = chosen;
        accent.volume = occasionalVolume * (safe ? 0.35f : 1f);
        accent.panStereo = Random.Range(-0.75f, 0.75f);
        accent.pitch = Random.Range(0.95f, 1.05f);
        accent.Play();
    }
    private void ScheduleSound()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(soundInterval.x, soundInterval.y));
        nextSound = Time.time + Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(soundInterval.x, soundInterval.y)));
    }
    private void OnDisable()
    {
        paused = false;
        if (bed) { bed.Stop(); bed.volume = 0f; }
        if (accent) accent.Stop();
    }
    private void OnDestroy()
    {
        if (bed) Destroy(bed.gameObject);
        if (accent) Destroy(accent.gameObject);
        if (placeholderWind) Destroy(placeholderWind);
        if (placeholderCreak) Destroy(placeholderCreak);
    }
}
