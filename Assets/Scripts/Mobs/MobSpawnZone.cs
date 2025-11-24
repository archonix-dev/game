using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Триггерная зона, определяющая каких мобов можно спавнить в конкретной локации.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MobSpawnZone : MonoBehaviour
{
    [Tooltip("Название зоны для удобства в инспекторе")]
    public string zoneId = "DefaultZone";

    [Tooltip("Список префабов, которые могут быть заспавнены в зоне")]
    [SerializeField] private List<GameObject> mobPrefabs = new List<GameObject>();

    [Tooltip("Максимальное количество мобов от одного игрока одновременно в этой зоне")]
    [SerializeField, Min(1)] private int perPlayerCap = 6;

    [Tooltip("Максимальное количество мобов в зоне (от всех игроков)")]
    [SerializeField, Min(1)] private int zoneCap = 24;

    private Collider triggerCollider;

    public bool HasAvailablePrefabs => mobPrefabs != null && mobPrefabs.Count > 0;
    public int PerPlayerCap => perPlayerCap;
    public int ZoneCap => zoneCap;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkServer.active)
            return;

        var reporter = other.GetComponentInParent<PlayerCameraReporter>();
        if (reporter != null && MobSpawnerManager.Instance != null)
        {
            MobSpawnerManager.Instance.ServerAssignZone(reporter.netId, this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!NetworkServer.active)
            return;

        var reporter = other.GetComponentInParent<PlayerCameraReporter>();
        if (reporter != null && MobSpawnerManager.Instance != null)
        {
            MobSpawnerManager.Instance.ServerClearZone(reporter.netId, this);
        }
    }

    public GameObject GetRandomPrefab()
    {
        if (!HasAvailablePrefabs)
            return null;
        int index = Random.Range(0, mobPrefabs.Count);
        return mobPrefabs[index];
    }

    public bool TryGetRandomPointInside(out Vector3 point)
    {
        EnsureCollider();
        point = Vector3.zero;
        if (triggerCollider == null)
            return false;

        if (triggerCollider is BoxCollider box)
        {
            Vector3 localPoint = box.center + new Vector3(
                Random.Range(-0.5f, 0.5f) * box.size.x,
                Random.Range(-0.5f, 0.5f) * box.size.y,
                Random.Range(-0.5f, 0.5f) * box.size.z);
            point = transform.TransformPoint(localPoint);
            return true;
        }

        if (triggerCollider is SphereCollider sphere)
        {
            Vector3 localPoint = sphere.center + Random.insideUnitSphere * sphere.radius;
            point = transform.TransformPoint(localPoint);
            return true;
        }

        Bounds bounds = triggerCollider.bounds;
        for (int i = 0; i < 8; i++)
        {
            Vector3 sample = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z));

            if (ContainsPoint(sample))
            {
                point = sample;
                return true;
            }
        }

        point = bounds.center;
        return ContainsPoint(point);
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        EnsureCollider();
        if (triggerCollider == null)
            return false;

        Vector3 closest = triggerCollider.ClosestPoint(worldPoint);
        return (closest - worldPoint).sqrMagnitude <= 0.0001f;
    }

    private void OnDrawGizmos()
    {
        EnsureCollider();

        if (triggerCollider is BoxCollider box)
        {
            Gizmos.color = Color.green;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.matrix = matrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }

    private void EnsureCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
    }
}

