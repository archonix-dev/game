using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections;
using System.Text;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Компонент для отображения игрока в лобби. Показывает имя, пинг, цвет и статус админа.
/// Локальный UI элемент (не NetworkObject), создается каждым клиентом отдельно.
/// </summary>
public class PlayerLobbyItem : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Текст с именем игрока")]
    public Text playerNameText;
    
    [Tooltip("Текст с пингом")]
    public Text pingText;
    
    [Tooltip("Массив Image компонентов для отображения цвета игрока (image_color_player)")]
    public Image[] colorPlayerImages;
    
    [Tooltip("GameObject для отображения статуса админа")]
    public GameObject adminIndicator;

    private uint clientId;
    private bool isAdmin;
    private Color playerColor = Color.white;
    private string playerName = "";
    private MirrorNetworkManager networkManager;
    private float pingUpdateInterval = 5f; // Обновление пинга раз в 5 секунд
    private float lastPingUpdate = 0f;
    private bool isInitialized = false;
    private int cachedRTT = 0; // Кэшированный RTT для синхронизации
    
    // Публичные свойства для доступа к данным
    public string PlayerName => playerName;
    public Color PlayerColor => playerColor;

    void Start()
    {
        networkManager = MirrorNetworkManager.Instance;
    }

    void Update()
    {
        // Обновляем пинг периодически
        if (Time.time - lastPingUpdate >= pingUpdateInterval)
        {
            UpdatePing();
            lastPingUpdate = Time.time;
        }
    }

    /// <summary>
    /// Инициализирует элемент игрока в лобби (вызывается локально на каждом клиенте)
    /// </summary>
    public void Initialize(uint clientId, bool isAdmin, string playerName = null, Color? playerColor = null)
    {
        this.clientId = clientId;
        this.isAdmin = isAdmin;
        
        // Получаем имя из Steam, если не указано
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
                else
                {
                    playerName = GenerateRandomPlayerName();
                }
            }
            else
            {
                playerName = GenerateRandomPlayerName();
            }
            #else
            playerName = GenerateRandomPlayerName();
            #endif
        }
        this.playerName = playerName;
        
        // Используем переданный цвет или цвет по умолчанию
        if (playerColor.HasValue)
        {
            this.playerColor = playerColor.Value;
        }
        
        // Сохраняем имя в PlayerPrefs только для локального игрока
        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
        }
        
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }
        
        if (networkManager != null && clientId == localClientId)
        {
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
        }
        
        isInitialized = true;
        UpdateUI();
        
        // Уведомляем LobbyManager о появлении нового PlayerLobbyItem
        NotifyLobbyManager();
    }

    /// <summary>
    /// Уведомляет LobbyManager о появлении этого PlayerLobbyItem
    /// </summary>
    private void NotifyLobbyManager()
    {
        if (clientId == 0) return;
        
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.RegisterPlayerLobbyItem(clientId, gameObject);
        }
    }

    void UpdateUI()
    {
        // Обновляем имя игрока
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        // Обновляем цвет игрока
        UpdatePlayerColor();

        // Показываем/скрываем индикатор админа
        if (adminIndicator != null)
        {
            adminIndicator.SetActive(isAdmin);
        }

        // Обновляем пинг
        UpdatePing();
    }

    void UpdatePlayerColor()
    {
        // Применяем цвет ко всем Image компонентам
        if (colorPlayerImages != null && colorPlayerImages.Length > 0)
        {
            foreach (Image image in colorPlayerImages)
            {
                if (image != null)
                {
                    image.color = playerColor;
                }
            }
        }
    }

    void UpdatePing()
    {
        if (networkManager == null || pingText == null)
            return;

        // Получаем пинг клиента
        int ping = GetPing(clientId);
        
        // Обновляем текст пинга
        pingText.text = $"{ping} ms";
        
        // Обновляем цвет текста в зависимости от пинга
        UpdatePingColor(ping);
    }

    int GetPing(uint clientId)
    {
        if (networkManager == null)
            return 0;

        // Получаем connectionId локального клиента
        uint localClientId = 0;
        if (NetworkClient.connection != null)
        {
            var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
            }
        }

        // У хоста пинг всегда 5 (фиксированное значение)
        if (NetworkServer.active && NetworkClient.active && clientId == localClientId)
        {
            return 5;
        }

        // Если это локальный клиент (не хост), получаем свой RTT к серверу
        if (NetworkClient.active && clientId == localClientId)
        {
            return GetLocalClientRTT();
        }

        // Если мы хост и это другой игрок, получаем его RTT к серверу
        if (NetworkServer.active && NetworkServer.connections.ContainsKey((int)clientId))
        {
            return GetServerRTTForClient(clientId);
        }

        // Если мы клиент и это другой игрок, используем кэшированный RTT (синхронизирован через сервер)
        // Если кэшированного RTT нет, возвращаем значение по умолчанию
        return cachedRTT > 0 ? cachedRTT : 0;
    }

    /// <summary>
    /// Получает RTT локального клиента (когда мы клиент, а не хост)
    /// </summary>
    private int GetLocalClientRTT()
    {
        if (NetworkClient.connection == null)
            return 0;

        try
        {
            // Пытаемся получить RTT через свойство
            var rttProperty = NetworkClient.connection.GetType().GetProperty("rtt");
            if (rttProperty != null)
            {
                object rttValue = rttProperty.GetValue(NetworkClient.connection);
                if (rttValue != null)
                {
                    // RTT обычно в миллисекундах
                    int rtt = 0;
                    if (rttValue is int)
                        rtt = (int)rttValue;
                    else if (rttValue is float)
                        rtt = Mathf.RoundToInt((float)rttValue);
                    else if (rttValue is double)
                        rtt = Mathf.RoundToInt((float)(double)rttValue);
                    
                    // Если RTT больше 0, возвращаем его
                    if (rtt > 0)
                        return rtt;
                }
            }

            // Пытаемся получить RTT через поле
            var rttField = NetworkClient.connection.GetType().GetField("rtt");
            if (rttField != null)
            {
                object rttValue = rttField.GetValue(NetworkClient.connection);
                if (rttValue != null)
                {
                    int rtt = 0;
                    if (rttValue is int)
                        rtt = (int)rttValue;
                    else if (rttValue is float)
                        rtt = Mathf.RoundToInt((float)rttValue);
                    else if (rttValue is double)
                        rtt = Mathf.RoundToInt((float)(double)rttValue);
                    
                    // Если RTT больше 0, возвращаем его
                    if (rtt > 0)
                        return rtt;
                }
            }
            
            // Пытаемся получить RTT через свойство averageRTT (альтернативный способ)
            var avgRttProperty = NetworkClient.connection.GetType().GetProperty("averageRTT");
            if (avgRttProperty != null)
            {
                object rttValue = avgRttProperty.GetValue(NetworkClient.connection);
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
        catch (System.Exception)
        {
            // Если не удалось получить RTT, возвращаем 0
        }

        // Если RTT еще не инициализирован, возвращаем минимальное значение (не 0)
        // Это может произойти сразу после подключения
        return 1;
    }

    /// <summary>
    /// Получает RTT для клиента на сервере (когда мы хост)
    /// </summary>
    private int GetServerRTTForClient(uint clientId)
    {
        if (!NetworkServer.active) return 0;

        // ВАЖНО: Проверяем наличие подключения перед доступом с обработкой ошибок
        NetworkConnectionToClient connection = null;
        try
        {
            if (NetworkServer.connections.ContainsKey((int)clientId))
            {
                connection = NetworkServer.connections[(int)clientId];
            }
            else
            {
                return 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerLobbyItem] Ошибка при получении подключения для clientId={clientId}: {e.Message}");
            return 0;
        }

        if (connection == null)
            return 0;

        try
        {
            // Пытаемся получить RTT через свойство
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

            // Пытаемся получить RTT через поле
            var rttField = connection.GetType().GetField("rtt");
            if (rttField != null)
            {
                object rttValue = rttField.GetValue(connection);
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
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerLobbyItem] Ошибка при получении RTT для clientId={clientId}: {e.Message}");
        }

        return 0;
    }

    void UpdatePingColor(int ping)
    {
        if (pingText == null)
            return;

        Color pingColor;

        if (ping <= 50)
        {
            // Белый цвет для низкого пинга
            pingColor = Color.white;
        }
        else if (ping >= 80 && ping <= 110)
        {
            // Плавный переход к желтоватому цвету
            float t = (ping - 80f) / 30f; // Нормализуем от 80 до 110
            pingColor = Color.Lerp(Color.white, new Color(1f, 1f, 0.5f, 1f), t);
        }
        else if (ping >= 200)
        {
            // Красный цвет для высокого пинга
            pingColor = Color.red;
        }
        else
        {
            // Плавный переход от желтого к красному для пинга от 110 до 200
            float t = (ping - 110f) / 90f; // Нормализуем от 110 до 200
            pingColor = Color.Lerp(new Color(1f, 1f, 0.5f, 1f), Color.red, t);
        }

        pingText.color = pingColor;
    }

    string GenerateRandomPlayerName()
    {
        // Генерируем случайное имя формата Player_XXXXXX (6 букв от A до Z)
        StringBuilder nameBuilder = new StringBuilder("Player_");
        System.Random random = new System.Random();
        
        for (int i = 0; i < 6; i++)
        {
            char randomChar = (char)('A' + random.Next(0, 26));
            nameBuilder.Append(randomChar);
        }
        
        return nameBuilder.ToString();
    }

    /// <summary>
    /// Устанавливает цвет игрока (локально)
    /// </summary>
    public void SetPlayerColor(Color color)
    {
        playerColor = color;
        UpdatePlayerColor();
    }

    /// <summary>
    /// Устанавливает имя игрока
    /// </summary>
    public void SetPlayerName(string name)
    {
        playerName = name;
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
    }

    /// <summary>
    /// Получает ID клиента
    /// </summary>
    public uint GetClientId()
    {
        return clientId;
    }

    /// <summary>
    /// Проверяет, является ли игрок админом
    /// </summary>
    public bool IsAdmin()
    {
        return isAdmin;
    }

    /// <summary>
    /// Устанавливает RTT для этого игрока (синхронизируется через сервер)
    /// </summary>
    public void SetRTT(int rtt)
    {
        cachedRTT = rtt;
        // Обновляем UI сразу, если пинг уже инициализирован
        if (isInitialized && pingText != null)
        {
            UpdatePing();
        }
    }
}

