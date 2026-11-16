using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(-140)]
public class SpecialKeyGate : MonoBehaviour, IInteractionPromptSource
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ItemData specialKey;
    [SerializeField] private TutorialCompletion tutorialCompletion;
    [Tooltip("Solid barriers only. Keep the separate interaction trigger out of this array.")]
    [SerializeField] private Collider2D[] blockingColliders;
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private UnityEvent onUnlocked = new UnityEvent();

    [Header("Opening feedback")]
    [SerializeField, Min(0f)] private float openingDuration = 0.75f;
    [SerializeField] private Vector3 openingOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool fadeClosedVisual = true;
    [SerializeField] private ParticleSystem openingVfxPrefab;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioClip openingClip;
    [SerializeField] private bool useTemporarySound = true;
    [SerializeField, Range(0f, 1f)] private float openingVolume = 0.6f;

    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private StaminaSystem stamina;
    private bool unlocked, unlocking, openingStarted;
    private SpriteRenderer[] closedSprites;
    private Color[] closedColors;
    private bool[] originalBarrierStates;
    private Vector3 closedPosition;
    private AudioSource voice;
    private AudioClip temporaryClip;
    public bool IsUnlocked => unlocked;
    public bool PromptAvailable => isActiveAndEnabled && !unlocked && !unlocking && occupants.Count > 0 &&
        playerInventory && playerInventory.isActiveAndEnabled && stamina && !stamina.IsDead &&
        tutorialCompletion && !tutorialCompletion.IsCompleted;
    public Vector3 PromptPosition => transform.position;
    public int PromptPriority => 200;
    public string PromptAction => "Interact";
    public string PromptLabel => playerInventory && specialKey && playerInventory.GetItemAmount(specialKey) > 0
        ? "Unlock Gate" : "Special Key required";

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;
    private void Awake()
    {
        Collider2D interactionTrigger = GetComponent<Collider2D>();
        stamina = playerInventory ? playerInventory.GetComponent<StaminaSystem>() : null;
        bool valid = playerInventory && stamina && specialKey && tutorialCompletion &&
            interactionTrigger.isTrigger && blockingColliders != null && blockingColliders.Length > 0;
        if (valid)
            foreach (Collider2D collider in blockingColliders)
                if (!collider || collider == interactionTrigger || collider.isTrigger) { valid = false; break; }
        if ((closedVisual && transform.IsChildOf(closedVisual.transform)) ||
            (openVisual && transform.IsChildOf(openVisual.transform)) ||
            (closedVisual && openVisual && (closedVisual.transform.IsChildOf(openVisual.transform) ||
                openVisual.transform.IsChildOf(closedVisual.transform)))) valid = false;
        if (!valid)
        {
            Debug.LogError("Gate needs player inventory, key asset, TutorialCompletion, a trigger and separate solid barriers. Closed/Open visuals must be separate gate children.", this);
            enabled = false;
            return;
        }
        originalBarrierStates = new bool[blockingColliders.Length];
        for (int i = 0; i < blockingColliders.Length; i++) originalBarrierStates[i] = blockingColliders[i].enabled;
        if (closedVisual)
        {
            closedPosition = closedVisual.transform.localPosition;
            closedSprites = closedVisual.GetComponentsInChildren<SpriteRenderer>(true);
            closedColors = new Color[closedSprites.Length];
            for (int i = 0; i < closedSprites.Length; i++) closedColors[i] = closedSprites[i].color;
        }
        if (openVisual) openVisual.SetActive(false);
        var child = new GameObject("GateOpeningVoice");
        child.transform.SetParent(transform, false);
        voice = child.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.ignoreListenerPause = true;
        voice.outputAudioMixerGroup = sfxGroup;
        if (!openingClip && useTemporarySound) temporaryClip = TutorialFeedbackAudio.CreateGateSound();
    }
    private void OnEnable() => InteractionPromptRegistry.Register(this);
    private void OnDisable()
    {
        InteractionPromptRegistry.Unregister(this);
        occupants.Clear();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerInventory>() == playerInventory) occupants.Add(other);
    }
    private void OnTriggerExit2D(Collider2D other) => occupants.Remove(other);
    private void Update()
    {
        occupants.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy);
        if (!PromptAvailable) return;
        GameInput input = GameInput.Instance;
        if (!input || !input.PromptsAllowed ||
            !ReferenceEquals(InteractionPromptRegistry.Select(playerInventory.transform.position), this)) return;
        if (input.TryInteract()) TryUnlock();
    }
    public bool TryUnlock()
    {
        GameInput input = GameInput.Instance;
        if (!PromptAvailable || !input || !input.PromptsAllowed) return false;
        if (playerInventory.GetItemAmount(specialKey) < 1)
        {
            Debug.Log("Craft the Special Key at a crafting station to open this gate.", this);
            return false;
        }
        if (!tutorialCompletion.CanComplete)
        {
            Debug.LogError("TutorialCompletion is not ready. Assign its Player, completion panel and Main Menu button before testing the gate. The key was not consumed.", this);
            return false;
        }
        unlocking = true;
        if (!playerInventory.RemoveItem(specialKey, 1)) { unlocking = false; return false; }
        unlocked = true;
        foreach (Collider2D collider in blockingColliders) if (collider) collider.enabled = false;
        input.BlockThisFrame();
        if (!tutorialCompletion.Complete(this))
        {
            // A listener may have changed the player/UI state during inventory notification.
            unlocked = false;
            for (int i = 0; i < blockingColliders.Length; i++)
                if (blockingColliders[i]) blockingColliders[i].enabled = originalBarrierStates[i];
            playerInventory.AddItem(specialKey, 1);
            unlocking = false;
            return false;
        }
        unlocking = false;
        onUnlocked.Invoke();
        return true;
    }

    public IEnumerator PlayOpeningFeedback(TutorialCompletion owner)
    {
        if (openingStarted || !unlocked) yield break;
        openingStarted = true;
        bool prefabHasAudio = false;
        if (openingVfxPrefab)
        {
            var effect = Instantiate(openingVfxPrefab, transform.position, Quaternion.identity);
            owner.TrackGateEffect(effect.gameObject);
            foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.useUnscaledTime = true;
                main.loop = false;
            }
            foreach (var audio in effect.GetComponentsInChildren<AudioSource>(true))
            {
                audio.ignoreListenerPause = true;
                if (audio.playOnAwake && audio.clip)
                {
                    prefabHasAudio = true;
                    if (!audio.isPlaying) audio.Play();
                }
            }
            effect.Play(true);
            owner.StartCoroutine(CleanupEffect(effect));
        }
        AudioClip clip = openingClip ? openingClip : (!prefabHasAudio ? temporaryClip : null);
        if (voice && clip)
        {
            voice.clip = clip;
            voice.volume = openingVolume;
            voice.Play();
        }
        float elapsed = 0f;
        float duration = Mathf.Max(0f, openingDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            if (closedVisual)
            {
                closedVisual.transform.localPosition = closedPosition + openingOffset * eased;
                if (fadeClosedVisual && closedSprites != null)
                    for (int i = 0; i < closedSprites.Length; i++)
                        if (closedSprites[i])
                        {
                            Color color = closedColors[i];
                            color.a *= 1f - eased;
                            closedSprites[i].color = color;
                        }
            }
            yield return null;
        }
        FinishOpeningVisuals();
    }

    public void FinishOpeningVisuals()
    {
        if (closedVisual) closedVisual.SetActive(false);
        if (openVisual) openVisual.SetActive(true);
    }

    private static IEnumerator CleanupEffect(ParticleSystem effect)
    {
        float deadline = Time.unscaledTime + 15f;
        while (effect && Time.unscaledTime < deadline)
        {
            bool alive = effect.IsAlive(true);
            foreach (var audio in effect.GetComponentsInChildren<AudioSource>(true))
                if (audio.isPlaying) alive = true;
            if (!alive) break;
            yield return null;
        }
        if (effect) Destroy(effect.gameObject);
    }

    private void OnDestroy()
    {
        if (voice) Destroy(voice.gameObject);
        if (temporaryClip) Destroy(temporaryClip);
    }
}
