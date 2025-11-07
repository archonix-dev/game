using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private string gameSceneName = "Test";
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private GameObject playerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;
    
    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
            }
            return instance;
        }
    }
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Подписываемся на события подключения клиентов
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        
        // Спавним всех подключенных игроков после загрузки сцены
        if (IsServer)
        {
            StartCoroutine(SpawnAllPlayersAfterSceneLoad());
        }
    }
    
    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        base.OnNetworkDespawn();
    }
    
    void OnClientConnected(ulong clientId)
    {
        
        // Спавним игрока для нового клиента
        if (IsServer)
        {
            StartCoroutine(SpawnPlayerAfterDelay(clientId));
        }
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        
        // Удаляем игрока отключившегося клиента
        if (IsServer)
        {
            RemovePlayerForClient(clientId);
        }
    }
    
    
    void RemovePlayerForClient(ulong clientId)
    {
        // Находим и удаляем игрока
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.Despawn();
            }
        }
    }
    
    IEnumerator SpawnAllPlayersAfterSceneLoad()
    {
        
        // Ждем загрузки сцены
        yield return new WaitForSeconds(1f);
        
        
        // Спавним всех подключенных клиентов
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            SpawnPlayerForClient(client.Key);
        }
    }
    
    IEnumerator SpawnPlayerAfterDelay(ulong clientId)
    {
        
        // Ждем немного чтобы сцена загрузилась
        yield return new WaitForSeconds(0.5f);
        
        
        // Спавним игрока
        SpawnPlayerForClient(clientId);
    }
    
    void SpawnPlayerForClient(ulong clientId)
    {
        
        if (playerPrefab == null)
        {
            return;
        }
        
        Vector3 spawnPosition = GetSpawnPosition();
        
        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
        }
        else
        {
            Destroy(playerObject);
        }
    }
    
    Vector3 GetSpawnPosition()
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }
        else
        {
            return Vector3.zero;
        }
    }
    
    // Методы для управления игрой
    [ServerRpc(RequireOwnership = false)]
    public void StartGameServerRpc()
    {
        // Здесь можно добавить логику начала игры
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void EndGameServerRpc()
    {
        // Здесь можно добавить логику окончания игры
    }
    
    public void LoadGameScene()
    {
        if (IsServer)
        {
            LoadGameSceneClientRpc();
        }
    }
    
    [ClientRpc]
    void LoadGameSceneClientRpc()
    {
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void LoadMenuScene()
    {
        if (IsServer)
        {
            LoadMenuSceneClientRpc();
        }
    }
    
    [ClientRpc]
    void LoadMenuSceneClientRpc()
    {
        SceneManager.LoadScene(menuSceneName);
    }
    
    // Методы для получения информации об игре
    public int GetPlayerCount()
    {
        return NetworkManager.Singleton.ConnectedClients.Count;
    }
    
    public bool IsGameActive()
    {
        return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;
    }
    
    void OnDrawGizmosSelected()
    {
        // Рисуем точку спавна в редакторе
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 1f);
        }
    }
}
