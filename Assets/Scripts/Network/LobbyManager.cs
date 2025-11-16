using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Главный менеджер лобби. Управляет созданием лобби, подключением и отображением игроков.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    private static LobbyManager instance;
    public static LobbyManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyManager>();
            }
            return instance;
        }
    }

    [Header("Кнопки")]
    [Tooltip("Кнопка 'Настройки лобби' - открывает панель настроек (только для админа)")]
    public Button lobbySettingsButton;
    
    [Tooltip("Кнопка 'Начать игру' - загружает сцену игры (только для админа)")]
    public Button startGameButton;
    
    [Tooltip("GameObject панели поиска лобби")]
    public GameObject lobbySearchPanel;
    
    [Tooltip("Кнопка для показа/скрытия панели поиска лобби")]
    public Button showLobbySearchButton;
    
    [Tooltip("Кнопка для открытия панели выбора цвета")]
    public Button colorSelectionButton;

    [Header("UI Панели")]
    [Tooltip("Панель настроек лобби")]
    public GameObject lobbySettingsPanel;
    
    [Header("Поиск лобби")]
    [Tooltip("InputField для поиска лобби по никнейму хоста")]
    public InputField lobbySearchInputField;
    
    [Tooltip("Transform контейнер для списка найденных лобби")]
    public Transform lobbySearchResultsContainer;
    
    [Tooltip("Префаб найденного лобби")]
    public GameObject lobbySearchItemPrefab;
    
    [Tooltip("Панель выбора цвета")]
    public GameObject colorSelectionPanel;

    [Header("Отображение игроков")]
    [Tooltip("Transform контейнер для списка игроков в лобби")]
    public Transform playersListContainer;
    
    [Tooltip("Префаб игрока в лобби")]
    public GameObject playerLobbyPrefab;

    [Header("Настройки сети")]
    [Tooltip("Максимальное количество игроков")]
    public int maxPlayers = 8;
    
    [Header("Загрузка сцены")]
    [Tooltip("Компонент AsyncSceneLoaderWithAnimation для асинхронной загрузки сцены с анимацией")]
    public AsyncSceneLoaderWithAnimation sceneLoader;
    
    [Tooltip("Имя сцены для загрузки при нажатии 'Начать игру'")]
    public string gameSceneName = "Lobby";
    
    [Header("Статус подключения")]
    [Tooltip("Текст для отображения статуса подключения к лобби")]
    public Text statusText;

    private MirrorNetworkManager networkManager;
    private MonoBehaviour transport; // FizzySteamworks transport
    public Dictionary<uint, GameObject> playerLobbyItems = new Dictionary<uint, GameObject>(); // Публичное для доступа из LobbyPlayerSync
    private bool isRTTUpdateRunning = false; // Флаг для предотвращения множественных корутин обновления RTT
    private LobbyPlayerSync playerSync; // Компонент для синхронизации списка игроков

    void Awake()
    {
        // Singleton pattern - уничтожаем дубликаты
        if (instance == null)
        {
            instance = this;
            // Сохраняем LobbyManager между сценами
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning("[LobbyManager] Обнаружен дубликат LobbyManager! Уничтожаем...");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SetupButtons();
        
        // Находим AsyncSceneLoaderWithAnimation если он не назначен
        if (sceneLoader == null)
        {
            sceneLoader = FindObjectOfType<AsyncSceneLoaderWithAnimation>();
        }

        // Скрываем панели по умолчанию
        if (lobbySettingsPanel != null)
            lobbySettingsPanel.SetActive(false);
        
        if (lobbySearchPanel != null)
            lobbySearchPanel.SetActive(false);
        
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);

        // Пытаемся инициализировать NetworkManager
        InitializeNetworkManager();
        
        // Находим или создаем LobbyPlayerSync компонент
        playerSync = GetComponent<LobbyPlayerSync>();
        if (playerSync == null)
        {
            playerSync = gameObject.AddComponent<LobbyPlayerSync>();
            Debug.Log("[LobbyManager] Создан компонент LobbyPlayerSync для синхронизации списка игроков");
        }
        
        // Подписываемся на смену сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        UpdateUI();
        
        // Запускаем периодическую синхронизацию через Steam API (резервный вариант)
        // ВАЖНО: Используется только когда Mirror не активен
        StartCoroutine(SyncPlayersFromSteamLobby());
        
        // Подписываемся на события отключения для очистки состояния
        if (networkManager != null)
        {
            // События обрабатываются через MirrorNetworkManager
        }
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Отписываемся от событий SteamLobbyManager
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager != null)
        {
            steamLobbyManager.OnLobbiesFound -= OnLobbiesFound;
        }
        
        if (networkManager != null)
        {
            // В Mirror события обрабатываются через переопределение методов в MirrorNetworkManager
            // Подписки на события не требуются
        }
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string currentScene = scene.name;
        
        // Если мы в сцене Lobby, синхронизируем список игроков
        if (currentScene == gameSceneName)
        {
            // Если мы сервер, создаем список игроков для всех подключенных
            if (NetworkServer.active)
            {
                StartCoroutine(SyncPlayersListAfterSceneLoad());
            }
            
            // Если мы клиент, синхронизируем список игроков из NetworkPlayer
            if (NetworkClient.active)
            {
                StartCoroutine(SyncPlayersFromNetworkPlayers());
            }
        }
    }
    
    /// <summary>
    /// Синхронизирует список игроков из NetworkPlayer на клиенте
    /// В сцене Menu игроки могут не быть заспавнены, поэтому используем альтернативный подход
    /// </summary>
    System.Collections.IEnumerator SyncPlayersFromNetworkPlayers()
    {
        // Ждем немного, чтобы подключение установилось
        yield return new WaitForSeconds(0.5f);
        
        if (!NetworkClient.active) 
        {
            Debug.LogWarning("[LobbyManager] SyncPlayersFromNetworkPlayers: NetworkClient не активен!");
            yield break;
        }
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[LobbyManager] SyncPlayersFromNetworkPlayers: Текущая сцена = {currentScene}");
        
        // В сцене Menu игроки могут не быть заспавнены, поэтому используем данные из подключений
        // Пытаемся найти NetworkPlayer объекты
        NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
        
        Debug.Log($"[LobbyManager] SyncPlayersFromNetworkPlayers: Найдено NetworkPlayer объектов: {allPlayers.Length}");
        
        if (allPlayers.Length > 0)
        {
            // Если нашли NetworkPlayer объекты, используем их
            foreach (NetworkPlayer player in allPlayers)
            {
                if (player != null && player.netIdentity != null && player.netIdentity.netId != 0)
                {
                    uint playerId = player.PlayerId;
                    
                    // Если PlayerId = 0 (на клиенте connectionToClient = null), используем connectionId из NetworkClient
                    if (playerId == 0 && NetworkClient.active && !NetworkServer.active)
                    {
                        if (player.isOwned)
                        {
                            if (NetworkClient.connection != null)
                            {
                                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                                if (connectionIdField != null)
                                {
                                    playerId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                                }
                            }
                        }
                        else
                        {
                            // Для других игроков на клиенте используем netId как временный идентификатор
                            playerId = player.netIdentity.netId;
                        }
                    }
                    
                    string playerName = player.PlayerName;
                    Color playerColor = player.PlayerColor;
                    
                    Debug.Log($"[LobbyManager] Найден NetworkPlayer: ID={playerId}, Name={playerName}, Color={playerColor}");
                    
                    bool isAdmin = false;
                    uint localClientId = 0;
                    if (NetworkClient.connection != null)
                    {
                        var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                        if (connectionIdField != null)
                        {
                            localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                        }
                    }
                    
            // Определяем, является ли игрок админом (хостом)
            // Хост всегда имеет connectionId = 0 или является локальным клиентом на сервере
            if (NetworkServer.active && NetworkClient.active)
            {
                // Мы хост - проверяем, является ли это локальный клиент
                if (playerId == 0 || (playerId == localClientId && localClientId == 0))
                    {
                        isAdmin = true;
                    }
            }
            else if (playerId == 0 && NetworkServer.active)
                    {
                // На сервере connectionId = 0 означает хост
                        isAdmin = true;
                    }
                    
                    if (!playerLobbyItems.ContainsKey(playerId))
                    {
                        Debug.Log($"[LobbyManager] Создаем PlayerLobbyItem для ID={playerId}, Name={playerName}, isAdmin={isAdmin}");
                        CreatePlayerLobbyItemLocally(playerId, isAdmin, playerName, playerColor);
                    }
                    else
                    {
                        UpdatePlayerDataAndSync(playerId, isAdmin, playerName, playerColor);
                    }
                }
            }
            
            Debug.Log($"[LobbyManager] ✓ Синхронизация завершена через NetworkPlayer. Создано PlayerLobbyItem: {playerLobbyItems.Count}");
        }
        else
        {
            // Если NetworkPlayer объекты не найдены (например, в сцене Menu)
            // На клиенте мы не можем получить свой connectionId напрямую
            // Поэтому не создаем PlayerLobbyItem здесь - сервер должен отправить список игроков
            Debug.Log("[LobbyManager] NetworkPlayer объекты не найдены. На клиенте ждем данные от сервера...");
            
            // Проверяем, являемся ли мы хостом или удаленным клиентом
            // Хост = NetworkServer.active && NetworkClient.active (запустили сервер и подключились к нему)
            // Удаленный клиент = NetworkClient.active && !NetworkServer.active (подключились к серверу, но не запустили его)
            // ВАЖНО: Проверяем тип подключения - LocalConnectionToServer означает локальное подключение хоста
            // НО: Если NetworkServer.active = false, мы точно не хост, даже если есть LocalConnectionToServer
            bool isLocalConnection = NetworkClient.connection != null && 
                                     NetworkClient.connection.GetType().Name == "LocalConnectionToServer";
            // Хост = сервер активен И клиент активен И это локальное подключение
            // Если NetworkServer.active = false, мы точно не хост
            bool isHost = NetworkServer.active && NetworkClient.active && isLocalConnection;
            // Удаленный клиент = клиент активен И сервер НЕ активен
            bool isClientOnly = NetworkClient.active && !NetworkServer.active;
            
            Debug.Log($"[LobbyManager] SyncPlayersFromNetworkPlayers: isHost={isHost}, isClientOnly={isClientOnly}, NetworkServer.active={NetworkServer.active}, NetworkClient.active={NetworkClient.active}, isLocalConnection={isLocalConnection}, connectionType={NetworkClient.connection?.GetType().Name ?? "null"}");
            
            if (isHost)
            {
                // Мы хост - создаем PlayerLobbyItem для себя
                uint localClientId = 0; // Хост всегда имеет connectionId = 0
                string localPlayerName = GetLocalPlayerSteamName();
                Color localPlayerColor = Color.white;
                if (PlayerPrefs.HasKey("PlayerColor_R") && PlayerPrefs.HasKey("PlayerColor_G") && 
                    PlayerPrefs.HasKey("PlayerColor_B") && PlayerPrefs.HasKey("PlayerColor_A"))
                {
                    localPlayerColor = new Color(
                        PlayerPrefs.GetFloat("PlayerColor_R", 0.05f),
                        PlayerPrefs.GetFloat("PlayerColor_G", 0.82f),
                        PlayerPrefs.GetFloat("PlayerColor_B", 0.27f),
                        PlayerPrefs.GetFloat("PlayerColor_A", 1f)
                    );
                }
                
                Debug.Log($"[LobbyManager] Создаем PlayerLobbyItem для хоста: ID={localClientId}, Name={localPlayerName}");
                CreatePlayerLobbyItemLocally(localClientId, true, localPlayerName, localPlayerColor);
            }
            else if (isClientOnly)
            {
                // Мы удаленный клиент (не хост) - не создаем PlayerLobbyItem, ждем данные от сервера
                Debug.Log("[LobbyManager] На клиенте ждем синхронизацию списка игроков от сервера через LobbyPlayerSync...");
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] Неопределенное состояние: NetworkServer.active={NetworkServer.active}, NetworkClient.active={NetworkClient.active}, isLocalConnection={isLocalConnection}");
            }
        }
    }
    
    System.Collections.IEnumerator SyncPlayersListAfterSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (!NetworkServer.active) yield break;
        
        // Синхронизируем список игроков для всех подключенных клиентов
        // ВАЖНО: Создаем копию коллекции, чтобы избежать ошибок при изменении во время итерации
        var connectionsCopy = new System.Collections.Generic.List<NetworkConnectionToClient>();
        foreach (var connection in NetworkServer.connections.Values)
        {
            if (connection != null)
            {
                connectionsCopy.Add(connection);
            }
        }
        
        foreach (var connection in connectionsCopy)
        {
            if (connection != null && connection.isReady)
            {
                uint connId = (uint)connection.connectionId;
                CreatePlayerLobbyItem(connId);
            }
        }
    }
    
    /// <summary>
    /// Периодически синхронизирует список игроков через Steam API (резервный вариант, если Mirror не видит подключения)
    /// ВАЖНО: Основная синхронизация должна происходить через Mirror Network (LobbyPlayerSync)
    /// Этот метод используется только как резервный вариант, когда Mirror не синхронизирует данные
    /// </summary>
    System.Collections.IEnumerator SyncPlayersFromSteamLobby()
    {
        while (gameObject != null)
        {
            yield return new WaitForSeconds(10f); // Проверяем каждые 10 секунд (увеличено, чтобы не конфликтовать с Mirror)
            
            // Проверяем, что объект все еще активен
            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            {
                break;
            }
            
            #if !DISABLESTEAMWORKS
            // ВАЖНО: Если Mirror активен, НЕ используем Steam API синхронизацию
            // Это предотвращает конфликты и дублирование данных
            // Mirror синхронизация имеет приоритет
            if (NetworkServer.active || NetworkClient.active)
            {
                // Если Mirror активен, используем только Mirror синхронизацию
                // Steam API синхронизация используется только если Mirror не активен
                continue;
            }
            
            SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
            if (steamLobbyManager == null) continue;
            
            ulong lobbyId = steamLobbyManager.GetCurrentLobbyId();
            if (lobbyId == 0) continue; // Не в лобби
            
            // Получаем список игроков из Steam лобби
            var members = steamLobbyManager.GetLobbyMembers();
            if (members == null || members.Count == 0) continue;
            
            // Получаем Steam ID владельца лобби (хоста)
            ulong ownerSteamId = steamLobbyManager.GetLobbyOwnerId();
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
            bool isHost = (ownerSteamId == mySteamId);
            
            // ВАЖНО: Если Mirror активен, не используем Steam API синхронизацию
            // Это предотвращает конфликты между Mirror и Steam API синхронизацией
            if (NetworkServer.active || NetworkClient.active)
            {
                continue;
            }
            
            // ВАЖНО: Этот код выполняется только если Mirror НЕ активен
            // Если Mirror активен, синхронизация происходит через LobbyPlayerSync
            // Создаем или обновляем PlayerLobbyItem для каждого игрока
            foreach (var member in members)
            {
                ulong memberSteamId = member.Item1;
                string memberName = member.Item2;
                
                // Используем Steam ID как уникальный идентификатор
                // Для хоста используем 0, для других - хеш от Steam ID
                uint connectionId = 0;
                if (memberSteamId == ownerSteamId)
                {
                    connectionId = 0; // Хост всегда имеет connectionId = 0
                }
                else
                {
                    // Для клиентов используем хеш от Steam ID (первые 4 байта)
                    connectionId = (uint)(memberSteamId & 0xFFFFFFFF);
                }
                
                bool isAdmin = (memberSteamId == ownerSteamId);
                
                // Получаем цвет игрока из NetworkPlayer, если он есть, иначе из PlayerPrefs
                Color playerColor = Color.white;
                
                // Пытаемся найти NetworkPlayer для этого игрока
                NetworkPlayer networkPlayer = null;
                NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
                foreach (NetworkPlayer player in allPlayers)
                {
                    if (player != null && player.netIdentity != null && player.netIdentity.netId != 0)
                    {
                        uint playerId = player.PlayerId;
                        if (playerId == connectionId || (connectionId == 0 && isHost && player.isOwned))
                        {
                            networkPlayer = player;
                            break;
                        }
                    }
                }
                
                if (networkPlayer != null)
                {
                    // Используем цвет из NetworkPlayer (синхронизированный)
                    playerColor = networkPlayer.PlayerColor;
                }
                else if (memberSteamId == mySteamId)
                {
                    // Для себя используем сохраненный цвет из PlayerPrefs
                    if (PlayerPrefs.HasKey("PlayerColor_R") && PlayerPrefs.HasKey("PlayerColor_G") && 
                        PlayerPrefs.HasKey("PlayerColor_B") && PlayerPrefs.HasKey("PlayerColor_A"))
                    {
                        playerColor = new Color(
                            PlayerPrefs.GetFloat("PlayerColor_R", 0.05f),
                            PlayerPrefs.GetFloat("PlayerColor_G", 0.82f),
                            PlayerPrefs.GetFloat("PlayerColor_B", 0.27f),
                            PlayerPrefs.GetFloat("PlayerColor_A", 1f)
                        );
                    }
                }
                // Для других игроков оставляем белый цвет, если NetworkPlayer не найден
                
                // Создаем или обновляем PlayerLobbyItem
                // ВАЖНО: Это резервный вариант, основная синхронизация через Mirror
                if (!playerLobbyItems.ContainsKey(connectionId))
                {
                    Debug.Log($"[LobbyManager] [Steam API Sync] Создание PlayerLobbyItem для {memberName} (connectionId={connectionId})");
                    CreatePlayerLobbyItemLocally(connectionId, isAdmin, memberName, playerColor);
                }
                else
                {
                    // Обновляем существующий PlayerLobbyItem только если цвет изменился или имя изменилось
                    GameObject playerItem = playerLobbyItems[connectionId];
                    if (playerItem != null)
                    {
                        PlayerLobbyItem item = playerItem.GetComponent<PlayerLobbyItem>();
                        if (item != null)
                        {
                            // Обновляем только имя и админ статус, цвет обновляем только если он не белый (чтобы не перезаписывать синхронизированный цвет)
                            if (playerColor != Color.white || networkPlayer != null)
                            {
                                item.Initialize(connectionId, isAdmin, memberName, playerColor);
                            }
                            else
                            {
                                // Обновляем только имя и админ статус, цвет не трогаем
                                item.Initialize(connectionId, isAdmin, memberName, item.PlayerColor);
                            }
                        }
                    }
                }
            }
            #endif
        }
    }
    
    void OnEnable()
    {
        if (networkManager != null)
        {
            SetupNetworkCallbacks();
        }
    }

    void InitializeNetworkManager()
    {
        networkManager = MirrorNetworkManager.Instance;
        if (networkManager == null)
        {
            // NetworkManager может быть не создан в сцене - это нормально
            // Попробуем найти его позже
            Debug.LogWarning("MirrorNetworkManager не найден. Инициализация будет выполнена позже.");
            StartCoroutine(TryInitializeNetworkManager());
            return;
        }

        // Проверяем, используется ли FizzySteamworks транспорт
        // Находим FizzySteamworks транспорт через рефлексию
        System.Type transportType = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            transportType = assembly.GetType("FizzySteamworks") ?? 
                           assembly.GetType("com.mirror.steamworks.net.FizzySteamworks");
            if (transportType != null) break;
        }
        
        if (transportType != null)
        {
            transport = networkManager.GetComponent(transportType) as MonoBehaviour;
        }
        
        if (transport == null)
        {
            // Пытаемся найти по имени
            Component[] allComponents = networkManager.GetComponents<Component>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().Name == "FizzySteamworks")
                {
                    transport = comp as MonoBehaviour;
                    break;
                }
            }
        }
        if (transport == null)
        {
            Debug.LogWarning("[LobbyManager] FizzySteamworks транспорт не найден на NetworkManager! Добавьте компонент FizzySteamworks.");
            return;
        }
        
        Debug.Log("[LobbyManager] Используется FizzySteamworks транспорт");

        SetupNetworkCallbacks();
    }

    System.Collections.IEnumerator TryInitializeNetworkManager()
    {
        // Ждем несколько кадров и пытаемся найти NetworkManager снова
        yield return new WaitForSeconds(0.1f);
        
        int attempts = 0;
        while (networkManager == null && attempts < 50)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager != null)
            {
                // Проверяем, используется ли FizzySteamworks транспорт
                // Находим FizzySteamworks транспорт через рефлексию
            System.Type transportType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                transportType = assembly.GetType("FizzySteamworks") ?? 
                               assembly.GetType("com.mirror.steamworks.net.FizzySteamworks");
                if (transportType != null) break;
            }
            
            if (transportType != null)
            {
                transport = networkManager.GetComponent(transportType) as MonoBehaviour;
            }
            
            if (transport == null)
            {
                // Пытаемся найти по имени
                Component[] allComponents = networkManager.GetComponents<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp.GetType().Name == "FizzySteamworks")
                    {
                        transport = comp as MonoBehaviour;
                        break;
                    }
                }
            }
                if (transport != null)
                {
                    Debug.Log("[LobbyManager] Используется FizzySteamworks транспорт");
                    SetupNetworkCallbacks();
                    UpdateUI();
                    Debug.Log("MirrorNetworkManager найден и инициализирован!");
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (networkManager == null)
        {
            Debug.LogWarning("MirrorNetworkManager не найден после нескольких попыток. Убедитесь, что MirrorNetworkManager добавлен в сцену.");
        }
    }

    void SetupButtons()
    {
        if (lobbySettingsButton != null)
            lobbySettingsButton.onClick.AddListener(OnLobbySettingsButtonClicked);
        
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        
        if (colorSelectionButton != null)
            colorSelectionButton.onClick.AddListener(OnColorSelectionButtonClicked);
        
        if (showLobbySearchButton != null)
            showLobbySearchButton.onClick.AddListener(OnShowLobbySearchButtonClicked);
        
        // Настраиваем поиск лобби
        SetupLobbySearch();
    }
    
    void SetupLobbySearch()
    {
        if (lobbySearchInputField != null)
        {
            // Добавляем обработчик для поиска при нажатии Enter
            lobbySearchInputField.onEndEdit.AddListener(OnLobbySearchInputEndEdit);
        }
        
        // Подписываемся на события SteamLobbyManager для получения результатов поиска
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager != null)
        {
            steamLobbyManager.OnLobbiesFound += OnLobbiesFound;
        }
    }

    void SetupNetworkCallbacks()
    {
        // События Mirror обрабатываются автоматически через виртуальные методы
    }
    
    // Mirror не использует ConnectionApproval как Unity Netcode
    // В Mirror подключения обрабатываются через OnServerConnect и OnClientConnect

    /// <summary>
    /// Создает Steam лобби (которое затем создаст Unity Netcode лобби)
    /// </summary>
    void CreateSteamLobby()
    {
        SteamLobbyManager steamLobbyManager = FindObjectOfType<SteamLobbyManager>();
        
        #if !DISABLESTEAMWORKS
        if (steamLobbyManager != null && SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
        {
            // Создаем Steam лобби
            steamLobbyManager.CreateLobby();
        }
        else
        {
            Debug.LogError("[LobbyManager] Steam не инициализирован или SteamLobbyManager не найден!");
        }
        #else
        // Если Steam не доступен, создаем обычное лобби
        Debug.LogWarning("[LobbyManager] Steam не доступен, создаем обычное лобби без Steam");
        CreateLobby();
        #endif
    }

    /// <summary>
    /// Публичный метод для создания лобби (вызывается из других скриптов)
    /// </summary>
    public void CreateLobby()
    {
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект LobbyManager неактивен! Невозможно создать лобби.");
            return;
        }

        // Проверяем, что NetworkManager доступен
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager не найден! Убедитесь, что NetworkManager добавлен в сцену.");
                return;
            }
        }

        if (NetworkServer.active && NetworkClient.active)
        {
            if (!NetworkServer.active)
            {
                DisconnectFromCurrentLobby();
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(CreateLobbyAfterDisconnect());
                }
                return;
            }
        }
        else if (NetworkClient.active)
        {
            DisconnectFromCurrentLobby();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CreateLobbyAfterDisconnect());
            }
            return;
        }

        // FizzySteamworks транспорт находится автоматически

        // Создаем лобби
        CreateLobbyInternal();
    }
    
    
    System.Collections.IEnumerator CreateLobbyAfterDisconnect()
    {
        if (!gameObject.activeInHierarchy) yield break;

        yield return new WaitForSeconds(0.5f);
        
        int attempts = 0;
        while (networkManager != null && ((NetworkServer.active && NetworkClient.active) || NetworkClient.active) && attempts < 20)
        {
            if (!gameObject.activeInHierarchy) yield break;
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (!gameObject.activeInHierarchy || (networkManager != null && ((NetworkServer.active && NetworkClient.active) || NetworkClient.active)))
        {
            yield break;
        }
        
        yield return new WaitForSeconds(0.5f);
        CreateLobbyInternal();
    }
    
    public void CreateMirrorLobbyAfterSteamLobby()
    {
        CreateLobbyInternal();
    }
    
    void CreateLobbyInternal()
    {
        // Проверяем и инициализируем transport
        if (transport == null)
        {
            // Находим FizzySteamworks транспорт через рефлексию
            System.Type transportType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                transportType = assembly.GetType("FizzySteamworks") ?? 
                               assembly.GetType("com.mirror.steamworks.net.FizzySteamworks");
                if (transportType != null) break;
            }
            
            if (transportType != null)
            {
                transport = networkManager.GetComponent(transportType) as MonoBehaviour;
            }
            
            if (transport == null)
            {
                // Пытаемся найти по имени
                Component[] allComponents = networkManager.GetComponents<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp.GetType().Name == "FizzySteamworks")
                    {
                        transport = comp as MonoBehaviour;
                        break;
                    }
                }
            }
            if (transport == null)
            {
                Debug.LogError("[LobbyManager] FizzySteamworks транспорт не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            // FizzySteamworks использует Steam P2P, не требует настройки порта и адреса
            // Steam автоматически обрабатывает подключение через лобби
            
            if (networkManager != null)
            {
                networkManager.StartHostGame();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Ошибка создания лобби: {e.Message}");
        }

        UpdateUI();
    }

    void OnLobbySettingsButtonClicked()
    {
        if (!IsHost()) return;
        
        // Открываем панель настроек лобби
        if (lobbySettingsPanel != null)
        {
            ActivatePanelWithParents(lobbySettingsPanel);
            
            // Если пароль еще не установлен, генерируем его и отображаем
            SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
            if (steamLobbyManager != null)
            {
                ulong lobbyId = steamLobbyManager.GetCurrentLobbyId();
                if (lobbyId != 0)
                {
                    #if !DISABLESTEAMWORKS
                    string currentPassword = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), "password");
                    if (string.IsNullOrEmpty(currentPassword))
                    {
                        // Генерируем случайный 6-значный пароль
                        string newPassword = GenerateRandomPassword();
                        steamLobbyManager.SetLobbyData("password", newPassword);
                        Debug.Log($"[LobbyManager] Сгенерирован пароль лобби: {newPassword}");
                        
                        // Обновляем отображение пароля в панели настроек
                        LobbySettingsPanel settingsPanel = lobbySettingsPanel.GetComponent<LobbySettingsPanel>();
                        if (settingsPanel != null)
                        {
                            settingsPanel.SetPasswordDisplay(newPassword);
                        }
                    }
                    else
                    {
                        // Пароль уже установлен, просто обновляем отображение
                        LobbySettingsPanel settingsPanel = lobbySettingsPanel.GetComponent<LobbySettingsPanel>();
                        if (settingsPanel != null)
                        {
                            settingsPanel.SetPasswordDisplay(currentPassword);
                        }
                    }
                    #endif
                }
            }
        }
        else
        {
            Debug.LogWarning("[LobbyManager] Панель настроек лобби не назначена!");
        }
    }

    void OnStartGameButtonClicked()
    {
        if (!IsHost() || string.IsNullOrEmpty(gameSceneName)) return;
        
        if (networkManager != null && NetworkManager.singleton != null)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == gameSceneName) return;
            
            if (string.IsNullOrEmpty(NetworkManager.singleton.onlineScene))
            {
                NetworkManager.singleton.onlineScene = gameSceneName;
            }
            
            try
            {
                NetworkManager.singleton.ServerChangeScene(gameSceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Ошибка при загрузке сцены: {e.Message}");
            }
        }
    }

    void OnLobbySearchInputEndEdit(string searchText)
    {
        // Поиск выполняется при нажатии Enter или потере фокуса
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SearchLobbies(searchText);
        }
    }
    
    /// <summary>
    /// Выполняет поиск лобби по никнейму хоста
    /// </summary>
    public void SearchLobbies(string hostName)
    {
        if (string.IsNullOrEmpty(hostName))
        {
            Debug.LogWarning("[LobbyManager] Имя хоста для поиска не указано!");
            return;
        }
        
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[LobbyManager] Steam не инициализирован!");
            return;
        }
        #endif
        
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager != null)
        {
            Debug.Log($"[LobbyManager] Поиск лобби по никнейму хоста: {hostName}");
            steamLobbyManager.SearchLobbiesByHostName(hostName);
        }
        else
        {
            Debug.LogError("[LobbyManager] SteamLobbyManager не найден!");
        }
    }
    
    /// <summary>
    /// Обработчик найденных лобби
    /// </summary>
    void OnLobbiesFound(System.Collections.Generic.List<SteamLobbyManager.LobbySearchResult> lobbies)
    {
        // Очищаем предыдущие результаты
        ClearLobbySearchResults();
        
        if (lobbies == null || lobbies.Count == 0)
        {
            Debug.Log("[LobbyManager] Лобби не найдены");
            return;
        }
        
        // Получаем список друзей из Steam для фильтрации
        System.Collections.Generic.HashSet<ulong> friendsSteamIds = GetSteamFriendsList();
        
        // Получаем наш собственный Steam ID для исключения собственного лобби
        ulong mySteamId = 0;
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
        {
            mySteamId = SteamUser.GetSteamID().m_SteamID;
        }
        #endif
        
        // Создаем элементы для всех найденных лобби (не только друзей)
        // Фильтруем только собственное лобби и лобби с невалидным SteamID
        int displayedLobbiesCount = 0;
        
        foreach (var lobby in lobbies)
        {
            // Если SteamID хоста = 0 (невалидный), пропускаем это лобби
            if (lobby.hostSteamId == 0)
            {
                Debug.LogWarning($"[LobbyManager] Пропущено лобби с невалидным SteamID: Host={lobby.hostName}");
                continue;
            }
            
            // Пропускаем собственное лобби (мы уже в нем)
            if (lobby.hostSteamId == mySteamId)
            {
                Debug.Log($"[LobbyManager] Пропущено собственное лобби: Host={lobby.hostName}");
                continue;
            }
            
            // Проверяем, является ли хост лобби нашим другом (для информации)
            bool isFriend = friendsSteamIds.Contains(lobby.hostSteamId);
            
            // Создаем элемент для всех лобби (не только друзей)
            // Это позволяет видеть все доступные лобби, а не только лобби друзей
            CreateLobbySearchItem(lobby);
            displayedLobbiesCount++;
            
            if (isFriend)
            {
                Debug.Log($"[LobbyManager] Найдено лобби друга: Host={lobby.hostName}, SteamID={lobby.hostSteamId}, Players={lobby.currentPlayers}/{lobby.maxPlayers}");
            }
            else
            {
                Debug.Log($"[LobbyManager] Найдено лобби (не друг): Host={lobby.hostName}, SteamID={lobby.hostSteamId}, Players={lobby.currentPlayers}/{lobby.maxPlayers}");
            }
        }
        
        Debug.Log($"[LobbyManager] Отображено {displayedLobbiesCount} лобби из {lobbies.Count} найденных. Друзей в Steam: {friendsSteamIds.Count}");
        
        // Отладочная информация для диагностики проблемы с лобби
        if (displayedLobbiesCount == 0 && lobbies.Count > 0)
        {
            Debug.LogWarning($"[LobbyManager] Найдено {lobbies.Count} лобби, но ни одно не отображено. Друзей в Steam: {friendsSteamIds.Count}");
            
            // Выводим первые 5 лобби для отладки
            int debugCount = Mathf.Min(5, lobbies.Count);
            for (int i = 0; i < debugCount; i++)
            {
                var lobby = lobbies[i];
                Debug.LogWarning($"[LobbyManager] Лобби {i+1}: Host={lobby.hostName}, SteamID={lobby.hostSteamId}, IsFriend={friendsSteamIds.Contains(lobby.hostSteamId)}, IsMine={lobby.hostSteamId == mySteamId}");
            }
        }
    }
    
    /// <summary>
    /// Получает список Steam ID всех друзей
    /// </summary>
    System.Collections.Generic.HashSet<ulong> GetSteamFriendsList()
    {
        var friendsIds = new System.Collections.Generic.HashSet<ulong>();
        
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            return friendsIds;
        }
        
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagAll);
        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagAll);
            if (friendId.IsValid())
            {
                friendsIds.Add(friendId.m_SteamID);
            }
        }
        #endif
        
        return friendsIds;
    }
    
    /// <summary>
    /// Создает элемент найденного лобби
    /// </summary>
    void CreateLobbySearchItem(SteamLobbyManager.LobbySearchResult lobby)
    {
        if (lobbySearchItemPrefab == null || lobbySearchResultsContainer == null)
        {
            Debug.LogError("[LobbyManager] Префаб или контейнер для поиска лобби не назначены!");
            return;
        }
        
        GameObject lobbyItem = Instantiate(lobbySearchItemPrefab, lobbySearchResultsContainer);
        LobbySearchItem searchItem = lobbyItem.GetComponent<LobbySearchItem>();
        
        if (searchItem == null)
        {
            Debug.LogError("[LobbyManager] Компонент LobbySearchItem не найден на префабе!");
            Destroy(lobbyItem);
            return;
        }
        
        searchItem.Initialize(lobby.lobbyId, lobby.hostName, lobby.currentPlayers, lobby.maxPlayers, lobby.password);
    }
    
    /// <summary>
    /// Очищает список найденных лобби
    /// </summary>
    void ClearLobbySearchResults()
    {
        if (lobbySearchResultsContainer == null) return;
        
        foreach (Transform child in lobbySearchResultsContainer)
        {
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
    
    /// <summary>
    /// Подключается к лобби по Steam ID
    /// </summary>
    public void ConnectToLobbyBySteamId(ulong lobbySteamId)
    {
        if (lobbySteamId == 0)
        {
            UpdateStatusText("Ошибка: Неверный идентификатор лобби", false);
            Debug.LogError("[LobbyManager] Неверный Steam ID лобби!");
            return;
        }
        
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager == null)
        {
            UpdateStatusText("Ошибка: Не удалось инициализировать подключение", false);
            Debug.LogError("[LobbyManager] SteamLobbyManager не найден!");
            return;
        }
        
        UpdateStatusText("Подключение к лобби...", true);
        
        // Получаем Steam ID хоста из лобби
        #if !DISABLESTEAMWORKS
        CSteamID lobbyId = new CSteamID(lobbySteamId);
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
        ulong hostSteamId = ownerId.m_SteamID;
        
        Debug.Log($"[LobbyManager] Подключение к лобби {lobbySteamId}, хост Steam ID: {hostSteamId}");
        
        // Сначала присоединяемся к Steam лобби
        SteamMatchmaking.JoinLobby(lobbyId);
        
        // Подключение к Mirror серверу произойдет автоматически через OnLobbyEntered в SteamLobbyManager
        #endif
    }

    void ActivatePanelWithParents(GameObject panel)
    {
        if (panel == null)
            return;

        // Активируем панель
        panel.SetActive(true);
        
        // Убеждаемся, что панель и все её родители активны
        Transform parent = panel.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
            }
            parent = parent.parent;
        }
        
        // Проверяем Canvas и активируем его, если нужно
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.gameObject.activeSelf)
        {
            canvas.gameObject.SetActive(true);
        }
        
        // Перемещаем панель в конец иерархии, чтобы она была на переднем плане
        panel.transform.SetAsLastSibling();
    }

    void OnColorSelectionButtonClicked()
    {
        if (colorSelectionPanel != null)
        {
            ActivatePanelWithParents(colorSelectionPanel);
        }
    }
    
    void OnShowLobbySearchButtonClicked()
    {
        if (lobbySearchPanel != null)
        {
            // Проверяем текущее состояние панели (activeInHierarchy учитывает активность родителей)
            bool isActive = lobbySearchPanel.activeInHierarchy;
            
            if (isActive)
            {
                // Если панель активна и видна, скрываем её
                lobbySearchPanel.SetActive(false);
                
                // Показываем список игроков при закрытии панели поиска
                if (playersListContainer != null)
                {
                    playersListContainer.gameObject.SetActive(true);
                }
            }
            else
            {
                // Если панель неактивна или не видна, активируем её и родителей
                ActivatePanelWithParents(lobbySearchPanel);
                
                // Скрываем список игроков при открытии панели поиска
                if (playersListContainer != null)
                {
                    playersListContainer.gameObject.SetActive(false);
                }
                
                // Автоматически обновляем список лобби при открытии панели
                RefreshLobbyList();
            }
        }
    }
    
    /// <summary>
    /// Обновляет список доступных лобби
    /// </summary>
    public void RefreshLobbyList()
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[LobbyManager] Steam не инициализирован!");
            return;
        }
        
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager != null)
        {
            Debug.Log("[LobbyManager] Обновление списка лобби...");
            // Запрашиваем все доступные лобби
            // Steam вернет лобби в зависимости от типа (k_ELobbyTypeFriendsOnly - только друзья, k_ELobbyTypePublic - все)
            steamLobbyManager.SearchAllLobbies();
        }
        else
        {
            Debug.LogError("[LobbyManager] SteamLobbyManager не найден!");
        }
        #endif
    }

    public void HideColorSelectionPanel()
    {
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(false);
        }
    }

    // Метод вызывается Mirror через событие OnStartServer
    public void OnMirrorServerStarted()
    {
        UpdateUI();
        
        // Убеждаемся, что LobbyPlayerSync заспавнен на сервере
        if (playerSync != null && NetworkServer.active)
        {
            NetworkIdentity netId = playerSync.GetComponent<NetworkIdentity>();
            if (netId != null && netId.netId == 0)
            {
                NetworkServer.Spawn(playerSync.gameObject);
                Debug.Log("[LobbyManager] LobbyPlayerSync заспавнен на сервере через OnMirrorServerStarted");
            }
        }
        
        if (NetworkServer.active && NetworkClient.active)
        {
            StartCoroutine(CreateHostPlayerLobbyItemDelayed());
        }
        
        // Запускаем периодическое обновление RTT
        if (!isRTTUpdateRunning)
        {
            isRTTUpdateRunning = true;
            StartCoroutine(UpdateRTTPeriodically());
        }
    }
    
    System.Collections.IEnumerator CreateHostPlayerLobbyItemDelayed()
    {
        yield return new WaitForSeconds(0.3f);
        
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        
        CreatePlayerLobbyItem(localClientId);
    }


    public void OnMirrorClientConnected(uint connectionId)
    {
        if (!gameObject.activeInHierarchy) return;
        
        Debug.Log($"[LobbyManager] OnMirrorClientConnected: connectionId={connectionId}, NetworkServer.active={NetworkServer.active}, NetworkClient.active={NetworkClient.active}");
        
        UpdateUI();
        
        if (NetworkServer.active)
        {
            Debug.Log($"[LobbyManager] Сервер: подключился клиент с connectionId={connectionId}. Всего подключений: {NetworkServer.connections.Count}");
            
            // На сервере создаем PlayerLobbyItem для подключившегося клиента
            CreatePlayerLobbyItem(connectionId);
            
            // Также создаем UI для всех остальных уже подключенных игроков, если их еще нет
            // ВАЖНО: Создаем копию коллекции, чтобы избежать ошибок при изменении во время итерации
            var connectionsCopy = new System.Collections.Generic.List<NetworkConnectionToClient>();
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection != null)
                {
                    connectionsCopy.Add(connection);
                }
            }
            
            foreach (var connection in connectionsCopy)
            {
                if (connection == null) continue;
                uint connId = (uint)connection.connectionId;
                if (connId != connectionId && !playerLobbyItems.ContainsKey(connId))
                {
                    CreatePlayerLobbyItem(connId);
                }
            }
            
            // Отправляем список всех игроков новому клиенту через LobbyPlayerSync
            if (playerSync != null)
            {
                NetworkConnectionToClient conn = null;
                // Используем копию для поиска нужного подключения
                foreach (var connection in connectionsCopy)
                {
                    if (connection != null && connection.connectionId == connectionId)
                    {
                        conn = connection;
                        break;
                    }
                }
                
                if (conn != null)
                {
                    Debug.Log($"[LobbyManager] Найдено подключение для connectionId={connectionId}, отправляем список игроков");
                    // Ждем немного, чтобы PlayerLobbyItem успели создаться и LobbyPlayerSync синхронизировался с клиентом
                    StartCoroutine(SendPlayersListToClientDelayed(conn, connectionId));
                }
                else
                {
                    Debug.LogWarning($"[LobbyManager] Не найдено подключение для connectionId={connectionId}!");
                }
            }
            else
            {
                Debug.LogWarning("[LobbyManager] playerSync == null, не можем отправить список игроков!");
            }
            
            // Синхронизируем данные нового клиента с остальными через NetworkPlayer
            StartCoroutine(SyncNewClientToAll(connectionId));
        }
        else if (NetworkClient.active)
        {
            Debug.Log("[LobbyManager] Клиент: подключились к серверу");
            // На клиенте синхронизируем список игроков из NetworkPlayer
            StartCoroutine(SyncPlayersFromNetworkPlayers());
            
            // Открываем меню подключения через CameraMovementController с небольшой задержкой
            StartCoroutine(OpenConnectMenuDelayed());
            
            // Обновляем UI, показывая что мы подключились
            UpdateUI();
        }
    }
    
    /// <summary>
    /// Отправляет список игроков клиенту с задержкой
    /// </summary>
    System.Collections.IEnumerator SendPlayersListToClientDelayed(NetworkConnectionToClient conn, uint connectionId)
    {
        // Ждем, чтобы LobbyPlayerSync успел синхронизироваться с клиентом
        yield return new WaitForSeconds(1.0f);
        
        if (playerSync != null && conn != null && NetworkServer.connections.ContainsKey(conn.connectionId))
        {
            // Проверяем, что LobbyPlayerSync заспавнен и синхронизирован
            NetworkIdentity netId = playerSync.GetComponent<NetworkIdentity>();
            if (netId != null && netId.netId != 0)
            {
                Debug.Log($"[LobbyManager] Отправка списка игроков клиенту {connectionId} через LobbyPlayerSync (netId={netId.netId})");
                playerSync.SendPlayersListToClient(conn);
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] LobbyPlayerSync еще не заспавнен (netId={netId?.netId ?? 0}). Повторная попытка через 0.5 сек...");
                yield return new WaitForSeconds(0.5f);
                
                // Повторная попытка
                if (playerSync != null && conn != null && NetworkServer.connections.ContainsKey(conn.connectionId))
                {
                    netId = playerSync.GetComponent<NetworkIdentity>();
                    if (netId != null && netId.netId != 0)
                    {
                        playerSync.SendPlayersListToClient(conn);
                    }
                    else
                    {
                        Debug.LogError($"[LobbyManager] Не удалось отправить список игроков клиенту {connectionId} - LobbyPlayerSync не заспавнен!");
                    }
                }
            }
        }
    }
    
    public void OnClientConnected()
    {
        if (!NetworkClient.active) return;
        
        UpdateStatusText("Успешно подключено к лобби!", true);
        
        // На клиенте синхронизируем список игроков из NetworkPlayer
        StartCoroutine(SyncPlayersFromNetworkPlayers());
        
        // Открываем меню подключения через CameraMovementController с небольшой задержкой
        StartCoroutine(OpenConnectMenuDelayed());
        
        // Обновляем UI, показывая что мы подключились
        UpdateUI();
    }
    
    /// <summary>
    /// Открывает меню подключения с задержкой (чтобы данные успели синхронизироваться)
    /// </summary>
    System.Collections.IEnumerator OpenConnectMenuDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        CameraMovementController cameraController = FindObjectOfType<CameraMovementController>();
        if (cameraController != null)
        {
            cameraController.OpenConnectMenu();
        }
    }
    
    /// <summary>
    /// Синхронизирует данные нового клиента со всеми остальными через NetworkPlayer
    /// </summary>
    System.Collections.IEnumerator SyncNewClientToAll(uint newClientId)
    {
        yield return new WaitForSeconds(0.3f);
        
        // Находим NetworkPlayer для нового клиента
        NetworkPlayer newPlayer = FindNetworkPlayerById(newClientId);
        if (newPlayer != null)
        {
            // Данные уже синхронизированы через NetworkPlayer SyncVar
            // Остальные клиенты получат их автоматически
            Debug.Log($"[LobbyManager] Данные нового клиента {newClientId} синхронизированы через NetworkPlayer");
        }
    }
    
    /// <summary>
    /// Находит NetworkPlayer по connectionId
    /// </summary>
    NetworkPlayer FindNetworkPlayerById(uint connectionId)
    {
        NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
        foreach (NetworkPlayer player in allPlayers)
        {
            if (player != null && player.netIdentity != null && player.netIdentity.netId != 0 && player.PlayerId == connectionId)
            {
                return player;
            }
        }
        return null;
    }


    public void OnMirrorClientDisconnected(uint connectionId)
    {
        if (playerLobbyItems.ContainsKey(connectionId))
        {
            GameObject playerItem = playerLobbyItems[connectionId];
            if (playerItem != null)
            {
                Destroy(playerItem);
            }
            playerLobbyItems.Remove(connectionId);
        }
        
        // Отправляем уведомление об удалении игрока всем клиентам
        if (NetworkServer.active && playerSync != null)
        {
            playerSync.BroadcastPlayerRemoved(connectionId);
        }
        
        // Если это отключение клиента (не сервера), показываем статус
        if (NetworkClient.active && !NetworkServer.active)
        {
            UpdateStatusText("Отключено от лобби", false);
        }
        
        ReorderPlayersList();
        UpdateUI();
    }


    public void RegisterPlayerLobbyItem(uint connectionId, GameObject playerItem)
    {
        if (playerItem == null) return;

        if (!playerLobbyItems.ContainsKey(connectionId))
        {
            playerLobbyItems[connectionId] = playerItem;
            
            if (IsHost())
            {
                ReorderPlayersList();
            }
        }
    }

    void ReorderPlayersList()
    {
        if (playersListContainer == null || !IsHost())
            return;

        // Находим админа (хоста) и перемещаем его в начало
        // В Mirror для клиента connectionId получаем через рефлексию
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        if (NetworkClient.connection != null && playerLobbyItems.ContainsKey(localClientId))
        {
            GameObject adminItem = playerLobbyItems[localClientId];
            adminItem.transform.SetAsFirstSibling();
        }
    }

    void CreatePlayerLobbyItem(uint connectionId)
    {
        if (playerLobbyPrefab == null || playersListContainer == null) return;
        if (playerLobbyItems.ContainsKey(connectionId)) return;

        if (!playersListContainer.gameObject.activeInHierarchy)
        {
            playersListContainer.gameObject.SetActive(true);
        }

        if (NetworkServer.active)
        {
            // Определяем, является ли игрок админом (хостом)
            uint localClientId = 0;
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
            // Определяем, является ли игрок админом (хостом)
            bool isAdmin = false;
            if (NetworkServer.active && NetworkClient.active)
            {
                // Мы хост - проверяем, является ли это локальный клиент
                isAdmin = (connectionId == 0) || (connectionId == localClientId && localClientId == 0);
            }
            else if (connectionId == 0 && NetworkServer.active)
            {
                // На сервере connectionId = 0 означает хост
                isAdmin = true;
            }
            
            // Пытаемся получить данные из NetworkPlayer
            NetworkPlayer player = FindNetworkPlayerById(connectionId);
            string playerName = "";
            Color playerColor = Color.white;
            
            if (player != null)
            {
                playerName = player.PlayerName;
                playerColor = player.PlayerColor;
            }
            
            // Если данные из NetworkPlayer недоступны, получаем из Steam или PlayerPrefs
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = GetPlayerNameFromSteam(connectionId);
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = GenerateRandomPlayerName();
                }
            }
            
            // Для хоста всегда используем цвет из PlayerPrefs, если он есть
            if (isAdmin && PlayerPrefs.HasKey("PlayerColor_R") && PlayerPrefs.HasKey("PlayerColor_G") && 
                PlayerPrefs.HasKey("PlayerColor_B") && PlayerPrefs.HasKey("PlayerColor_A"))
            {
                Color savedColor = new Color(
                    PlayerPrefs.GetFloat("PlayerColor_R", 0.05f),
                    PlayerPrefs.GetFloat("PlayerColor_G", 0.82f),
                    PlayerPrefs.GetFloat("PlayerColor_B", 0.27f),
                    PlayerPrefs.GetFloat("PlayerColor_A", 1f)
                );
                // Используем сохраненный цвет, если NetworkPlayer не найден или его цвет белый
                if (player == null || playerColor == Color.white)
                {
                    playerColor = savedColor;
                }
            }
            // Для других игроков используем цвет из NetworkPlayer, если он есть
            
            CreatePlayerLobbyItemLocally(connectionId, isAdmin, playerName, playerColor);
            
            // Данные синхронизируются автоматически через NetworkPlayer SyncVar
        }
    }
    
    public void CreatePlayerLobbyItemLocally(uint connectionId, bool isAdmin, string playerName, Color playerColor)
    {
        if (playerLobbyPrefab == null)
        {
            Debug.LogError($"[LobbyManager] CreatePlayerLobbyItemLocally: playerLobbyPrefab не назначен! connectionId={connectionId}");
            return;
        }
        
        if (playersListContainer == null)
        {
            Debug.LogError($"[LobbyManager] CreatePlayerLobbyItemLocally: playersListContainer не назначен! connectionId={connectionId}");
            return;
        }

        Debug.Log($"[LobbyManager] CreatePlayerLobbyItemLocally: connectionId={connectionId}, isAdmin={isAdmin}, name={playerName}, color={playerColor}");

        // Убеждаемся, что контейнер активен
        if (!playersListContainer.gameObject.activeInHierarchy)
        {
            Debug.Log($"[LobbyManager] Активируем playersListContainer");
            playersListContainer.gameObject.SetActive(true);
        }

        // Если элемент уже существует, обновляем его
        if (playerLobbyItems.ContainsKey(connectionId))
        {
            PlayerLobbyItem existingItem = playerLobbyItems[connectionId].GetComponent<PlayerLobbyItem>();
            if (existingItem != null)
            {
                existingItem.Initialize(connectionId, isAdmin, playerName, playerColor);
                Debug.Log($"[LobbyManager] Обновлен PlayerLobbyItem для connectionId={connectionId}, name={playerName}");
            }
            
            // ВАЖНО: Если мы на сервере, синхронизируем обновление с клиентами через LobbyPlayerSync
            if (NetworkServer.active && playerSync != null)
            {
                playerSync.BroadcastPlayerUpdate(connectionId, playerName, playerColor, isAdmin);
            }
            
            return;
        }

        GameObject playerItem = Instantiate(playerLobbyPrefab, playersListContainer);
        PlayerLobbyItem playerLobbyItem = playerItem.GetComponent<PlayerLobbyItem>();
        
        if (playerLobbyItem == null)
        {
            Debug.LogError($"[LobbyManager] PlayerLobbyItem компонент не найден на префабе!");
            Destroy(playerItem);
            return;
        }

        // Получаем никнейм из Steam, если не передан
        if (string.IsNullOrEmpty(playerName))
        {
            #if !DISABLESTEAMWORKS
            if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
            {
                string steamName = SteamManager.Instance.GetSteamName();
                if (!string.IsNullOrEmpty(steamName))
                {
                    playerName = steamName;
                }
            }
            #endif
        }

        playerLobbyItem.Initialize(connectionId, isAdmin, playerName, playerColor);
        playerLobbyItems[connectionId] = playerItem;
        
        Debug.Log($"[LobbyManager] Создан PlayerLobbyItem для connectionId={connectionId}, name={playerName}, isAdmin={isAdmin}, totalItems={playerLobbyItems.Count}");
        
        // ВАЖНО: Если мы на сервере, синхронизируем создание с клиентами через LobbyPlayerSync
        if (NetworkServer.active && playerSync != null)
        {
            playerSync.BroadcastPlayerUpdate(connectionId, playerName, playerColor, isAdmin);
        }
        
        ReorderPlayersList();
    }
    
    /// <summary>
    /// Обновляет данные PlayerLobbyItem из NetworkPlayer при изменении SyncVar
    /// </summary>
    public void OnNetworkPlayerDataChanged(uint connectionId)
    {
        NetworkPlayer player = FindNetworkPlayerById(connectionId);
        if (player != null && playerLobbyItems.ContainsKey(connectionId))
        {
            bool isAdmin = false;
            uint localClientId = 0;
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
            
            // Определяем, является ли игрок админом (хостом)
            if (NetworkServer.active && NetworkClient.active)
            {
                // Мы хост - проверяем, является ли это локальный клиент
                if (connectionId == 0 || (connectionId == localClientId && localClientId == 0))
            {
                isAdmin = true;
            }
            }
            else if (connectionId == 0 && NetworkServer.active)
            {
                // На сервере connectionId = 0 означает хост
                isAdmin = true;
            }
            
            UpdatePlayerDataAndSync(connectionId, isAdmin, player.PlayerName, player.PlayerColor);
        }
    }
    
    /// <summary>
    /// Периодически обновляет RTT всех игроков и синхронизирует их клиентам
    /// </summary>
    System.Collections.IEnumerator UpdateRTTPeriodically()
    {
        while (NetworkServer.active)
        {
            yield return new WaitForSeconds(5f); // Обновляем раз в 5 секунд
            
            if (!NetworkServer.active)
            {
                isRTTUpdateRunning = false;
                yield break;
            }
            
            if (playerLobbyItems.Count == 0) continue;
            
            // Собираем RTT всех игроков
            Dictionary<uint, int> rttData = new Dictionary<uint, int>();
            
            foreach (var kvp in playerLobbyItems)
            {
                uint playerId = kvp.Key;
                int rtt = GetPlayerRTT(playerId);
                rttData[playerId] = rtt;
            }
            
            // Обновляем RTT локально для всех PlayerLobbyItem
            foreach (var kvp in rttData)
            {
                UpdatePlayerRTTLocally(kvp.Key, kvp.Value);
            }
        }
        
        isRTTUpdateRunning = false;
    }
    
    /// <summary>
    /// Получает RTT игрока (для сервера)
    /// </summary>
    private int GetPlayerRTT(uint connectionId)
    {
        // У хоста пинг всегда 5
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        
        if (NetworkServer.active && NetworkClient.active && connectionId == localClientId)
        {
            return 5;
        }
        
        // Для других клиентов получаем RTT через NetworkServer.connections
        // ВАЖНО: Проверяем наличие подключения перед доступом
        NetworkConnectionToClient connection = null;
        try
        {
            if (NetworkServer.connections.ContainsKey((int)connectionId))
            {
                connection = NetworkServer.connections[(int)connectionId];
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Ошибка при получении подключения для connectionId={connectionId}: {e.Message}");
            return 1;
        }
        
        if (connection != null)
        {
            try
            {
                var rttProperty = connection.GetType().GetProperty("rtt");
                if (rttProperty != null)
                {
                    object rttValue = rttProperty.GetValue(connection);
                    if (rttValue != null)
                    {
                        if (rttValue is int)
                            return (int)rttValue;
                        else if (rttValue is float)
                            return Mathf.RoundToInt((float)rttValue);
                        else if (rttValue is double)
                            return Mathf.RoundToInt((float)(double)rttValue);
                    }
                }
                
                var rttField = connection.GetType().GetField("rtt");
                if (rttField != null)
                {
                    object rttValue = rttField.GetValue(connection);
                    if (rttValue != null)
                    {
                        int rtt = 0;
                        if (rttValue is int)
                            rtt = (int)rttValue;
                        else if (rttValue is float)
                            rtt = Mathf.RoundToInt((float)rttValue);
                        else if (rttValue is double)
                            rtt = Mathf.RoundToInt((float)(double)rttValue);
                        
                        if (rtt > 0)
                            return rtt;
                    }
                }
                
                // Пытаемся получить через averageRTT
                var avgRttProperty = connection.GetType().GetProperty("averageRTT");
                if (avgRttProperty != null)
                {
                    object rttValue = avgRttProperty.GetValue(connection);
                    if (rttValue != null)
                    {
                        int rtt = 0;
                        if (rttValue is int)
                            rtt = (int)rttValue;
                        else if (rttValue is float)
                            rtt = Mathf.RoundToInt((float)rttValue);
                        else if (rttValue is double)
                            rtt = Mathf.RoundToInt((float)(double)rttValue);
                        
                        if (rtt > 0)
                            return rtt;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Ошибка при получении RTT для connectionId={connectionId}: {e.Message}");
            }
        }
        
        // Если RTT еще не инициализирован, возвращаем минимальное значение (не 0)
        return 1;
    }
    
    /// <summary>
    /// Обновляет RTT игрока локально (вызывается через ClientRpc)
    /// </summary>
    public void UpdatePlayerRTTLocally(uint connectionId, int rtt)
    {
        if (playerLobbyItems.ContainsKey(connectionId))
        {
            PlayerLobbyItem playerItem = playerLobbyItems[connectionId].GetComponent<PlayerLobbyItem>();
            if (playerItem != null)
            {
                playerItem.SetRTT(rtt);
            }
        }
    }
    
    public void UpdatePlayerColorLocally(uint connectionId, Color playerColor)
    {
        if (playerLobbyItems.ContainsKey(connectionId))
        {
            PlayerLobbyItem playerItem = playerLobbyItems[connectionId].GetComponent<PlayerLobbyItem>();
            if (playerItem != null)
            {
                playerItem.SetPlayerColor(playerColor);
            }
        }
    }
    
    public void UpdatePlayerDataAndSync(uint connectionId, bool isAdmin, string playerName, Color playerColor)
    {
        if (playerLobbyItems.ContainsKey(connectionId))
        {
            PlayerLobbyItem playerItem = playerLobbyItems[connectionId].GetComponent<PlayerLobbyItem>();
            if (playerItem != null)
            {
                playerItem.Initialize(connectionId, isAdmin, playerName, playerColor);
            }
        }
        else
        {
            CreatePlayerLobbyItemLocally(connectionId, isAdmin, playerName, playerColor);
        }
        
        // Отправляем обновление данных игрока всем клиентам через LobbyPlayerSync
        if (NetworkServer.active && playerSync != null)
        {
            playerSync.BroadcastPlayerUpdate(connectionId, playerName, playerColor, isAdmin);
        }
    }
    
    
    /// <summary>
    /// Получает connection ID клиента через рефлексию (для Mirror)
    /// </summary>
    private uint GetClientConnectionId()
    {
        // В Mirror для клиента connectionId получаем через рефлексию
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        return localClientId;
    }
    
    private string GetLocalPlayerSteamName()
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
        {
            string steamName = SteamManager.Instance.GetSteamName();
            if (!string.IsNullOrEmpty(steamName))
            {
                return steamName;
            }
        }
        #endif
        
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            return PlayerPrefs.GetString("PlayerName", "Player");
        }
        
        return GenerateRandomPlayerName();
    }
    
    /// <summary>
    /// Получает список всех игроков для синхронизации
    /// </summary>
    public Dictionary<uint, (bool isAdmin, string playerName, Color playerColor)> GetAllPlayersData()
    {
        Dictionary<uint, (bool isAdmin, string playerName, Color playerColor)> playersData = new Dictionary<uint, (bool, string, Color)>();
        
        foreach (var playerItem in playerLobbyItems)
        {
            uint playerConnectionId = playerItem.Key;
            PlayerLobbyItem item = playerItem.Value?.GetComponent<PlayerLobbyItem>();
            
            if (item != null)
            {
                bool isAdmin = item.IsAdmin();
                playersData[playerConnectionId] = (isAdmin, item.PlayerName, item.PlayerColor);
            }
        }
        
        return playersData;
    }
    
    public string GetPlayerNameFromSteam(uint connectionId)
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
        {
            uint localClientId = 0;
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
            if (NetworkServer.active && NetworkClient.active && NetworkClient.connection != null && connectionId == localClientId)
            {
                string steamName = SteamManager.Instance.GetSteamName();
                if (!string.IsNullOrEmpty(steamName))
                {
                    return steamName;
                }
            }
            
            NetworkConnectionToClient connection = null;
            try
            {
                if (NetworkServer.connections.ContainsKey((int)connectionId))
                {
                    connection = NetworkServer.connections[(int)connectionId];
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Ошибка при получении подключения для connectionId={connectionId}: {e.Message}");
                return "";
            }
            
            if (connection != null)
            {
                try
                {
                    string address = connection.address;
                    if (ulong.TryParse(address, out ulong steamId))
                    {
                        string friendName = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
                        if (!string.IsNullOrEmpty(friendName))
                        {
                            return friendName;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LobbyManager] Ошибка при получении имени игрока из Steam: {e.Message}");
                }
            }
        }
        #endif
        
        return "";
    }
    
    /// <summary>
    /// Генерирует случайное имя игрока
    /// </summary>
    private string GenerateRandomPlayerName()
    {
        // Генерируем случайное имя формата Player_XXXXXX (6 букв от A до Z)
        System.Text.StringBuilder nameBuilder = new System.Text.StringBuilder("Player_");
        System.Random random = new System.Random();
        
        for (int i = 0; i < 6; i++)
        {
            char randomChar = (char)('A' + random.Next(0, 26));
            nameBuilder.Append(randomChar);
        }
        
        return nameBuilder.ToString();
    }

    /// <summary>
    /// Обновляет текст статуса подключения к лобби
    /// </summary>
    /// <param name="message">Сообщение для отображения</param>
    /// <param name="isSuccess">true для зеленого цвета (успех), false для красного (ошибка)</param>
    public void UpdateStatusText(string message, bool isSuccess = true)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isSuccess ? Color.green : Color.red;
        }
    }

    void UpdateUI()
    {
        bool isHost = IsHost();

        // Кнопки админа показываются только хосту
        if (lobbySettingsButton != null)
        {
            lobbySettingsButton.gameObject.SetActive(isHost);
            lobbySettingsButton.interactable = isHost;
        }

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = isHost;
        }

        // Кнопка поиска лобби доступна всегда
        if (showLobbySearchButton != null)
        {
            showLobbySearchButton.interactable = true;
        }
    }

    public bool IsHost()
    {
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
        }
        // Хост = сервер активен И клиент активен (локальное подключение)
        return NetworkServer.active && NetworkClient.active;
    }

    public void ConnectToLobby(string ipAddress, string password, ushort port = 0) // port не используется для FizzySteamworks
    {
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект LobbyManager неактивен! Невозможно подключиться к лобби.");
            return;
        }

        // Проверяем, что NetworkManager доступен
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager не найден! Убедитесь, что NetworkManager добавлен в сцену.");
                return;
            }
        }

        // Если мы уже подключены к лобби, отключаемся сначала
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active)
        {
            Debug.Log("Отключение от текущего лобби перед подключением к новому...");
            DisconnectFromCurrentLobby();
            
            // Ждем немного, чтобы отключение завершилось
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ConnectAfterDisconnect(ipAddress, password, port));
            }
            else
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным после отключения! Невозможно запустить корутину подключения.");
            }
            return;
        }

        // Если не подключены, подключаемся сразу
        ConnectToLobbyInternal(ipAddress, password, port);
    }

    System.Collections.IEnumerator ConnectAfterDisconnect(string ipAddress, string password, ushort port = 0) // port не используется для FizzySteamworks
    {
        // Проверяем, что объект все еще активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен в корутине ConnectAfterDisconnect!");
            yield break;
        }

        Debug.Log("[LobbyManager] Ожидание закрытия сокета перед переподключением...");
        
        // Ждем достаточно времени, чтобы сокет успел правильно закрыться
        // Оптимизировано: 1 секунда обычно достаточно для FizzySteamworks
        yield return new WaitForSeconds(1.0f);
        
        // Проверяем, что мы действительно отключены
        int attempts = 0;
        while (networkManager != null && ((NetworkServer.active && NetworkClient.active) || NetworkClient.active) && attempts < 40)
        {
            // Проверяем, что объект все еще активен
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным во время ожидания отключения!");
                yield break;
            }
            
            yield return new WaitForSeconds(0.15f);
            attempts++;
        }
        
        // Проверяем, что объект все еще активен перед подключением
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен перед подключением!");
            yield break;
        }
        
        // Проверяем NetworkManager еще раз
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager не найден после отключения!");
                yield break;
            }
        }
        
        // FizzySteamworks не требует настройки transport
        
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active)
        {
            Debug.LogWarning("[LobbyManager] Не удалось отключиться от текущего лобби! Попытка принудительного отключения...");
            try
            {
                if (NetworkServer.active && NetworkClient.active)
            {
                networkManager.StopHost();
                }
                else if (NetworkClient.active)
                {
                networkManager.StopClient();
                }
                else if (NetworkServer.active)
                {
                networkManager.StopServer();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                yield break;
            }
            
            // Ждем после отключения
            yield return new WaitForSeconds(1.0f);
        }
        
        // Задержка для полного закрытия сокета и очистки transport
        // Оптимизировано: 1.5 секунды достаточно для FizzySteamworks
        Debug.Log("[LobbyManager] Ожидание полного закрытия сокета и очистки ресурсов...");
        yield return new WaitForSeconds(1.5f);
        
        // FizzySteamworks не требует настройки transport - он работает через Steam API
        
        // Финальная проверка состояния NetworkManager перед подключением
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager не найден перед подключением!");
                yield break;
            }
        }
        
        // Убеждаемся, что NetworkManager полностью отключен
        if (NetworkClient.active || (NetworkServer.active && NetworkClient.active))
        {
            Debug.LogWarning("[LobbyManager] NetworkManager все еще подключен! Ожидание дополнительного времени...");
            yield return new WaitForSeconds(1.0f);
            
            // Если все еще подключен, принудительно отключаем
            if (NetworkClient.active || (NetworkServer.active && NetworkClient.active))
            {
                try
                {
                    if (NetworkServer.active && NetworkClient.active)
                {
                    networkManager.StopHost();
                    }
                    else if (NetworkClient.active)
                    {
                networkManager.StopClient();
                    }
                    else if (NetworkServer.active)
                    {
                networkManager.StopServer();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                }
                
                yield return new WaitForSeconds(1.0f);
            }
        }
        
        // Финальная задержка перед подключением
        Debug.Log("[LobbyManager] Финальная задержка перед подключением...");
        yield return new WaitForSeconds(0.3f);
        
        Debug.Log("[LobbyManager] Сокет должен быть закрыт. Начинаем подключение к новому лобби...");
        
        // Подключаемся к новому лобби
        ConnectToLobbyInternal(ipAddress, password, port);
    }

    void ConnectToLobbyInternal(string ipAddress, string password, ushort port = 0) // port не используется для FizzySteamworks
    {
        // Для FizzySteamworks подключение происходит через Steam лобби, а не через IP
        // Этот метод используется для подключения через Steam лобби
        
        // Проверяем и инициализируем transport
        if (transport == null)
        {
            // Находим FizzySteamworks транспорт через рефлексию
            System.Type transportType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                transportType = assembly.GetType("FizzySteamworks") ?? 
                               assembly.GetType("com.mirror.steamworks.net.FizzySteamworks");
                if (transportType != null) break;
            }
            
            if (transportType != null)
            {
                transport = networkManager.GetComponent(transportType) as MonoBehaviour;
            }
            
            if (transport == null)
            {
                // Пытаемся найти по имени
                Component[] allComponents = networkManager.GetComponents<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp.GetType().Name == "FizzySteamworks")
                    {
                        transport = comp as MonoBehaviour;
                        break;
                    }
                }
            }
            if (transport == null)
            {
                UpdateStatusText("Ошибка: Не удалось установить соединение. Проверьте настройки сети", false);
                Debug.LogError("[LobbyManager] ✗ FizzySteamworks транспорт не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            // Убеждаемся, что NetworkManager полностью отключен перед новым подключением
        if (NetworkClient.active || (NetworkServer.active && NetworkClient.active))
            {
                Debug.LogWarning("[LobbyManager] Уже подключен! Попытка принудительного отключения...");
                try
            {
                if (NetworkServer.active && NetworkClient.active)
                {
                    networkManager.StopHost();
                }
                else if (NetworkClient.active)
                {
                    networkManager.StopClient();
                }
                else if (NetworkServer.active)
                {
                    networkManager.StopServer();
                }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                }
                
                // Если мы все еще подключены, запускаем корутину для ожидания отключения
            if (NetworkClient.active || (NetworkServer.active && NetworkClient.active))
                {
                    Debug.LogWarning("[LobbyManager] Ожидание завершения отключения...");
                    StartCoroutine(WaitForDisconnectAndConnect(ipAddress, password, port));
                    return;
                }
            }
            
            // Для FizzySteamworks подключение происходит через Steam ID из лобби
            // Если ipAddress является Steam ID (число), используем его
            // Иначе получаем Steam ID из текущего лобби
            ulong steamId = 0;
            if (ulong.TryParse(ipAddress, out steamId))
            {
                // ipAddress - это Steam ID
                UpdateStatusText("Подключение к серверу...", true);
                Debug.Log($"[LobbyManager] Подключение к серверу через Steam ID: {steamId}");
                networkManager.ConnectToSteamId(steamId);
            }
            else
            {
                // Получаем Steam ID хоста из текущего лобби
                SteamLobbyManager steamLobbyManager = FindObjectOfType<SteamLobbyManager>();
                if (steamLobbyManager != null)
                {
                    ulong hostSteamId = steamLobbyManager.GetLobbyOwnerId();
                    if (hostSteamId != 0)
                    {
                        UpdateStatusText("Подключение к серверу...", true);
                        Debug.Log($"[LobbyManager] Подключение к серверу через Steam лобби: {hostSteamId}");
                        networkManager.ConnectToSteamId(hostSteamId);
                    }
                    else
                    {
                        UpdateStatusText("Ошибка: Не удалось найти хост лобби. Лобби может быть недоступно", false);
                        Debug.LogError("[LobbyManager] ✗ Не удалось получить Steam ID хоста из лобби!");
                    }
                }
                else
                {
                    UpdateStatusText("Ошибка: Не удалось инициализировать подключение", false);
                    Debug.LogError("[LobbyManager] ✗ SteamLobbyManager не найден! Подключение через Steam невозможно.");
                }
            }
        }
        catch (System.Exception e)
        {
            // Формируем понятное сообщение об ошибке
            string userMessage = "Не удалось подключиться к лобби";
            if (e.Message.Contains("timeout") || e.Message.Contains("Timeout"))
                userMessage = "Превышено время ожидания. Сервер не отвечает";
            else if (e.Message.Contains("connection") || e.Message.Contains("Connection"))
                userMessage = "Ошибка соединения. Проверьте подключение к интернету";
            else if (e.Message.Contains("refused") || e.Message.Contains("Refused"))
                userMessage = "Соединение отклонено. Лобби может быть недоступно";
            
            UpdateStatusText(userMessage, false);
            Debug.LogError($"[LobbyManager] ✗ Ошибка подключения: {e.Message}\n{e.StackTrace}");
        }

        UpdateUI();
    }
    
    // StartClientConnection больше не нужен для FizzySteamworks
    // Подключение происходит напрямую через ConnectToSteamId
    
    System.Collections.IEnumerator WaitForDisconnectAndConnect(string ipAddress, string password, ushort port = 0) // port не используется для FizzySteamworks
    {
        // Ждем отключения
        int attempts = 0;
        while (networkManager != null && (NetworkClient.active || (NetworkServer.active && NetworkClient.active)) && attempts < 20)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (networkManager != null && (NetworkClient.active || (NetworkServer.active && NetworkClient.active)))
        {
            Debug.LogError("[LobbyManager] Не удалось отключиться! Невозможно подключиться к новому лобби.");
            yield break;
        }
        
        // Ждем еще немного для полного закрытия сокета
        yield return new WaitForSeconds(0.5f);
        
        // Подключаемся к новому лобби
        ConnectToLobbyInternal(ipAddress, password, port);
    }
    
    // VerifyConnection больше не нужен для FizzySteamworks
    // Подключение через Steam обрабатывается автоматически

    /// <summary>
    /// Публичный метод для закрытия и удаления лобби
    /// </summary>
    public void CloseAndDestroyLobby()
    {
        DisconnectFromCurrentLobby();
    }
    
    void DisconnectFromCurrentLobby()
    {
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
                return;
        }

        bool wasHost = NetworkServer.active && NetworkClient.active;
        bool wasClient = NetworkClient.active;

        // Очищаем словарь игроков перед отключением
        if (playerLobbyItems != null)
        {
            foreach (var item in playerLobbyItems.Values)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            playerLobbyItems.Clear();
        }

        // Очищаем список игроков в UI перед отключением
        ClearPlayersList();

        // Отключаемся от сети
        // Используем безопасные методы остановки, которые предотвращают перезагрузку сцены Menu
        if (wasHost)
        {
            Debug.Log("[LobbyManager] Останавливаем хост...");
            try
            {
                // Используем безопасный метод остановки хоста
                if (networkManager is MirrorNetworkManager mirrorManager)
                {
                    mirrorManager.StopHostSafe();
                }
                else
                {
                    networkManager.StopHost();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Ошибка при остановке хоста: {e.Message}");
            }
        }
        else if (wasClient)
        {
            Debug.Log("[LobbyManager] Отключаемся от сервера...");
            try
            {
                // Используем безопасный метод остановки клиента
                if (networkManager is MirrorNetworkManager mirrorManager)
                {
                    mirrorManager.StopClientSafe();
                }
                else
                {
                    networkManager.StopClient();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Ошибка при отключении клиента: {e.Message}");
            }
        }
        
        // Transport закроется автоматически при Shutdown()
        // Дополнительное время ожидания будет в корутине ConnectAfterDisconnect

        UpdateUI();
    }
    

    void ClearPlayersList()
    {
        // Удаляем все UI элементы игроков
        foreach (var playerItem in playerLobbyItems.Values)
        {
            if (playerItem != null)
            {
                Destroy(playerItem);
            }
        }
        playerLobbyItems.Clear();
    }

    /// <summary>
    /// Генерирует случайный 6-значный цифровой пароль
    /// </summary>
    private string GenerateRandomPassword()
    {
        System.Random random = new System.Random();
        return random.Next(100000, 999999).ToString();
    }

}



