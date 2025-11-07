using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
    
    [Header("VPN Detection")]
    [SerializeField] private bool autoDetectVpnIp = true;
    [SerializeField] private string[] vpnAdapterNames = { "Hamachi", "Radmin VPN", "TAP", "VirtualBox" };
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableTransportFailureDetection = true;
    
    private NetworkManager networkManager;
    private UnityTransport transport;
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
        
        bool isCurrentlyConnected = networkManager.IsClient || networkManager.IsHost;
        
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
        if (wasConnected && !isCurrentlyConnected && connectionTime >= MIN_CONNECTION_TIME)
        {
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
            UpdateStatusText("Ошибка: NetworkManager не найден!");
            return;
        }
        
        // Получаем UnityTransport
        transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            UpdateStatusText("Ошибка: UnityTransport не найден!");
            return;
        }
        
        // Подписываемся на события NetworkManager
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
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
        string vpnIp = GetVpnIpAddress();
        localIpAddress = vpnIp;
        
        if (localIpText != null)
        {
            if (!string.IsNullOrEmpty(vpnIp))
            {
                localIpText.text = $"Ваш VPN IP: {vpnIp}";
            }
            else
            {
                string localIp = GetLocalIpAddress();
                localIpText.text = $"Ваш IP: {localIp}";
            }
        }
    }
    
    public void StartHost()
    {
        if (!isInitialized || networkManager == null)
        {
            UpdateStatusText("Ошибка: NetworkManager не инициализирован!");
            return;
        }
        
        if (networkManager.IsHost || networkManager.IsClient)
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
            // Настраиваем транспорт для хоста
            transport.ConnectionData.Port = port;
            
            UpdateStatusText("Запуск сервера...");
            
            // Запускаем хост
            bool success = networkManager.StartHost();
            if (success)
            {
                string vpnIp = GetVpnIpAddress();
                string ipDisplay = !string.IsNullOrEmpty(vpnIp) ? vpnIp : GetLocalIpAddress();
                UpdateStatusText($"Сервер запущен!\nIP: {ipDisplay}\nПорт: {port}\nОжидание подключений...");
            }
            else
            {
                UpdateStatusText("Ошибка запуска сервера!");
            }
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
        
        if (networkManager.IsHost || networkManager.IsClient)
        {
            UpdateStatusText("Уже подключен!");
            return;
        }
        
        // Получаем IP адрес хоста
        string hostIp = "";
        if (hostIpInput != null)
        {
            hostIp = hostIpInput.text.Trim();
        }
        
        if (string.IsNullOrEmpty(hostIp))
        {
            UpdateStatusText("Введите IP адрес хоста!");
            return;
        }
        
        // Проверяем валидность IP
        if (!IsValidIpAddress(hostIp))
        {
            UpdateStatusText("Неверный формат IP адреса!");
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
            // Настраиваем транспорт для клиента
            transport.ConnectionData.Address = hostIp;
            transport.ConnectionData.Port = port;
            
            UpdateStatusText($"Подключение к {hostIp}:{port}...");
            
            // Запускаем клиент
            bool success = networkManager.StartClient();
            if (success)
            {
                UpdateStatusText("Подключение...");
            }
            else
            {
                UpdateStatusText("Ошибка подключения!");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatusText($"Ошибка подключения: {e.Message}");
        }
        
        UpdateUI();
    }
    
    public void Disconnect()
    {
        if (networkManager == null) return;
        
        if (networkManager.IsHost || networkManager.IsClient)
        {
            networkManager.Shutdown();
            UpdateStatusText("Отключено");
        }
        
        UpdateUI();
    }
    
    void OnServerStarted()
    {
        UpdateStatusText("Сервер запущен и готов к подключениям");
        UpdateUI();
    }
    
    void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            int playerCount = networkManager.ConnectedClients.Count;
            UpdateStatusText($"Игрок подключен! Игроков: {playerCount}/{maxPlayers}");
        }
        else
        {
            UpdateStatusText("Подключен к серверу!");
        }
        
        wasConnected = true;
        UpdateUI();
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            int playerCount = networkManager.ConnectedClients.Count;
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
        if (networkManager.IsHost || networkManager.IsClient)
        {
            networkManager.Shutdown();
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
        
        bool isConnected = networkManager.IsHost || networkManager.IsClient;
        
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
    
    // Получение VPN IP адреса
    string GetVpnIpAddress()
    {
        if (!autoDetectVpnIp) return "";
        
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (NetworkInterface ni in interfaces)
            {
                // Проверяем, активен ли интерфейс
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                
                // Проверяем, является ли это VPN адаптером
                bool isVpnAdapter = false;
                foreach (string vpnName in vpnAdapterNames)
                {
                    if (ni.Description.Contains(vpnName) || ni.Name.Contains(vpnName))
                    {
                        isVpnAdapter = true;
                        break;
                    }
                }
                
                // Также проверяем по IP диапазонам VPN
                // Hamachi: 25.0.0.0/8 или 5.0.0.0/8
                // Radmin VPN: обычно 26.0.0.0/8
                IPInterfaceProperties ipProps = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipString = ip.Address.ToString();
                        byte[] bytes = ip.Address.GetAddressBytes();
                        
                        // Проверяем диапазоны VPN
                        bool isVpnRange = bytes[0] == 25 || bytes[0] == 5 || bytes[0] == 26;
                        
                        if (isVpnAdapter || isVpnRange)
                        {
                            // Исключаем loopback
                            if (!IPAddress.IsLoopback(ip.Address))
                            {
                                return ipString;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MultiplayerManager] Ошибка определения VPN IP: {e.Message}");
        }
        
        return "";
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
        return networkManager != null && (networkManager.IsHost || networkManager.IsClient);
    }
    
    public bool IsHost()
    {
        return networkManager != null && networkManager.IsHost;
    }
    
    public bool IsClient()
    {
        return networkManager != null && networkManager.IsClient;
    }
    
    public string GetLocalIp()
    {
        return localIpAddress;
    }
    
    public int GetPlayerCount()
    {
        if (networkManager == null) return 0;
        return networkManager.ConnectedClients.Count;
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }
}
