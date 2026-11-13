using UnityEngine;
using UnityEngine.Audio;

public class EnemyAudio : MonoBehaviour
{
    [SerializeField] private AudioClip[] detectionClips;
    [SerializeField] private AudioClip[] windupClips;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private AudioClip[] deathClips;
    [Header("Optional bat wings")]
    [SerializeField] private AudioClip[] wingClips;
    [SerializeField] private Vector2 wingInterval = new Vector2(0.45f, 0.65f);
    [SerializeField, Range(0f, 1f)] private float wingVolume = 0.15f;
    [Header("Mix")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private Transform listener;
    [SerializeField, Min(1f)] private float audibleDistance = 18f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    private AudioSource cueSource, wingSource;
    private AudioClip lastClip;
    private float nextWing;
    private bool quietWings, paused;

    public void SetListener(Transform target) => listener = target;
    private void Awake()
    {
        cueSource = MakeSource("EnemyCues");
        wingSource = MakeSource("EnemyWings");
    }
    private AudioSource MakeSource(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }
    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            if (!paused) { cueSource.Pause(); wingSource.Pause(); paused = true; }
            return;
        }
        if (paused) { cueSource.UnPause(); wingSource.UnPause(); paused = false; }
        UpdateMix(cueSource, volume);
        UpdateMix(wingSource, wingVolume);
        if (quietWings || Time.time < nextWing || wingSource.isPlaying) return;
        float min = Mathf.Max(0.1f, Mathf.Min(wingInterval.x, wingInterval.y));
        nextWing = Time.time + Random.Range(min, Mathf.Max(min, Mathf.Max(wingInterval.x, wingInterval.y)));
        Play(wingSource, Pick(wingClips), wingVolume);
    }
    private void UpdateMix(AudioSource source, float level)
    {
        float distance = listener ? Vector2.Distance(transform.position, listener.position) : 0f;
        source.volume = level * Mathf.Clamp01(1f - distance / Mathf.Max(1f, audibleDistance));
        source.panStereo = listener ? Mathf.Clamp((transform.position.x - listener.position.x) / 10f, -0.75f, 0.75f) : 0f;
    }
    private AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip && clip != lastClip) { lastClip = clip; return clip; }
        }
        foreach (AudioClip clip in clips) if (clip) { lastClip = clip; return clip; }
        return null;
    }
    private void Play(AudioSource source, AudioClip clip, float level)
    {
        if (!clip || !source || Time.timeScale <= 0f) return;
        UpdateMix(source, level);
        source.pitch = Random.Range(Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y)), Mathf.Max(0.1f, Mathf.Max(pitchRange.x, pitchRange.y)));
        source.clip = clip;
        source.Play();
    }
    public void Detection() => Play(cueSource, Pick(detectionClips), volume);
    public void Windup() => Play(cueSource, Pick(windupClips), volume);
    public void Attack() => Play(cueSource, Pick(attackClips), volume);
    public void Hit() => Play(cueSource, Pick(hitClips), volume);
    public void StopCue() { if (cueSource) cueSource.Stop(); }
    public void SetQuietWings(bool value)
    {
        quietWings = value;
        if (value && wingSource) wingSource.Stop();
    }
    public void Death()
    {
        AudioClip clip = Pick(deathClips);
        if (!clip) return;
        var tail = new GameObject("EnemyDeathSound");
        tail.transform.position = transform.position;
        var source = tail.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        UpdateMix(source, volume);
        source.clip = clip;
        source.pitch = Random.Range(0.95f, 1.05f);
        source.Play();
        tail.AddComponent<EnemyAudioTail>();
    }
    private void OnDisable()
    {
        if (cueSource) cueSource.Stop();
        if (wingSource) wingSource.Stop();
        quietWings = paused = false;
        nextWing = Time.time + 0.2f;
    }
    private void OnDestroy()
    {
        if (cueSource) Destroy(cueSource.gameObject);
        if (wingSource) Destroy(wingSource.gameObject);
    }
}
