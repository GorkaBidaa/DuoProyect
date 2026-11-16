using System.Collections.Generic;
using UnityEngine;

public enum BiomeParticleType { Forest, Snow, None }

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class BiomeParticleZone : MonoBehaviour
{
    [SerializeField] private BiomeParticleType biome = BiomeParticleType.Snow;
    [SerializeField] private int priority = 10;

    [Header("Progressive Transition (World Units)")]
    [Tooltip("Distance from the bottom/edges of the box over which particles gradually blend from the previous biome.")]
    [SerializeField, Min(0f)] private float blendDistance = 8f;
    [Tooltip("If enabled, blends progressively from bottom to top (ideal for vertical shafts between Forest and Snow).")]
    [SerializeField] private bool verticalBottomToTopBlend = true;

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
        ResolveBlend(position, fallback, out float forestWeight, out float snowWeight);
        if (forestWeight <= 0.01f && snowWeight <= 0.01f) return BiomeParticleType.None;
        return snowWeight >= forestWeight ? BiomeParticleType.Snow : BiomeParticleType.Forest;
    }

    // Calculates continuous weights (0..1) for Forest and Snow based on how deep the player is inside the zone
    public static void ResolveBlend(Vector2 position, BiomeParticleType fallback, out float forestWeight, out float snowWeight)
    {
        forestWeight = fallback == BiomeParticleType.Forest ? 1f : 0f;
        snowWeight = fallback == BiomeParticleType.Snow ? 1f : 0f;

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

        if (best == null) return;

        float targetForest = best.biome == BiomeParticleType.Forest ? 1f : 0f;
        float targetSnow = best.biome == BiomeParticleType.Snow ? 1f : 0f;

        float factor = best.CalculateBlendFactor(position);
        forestWeight = Mathf.Lerp(forestWeight, targetForest, factor);
        snowWeight = Mathf.Lerp(snowWeight, targetSnow, factor);
    }

    private float CalculateBlendFactor(Vector2 position)
    {
        if (blendDistance <= 0.01f || !area) return 1f;

        Bounds b = area.bounds;
        if (verticalBottomToTopBlend)
        {
            // 0 at the bottom edge of the zone, reaching 1 after climbing 'blendDistance' units
            float distFromBottom = position.y - b.min.y;
            return Mathf.Clamp01(distFromBottom / blendDistance);
        }

        float dx = Mathf.Min(position.x - b.min.x, b.max.x - position.x);
        float dy = Mathf.Min(position.y - b.min.y, b.max.y - position.y);
        float distFromEdge = Mathf.Min(dx, dy);
        return Mathf.Clamp01(distFromEdge / blendDistance);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (!box) return;

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Color mainColor = biome == BiomeParticleType.Snow ? Color.cyan :
            biome == BiomeParticleType.Forest ? Color.green : Color.gray;
        Gizmos.color = mainColor;
        Gizmos.DrawWireCube(box.offset, new Vector3(box.size.x, box.size.y, 0.05f));

        // Draw the progressive transition threshold line in the Scene view
        if (blendDistance > 0.01f && verticalBottomToTopBlend && blendDistance < box.size.y)
        {
            Gizmos.color = Color.yellow;
            float localBlendY = box.offset.y - (box.size.y * 0.5f) + blendDistance;
            Vector3 left = new Vector3(box.offset.x - box.size.x * 0.5f, localBlendY, 0f);
            Vector3 right = new Vector3(box.offset.x + box.size.x * 0.5f, localBlendY, 0f);
            Gizmos.DrawLine(left, right);
        }

        Gizmos.matrix = previous;
    }
}