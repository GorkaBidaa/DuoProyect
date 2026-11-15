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

    private ParticleSystem[] forestSystems, snowSystems;
    private BiomeParticleType currentBiome;
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
        Configure(forestSystems);
        Configure(snowSystems);
        StopAndClear(forestSystems);
        StopAndClear(snowSystems);
        initialized = true;
    }

    private bool ValidRoot(GameObject root) => root && root.activeInHierarchy &&
        root.transform != transform && root.transform.IsChildOf(transform);

    private static void Configure(ParticleSystem[] systems)
    {
        foreach (ParticleSystem system in systems)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

    private void OnEnable() => refresh = true;

    private void LateUpdate()
    {
        if (!initialized || !player || !followTarget || Time.timeScale <= 0f) return;
        Vector3 view = followTarget.position;
        transform.position = new Vector3(view.x + followOffset.x, view.y + followOffset.y, effectPlaneZ);
        BiomeParticleType next = BiomeParticleZone.Resolve(player.position, defaultBiome);
        float thresholdSquared = teleportClearDistance * teleportClearDistance;
        bool jumped = !refresh &&
            (((Vector2)(player.position - previousPlayer)).sqrMagnitude > thresholdSquared ||
             ((Vector2)(view - previousView)).sqrMagnitude > thresholdSquared);

        if (refresh || jumped || next != currentBiome)
        {
            StopAndClear(forestSystems);
            StopAndClear(snowSystems);
            currentBiome = next;
            if (next == BiomeParticleType.Forest) StartEffect(forestSystems);
            else if (next == BiomeParticleType.Snow) StartEffect(snowSystems);
            refresh = false;
        }
        previousPlayer = player.position;
        previousView = view;
    }

    private void StartEffect(ParticleSystem[] systems)
    {
        foreach (ParticleSystem system in systems)
        {
            if (!system || !system.gameObject.activeInHierarchy) continue;
            if (warmupSeconds > 0f) system.Simulate(warmupSeconds, false, true, false);
            system.Play(false);
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
