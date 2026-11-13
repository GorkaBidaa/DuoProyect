using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D), typeof(Collider2D))]
public class SurfaceFootsteps : MonoBehaviour
{
    [System.Serializable]
    public class MaterialSounds
    {
        public PhysicsMaterial2D material;
        public AudioClip[] clips;
    }
    [SerializeField] private MaterialSounds[] materialSounds;
    [SerializeField] private AudioClip[] fallbackClips;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.93f, 1.07f);
    [SerializeField, Min(0f)] private float minimumSpeed = 0.3f;
    private PlayerMovement movement;
    private StaminaSystem stamina;
    private Rigidbody2D body;
    private Collider2D shape;
    private AudioSource source;
    private AudioClip placeholder, previousClip;
    [SerializeField, Min(0.08f)] private float minimumStepInterval = 0.1f;
    private float nextStepTime;
    private bool paused;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stamina = GetComponent<StaminaSystem>();
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<Collider2D>();
        var audioObject = new GameObject("FootstepAudio");
        audioObject.transform.SetParent(transform, false);
        source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = mixerGroup;
        placeholder = ForestAudioPlaceholders.Step();
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f)
        {
            if (!paused) { source.Pause(); paused = true; }
            return;
        }
        if (paused) { source.UnPause(); paused = false; }
    }

    // Add this Animation Event on each grounded foot-contact frame of Player_Walk.
    public void Footstep()
    {
        if (!isActiveAndEnabled || !source || Time.timeScale <= 0f || paused ||
            Time.time < nextStepTime || !movement.enabled || !movement.isGrounded ||
            movement.IsTeleporting || Mathf.Abs(movement.HorizontalInput) < 0.1f ||
            (stamina && stamina.IsDead) || (GameInput.Instance && GameInput.Instance.GameplayBlocked))
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movement.groundLayer);
        filter.useTriggers = false;
        int count = shape.Cast(Vector2.down, filter, groundHits, 0.18f, true);
        Collider2D ground = null;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (groundHits[i].collider && groundHits[i].normal.y >= 0.4f && groundHits[i].distance < nearest)
            { ground = groundHits[i].collider; nearest = groundHits[i].distance; }
        if (!ground) return;

        MovingPlatform platform = ground.GetComponentInParent<MovingPlatform>();
        Vector2 supportVelocity = platform ? platform.Velocity : ground.attachedRigidbody ? ground.attachedRigidbody.linearVelocity : Vector2.zero;
        if (Mathf.Abs(body.linearVelocity.x - supportVelocity.x) < minimumSpeed) return;
        nextStepTime = Time.time + Mathf.Max(0.08f, minimumStepInterval);
        // One source/voice: the new contact replaces the previous tail instead of layering it.
        PlayStep(ground);
    }

    private void PlayStep(Collider2D ground)
    {
        AudioClip[] clips = null;
        FootstepSurface surface = ground.GetComponentInParent<FootstepSurface>();
        if (surface) clips = surface.footsteps;
        if (!HasClip(clips))
        {
            PhysicsMaterial2D material = ground.sharedMaterial;
            if (!material && ground.attachedRigidbody) material = ground.attachedRigidbody.sharedMaterial;
            if (materialSounds != null)
                foreach (MaterialSounds entry in materialSounds)
                    if (entry != null && entry.material == material && HasClip(entry.clips)) { clips = entry.clips; break; }
        }
        if (!HasClip(clips)) clips = fallbackClips;
        AudioClip clip = Pick(clips);
        if (!clip) clip = placeholder;
        previousClip = clip;
        source.pitch = Random.Range(Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y)), Mathf.Max(0.1f, Mathf.Max(pitchRange.x, pitchRange.y)));
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private AudioClip Pick(AudioClip[] clips)
    {
        if (!HasClip(clips)) return null;
        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip && clip != previousClip) return clip;
        }
        foreach (AudioClip clip in clips) if (clip) return clip;
        return null;
    }
    private static bool HasClip(AudioClip[] clips)
    {
        if (clips != null) foreach (AudioClip clip in clips) if (clip) return true;
        return false;
    }
    private void OnDisable()
    {
        paused = false;
        if (source) source.Stop();
    }
    private void OnDestroy()
    {
        if (source) Destroy(source.gameObject);
        if (placeholder) Destroy(placeholder);
    }
}

