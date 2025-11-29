using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkManager с поддержкой FizzySteamworks для создания и присоединения к лобби через Steam
/// </summary>
public class LobbyNetworkManager : NetworkManager
{
    [Header("Steam Settings")]
    [Tooltip("Steam App ID (должен быть настроен в Steamworks)")]
    public uint steamAppID = 480; // Spacewar по умолчанию для тестирования
    
    [Header("Lobby Settings")]
    [Tooltip("Максимальное количество игроков в лобби по умолчанию")]
    public int defaultMaxPlayers = 4;
    
    [Tooltip("Сцена лобби (Lobby)")]
    public string lobbySceneName = "Lobby";
    
    [Tooltip("Сцена меню (Menu)")]
    public string menuSceneName = "Menu";
    
    [Header("Main Game Settings")]
    [Tooltip("Сцена основной игры (Main)")]
    public string mainSceneName = "Main";
    
    private static LobbyNetworkManager instance;
    
    class PendingPurchase
    {
        public uint connectionId;
        public ItemData itemData;
    }
    
    private readonly List<PendingPurchase> pendingPurchases = new List<PendingPurchase>();
    private bool mainSceneLoadInProgress;
    private readonly Dictionary<int, int> cachedConnectionCoins = new Dictionary<int, int>();
    
    // Очередь спавна игроков на сцене Lobby (ожидаем, пока соединение станет ready)
    private readonly Dictionary<int, Coroutine> lobbySpawnCoroutines = new Dictionary<int, Coroutine>();
    private const float lobbySpawnReadyTimeout = 10f;
    private Coroutine lobbyClientReadyCoroutine;
    
