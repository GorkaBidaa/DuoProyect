using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlayerInventory), typeof(PlayerHealth), typeof(StaminaSystem))]
public class PlayerUpgrades : MonoBehaviour
{
    [SerializeField] private PlayerUpgradeData[] upgrades;
    [SerializeField] private bool onlyAtBase = true;
    [SerializeField] private bool saveBetweenSessions = true;
    [SerializeField] private string savePrefix = "DuoProyect.Upgrades.v1";
    [Header("Inspector test purchase")]
    [SerializeField] private PlayerUpgradeKind testUpgrade;
    private PlayerInventory inventory;
    private PlayerHealth health;
    private StaminaSystem stamina;
    private ShadowAnchor anchor;
    private PlayerMovement movement;
    private readonly Dictionary<PlayerUpgradeKind, PlayerUpgradeData> catalog = new Dictionary<PlayerUpgradeKind, PlayerUpgradeData>();
    private readonly Dictionary<PlayerUpgradeKind, int> purchased = new Dictionary<PlayerUpgradeKind, int>();
    private bool ready, purchasing;
    public event Action UpgradesChanged;
    public IReadOnlyList<PlayerUpgradeData> Catalog => upgrades;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        health = GetComponent<PlayerHealth>();
        stamina = GetComponent<StaminaSystem>();
        anchor = GetComponent<ShadowAnchor>();
        movement = GetComponent<PlayerMovement>();
    }
    private void Start()
    {
        if (!anchor || !movement || upgrades == null || upgrades.Length == 0)
        { Debug.LogError("PlayerUpgrades needs movement, ShadowAnchor and its five upgrade assets.", this); enabled = false; return; }
        foreach (PlayerUpgradeData data in upgrades)
        {
            if (!data || !data.IsValid() || catalog.ContainsKey(data.kind))
            { Debug.LogError("Each upgrade must be valid, with one asset per kind and positive unique resource costs.", this); enabled = false; return; }
            catalog.Add(data.kind, data);
        }
        if (catalog.Count != 5)
        { Debug.LogError("Assign all five upgrade kinds to PlayerUpgrades.", this); enabled = false; return; }
        // All other components have initialized before bonuses are loaded.
        foreach (var pair in catalog)
        {
            int level = saveBetweenSessions ? Mathf.Clamp(PlayerPrefs.GetInt(Key(pair.Key), 0), 0, pair.Value.levels.Length) : 0;
            purchased[pair.Key] = level;
            for (int i = 0; i < level; i++) Apply(pair.Value, pair.Value.levels[i].amount);
        }
        ready = true;
        UpgradesChanged?.Invoke();
    }
    private string Key(PlayerUpgradeKind kind) => savePrefix + "." + kind;

    public int GetLevel(PlayerUpgradeKind kind)
    {
        // An ability already granted by Inspector or another pickup cannot be bought again.
        if (kind == PlayerUpgradeKind.ShadowDash && anchor && anchor.ShadowDashUnlocked) return 1;
        if (kind == PlayerUpgradeKind.WallJump && movement && movement.WallJumpUnlocked) return 1;
        return purchased.TryGetValue(kind, out int level) ? level : 0;
    }
    public PlayerUpgradeLevel GetNextLevel(PlayerUpgradeKind kind)
    {
        if (!catalog.TryGetValue(kind, out var data)) return null;
        int level = GetLevel(kind);
        return level < data.levels.Length ? data.levels[level] : null;
    }
    public bool CanPurchase(PlayerUpgradeKind kind, out string reason)
    {
        reason = "";
        if (!ready || !isActiveAndEnabled || purchasing) { reason = "Upgrades are not available."; return false; }
        if (stamina.IsDead || health.CurrentHealth <= 0 || Time.timeScale <= 0f) { reason = "Player must be alive and gameplay active."; return false; }
        if (onlyAtBase && !stamina.IsInSafeZone) { reason = "Return to base to upgrade."; return false; }
        if (!catalog.TryGetValue(kind, out var data) || !data.IsValid()) { reason = "Upgrade is not configured."; return false; }
        PlayerUpgradeLevel next = GetNextLevel(kind);
        if (next == null) { reason = "Already unlocked or at maximum level."; return false; }
        foreach (var cost in next.costs)
            if (inventory.GetResourceAmount(cost.resource) < cost.amount)
            { reason = "Not enough " + cost.resource.resourceName + "."; return false; }
        return true;
    }
    public bool Purchase(PlayerUpgradeKind kind)
    {
        if (!CanPurchase(kind, out string reason)) { Debug.Log(reason, this); return false; }
        purchasing = true;
        try
        {
            var next = GetNextLevel(kind);
            if (!inventory.TrySpendResources(next.costs)) return false;
            int newLevel = GetLevel(kind) + 1;
            Apply(catalog[kind], next.amount);
            purchased[kind] = newLevel;
            if (saveBetweenSessions) { PlayerPrefs.SetInt(Key(kind), newLevel); PlayerPrefs.Save(); }
            UpgradesChanged?.Invoke();
            Debug.Log(catalog[kind].upgradeName + " upgraded to level " + newLevel + ".", this);
            return true;
        }
        finally { purchasing = false; }
    }
    private void Apply(PlayerUpgradeData data, float amount)
    {
        switch (data.kind)
        {
            case PlayerUpgradeKind.MaxHealth: health.SetMaxHealth(health.MaxHealth + Mathf.RoundToInt(amount)); break;
            case PlayerUpgradeKind.MaxStamina: stamina.SetMaxStamina(stamina.maxStamina + amount); break;
            case PlayerUpgradeKind.AnchorRange:
                float margin = Mathf.Max(0f, anchor.RadiusForgive - anchor.maxTeleportRadius);
                anchor.maxTeleportRadius += amount;
                anchor.RadiusForgive = anchor.maxTeleportRadius + margin;
                break;
            case PlayerUpgradeKind.ShadowDash: anchor.UnlockShadowDash(); break;
            case PlayerUpgradeKind.WallJump: movement.UnlockWallJump(); break;
        }
    }
    [ContextMenu("Test/Purchase Selected Upgrade")]
    private void TestPurchase() { if (Application.isPlaying) Purchase(testUpgrade); }
    [ContextMenu("Test/Print Upgrade Levels")]
    private void PrintLevels()
    {
        if (!Application.isPlaying) return;
        foreach (var pair in catalog) Debug.Log(pair.Value.upgradeName + ": " + GetLevel(pair.Key) + "/" + pair.Value.levels.Length, this);
    }
    [ContextMenu("Test/Clear Saved Upgrades For Next Session")]
    private void ClearSaved()
    {
        foreach (PlayerUpgradeKind kind in Enum.GetValues(typeof(PlayerUpgradeKind))) PlayerPrefs.DeleteKey(Key(kind));
        PlayerPrefs.Save();
        Debug.Log("Saved upgrade levels cleared. Active stats stay unchanged; restart Play Mode for a fresh test.", this);
    }
}
