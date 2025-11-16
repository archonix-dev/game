using UnityEngine;
using UnityEngine.UI;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Компонент для отображения найденного лобби в списке поиска. Показывает имя хоста, количество игроков, кнопку подключения и поле для пароля.
/// </summary>
public class LobbySearchItem : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Текст с именем хоста лобби")]
    public Text hostNameText;
    
    [Tooltip("Кнопка подключения к лобби")]
    public Button connectButton;
    
    [Tooltip("InputField для ввода пароля лобби")]
    public InputField passwordInput;
    
    [Tooltip("Текст с количеством игроков в лобби")]
    public Text playersCountText;

    private ulong lobbySteamId;
    private string hostName = "";
    private int currentPlayers = 0;
    private int maxPlayers = 8;
    private string lobbyPassword = "";
    private LobbyManager lobbyManager;

    void Start()
    {
        lobbyManager = LobbyManager.Instance;
        
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }
    }

    /// <summary>
    /// Инициализирует элемент найденного лобби
    /// </summary>
    public void Initialize(ulong lobbyId, string hostName, int currentPlayers, int maxPlayers, string password = "")
    {
        this.lobbySteamId = lobbyId;
        this.hostName = hostName;
        this.currentPlayers = currentPlayers;
        this.maxPlayers = maxPlayers;
        this.lobbyPassword = password;
        
        UpdateUI();
    }

    void UpdateUI()
    {
        // Устанавливаем имя хоста
        if (hostNameText != null)
        {
            hostNameText.text = hostName;
        }
        
        // Обновляем количество игроков
        if (playersCountText != null)
        {
            playersCountText.text = $"{currentPlayers}/{maxPlayers}";
        }
        
        // Очищаем поле пароля
        if (passwordInput != null)
        {
            passwordInput.text = "";
        }
    }

    /// <summary>
    /// Обработчик нажатия на кнопку подключения
    /// </summary>
    void OnConnectButtonClicked()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("[LobbySearchItem] LobbyManager не найден!");
            return;
        }
        
        // Получаем введенный пароль
        string enteredPassword = "";
        if (passwordInput != null)
        {
            enteredPassword = passwordInput.text.Trim();
        }
        
        // Проверяем пароль, если он установлен
        if (!string.IsNullOrEmpty(lobbyPassword))
        {
            if (enteredPassword != lobbyPassword)
            {
                Debug.LogWarning("[LobbySearchItem] Неверный пароль!");
                // Можно показать сообщение об ошибке
                return;
            }
        }
        
        Debug.Log($"[LobbySearchItem] Подключение к лобби {lobbySteamId}, хост: {hostName}");
        
        // Подключаемся к лобби через LobbyManager
        lobbyManager.ConnectToLobbyBySteamId(lobbySteamId);
    }
    
    /// <summary>
    /// Обновляет количество игроков в лобби
    /// </summary>
    public void UpdatePlayersCount(int current, int max)
    {
        currentPlayers = current;
        maxPlayers = max;
        
        if (playersCountText != null)
        {
            playersCountText.text = $"{current}/{max}";
        }
    }
    
    /// <summary>
    /// Получает Steam ID лобби
    /// </summary>
    public ulong GetLobbySteamId()
    {
        return lobbySteamId;
    }
}

