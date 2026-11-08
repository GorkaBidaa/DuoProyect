using UnityEngine;

[RequireComponent(typeof(StaminaSystem))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private PlayerHUD hud;
    [SerializeField] private CursedForestHud forestHud;


    [Header("Hit response")]
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.85f;
    [SerializeField] private Vector2 knockback = new Vector2(5f, 3f);
    [SerializeField, Min(0f)] private float hitStunDuration = 0.16f;
    [SerializeField] private PlayerHitFeedback hitFeedback;
    private float invulnerableUntil;
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    private int currentHealth;
    private StaminaSystem stamina;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        stamina = GetComponent<StaminaSystem>();
        if (hitFeedback == null) hitFeedback = GetComponent<PlayerHitFeedback>();
    }

    private void Start()
    {
        RefreshHUD();
    }

    public void TakeDamage(int amount) => TryTakeHit(amount, Vector2.zero);

    // A zero direction applies damage/feedback without knockback (e.g. a test or hazard).
    public bool TryTakeHit(int amount, Vector2 attackDirection)
    {
        if (amount <= 0 || currentHealth <= 0 || IsInvulnerable ||
            (stamina != null && (stamina.isImmortal || stamina.IsDead)))
            return false;

        invulnerableUntil = Time.time + invulnerabilityDuration;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        RefreshHUD();
        if (hitFeedback != null) hitFeedback.Play(invulnerabilityDuration);
        if (currentHealth == 0)
        {
            if (stamina != null) stamina.Die();
        }
        else if (attackDirection.sqrMagnitude > 0.001f)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            float sign = Mathf.Abs(attackDirection.x) > 0.01f ? Mathf.Sign(attackDirection.x) : 0f;
            if (movement != null)
                movement.ApplyHitKnockback(new Vector2(sign * knockback.x, knockback.y), hitStunDuration);
        }
        return true;
    }

    public bool TryRestoreHealth(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 ||
            currentHealth >= maxHealth)
            return false;

        currentHealth += Mathf.Min(amount, maxHealth - currentHealth);
        RefreshHUD();
        return true;
    }

    public void ResetAfterRespawn()
    {
        currentHealth = maxHealth;
        invulnerableUntil = 0f;
        if (hitFeedback != null) hitFeedback.Clear();
        RefreshHUD();
    }

    private void RefreshHUD()
    {
        if (hud != null)
            hud.SetHealth(currentHealth, maxHealth);

    }

    [ContextMenu("Test/Take 25 Damage")]
    private void TestDamage()
    {
        if (Application.isPlaying)
            TakeDamage(25);
    }
}