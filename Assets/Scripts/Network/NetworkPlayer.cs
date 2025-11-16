using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ClientNetworkTransform networkTransform;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    
    [Header("Player Settings")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private Color playerColor = Color.white;
    
    [Header("Player Model Visibility")]
    [Tooltip("Массив GameObject модели игрока, которые должны быть скрыты для владельца, но видны в лобби")]
    [SerializeField] private GameObject[] playerModelObjects;
    
    [Tooltip("Имя сцены лобби (для проверки видимости модели)")]
    [SerializeField] private string lobbySceneName = "Lobby";
    
    // Сетевые переменные Mirror
    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    private string networkPlayerName = "Player";
    
    [SyncVar(hook = nameof(OnPlayerColorChanged))]
    private Color networkPlayerColor = Color.white;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Инициализируем компоненты
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
            
        if (networkTransform == null)
            networkTransform = GetComponent<ClientNetworkTransform>();
            
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
            
        if (audioListener == null)
            audioListener = GetComponentInChildren<AudioListener>();
        
        // Настраиваем камеру и аудио только для владельца
        if (isOwned)
        {
            SetupOwnerPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
        
        // Устанавливаем начальные значения
        if (isOwned)
        {
            // Загружаем цвет из PlayerPrefs, если он был выбран в меню
            LoadColorFromPlayerPrefs();
            
            // Загружаем имя из Steam или PlayerPrefs
            LoadNameFromPlayerPrefs();
            
            // Если имя все еще "Player", пытаемся получить из Steam еще раз
            if (playerName == "Player")
            {
                #if !DISABLESTEAMWORKS
                if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
                {
                    string steamName = SteamManager.Instance.GetSteamName();
                    if (!string.IsNullOrEmpty(steamName))
                    {
                        playerName = steamName;
                        Debug.Log($"[NetworkPlayer] Имя получено из Steam в OnStartClient: {playerName}");
                    }
                }
                #endif
                
                // Если Steam не доступен, пробуем PlayerPrefs
                if (playerName == "Player" && PlayerPrefs.HasKey("PlayerName"))
                {
                    playerName = PlayerPrefs.GetString("PlayerName", "Player");
                    Debug.Log($"[NetworkPlayer] Имя загружено из PlayerPrefs в OnStartClient: {playerName}");
                }
            }
            
            // Устанавливаем сетевые переменные через Command
            SetPlayerNameCommand(playerName);
            SetPlayerColorCommand(playerColor);
            
            Debug.Log($"[NetworkPlayer] NetworkPlayer инициализирован: PlayerName={playerName}, PlayerColor={playerColor}");
        }
        
        // Применяем начальные значения сразу (для всех клиентов)
        ApplyPlayerColor(networkPlayerColor);
        
        // Применяем имя сразу после спавна
        if (!string.IsNullOrEmpty(networkPlayerName))
        {
            NotifyVoiceWaveVisualizer();
        }
        
        // Обновляем видимость модели игрока
        UpdatePlayerModelVisibility();
    }
    
    void SetupOwnerPlayer()
    {
        // Включаем камеру и аудио только для владельца
        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";
        }
        
        if (audioListener != null)
        {
            audioListener.enabled = true;
        }
        
        // Включаем управление игроком
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        // Обновляем видимость модели для владельца
        UpdatePlayerModelVisibility();
    }
    
    void SetupRemotePlayer()
    {
        // Отключаем камеру и аудио для других игроков
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }
        
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        
        // Отключаем управление для других игроков
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Для других игроков модель всегда видна
        SetPlayerModelObjectsVisibility(true);
    }
    
    void LoadColorFromPlayerPrefs()
    {
        // Загружаем цвет из PlayerPrefs, если он был сохранен
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
    }
    
    void LoadNameFromPlayerPrefs()
    {
        // Пытаемся получить имя из Steam
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance != null && SteamManager.Instance.IsSteamInitialized())
        {
            string steamName = SteamManager.Instance.GetSteamName();
            if (!string.IsNullOrEmpty(steamName))
            {
                playerName = steamName;
                Debug.Log($"[NetworkPlayer] Имя получено из Steam: {playerName}");
                return;
            }
        }
        #endif
        
        // Если Steam не доступен, загружаем из PlayerPrefs
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            playerName = PlayerPrefs.GetString("PlayerName", "Player");
        }
    }
    
    void NotifyVoiceWaveVisualizer()
    {
        // Находим VoiceWaveVisualizer на этом объекте или в дочерних объектах
        VoiceWaveVisualizer voiceVisualizer = GetComponentInChildren<VoiceWaveVisualizer>();
        if (voiceVisualizer != null)
        {
            voiceVisualizer.ApplyPlayerColor(networkPlayerColor);
            voiceVisualizer.SetPlayerName(networkPlayerName);
        }
    }
    
    void OnPlayerColorChanged(Color oldColor, Color newColor)
    {
        playerColor = newColor;
        
        // Применяем цвет к игроку (например, к материалу)
        ApplyPlayerColor(newColor);
        
        // Уведомляем VoiceWaveVisualizer об изменении цвета
        NotifyVoiceWaveVisualizer();
        
        // Уведомляем LobbyManager об изменении цвета для обновления PlayerLobbyItem
        NotifyLobbyManager();
    }
    
    void OnPlayerNameChanged(string oldName, string newName)
    {
        playerName = newName;
        
        // Уведомляем VoiceWaveVisualizer об изменении имени
        NotifyVoiceWaveVisualizer();
        
        // Уведомляем LobbyManager об изменении имени для обновления PlayerLobbyItem
        NotifyLobbyManager();
    }
    
    /// <summary>
    /// Уведомляет LobbyManager об изменении данных игрока
    /// </summary>
    void NotifyLobbyManager()
    {
        if (netIdentity == null || netIdentity.netId == 0) return;
        
        LobbyManager lobbyManager = LobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.OnNetworkPlayerDataChanged(PlayerId);
        }
    }
    
    void ApplyPlayerColor(Color color)
    {
        // НЕ применяем цвет ко всем рендерерам (голова и тело не должны окрашиваться)
        // Цвет применяется только к VoiceWaveVisualizer компонентам (lineRenderer, targetSpriteRenderer, statusText)
        // через метод NotifyVoiceWaveVisualizer
        
        // Уведомляем VoiceWaveVisualizer о применении цвета
        NotifyVoiceWaveVisualizer();
    }
    
    // Методы для изменения имени и цвета (только для владельца)
    [Command]
    public void SetPlayerNameCommand(string newName)
    {
        networkPlayerName = newName;
    }
    
    [Command]
    public void SetPlayerColorCommand(Color newColor)
    {
        networkPlayerColor = newColor;
    }
    
    // Публичные свойства для доступа к данным игрока
    public string PlayerName => networkPlayerName;
    public Color PlayerColor => networkPlayerColor;
    public uint PlayerId
    {
        get
        {
            // На сервере используем connectionToClient
            if (connectionToClient != null)
            {
                return (uint)connectionToClient.connectionId;
            }
            
            // На клиенте для локального игрока пытаемся получить connectionId через рефлексию
            if (isOwned && NetworkClient.active && NetworkClient.connection != null)
            {
                try
                {
                    var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                    if (connectionIdField != null)
                    {
                        return (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                    }
                }
                catch (System.Exception) { }
            }
            
            // Если ничего не помогло, используем netId как временный идентификатор
            if (netIdentity != null && netIdentity.netId != 0)
            {
                // Используем младшие 32 бита netId как connectionId
                return (uint)(netIdentity.netId & 0xFFFFFFFF);
            }
            
            return 0;
        }
    }
    
    // Метод для получения информации об игроке
    public string GetPlayerInfo()
    {
        return $"Player {PlayerId}: {PlayerName}";
    }
    
    /// <summary>
    /// Проверяет, находимся ли мы в сцене лобби
    /// </summary>
    private bool IsInLobbyScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        return currentSceneName == lobbySceneName;
    }
    
    /// <summary>
    /// Обновляет видимость объектов модели игрока
    /// </summary>
    private void UpdatePlayerModelVisibility()
    {
        // В редакторе (окно Scene) модель всегда видна для визуализации
        if (!Application.isPlaying)
        {
            SetPlayerModelObjectsVisibility(true);
            return;
        }
        
        if (isOwned)
        {
            // Для владельца в игре: модель скрыта (не видим сами себя)
            SetPlayerModelObjectsVisibility(false);
        }
        else
        {
            // Для других игроков: модель всегда видна (в лобби и в игре)
            SetPlayerModelObjectsVisibility(true);
        }
    }
    
    /// <summary>
    /// Устанавливает видимость всех объектов модели игрока
    /// </summary>
    private void SetPlayerModelObjectsVisibility(bool visible)
    {
        if (playerModelObjects == null || playerModelObjects.Length == 0)
            return;
        
        foreach (GameObject modelObject in playerModelObjects)
        {
            if (modelObject != null)
            {
                modelObject.SetActive(visible);
            }
        }
    }
    
    /// <summary>
    /// Вызывается при смене сцены для обновления видимости модели
    /// </summary>
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Обновляем видимость модели при смене сцены
        if (netIdentity != null && netIdentity.netId != 0)
        {
            UpdatePlayerModelVisibility();
        }
    }
}
