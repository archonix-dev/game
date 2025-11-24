using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class SpawnedMobHandle : NetworkBehaviour
{
    public MobSpawnZone SourceZone { get; private set; }
    private MobSpawnerManager manager;

    [SyncVar] private uint ownerNetId;

    public void Initialize(MobSpawnerManager manager, NetworkIdentity ownerIdentity, MobSpawnZone zone)
    {
        this.manager = manager;
        ownerNetId = ownerIdentity != null ? ownerIdentity.netId : 0;
        SourceZone = zone;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        manager?.ServerHandleMobDespawn(this);
    }
}

