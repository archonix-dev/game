using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Collections.Generic;

public class MultiplayerManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private InputField lobbyIdInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Text statusText;
    
    [Header("Network Settings")]
    // maxPlayers настраивается индивидуально для каждого сервера в dedicatedServers
    
    [Header("Lobby Settings")]
    [SerializeField] private string currentLobbyId = "";
    [SerializeField] private string currentPassword = "";
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableTransportFailureDetection = true;
    
    [Header("Dedicated Servers")]
    [SerializeField] public bool useDedicatedServers = false;
    [SerializeField] private List<DedicatedServer> dedicatedServers = new List<DedicatedServer>();
    [SerializeField] private string currentServerId = "";
    
    // Словарь для хранения активных лобби (в реальном проекте это должно быть на сервере)
    private static Dictionary<string, LobbyData> activeLobbies = new Dictionary<string, LobbyData>();
    
    [System.Serializable]
    public class LobbyData
    {
        public string lobbyId;
        public string password;
        public string relayJoinCode;
        public string hostPlayerId;
        public string serverId; // ID выделенного сервера
    }
    
    [System.Serializable]
    public class DedicatedServer
    {
        public string serverId;        // Уникальный ID сервера
        public string serverName;      // Отображаемое имя сервера
        public string ipAddress;       // IP адрес сервера
        public ushort port;           // Порт сервера
        public int maxPlayers;        // Максимальное количество игроков для этого сервера
        public bool isAvailable;      // Доступен ли сервер для новых лобби
        public string region;         // Регион сервера (Europe, America, Asia, etc.)
        public int currentPlayers;    // Текущее количество игроков на сервере
    }
    
    private NetworkManager networkManager;
    private UnityTransport transport;
    private bool isInitialized = false;
    private bool wasConnected = false;
    private bool isHandlingTransportFailure = false;
    private float connectionTime = 0f;
    private const float MIN_CONNECTION_TIME = 3f; // Минимум 3 секунды подключения
    
    void Start()
    {
        InitializeNetworkManager();
        SetupUI();
        SetupLocalServer();
        
        isInitialized = true;
        UpdateStatusText("Готов к подключению");
    }
    
    void Update()
    {
        if (!isInitialized || networkManager == null || isHandlingTransportFailure || !enableTransportFailureDetection) return;
        
        bool isCurrentlyConnected = networkManager.IsClient;
        
        // Отслеживаем время подключения
        if (isCurrentlyConnected && wasConnected)
        {
            connectionTime += Time.deltaTime;
        }
        else if (!isCurrentlyConnected)
        {
            connectionTime = 0f;
        }
        
        // Проверяем, не произошла ли неожиданная потеря соединения
        // Только если мы были подключены достаточно долго
        if (wasConnected && !isCurrentlyConnected && connectionTime >= MIN_CONNECTION_TIME)
        {
            Debug.LogWarning($"Unexpected disconnection detected after {connectionTime:F1}s - possible transport failure");
            HandleTransportFailure();
        }
        
        wasConnected = isCurrentlyConnected;
    }
    
    void InitializeNetworkManager()
    {
        // Проверяем наличие NetworkManager
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager.Singleton is null! Make sure NetworkManager is in the scene.");
            UpdateStatusText("Ошибка: NetworkManager не найден!");
            return;
        }
        
        // Получаем UnityTransport
        transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component not found on NetworkManager!");
            UpdateStatusText("Ошибка: UnityTransport не найден!");
            return;
        }
        
        // Транспорт будет настроен при создании/подключении к лобби
        
        // Подписываемся на события NetworkManager
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        
        Debug.Log("NetworkManager initialized successfully");
    }
    
    
    
    void SetupUI()
    {
        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(CreateLobby);
            
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.AddListener(JoinLobby);
    }
    
    void SetupLocalServer()
    {
        // Инициализация выделенных серверов
        Debug.Log("MultiplayerManager инициализирован для работы с удаленными серверами");
    }
    
    void CheckServerStatus()
    {
        // Проверяем состояние NetworkManager
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"NetworkManager состояние - IsServer: {NetworkManager.Singleton.IsServer}, IsHost: {NetworkManager.Singleton.IsHost}, IsClient: {NetworkManager.Singleton.IsClient}");
        }
    }
    
    public void CreateLobby()
    {
        Debug.Log("CreateLobby called");
        
        if (!isInitialized || networkManager == null)
        {
            UpdateStatusText("Ошибка: NetworkManager не инициализирован!");
            return;
        }
        
        if (lobbyIdInput == null || passwordInput == null) 
        {
            UpdateStatusText("Ошибка: UI элементы не назначены!");
            return;
        }
        
        string lobbyId = lobbyIdInput.text.Trim();
        string password = passwordInput.text.Trim();
        
        if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(password))
        {
            UpdateStatusText("Введите ID лобби и пароль!");
            return;
        }
        
        if (networkManager.IsClient)
        {
            UpdateStatusText("Уже подключен к серверу!");
            return;
        }
        
        // Проверяем, не существует ли уже лобби с таким ID
        if (activeLobbies.ContainsKey(lobbyId))
        {
            UpdateStatusText("Лобби с таким ID уже существует!");
            return;
        }
        
        UpdateStatusText("Создание лобби...");
        Debug.Log($"Attempting to create lobby: {lobbyId}");
        
        try
        {
            // Ищем свободный выделенный сервер
            UpdateStatusText("Поиск свободного сервера...");
            DedicatedServer availableServer = FindAvailableServer();
            
            if (availableServer == null)
            {
                UpdateStatusText("Нет доступных серверов!\nПопробуйте позже.");
                return;
            }
            
            // Настраиваем транспорт для выделенного сервера
            transport.ConnectionData.Address = availableServer.ipAddress;
            transport.ConnectionData.Port = availableServer.port;
            
            // Помечаем сервер как занятый
            MarkServerAsOccupied(availableServer.serverId);
            currentServerId = availableServer.serverId;
            
            // Сохраняем данные лобби в глобальном словаре
            LobbyData lobbyData = new LobbyData
            {
                lobbyId = lobbyId,
                password = password,
                relayJoinCode = "",
                hostPlayerId = "dedicated",
                serverId = availableServer.serverId
            };
            activeLobbies[lobbyId] = lobbyData;
            
            UpdateStatusText($"Сервер найден!\n{availableServer.serverName}\nIP: {availableServer.ipAddress}:{availableServer.port}\nПодключение...");
            
            // Сохраняем данные лобби
            currentLobbyId = lobbyId;
            currentPassword = password;
            
            // Подключаемся к серверу как клиент
            Debug.Log($"Attempting to connect to server: {availableServer.ipAddress}:{availableServer.port}");
            
            bool success = networkManager.StartClient();
            if (success)
            {
                string statusMessage = $"Лобби создано!\nID: {lobbyId}\nПароль: {password}\nСервер: {availableServer.serverName}\nIP: {availableServer.ipAddress}:{availableServer.port}\nПодключение к серверу...";
                UpdateStatusText(statusMessage);
                Debug.Log($"Lobby created and connecting to dedicated server: {lobbyId} with password: {password}");
            }
            else
            {
                UpdateStatusText("Ошибка подключения к серверу!");
                Debug.LogError("Failed to start client");
                // Удаляем лобби из словаря при ошибке
                activeLobbies.Remove(lobbyId);
                // Освобождаем сервер
                MarkServerAsAvailable(availableServer.serverId);
                currentServerId = "";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creating lobby: {e.Message}");
            
            // Очищаем лобби из словаря при ошибке
            if (activeLobbies.ContainsKey(lobbyId))
            {
                activeLobbies.Remove(lobbyId);
            }
            
            // Освобождаем сервер при ошибке
            if (!string.IsNullOrEmpty(currentServerId))
            {
                MarkServerAsAvailable(currentServerId);
                currentServerId = "";
            }
            
            UpdateStatusText($"Ошибка создания лобби: {e.Message}");
        }
        
        UpdateUI();
    }
    
    public void JoinLobby()
    {
        Debug.Log("JoinLobby called");
        
        if (!isInitialized || networkManager == null)
        {
            UpdateStatusText("Ошибка: NetworkManager не инициализирован!");
            return;
        }
        
        if (lobbyIdInput == null || passwordInput == null) 
        {
            UpdateStatusText("Ошибка: UI элементы не назначены!");
            return;
        }
        
        string lobbyId = lobbyIdInput.text.Trim();
        string password = passwordInput.text.Trim();
        
        if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(password))
        {
            UpdateStatusText("Введите ID лобби и пароль!");
            return;
        }
        
        if (networkManager.IsClient)
        {
            UpdateStatusText("Уже подключен к серверу!");
            return;
        }
        
        UpdateStatusText("Поиск лобби...");
        Debug.Log($"Attempting to join lobby: {lobbyId}");
        
        try
        {
            // Ищем лобби в словаре
            if (!activeLobbies.ContainsKey(lobbyId))
            {
                UpdateStatusText("Лобби не найдено!");
                return;
            }
            
            LobbyData lobbyData = activeLobbies[lobbyId];
            
            // Проверяем пароль
            if (lobbyData.password != password)
            {
                UpdateStatusText("Неверный пароль!");
                return;
            }
            
            UpdateStatusText("Подключение к лобби...");
            
            // Подключаемся к выделенному серверу
            if (string.IsNullOrEmpty(lobbyData.serverId))
            {
                UpdateStatusText("Ошибка: Лобби не имеет назначенного сервера!");
                return;
            }
            
            DedicatedServer server = FindServerById(lobbyData.serverId);
            if (server != null)
            {
                transport.ConnectionData.Address = server.ipAddress;
                transport.ConnectionData.Port = server.port;
                UpdateStatusText($"Подключение к серверу {server.serverName}...");
            }
            else
            {
                UpdateStatusText("Ошибка: Сервер не найден!");
                return;
            }
            
            // Запускаем клиент
            bool success = networkManager.StartClient();
            if (success)
            {
                UpdateStatusText("Подключение...");
                Debug.Log($"Joining lobby: {lobbyId}");
            }
            else
            {
                UpdateStatusText("Ошибка подключения!");
                Debug.LogError("Failed to start client");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error joining lobby: {e.Message}");
            UpdateStatusText($"Ошибка подключения: {e.Message}");
        }
        
        UpdateUI();
    }
    
    
    public void Disconnect()
    {
        if (networkManager.IsClient)
        {
            // Освобождаем выделенный сервер при отключении
            if (!string.IsNullOrEmpty(currentServerId))
            {
                DecrementServerPlayerCount(currentServerId);
                Debug.Log($"Decremented player count for server {currentServerId}");
            }
            
            networkManager.Shutdown();
            UpdateStatusText("Отключено");
            Debug.Log("Disconnected from network");
        }
        
        // Сбрасываем текущие данные лобби
        currentLobbyId = "";
        currentPassword = "";
        currentServerId = "";
        
        UpdateUI();
    }
    
    void OnClientConnected(ulong clientId)
    {
        Debug.Log("Connected to Dedicated Server as Client");
        UpdateStatusText("Подключен к лобби");
        
        // Обновляем счетчик игроков на сервере
        if (!string.IsNullOrEmpty(currentServerId))
        {
            IncrementServerPlayerCount(currentServerId);
        }
        
        wasConnected = true;
        UpdateUI();
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Disconnected from Dedicated Server");
        UpdateStatusText("Отключен от лобби");
        
        // Уменьшаем счетчик игроков на сервере
        if (!string.IsNullOrEmpty(currentServerId))
        {
            DecrementServerPlayerCount(currentServerId);
        }
        
        UpdateUI();
    }
    
    
    void HandleTransportFailure()
    {
        if (isHandlingTransportFailure) return; // Предотвращаем множественные вызовы
        
        isHandlingTransportFailure = true;
        
        Debug.LogError("Transport failure detected! Connection lost.");
        UpdateStatusText("Ошибка подключения к серверу!\nПопытка переподключения...");
        
        // Уменьшаем счетчик игроков на сервере при ошибке
        if (!string.IsNullOrEmpty(currentServerId))
        {
            DecrementServerPlayerCount(currentServerId);
            Debug.Log($"Decremented player count for server {currentServerId} due to transport failure");
        }
        
        // Отключаемся от сети
        if (networkManager.IsClient)
        {
            networkManager.Shutdown();
        }
        
        // Сбрасываем состояние
        currentLobbyId = "";
        currentPassword = "";
        wasConnected = false;
        connectionTime = 0f;
        
        UpdateUI();
        
        // Показываем сообщение пользователю
        UpdateStatusText("Соединение потеряно!\nСоздайте новое лобби или попробуйте подключиться снова.");
        
        // Сбрасываем флаг через небольшую задержку
        Invoke(nameof(ResetTransportFailureFlag), 2f);
    }
    
    void ResetTransportFailureFlag()
    {
        isHandlingTransportFailure = false;
    }
    
    void UpdateUI()
    {
        if (networkManager == null) return;
        
        bool isConnected = networkManager.IsClient;
        
        if (createLobbyButton != null)
            createLobbyButton.interactable = !isConnected;
            
        if (joinLobbyButton != null)
            joinLobbyButton.interactable = !isConnected;
            
        if (lobbyIdInput != null)
            lobbyIdInput.interactable = !isConnected;
            
        if (passwordInput != null)
            passwordInput.interactable = !isConnected;
    }
    
    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"Multiplayer Status: {message}");
    }
    
    // Публичные методы для получения информации
    public bool IsConnected()
    {
        return networkManager.IsClient;
    }
    
    public bool IsClient()
    {
        return networkManager.IsClient;
    }
    
    public string GetLobbyId()
    {
        return lobbyIdInput != null ? lobbyIdInput.text.Trim() : "";
    }
    
    public string GetPassword()
    {
        return passwordInput != null ? passwordInput.text.Trim() : "";
    }
    
    public string GetCurrentLobbyId()
    {
        return currentLobbyId;
    }
    
    public string GetCurrentPassword()
    {
        return currentPassword;
    }
    
    public bool CheckLobbyCredentials(string lobbyId, string password)
    {
        return currentLobbyId == lobbyId && currentPassword == password;
    }
    
    
    public static Dictionary<string, LobbyData> GetActiveLobbies()
    {
        return activeLobbies;
    }
    
    public static void ClearActiveLobbies()
    {
        activeLobbies.Clear();
    }
    
    // Методы для работы с выделенными серверами
    public void AddDedicatedServer(string serverId, string serverName, string ipAddress, ushort port, int maxPlayers, string region = "")
    {
        DedicatedServer server = new DedicatedServer
        {
            serverId = serverId,
            serverName = serverName,
            ipAddress = ipAddress,
            port = port,
            maxPlayers = maxPlayers,
            isAvailable = true,
            region = region,
            currentPlayers = 0
        };
        
        dedicatedServers.Add(server);
        Debug.Log($"Added dedicated server: {serverName} ({ipAddress}:{port})");
    }
    
    public void RemoveDedicatedServer(string serverId)
    {
        dedicatedServers.RemoveAll(server => server.serverId == serverId);
        Debug.Log($"Removed dedicated server: {serverId}");
    }
    
    public DedicatedServer FindAvailableServer()
    {
        return dedicatedServers.Find(server => server.isAvailable && server.currentPlayers < server.maxPlayers);
    }
    
    public DedicatedServer FindServerById(string serverId)
    {
        return dedicatedServers.Find(server => server.serverId == serverId);
    }
    
    public void MarkServerAsOccupied(string serverId)
    {
        DedicatedServer server = FindServerById(serverId);
        if (server != null)
        {
            server.isAvailable = false;
            server.currentPlayers = 1; // Первый игрок подключился
            Debug.Log($"Server {serverId} marked as occupied with {server.currentPlayers} players");
        }
    }
    
    public void MarkServerAsAvailable(string serverId)
    {
        DedicatedServer server = FindServerById(serverId);
        if (server != null)
        {
            server.isAvailable = true;
            server.currentPlayers = 0;
            Debug.Log($"Server {serverId} marked as available");
        }
    }
    
    public List<DedicatedServer> GetAllServers()
    {
        return new List<DedicatedServer>(dedicatedServers);
    }
    
    public void UpdateServerPlayerCount(string serverId, int playerCount)
    {
        DedicatedServer server = FindServerById(serverId);
        if (server != null)
        {
            server.currentPlayers = playerCount;
            server.isAvailable = playerCount < server.maxPlayers;
            Debug.Log($"Server {serverId} player count updated to {playerCount}/{server.maxPlayers}");
        }
    }
    
    public void IncrementServerPlayerCount(string serverId)
    {
        DedicatedServer server = FindServerById(serverId);
        if (server != null)
        {
            server.currentPlayers++;
            server.isAvailable = server.currentPlayers < server.maxPlayers;
            Debug.Log($"Server {serverId} player count incremented to {server.currentPlayers}/{server.maxPlayers}");
        }
    }
    
    public void DecrementServerPlayerCount(string serverId)
    {
        DedicatedServer server = FindServerById(serverId);
        if (server != null)
        {
            server.currentPlayers = Mathf.Max(0, server.currentPlayers - 1);
            server.isAvailable = server.currentPlayers < server.maxPlayers;
            Debug.Log($"Server {serverId} player count decremented to {server.currentPlayers}/{server.maxPlayers}");
        }
    }
    
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
