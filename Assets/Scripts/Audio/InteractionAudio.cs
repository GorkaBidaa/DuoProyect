using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum InteractionCue
{
    Food, LureThrow, LureLand, RepellantOn, RepellantOff, TrapPlaced, TrapArmed,
    TrapTriggered, RopeDeploy, RopeTravel, CraftSuccess, CraftDenied, ResourceCollect, GloomCollect
}

public class InteractionAudio : MonoBehaviour
{
    [System.Serializable]
    public class Sound
    {
        public InteractionCue cue;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.45f;
        [Min(0.05f)] public float minimumInterval = 0.12f;
        public Sound(InteractionCue cue) { this.cue = cue; }
    }
    [SerializeField] private Transform player;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField, Min(1f)] private float audibleDistance = 18f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField]
    private Sound[] sounds = {
        new Sound(InteractionCue.Food), new Sound(InteractionCue.LureThrow), new Sound(InteractionCue.LureLand),
        new Sound(InteractionCue.RepellantOn), new Sound(InteractionCue.RepellantOff),
        new Sound(InteractionCue.TrapPlaced), new Sound(InteractionCue.TrapArmed), new Sound(InteractionCue.TrapTriggered),
        new Sound(InteractionCue.RopeDeploy), new Sound(InteractionCue.RopeTravel),
        new Sound(InteractionCue.CraftSuccess), new Sound(InteractionCue.CraftDenied),
        new Sound(InteractionCue.ResourceCollect), new Sound(InteractionCue.GloomCollect)
    };
    private static InteractionAudio instance;
    private readonly AudioSource[] voices = new AudioSource[8];
    private readonly Dictionary<InteractionCue, float> nextTime = new Dictionary<InteractionCue, float>();
    private readonly Dictionary<InteractionCue, AudioSource> active = new Dictionary<InteractionCue, AudioSource>();
    private readonly Dictionary<InteractionCue, AudioClip> previous = new Dictionary<InteractionCue, AudioClip>();
    private bool paused;

    private void Awake()
    {
        if (instance && instance != this)
        { Debug.LogError("Keep one InteractionAudio per scene.", this); enabled = false; return; }
        if (!player) { Debug.LogError("Assign InteractionAudio's Player Transform.", this); enabled = false; return; }
        instance = this;
        for (int i = 0; i < voices.Length; i++)
        {
            var child = new GameObject("InteractionVoice " + i);
            child.transform.SetParent(transform, false);
            voices[i] = child.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
            voices[i].outputAudioMixerGroup = mixerGroup;
        }
    }
    private void OnEnable() { if (voices[0]) instance = this; }
    public static void Play(InteractionCue cue, Vector3 position)
    {
        if (instance && instance.isActiveAndEnabled) instance.PlayCue(cue, position);
    }
    private void PlayCue(InteractionCue cue, Vector3 position)
    {
        if (Time.timeScale <= 0f || !player || (nextTime.TryGetValue(cue, out float next) && Time.time < next)) return;
        if (paused)
        {
            foreach (AudioSource pausedVoice in voices) if (pausedVoice) pausedVoice.UnPause();
            paused = false;
        }
        if (active.TryGetValue(cue, out AudioSource playing) && playing && playing.isPlaying) return;
        Sound sound = null;
        if (sounds != null) foreach (Sound entry in sounds) if (entry != null && entry.cue == cue) { sound = entry; break; }
        if (sound == null || sound.clips == null || sound.clips.Length == 0) return;
        AudioClip clip = null;
        previous.TryGetValue(cue, out AudioClip last);
        int start = Random.Range(0, sound.clips.Length);
        for (int i = 0; i < sound.clips.Length; i++)
        {
            var candidate = sound.clips[(start + i) % sound.clips.Length];
            if (candidate && candidate != last) { clip = candidate; break; }
        }
        if (!clip) foreach (var candidate in sound.clips) if (candidate) { clip = candidate; break; }
        if (!clip) return;
        float distance = Vector2.Distance(position, player.position);
        float level = sound.volume * Mathf.Clamp01(1f - distance / Mathf.Max(1f, audibleDistance));
        if (level <= 0.001f) return;
        AudioSource voice = null;
        foreach (AudioSource candidate in voices) if (candidate && !candidate.isPlaying) { voice = candidate; break; }
        if (!voice) return;
        // A voice can be reused by another cue; remove its old association first.
        InteractionCue? stale = null;
        foreach (var entry in active) if (entry.Value == voice) { stale = entry.Key; break; }
        if (stale.HasValue) active.Remove(stale.Value);
        active[cue] = voice;
        previous[cue] = clip;
        nextTime[cue] = Time.time + Mathf.Max(0.05f, sound.minimumInterval);
        voice.clip = clip;
        voice.volume = level;
        voice.panStereo = Mathf.Clamp((position.x - player.position.x) / 10f, -0.65f, 0.65f);
        voice.pitch = Random.Range(Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y)), Mathf.Max(0.1f, Mathf.Max(pitchRange.x, pitchRange.y)));
        voice.Play();
    }
    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            if (!paused) { foreach (AudioSource voice in voices) if (voice) voice.Pause(); paused = true; }
        }
        else if (paused) { foreach (AudioSource voice in voices) if (voice) voice.UnPause(); paused = false; }
    }
    private void OnDisable()
    {
        foreach (AudioSource voice in voices) if (voice) voice.Stop();
        active.Clear(); nextTime.Clear(); paused = false;
        if (instance == this) instance = null;
    }
    private void OnDestroy() { foreach (AudioSource voice in voices) if (voice) Destroy(voice.gameObject); }
}
