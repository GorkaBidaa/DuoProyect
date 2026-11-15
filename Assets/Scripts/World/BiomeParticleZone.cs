using System.Collections.Generic;
using UnityEngine;

public enum BiomeParticleType { Forest, Snow, None }

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class BiomeParticleZone : MonoBehaviour
{
    [SerializeField] private BiomeParticleType biome = BiomeParticleType.Snow;
    [SerializeField] private int priority = 10;
    private BoxCollider2D area;
    private static readonly List<BiomeParticleZone> zones = new List<BiomeParticleZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => zones.Clear();

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
    }

    private void OnEnable()
    {
        if (!zones.Contains(this)) zones.Add(this);
    }

    private void OnDisable() => zones.Remove(this);

    public static BiomeParticleType Resolve(Vector2 position, BiomeParticleType fallback)
    {
        BiomeParticleZone best = null;
        float bestSize = float.PositiveInfinity;
        foreach (BiomeParticleZone zone in zones)
        {
            if (!zone || !zone.isActiveAndEnabled || !zone.area || !zone.area.enabled ||
                !zone.area.OverlapPoint(position)) continue;
            float size = zone.area.bounds.size.sqrMagnitude;
            if (best == null || zone.priority > best.priority ||
                (zone.priority == best.priority && size < bestSize))
            {
                best = zone;
                bestSize = size;
            }
        }
        return best != null ? best.biome : fallback;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (!box) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = biome == BiomeParticleType.Snow ? Color.cyan :
            biome == BiomeParticleType.Forest ? Color.green : Color.gray;
        Gizmos.DrawWireCube(box.offset, new Vector3(box.size.x, box.size.y, 0.05f));
        Gizmos.matrix = previous;
    }
}
