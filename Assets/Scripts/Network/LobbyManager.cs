using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Главный менеджер лобби. Управляет созданием лобби, подключением и отображением игроков.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Кнопки")]
    [Tooltip("Кнопка 'Играть' - создает лобби")]
    public Button playButton;
    
    [Tooltip("Кнопка 'Настройки лобби' - открывает панель настроек (только для админа)")]
    public Button lobbySettingsButton;
    
    [Tooltip("Кнопка 'Начать игру' - загружает сцену игры (только для админа)")]
    public Button startGameButton;
    
    [Tooltip("Кнопка 'Подключиться к другому лобби'")]
    public Button connectToLobbyButton;
    
    [Tooltip("Кнопка для открытия панели выбора цвета")]
    public Button colorSelectionButton;

    [Header("UI Панели")]
    [Tooltip("Панель настроек лобби")]
    public GameObject lobbySettingsPanel;
    
    [Tooltip("Панель подключения к другому лобби")]
    public GameObject connectToLobbyPanel;
    
    [Tooltip("Панель выбора цвета")]
    public GameObject colorSelectionPanel;

    [Header("Отображение игроков")]
    [Tooltip("Transform контейнер для списка игроков в лобби")]
    public Transform playersListContainer;
    
    [Tooltip("Префаб игрока в лобби")]
    public GameObject playerLobbyPrefab;

    [Header("Настройки сети")]
    [Tooltip("Порт по умолчанию")]
    public ushort defaultPort = 7777;
    
    [Tooltip("Максимальное количество игроков")]
    public int maxPlayers = 8;

    private NetworkManager networkManager;
    private UnityTransport transport;
    private Dictionary<ulong, GameObject> playerLobbyItems = new Dictionary<ulong, GameObject>();
    private LobbyNetworkManager lobbyNetworkManager;
    private string pendingPassword = "";

    void Start()
    {
        SetupButtons();
        
        // Находим LobbyNetworkManager если он есть
        lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();

        // Скрываем панели по умолчанию
        if (lobbySettingsPanel != null)
            lobbySettingsPanel.SetActive(false);
        
        if (connectToLobbyPanel != null)
            connectToLobbyPanel.SetActive(false);
        
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);

        // Пытаемся инициализировать NetworkManager
        InitializeNetworkManager();
        
        UpdateUI();
    }

    void InitializeNetworkManager()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            // NetworkManager может быть не создан в сцене - это нормально
            // Попробуем найти его позже
            Debug.LogWarning("NetworkManager не найден. Инициализация будет выполнена позже.");
            StartCoroutine(TryInitializeNetworkManager());
            return;
        }

        transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogWarning("UnityTransport не найден на NetworkManager!");
            return;
        }

        SetupNetworkCallbacks();
    }

    System.Collections.IEnumerator TryInitializeNetworkManager()
    {
        // Ждем несколько кадров и пытаемся найти NetworkManager снова
        yield return new WaitForSeconds(0.1f);
        
        int attempts = 0;
        while (networkManager == null && attempts < 50)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    SetupNetworkCallbacks();
                    UpdateUI();
                    Debug.Log("NetworkManager найден и инициализирован!");
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (networkManager == null)
        {
            Debug.LogWarning("NetworkManager не найден после нескольких попыток. Убедитесь, что NetworkManager добавлен в сцену.");
        }
    }

    void SetupButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
            Debug.Log("Кнопка 'Играть' подключена.");
        }
        
        if (lobbySettingsButton != null)
        {
            lobbySettingsButton.onClick.AddListener(OnLobbySettingsButtonClicked);
            Debug.Log("Кнопка 'Настройки лобби' подключена.");
        }
        
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            Debug.Log("Кнопка 'Начать игру' подключена.");
        }
        
        if (connectToLobbyButton != null)
        {
            connectToLobbyButton.onClick.AddListener(OnConnectToLobbyButtonClicked);
            Debug.Log("Кнопка 'Подключиться к другому лобби' подключена.");
        }
        else
        {
            Debug.LogWarning("Кнопка 'Подключиться к другому лобби' не назначена в инспекторе!");
        }
        
        if (colorSelectionButton != null)
        {
            colorSelectionButton.onClick.AddListener(OnColorSelectionButtonClicked);
            Debug.Log("Кнопка 'Выбор цвета' подключена.");
        }
        else
        {
            Debug.LogWarning("Кнопка 'Выбор цвета' не назначена в инспекторе!");
        }
    }

    void SetupNetworkCallbacks()
    {
        if (networkManager == null)
            return;

        // Отписываемся от старых событий на случай повторной инициализации
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnServerStarted -= OnServerStarted;

        // Подписываемся на события NetworkManager
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
    }

    void OnPlayButtonClicked()
    {
        // Создаем лобби (запускаем хост)
        CreateLobby();
    }

    /// <summary>
    /// Публичный метод для создания лобби (вызывается из других скриптов)
    /// </summary>
    public void CreateLobby()
    {
        // Проверяем, что NetworkManager доступен
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager не найден! Убедитесь, что NetworkManager добавлен в сцену.");
                return;
            }
        }

        if (networkManager.IsHost || networkManager.IsClient)
        {
            Debug.LogWarning("Уже подключен к лобби!");
            return;
        }

        // Проверяем и инициализируем transport
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            transport.ConnectionData.Port = defaultPort;
            
            bool success = networkManager.StartHost();
            if (success)
            {
                Debug.Log("Лобби создано!");
                // Инициализация лобби произойдет в OnServerStarted
            }
            else
            {
                Debug.LogError("Ошибка создания лобби!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка создания лобби: {e.Message}");
        }

        UpdateUI();
    }

    void OnLobbySettingsButtonClicked()
    {
        Debug.Log("OnLobbySettingsButtonClicked вызван!");
        
        // Открываем панель настроек лобби (только для админа)
        if (IsHost())
        {
            if (lobbySettingsPanel != null)
            {
                // Убеждаемся, что панель и все её родители активны
                ActivatePanelWithParents(lobbySettingsPanel);
                
                Debug.Log("Панель настроек лобби открыта.");
            }
            else
            {
                Debug.LogError("lobbySettingsPanel не назначен в инспекторе LobbyManager!");
            }
        }
    }

    void OnStartGameButtonClicked()
    {
        // Загружаем сцену игры (только для админа)
        if (IsHost())
        {
            SceneManager.LoadScene("Mansion");
        }
    }

    void OnConnectToLobbyButtonClicked()
    {
        Debug.Log("OnConnectToLobbyButtonClicked вызван!");
        
        // Показываем панель подключения к другому лобби
        if (connectToLobbyPanel != null)
        {
            // Убеждаемся, что панель и все её родители активны
            ActivatePanelWithParents(connectToLobbyPanel);
            
            // Дополнительно убеждаемся, что панель активна (на случай если что-то её скрыло)
            if (!connectToLobbyPanel.activeSelf)
            {
                connectToLobbyPanel.SetActive(true);
            }
            
            Debug.Log($"Панель подключения к лобби открыта. Активна: {connectToLobbyPanel.activeSelf}");
        }
        else
        {
            Debug.LogError("connectToLobbyPanel не назначен в инспекторе LobbyManager!");
        }
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
        Debug.Log("OnColorSelectionButtonClicked вызван!");
        
        // Показываем панель выбора цвета
        if (colorSelectionPanel != null)
        {
            // Убеждаемся, что панель и все её родители активны
            ActivatePanelWithParents(colorSelectionPanel);
            
            Debug.Log("Панель выбора цвета открыта.");
        }
        else
        {
            Debug.LogError("colorSelectionPanel не назначен в инспекторе LobbyManager!");
        }
    }

    public void HideColorSelectionPanel()
    {
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(false);
            Debug.Log("Панель выбора цвета скрыта через LobbyManager.");
        }
    }

    void OnServerStarted()
    {
        Debug.Log("Сервер запущен");
        UpdateUI();
        
        // Создаем LobbyNetworkManager как NetworkObject если его нет
        StartCoroutine(CreateLobbyNetworkManager());
        
        // Создаем UI для хоста
        if (networkManager.IsHost)
        {
            CreatePlayerLobbyItem(networkManager.LocalClientId);
        }
    }

    System.Collections.IEnumerator CreateLobbyNetworkManager()
    {
        // Ждем один кадр, чтобы убедиться, что сеть полностью инициализирована
        yield return null;
        
        if (lobbyNetworkManager == null && networkManager != null && networkManager.IsServer)
        {
            GameObject lobbyNetObj = new GameObject("LobbyNetworkManager");
            lobbyNetworkManager = lobbyNetObj.AddComponent<LobbyNetworkManager>();
            NetworkObject networkObject = lobbyNetObj.AddComponent<NetworkObject>();
            
            // Spawn только на сервере
            if (networkManager.IsServer)
            {
                networkObject.Spawn();
                
                // Инициализируем данные лобби
                lobbyNetworkManager.InitializeLobby(maxPlayers, "");
            }
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Клиент подключен: {clientId}");
        
        // Если это мы подключились как клиент, проверяем пароль
        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            // Ждем немного, чтобы LobbyNetworkManager был создан на сервере
            StartCoroutine(CheckPasswordAfterConnection());
        }
        
        UpdateUI();
        
        // Создаем UI для нового игрока
        CreatePlayerLobbyItem(clientId);
    }

    System.Collections.IEnumerator CheckPasswordAfterConnection()
    {
        // Ждем пока LobbyNetworkManager будет доступен
        yield return new WaitForSeconds(0.5f);
        
        if (lobbyNetworkManager == null)
        {
            lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
        }
        
        if (lobbyNetworkManager != null && !string.IsNullOrEmpty(pendingPassword))
        {
            // Проверяем пароль (это нужно делать через ServerRpc в реальной реализации)
            // Здесь упрощенная версия
            Debug.Log($"Попытка подключения с паролем: {pendingPassword}");
            pendingPassword = "";
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Клиент отключен: {clientId}");
        
        // Удаляем UI игрока
        if (playerLobbyItems.ContainsKey(clientId))
        {
            Destroy(playerLobbyItems[clientId]);
            playerLobbyItems.Remove(clientId);
        }
        
        // Переупорядочиваем список игроков
        ReorderPlayersList();
        
        UpdateUI();
    }

    void ReorderPlayersList()
    {
        if (playersListContainer == null || networkManager == null || !IsHost())
            return;

        // Находим админа (хоста) и перемещаем его в начало
        if (playerLobbyItems.ContainsKey(networkManager.LocalClientId))
        {
            GameObject adminItem = playerLobbyItems[networkManager.LocalClientId];
            adminItem.transform.SetAsFirstSibling();
        }
    }

    void CreatePlayerLobbyItem(ulong clientId)
    {
        if (playerLobbyPrefab == null || playersListContainer == null)
        {
            Debug.LogWarning("Префаб игрока или контейнер не назначены!");
            return;
        }

        // Проверяем, не создан ли уже UI для этого игрока
        if (playerLobbyItems.ContainsKey(clientId))
        {
            return;
        }

        GameObject playerItem = Instantiate(playerLobbyPrefab, playersListContainer);
        PlayerLobbyItem playerLobbyItem = playerItem.GetComponent<PlayerLobbyItem>();
        
        if (playerLobbyItem != null)
        {
            bool isAdmin = networkManager != null && IsHost() && clientId == networkManager.LocalClientId;
            playerLobbyItem.Initialize(clientId, isAdmin);
        }

        playerLobbyItems[clientId] = playerItem;
        
        // Если это админ, перемещаем его в начало списка
        ReorderPlayersList();
    }

    void UpdateUI()
    {
        bool isConnected = false;
        bool isHost = false;

        if (networkManager != null)
        {
            isConnected = networkManager.IsHost || networkManager.IsClient;
            isHost = IsHost();
        }

        // Кнопка "Играть" всегда активна
        if (playButton != null)
            playButton.interactable = true;

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

        // Кнопка подключения доступна всегда
        if (connectToLobbyButton != null)
            connectToLobbyButton.interactable = true;
    }

    bool IsHost()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }
        return networkManager != null && networkManager.IsHost;
    }

    public void ConnectToLobby(string ipAddress, string password)
    {
        // Проверяем, что NetworkManager доступен
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager не найден! Убедитесь, что NetworkManager добавлен в сцену.");
                return;
            }
        }

        // Если мы уже подключены к лобби, отключаемся сначала
        if (networkManager.IsHost || networkManager.IsClient)
        {
            Debug.Log("Отключение от текущего лобби перед подключением к новому...");
            DisconnectFromCurrentLobby();
            
            // Ждем немного, чтобы отключение завершилось
            StartCoroutine(ConnectAfterDisconnect(ipAddress, password));
            return;
        }

        // Если не подключены, подключаемся сразу
        ConnectToLobbyInternal(ipAddress, password);
    }

    System.Collections.IEnumerator ConnectAfterDisconnect(string ipAddress, string password)
    {
        // Ждем, пока отключение завершится
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что мы действительно отключены
        int attempts = 0;
        while ((networkManager.IsHost || networkManager.IsClient) && attempts < 10)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (networkManager.IsHost || networkManager.IsClient)
        {
            Debug.LogWarning("Не удалось отключиться от текущего лобби!");
            yield break;
        }
        
        // Подключаемся к новому лобби
        ConnectToLobbyInternal(ipAddress, password);
    }

    void ConnectToLobbyInternal(string ipAddress, string password)
    {
        // Проверяем и инициализируем transport
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            transport.ConnectionData.Address = ipAddress;
            transport.ConnectionData.Port = defaultPort;
            
            // Сохраняем пароль для проверки после подключения
            pendingPassword = password;
            
            bool success = networkManager.StartClient();
            if (success)
            {
                Debug.Log($"Подключение к {ipAddress}...");
            }
            else
            {
                Debug.LogError("Ошибка подключения к лобби!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка подключения: {e.Message}");
        }

        UpdateUI();
    }

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
            return;

        bool wasHost = networkManager.IsHost;
        bool wasClient = networkManager.IsClient;

        // Отключаемся от сети
        if (wasHost)
        {
            Debug.Log("Останавливаем хост...");
            networkManager.Shutdown();
        }
        else if (wasClient)
        {
            Debug.Log("Отключаемся от сервера...");
            networkManager.Shutdown();
        }

        // Очищаем список игроков в UI после отключения
        // Это будет сделано также в OnClientDisconnected, но очистим сразу для надежности
        ClearPlayersList();

        // Очищаем ссылки
        lobbyNetworkManager = null;

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

    public string GetLocalIpAddress()
    {
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                
                // Проверяем VPN адаптеры (Hamachi, Radmin VPN)
                bool isVpnAdapter = false;
                string[] vpnNames = { "Hamachi", "Radmin VPN", "TAP", "VirtualBox" };
                foreach (string vpnName in vpnNames)
                {
                    if (ni.Description.Contains(vpnName) || ni.Name.Contains(vpnName))
                    {
                        isVpnAdapter = true;
                        break;
                    }
                }
                
                IPInterfaceProperties ipProps = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipString = ip.Address.ToString();
                        byte[] bytes = ip.Address.GetAddressBytes();
                        
                        bool isVpnRange = bytes[0] == 25 || bytes[0] == 5 || bytes[0] == 26;
                        
                        if (isVpnAdapter || isVpnRange)
                        {
                            if (!IPAddress.IsLoopback(ip.Address))
                            {
                                return ipString;
                            }
                        }
                    }
                }
            }
            
            // Если VPN не найден, возвращаем обычный локальный IP
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
            Debug.LogWarning($"Ошибка получения IP адреса: {e.Message}");
        }
        
        return "Не определен";
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }
}

