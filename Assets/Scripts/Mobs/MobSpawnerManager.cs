using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Данные о положении камеры игрока, которые нужны серверу для определения зоны видимости.
/// </summary>
[System.Serializable]
public struct MobCameraSnapshot
{
    public Vector3 position;
    public Vector3 forward;
    public float fieldOfView;
    public float farClipPlane;

    public bool IsValid => fieldOfView > 0f && farClipPlane > 0f;

    public MobCameraSnapshot(Vector3 position, Vector3 forward, float fieldOfView, float farClipPlane)
    {
        this.position = position;
        this.forward = forward == Vector3.zero ? Vector3.forward : forward.normalized;
        this.fieldOfView = Mathf.Max(1f, fieldOfView);
        this.farClipPlane = Mathf.Max(0.1f, farClipPlane);
    }
}

/// <summary>
/// Центральный менеджер спавна мобов. Должен быть добавлен на сцену один раз.
/// </summary>
public class MobSpawnerManager : NetworkBehaviour
{
    public class PlayerSpawnState
    {
        public PlayerCameraReporter reporter;
        public MobCameraSnapshot snapshot;
        public MobSpawnZone currentZone;
        public readonly HashSet<NetworkIdentity> spawnedMobs = new HashSet<NetworkIdentity>();
    }

    public static MobSpawnerManager Instance { get; private set; }

