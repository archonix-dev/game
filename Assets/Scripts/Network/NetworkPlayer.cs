using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
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
    
    // Сетевые переменные
    private NetworkVariable<FixedString64Bytes> networkPlayerName = new NetworkVariable<FixedString64Bytes>(
        "Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(
        Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
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
        if (IsOwner)
        {
            SetupOwnerPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
        
        // Подписываемся на изменения сетевых переменных
        networkPlayerName.OnValueChanged += OnPlayerNameChanged;
        networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // Устанавливаем начальные значения
        if (IsOwner)
        {
            // Загружаем цвет из PlayerPrefs, если он был выбран в меню
            LoadColorFromPlayerPrefs();
            
            // Загружаем имя из PlayerPrefs или используем значение по умолчанию
            LoadNameFromPlayerPrefs();
            
            // Если имя все еще "Player" и есть в PlayerPrefs, загружаем еще раз
            if (playerName == "Player" && PlayerPrefs.HasKey("PlayerName"))
            {
                playerName = PlayerPrefs.GetString("PlayerName", "Player");
                Debug.Log($"[NetworkPlayer] Имя загружено из PlayerPrefs в OnNetworkSpawn: {playerName}");
            }
            
            // Устанавливаем сетевые переменные
            networkPlayerName.Value = new FixedString64Bytes(playerName);
            networkPlayerColor.Value = playerColor;
            
            Debug.Log($"[NetworkPlayer] NetworkPlayer инициализирован: PlayerName={playerName}, PlayerColor={playerColor}");
        }
        
        // Применяем начальные значения сразу (для всех клиентов)
        ApplyPlayerColor(networkPlayerColor.Value);
        
        // Применяем имя сразу после спавна
        if (networkPlayerName.Value.Length > 0)
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
    
    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        networkPlayerName.OnValueChanged -= OnPlayerNameChanged;
        networkPlayerColor.OnValueChanged -= OnPlayerColorChanged;
        
        base.OnNetworkDespawn();
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
        // Загружаем имя из PlayerPrefs, если оно было сохранено
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
            voiceVisualizer.ApplyPlayerColor(networkPlayerColor.Value);
            voiceVisualizer.SetPlayerName(networkPlayerName.Value.ToString());
        }
    }
    
    void OnPlayerColorChanged(Color oldColor, Color newColor)
    {
        playerColor = newColor;
        
        // Применяем цвет к игроку (например, к материалу)
        ApplyPlayerColor(newColor);
        
        // Уведомляем VoiceWaveVisualizer об изменении цвета
        NotifyVoiceWaveVisualizer();
    }
    
    void OnPlayerNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        playerName = newName.ToString();
        
        // Уведомляем VoiceWaveVisualizer об изменении имени
        NotifyVoiceWaveVisualizer();
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
    [ServerRpc(RequireOwnership = true)]
    public void SetPlayerNameServerRpc(FixedString64Bytes newName)
    {
        networkPlayerName.Value = newName;
    }
    
    [ServerRpc(RequireOwnership = true)]
    public void SetPlayerColorServerRpc(Color newColor)
    {
        networkPlayerColor.Value = newColor;
    }
    
    // Публичные свойства для доступа к данным игрока
    public string PlayerName => networkPlayerName.Value.ToString();
    public Color PlayerColor => networkPlayerColor.Value;
    public ulong PlayerId => OwnerClientId;
    
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
        
        if (IsOwner)
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
        if (IsSpawned)
        {
            UpdatePlayerModelVisibility();
        }
    }
}
