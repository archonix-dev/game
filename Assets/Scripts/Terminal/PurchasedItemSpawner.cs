using Mirror;
using UnityEngine;

/// <summary>
/// Спавнит купленные через терминал предметы при загрузке сцены Main.
/// </summary>
public class PurchasedItemSpawner : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        LobbyNetworkManager.Instance?.SpawnPurchasedItemsAtPoints(spawnPoints);
    }
}

