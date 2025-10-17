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
            DontDestroyOnLoad(gameObject);
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
        Debug.Log($"Client {clientId} connected to game");
        
        // Спавним игрока для нового клиента
        if (IsServer)
        {
            StartCoroutine(SpawnPlayerAfterDelay(clientId));
        }
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected from game");
        
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
                Debug.Log($"Player removed for client {clientId}");
            }
        }
    }
    
    IEnumerator SpawnAllPlayersAfterSceneLoad()
    {
        Debug.Log("SpawnAllPlayersAfterSceneLoad started");
        
        // Ждем загрузки сцены
        yield return new WaitForSeconds(1f);
        
        Debug.Log($"Found {NetworkManager.Singleton.ConnectedClients.Count} connected clients");
        
        // Спавним всех подключенных клиентов
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            Debug.Log($"Spawning player for existing client {client.Key}");
            SpawnPlayerForClient(client.Key);
        }
    }
    
    IEnumerator SpawnPlayerAfterDelay(ulong clientId)
    {
        Debug.Log($"SpawnPlayerAfterDelay started for client {clientId}");
        
        // Ждем немного чтобы сцена загрузилась
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"Spawning player for new client {clientId}");
        
        // Спавним игрока
        SpawnPlayerForClient(clientId);
    }
    
    void SpawnPlayerForClient(ulong clientId)
    {
        Debug.Log($"Attempting to spawn player for client {clientId}");
        
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned!");
            return;
        }
        
        Vector3 spawnPosition = GetSpawnPosition();
        Debug.Log($"Spawning at position: {spawnPosition}");
        
        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"Player spawned for client {clientId} at position {spawnPosition}");
        }
        else
        {
            Debug.LogError("Player prefab doesn't have NetworkObject component!");
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
        Debug.Log("Game started by server");
        // Здесь можно добавить логику начала игры
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void EndGameServerRpc()
    {
        Debug.Log("Game ended by server");
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
