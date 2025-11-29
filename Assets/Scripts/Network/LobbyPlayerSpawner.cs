using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Спавнит игроков на сцене Lobby
/// </summary>
public class LobbyPlayerSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Transform для спавна игроков")]
    public Transform[] spawnPoints;
    
    [Tooltip("Префаб игрока для спавна")]
    public GameObject playerPrefab;
    
    private static LobbyPlayerSpawner instance;
    private System.Collections.Generic.HashSet<int> spawnedConnections = new System.Collections.Generic.HashSet<int>();
    
    public static LobbyPlayerSpawner Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyPlayerSpawner>();
            }
            return instance;
        }
    }
    
    void Awake()
    {
        instance = this;
        Debug.Log("[LobbyPlayerSpawner] Awake вызван");
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[LobbyPlayerSpawner] OnStartServer вызван - спавнер готов на сервере");
        Debug.Log($"[LobbyPlayerSpawner] playerPrefab назначен: {(playerPrefab != null ? playerPrefab.name : "НЕТ!")}");
        Debug.Log($"[LobbyPlayerSpawner] spawnPoints назначено: {(spawnPoints != null ? spawnPoints.Length : 0)} точек");
        
        // Подписываемся на события подключения/отключения клиентов
        NetworkServer.OnConnectedEvent += OnClientConnected;
        NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
        
        Debug.Log("[LobbyPlayerSpawner] Подписался на события подключения/отключения клиентов");
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[LobbyPlayerSpawner] OnStopServer вызван - отписываемся от событий");
        NetworkServer.OnConnectedEvent -= OnClientConnected;
        NetworkServer.OnDisconnectedEvent -= OnClientDisconnected;
        spawnedConnections.Clear();
    }
    
    void OnClientConnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[LobbyPlayerSpawner] OnClientConnected вызван для подключения {conn.connectionId}");
        Debug.Log($"[LobbyPlayerSpawner] Подключение готово (isReady): {conn.isReady}");
        Debug.Log($"[LobbyPlayerSpawner] Текущая сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        
        if (LobbyNetworkManager.Instance != null)
        {
            Debug.Log("[LobbyPlayerSpawner] Запрашиваем спавн через LobbyNetworkManager");
            LobbyNetworkManager.Instance.RequestLobbyPlayerSpawn(conn);
        }
        else
        {
            Debug.LogWarning("[LobbyPlayerSpawner] LobbyNetworkManager.Instance не найден, спавним напрямую");
            SpawnPlayer(conn);
        }
    }
    
    /// <summary>
    /// Спавнит игрока для указанного подключения
    /// </summary>
    [Server]
    public void SpawnPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[LobbyPlayerSpawner] SpawnPlayer вызван для подключения {conn.connectionId}");
        Debug.Log($"[LobbyPlayerSpawner] NetworkServer.active: {NetworkServer.active}");
        Debug.Log($"[LobbyPlayerSpawner] isServer: {isServer}");
        
        if (playerPrefab == null)
        {
            Debug.LogError("[LobbyPlayerSpawner] playerPrefab не назначен! Назначьте игровой префаб в Inspector.");
            return;
        }
        
        Debug.Log($"[LobbyPlayerSpawner] playerPrefab: {playerPrefab.name}");
        
        // Проверяем, не заспавнен ли уже игрок для этого подключения
        if (spawnedConnections.Contains(conn.connectionId))
        {
            Debug.LogWarning($"[LobbyPlayerSpawner] Игрок для подключения {conn.connectionId} уже заспавнен!");
            Debug.Log($"[LobbyPlayerSpawner] Уже заспавненные подключения: {string.Join(", ", spawnedConnections)}");
            return;
        }
        
        if (!conn.isReady)
        {
            Debug.LogWarning($"[LobbyPlayerSpawner] Подключение {conn.connectionId} еще не готово. Ожидаем событие готовности перед спавном.");
            
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.RequestLobbyPlayerSpawn(conn);
            }
            else
            {
                StartCoroutine(SpawnPlayerWhenReady(conn));
            }
            return;
        }
        
        Debug.Log($"[LobbyPlayerSpawner] Подключение {conn.connectionId} готово, продолжаем спавн");
        
        // Выбираем точку спавна
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;
        
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Используем индекс подключения для выбора точки спавна
            int spawnIndex = conn.connectionId % spawnPoints.Length;
            if (spawnPoints[spawnIndex] != null)
            {
                spawnPosition = spawnPoints[spawnIndex].position;
                spawnRotation = spawnPoints[spawnIndex].rotation;
            }
            else
            {
                Debug.LogWarning($"[LobbyPlayerSpawner] Точка спавна {spawnIndex} не назначена!");
            }
        }
        else
        {
            Debug.LogWarning("[LobbyPlayerSpawner] Точки спавна не назначены! Игрок будет заспавнен в (0,0,0)");
        }
        
        // Создаем игрока
        Debug.Log($"[LobbyPlayerSpawner] Создаем экземпляр префаба {playerPrefab.name} в позиции {spawnPosition}");
        GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        Debug.Log($"[LobbyPlayerSpawner] Экземпляр создан: {player.name}");
        
        // Монеты теперь сохраняются в PlayerPrefs и загружаются автоматически при старте CoinManager
        // Не нужно кешировать или восстанавливать монеты
        
        // Проверяем NetworkIdentity на префабе
        NetworkIdentity playerIdentity = player.GetComponent<NetworkIdentity>();
        if (playerIdentity == null)
        {
            Debug.LogError($"[LobbyPlayerSpawner] Префаб {playerPrefab.name} не имеет компонента NetworkIdentity!");
        }
        else
        {
            Debug.Log($"[LobbyPlayerSpawner] NetworkIdentity найден на префабе, assetId: {playerIdentity.assetId}");
        }
        

        // Используем ReplacePlayerForConnection если есть player object, иначе AddPlayerForConnection
        // Это необходимо, так как у каждого подключения может быть только один "player object"
        Debug.Log($"[LobbyPlayerSpawner] Текущий player object для подключения: {(conn.identity != null ? conn.identity.name : "НЕТ")}");
        
        bool success = false;
        if (conn.identity != null)
        {
            // Если есть player object (LobbyPlayer), заменяем его на игровой префаб
            Debug.Log($"[LobbyPlayerSpawner] Заменяем существующий player object {conn.identity.name} на игровой префаб");
            success = NetworkServer.ReplacePlayerForConnection(conn, player, ReplacePlayerOptions.Destroy);
            if (success)
            {
                Debug.Log($"[LobbyPlayerSpawner] NetworkServer.ReplacePlayerForConnection выполнен успешно");
            }
            else
            {
                Debug.LogError($"[LobbyPlayerSpawner] NetworkServer.ReplacePlayerForConnection НЕ выполнен!");
            }
        }
        else
        {
            // Если нет player object, добавляем игровой префаб как новый player object
            Debug.Log($"[LobbyPlayerSpawner] Добавляем игровой префаб как новый player object (LobbyPlayer уже уничтожен)");
            success = NetworkServer.AddPlayerForConnection(conn, player);
            if (success)
            {
                Debug.Log($"[LobbyPlayerSpawner] NetworkServer.AddPlayerForConnection выполнен успешно");
            }
            else
            {
                Debug.LogError($"[LobbyPlayerSpawner] NetworkServer.AddPlayerForConnection НЕ выполнен!");
            }
        }
        
        if (!success)
        {
            Debug.LogError($"[LobbyPlayerSpawner] Не удалось привязать игрока к подключению {conn.connectionId}, уничтожаем инстанс.");
            Destroy(player);
            return;
        }
        
        // Проверяем состояние объекта после спавна
        Debug.Log($"[LobbyPlayerSpawner] Проверка объекта после спавна:");
        Debug.Log($"[LobbyPlayerSpawner] - GameObject активен: {player.activeSelf}");
        Debug.Log($"[LobbyPlayerSpawner] - GameObject активен в иерархии: {player.activeInHierarchy}");
        Debug.Log($"[LobbyPlayerSpawner] - Позиция объекта: {player.transform.position}");
        Debug.Log($"[LobbyPlayerSpawner] - Имя объекта: {player.name}");
        
        // Проверяем компоненты на объекте
        var components = player.GetComponents<Component>();
        Debug.Log($"[LobbyPlayerSpawner] Компоненты на объекте ({components.Length}):");
        foreach (var comp in components)
        {
            if (comp != null)
            {
                Debug.Log($"[LobbyPlayerSpawner]   - {comp.GetType().Name}");
            }
        }
        
        // Проверяем, есть ли LobbyPlayer (не должно быть!)
        LobbyPlayer lobbyPlayerComp = player.GetComponent<LobbyPlayer>();
        if (lobbyPlayerComp != null)
        {
            Debug.LogError($"[LobbyPlayerSpawner] ⚠️ ВНИМАНИЕ! На игровом префабе найден компонент LobbyPlayer! Это неправильно!");
        }
        else
        {
            Debug.Log($"[LobbyPlayerSpawner] ✓ Компонент LobbyPlayer не найден (правильно)");
        }
        
        // Проверяем NetworkIdentity
        NetworkIdentity ni = player.GetComponent<NetworkIdentity>();
        if (ni != null)
        {
            Debug.Log($"[LobbyPlayerSpawner] NetworkIdentity: netId={ni.netId}, isServer={ni.isServer}, isClient={ni.isClient}, isLocalPlayer={ni.isLocalPlayer}");
        }
        
        // Отмечаем, что игрок заспавнен
        spawnedConnections.Add(conn.connectionId);
        
        Debug.Log($"[LobbyPlayerSpawner] ✓ Игрок успешно заспавнен для подключения {conn.connectionId} в позиции {spawnPosition}");
        Debug.Log($"[LobbyPlayerSpawner] Всего заспавнено игроков: {spawnedConnections.Count}");
        
        // Проверяем через небольшую задержку, что объект все еще существует
        StartCoroutine(CheckPlayerAfterSpawn(player, conn.connectionId));
    }

    /// <summary>
    /// Ожидает готовности подключения и спавнит игрока
    /// </summary>
    [Server]
    IEnumerator SpawnPlayerWhenReady(NetworkConnectionToClient conn)
    {
        if (conn == null)
        {
            yield break;
        }

        int connectionId = conn.connectionId;
        float timeout = 5f;
        float elapsed = 0f;

        Debug.Log($"[LobbyPlayerSpawner] SpawnPlayerWhenReady запущена для подключения {connectionId}");

        while (conn != null && !conn.isReady && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
            if (elapsed % 1f < 0.05f)
            {
                Debug.Log($"[LobbyPlayerSpawner] Ожидание готовности подключения {connectionId}... ({elapsed:F1}/{timeout} сек)");
            }
        }

        if (conn == null)
        {
            Debug.LogWarning($"[LobbyPlayerSpawner] Подключение {connectionId} исчезло во время ожидания готовности");
            yield break;
        }

        if (!conn.isReady)
        {
            Debug.LogError($"[LobbyPlayerSpawner] Подключение {connectionId} не стало готовым в течение {timeout} секунд. Спавн отменен.");
            yield break;
        }

        Debug.Log($"[LobbyPlayerSpawner] Подключение {connectionId} стало готовым через {elapsed:F1} секунд, повторяем попытку спавна");
        SpawnPlayer(conn);
    }

    /// <summary>
    /// Сбрасывает состояние заспавненных подключений (используется при смене сцены)
    /// </summary>
    [Server]
    public void ResetSpawnedConnections()
    {
        spawnedConnections.Clear();
    }

    /// <summary>
    /// Принудительно спавнит игрока, даже если он уже числится заспавненным (например, после смены сцены)
    /// </summary>
    [Server]
    public void ForceSpawnPlayer(NetworkConnectionToClient conn)
    {
        if (conn == null)
            return;

        spawnedConnections.Remove(conn.connectionId);
        SpawnPlayer(conn);
    }
    
    /// <summary>
    /// Проверяет объект игрока через некоторое время после спавна
    /// </summary>
    IEnumerator CheckPlayerAfterSpawn(GameObject player, int connectionId)
    {
        yield return new WaitForSeconds(1f);
        
        if (player == null)
        {
            Debug.LogError($"[LobbyPlayerSpawner] ⚠️ Игрок для подключения {connectionId} был уничтожен через 1 секунду после спавна!");
            spawnedConnections.Remove(connectionId);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == LobbyNetworkManager.Instance?.lobbySceneName)
            {
                if (NetworkServer.connections.TryGetValue(connectionId, out var conn) && conn != null)
                {
                    Debug.Log($"[LobbyPlayerSpawner] Пытаемся повторно заспавнить подключение {connectionId} после обнаружения уничтожения");
                    LobbyNetworkManager.Instance.RequestLobbyPlayerSpawn(conn);
                }
            }
        }
        else
        {
            Debug.Log($"[LobbyPlayerSpawner] Проверка через 1 сек: игрок {player.name} все еще существует");
            Debug.Log($"[LobbyPlayerSpawner] - Активен: {player.activeSelf}");
            Debug.Log($"[LobbyPlayerSpawner] - Позиция: {player.transform.position}");
            
            // Проверяем, виден ли объект в Hierarchy
            if (player.scene.IsValid())
            {
                Debug.Log($"[LobbyPlayerSpawner] - Объект в сцене: {player.scene.name}");
            }
            else
            {
                Debug.LogWarning($"[LobbyPlayerSpawner] - Объект не в сцене!");
            }
        }
    }
    
    /// <summary>
    /// Очищает список заспавненных подключений (вызывается при отключении)
    /// </summary>
    [Server]
    public void OnClientDisconnected(NetworkConnectionToClient conn)
    {
        spawnedConnections.Remove(conn.connectionId);
        PlayerCustomizationStorage.RemoveByConnectionId(conn.connectionId);
    }
}

