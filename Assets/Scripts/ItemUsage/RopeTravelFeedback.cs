using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public class RopeTravelFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Material travelMaterial;
    [SerializeField, Min(0.01f)] private float whiteFlashDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.12f;
    [Header("Optional endpoint bursts")]
    [SerializeField] private ParticleSystem departureBurstPrefab;
    [SerializeField] private ParticleSystem arrivalBurstPrefab;

    private static readonly int FadeId = Shader.PropertyToID("_RopeFade");
    private static readonly int FlashId = Shader.PropertyToID("_RopeFlash");
    private PlayerMovement movement;
    private Rigidbody2D body;
    private StaminaSystem stamina;
    private ShadowAnchor anchor;
    private Coroutine routine;
    private Material originalMaterial;
    private MaterialPropertyBlock originalProperties, effectProperties;
    private RigidbodyConstraints2D originalConstraints;
    private bool captured;
    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
        stamina = GetComponent<StaminaSystem>();
        anchor = GetComponent<ShadowAnchor>();
        if (!playerSprite) playerSprite = GetComponentInChildren<SpriteRenderer>();
        originalProperties = new MaterialPropertyBlock();
        effectProperties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (stamina) stamina.Respawned += CancelTravel;
    }

    public bool TryTravel(Vector3 destination)
    {
        if (!isActiveAndEnabled || IsPlaying || !movement || !movement.enabled ||
            movement.IsTeleporting || (stamina && stamina.IsDead)) return false;
        if (!playerSprite || !travelMaterial || !travelMaterial.HasProperty(FadeId) ||
            !travelMaterial.HasProperty(FlashId))
        {
            Debug.LogError("RopeTravelFeedback needs the player's SpriteRenderer and a material using CursedForest/RopeTravelFlash. Rope travel will use the instant fallback.", this);
            return false;
        }

        if (anchor) anchor.ForceDestroyAnchor();
        movement.ResetMovementState();
        originalConstraints = body.constraints;
        originalMaterial = playerSprite.sharedMaterial;
        playerSprite.GetPropertyBlock(originalProperties);
        playerSprite.GetPropertyBlock(effectProperties);
        captured = true;
        IsPlaying = true;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        movement.SetTeleportingState(true);
        playerSprite.sharedMaterial = travelMaterial;
        routine = StartCoroutine(Travel(destination));
        return true;
    }

    private bool CanContinue => IsPlaying && isActiveAndEnabled && body && playerSprite &&
        movement && movement.enabled && !(stamina && stamina.IsDead);

    private IEnumerator Travel(Vector3 destination)
    {
        try
        {
            SpawnBurst(departureBurstPrefab);
            SetEffect(1f, 1f);
            float elapsed = 0f;
            while (elapsed < whiteFlashDuration)
            {
                if (!CanContinue) yield break;
                yield return null;
                elapsed += Time.deltaTime;
            }

            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                if (!CanContinue) yield break;
                SetEffect(1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeOutDuration)), 1f);
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (!CanContinue) yield break;
            SetEffect(0f, 0f);
            body.position = new Vector2(destination.x, destination.y);
            transform.position = destination;
            Physics2D.SyncTransforms();
            InteractionAudio.Play(InteractionCue.RopeTravel, transform.position);
            SpawnBurst(arrivalBurstPrefab);

            elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                if (!CanContinue) yield break;
                SetEffect(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInDuration)), 0f);
                yield return null;
                elapsed += Time.deltaTime;
            }
            SetEffect(1f, 0f);
        }
        finally
        {
            Restore();
            routine = null;
        }
    }

    private void SetEffect(float fade, float flash)
    {
        if (!playerSprite) return;
        effectProperties.SetFloat(FadeId, fade);
        effectProperties.SetFloat(FlashId, flash);
        playerSprite.SetPropertyBlock(effectProperties);
    }

    private void SpawnBurst(ParticleSystem prefab)
    {
        if (!prefab || !playerSprite) return;
        ParticleSystem burst = Instantiate(prefab, playerSprite.bounds.center, Quaternion.identity);
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = burst.main;
        main.loop = false;
        main.useUnscaledTime = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        burst.Play(true);
    }

    private void Update()
    {
        if (IsPlaying && !CanContinue) CancelTravel();
    }

    private void Restore()
    {
        if (!captured) return;
        captured = false;
        if (playerSprite)
        {
            playerSprite.sharedMaterial = originalMaterial;
            playerSprite.SetPropertyBlock(originalProperties.isEmpty ? null : originalProperties);
        }
        if (body) body.constraints = originalConstraints;
        if (movement)
        {
            movement.SetTeleportingState(false);
            if (movement.enabled && !(stamina && stamina.IsDead))
                movement.ResetMovementState();
            else if (body)
            {
                body.linearVelocity = Vector2.zero;
                body.gravityScale = 0f;
            }
        }
        IsPlaying = false;
    }

    public void CancelTravel()
    {
        if (routine != null)
        {
            Coroutine running = routine;
            routine = null;
            StopCoroutine(running);
        }
        Restore();
    }

    private void OnDisable()
    {
        if (stamina) stamina.Respawned -= CancelTravel;
        CancelTravel();
    }
}
