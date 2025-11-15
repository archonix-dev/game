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
    
    [Header("Загрузка сцены")]
    [Tooltip("Компонент AsyncSceneLoaderWithAnimation для асинхронной загрузки сцены с анимацией")]
    public AsyncSceneLoaderWithAnimation sceneLoader;
    
    [Tooltip("Имя сцены для загрузки при нажатии 'Начать игру'")]
    public string gameSceneName = "Lobby";

    private NetworkManager networkManager;
    private UnityTransport transport;
    private Dictionary<ulong, GameObject> playerLobbyItems = new Dictionary<ulong, GameObject>();
    private LobbyNetworkManager lobbyNetworkManager;
    private string pendingPassword = "";

    void Start()
    {
        // КРИТИЧЕСКИ ВАЖНО: Убеждаемся, что LobbyManager всегда активен
        // Это необходимо для обработки сетевых событий
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("[LobbyManager] LobbyManager был неактивен при Start! Активируем...");
            gameObject.SetActive(true);
        }
        
        SetupButtons();
        
        // Находим LobbyNetworkManager если он есть
        lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
        
        // Находим AsyncSceneLoaderWithAnimation если он не назначен
        if (sceneLoader == null)
        {
            sceneLoader = FindObjectOfType<AsyncSceneLoaderWithAnimation>();
        }

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
        
        Debug.Log("[LobbyManager] LobbyManager инициализирован и активен");
    }
    
    void OnEnable()
    {
        // Убеждаемся, что подписки на события активны при включении объекта
        if (networkManager != null)
        {
            SetupNetworkCallbacks();
            Debug.Log("[LobbyManager] LobbyManager включен, подписки на события обновлены");
        }
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
        
        // Отписываемся от событий transport, если они были подписаны
        if (transport != null)
        {
            try
            {
                // UnityTransport может иметь события ошибок, но они обычно обрабатываются автоматически
            }
            catch { }
        }

        // Подписываемся на события NetworkManager
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
        
        Debug.Log("[LobbyManager] Подписки на сетевые события установлены");
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
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект LobbyManager неактивен! Невозможно создать лобби.");
            return;
        }

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

        // Если уже подключены, отключаемся сначала
        if (networkManager.IsHost || networkManager.IsClient)
        {
            Debug.Log("[LobbyManager] Уже подключен к лобби. Отключаемся перед созданием нового...");
            DisconnectFromCurrentLobby();
            
            // Ждем, пока отключение завершится
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CreateLobbyAfterDisconnect());
            }
            else
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным после отключения! Невозможно запустить корутину создания лобби.");
            }
            return;
        }

        // Проверяем и инициализируем transport
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[LobbyManager] UnityTransport не найден на NetworkManager!");
                return;
            }
        }

        // Создаем лобби
        CreateLobbyInternal();
    }
    
    System.Collections.IEnumerator CreateLobbyAfterDisconnect()
    {
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен в корутине CreateLobbyAfterDisconnect!");
            yield break;
        }

        // Ждем, пока отключение завершится
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что мы действительно отключены
        int attempts = 0;
        while (networkManager != null && (networkManager.IsHost || networkManager.IsClient) && attempts < 20)
        {
            // Проверяем, что объект все еще активен
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным во время ожидания отключения!");
                yield break;
            }
            
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        // Проверяем, что объект все еще активен перед созданием лобби
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен перед созданием лобби!");
            yield break;
        }
        
        if (networkManager != null && (networkManager.IsHost || networkManager.IsClient))
        {
            Debug.LogWarning("[LobbyManager] Не удалось отключиться от текущего лобби!");
            yield break;
        }
        
        // Ждем еще немного, чтобы порт освободился
        yield return new WaitForSeconds(0.5f);
        
        // Создаем новое лобби
        CreateLobbyInternal();
    }
    
    void CreateLobbyInternal()
    {
        // Проверяем и инициализируем transport
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[LobbyManager] UnityTransport не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            transport.ConnectionData.Port = defaultPort;
            
            // Получаем локальный IP адрес для отображения
            string localIp = GetLocalIpAddress();
            
            Debug.Log($"[LobbyManager] Создание сервера на порту {defaultPort}...");
            Debug.Log($"[LobbyManager] IP адрес сервера: {localIp}");
            bool success = networkManager.StartHost();
            if (success)
            {
                Debug.Log($"[LobbyManager] ✓ Сервер создан! IP: {localIp}, Порт: {defaultPort}, Сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                // Инициализация лобби произойдет в OnServerStarted
            }
            else
            {
                Debug.LogError($"[LobbyManager] ✗ Ошибка создания сервера! IP: {localIp}, Порт: {defaultPort}. Возможно, порт занят другим процессом.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] ✗ Ошибка создания лобби: {e.Message}\n{e.StackTrace}");
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
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("[LobbyManager] Имя сцены игры не указано!");
                return;
            }
            
            // Загружаем сцену через NetworkManager для синхронизации со всеми клиентами
            if (networkManager != null && networkManager.SceneManager != null)
            {
                Debug.Log($"[LobbyManager] Загрузка игровой сцены {gameSceneName} для всех игроков...");
                networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[LobbyManager] NetworkManager или SceneManager не найден! Невозможно загрузить сцену для всех игроков.");
            }
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
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string localIp = GetLocalIpAddress();
        Debug.Log($"[LobbyManager] ✓ Сервер запущен! IP: {localIp}, Порт: {defaultPort}, Сцена: {currentScene}, LocalClientId: {networkManager.LocalClientId}");
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
        Debug.Log($"[LobbyManager] ===== OnClientConnected ВЫЗВАН для clientId={clientId} =====");
        
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[LobbyManager] Объект неактивен при подключении клиента {clientId}! Пропускаем обработку.");
            Debug.LogWarning($"[LobbyManager] Активируем объект...");
            gameObject.SetActive(true);
            // Продолжаем обработку после активации
        }

        if (networkManager == null)
        {
            Debug.LogError($"[LobbyManager] NetworkManager null при подключении клиента {clientId}!");
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError($"[LobbyManager] NetworkManager.Singleton тоже null!");
                return;
            }
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isLocalClient = clientId == networkManager.LocalClientId;
        string role = networkManager.IsHost ? "Хост" : (isLocalClient ? "Клиент" : "Другой клиент");
        
        Debug.Log($"[LobbyManager] ✓ Клиент подключен: ID={clientId}, Роль={role}, Сцена={currentScene}, Всего игроков={networkManager.ConnectedClients.Count}");
        Debug.Log($"[LobbyManager] Детали: IsHost={networkManager.IsHost}, IsClient={networkManager.IsClient}, IsServer={networkManager.IsServer}, LocalClientId={networkManager.LocalClientId}");
        
        // Если это мы подключились как клиент, проверяем пароль
        if (networkManager.IsClient && isLocalClient)
        {
            Debug.Log($"[LobbyManager] Проверка пароля для клиента {clientId}...");
            // Ждем немного, чтобы LobbyNetworkManager был создан на сервере
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(CheckPasswordAfterConnection());
            }
            else
            {
                Debug.LogError("[LobbyManager] Объект неактивен! Невозможно запустить корутину проверки пароля.");
            }
        }
        
        UpdateUI();
        
        // ВАЖНО: Создаем UI для игроков только на сервере
        // NetworkObject автоматически синхронизируется со всеми клиентами
        if (networkManager.IsServer)
        {
            // Создаем UI для всех подключенных клиентов (включая хоста)
            CreatePlayerLobbyItem(clientId);
            
            // Также создаем UI для всех остальных уже подключенных игроков
            // Это нужно, если хост подключается после других клиентов
            foreach (var connectedClient in networkManager.ConnectedClients)
            {
                if (connectedClient.Key != clientId && !playerLobbyItems.ContainsKey(connectedClient.Key))
                {
                    CreatePlayerLobbyItem(connectedClient.Key);
                }
            }
        }
        else
        {
            // Клиенты не создают PlayerLobbyItem напрямую
            // Они получают их через ClientRpc от сервера
            Debug.Log($"[LobbyManager] Клиент: ожидание ClientRpc для игрока {clientId} от сервера...");
            
            // Запрашиваем список всех игроков у сервера (если подключились позже)
            if (lobbyNetworkManager != null && lobbyNetworkManager.IsSpawned)
            {
                // Запрашиваем список всех игроков через LobbyNetworkManager
                lobbyNetworkManager.RequestAllPlayersServerRpc();
            }
            else
            {
                // Если LobbyNetworkManager еще не создан, ждем и запрашиваем позже
                StartCoroutine(RequestAllPlayersDelayed());
            }
        }
    }

    System.Collections.IEnumerator CheckPasswordAfterConnection()
    {
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен в корутине CheckPasswordAfterConnection!");
            yield break;
        }

        // Ждем пока LobbyNetworkManager будет доступен
        yield return new WaitForSeconds(0.5f);
        
        int attempts = 0;
        while (lobbyNetworkManager == null && attempts < 15)
        {
            // Проверяем, что объект все еще активен
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным во время поиска LobbyNetworkManager!");
                yield break;
            }

            // Проверяем, что мы все еще подключены
            if (networkManager == null || (!networkManager.IsClient && !networkManager.IsHost))
            {
                Debug.LogWarning("[LobbyManager] Отключились во время ожидания LobbyNetworkManager!");
                pendingPassword = "";
                yield break;
            }

            lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
            if (lobbyNetworkManager == null)
            {
                yield return new WaitForSeconds(0.2f);
                attempts++;
            }
        }
        
        // Проверяем, что объект все еще активен перед проверкой пароля
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен перед проверкой пароля!");
            pendingPassword = "";
            yield break;
        }
        
        // Проверяем, что мы все еще подключены
        if (networkManager == null || (!networkManager.IsClient && !networkManager.IsHost))
        {
            Debug.LogWarning("[LobbyManager] Отключились перед проверкой пароля!");
            pendingPassword = "";
            yield break;
        }
        
        if (lobbyNetworkManager != null && !string.IsNullOrEmpty(pendingPassword))
        {
            // Проверяем пароль через ServerRpc
            string passwordToCheck = pendingPassword;
            pendingPassword = ""; // Очищаем сразу, чтобы не проверять повторно
            Debug.Log($"[LobbyManager] Проверка пароля для подключения...");
            lobbyNetworkManager.CheckPasswordServerRpc(
                new Unity.Collections.FixedString32Bytes(passwordToCheck)
            );
        }
        else if (lobbyNetworkManager == null)
        {
            Debug.LogWarning("[LobbyManager] LobbyNetworkManager не найден! Пароль не может быть проверен.");
            pendingPassword = "";
        }
        else if (string.IsNullOrEmpty(pendingPassword))
        {
            Debug.LogWarning("[LobbyManager] Пароль не был сохранен для проверки.");
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"[LobbyManager] ✗ Клиент отключен: ID={clientId}, Сцена={currentScene}, Осталось игроков={networkManager.ConnectedClients.Count}");
        
        // Удаляем UI игрока
        if (playerLobbyItems.ContainsKey(clientId))
        {
            GameObject playerItem = playerLobbyItems[clientId];
            NetworkObject networkObject = playerItem != null ? playerItem.GetComponent<NetworkObject>() : null;
            
            // Если это NetworkObject, деспавним его правильно
            if (networkObject != null && networkObject.IsSpawned)
            {
                // На сервере деспавним NetworkObject (это автоматически удалит его у всех клиентов)
                if (networkManager != null && networkManager.IsServer)
                {
                    networkObject.Despawn();
                }
            }
            else
            {
                // Если это не NetworkObject или уже не заспавнен, просто уничтожаем
                if (playerItem != null)
                {
                    Destroy(playerItem);
                }
            }
            
            playerLobbyItems.Remove(clientId);
        }
        
        // Переупорядочиваем список игроков
        ReorderPlayersList();
        
        UpdateUI();
    }


    /// <summary>
    /// Регистрирует PlayerLobbyItem в словаре (вызывается из PlayerLobbyItem при синхронизации)
    /// </summary>
    public void RegisterPlayerLobbyItem(ulong clientId, GameObject playerItem)
    {
        if (playerItem == null)
            return;

        // Добавляем в словарь, если еще не добавлен
        if (!playerLobbyItems.ContainsKey(clientId))
        {
            playerLobbyItems[clientId] = playerItem;
            Debug.Log($"[LobbyManager] PlayerLobbyItem для игрока {clientId} зарегистрирован (синхронизирован с клиента)");
            
            // Переупорядочиваем список, если это хост
            if (IsHost())
            {
                ReorderPlayersList();
            }
        }
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
            Debug.LogWarning($"[LobbyManager] Префаб игрока или контейнер не назначены! clientId={clientId}");
            return;
        }

        // Проверяем, не создан ли уже UI для этого игрока
        if (playerLobbyItems.ContainsKey(clientId))
        {
            Debug.Log($"[LobbyManager] UI для игрока {clientId} уже существует, пропускаем создание");
            return;
        }

        // Убеждаемся, что контейнер активен
        if (!playersListContainer.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[LobbyManager] Контейнер списка игроков неактивен! Активируем...");
            playersListContainer.gameObject.SetActive(true);
        }

        // Создаем PlayerLobbyItem локально на всех клиентах через ClientRpc
        if (networkManager != null && networkManager.IsServer)
        {
            // Админ - это хост (сервер)
            // Хост всегда имеет clientId = 0 или clientId == LocalClientId
            bool isAdmin = (clientId == 0) || (networkManager.IsHost && clientId == networkManager.LocalClientId);
            
            Debug.Log($"[LobbyManager] Создание PlayerLobbyItem для clientId={clientId}, isAdmin={isAdmin}, IsHost={networkManager.IsHost}, LocalClientId={networkManager.LocalClientId}");
            
            // Генерируем случайное имя для игрока
            string playerName = GenerateRandomPlayerName();
            
            // Получаем цвет игрока (если есть сохраненный)
            Color playerColor = Color.white;
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
            
            Debug.Log($"[LobbyManager] Отправка данных игрока {clientId} всем клиентам через ClientRpc");
            
            // Создаем локально на сервере
            CreatePlayerLobbyItemLocally(clientId, isAdmin, playerName, playerColor);
            
            // Отправляем данные всем клиентам через LobbyNetworkManager
            if (lobbyNetworkManager != null)
            {
                lobbyNetworkManager.BroadcastPlayerLobbyItem(clientId, isAdmin, playerName, playerColor);
            }
            else
            {
                Debug.LogWarning("[LobbyManager] LobbyNetworkManager не найден! Данные не будут отправлены другим клиентам.");
            }
        }
        else
        {
            // Клиенты не создают PlayerLobbyItem напрямую - они получают их через ClientRpc
            Debug.Log($"[LobbyManager] Клиент: ожидание ClientRpc для игрока {clientId} от сервера...");
        }
    }
    
    /// <summary>
    /// Создает PlayerLobbyItem локально на клиенте (вызывается через ClientRpc)
    /// </summary>
    public void CreatePlayerLobbyItemLocally(ulong clientId, bool isAdmin, string playerName, Color playerColor)
    {
        if (playerLobbyPrefab == null || playersListContainer == null)
        {
            Debug.LogWarning($"[LobbyManager] Префаб игрока или контейнер не назначены! clientId={clientId}");
            return;
        }

        // Проверяем, не создан ли уже UI для этого игрока
        if (playerLobbyItems.ContainsKey(clientId))
        {
            Debug.Log($"[LobbyManager] UI для игрока {clientId} уже существует, обновляем данные");
            // Обновляем существующий элемент
            PlayerLobbyItem existingItem = playerLobbyItems[clientId].GetComponent<PlayerLobbyItem>();
            if (existingItem != null)
            {
                existingItem.Initialize(clientId, isAdmin, playerName, playerColor);
            }
            return;
        }

        // Убеждаемся, что контейнер активен
        if (!playersListContainer.gameObject.activeInHierarchy)
        {
            playersListContainer.gameObject.SetActive(true);
        }

        // Создаем экземпляр префаба (локальный объект, не NetworkObject)
        GameObject playerItem = Instantiate(playerLobbyPrefab, playersListContainer);
        PlayerLobbyItem playerLobbyItem = playerItem.GetComponent<PlayerLobbyItem>();
        
        if (playerLobbyItem == null)
        {
            Debug.LogError($"[LobbyManager] PlayerLobbyItem компонент не найден на префабе для игрока {clientId}!");
            Destroy(playerItem);
            return;
        }

        // Инициализируем данные игрока (локально на каждом клиенте)
        playerLobbyItem.Initialize(clientId, isAdmin, playerName, playerColor);
        
        playerLobbyItems[clientId] = playerItem;
        
        Debug.Log($"[LobbyManager] ✓ PlayerLobbyItem для игрока {clientId} создан локально. isAdmin={isAdmin}, playerName={playerName}. Всего игроков в UI: {playerLobbyItems.Count}");
        
        // Если это админ, перемещаем его в начало списка
        ReorderPlayersList();
    }
    
    /// <summary>
    /// Обновляет цвет игрока локально (вызывается через ClientRpc)
    /// </summary>
    public void UpdatePlayerColorLocally(ulong clientId, Color playerColor)
    {
        if (playerLobbyItems.ContainsKey(clientId))
        {
            PlayerLobbyItem playerItem = playerLobbyItems[clientId].GetComponent<PlayerLobbyItem>();
            if (playerItem != null)
            {
                playerItem.SetPlayerColor(playerColor);
                Debug.Log($"[LobbyManager] Цвет игрока {clientId} обновлен локально: {playerColor}");
            }
        }
    }
    
    /// <summary>
    /// Корутина для запроса списка всех игроков с задержкой
    /// </summary>
    System.Collections.IEnumerator RequestAllPlayersDelayed()
    {
        // Ждем, пока LobbyNetworkManager будет создан
        int attempts = 0;
        while (lobbyNetworkManager == null && attempts < 20)
        {
            lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
            if (lobbyNetworkManager == null)
            {
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }
        }
        
        if (lobbyNetworkManager != null && lobbyNetworkManager.IsSpawned)
        {
            // Запрашиваем список всех игроков через LobbyNetworkManager
            lobbyNetworkManager.RequestAllPlayersServerRpc();
        }
    }
    
    /// <summary>
    /// Получает список всех игроков для синхронизации (публичный метод для LobbyNetworkManager)
    /// </summary>
    public Dictionary<ulong, (bool isAdmin, string playerName, Color playerColor)> GetAllPlayersData()
    {
        Dictionary<ulong, (bool isAdmin, string playerName, Color playerColor)> playersData = new Dictionary<ulong, (bool, string, Color)>();
        
        foreach (var playerItem in playerLobbyItems)
        {
            ulong playerClientId = playerItem.Key;
            PlayerLobbyItem item = playerItem.Value?.GetComponent<PlayerLobbyItem>();
            
            if (item != null)
            {
                bool isAdmin = item.IsAdmin();
                playersData[playerClientId] = (isAdmin, item.PlayerName, item.PlayerColor);
            }
        }
        
        return playersData;
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
        // Проверяем, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект LobbyManager неактивен! Невозможно подключиться к лобби.");
            return;
        }

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
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ConnectAfterDisconnect(ipAddress, password));
            }
            else
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным после отключения! Невозможно запустить корутину подключения.");
            }
            return;
        }

        // Если не подключены, подключаемся сразу
        ConnectToLobbyInternal(ipAddress, password);
    }

    System.Collections.IEnumerator ConnectAfterDisconnect(string ipAddress, string password)
    {
        // Проверяем, что объект все еще активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyManager] Объект неактивен в корутине ConnectAfterDisconnect!");
            yield break;
        }

        Debug.Log("[LobbyManager] Ожидание закрытия сокета перед переподключением...");
        
        // Ждем больше времени, чтобы сокет успел правильно закрыться
        // Увеличено до 2 секунд для надежного закрытия сокета
        yield return new WaitForSeconds(2.0f);
        
        // Проверяем, что мы действительно отключены
        int attempts = 0;
        while (networkManager != null && (networkManager.IsHost || networkManager.IsClient) && attempts < 40)
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
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager не найден после отключения!");
                yield break;
            }
        }
        
        // Проверяем transport
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
        }
        
        if (networkManager.IsHost || networkManager.IsClient)
        {
            Debug.LogWarning("[LobbyManager] Не удалось отключиться от текущего лобби! Попытка принудительного отключения...");
            try
            {
                networkManager.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                yield break;
            }
            
            // Ждем после отключения (yield вне блока try-catch)
            yield return new WaitForSeconds(2.0f);
        }
        
        // Увеличенная задержка для полного закрытия сокета и очистки transport
        // Вместо пересоздания transport, используем длительное ожидание для полной очистки
        Debug.Log("[LobbyManager] Ожидание полного закрытия сокета и очистки ресурсов...");
        yield return new WaitForSeconds(3.0f);
        
        // Принудительная сборка мусора для очистки старых ресурсов
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        
        yield return new WaitForSeconds(1.0f);
        
        // Убеждаемся, что transport все еще существует и готов
        if (networkManager != null)
        {
            if (transport == null)
            {
                transport = networkManager.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    Debug.LogError("[LobbyManager] UnityTransport не найден после ожидания!");
                    yield break;
                }
            }
            
            // Убеждаемся, что transport включен
            if (!transport.enabled)
            {
                transport.enabled = true;
                yield return new WaitForSeconds(0.2f);
            }
            
            // Убеждаемся, что NetworkManager использует правильный transport
            if (networkManager.NetworkConfig.NetworkTransport != transport)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager не использует transport! Обновляем...");
                networkManager.NetworkConfig.NetworkTransport = transport;
            }
            
            Debug.Log("[LobbyManager] Transport готов к новому подключению");
        }
        
        // Финальная проверка состояния NetworkManager перед подключением
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager не найден перед подключением!");
                yield break;
            }
        }
        
        // Убеждаемся, что NetworkManager полностью отключен
        if (networkManager.IsClient || networkManager.IsHost)
        {
            Debug.LogWarning("[LobbyManager] NetworkManager все еще подключен! Ожидание дополнительного времени...");
            yield return new WaitForSeconds(1.0f);
            
            // Если все еще подключен, принудительно отключаем
            if (networkManager.IsClient || networkManager.IsHost)
            {
                try
                {
                    networkManager.Shutdown();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                }
                
                yield return new WaitForSeconds(1.0f);
            }
        }
        
        // Финальная задержка перед подключением для полной очистки всех ресурсов
        Debug.Log("[LobbyManager] Финальная задержка перед подключением...");
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[LobbyManager] Сокет должен быть закрыт. Начинаем подключение к новому лобби...");
        
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
                Debug.LogError("[LobbyManager] ✗ UnityTransport не найден на NetworkManager!");
                return;
            }
        }

        try
        {
            // Проверяем валидность IP адреса
            System.Net.IPAddress ip;
            if (!System.Net.IPAddress.TryParse(ipAddress, out ip))
            {
                Debug.LogError($"[LobbyManager] ✗ Неверный формат IP адреса: {ipAddress}");
                return;
            }
            
            // Убеждаемся, что NetworkManager полностью отключен перед новым подключением
            if (networkManager.IsClient || networkManager.IsHost)
            {
                Debug.LogWarning("[LobbyManager] Уже подключен! Попытка принудительного отключения...");
                try
                {
                    networkManager.Shutdown();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyManager] Ошибка при принудительном отключении: {e.Message}");
                }
                
                // Если мы все еще подключены, запускаем корутину для ожидания отключения
                if (networkManager.IsClient || networkManager.IsHost)
                {
                    Debug.LogWarning("[LobbyManager] Ожидание завершения отключения...");
                    StartCoroutine(WaitForDisconnectAndConnect(ipAddress, password));
                    return;
                }
            }
            
            // Настраиваем transport перед подключением
            transport.ConnectionData.Address = ipAddress;
            transport.ConnectionData.Port = defaultPort;
            
            // Сохраняем пароль для проверки после подключения
            pendingPassword = password;
            
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"[LobbyManager] Подключение к серверу: IP={ipAddress}, Порт={defaultPort}, Сцена={currentScene}");
            
            // Запускаем подключение через корутину, чтобы дать время transport'у инициализироваться
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(StartClientConnection(ipAddress));
            }
            else
            {
                Debug.LogError("[LobbyManager] Объект неактивен! Невозможно запустить подключение.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] ✗ Ошибка подключения: {e.Message}\n{e.StackTrace}");
            // Очищаем pendingPassword при ошибке
            pendingPassword = "";
        }

        UpdateUI();
    }
    
    System.Collections.IEnumerator StartClientConnection(string ipAddress)
    {
        // Увеличенная задержка перед подключением, чтобы убедиться, что transport полностью готов
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что объект все еще активен
        if (!gameObject.activeInHierarchy || networkManager == null)
        {
            Debug.LogError("[LobbyManager] Объект стал неактивным перед StartClient!");
            pendingPassword = ""; // Очищаем пароль при ошибке
            yield break;
        }
        
        // Проверяем еще раз, что мы не подключены
        if (networkManager.IsClient || networkManager.IsHost)
        {
            Debug.LogWarning("[LobbyManager] Уже подключен перед StartClient!");
            pendingPassword = ""; // Очищаем пароль
            yield break;
        }
        
        // Проверяем transport еще раз
        if (transport == null)
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[LobbyManager] UnityTransport не найден перед StartClient!");
                pendingPassword = ""; // Очищаем пароль
                yield break;
            }
        }
        
        // Убеждаемся, что transport включен
        if (!transport.enabled)
        {
            Debug.LogWarning("[LobbyManager] Transport отключен! Включаем...");
            transport.enabled = true;
            yield return new WaitForSeconds(0.2f);
        }
        
        // Убеждаемся, что NetworkManager использует правильный transport
        if (networkManager.NetworkConfig.NetworkTransport != transport)
        {
            Debug.LogWarning("[LobbyManager] NetworkManager не использует правильный transport! Обновляем перед StartClient...");
            networkManager.NetworkConfig.NetworkTransport = transport;
            yield return new WaitForSeconds(0.1f);
        }
        
        // Убеждаемся, что адрес и порт установлены
        transport.ConnectionData.Address = ipAddress;
        transport.ConnectionData.Port = defaultPort;
        
        Debug.Log($"[LobbyManager] Transport настроен: Address={transport.ConnectionData.Address}, Port={transport.ConnectionData.Port}");
        
        // Проверяем доступность сервера перед подключением (опционально)
        Debug.Log($"[LobbyManager] Попытка подключения к {ipAddress}:{defaultPort}...");
        Debug.Log($"[LobbyManager] Убедитесь, что сервер запущен и доступен на этом IP!");
        
        // Увеличенная задержка перед StartClient для полной готовности transport
        // Это критически важно для предотвращения ошибок сокета
        yield return new WaitForSeconds(0.8f);
        
        bool success = false;
        try
        {
            Debug.Log($"[LobbyManager] Вызов StartClient() для подключения к {ipAddress}:{defaultPort}...");
            // Проверяем transport еще раз перед StartClient
            if (networkManager.NetworkConfig.NetworkTransport == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager.NetworkConfig.NetworkTransport равен null! Устанавливаем transport...");
                networkManager.NetworkConfig.NetworkTransport = transport;
            }
            success = networkManager.StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] ✗ Ошибка при StartClient(): {e.Message}\n{e.StackTrace}");
            pendingPassword = ""; // Очищаем пароль при ошибке
            yield break;
        }
        
        if (success)
        {
            Debug.Log($"[LobbyManager] ✓ Подключение инициализировано к {ipAddress}:{defaultPort}");
            
            // Увеличенная задержка для инициализации сокета
            // Это помогает избежать ошибки "All socket receive requests were marked as failed"
            yield return new WaitForSeconds(1.0f);
            
            // Проверяем, что объект все еще активен
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError("[LobbyManager] Объект стал неактивным после StartClient!");
                yield break;
            }
            
            // Проверяем, что подключение все еще активно
            if (networkManager != null && networkManager.IsClient)
            {
                Debug.Log($"[LobbyManager] Подключение активно. Проверка соединения...");
                
                // ПРИМЕЧАНИЕ: Ошибка "All socket receive requests were marked as failed" 
                // может появляться из-за старых сокетов Unity Transport, которые еще не полностью закрыты.
                // Это предупреждение не всегда критично - если подключение устанавливается и работает,
                // можно игнорировать это предупреждение.
                Debug.Log($"[LobbyManager] Примечание: Если вы видите предупреждение о сокетах, но подключение работает - это нормально.");
            }
            
            // Запускаем корутину для проверки успешности подключения
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(VerifyConnection(ipAddress));
            }
        }
        else
        {
            Debug.LogError("[LobbyManager] ✗ Ошибка: StartClient() вернул false!");
            pendingPassword = ""; // Очищаем пароль при ошибке
        }
    }
    
    System.Collections.IEnumerator WaitForDisconnectAndConnect(string ipAddress, string password)
    {
        // Ждем отключения
        int attempts = 0;
        while (networkManager != null && (networkManager.IsClient || networkManager.IsHost) && attempts < 20)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (networkManager != null && (networkManager.IsClient || networkManager.IsHost))
        {
            Debug.LogError("[LobbyManager] Не удалось отключиться! Невозможно подключиться к новому лобби.");
            pendingPassword = "";
            yield break;
        }
        
        // Ждем еще немного для полного закрытия сокета
        yield return new WaitForSeconds(0.5f);
        
        // Подключаемся к новому лобби
        ConnectToLobbyInternal(ipAddress, password);
    }
    
    System.Collections.IEnumerator VerifyConnection(string ipAddress)
    {
        Debug.Log($"[LobbyManager] Начало проверки подключения к {ipAddress}...");
        
        // Ждем до 10 секунд для установления соединения (увеличено для надежности)
        float timeout = 10f;
        float elapsed = 0f;
        float checkInterval = 0.2f; // Проверяем каждые 0.2 секунды
        
        while (elapsed < timeout)
        {
            if (networkManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkManager стал null во время проверки подключения!");
                yield break;
            }
            
            // Для клиента проверяем IsConnectedClient и ConnectedClients
            if (networkManager.IsClient)
            {
                bool isConnected = networkManager.IsConnectedClient;
                int connectedCount = networkManager.ConnectedClients.Count;
                
                // Логируем состояние каждые 2 секунды
                if (Mathf.FloorToInt(elapsed) % 2 == 0 && Mathf.FloorToInt(elapsed * 5) % 10 == 0)
                {
                    Debug.Log($"[LobbyManager] Проверка подключения: IsClient={networkManager.IsClient}, IsConnectedClient={isConnected}, ConnectedClients={connectedCount}, elapsed={elapsed:F1}s");
                }
                
                // Для клиента соединение установлено, если IsConnectedClient = true
                // ИЛИ если мы видим других клиентов (включая хоста)
                if (isConnected || connectedCount > 0)
                {
                    Debug.Log($"[LobbyManager] ✓ Соединение установлено с {ipAddress}");
                    Debug.Log($"[LobbyManager] Подключенных клиентов: {connectedCount}");
                    Debug.Log($"[LobbyManager] IsConnectedClient: {isConnected}");
                    Debug.Log($"[LobbyManager] IsClient: {networkManager.IsClient}");
                    
                    // Если подключение работает, предупреждение о сокетах можно игнорировать
                    Debug.Log($"[LobbyManager] Примечание: Предупреждение 'All socket receive requests were marked as failed' " +
                             $"может появляться из-за старых сокетов, но подключение работает нормально.");
                    yield break;
                }
            }
            // Для хоста проверяем, что есть подключенные клиенты
            else if (networkManager.IsHost)
            {
                int connectedCount = networkManager.ConnectedClients.Count;
                if (connectedCount > 0)
                {
                    Debug.Log($"[LobbyManager] ✓ Хост видит {connectedCount} подключенных клиентов");
                    yield break;
                }
            }
            
            elapsed += checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
        
        // Если таймаут истек
        Debug.LogError($"[LobbyManager] ✗ ТАЙМАУТ подключения к {ipAddress} за {timeout} секунд!");
        Debug.LogError($"[LobbyManager] Финальное состояние: IsClient={networkManager?.IsClient}, IsHost={networkManager?.IsHost}, IsConnectedClient={networkManager?.IsConnectedClient}, ConnectedClients={networkManager?.ConnectedClients.Count ?? 0}");
        
        // Проверяем, вызывался ли OnClientConnected
        Debug.LogError($"[LobbyManager] ВАЖНО: Если OnClientConnected НЕ вызывался, значит соединение не установилось!");
        Debug.LogError($"[LobbyManager] Проверьте логи выше - должно быть сообщение '===== OnClientConnected ВЫЗВАН ====='");
        
        if (networkManager == null || !networkManager.IsClient)
        {
            Debug.LogWarning($"[LobbyManager] ⚠ Таймаут подключения к {ipAddress}. Проверьте:\n" +
                           $"1. Правильность IP адреса\n" +
                           $"2. Что сервер запущен\n" +
                           $"3. Что порт {defaultPort} открыт в брандмауэре\n" +
                           $"4. Что оба компьютера в одной сети (Radmin VPN)");
        }
        else
        {
            Debug.LogWarning($"[LobbyManager] ⚠ Подключение инициализировано, но соединение не установлено за {timeout} секунд.");
            Debug.LogWarning($"[LobbyManager] Возможные причины:\n" +
                           $"1. Сервер не отвечает или недоступен\n" +
                           $"2. Проблемы с сетью/брандмауэром\n" +
                           $"3. Ошибки сокета мешают подключению (проверьте ошибки выше)\n" +
                           $"4. Сервер не запущен на указанном IP: {ipAddress}:{defaultPort}");
            
            // Пытаемся проверить доступность сервера
            Debug.LogWarning($"[LobbyManager] Попробуйте проверить доступность сервера:\n" +
                           $"ping {ipAddress}\n" +
                           $"или проверьте, что сервер действительно запущен на этом IP");
        }
        
        // Отключаемся, если соединение не установилось
        if (networkManager != null && networkManager.IsClient && !networkManager.IsConnectedClient)
        {
            Debug.LogWarning("[LobbyManager] Отключаемся из-за таймаута подключения...");
            try
            {
                networkManager.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyManager] Ошибка при отключении: {e.Message}");
            }
        }
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
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                return;
        }

        bool wasHost = networkManager.IsHost;
        bool wasClient = networkManager.IsClient;

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
        if (wasHost)
        {
            Debug.Log("[LobbyManager] Останавливаем хост...");
            try
            {
                networkManager.Shutdown();
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
                networkManager.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Ошибка при отключении клиента: {e.Message}");
            }
        }
        
        // Transport закроется автоматически при Shutdown()
        // Дополнительное время ожидания будет в корутине ConnectAfterDisconnect
        
        // Очищаем pendingPassword при отключении (если мы не переподключаемся, он будет установлен заново)
        // Не очищаем здесь, так как он может быть нужен для переподключения
        // pendingPassword будет очищен в ConnectToLobbyInternal или при ошибке
        
        // Очищаем ссылки
        lobbyNetworkManager = null;

        UpdateUI();
    }
    
    /// <summary>
    /// Очищает pendingPassword (вызывается при ошибке подключения или отмене)
    /// </summary>
    public void ClearPendingPassword()
    {
        pendingPassword = "";
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



