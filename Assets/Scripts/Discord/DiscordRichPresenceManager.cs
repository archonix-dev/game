using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Discord;

/// <summary>
/// Управляет Discord Rich Presence для отображения статуса игры в Discord
/// </summary>
public class DiscordRichPresenceManager : MonoBehaviour
{
    [Header("Discord Settings")]
    [Tooltip("Включить Discord Rich Presence (если выключено, Discord не будет запускаться автоматически)")]
    [SerializeField] private bool enableDiscordRichPresence = true;
    
    [Tooltip("Discord Application Client ID (из Discord Developer Portal)")]
    [SerializeField] private long clientId = 1445531932019527690;
    
    [Header("Scene Names")]
    [Tooltip("Название сцены меню")]
    [SerializeField] private string menuSceneName = "Menu";
    
    [Tooltip("Название сцены лобби")]
    [SerializeField] private string lobbySceneName = "Lobby";
    
    [Tooltip("Название сцены основной игры")]
    [SerializeField] private string mainSceneName = "Main";
    
    [Header("Update Settings")]
    [Tooltip("Интервал обновления Rich Presence (секунды)")]
    [SerializeField] private float updateInterval = 1f;
    
    private static DiscordRichPresenceManager instance;
    private Discord.Discord discord;
    private ActivityManager activityManager;
    private string currentLocation = "";
    private string currentScene = "";
    private int currentPlayers = 0;
    private int maxPlayers = 4;
    private float lastUpdateTime = 0f;
    private bool isInitialized = false;
    
