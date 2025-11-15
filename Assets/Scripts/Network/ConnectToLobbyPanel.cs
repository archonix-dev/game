using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Панель подключения к другому лобби. Позволяет ввести IP адрес и пароль для подключения.
/// </summary>
public class ConnectToLobbyPanel : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Поле ввода IP адреса лобби")]
    public InputField ipAddressInput;
    
    [Tooltip("Поле ввода пароля лобби")]
    public InputField passwordInput;
    
    [Tooltip("Кнопка 'Войти' (подключиться)")]
    public Button connectButton;
    
    [Tooltip("Кнопка 'Назад' (закрыть панель)")]
    public Button backButton;
    
    [Tooltip("Текст для отображения статуса подключения (опционально)")]
    public Text statusText;

    private LobbyManager lobbyManager;
    private NetworkManager networkManager;
    private bool isConnecting = false;
    private bool isReconnecting = false; // Флаг для отслеживания процесса переподключения

    void Start()
    {
        lobbyManager = FindObjectOfType<LobbyManager>();
        
        // Пытаемся найти NetworkManager, но не критично, если его еще нет
        networkManager = NetworkManager.Singleton;

        SetupButtons();
        
        // Подписываемся на события NetworkManager для отслеживания подключения
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            // Если NetworkManager еще не создан, попробуем найти его позже
            StartCoroutine(FindNetworkManagerDelayed());
        }
    }
    
    System.Collections.IEnumerator FindNetworkManagerDelayed()
    {
        // Ждем несколько кадров, чтобы NetworkManager успел инициализироваться
        yield return new WaitForSeconds(0.5f);
        
        int attempts = 0;
        while (networkManager == null && attempts < 10)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback += OnClientConnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                Debug.Log("[ConnectToLobbyPanel] NetworkManager найден!");
                yield break;
            }
            attempts++;
            yield return new WaitForSeconds(0.2f);
        }
        
        if (networkManager == null)
        {
            Debug.LogWarning("[ConnectToLobbyPanel] NetworkManager не найден, но подключение все равно возможно.");
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

    void SetupButtons()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);
    }
    
    void Update()
    {
        // Кнопка всегда активна
        if (connectButton != null)
        {
            connectButton.interactable = true;
        }
    }

    void OnConnectButtonClicked()
    {
        // Предотвращаем множественные нажатия
        if (isConnecting)
        {
            Debug.LogWarning("[ConnectToLobbyPanel] Подключение уже в процессе!");
            return;
        }

        // Получаем IP адрес и пароль
        string ipAddress = "";
        string password = "";

        if (ipAddressInput != null)
        {
            ipAddress = ipAddressInput.text.Trim();
        }

        if (passwordInput != null)
        {
            password = passwordInput.text.Trim();
        }

        // Проверяем, что IP адрес введен
        if (string.IsNullOrEmpty(ipAddress))
        {
            UpdateStatus("Введите IP адрес лобби!", true);
            Debug.LogWarning("[ConnectToLobbyPanel] Введите IP адрес лобби!");
            return;
        }

        // Проверяем валидность IP адреса
        System.Net.IPAddress ip;
        if (!System.Net.IPAddress.TryParse(ipAddress, out ip))
        {
            UpdateStatus("Неверный формат IP адреса!", true);
            Debug.LogWarning($"[ConnectToLobbyPanel] Неверный формат IP адреса: {ipAddress}");
            return;
        }

        // Проверяем, что пароль введен
        if (string.IsNullOrEmpty(password))
        {
            UpdateStatus("Введите пароль лобби!", true);
            Debug.LogWarning("[ConnectToLobbyPanel] Введите пароль лобби!");
            return;
        }

        // Проверяем, что LobbyManager найден
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager == null)
            {
                UpdateStatus("Ошибка: LobbyManager не найден!", true);
                Debug.LogError("[ConnectToLobbyPanel] LobbyManager не найден!");
                return;
            }
        }

        // Проверяем, что NetworkManager доступен
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                UpdateStatus("Ошибка: NetworkManager не найден!", true);
                Debug.LogError("[ConnectToLobbyPanel] NetworkManager не найден!");
                return;
            }
        }

        // Начинаем подключение
        isConnecting = true;
        
        // Проверяем, нужно ли переподключение (отключение от текущего лобби)
        bool needsReconnect = networkManager.IsClient || networkManager.IsHost;
        isReconnecting = needsReconnect;
        
        // Показываем статус в зависимости от текущего состояния
        if (needsReconnect)
        {
            UpdateStatus($"Отключение от текущего лобби и подключение к {ipAddress}...", false);
            Debug.Log($"[ConnectToLobbyPanel] Переподключение: отключение от текущего лобби и подключение к {ipAddress}");
        }
        else
        {
            UpdateStatus($"Подключение к {ipAddress}...", false);
            Debug.Log($"[ConnectToLobbyPanel] Начало подключения к {ipAddress}");
        }
        
        // Подключаемся к лобби (LobbyManager сам отключит от текущего, если нужно)
        lobbyManager.ConnectToLobby(ipAddress, password);
        
        // Запускаем таймер для проверки успешности подключения
        StartCoroutine(CheckConnectionStatus());
    }
    
    System.Collections.IEnumerator CheckConnectionStatus()
    {
        // Ждем до 15 секунд для подключения (увеличено для переподключения)
        float timeout = 15f;
        float elapsed = 0f;
        
        // Если идет переподключение, даем больше времени на отключение
        if (isReconnecting)
        {
            yield return new WaitForSeconds(1.5f);
        }
        
        while (elapsed < timeout && isConnecting)
        {
            // Проверяем, что NetworkManager все еще доступен
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }
            
            if (networkManager != null && networkManager.IsClient)
            {
                // Проверяем, что соединение действительно установлено
                if (networkManager.ConnectedClients.Count > 0 || networkManager.IsConnectedClient)
                {
                    // Подключение успешно
                    UpdateStatus("Подключение успешно!", false);
                    Debug.Log("[ConnectToLobbyPanel] ✓ Подключение успешно!");
                    isConnecting = false;
                    isReconnecting = false;
                    
                    // Закрываем панель через небольшую задержку
                    yield return new WaitForSeconds(1f);
                    if (gameObject != null)
                    {
                        gameObject.SetActive(false);
                    }
                    yield break;
                }
            }
            
            // Проверяем, не произошло ли отключение во время подключения (если не переподключение)
            if (!isReconnecting && networkManager != null && !networkManager.IsClient && !networkManager.IsHost)
            {
                // Если мы не в процессе переподключения и отключились, это ошибка
                if (elapsed > 2f) // Даем время на начальное подключение
                {
                    UpdateStatus("Ошибка: Подключение было прервано. Проверьте IP и пароль.", true);
                    Debug.LogError("[ConnectToLobbyPanel] ✗ Подключение было прервано!");
                    isConnecting = false;
                    isReconnecting = false;
                    yield break;
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Если таймаут истек, но мы не подключились
        if (isConnecting)
        {
            UpdateStatus("Ошибка: Не удалось подключиться. Проверьте IP и пароль.", true);
            Debug.LogError("[ConnectToLobbyPanel] ✗ Таймаут подключения!");
            isConnecting = false;
            isReconnecting = false;
        }
    }
    
    void OnClientConnected(ulong clientId)
    {
        if (networkManager != null && clientId == networkManager.LocalClientId)
        {
            Debug.Log($"[ConnectToLobbyPanel] ✓ Клиент {clientId} успешно подключен!");
            UpdateStatus("Подключение успешно!", false);
            isConnecting = false;
            isReconnecting = false; // Сбрасываем флаг переподключения при успешном подключении
        }
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        if (networkManager != null && clientId == networkManager.LocalClientId)
        {
            // Если мы находимся в процессе переподключения, это ожидаемое отключение
            if (isReconnecting && isConnecting)
            {
                Debug.Log($"[ConnectToLobbyPanel] Ожидаемое отключение от предыдущего лобби (переподключение к новому)...");
                // Не показываем ошибку и не сбрасываем isConnecting, так как это часть процесса переподключения
                // isReconnecting будет сброшен после успешного подключения или таймаута
                return;
            }
            
            // Если мы не в процессе подключения, это неожиданное отключение
            if (!isConnecting)
            {
                Debug.Log($"[ConnectToLobbyPanel] ✗ Клиент {clientId} отключен!");
                UpdateStatus("Подключение разорвано.", true);
            }
            // Если мы в процессе подключения, но не переподключения, это может быть ошибка пароля
            else
            {
                Debug.LogWarning($"[ConnectToLobbyPanel] Отключение во время подключения. Возможно, неверный пароль.");
                // Не сбрасываем isConnecting здесь, пусть таймаут обработает это
            }
        }
    }
    
    void UpdateStatus(string message, bool isError)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = isError ? Color.red : Color.green;
        }
        Debug.Log($"[ConnectToLobbyPanel] {message}");
    }
    
    /// <summary>
    /// Публичный метод для уведомления об ошибке пароля (вызывается из LobbyNetworkManager)
    /// </summary>
    public void OnPasswordError()
    {
        if (isConnecting)
        {
            UpdateStatus("Ошибка: Неверный пароль! Подключение отклонено.", true);
            Debug.LogError("[ConnectToLobbyPanel] Неверный пароль!");
            isConnecting = false;
            isReconnecting = false;
        }
    }

    void OnBackButtonClicked()
    {
        // Сбрасываем флаги при закрытии панели
        isConnecting = false;
        isReconnecting = false;
        
        // Закрываем панель
        gameObject.SetActive(false);
    }
}