    [Header("Spawn Timing")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 4f;
    [SerializeField, Range(1, 10)] private int spawnAttemptsPerTick = 3;

    [Header("Distance Settings")]
    [SerializeField, Min(0f)] private float beyondFarClipOffset = 4f;
    [SerializeField, Min(1f)] private float additionalDistanceSpread = 12f;
    [SerializeField, Min(0.1f)] private float fallbackSafeRadius = 18f;

    [Header("Visibility Settings")]
    [SerializeField, Range(0f, 30f)] private float fovPaddingDegrees = 5f;
    [SerializeField, Range(0f, 1f)] private float verticalDirectionBias = 0.2f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.1f)] private float groundRayLength = 20f;
    [SerializeField] private float spawnHeightOffset = 0.3f;

    [Header("Population Limits")]
    [SerializeField, Min(1)] private int globalMobLimit = 120;
    [SerializeField, Min(1)] private int perPlayerLimit = 8;

    [Header("Replacement Settings")]
    [SerializeField] private Vector2 replacementSpawnDelay = new Vector2(0.5f, 1.5f);
    [SerializeField, Range(0f, 179f)] private float minRearSpawnAngle = 100f;

    private readonly Dictionary<uint, PlayerSpawnState> playerStates = new Dictionary<uint, PlayerSpawnState>();
    private readonly Dictionary<MobSpawnZone, HashSet<NetworkIdentity>> zonePopulations = new Dictionary<MobSpawnZone, HashSet<NetworkIdentity>>();
    private readonly Dictionary<NetworkIdentity, uint> mobOwners = new Dictionary<NetworkIdentity, uint>();
    private Coroutine spawnRoutine;
    private readonly Dictionary<uint, Coroutine> pendingRespawnCoroutines = new Dictionary<uint, Coroutine>();

    public IReadOnlyDictionary<uint, PlayerSpawnState> DebugPlayerStates => playerStates;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MobSpawnerManager] На сцене найден второй экземпляр, он будет уничтожен.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(ServerSpawnLoop());
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        foreach (var pending in pendingRespawnCoroutines.Values)
        {
            if (pending != null)
            {
                StopCoroutine(pending);
            }
        }
        pendingRespawnCoroutines.Clear();
        playerStates.Clear();
        zonePopulations.Clear();
        mobOwners.Clear();
    }

    private IEnumerator ServerSpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            yield return wait;

            if (!NetworkServer.active)
            {
                continue;
            }

            foreach (var kvp in playerStates)
            {
                var state = kvp.Value;
                if (state == null || state.reporter == null)
                    continue;

                if (!state.snapshot.IsValid)
                    continue;

                if (state.currentZone == null || !state.currentZone.HasAvailablePrefabs)
                    continue;

                var zoneLimit = Mathf.Min(perPlayerLimit, state.currentZone.PerPlayerCap);
                if (state.spawnedMobs.Count >= zoneLimit)
                    continue;

                if (GetTotalMobCount() >= globalMobLimit)
                    break;

                for (int attempt = 0; attempt < spawnAttemptsPerTick; attempt++)
                {
                    if (TryBuildSpawnCommand(state, out var prefab, out var position, out var rotation))
                    {
                        SpawnMobForPlayer(state, prefab, position, rotation);
                        break;
                    }
                }
            }
        }
    }

    [Server]
    private int GetTotalMobCount()
    {
        return mobOwners.Count;
    }

    [Server]
    private bool TryBuildSpawnCommand(PlayerSpawnState state, out GameObject prefab, out Vector3 position, out Quaternion rotation)
    {
        prefab = null;
        position = Vector3.zero;
        rotation = Quaternion.identity;

        var zone = state.currentZone;
        if (zone == null || !zone.HasAvailablePrefabs)
            return false;

        if (zonePopulations.TryGetValue(zone, out var zonePopulation))
        {
            if (zonePopulation.Count >= zone.ZoneCap)
                return false;
        }

        var snapshot = state.snapshot;
        var playerSafeRadius = ResolveSafeRadius(state);
        var cosThreshold = Mathf.Cos(Mathf.Deg2Rad * ((snapshot.fieldOfView * 0.5f) + fovPaddingDegrees));
        var cameraForward = snapshot.forward == Vector3.zero ? Vector3.forward : snapshot.forward.normalized;
        bool enforceRearSpawn = minRearSpawnAngle > 0f;
        float rearSpawnCosThreshold = enforceRearSpawn ? Mathf.Cos(Mathf.Deg2Rad * minRearSpawnAngle) : 1f;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (!zone.TryGetRandomPointInside(out var candidate))
                break;

            var checkPoint = candidate;
            if (TryProjectOnGround(candidate, out var grounded))
            {
                if (zone.ContainsPoint(grounded))
                {
                    checkPoint = grounded;
                }
            }

            Vector3 toCandidate = checkPoint - snapshot.position;
            float distance = toCandidate.magnitude;

            // Не спавним слишком близко к игроку
            if (distance < playerSafeRadius)
                continue;

            Vector3 dir = toCandidate.sqrMagnitude > 0.0001f ? toCandidate.normalized : -cameraForward;

            if (verticalDirectionBias > 0f)
            {
                dir.y *= 1f - verticalDirectionBias;
                dir = dir.normalized;
            }

            // Не спавним в пределах текущего поля зрения камеры (по FOV),
            // чтобы моб появлялся "за кадром", но при этом ОСТАВАЛСЯ в пределах зоны.
            if (Vector3.Dot(cameraForward, dir) > cosThreshold)
                continue;

            if (enforceRearSpawn && Vector3.Dot(cameraForward, dir) > rearSpawnCosThreshold)
                continue;

            prefab = zone.GetRandomPrefab();
            if (prefab == null)
                return false;

            rotation = Quaternion.LookRotation(-dir.SetY(0f), Vector3.up);
            position = checkPoint;
            return true;
        }

        return false;
    }

    private bool TryProjectOnGround(Vector3 start, out Vector3 grounded, bool snapToGround = true)
    {
        grounded = start;
        if (!snapToGround)
            return true;

        var rayOrigin = start + Vector3.up * (groundRayLength * 0.5f);
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            grounded = hit.point + Vector3.up * spawnHeightOffset;
            return true;
        }
        return false;
    }

    [Server]
    private void SpawnMobForPlayer(PlayerSpawnState state, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var instance = Instantiate(prefab, position, rotation);
        var handle = instance.GetComponent<SpawnedMobHandle>();
        if (handle == null)
        {
            handle = instance.AddComponent<SpawnedMobHandle>();
        }

        var identity = instance.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogError($"[MobSpawnerManager] Префаб {prefab.name} не содержит NetworkIdentity.");
            Destroy(instance);
            return;
        }

        handle.Initialize(this, state.reporter.netIdentity, state.currentZone);
        NetworkServer.Spawn(instance);

        state.spawnedMobs.Add(identity);
        mobOwners[identity] = state.reporter.netId;

        if (!zonePopulations.TryGetValue(state.currentZone, out var zonePool))
        {
            zonePool = new HashSet<NetworkIdentity>();
            zonePopulations[state.currentZone] = zonePool;
        }
        zonePool.Add(identity);
    }

    [Server]
    internal void ServerHandleMobDespawn(SpawnedMobHandle handle)
    {
        var identity = handle.netIdentity;
        if (identity == null)
            return;

        uint ownerNetId = 0;

        if (mobOwners.TryGetValue(identity, out ownerNetId))
        {
            if (playerStates.TryGetValue(ownerNetId, out var state))
            {
                state.spawnedMobs.Remove(identity);
            }
            mobOwners.Remove(identity);
        }

        if (handle.SourceZone != null && zonePopulations.TryGetValue(handle.SourceZone, out var zonePool))
        {
            zonePool.Remove(identity);
        }

        if (ownerNetId != 0 && playerStates.TryGetValue(ownerNetId, out var ownerState) && ownerState.currentZone != null)
        {
            ScheduleReplacement(ownerNetId, ownerState);
        }
    }

    #region Registration API

    [Server]
    public void ServerRegisterPlayer(PlayerCameraReporter reporter)
    {
        if (reporter == null)
            return;

        if (!playerStates.TryGetValue(reporter.netId, out var state))
        {
            state = new PlayerSpawnState
            {
                reporter = reporter,
                snapshot = new MobCameraSnapshot(reporter.transform.position, reporter.transform.forward, 90f, 50f)
            };
            playerStates.Add(reporter.netId, state);
        }
        else
        {
            state.reporter = reporter;
        }
    }

    [Server]
    public void ServerUnregisterPlayer(PlayerCameraReporter reporter)
    {
        if (reporter == null)
            return;

        var netId = reporter.netId;
        if (playerStates.TryGetValue(netId, out var state))
        {
            foreach (var mob in state.spawnedMobs)
            {
                if (mob != null && mob.gameObject != null)
                {
                    NetworkServer.Destroy(mob.gameObject);
                }
            }
            playerStates.Remove(netId);
        }

        if (pendingRespawnCoroutines.TryGetValue(netId, out var pending))
        {
            if (pending != null)
            {
                StopCoroutine(pending);
            }
            pendingRespawnCoroutines.Remove(netId);
        }
    }

    [Server]
    public void ServerReceiveCameraSnapshot(uint netId, MobCameraSnapshot snapshot)
    {
        if (playerStates.TryGetValue(netId, out var state))
        {
            state.snapshot = snapshot;
        }
    }

    [Server]
    public void ServerAssignZone(uint netId, MobSpawnZone zone)
    {
        if (!playerStates.TryGetValue(netId, out var state))
            return;

        if (state.currentZone == zone)
            return;

        if (state.currentZone != null)
        {
            DespawnPlayerMobs(state, state.currentZone);
        }

        state.currentZone = zone;
        FillPlayerQuota(state);
    }

    [Server]
    public void ServerClearZone(uint netId, MobSpawnZone zone)
    {
        if (!playerStates.TryGetValue(netId, out var state))
            return;

        if (state.currentZone != zone)
            return;

        DespawnPlayerMobs(state, zone);
        state.currentZone = null;
    }

    #endregion

    private float ResolveSafeRadius(PlayerSpawnState state)
    {
        if (state?.reporter != null)
        {
            return Mathf.Max(0.1f, state.reporter.SafeSpawnRadius);
        }

        return fallbackSafeRadius;
    }

    [Server]
    private void DespawnPlayerMobs(PlayerSpawnState state, MobSpawnZone zoneFilter = null)
    {
        if (state == null || state.spawnedMobs.Count == 0)
            return;

        state.spawnedMobs.RemoveWhere(identity => identity == null);

        var toDespawn = new List<NetworkIdentity>();
        foreach (var identity in state.spawnedMobs)
        {
            if (identity == null)
                continue;

            if (zoneFilter != null)
            {
                var handle = identity.GetComponent<SpawnedMobHandle>();
                if (handle == null || handle.SourceZone != zoneFilter)
                    continue;
            }

            toDespawn.Add(identity);
        }

        foreach (var identity in toDespawn)
        {
            if (identity != null && identity.gameObject != null)
            {
                NetworkServer.Destroy(identity.gameObject);
            }
        }
    }

    [Server]
    private void TrySpawnReplacement(PlayerSpawnState state)
    {
        if (state == null || state.currentZone == null || !state.snapshot.IsValid)
            return;

        if (!state.currentZone.HasAvailablePrefabs)
            return;

        var zoneLimit = Mathf.Min(perPlayerLimit, state.currentZone.PerPlayerCap);
        if (state.spawnedMobs.Count >= zoneLimit)
            return;

        if (GetTotalMobCount() >= globalMobLimit)
            return;

        if (TryBuildSpawnCommand(state, out var prefab, out var position, out var rotation))
        {
            SpawnMobForPlayer(state, prefab, position, rotation);
        }
    }

    void ScheduleReplacement(uint ownerNetId, PlayerSpawnState state)
    {
        if (state == null || state.currentZone == null)
            return;

        if (pendingRespawnCoroutines.TryGetValue(ownerNetId, out var pending) && pending != null)
        {
            StopCoroutine(pending);
            pendingRespawnCoroutines.Remove(ownerNetId);
        }

        var coroutine = StartCoroutine(RespawnAfterDelay(ownerNetId));
        pendingRespawnCoroutines[ownerNetId] = coroutine;
    }

    IEnumerator RespawnAfterDelay(uint ownerNetId)
    {
        float minDelay = Mathf.Min(replacementSpawnDelay.x, replacementSpawnDelay.y);
        float maxDelay = Mathf.Max(replacementSpawnDelay.x, replacementSpawnDelay.y);
        float delay = Mathf.Max(0f, Random.Range(minDelay, maxDelay));

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        pendingRespawnCoroutines.Remove(ownerNetId);

        if (!NetworkServer.active)
            yield break;

        if (!playerStates.TryGetValue(ownerNetId, out var state))
            yield break;

        if (state.currentZone == null)
            yield break;

        TrySpawnReplacement(state);
    }

    [Server]
    private void FillPlayerQuota(PlayerSpawnState state)
    {
        if (state == null)
            return;

        for (int i = 0; i < spawnAttemptsPerTick; i++)
        {
            int beforeCount = state.spawnedMobs.Count;
            TrySpawnReplacement(state);
            if (state.spawnedMobs.Count == beforeCount)
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        if (Application.isPlaying && playerStates != null)
        {
            foreach (var state in playerStates.Values)
            {
                if (state == null)
                    continue;
                var snapshot = state.snapshot;
                if (!snapshot.IsValid)
                    continue;
                Gizmos.DrawWireSphere(snapshot.position, ResolveSafeRadius(state));
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, fallbackSafeRadius);
        }
    }
}

internal static class VectorExtensions
{
    public static Vector3 SetY(this Vector3 vector, float y)
    {
        vector.y = y;
        return vector;
    }
}

