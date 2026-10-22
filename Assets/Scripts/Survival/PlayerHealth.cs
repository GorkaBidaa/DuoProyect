using UnityEngine;

[RequireComponent(typeof(StaminaSystem))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private PlayerHUD hud;

    private int currentHealth;
    private StaminaSystem stamina;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        stamina = GetComponent<StaminaSystem>();
    }

    private void Start()
    {
        RefreshHUD();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        RefreshHUD();

        if (currentHealth == 0)
            stamina.Die();
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