    public static DiscordRichPresenceManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<DiscordRichPresenceManager>();
            }
            return instance;
        }
    }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Инициализируем Discord только если Rich Presence включен
            if (enableDiscordRichPresence)
            {
                InitializeDiscord();
            }
            else
            {
                Debug.Log("[DiscordRichPresenceManager] Discord Rich Presence отключен в настройках");
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Подписываемся на события смены сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Подписываемся на события LocationZone
        LocationZone.OnLocalPlayerEnterZone += OnLocationZoneEntered;
        
        // Обновляем Rich Presence при старте
        UpdateRichPresence();
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LocationZone.OnLocalPlayerEnterZone -= OnLocationZoneEntered;
        
        // Закрываем Discord SDK
        if (discord != null)
        {
            discord.Dispose();
            discord = null;
        }
    }
    
    void Update()
    {
        if (!isInitialized || discord == null)
            return;
        
        // Обновляем Discord SDK (требуется вызывать каждый кадр)
        try
        {
            discord.RunCallbacks();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiscordRichPresenceManager] Ошибка при вызове RunCallbacks: {e.Message}");
        }
        
        // Периодически обновляем Rich Presence
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateRichPresence();
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// Инициализирует Discord SDK
    /// </summary>
    void InitializeDiscord()
    {
        // Не инициализируем, если Rich Presence отключен
        if (!enableDiscordRichPresence)
        {
            isInitialized = false;
            return;
        }
        
        try
        {
            // Используем CreateFlags.NoRequireDiscord, чтобы не запускать Discord автоматически
            // Это предотвратит автоматический запуск Discord, если он не запущен
            discord = new Discord.Discord(clientId, (ulong)CreateFlags.NoRequireDiscord);
            activityManager = discord.GetActivityManager();
            isInitialized = true;
            Debug.Log($"[DiscordRichPresenceManager] Discord SDK инициализирован с Client ID: {clientId}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiscordRichPresenceManager] Не удалось инициализировать Discord SDK: {e.Message}");
            Debug.Log("[DiscordRichPresenceManager] Rich Presence не будет работать. Discord может быть не запущен или не установлен.");
            isInitialized = false;
            discord = null;
            activityManager = null;
        }
    }
    
    /// <summary>
    /// Обработчик загрузки сцены
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentScene = scene.name;
        currentLocation = ""; // Сбрасываем локацию при смене сцены
        
        // Обновляем Rich Presence при смене сцены
        UpdateRichPresence();
    }
    
    /// <summary>
    /// Обработчик входа в зону локации
    /// </summary>
    void OnLocationZoneEntered(string locationName)
    {
        if (!string.IsNullOrWhiteSpace(locationName))
        {
            currentLocation = locationName;
            UpdateRichPresence();
        }
    }
    
    /// <summary>
    /// Обновляет Rich Presence в Discord
    /// </summary>
    void UpdateRichPresence()
    {
        if (!isInitialized || activityManager == null)
            return;
        
        // Получаем актуальную информацию о лобби
        UpdatePlayerCount();
        
        // Создаем Activity
        var activity = new Activity
        {
            Type = ActivityType.Playing,
            ApplicationId = clientId,
            Instance = true
        };
        
        // Настраиваем в зависимости от текущей сцены
        if (currentScene == menuSceneName)
        {
            // На сцене Menu
            activity.Details = "localhost";
            activity.State = $"$sudo players {currentPlayers} / {maxPlayers}";
        }
        else if (currentScene == lobbySceneName)
        {
            // На сцене Lobby
            activity.Details = $"$sudo players {currentPlayers} / {maxPlayers}";
            activity.State = "$sudo location main";
        }
        else if (currentScene == mainSceneName)
        {
            // На сцене Main
            activity.Details = $"$sudo players {currentPlayers} / {maxPlayers}";
            
            // Если есть информация о локации, используем её, иначе "main"
            if (!string.IsNullOrWhiteSpace(currentLocation))
            {
                activity.State = $"$sudo location {currentLocation}";
            }
            else
            {
                activity.State = "$sudo location main";
            }
        }
        else
        {
            // На других сценах (например, Start)
            activity.Details = "В главном меню";
            activity.State = "";
        }
        
        // Обновляем Activity в Discord
        activityManager.UpdateActivity(activity, (result) =>
        {
            if (result != Result.Ok)
            {
                Debug.LogWarning($"[DiscordRichPresenceManager] Не удалось обновить Rich Presence: {result}");
            }
        });
    }
    
    /// <summary>
    /// Обновляет информацию о количестве игроков
    /// </summary>
    void UpdatePlayerCount()
    {
        // Получаем максимальное количество игроков
        maxPlayers = 4; // Значение по умолчанию
        
        if (LobbyManager.Instance != null)
        {
            maxPlayers = LobbyManager.Instance.maxPlayers;
        }
        else if (LobbyNetworkManager.Instance != null)
        {
            maxPlayers = LobbyNetworkManager.Instance.defaultMaxPlayers;
        }
        
        // Получаем текущее количество игроков
        var networkManager = Mirror.NetworkManager.singleton;
        if (networkManager != null && networkManager.isNetworkActive)
        {
            // Используем numPlayers из NetworkManager (работает и на сервере, и на клиенте)
            currentPlayers = networkManager.numPlayers;
            
            // Если numPlayers равен 0, но есть подключения, используем количество подключений
            if (currentPlayers == 0 && NetworkServer.active && NetworkServer.connections.Count > 0)
            {
                currentPlayers = NetworkServer.connections.Count;
            }
        }
        else
        {
            // Не подключены к сети
            currentPlayers = 0;
        }
    }
    
    /// <summary>
    /// Принудительно обновляет Rich Presence (можно вызвать извне)
    /// </summary>
    public void ForceUpdate()
    {
        UpdateRichPresence();
    }
    
    /// <summary>
    /// Устанавливает текущую локацию вручную
    /// </summary>
    public void SetLocation(string locationName)
    {
        currentLocation = locationName ?? "";
        UpdateRichPresence();
    }
    
    /// <summary>
    /// Включает или выключает Discord Rich Presence
    /// </summary>
    public void SetDiscordRichPresenceEnabled(bool enabled)
    {
        if (enableDiscordRichPresence == enabled)
            return;
        
        enableDiscordRichPresence = enabled;
        
        if (enabled)
        {
            // Включаем Rich Presence
            if (!isInitialized)
            {
                InitializeDiscord();
            }
        }
        else
        {
            // Выключаем Rich Presence
            if (discord != null)
            {
                try
                {
                    discord.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DiscordRichPresenceManager] Ошибка при закрытии Discord SDK: {e.Message}");
                }
                discord = null;
                activityManager = null;
                isInitialized = false;
            }
        }
    }
    
    /// <summary>
    /// Возвращает, включен ли Discord Rich Presence
    /// </summary>
    public bool IsDiscordRichPresenceEnabled()
    {
        return enableDiscordRichPresence;
    }
}

