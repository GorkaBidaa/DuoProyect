using UnityEngine;

public enum EnemyAnimationPhase { Locomotion, Windup, Attack, Recover, Dead }

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class EnemyAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer visuals;
    [Header("Clips: found by name when left empty")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip moveClip;
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip recoveryClip;
    [SerializeField] private AnimationClip deathClip;
    [Tooltip("Where the attack preparation ends and the swoop/lunge starts. ")]
    [SerializeField, Range(0.05f, 0.95f)] private float attackReleasePoint = 0.35f;
    [SerializeField] private bool artworkFacesRight = true;
    [SerializeField, Min(0.01f)] private float moveThreshold = 0.1f;

    private Rigidbody2D body;
    private EnemyAudio audio;
    private EnemyAnimationPhase phase;
    private float elapsed, duration, deathStarted;
    private int playingHash;
    private bool initialized, ready, sampled;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (!animator) animator = GetComponent<Animator>();
        if (!visuals) visuals = GetComponent<SpriteRenderer>();
        body = GetComponentInParent<Rigidbody2D>();
        audio = GetComponentInParent<EnemyAudio>();
        if (!animator || !animator.runtimeAnimatorController || !visuals || !body)
        {
            Debug.LogError("EnemyAnimationDriver belongs on the character's Animator/SpriteRenderer object and needs a controller and parent Rigidbody2D.", this);
            return;
        }
        bool bat = GetComponentInParent<BatEnemy>() != null;
        string prefix = bat ? "Bat" : "Mushroom";
        idleClip = idleClip ? idleClip : FindClip(prefix + (bat ? "_Fly" : "_Idle"));
        moveClip = moveClip ? moveClip : bat ? idleClip : FindClip(prefix + "_Run");
        attackClip = attackClip ? attackClip : FindClip(prefix + "_Attack");
        recoveryClip = recoveryClip ? recoveryClip : bat ? idleClip : FindClip(prefix + "_Recover");
        deathClip = deathClip ? deathClip : FindClip(prefix + "_Die");
        if (!idleClip || !moveClip || !attackClip || !recoveryClip || !deathClip)
        {
            Debug.LogError("EnemyAnimationDriver needs Idle/Fly, Move, Attack, Recover and Die clips. Assign missing clips and ensure controller state names match their clip names.", this);
            return;
        }
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ready = true;
    }

    private AnimationClip FindClip(string name)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == name) return clip;
        return null;
    }

    public void SetPhase(EnemyAnimationPhase next, float timeInPhase, float phaseDuration)
    {
        Initialize();
        if (phase == EnemyAnimationPhase.Dead) return;
        phase = next;
        elapsed = timeInPhase;
        duration = phaseDuration;
    }

    public void Face(Vector2 direction)
    {
        Initialize();
        if (visuals && Mathf.Abs(direction.x) > 0.01f)
            visuals.flipX = artworkFacesRight ? direction.x < 0f : direction.x > 0f;
    }

    // Pure timing helper: preserve the AI's warning and attack durations.
    public static float AttackSample(bool windup, float time, float length, float release)
    {
        float progress = (float)System.Math.Max(0.0, System.Math.Min(1.0, time / System.Math.Max(0.001f, length)));
        release = (float)System.Math.Max(0.05, System.Math.Min(0.95, release));
        return windup ? progress * release : release + progress * (1f - release);
    }

    private int StateHash(AnimationClip clip) => Animator.StringToHash(animator.GetLayerName(0) + "." + clip.name);

    private void LateUpdate()
    {
        if (!ready || !animator.isActiveAndEnabled || Time.timeScale <= 0f) return;
        if (phase == EnemyAnimationPhase.Dead)
        {
            Sample(deathClip, (Time.time - deathStarted) / Mathf.Max(0.01f, deathClip.length));
            return;
        }
        if (phase == EnemyAnimationPhase.Windup || phase == EnemyAnimationPhase.Attack)
        {
            Sample(attackClip, AttackSample(phase == EnemyAnimationPhase.Windup, elapsed, duration, attackReleasePoint));
            return;
        }
        if (body && Mathf.Abs(body.linearVelocity.x) > moveThreshold) Face(body.linearVelocity);
        AnimationClip clip = phase == EnemyAnimationPhase.Recover ? recoveryClip :
            body && body.linearVelocity.sqrMagnitude > moveThreshold * moveThreshold ? moveClip : idleClip;
        int hash = StateHash(clip);
        animator.speed = 1f;
        if (playingHash != hash || sampled)
        {
            playingHash = hash;
            sampled = false;
            animator.Play(hash, 0, 0f);
        }
    }

    private void Sample(AnimationClip clip, float normalizedTime)
    {
        int hash = StateHash(clip);
        animator.speed = 0f;
        animator.Play(hash, 0, Mathf.Clamp(normalizedTime, 0f, 0.9999f));
        animator.Update(0f);
        playingHash = hash;
        sampled = true;
    }

    public float PlayDeath()
    {
        Initialize();
        phase = EnemyAnimationPhase.Dead;
        deathStarted = Time.time;
        if (!ready) return 0f;
        Sample(deathClip, 0f);
        return deathClip.length + 0.06f;
    }

    // Optional Animation Events on the looping movement clips only.
    public void Wingbeat()
    {
        if (phase != EnemyAnimationPhase.Dead && phase != EnemyAnimationPhase.Windup &&
            phase != EnemyAnimationPhase.Attack && audio) audio.AnimationWingbeat();
    }

    public void Footstep()
    {
        if (phase == EnemyAnimationPhase.Locomotion && body &&
            Mathf.Abs(body.linearVelocity.x) > moveThreshold && audio) audio.Footstep();
    }

    private void OnDisable()
    {
        if (animator) animator.speed = 1f;
    }
}
