using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class BiomeParticleController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform followTarget;
    [SerializeField] private GameObject forestEffectRoot;
    [SerializeField] private GameObject snowEffectRoot;
    [SerializeField] private BiomeParticleType defaultBiome = BiomeParticleType.Forest;
    [Tooltip("World-space offset above the gameplay camera.")]
    [SerializeField] private Vector2 followOffset = new Vector2(0f, 9f);
    [Tooltip("Simulate a few seconds when an effect starts, so particles already fill the view.")]
    [SerializeField, Range(0f, 8f)] private float warmupSeconds = 4f;
    [Tooltip("Clear old particles after a large player or camera position jump.")]
    [SerializeField, Min(0.1f)] private float teleportClearDistance = 12f;

    [Header("Smooth Crossfade Settings")]
    [Tooltip("Speed at which emission rates blend when transitioning between Forest and Snow.")]
    [SerializeField, Min(0.1f)] private float crossfadeSpeed = 1.2f;

    private ParticleSystem[] forestSystems, snowSystems;
    private float[] forestBaseRates, snowBaseRates;
    private float currentForestWeight, currentSnowWeight;
    private Vector3 previousPlayer, previousView;
    private float effectPlaneZ;
    private bool refresh = true;
    private bool initialized;

    private void Awake()
    {
        if (!followTarget && Camera.main) followTarget = Camera.main.transform;
        if (!player || !followTarget || !ValidRoot(forestEffectRoot) || !ValidRoot(snowEffectRoot) ||
            forestEffectRoot == snowEffectRoot ||
            forestEffectRoot.transform.IsChildOf(snowEffectRoot.transform) ||
            snowEffectRoot.transform.IsChildOf(forestEffectRoot.transform))
        {
            Debug.LogError("BiomeParticleController needs Player, camera Follow Target, and separate active Forest/Snow effect children beneath this Weather object.", this);
            enabled = false;
            return;
        }

        forestSystems = forestEffectRoot.GetComponentsInChildren<ParticleSystem>();
        snowSystems = snowEffectRoot.GetComponentsInChildren<ParticleSystem>();
        if (forestSystems.Length == 0 || snowSystems.Length == 0)
        {
            Debug.LogError("Both biome effect roots need an active ParticleSystem in their hierarchy.", this);
            enabled = false;
            return;
        }

        effectPlaneZ = transform.position.z;
        forestBaseRates = CacheAndConfigure(forestSystems);
        snowBaseRates = CacheAndConfigure(snowSystems);
        StopAndClear(forestSystems);
        StopAndClear(snowSystems);
        initialized = true;
    }

    private bool ValidRoot(GameObject root) => root && root.activeInHierarchy &&
        root.transform != transform && root.transform.IsChildOf(transform);

    private static float[] CacheAndConfigure(ParticleSystem[] systems)
    {
        float[] baseRates = new float[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            var main = system.main;
            main.playOnAwake = false;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = system.emission;
            baseRates[i] = Mathf.Max(1f, emission.rateOverTimeMultiplier);
        }
        return baseRates;
    }

    private void OnEnable() => refresh = true;

    private void LateUpdate()
    {
        if (!initialized || !player || !followTarget || Time.timeScale <= 0f) return;

        Vector3 view = followTarget.position;
        transform.position = new Vector3(view.x + followOffset.x, view.y + followOffset.y, effectPlaneZ);

        BiomeParticleZone.ResolveBlend(player.position, defaultBiome, out float targetForest, out float targetSnow);

        float thresholdSquared = teleportClearDistance * teleportClearDistance;
        bool jumped = !refresh &&
            (((Vector2)(player.position - previousPlayer)).sqrMagnitude > thresholdSquared ||
             ((Vector2)(view - previousView)).sqrMagnitude > thresholdSquared);

        if (refresh || jumped)
        {
            StopAndClear(forestSystems);
            StopAndClear(snowSystems);

            currentForestWeight = targetForest;
            currentSnowWeight = targetSnow;

            ApplyWeights(forestSystems, forestBaseRates, currentForestWeight, true);
            ApplyWeights(snowSystems, snowBaseRates, currentSnowWeight, true);
            refresh = false;
        }
        else
        {
            float step = crossfadeSpeed * Time.deltaTime;
            currentForestWeight = Mathf.MoveTowards(currentForestWeight, targetForest, step);
            currentSnowWeight = Mathf.MoveTowards(currentSnowWeight, targetSnow, step);

            ApplyWeights(forestSystems, forestBaseRates, currentForestWeight, false);
            ApplyWeights(snowSystems, snowBaseRates, currentSnowWeight, false);
        }

        previousPlayer = player.position;
        previousView = view;
    }

    private void ApplyWeights(ParticleSystem[] systems, float[] baseRates, float weight, bool instantWarmup)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (!system || !system.gameObject.activeInHierarchy) continue;

            var emission = system.emission;
            emission.rateOverTimeMultiplier = baseRates[i] * weight;

            if (weight > 0.01f)
            {
                if (!system.isPlaying)
                {
                    if (instantWarmup && warmupSeconds > 0f)
                        system.Simulate(warmupSeconds, false, true, false);
                    system.Play(false);
                }
            }
            else if (system.isPlaying)
            {
                // Stop emitting new particles, but let existing airborne particles finish falling naturally
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private static void StopAndClear(ParticleSystem[] systems)
    {
        if (systems == null) return;
        foreach (ParticleSystem system in systems)
            if (system) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        StopAndClear(forestSystems);
        StopAndClear(snowSystems);
        refresh = true;
    }
}