    public static LobbyNetworkManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyNetworkManager>();
            }
            return instance;
        }
    }
    
    public override void Awake()
    {
        base.Awake();
        instance = this;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[LobbyNetworkManager] Сервер запущен");
        mainSceneLoadInProgress = false;
        pendingPurchases.Clear();
        cachedConnectionCoins.Clear();
        
        // Регистрируем префабы для спавна (когда сервер запущен, LobbyPlayerSpawner уже должен быть на сцене)
        RegisterSpawnablePrefabs();
        
        // Обновляем список игроков после запуска сервера (с задержкой для спавна LobbyPlayer)
        if (LobbyManager.Instance != null)
        {
            Invoke(nameof(DelayedUpdatePlayerList), 1f);
        }
    }
    
    /// <summary>
    /// Регистрирует все необходимые префабы для сетевого спавна
    /// </summary>
    void RegisterSpawnablePrefabs()
    {
        // Проверяем, что префаб игрока для сцены Lobby зарегистрирован
        // Это нужно для того, чтобы клиенты могли получать объекты, заспавненные сервером
        LobbyPlayerSpawner spawner = FindObjectOfType<LobbyPlayerSpawner>();
        
        if (spawner != null && spawner.playerPrefab != null)
        {
            GameObject playerPrefab = spawner.playerPrefab;
            NetworkIdentity playerIdentity = playerPrefab.GetComponent<NetworkIdentity>();
            
            if (playerIdentity != null && playerIdentity.assetId != 0)
            {
                // Проверяем, не зарегистрирован ли уже префаб
                bool alreadyRegistered = spawnPrefabs.Contains(playerPrefab);
                
                if (!alreadyRegistered)
                {
                    // Добавляем префаб в список зарегистрированных
                    spawnPrefabs.Add(playerPrefab);
                    Debug.Log($"[LobbyNetworkManager] Префаб игрока {playerPrefab.name} (assetId: {playerIdentity.assetId}) добавлен в spawnPrefabs");
                }
                else
                {
                    Debug.Log($"[LobbyNetworkManager] Префаб игрока {playerPrefab.name} уже зарегистрирован");
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyNetworkManager] Префаб игрока {playerPrefab.name} не имеет NetworkIdentity или assetId равен 0!");
            }
        }
        else
        {
            Debug.LogWarning("[LobbyNetworkManager] LobbyPlayerSpawner или playerPrefab не найдены. Префаб игрока не будет зарегистрирован автоматически.");
            Debug.LogWarning("[LobbyNetworkManager] Убедитесь, что префаб игрока добавлен в NetworkManager.spawnPrefabs вручную в Inspector.");
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[LobbyNetworkManager] Сервер остановлен");
        pendingPurchases.Clear();
        mainSceneLoadInProgress = false;
        ClearLobbySpawnQueue();
    }
    
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[LobbyNetworkManager] Клиент подключен к серверу");
        
        // Обновляем список игроков после подключения (с задержкой для спавна LobbyPlayer)
        if (LobbyManager.Instance != null)
        {
            Invoke(nameof(DelayedUpdatePlayerList), 0.5f);
        }
    }
    
    /// <summary>
    /// Обновляет список игроков с задержкой
    /// </summary>
    void DelayedUpdatePlayerList()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("[LobbyNetworkManager] Клиент отключен от сервера");
    }
    
    /// <summary>
    /// Регистрирует покупку предмета, чтобы заспавнить его позже на сцене Main.
    /// </summary>
    [Server]
    public void RegisterPurchasedItem(NetworkConnectionToClient buyerConnection, ItemData itemData)
    {
        if (!NetworkServer.active || buyerConnection == null || itemData == null)
            return;
        
        pendingPurchases.Add(new PendingPurchase
        {
            connectionId = (uint)buyerConnection.connectionId,
            itemData = itemData
        });
        
        Debug.Log($"[LobbyNetworkManager] Зарегистрирована покупка предмета {itemData.name} для подключения {buyerConnection.connectionId}");
    }
    
    /// <summary>
    /// Спавнит купленные предметы на сцене Main в указанных точках.
    /// </summary>
    [Server]
    public void SpawnPurchasedItemsAtPoints(Transform[] spawnPoints)
    {
        if (!NetworkServer.active || pendingPurchases.Count == 0)
            return;
        
        int pointIndex = 0;
        foreach (var purchase in pendingPurchases)
        {
            if (purchase.itemData == null || purchase.itemData.itemPrefab == null)
            {
                Debug.LogWarning("[LobbyNetworkManager] Пропущен предмет без itemPrefab при спавне покупок.");
                continue;
            }
            
            Transform targetPoint = null;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                targetPoint = spawnPoints[pointIndex % spawnPoints.Length];
            }
            
            Vector3 spawnPosition = targetPoint != null ? targetPoint.position : Vector3.zero;
            Quaternion spawnRotation = targetPoint != null ? targetPoint.rotation : Quaternion.identity;
            
            GameObject spawnedItem = Instantiate(purchase.itemData.itemPrefab, spawnPosition, spawnRotation);
            NetworkServer.Spawn(spawnedItem);
            Debug.Log($"[LobbyNetworkManager] Заспавнен купленный предмет {purchase.itemData.name} в позиции {spawnPosition}");
            
            pointIndex++;
        }
        
        pendingPurchases.Clear();
    }
    
    [Server]
    public void CacheCoinsForConnection(int connectionId, int coins)
    {
        cachedConnectionCoins[connectionId] = Mathf.Max(0, coins);
    }
    
    [Server]
    public bool TryConsumeCachedCoins(int connectionId, out int coins)
    {
        if (cachedConnectionCoins.TryGetValue(connectionId, out coins))
        {
            cachedConnectionCoins.Remove(connectionId);
            return true;
        }
        
        coins = 0;
        return false;
    }
    
    /// <summary>
    /// Создает лобби через Steam
    /// </summary>
    public void CreateLobby()
    {
        Debug.Log("[LobbyNetworkManager] Создание лобби через Steam...");
        StartHost();
    }
    
    /// <summary>
    /// Присоединяется к лобби по Steam ID
    /// </summary>
    public void JoinLobby(ulong steamLobbyID)
    {
        Debug.Log($"[LobbyNetworkManager] Присоединение к лобби: {steamLobbyID}");
        networkAddress = steamLobbyID.ToString();
        StartClient();
    }
    
    /// <summary>
    /// Переходит на сцену лобби (вызывается создателем лобби)
    /// </summary>
    public void LoadLobbyScene()
    {
        if (NetworkServer.active)
        {
            Debug.Log("[LobbyNetworkManager] Загрузка сцены лобби...");
            ServerChangeScene(lobbySceneName);
        }
    }
    
    /// <summary>
    /// Помечает начало подготовки к переходу на сцену Main. Возвращает false, если переход уже выполняется.
    /// </summary>
    [Server]
    public bool TryBeginMainSceneLoad()
    {
        if (mainSceneLoadInProgress)
        {
            return false;
        }
        
        mainSceneLoadInProgress = true;
        return true;
    }
    
    /// <summary>
    /// Возвращает true, если переход на основную сцену уже запущен.
    /// </summary>
    public bool IsMainSceneLoading => mainSceneLoadInProgress;
    
    /// <summary>
    /// Переходит на сцену основной игры (Main).
    /// </summary>
    [Server]
    public void LoadMainScene()
    {
        if (!NetworkServer.active)
            return;
        
        if (!mainSceneLoadInProgress)
        {
            mainSceneLoadInProgress = true;
        }
        
        if (string.IsNullOrEmpty(mainSceneName))
        {
            Debug.LogError("[LobbyNetworkManager] mainSceneName не задан, не можем загрузить основную сцену.");
            return;
        }
        
        Debug.Log("[LobbyNetworkManager] Загрузка основной сцены...");
        ServerChangeScene(mainSceneName);
    }
    
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        Debug.Log($"[LobbyNetworkManager] Сервер перешел на сцену: {sceneName}");
        if (sceneName != lobbySceneName)
        {
            // Как только уходим со сцены Lobby, очищаем отложенные запросы на спавн
            ClearLobbySpawnQueue();
        }
        
        // Если перешли на сцену Lobby, регистрируем префабы и спавним игроков
        if (sceneName == lobbySceneName)
        {
            // Регистрируем префаб игрока (на случай, если он не был зарегистрирован ранее)
            RegisterSpawnablePrefabs();
            
            // Принудительно помечаем все подключения готовыми, чтобы спавнер не ждал isReady
            ForceAllConnectionsReadyForLobby();
            
            // Регистрируем все разрушаемые объекты из сцены для синхронизации
            RegisterDestructibleObjects();
            
            // Спавним игроков
            SpawnPlayersOnLobbyScene();
        }
        else if (sceneName == mainSceneName)
        {
            StartCoroutine(SpawnPlayersOnMainScene());
        }
    }
    
    public override void OnClientSceneChanged()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[LobbyNetworkManager] OnClientSceneChanged вызван для сцены: {currentScene}");
        Debug.Log($"[LobbyNetworkManager] autoCreatePlayer: {autoCreatePlayer}, localPlayer: {(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.name : "НЕТ")}");
        
        // На сцене Lobby НЕ спавним LobbyPlayer автоматически
        // Игровой префаб будет заспавнен через LobbyPlayerSpawner
        if (currentScene == lobbySceneName)
        {
            Debug.Log("[LobbyNetworkManager] На сцене Lobby, автоматический спавн LobbyPlayer отключен");
            
            // ВАЖНО: Отключаем autoCreatePlayer ПЕРЕД вызовом базового метода
            // Это предотвратит попытку Mirror автоматически спавнить LobbyPlayer
            bool originalAutoCreatePlayer = autoCreatePlayer;
            autoCreatePlayer = false;
            Debug.Log($"[LobbyNetworkManager] Отключили autoCreatePlayer (было: {originalAutoCreatePlayer})");
            
            // Выполняем необходимые операции вручную, но НЕ вызываем NetworkClient.AddPlayer()
            StartEnsureClientReadyForLobby();
            
            // НЕ вызываем base.OnClientSceneChanged() на сцене Lobby
            // Это предотвратит автоматический спавн LobbyPlayer
            
            // Восстанавливаем autoCreatePlayer после того, как игровой префаб заспавнен
            // (через 2 секунды, чтобы дать время LobbyPlayerSpawner заспавнить игрока)
            StartCoroutine(RestoreAutoCreatePlayer(originalAutoCreatePlayer));
            return;
        }
        
        // На других сценах (Menu) используем стандартное поведение
        Debug.Log("[LobbyNetworkManager] На сцене Menu, используем стандартное поведение");
        base.OnClientSceneChanged();
    }
    
    /// <summary>
    /// Восстанавливает autoCreatePlayer после смены сцены на Lobby
    /// </summary>
    System.Collections.IEnumerator RestoreAutoCreatePlayer(bool originalValue)
    {
        // Ждем достаточно времени, чтобы LobbyPlayerSpawner заспавнил игровой префаб
        // и чтобы NetworkClient.localPlayer был установлен
        yield return new WaitForSeconds(2f);
        
        // Проверяем, что игровой префаб заспавнен
        if (NetworkClient.localPlayer != null)
        {
            Debug.Log($"[LobbyNetworkManager] Игровой префаб заспавнен: {NetworkClient.localPlayer.name}, восстанавливаем autoCreatePlayer");
            autoCreatePlayer = originalValue;
        }
        else
        {
            Debug.LogWarning("[LobbyNetworkManager] Игровой префаб не заспавнен через 2 секунды! Не восстанавливаем autoCreatePlayer.");
            // Не восстанавливаем autoCreatePlayer, если игрок не заспавнен
        }
    }
    
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[LobbyNetworkManager] OnServerAddPlayer вызван для подключения {conn.connectionId} на сцене {currentScene}");
        
        // На сцене Lobby НЕ спавним LobbyPlayer автоматически
        // Игровой префаб будет заспавнен через LobbyPlayerSpawner
        if (currentScene == lobbySceneName)
        {
            Debug.Log("[LobbyNetworkManager] На сцене Lobby, автоматический спавн LobbyPlayer отключен. Игровой префаб будет заспавнен через LobbyPlayerSpawner.");
            // Не вызываем base.OnServerAddPlayer() на сцене Lobby
            return;
        }
        
        // На сцене Menu спавним LobbyPlayer как обычно
        Debug.Log("[LobbyNetworkManager] На сцене Menu, спавним LobbyPlayer");
        base.OnServerAddPlayer(conn);
    }
    
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn != null)
        {
            cachedConnectionCoins.Remove(conn.connectionId);
        }
        base.OnServerDisconnect(conn);
    }
    
    /// <summary>
    /// Регистрирует все разрушаемые объекты из сцены для синхронизации в мультиплеере
    /// </summary>
    void RegisterDestructibleObjects()
    {
        Debug.Log("[LobbyNetworkManager] Регистрация разрушаемых объектов из сцены...");
        
        // Находим все объекты с DestructibleObject
        DestructibleObject[] destructibleObjects = FindObjectsOfType<DestructibleObject>();
        Debug.Log($"[LobbyNetworkManager] Найдено {destructibleObjects.Length} разрушаемых объектов");
        
        foreach (DestructibleObject destructible in destructibleObjects)
        {
            if (destructible == null) continue;
            
            GameObject obj = destructible.gameObject;
            
            // Проверяем, есть ли уже NetworkIdentity
            NetworkIdentity networkIdentity = obj.GetComponent<NetworkIdentity>();
            
            if (networkIdentity == null)
            {
                // Добавляем NetworkIdentity если его нет
                networkIdentity = obj.AddComponent<NetworkIdentity>();
                Debug.Log($"[LobbyNetworkManager] Добавлен NetworkIdentity на {obj.name}");
            }
            
            // Проверяем, есть ли NetworkDestructibleObject
            NetworkDestructibleObject networkDestructible = obj.GetComponent<NetworkDestructibleObject>();
            if (networkDestructible == null)
            {
                // Добавляем NetworkDestructibleObject для синхронизации
                networkDestructible = obj.AddComponent<NetworkDestructibleObject>();
                Debug.Log($"[LobbyNetworkManager] Добавлен NetworkDestructibleObject на {obj.name}");
            }
            
            // Проверяем, есть ли NetworkTransform для синхронизации позиции
            // Используем NetworkTransformReliable для надежной синхронизации
            if (obj.GetComponent<Mirror.NetworkTransformReliable>() == null && 
                obj.GetComponent<Mirror.NetworkTransformUnreliable>() == null &&
                obj.GetComponent<Mirror.NetworkTransformHybrid>() == null)
            {
                // Убеждаемся, что объект активен перед добавлением компонента
                // NetworkTransform требует активный объект для правильной инициализации
                bool wasActive = obj.activeSelf;
                if (!wasActive)
                {
                    obj.SetActive(true);
                }
                
                // Добавляем NetworkTransformReliable для синхронизации позиции и поворота
                var networkTransform = obj.AddComponent<Mirror.NetworkTransformReliable>();
                
                // Устанавливаем target явно ДО того, как компонент начнет работать
                // Это предотвращает NullReferenceException в LateUpdate
                networkTransform.target = obj.transform;
                
                // Настраиваем параметры синхронизации
                networkTransform.syncPosition = true;
                networkTransform.syncRotation = true;
                networkTransform.syncScale = false;
                
                // Восстанавливаем состояние активности, если нужно
                // Но только если объект не должен быть активен
                // (обычно объекты должны быть активны для синхронизации)
                // if (!wasActive)
                // {
                //     obj.SetActive(false);
                // }
                
                Debug.Log($"[LobbyNetworkManager] Добавлен NetworkTransformReliable на {obj.name}, target: {networkTransform.target?.name ?? "null"}");
            }
            
            // Спавним объект если он еще не заспавнен (netId == 0 означает что объект не заспавнен)
            if (networkIdentity.netId == 0)
            {
                // Если это объект из сцены (sceneId != 0), спавним его напрямую
                if (networkIdentity.sceneId != 0)
                {
                    NetworkServer.Spawn(obj);
                    Debug.Log($"[LobbyNetworkManager] Заспавнен объект из сцены: {obj.name} (sceneId: {networkIdentity.sceneId})");
                }
                else if (networkIdentity.assetId != 0)
                {
                    // Если это префаб (assetId != 0), спавним его
                    NetworkServer.Spawn(obj);
                    Debug.Log($"[LobbyNetworkManager] Заспавнен префаб: {obj.name} (assetId: {networkIdentity.assetId})");
                }
                else
                {
                    // Если нет ни sceneId, ни assetId, объект не может быть заспавнен автоматически
                    // В этом случае нужно либо добавить sceneId вручную в Unity, либо использовать префаб
                    Debug.LogWarning($"[LobbyNetworkManager] Объект {obj.name} не может быть заспавнен автоматически: нет sceneId и assetId. Добавьте NetworkIdentity вручную в Unity или используйте префаб.");
                }
            }
        }
        
        Debug.Log("[LobbyNetworkManager] Регистрация разрушаемых объектов завершена");
    }
    
    /// <summary>
    /// Спавнит всех подключенных игроков на сцене Lobby
    /// </summary>
    void SpawnPlayersOnLobbyScene()
    {
        Debug.Log("[LobbyNetworkManager] SpawnPlayersOnLobbyScene вызван");
        Debug.Log($"[LobbyNetworkManager] NetworkServer.active: {NetworkServer.active}");
        Debug.Log($"[LobbyNetworkManager] Текущая сцена: {SceneManager.GetActiveScene().name}");
        
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[LobbyNetworkManager] Сервер не активен, не можем спавнить игроков");
            return;
        }
        
        Debug.Log($"[LobbyNetworkManager] Количество подключений: {NetworkServer.connections.Count}");
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn != null)
            {
                Debug.Log($"[LobbyNetworkManager] Подключение {conn.connectionId}: isReady={conn.isReady}");
            }
        }
        
        // Ждем немного, чтобы сцена полностью загрузилась
        Debug.Log("[LobbyNetworkManager] Запускаем задержку 0.5 сек перед спавном игроков");
        Invoke(nameof(SpawnPlayersDelayed), 0.5f);
    }
    
    /// <summary>
    /// Спавнит игроков с задержкой после загрузки сцены
    /// </summary>
    void SpawnPlayersDelayed()
    {
        Debug.Log("[LobbyNetworkManager] SpawnPlayersDelayed вызван");
        Debug.Log($"[LobbyNetworkManager] NetworkServer.active: {NetworkServer.active}");
        Debug.Log($"[LobbyNetworkManager] Текущая сцена: {SceneManager.GetActiveScene().name}");
        
        if (!NetworkServer.active)
        {
            Debug.LogError("[LobbyNetworkManager] Сервер не активен в SpawnPlayersDelayed!");
            return;
        }
        
        Debug.Log("[LobbyNetworkManager] Ищем LobbyPlayerSpawner на сцене...");
        LobbyPlayerSpawner spawner = FindObjectOfType<LobbyPlayerSpawner>();
        if (spawner == null)
        {
            Debug.LogError("[LobbyNetworkManager] LobbyPlayerSpawner не найден на сцене Lobby! Убедитесь, что он добавлен на сцену и имеет NetworkIdentity.");
            Debug.LogError("[LobbyNetworkManager] Проверьте, что GameObject с LobbyPlayerSpawner активен на сцене Lobby");
            return;
        }
        
        Debug.Log($"[LobbyNetworkManager] ✓ LobbyPlayerSpawner найден: {spawner.gameObject.name}");
        Debug.Log($"[LobbyNetworkManager] Начинаем спавн игроков. Подключений: {NetworkServer.connections.Count}");
        
        if (NetworkServer.connections.Count == 0)
        {
            Debug.LogWarning("[LobbyNetworkManager] Нет подключенных клиентов! Проверьте, что сервер запущен и клиенты подключены.");
        }
        
        // Спавним игроков для всех подключенных клиентов
        int spawnedCount = 0;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn != null)
            {
                Debug.Log($"[LobbyNetworkManager] Обрабатываем подключение {conn.connectionId} (isReady: {conn.isReady})");
                EnsureConnectionReadyForLobby(conn);
                if (conn.isReady)
                {
                    Debug.Log($"[LobbyNetworkManager] Подключение {conn.connectionId} готово, спавним немедленно");
                    spawner.SpawnPlayer(conn);
                    spawnedCount++;
                }
                else
                {
                    Debug.LogWarning($"[LobbyNetworkManager] Подключение {conn.connectionId} еще не готово. Добавляем в очередь ожидания готовности.");
                    RequestLobbyPlayerSpawn(conn);
                }
            }
            else
            {
                Debug.LogWarning("[LobbyNetworkManager] Найдено null подключение в списке!");
            }
        }
        
        Debug.Log($"[LobbyNetworkManager] Обработано подключений: {spawnedCount}");
    }
    
    System.Collections.IEnumerator SpawnPlayersOnMainScene()
    {
        yield return null;
        yield return null;
        
        LobbyPlayerSpawner spawner = FindObjectOfType<LobbyPlayerSpawner>();
        if (spawner != null)
        {
            spawner.ResetSpawnedConnections();
        }

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null)
                continue;
            
            if (conn.identity != null)
            {
                // Монеты теперь сохраняются в PlayerPrefs и не требуют кеширования
                NetworkServer.Destroy(conn.identity.gameObject);
            }
            
            if (spawner != null)
            {
                spawner.ForceSpawnPlayer(conn);
            }
            else
            {
                Transform startPosition = GetStartPosition();
                Vector3 spawnPosition = startPosition != null ? startPosition.position : Vector3.zero;
                Quaternion spawnRotation = startPosition != null ? startPosition.rotation : Quaternion.identity;
                
                GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
                NetworkServer.AddPlayerForConnection(conn, player);
            }
        }
    }

    /// <summary>
    /// Запрашивает спавн игрока на сцене Lobby как только соединение станет ready.
    /// </summary>
    public void RequestLobbyPlayerSpawn(NetworkConnectionToClient conn)
    {
        if (conn == null)
            return;

        LobbyPlayerSpawner spawner = LobbyPlayerSpawner.Instance;
        if (spawner == null)
        {
            Debug.LogWarning("[LobbyNetworkManager] RequestLobbyPlayerSpawn: LobbyPlayerSpawner еще не доступен. Попробуем позже.");
            return;
        }

        if (conn.isReady)
        {
            Debug.Log($"[LobbyNetworkManager] RequestLobbyPlayerSpawn: подключение {conn.connectionId} уже готово, спавним сразу.");
            spawner.SpawnPlayer(conn);
            return;
        }

        if (lobbySpawnCoroutines.ContainsKey(conn.connectionId))
        {
            Debug.Log($"[LobbyNetworkManager] RequestLobbyPlayerSpawn: подключение {conn.connectionId} уже ожидает готовности.");
            return;
        }

        Debug.Log($"[LobbyNetworkManager] RequestLobbyPlayerSpawn: запускаем ожидание готовности для подключения {conn.connectionId}.");
        lobbySpawnCoroutines[conn.connectionId] = StartCoroutine(WaitForLobbyReadyAndSpawn(conn));
    }

    IEnumerator WaitForLobbyReadyAndSpawn(NetworkConnectionToClient conn)
    {
        if (conn == null)
            yield break;

        int connectionId = conn.connectionId;
        float elapsed = 0f;

        while (conn != null && !conn.isReady && elapsed < lobbySpawnReadyTimeout)
        {
            EnsureConnectionReadyForLobby(conn);
            yield return null;
            elapsed += Time.deltaTime;
        }

        lobbySpawnCoroutines.Remove(connectionId);

        if (conn == null)
        {
            Debug.LogWarning($"[LobbyNetworkManager] Подключение {connectionId} исчезло до готовности, спавн отменен.");
            yield break;
        }

        if (!conn.isReady)
        {
            Debug.LogError($"[LobbyNetworkManager] Подключение {connectionId} не стало готовым за {lobbySpawnReadyTimeout} секунд, спавн отменен.");
            yield break;
        }

        if (SceneManager.GetActiveScene().name != lobbySceneName)
        {
            Debug.LogWarning($"[LobbyNetworkManager] Сцена уже не Lobby при спавне подключения {connectionId}, пропускаем.");
            yield break;
        }

        LobbyPlayerSpawner spawner = LobbyPlayerSpawner.Instance;
        if (spawner == null)
        {
            Debug.LogError("[LobbyNetworkManager] LobbyPlayerSpawner не найден после ожидания готовности, спавн невозможен.");
            yield break;
        }

        Debug.Log($"[LobbyNetworkManager] Подключение {connectionId} стало готовым спустя {elapsed:F1} сек, спавним игрока.");
        spawner.SpawnPlayer(conn);
    }

    void ClearLobbySpawnQueue()
    {
        foreach (var coroutine in lobbySpawnCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        lobbySpawnCoroutines.Clear();
    }

    void EnsureConnectionReadyForLobby(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.isReady || !conn.isAuthenticated)
            return;

        if (conn is LocalConnectionToClient)
        {
            Debug.Log($"[LobbyNetworkManager] Помечаем локальное подключение {conn.connectionId} готовым на сцене Lobby");
            NetworkServer.SetClientReady(conn);
        }
    }

    void ForceAllConnectionsReadyForLobby()
    {
        foreach (var kvp in NetworkServer.connections)
        {
            var conn = kvp.Value;
            if (conn == null)
                continue;

            EnsureConnectionReadyForLobby(conn);
        }
    }

    void StartEnsureClientReadyForLobby()
    {
        if (lobbyClientReadyCoroutine != null)
        {
            StopCoroutine(lobbyClientReadyCoroutine);
        }
        lobbyClientReadyCoroutine = StartCoroutine(EnsureClientReadyForLobbyRoutine());
    }

    IEnumerator EnsureClientReadyForLobbyRoutine()
    {
        const float timeout = 15f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
            {
                if (!NetworkClient.ready)
                {
                    Debug.Log("[LobbyNetworkManager] Client authenticated on Lobby scene, calling NetworkClient.Ready()");
                    NetworkClient.Ready();
                }
                lobbyClientReadyCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        lobbyClientReadyCoroutine = null;
        Debug.LogWarning("[LobbyNetworkManager] Не удалось дождаться аутентификации клиента для Ready на сцене Lobby (тайм-аут 15 сек)");
    }
}

