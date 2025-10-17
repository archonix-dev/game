using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private InputField ipInputField;
    [SerializeField] private Text statusText;
    
    [Header("Network Settings")]
    [SerializeField] private string defaultIP = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7778;
    
    private NetworkManager networkManager;
    private UnityTransport transport;
    
    void Start()
    {
        // Получаем компоненты
        networkManager = NetworkManager.Singleton;
        transport = networkManager.GetComponent<UnityTransport>();
        
        // Настраиваем кнопки
        if (hostButton != null)
            hostButton.onClick.AddListener(StartHost);
            
        if (clientButton != null)
            clientButton.onClick.AddListener(StartClient);
            
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(Disconnect);
            
        // Настраиваем поле ввода IP
        if (ipInputField != null)
        {
            ipInputField.text = defaultIP;
            ipInputField.onValueChanged.AddListener(OnIPChanged);
        }
        
        // Настраиваем NetworkManager
        SetupNetworkManager();
        
        // Обновляем UI
        UpdateUI();
    }
    
    void SetupNetworkManager()
    {
        if (transport != null)
        {
            transport.ConnectionData.Address = defaultIP;
            transport.ConnectionData.Port = defaultPort;
        }
        
        // Подписываемся на события NetworkManager
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
    }
    
    public void StartHost()
    {
        if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
        {
            Debug.LogWarning("Already connected to network!");
            return;
        }
        
        Debug.Log("Starting Host...");
        UpdateStatusText("Starting Host...");
        
        bool success = networkManager.StartHost();
        if (!success)
        {
            Debug.LogError("Failed to start Host!");
            UpdateStatusText("Failed to start Host!");
        }
    }
    
    public void StartClient()
    {
        if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
        {
            Debug.LogWarning("Already connected to network!");
            return;
        }
        
        Debug.Log("Starting Client...");
        UpdateStatusText("Connecting to Host...");
        
        bool success = networkManager.StartClient();
        if (!success)
        {
            Debug.LogError("Failed to start Client!");
            UpdateStatusText("Failed to connect to Host!");
        }
    }
    
    public void Disconnect()
    {
        if (networkManager.IsHost)
        {
            networkManager.Shutdown();
            Debug.Log("Host disconnected");
        }
        else if (networkManager.IsClient)
        {
            networkManager.Shutdown();
            Debug.Log("Client disconnected");
        }
        
        UpdateStatusText("Disconnected");
        UpdateUI();
    }
    
    void OnIPChanged(string newIP)
    {
        if (transport != null && !string.IsNullOrEmpty(newIP))
        {
            transport.ConnectionData.Address = newIP;
            Debug.Log($"IP changed to: {newIP}");
        }
    }
    
    void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            Debug.Log($"Client {clientId} connected to Host");
            UpdateStatusText($"Host - Client {clientId} connected");
        }
        else if (networkManager.IsClient)
        {
            Debug.Log("Connected to Host as Client");
            UpdateStatusText("Connected to Host");
        }
        
        UpdateUI();
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            Debug.Log($"Client {clientId} disconnected from Host");
            UpdateStatusText($"Host - Client {clientId} disconnected");
        }
        else if (networkManager.IsClient)
        {
            Debug.Log("Disconnected from Host");
            UpdateStatusText("Disconnected from Host");
        }
        
        UpdateUI();
    }
    
    void OnServerStarted()
    {
        Debug.Log("Server started");
        UpdateStatusText("Host started - Waiting for clients...");
        UpdateUI();
    }
    
    void UpdateUI()
    {
        bool isConnected = networkManager.IsHost || networkManager.IsClient;
        
        if (hostButton != null)
            hostButton.interactable = !isConnected;
            
        if (clientButton != null)
            clientButton.interactable = !isConnected;
            
        if (disconnectButton != null)
            disconnectButton.interactable = isConnected;
            
        if (ipInputField != null)
            ipInputField.interactable = !isConnected;
    }
    
    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
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
