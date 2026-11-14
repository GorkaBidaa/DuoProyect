using UnityEngine;

public enum PlayerUpgradeKind { MaxHealth, MaxStamina, AnchorRange, ShadowDash, WallJump }

[System.Serializable]
public class PlayerUpgradeLevel
{
    [Min(0.01f)] public float amount = 1f;
    public RecipeIngredient[] costs;
}

[CreateAssetMenu(menuName = "Cursed Forest/Player Upgrade")]
public class PlayerUpgradeData : ScriptableObject
{
    public PlayerUpgradeKind kind;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public PlayerUpgradeLevel[] levels;
    public bool IsUnlock => kind == PlayerUpgradeKind.ShadowDash || kind == PlayerUpgradeKind.WallJump;
    public bool IsValid()
    {
        if (levels == null || levels.Length == 0 || (IsUnlock && levels.Length != 1)) return false;
        foreach (PlayerUpgradeLevel level in levels)
        {
            if (level == null || float.IsNaN(level.amount) || float.IsInfinity(level.amount) || level.amount <= 0f ||
                (!IsUnlock && kind == PlayerUpgradeKind.MaxHealth && level.amount != Mathf.Round(level.amount)) ||
                level.costs == null || level.costs.Length == 0) return false;
            for (int i = 0; i < level.costs.Length; i++)
            {
                var cost = level.costs[i];
                if (cost == null || !cost.resource || cost.amount <= 0) return false;
                for (int j = 0; j < i; j++) if (level.costs[j].resource == cost.resource) return false;
            }
        }
        return true;
    }
}
