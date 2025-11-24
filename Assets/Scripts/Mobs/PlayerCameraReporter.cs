using Mirror;
using UnityEngine;

/// <summary>
/// Репортит на сервер актуальные данные о камере конкретного игрока.
/// Предполагается, что компонент висит на сетевом игроке.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerCameraReporter : NetworkBehaviour
{
    [Tooltip("Камера, от которой берем параметры (если пусто — ищем автоматически в дочерних)")]
    [SerializeField] private Camera trackedCamera;

    [Tooltip("Как часто отправлять обновление на сервер (в секундах)")]
    [SerializeField, Range(0.05f, 1f)] private float reportInterval = 0.15f;

    [Header("Spawn Safety")]
    [Tooltip("Минимальный радиус вокруг игрока, в котором мобы не появляются")]
    [SerializeField, Min(0f)] private float safeSpawnRadius = 18f;

    private float nextReportTime;

    public float SafeSpawnRadius => Mathf.Max(0f, safeSpawnRadius);

    private void EnsureCamera()
    {
        if (trackedCamera == null)
        {
            trackedCamera = GetComponentInChildren<Camera>(true);
        }
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        EnsureCamera();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        MobSpawnerManager.Instance?.ServerRegisterPlayer(this);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        MobSpawnerManager.Instance?.ServerUnregisterPlayer(this);
    }

    private void Update()
    {
        if (!isOwned)
            return;

        EnsureCamera();
        if (trackedCamera == null)
            return;

        if (Time.time < nextReportTime)
            return;

        nextReportTime = Time.time + reportInterval;
        var snapshot = new MobCameraSnapshot(
            trackedCamera.transform.position,
            trackedCamera.transform.forward,
            trackedCamera.fieldOfView,
            trackedCamera.farClipPlane);

        CmdReportCamera(snapshot);
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdReportCamera(MobCameraSnapshot snapshot)
    {
        MobSpawnerManager.Instance?.ServerReceiveCameraSnapshot(netId, snapshot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, SafeSpawnRadius);
    }
}

