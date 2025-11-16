using UnityEngine;
using Mirror;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class MirrorNetworkManager : NetworkManager
{
    [Header("Steam Settings")]
    [Tooltip("Steam App ID (из steam_appid.txt или настроек Steam)")]
    public uint steamAppId = 480;
    
    [Header("Lobby Settings")]
    [Tooltip("Максимальное количество игроков")]
    public int maxPlayers = 8;
    
    [Header("Connection Settings")]
    [Tooltip("Таймаут подключения в секундах")]
    public float connectionTimeout = 10f;
    
    [System.NonSerialized]
    private MonoBehaviour fizzyTransport;
    
    private float connectionStartTime = 0f;
    private bool isConnecting = false;
    
    public static MirrorNetworkManager Instance { get; private set; }
    
    public override void Awake()
    {
        base.Awake();
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "Menu" && !string.IsNullOrEmpty(onlineScene))
        {
            onlineScene = "";
        }
        
        System.Type transportType = null;
        
        string[] possibleNamespaces = new string[]
        {
            "FizzySteamworks",
            "com.mirror.steamworks.net",
            "Mirror.Steamworks"
        };
        
        foreach (var ns in possibleNamespaces)
        {
            transportType = System.Type.GetType(ns + ".FizzySteamworks, Assembly-CSharp") ??
                          System.Type.GetType(ns + ".FizzySteamworks");
            if (transportType != null) break;
        }
        
        if (transportType == null)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                transportType = assembly.GetType("FizzySteamworks") ?? 
                               assembly.GetType("com.mirror.steamworks.net.FizzySteamworks");
                if (transportType != null) break;
            }
        }
        
        if (transportType != null)
        {
            fizzyTransport = GetComponent(transportType) as MonoBehaviour;
        }
        
        if (fizzyTransport == null)
        {
            Component[] allComponents = GetComponents<Component>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().Name == "FizzySteamworks")
                {
                    fizzyTransport = comp as MonoBehaviour;
                    break;
                }
            }
        }
        
        if (fizzyTransport != null)
        {
            #if !DISABLESTEAMWORKS
            if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
            {
                #if !DISABLESTEAMWORKS
                steamAppId = SteamUtils.GetAppID().m_AppId;
                #else
                steamAppId = 0;
                #endif
            }
            #endif
        }
        
        RegisterServerEventHandlers();
        
        // Запускаем проверку таймаута подключения
        StartCoroutine(CheckConnectionTimeout());
    }
    
    void RegisterServerEventHandlers()
    {
    }
    
    /// <summary>
    /// Проверяет таймаут подключения
    /// </summary>
    System.Collections.IEnumerator CheckConnectionTimeout()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            
            // Проверяем, подключен ли клиент
            if (isConnecting && NetworkClient.active && !NetworkClient.isConnected)
            {
                float elapsedTime = Time.time - connectionStartTime;
                if (elapsedTime > connectionTimeout)
                {
                    Debug.LogWarning($"[MirrorNetworkManager] Таймаут подключения ({connectionTimeout} секунд). Отключаемся...");
                    StopClient();
                    isConnecting = false;
                    
                    // Уведомляем LobbyManager о таймауте
                    LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
                    if (lobbyManager != null)
                    {
                        lobbyManager.UpdateStatusText($"Таймаут подключения. Сервер не отвечает ({connectionTimeout} секунд)", false);
                    }
                }
            }
            else if (NetworkClient.isConnected)
            {
                isConnecting = false;
            }
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[MirrorNetworkManager] ✓ Сервер запущен в сцене: {currentScene}");
        
        // КРИТИЧЕСКИ ВАЖНО: Предотвращаем автоматическое переключение сцены при старте сервера в Menu
        // Если мы в сцене Menu, НЕ меняем сцену - игроки должны остаться в меню с лобби
        if (currentScene == "Menu")
        {
            // Очищаем onlineScene, чтобы Mirror не переключал сцену автоматически
            onlineScene = "";
            Debug.Log("[MirrorNetworkManager] Мы в сцене Menu - автоматическое переключение сцены отключено");
        }
        
        // Логируем информацию о транспорте
        if (fizzyTransport != null)
        {
            Debug.Log($"[MirrorNetworkManager] FizzySteamworks транспорт активен на сервере. Тип: {fizzyTransport.GetType().Name}");
            
            // Пытаемся получить информацию о сервере через рефлексию
            try
            {
                var serverField = fizzyTransport.GetType().GetField("server", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (serverField != null)
                {
                    var server = serverField.GetValue(fizzyTransport);
                    Debug.Log($"[MirrorNetworkManager] FizzySteamworks сервер найден: {server?.GetType().Name ?? "null"}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MirrorNetworkManager] Не удалось получить информацию о сервере FizzySteamworks: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[MirrorNetworkManager] FizzySteamworks транспорт не найден на сервере!");
        }
        
        // Запускаем периодическую проверку подключений
        StartCoroutine(CheckServerConnections());
        
        // Уведомляем LobbyManager о запуске сервера
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.OnMirrorServerStarted();
        }
    }
    
    /// <summary>
    /// Периодически проверяет подключения на сервере для отладки
    /// </summary>
    System.Collections.IEnumerator CheckServerConnections()
    {
        while (NetworkServer.active)
        {
            yield return new WaitForSeconds(2f);
        }
    }
    
    public override void OnStopServer()
    {
        // КРИТИЧЕСКИ ВАЖНО: Очищаем offlineScene при остановке сервера,
        // чтобы Mirror не перезагружал сцену автоматически
        // Это нужно как для Menu, так и для других сцен (например, при переходе в Lobby)
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedOfflineScene = offlineScene;
        
        // Очищаем offlineScene для всех сцен, чтобы предотвратить автоматическую перезагрузку
        offlineScene = "";
        Debug.Log($"[MirrorNetworkManager] Очищаем offlineScene для предотвращения автоматической перезагрузки сцены. Текущая сцена: {currentScene}");
        
        base.OnStopServer();
        
        // Восстанавливаем offlineScene после остановки только если мы не переходим в другую сцену
        // Если мы переходим в другую сцену (например, Lobby), не восстанавливаем offlineScene
        if (currentScene == "Menu" && savedOfflineScene == "Menu")
        {
            offlineScene = savedOfflineScene;
        }
        
        Debug.Log("[MirrorNetworkManager] ✗ Сервер остановлен");
    }
    
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        
        // Получаем connectionId клиента через рефлексию
        uint clientConnectionId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                clientConnectionId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        
        // Сбрасываем флаг подключения при успешном подключении
        isConnecting = false;
        
        Debug.Log($"[MirrorNetworkManager] ✓ Клиент подключен к серверу (connectionId={clientConnectionId})");
        
        // Уведомляем LobbyManager о подключении клиента (на стороне клиента)
        // ВАЖНО: На клиенте мы можем получить свой connectionId через рефлексию
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            // Запускаем клиентскую логику подключения (без connectionId, так как connectionId может быть 0 для хоста)
            lobbyManager.OnClientConnected();
        }
        else
        {
            Debug.LogWarning("[MirrorNetworkManager] LobbyManager не найден на клиенте при подключении! Клиент не сможет отправить свое Steam имя.");
        }
    }
    
    public override void OnClientDisconnect()
    {
        // Получаем connectionId клиента перед отключением (если еще доступен)
        uint clientConnectionId = 0;
        try
        {
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    clientConnectionId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MirrorNetworkManager] Не удалось получить connectionId при отключении: {e.Message}");
        }
        
        // КРИТИЧЕСКИ ВАЖНО: Если мы в сцене Menu, очищаем offlineScene,
        // чтобы Mirror не перезагружал сцену при отключении клиента
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "Menu")
        {
            string savedOfflineScene = offlineScene;
            offlineScene = "";
            Debug.Log("[MirrorNetworkManager] Мы в сцене Menu - очищаем offlineScene для предотвращения перезагрузки сцены");
            
            base.OnClientDisconnect();
            
            // Восстанавливаем offlineScene после отключения
            offlineScene = savedOfflineScene;
        }
        else
        {
            base.OnClientDisconnect();
        }
        
        // Сбрасываем флаг подключения при отключении
        isConnecting = false;
        
        Debug.Log($"[MirrorNetworkManager] ✗ Клиент отключен от сервера (connectionId={clientConnectionId})");
    }
    
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        
        // Получаем информацию о подключении для логирования
        string connInfo = $"connectionId={conn.connectionId}";
        try
        {
            // Пытаемся получить Steam ID подключения через рефлексию
            var steamIdField = conn.GetType().GetField("steamId");
            if (steamIdField != null)
            {
                var steamId = steamIdField.GetValue(conn);
                connInfo += $", steamId={steamId}";
            }
        }
        catch { }
        
        Debug.Log($"[MirrorNetworkManager] ✓ Клиент {conn.connectionId} подключился к серверу. Всего подключений: {NetworkServer.connections.Count}, {connInfo}");
        
        // Уведомляем LobbyManager о подключении клиента
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            Debug.Log($"[MirrorNetworkManager] Уведомляем LobbyManager о подключении клиента {conn.connectionId}");
            lobbyManager.OnMirrorClientConnected((uint)conn.connectionId);
        }
        else
        {
            Debug.LogWarning("[MirrorNetworkManager] LobbyManager не найден!");
        }
        
        // Уведомляем GameManager о подключении клиента (если мы в игровой сцене)
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnMirrorClientConnected((uint)conn.connectionId);
        }
    }
    
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"[MirrorNetworkManager] ✗ Клиент {conn.connectionId} отключился от сервера");
        
        // Уведомляем LobbyManager об отключении клиента
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.OnMirrorClientDisconnected((uint)conn.connectionId);
        }
        
        // Уведомляем GameManager об отключении клиента (если мы в игровой сцене)
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnMirrorClientDisconnected((uint)conn.connectionId);
        }
    }
    
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        Debug.Log($"[MirrorNetworkManager] Игрок добавлен для подключения {conn.connectionId}");
    }
    
    /// <summary>
    /// Останавливает хост с проверкой на перезагрузку сцены Menu
    /// </summary>
    public void StopHostSafe()
    {
        // КРИТИЧЕСКИ ВАЖНО: Если мы в сцене Menu, очищаем offlineScene,
        // чтобы Mirror не перезагружал сцену при остановке хоста
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedOfflineScene = null;
        bool needToClearOfflineScene = (currentScene == "Menu" && !string.IsNullOrEmpty(offlineScene));
        
        if (needToClearOfflineScene)
        {
            savedOfflineScene = offlineScene;
            offlineScene = "";
            Debug.Log("[MirrorNetworkManager] Мы в сцене Menu - очищаем offlineScene для предотвращения перезагрузки сцены при остановке хоста");
        }
        
        StopHost();
        
        // Восстанавливаем offlineScene после остановки
        if (needToClearOfflineScene && !string.IsNullOrEmpty(savedOfflineScene))
        {
            offlineScene = savedOfflineScene;
        }
    }
    
    /// <summary>
    /// Останавливает клиента с проверкой на перезагрузку сцены Menu
    /// </summary>
    public void StopClientSafe()
    {
        // КРИТИЧЕСКИ ВАЖНО: Если мы в сцене Menu, очищаем offlineScene,
        // чтобы Mirror не перезагружал сцену при остановке клиента
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedOfflineScene = null;
        bool needToClearOfflineScene = (currentScene == "Menu" && !string.IsNullOrEmpty(offlineScene));
        
        if (needToClearOfflineScene)
        {
            savedOfflineScene = offlineScene;
            offlineScene = "";
            Debug.Log("[MirrorNetworkManager] Мы в сцене Menu - очищаем offlineScene для предотвращения перезагрузки сцены при остановке клиента");
        }
        
        StopClient();
        
        // Восстанавливаем offlineScene после остановки
        if (needToClearOfflineScene && !string.IsNullOrEmpty(savedOfflineScene))
        {
            offlineScene = savedOfflineScene;
        }
    }
    
    /// <summary>
    /// Запускает хост через FizzySteamworks
    /// </summary>
    public void StartHostGame()
    {
        if (fizzyTransport == null)
        {
            Debug.LogError("[MirrorNetworkManager] FizzySteamworks транспорт не найден!");
            return;
        }
        
        // Убеждаемся, что Steam инициализирован
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null)
        {
            Debug.LogError("[MirrorNetworkManager] SteamManager не найден! Убедитесь, что SteamManager добавлен в сцену.");
            return;
        }
        
        if (!SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogWarning("[MirrorNetworkManager] Steam не инициализирован! Пытаемся инициализировать...");
            bool initialized = SteamManager.Instance.InitializeSteam();
            if (!initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Не удалось инициализировать Steam! Убедитесь, что Steam запущен.");
                return;
            }
        }
        #endif
        
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[MirrorNetworkManager] Запуск хоста в сцене: {currentScene}");
        
        // КРИТИЧЕСКИ ВАЖНО: Если мы в сцене Menu, предотвращаем автоматическое переключение сцены
        // Сохраняем текущую onlineScene и очищаем её временно, чтобы остаться в Menu
        string savedOnlineScene = onlineScene;
        if (currentScene == "Menu")
        {
            onlineScene = "";
            Debug.Log("[MirrorNetworkManager] Автоматическое переключение сцены отключено - остаемся в Menu");
        }
        
        Debug.Log("[MirrorNetworkManager] Запуск хоста...");
        StartHost();
        
        // Восстанавливаем onlineScene после старта (для будущих переходов через кнопку "Начать игру")
        if (currentScene == "Menu")
        {
            // Не восстанавливаем сразу - пусть остается пустой до нажатия кнопки
            // onlineScene будет установлена в LobbyManager при нажатии "Начать игру"
        }
    }
    
    /// <summary>
    /// Подключается к серверу через Steam ID
    /// </summary>
    public void ConnectToSteamId(ulong steamId)
    {
        if (steamId == 0)
        {
            Debug.LogError("[MirrorNetworkManager] Невалидный Steam ID (0)!");
            return;
        }
        
        if (fizzyTransport == null)
        {
            Debug.LogError("[MirrorNetworkManager] FizzySteamworks транспорт не найден!");
            return;
        }
        
        // Убеждаемся, что Steam инициализирован
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null)
        {
            Debug.LogError("[MirrorNetworkManager] SteamManager не найден! Убедитесь, что SteamManager добавлен в сцену.");
            return;
        }
        
        if (!SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogWarning("[MirrorNetworkManager] Steam не инициализирован! Пытаемся инициализировать...");
            bool initialized = SteamManager.Instance.InitializeSteam();
            if (!initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Не удалось инициализировать Steam! Убедитесь, что Steam запущен.");
                return;
            }
        }
        
        // Проверяем, что мы не пытаемся подключиться к себе
        ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
        if (steamId == mySteamId)
        {
            Debug.LogWarning("[MirrorNetworkManager] Попытка подключиться к собственному Steam ID! Пропускаем.");
            return;
        }
        #endif
        
        Debug.Log($"[MirrorNetworkManager] Подключение к Steam ID: {steamId}");
        
        // КРИТИЧЕСКИ ВАЖНО: Останавливаем хост/сервер/клиент перед подключением к удаленному серверу
        // Если мы хост, мы не должны подключаться как клиент к другому серверу
        // Если мы уже клиент, нужно остановить текущее подключение
        if (NetworkServer.active)
        {
            Debug.LogWarning("[MirrorNetworkManager] Сервер активен! Останавливаем сервер перед подключением к удаленному серверу...");
            StopServerSafe();
        }
        
        if (NetworkClient.active)
        {
            Debug.Log("[MirrorNetworkManager] Клиент уже активен. Останавливаем текущее подключение...");
            StopClientSafe();
            // Ждем немного, чтобы подключение закрылось
            StartCoroutine(ConnectAfterStopClient(steamId));
            return;
        }
        
        // FizzySteamworks использует Steam ID как адрес в формате строки
        // Это основной способ подключения в FizzySteamworks
        networkAddress = steamId.ToString();
        Debug.Log($"[MirrorNetworkManager] Установлен networkAddress: {networkAddress}");
        
        // Дополнительно пытаемся установить targetSteamId через рефлексию (если поддерживается)
        // Это необязательно, но может помочь в некоторых версиях FizzySteamworks
        if (fizzyTransport != null)
        {
            try
            {
                // Пробуем разные варианты имени поля/свойства
                string[] possibleNames = new string[] { "targetSteamId", "TargetSteamId", "targetSteamID", "TargetSteamID", "steamId", "SteamId" };
                bool found = false;
                
                foreach (string name in possibleNames)
                {
                    var field = fizzyTransport.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(ulong))
                    {
                        field.SetValue(fizzyTransport, steamId);
                        Debug.Log($"[MirrorNetworkManager] targetSteamId установлен через поле '{name}': {steamId}");
                        found = true;
                        break;
                    }
                    
                    var prop = fizzyTransport.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(ulong))
                    {
                        prop.SetValue(fizzyTransport, steamId);
                        Debug.Log($"[MirrorNetworkManager] targetSteamId установлен через свойство '{name}': {steamId}");
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    Debug.Log("[MirrorNetworkManager] targetSteamId не найден - используется только networkAddress (это нормально для большинства версий FizzySteamworks)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MirrorNetworkManager] Ошибка при установке targetSteamId через рефлексию: {e.Message}. Используется только networkAddress.");
            }
        }
        
        Debug.Log($"[MirrorNetworkManager] Запуск клиента для подключения к Steam ID: {steamId}");
        
        // Записываем время начала подключения для таймаута
        connectionStartTime = Time.time;
        isConnecting = true;
        
        try
        {
            StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MirrorNetworkManager] Ошибка при запуске клиента: {e.Message}");
            isConnecting = false;
            throw;
        }
    }
    
    /// <summary>
    /// Подключается к Steam ID после остановки клиента
    /// </summary>
    System.Collections.IEnumerator ConnectAfterStopClient(ulong steamId)
    {
        // Ждем остановки клиента
        int attempts = 0;
        while (NetworkClient.active && attempts < 30)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (NetworkClient.active)
        {
            Debug.LogError("[MirrorNetworkManager] Не удалось остановить клиент перед подключением!");
            yield break;
        }
        
        // Ждем еще немного для полного закрытия соединения
        yield return new WaitForSeconds(0.5f);
        
        // Подключаемся к новому серверу
        ConnectToSteamId(steamId);
    }
    
    /// <summary>
    /// Останавливает сервер с проверкой на перезагрузку сцены Menu
    /// </summary>
    public void StopServerSafe()
    {
        // КРИТИЧЕСКИ ВАЖНО: Если мы в сцене Menu, очищаем offlineScene,
        // чтобы Mirror не перезагружал сцену при остановке сервера
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedOfflineScene = null;
        bool needToClearOfflineScene = (currentScene == "Menu" && !string.IsNullOrEmpty(offlineScene));
        
        if (needToClearOfflineScene)
        {
            savedOfflineScene = offlineScene;
            offlineScene = "";
            Debug.Log("[MirrorNetworkManager] Мы в сцене Menu - очищаем offlineScene для предотвращения перезагрузки сцены при остановке сервера");
        }
        
        StopServer();
        
        // Восстанавливаем offlineScene после остановки
        if (needToClearOfflineScene && !string.IsNullOrEmpty(savedOfflineScene))
        {
            offlineScene = savedOfflineScene;
        }
    }
    
    /// <summary>
    /// Переопределяем OnApplicationQuit для безопасного выключения сети
    /// </summary>
    void OnApplicationQuit()
    {
        // КРИТИЧЕСКИ ВАЖНО: Сначала останавливаем сеть, затем Steam завершится автоматически
        // Это гарантирует, что транспорт закроет сокеты до завершения Steam
        
        // Останавливаем в правильном порядке: сначала клиент, потом сервер
        if (NetworkClient.active)
        {
            Debug.Log("[MirrorNetworkManager] Остановка клиента перед завершением приложения...");
            try
            {
                StopClient();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MirrorNetworkManager] Ошибка при остановке клиента: {e.Message}");
            }
        }
        
        if (NetworkServer.active)
        {
            Debug.Log("[MirrorNetworkManager] Остановка сервера перед завершением приложения...");
            try
            {
                StopServer();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MirrorNetworkManager] Ошибка при остановке сервера: {e.Message}");
            }
        }
        
        // Ждем немного, чтобы транспорт успел закрыть сокеты
        // В OnApplicationQuit мы не можем использовать корутины, поэтому просто даем время
        System.Threading.Thread.Sleep(100);
        
        // Steam завершится автоматически через SteamManager.OnApplicationQuit()
        // Не вызываем SteamAPI.Shutdown() здесь, чтобы избежать двойного вызова
        Debug.Log("[MirrorNetworkManager] Сеть остановлена. Steam будет завершен через SteamManager.");
    }
}

