using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private string gameSceneName = "Test";
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private GameObject playerPrefab;
    
    [Header("Scene Check")]
    [Tooltip("Имя текущей игровой сцены (где должны спавниться игроки)")]
    [SerializeField] private string currentGameSceneName = "Lobby";
    
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
        
        // Убеждаемся, что у GameManager есть NetworkObject
        // Если его нет и мы в сети, добавляем его
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && networkObject == null)
        {
            networkObject = gameObject.AddComponent<NetworkObject>();
        }
        
        // Подписываемся на события загрузки сцены NetworkManager
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        }
    }
    
    void Start()
    {
        // Если мы уже в игровой сцене и NetworkManager активен, инициализируем спавн
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != menuSceneName)
            {
                // Если GameManager не заспавнен, пытаемся заспавнить его
                NetworkObject networkObject = GetComponent<NetworkObject>();
                if (networkObject != null && !networkObject.IsSpawned)
                {
                    networkObject.Spawn();
                }
                else if (networkObject == null)
                {
                    Debug.LogWarning("[GameManager] NetworkObject компонент не найден! Добавьте его вручную в инспекторе.");
                }
                
                // Если мы заспавнены, запускаем спавн игроков
                if (IsSpawned)
                {
                    StartCoroutine(SpawnAllPlayersAfterSceneLoad());
                }
            }
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        string currentScene = SceneManager.GetActiveScene().name;
        int connectedClients = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 0;
        Debug.Log($"[GameManager] ✓ GameManager заспавнен! IsServer: {IsServer}, Сцена: {currentScene}, Подключено клиентов: {connectedClients}");
        
        // Проверяем, что мы в игровой сцене (не в меню)
        if (currentScene == menuSceneName)
        {
            Debug.Log("[GameManager] Мы в сцене меню, игроки не будут спавниться.");
            return;
        }
        
        // Подписываемся на события подключения клиентов
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log("[GameManager] Подписка на события подключения клиентов установлена.");
        }
        
        // Спавним всех подключенных игроков после загрузки сцены
        if (IsServer)
        {
            StartCoroutine(SpawnAllPlayersAfterSceneLoad());
        }
    }
    
    void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"[GameManager] ✓ Сцена {sceneName} загружена. IsServer: {IsServer}, IsSpawned: {IsSpawned}, Клиентов завершило: {clientsCompleted?.Count ?? 0}");
        
        // Проверяем, что мы в игровой сцене (не в меню)
        if (sceneName == menuSceneName)
        {
            Debug.Log("[GameManager] Загружена сцена меню, игроки не будут спавниться.");
            return;
        }
        
        // Если это игровая сцена и мы сервер, запускаем спавн игроков
        // GameManager должен быть в сцене Lobby как GameObject с NetworkObject компонентом
        if (IsServer)
        {
            // Ищем GameManager в сцене
            GameManager existingManager = FindObjectOfType<GameManager>();
            if (existingManager != null && existingManager.IsSpawned)
            {
                Debug.Log($"[GameManager] ✓ GameManager найден и заспавнен в сцене {sceneName}. Запуск спавна игроков...");
                existingManager.StartCoroutine(existingManager.SpawnAllPlayersAfterSceneLoad());
            }
            else if (existingManager != null && !existingManager.IsSpawned)
            {
                Debug.LogWarning("[GameManager] GameManager найден, но не заспавнен. Пытаемся заспавнить...");
                NetworkObject networkObject = existingManager.GetComponent<NetworkObject>();
                if (networkObject != null && !networkObject.IsSpawned)
                {
                    networkObject.Spawn();
                }
            }
            else
            {
                Debug.LogError("[GameManager] ✗ GameManager не найден в сцене Lobby! Убедитесь, что GameManager добавлен в сцену Lobby как GameObject с NetworkObject компонентом.");
            }
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
        
        // Отписываемся от событий загрузки сцены
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }
        
        base.OnNetworkDespawn();
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
            }
            
            // Отписываемся от событий подключения клиентов
            // Проверяем IsServer безопасно
            try
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                }
            }
            catch
            {
                // Игнорируем ошибки, если NetworkManager уже уничтожен
            }
        }
    }
    
    void OnClientConnected(ulong clientId)
    {
        // Проверяем, что мы в игровой сцене (не в меню)
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log($"[GameManager] Клиент {clientId} подключен, но мы в меню - игрок не будет спавниться.");
            return;
        }
        
        Debug.Log($"[GameManager] ✓ Клиент {clientId} подключен в сцене {currentScene}. Запуск спавна игрока...");
        
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
        // Ждем загрузки сцены и инициализации NetworkManager
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что NetworkManager доступен
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[GameManager] NetworkManager не найден!");
            yield break;
        }
        
        // Проверяем, что мы в игровой сцене (не в меню)
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log("[GameManager] Мы в сцене меню, игроки не будут спавниться.");
            yield break;
        }
        
        Debug.Log($"[GameManager] Начинаем спавн игроков в сцене {currentScene}. Подключено клиентов: {NetworkManager.Singleton.ConnectedClients.Count}");
        
        // Спавним всех подключенных клиентов
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            // Проверяем, что у клиента еще нет игрока
            if (client.Value.PlayerObject == null)
            {
                Debug.Log($"[GameManager] Спавним игрока для клиента {client.Key}");
                SpawnPlayerForClient(client.Key);
                yield return new WaitForSeconds(0.1f); // Небольшая задержка между спавнами
            }
            else
            {
                Debug.Log($"[GameManager] У клиента {client.Key} уже есть игрок, пропускаем.");
            }
        }
        
        Debug.Log("[GameManager] Спавн всех игроков завершен.");
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
        // Проверяем, что мы в игровой сцене
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log($"[GameManager] Попытка спавна игрока {clientId} в меню - отменено.");
            return;
        }
        
        // Проверяем, что у клиента еще нет игрока
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log($"[GameManager] У клиента {clientId} уже есть игрок, пропускаем спавн.");
                return;
            }
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] ✗ Player Prefab не назначен!");
            return;
        }
        
        Vector3 spawnPosition = GetSpawnPosition();
        
        Debug.Log($"[GameManager] Спавн игрока для клиента {clientId} в позиции {spawnPosition}...");
        
        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"[GameManager] ✓ Игрок {clientId} успешно заспавнен! Позиция: {spawnPosition}, Сцена: {currentScene}");
        }
        else
        {
            Debug.LogError($"[GameManager] ✗ Префаб игрока не имеет NetworkObject компонента!");
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
