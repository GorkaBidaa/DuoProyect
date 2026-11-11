using UnityEngine;

[DefaultExecutionOrder(-100)]
public class WorldResetCoordinator : MonoBehaviour
{
    public static WorldResetCoordinator Instance { get; private set; }
    [SerializeField] private StaminaSystem player;
    private bool resetting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one WorldResetCoordinator is allowed per scene.", this);
            enabled = false;
            return;
        }
        Instance = this;
        if (!player)
        {
            Debug.LogError("Assign the player StaminaSystem to WorldResetCoordinator.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (player) player.Respawned += HandleRespawn;
    }

    private void OnDisable()
    {
        if (player) player.Respawned -= HandleRespawn;
        if (Instance == this) Instance = null;
    }

    private void HandleRespawn() => ResetWorld(false);

    public void PlayerEnteredBase(StaminaSystem visitor)
    {
        if (!isActiveAndEnabled || visitor != player || visitor.IsDead || visitor.IsRespawnArrival)
            return;
        ResetWorld(true);
    }

    private void ResetWorld(bool resetResources)
    {
        if (resetting) return;
        resetting = true;
        try
        {
            // Clear temporary objects before new enemies become active. Do not call Die:
            // resetting an enemy must never award loot.
            foreach (EnemyLure lure in FindObjectsByType<EnemyLure>(FindObjectsSortMode.None)) Remove(lure.gameObject);
            foreach (LethalResinTrap trap in FindObjectsByType<LethalResinTrap>(FindObjectsSortMode.None)) Remove(trap.gameObject);
            foreach (MagnetResourcePickup drop in FindObjectsByType<MagnetResourcePickup>(FindObjectsSortMode.None)) Remove(drop.gameObject);
            foreach (EnemySpawnPoint point in FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)) point.ClearSpawn();
            foreach (EnemySpawnPoint point in FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)) point.Spawn();
            if (resetResources)
                foreach (ResourceSpawnPoint point in FindObjectsByType<ResourceSpawnPoint>(FindObjectsSortMode.None)) point.ResetResource();
        }
        finally { resetting = false; }
    }

    public static void Remove(GameObject target)
    {
        if (!target) return;
        target.SetActive(false);
        Destroy(target);
    }
}
