using UnityEngine;
using Mirror;
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
    
    private bool spawnCoroutineStarted = false; // Флаг для предотвращения дублирования
    
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
        
        // Убеждаемся, что у GameManager есть NetworkIdentity
        // Если его нет и мы в сети, добавляем его
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        if (NetworkManager.singleton != null && networkIdentity == null)
        {
            networkIdentity = gameObject.AddComponent<NetworkIdentity>();
        }
    }
    
    void Start()
    {
        // Если мы уже в игровой сцене и NetworkManager активен, инициализируем спавн
        if (NetworkServer.active)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != menuSceneName)
            {
                // Если GameManager не заспавнен, пытаемся заспавнить его
                NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
                if (networkIdentity != null && networkIdentity.netId == 0)
                {
                    NetworkServer.Spawn(gameObject);
                }
                else if (networkIdentity == null)
                {
                    Debug.LogWarning("[GameManager] NetworkIdentity компонент не найден! Добавьте его вручную в инспекторе.");
                }
                
                // Если мы заспавнены, запускаем спавн игроков
                // НО только если корутина еще не запущена (предотвращаем дублирование)
                if (netIdentity != null && netIdentity.netId != 0 && !spawnCoroutineStarted)
                {
                    spawnCoroutineStarted = true;
                    StartCoroutine(SpawnAllPlayersAfterSceneLoad());
                }
            }
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        string currentScene = SceneManager.GetActiveScene().name;
        int connectedClients = NetworkServer.connections.Count;
        Debug.Log($"[GameManager] ✓ GameManager запущен на сервере! Сцена: {currentScene}, Подключено клиентов: {connectedClients}");
        
        // Проверяем, что мы в игровой сцене (не в меню)
        if (currentScene == menuSceneName)
        {
            Debug.Log("[GameManager] Мы в сцене меню, игроки не будут спавниться.");
            return;
        }
        
        // Предотвращаем дублирование корутины спавна
        if (!spawnCoroutineStarted)
        {
            spawnCoroutineStarted = true;
            // Спавним всех подключенных игроков после загрузки сцены
            StartCoroutine(SpawnAllPlayersAfterSceneLoad());
        }
    }
    
    // Mirror обрабатывает загрузку сцен автоматически через NetworkManager
    // Этот метод больше не нужен, так как Mirror использует другую систему загрузки сцен
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[GameManager] GameManager остановлен на сервере");
        spawnCoroutineStarted = false; // Сбрасываем флаг при остановке сервера
    }
    
    void OnDestroy()
    {
        // Mirror автоматически обрабатывает отписку от событий
    }
    
    // Метод вызывается Mirror через MirrorNetworkManager.OnServerConnect
    public void OnMirrorClientConnected(uint connectionId)
    {
        // Проверяем, что мы в игровой сцене (не в меню)
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log($"[GameManager] Клиент {connectionId} подключен, но мы в меню - игрок не будет спавниться.");
            return;
        }
        
        Debug.Log($"[GameManager] ✓ Клиент {connectionId} подключен в сцене {currentScene}. Запуск спавна игрока...");
        
        // Спавним игрока для нового клиента
        if (isServer)
        {
            StartCoroutine(SpawnPlayerAfterDelay(connectionId));
        }
    }
    
    // Метод вызывается Mirror через MirrorNetworkManager.OnServerDisconnect
    public void OnMirrorClientDisconnected(uint connectionId)
    {
        // Удаляем игрока отключившегося клиента
        if (isServer)
        {
            RemovePlayerForClient(connectionId);
        }
    }
    
    
    void RemovePlayerForClient(uint connectionId)
    {
        // Находим и удаляем игрока через Mirror
        NetworkConnectionToClient conn = null;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (connection.connectionId == connectionId)
            {
                conn = connection;
                break;
            }
        }
        
        if (conn != null && conn.identity != null)
        {
            NetworkServer.Destroy(conn.identity.gameObject);
        }
    }
    
    IEnumerator SpawnAllPlayersAfterSceneLoad()
    {
        // Ждем загрузки сцены и инициализации NetworkManager
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что NetworkManager доступен
        if (!NetworkServer.active)
        {
            Debug.LogError("[GameManager] NetworkServer не активен!");
            spawnCoroutineStarted = false; // Сбрасываем флаг при ошибке
            yield break;
        }
        
        // Проверяем, что мы в игровой сцене (не в меню)
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log("[GameManager] Мы в сцене меню, игроки не будут спавниться.");
            spawnCoroutineStarted = false; // Сбрасываем флаг, если мы в меню
            yield break;
        }
        
        Debug.Log($"[GameManager] Начинаем спавн игроков в сцене {currentScene}. Подключено клиентов: {NetworkServer.connections.Count}");
        
        // Создаем список клиентов для спавна, чтобы избежать модификации коллекции во время итерации
        List<NetworkConnectionToClient> connectionsToSpawn = new List<NetworkConnectionToClient>();
        foreach (var connection in NetworkServer.connections.Values)
        {
            connectionsToSpawn.Add(connection);
        }
        
        // Спавним всех подключенных клиентов
        foreach (var connection in connectionsToSpawn)
        {
            // Проверяем, что у клиента еще нет игрока
            if (connection.identity == null)
            {
                Debug.Log($"[GameManager] Спавним игрока для клиента {connection.connectionId}");
                SpawnPlayerForClient(connection);
                yield return new WaitForSeconds(0.1f); // Небольшая задержка между спавнами
            }
            else
            {
                Debug.Log($"[GameManager] У клиента {connection.connectionId} уже есть игрок, пропускаем.");
            }
        }
        
        Debug.Log("[GameManager] Спавн всех игроков завершен.");
        spawnCoroutineStarted = false; // Сбрасываем флаг после завершения
    }
    
    IEnumerator SpawnPlayerAfterDelay(uint connectionId)
    {
        // Ждем немного чтобы сцена загрузилась
        yield return new WaitForSeconds(0.5f);
        
        // Находим соединение по connectionId
        NetworkConnectionToClient conn = null;
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (connection.connectionId == connectionId)
            {
                conn = connection;
                break;
            }
        }
        
        if (conn != null)
        {
            SpawnPlayerForClient(conn);
        }
    }
    
    void SpawnPlayerForClient(NetworkConnectionToClient conn)
    {
        // Проверяем, что мы в игровой сцене
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == menuSceneName)
        {
            Debug.Log($"[GameManager] Попытка спавна игрока {conn.connectionId} в меню - отменено.");
            return;
        }
        
        // Проверяем, что у клиента еще нет игрока
        if (conn.identity != null)
        {
            Debug.Log($"[GameManager] У клиента {conn.connectionId} уже есть игрок, пропускаем спавн.");
            return;
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] ✗ Player Prefab не назначен!");
            return;
        }
        
        Vector3 spawnPosition = GetSpawnPosition();
        
        Debug.Log($"[GameManager] Спавн игрока для клиента {conn.connectionId} в позиции {spawnPosition}...");
        
        GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkIdentity networkIdentity = playerObject.GetComponent<NetworkIdentity>();
        if (networkIdentity != null)
        {
            // Spawn игрока и назначаем его владельцем соединения
            NetworkServer.Spawn(playerObject, conn);
            Debug.Log($"[GameManager] ✓ Игрок {conn.connectionId} успешно заспавнен! Позиция: {spawnPosition}, Сцена: {currentScene}");
        }
        else
        {
            Debug.LogError($"[GameManager] ✗ Префаб игрока не имеет NetworkIdentity компонента!");
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
    [Command(requiresAuthority = false)]
    public void StartGameCommand()
    {
        // Здесь можно добавить логику начала игры
    }
    
    [Command(requiresAuthority = false)]
    public void EndGameCommand()
    {
        // Здесь можно добавить логику окончания игры
    }
    
    public void LoadGameScene()
    {
        if (isServer)
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
        if (isServer)
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
        return NetworkServer.connections.Count;
    }
    
    public bool IsGameActive()
    {
        return (NetworkServer.active && NetworkClient.active) || NetworkServer.active;
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
