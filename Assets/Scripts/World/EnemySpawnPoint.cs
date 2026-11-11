using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform patrolCenter;
    [SerializeField] private bool overridePatrolArea;
    [SerializeField] private Vector2 patrolSize = new Vector2(10f, 6f);
    private GameObject current;

    private void Start()
    {
        if (!current) Spawn();
    }

    public void ClearSpawn()
    {
        if (current) WorldResetCoordinator.Remove(current);
        current = null;
    }

    public void Spawn()
    {
        if (current || !isActiveAndEnabled) return;
        if (!enemyPrefab || !player ||
            (enemyPrefab.GetComponent<BatEnemy>() == null) == (enemyPrefab.GetComponent<WolfEnemy>() == null))
        {
            Debug.LogError("EnemySpawnPoint needs the player and a prefab with exactly one BatEnemy or WolfEnemy on its root.", this);
            return;
        }
        // An inactive parent defers Awake so references are assigned BEFORE validation.
        GameObject staging = new GameObject("Enemy spawn staging");
        staging.SetActive(false);
        current = Instantiate(enemyPrefab, transform.position, transform.rotation, staging.transform);
        Transform center = patrolCenter ? patrolCenter : transform;
        BatEnemy bat = current.GetComponent<BatEnemy>();
        WolfEnemy wolf = current.GetComponent<WolfEnemy>();
        if (bat) bat.ConfigureSpawn(player, center, overridePatrolArea, patrolSize);
        if (wolf) wolf.ConfigureSpawn(player, center, overridePatrolArea, patrolSize);
        current.SetActive(true);
        current.transform.SetParent(null, true);
        Destroy(staging);
    }

    private void OnDisable() => ClearSpawn();
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (overridePatrolArea) Gizmos.DrawWireCube(patrolCenter ? patrolCenter.position : transform.position, patrolSize);
    }
}
