using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class IntroTextAnimator : MonoBehaviour
{
    [Header("Letter reveal")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 32f;
    [SerializeField, Min(0f)] private float commaPause = 0.12f;
    [SerializeField, Min(0f)] private float sentencePause = 0.28f;
    [SerializeField, Min(0f)] private float lineBreakPause = 0.12f;

    [Header("Text voice")]
    [SerializeField] private bool textSoundEnabled = true;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [Tooltip("Optional short original blips. Empty uses a generated temporary tone.")]
    [SerializeField] private AudioClip[] textBlips;
    [SerializeField, Range(0f, 1f)] private float blipVolume = 0.2f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);
    [SerializeField, Min(1)] private int lettersPerBlip = 2;
    [SerializeField, Min(0.01f)] private float minimumBlipInterval = 0.05f;

    private TMP_Text body;
    private AudioSource voice;
    private AudioClip temporaryBlip;
    private readonly List<AudioClip> clips = new List<AudioClip>();
    private readonly System.Random random = new System.Random(3281);
    private int characterCount, visibleCharacters, spokenLetters, lastBlipFrame = -1;
    private float nextBlipAt;
    private bool cancelled;
    public bool IsRevealing { get; private set; }

    private void Awake()
    {
        body = GetComponent<TMP_Text>();
        var child = new GameObject("IntroTextVoice");
        child.transform.SetParent(transform, false);
        voice = child.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.loop = false;
        voice.spatialBlend = 0f;
        voice.ignoreListenerPause = true;
        voice.outputAudioMixerGroup = sfxGroup;
        if (textBlips != null)
            foreach (var clip in textBlips) if (clip) clips.Add(clip);
        if (clips.Count == 0)
        {
            temporaryBlip = CreateTemporaryBlip();
            clips.Add(temporaryBlip);
        }
        if (!sfxGroup)
            Debug.LogWarning("IntroTextAnimator: assign GameAudio's SFX mixer group so the audio settings control text blips.", this);
    }

    public void PreparePage(string page)
    {
        CancelReveal();
        cancelled = false;
        body.text = page ?? "";
        body.maxVisibleCharacters = 0;
        // Parse the full page first; reveal glyphs without changing wrapping or showing rich-text tags.
        body.ForceMeshUpdate();
        characterCount = body.textInfo.characterCount;
        visibleCharacters = spokenLetters = 0;
        nextBlipAt = 0f;
        lastBlipFrame = -1;
    }

    public IEnumerator RevealPreparedPage()
    {
        cancelled = false;
        IsRevealing = characterCount > 0;
        float revealAt = Time.unscaledTime;
        while (IsRevealing && isActiveAndEnabled && visibleCharacters < characterCount)
        {
            float now = Time.unscaledTime;
            while (IsRevealing && visibleCharacters < characterCount && now >= revealAt)
            {
                char character = body.textInfo.characterInfo[visibleCharacters].character;
                char next = visibleCharacters + 1 < characterCount
                    ? body.textInfo.characterInfo[visibleCharacters + 1].character : '\0';
                visibleCharacters++;
                body.maxVisibleCharacters = visibleCharacters;
                if (char.IsLetterOrDigit(character))
                {
                    spokenLetters++;
                    if ((spokenLetters - 1) % Mathf.Max(1, lettersPerBlip) == 0) TryBlip();
                }
                float delay = 1f / Mathf.Max(1f, charactersPerSecond);
                if (character == '\n' || character == '\r') delay += Mathf.Max(0f, lineBreakPause);
                else if (character == ',' || character == ';' || character == ':') delay += Mathf.Max(0f, commaPause);
                else if ((character == '.' && next != '.') || character == '!' || character == '?')
                    delay += Mathf.Max(0f, sentencePause);
                revealAt += delay;
            }
            if (visibleCharacters >= characterCount) break;
            yield return null;
        }
        IsRevealing = false;
        if (!cancelled && body) body.maxVisibleCharacters = characterCount;
    }

    public void RevealAll()
    {
        IsRevealing = false;
        visibleCharacters = characterCount;
        if (body) body.maxVisibleCharacters = characterCount;
        if (voice) voice.Stop();
    }

    public void CancelReveal()
    {
        cancelled = true;
        IsRevealing = false;
        if (voice) voice.Stop();
    }

    private void TryBlip()
    {
        if (!textSoundEnabled || !voice || clips.Count == 0 || voice.isPlaying ||
            Time.frameCount == lastBlipFrame || Time.unscaledTime < nextBlipAt) return;
        lastBlipFrame = Time.frameCount;
        nextBlipAt = Time.unscaledTime + Mathf.Max(0.01f, minimumBlipInterval);
        voice.clip = clips[random.Next(clips.Count)];
        float low = Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.25f, 3f);
        float high = Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), low, 3f);
        voice.pitch = Mathf.Lerp(low, high, (float)random.NextDouble());
        voice.volume = Mathf.Clamp01(blipVolume);
        // One voice, no PlayOneShot stack, and no sound for spaces/punctuation.
        voice.Play();
    }

    private static AudioClip CreateTemporaryBlip()
    {
        const int rate = 22050;
        const float duration = 0.035f;
        var samples = new float[(int)(rate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float attack = Mathf.Min(1f, t / 0.002f);
            float release = Mathf.Clamp01((duration - t) / 0.012f);
            samples[i] = attack * release *
                (0.4f * Mathf.Sin(2f * Mathf.PI * 300f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 900f * t));
        }
        var clip = AudioClip.Create("TemporaryIntroTextBlip", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        CancelReveal();
        if (body) body.maxVisibleCharacters = int.MaxValue;
    }

    private void OnDestroy()
    {
        if (voice) Destroy(voice.gameObject);
        if (temporaryBlip) Destroy(temporaryBlip);
    }
}
