using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;

public class MultiplayerManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private InputField hostIpInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button connectClientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Text statusText;
    [SerializeField] private Text localIpText;
    
    [Header("Network Settings")]
    [SerializeField] private ushort defaultPort = 7777;
    [SerializeField] private int maxPlayers = 8;
    
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableTransportFailureDetection = true;
    
    private MirrorNetworkManager networkManager;
    private MonoBehaviour transport; // FizzySteamworks transport
    private bool isInitialized = false;
    private bool wasConnected = false;
    private bool isHandlingTransportFailure = false;
    private float connectionTime = 0f;
    private const float MIN_CONNECTION_TIME = 3f;
    private string localIpAddress = "";
    
    void Start()
    {
        InitializeNetworkManager();
        SetupUI();
        UpdateLocalIpDisplay();

        isInitialized = true;
        UpdateStatusText("Готов к подключению");
    }
    
    void Update()
    {
        if (!isInitialized || networkManager == null || isHandlingTransportFailure || !enableTransportFailureDetection) return;
        
        bool isCurrentlyConnected = NetworkClient.active || (NetworkServer.active && NetworkClient.active);
        
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
        // ВАЖНО: Не обрабатываем отключение, если оно произошло слишком быстро (может быть нормальным переподключением)
        if (wasConnected && !isCurrentlyConnected && connectionTime >= MIN_CONNECTION_TIME)
        {
            HandleTransportFailure();
        }
        
        wasConnected = isCurrentlyConnected;
    }
    
    void InitializeNetworkManager()
    {
        // Проверяем наличие MirrorNetworkManager
        networkManager = MirrorNetworkManager.Instance;
        if (networkManager == null)
        {
            UpdateStatusText("Ошибка: MirrorNetworkManager не найден!");
            return;
        }
        
        // Получаем FizzySteamworks транспорт
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
            UpdateStatusText("Ошибка: FizzySteamworks транспорт не найден!");
            return;
        }
        
        // Mirror обрабатывает события через MirrorNetworkManager
        // Подписка на события происходит автоматически
    }
    
    void SetupUI()
    {
        if (startHostButton != null)
            startHostButton.onClick.AddListener(StartHost);
            
        if (connectClientButton != null)
            connectClientButton.onClick.AddListener(ConnectAsClient);
            
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(Disconnect);
        
        // Устанавливаем порт по умолчанию
        if (portInput != null)
        {
            portInput.text = defaultPort.ToString();
        }
    }
    
    void UpdateLocalIpDisplay()
    {
        string localIp = GetLocalIpAddress();
        localIpAddress = localIp;
        
        if (localIpText != null)
        {
            localIpText.text = $"Ваш IP: {localIp}";
        }
    }
    
    public void StartHost()
    {
        if (!isInitialized || networkManager == null)
        {
            UpdateStatusText("Ошибка: NetworkManager не инициализирован!");
            return;
        }
        
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active)
        {
            UpdateStatusText("Уже подключен!");
            return;
        }
        
        // Получаем порт
        ushort port = defaultPort;
        if (portInput != null && !string.IsNullOrEmpty(portInput.text))
        {
            if (!ushort.TryParse(portInput.text, out port))
            {
                UpdateStatusText("Неверный порт!");
                return;
            }
        }
        
        try
        {
            // FizzySteamworks не использует порты - подключение через Steam
            UpdateStatusText("Запуск сервера через Steam...");
            
            // Запускаем хост через MirrorNetworkManager
            networkManager.StartHostGame();
            
            UpdateStatusText("Сервер запущен через Steam!\nОжидание подключений через Steam лобби...");
        }
        catch (System.Exception e)
        {
            UpdateStatusText($"Ошибка запуска сервера: {e.Message}");
        }
        
        UpdateUI();
    }
    
    public void ConnectAsClient()
    {
        if (!isInitialized || networkManager == null)
        {
            UpdateStatusText("Ошибка: NetworkManager не инициализирован!");
            return;
        }
        
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active)
        {
            UpdateStatusText("Уже подключен!");
            return;
        }
        
        // Для FizzySteamworks подключение происходит через Steam ID
        // Получаем Steam ID хоста из лобби или ввода
        string hostInput = "";
        if (hostIpInput != null)
        {
            hostInput = hostIpInput.text.Trim();
        }
        
        ulong steamId = 0;
        if (!string.IsNullOrEmpty(hostInput) && ulong.TryParse(hostInput, out steamId))
        {
            // Введен Steam ID
            try
            {
                UpdateStatusText($"Подключение к Steam ID: {steamId}...");
                networkManager.ConnectToSteamId(steamId);
                UpdateStatusText("Подключение...");
            }
            catch (System.Exception e)
            {
                UpdateStatusText($"Ошибка подключения: {e.Message}");
            }
        }
        else
        {
            // Пытаемся получить Steam ID из текущего лобби
            SteamLobbyManager steamLobbyManager = FindObjectOfType<SteamLobbyManager>();
            if (steamLobbyManager != null)
            {
                ulong hostSteamId = steamLobbyManager.GetLobbyOwnerId();
                if (hostSteamId != 0)
                {
                    try
                    {
                        UpdateStatusText($"Подключение к Steam лобби: {hostSteamId}...");
                        networkManager.ConnectToSteamId(hostSteamId);
                        UpdateStatusText("Подключение...");
                    }
                    catch (System.Exception e)
                    {
                        UpdateStatusText($"Ошибка подключения: {e.Message}");
                    }
                }
                else
                {
                    UpdateStatusText("Не удалось получить Steam ID хоста из лобби!");
                }
            }
            else
            {
                UpdateStatusText("Введите Steam ID хоста или подключитесь к Steam лобби!");
            }
        }
        
        UpdateUI();
    }
    
    public void Disconnect()
    {
        if (networkManager == null) return;
        
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active || NetworkServer.active)
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
            UpdateStatusText("Отключено");
        }
        
        UpdateUI();
    }
    
    void OnServerStarted()
    {
        UpdateStatusText("Сервер запущен и готов к подключениям");
        UpdateUI();
    }
    
    // Методы OnClientConnected и OnClientDisconnected вызываются через MirrorNetworkManager
    // Они больше не нужны здесь, так как Mirror обрабатывает события автоматически
    
    void OnClientDisconnected(uint connectionId)
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            int playerCount = NetworkServer.connections.Count;
            UpdateStatusText($"Игрок отключен. Игроков: {playerCount}/{maxPlayers}");
        }
        else
        {
            UpdateStatusText("Отключен от сервера");
        }
        
        UpdateUI();
    }
    
    void HandleTransportFailure()
    {
        if (isHandlingTransportFailure) return;
        
        isHandlingTransportFailure = true;
        
        UpdateStatusText("Соединение потеряно!");
        
        // Отключаемся от сети
        if ((NetworkServer.active && NetworkClient.active) || NetworkClient.active || NetworkServer.active)
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
        
        wasConnected = false;
        connectionTime = 0f;
        
        UpdateUI();
        
        // Сбрасываем флаг через задержку
        Invoke(nameof(ResetTransportFailureFlag), 2f);
    }
    
    void ResetTransportFailureFlag()
    {
        isHandlingTransportFailure = false;
    }
    
    void UpdateUI()
    {
        if (networkManager == null) return;
        
        bool isConnected = (NetworkServer.active && NetworkClient.active) || NetworkClient.active;
        
        if (startHostButton != null)
            startHostButton.interactable = !isConnected;
            
        if (connectClientButton != null)
            connectClientButton.interactable = !isConnected;
            
        if (hostIpInput != null)
            hostIpInput.interactable = !isConnected;
            
        if (portInput != null)
            portInput.interactable = !isConnected;
        
        if (disconnectButton != null)
            disconnectButton.interactable = isConnected;
    }
    
    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[MultiplayerManager] {message}");
    }
    
    // Получение локального IP адреса
    string GetLocalIpAddress()
    {
        try
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            
            foreach (IPAddress ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MultiplayerManager] Ошибка получения локального IP: {e.Message}");
        }
        
        return "Не определен";
    }
    
    // Проверка валидности IP адреса
    bool IsValidIpAddress(string ipString)
    {
        if (string.IsNullOrEmpty(ipString))
            return false;
        
        string[] parts = ipString.Split('.');
        if (parts.Length != 4)
            return false;
        
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                return false;
        }
        
        return true;
    }
    
    // Публичные методы для получения информации
    public bool IsConnected()
    {
        return networkManager != null && ((NetworkServer.active && NetworkClient.active) || NetworkClient.active);
    }
    
    public bool IsHost()
    {
        return networkManager != null && NetworkServer.active && NetworkClient.active;
    }
    
    public bool IsClient()
    {
        return networkManager != null && NetworkClient.active;
    }
    
    public string GetLocalIp()
    {
        return localIpAddress;
    }
    
    public int GetPlayerCount()
    {
        if (networkManager == null) return 0;
        return NetworkServer.connections.Count;
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (networkManager != null)
        {
            // В Mirror события обрабатываются через переопределение методов в MirrorNetworkManager
            // Подписки на события не требуются
        }
    }
}
