using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// Панель выбора цвета игрока. Позволяет выбрать цвет из списка доступных цветов.
/// </summary>
public class ColorSelectionPanel : MonoBehaviour
{
    [Header("Цвета")]
    [Tooltip("Кнопка выбора красного цвета")]
    public Button redColorButton;
    
    [Tooltip("Кнопка выбора синего цвета")]
    public Button blueColorButton;
    
    [Tooltip("Кнопка выбора белого цвета")]
    public Button whiteColorButton;
    
    [Tooltip("Кнопка выбора зеленого цвета")]
    public Button greenColorButton;
    
    [Tooltip("Кнопка выбора фиолетового цвета")]
    public Button violetColorButton;
    
    [Tooltip("Кнопка выбора розового цвета")]
    public Button pinkColorButton;
    
    [Tooltip("Кнопка выбора голубого цвета")]
    public Button lightBlueColorButton;
    
    [Tooltip("Кнопка выбора салатового цвета")]
    public Button limeColorButton;

    [Header("Настройки цветов")]
    [Tooltip("Красный цвет")]
    public Color redColor = Color.red;
    
    [Tooltip("Синий цвет")]
    public Color blueColor = Color.blue;
    
    [Tooltip("Белый цвет")]
    public Color whiteColor = Color.white;
    
    [Tooltip("Зеленый цвет")]
    public Color greenColor = Color.green;
    
    [Tooltip("Фиолетовый цвет")]
    public Color violetColor = new Color(0.5f, 0f, 1f, 1f);
    
    [Tooltip("Розовый цвет")]
    public Color pinkColor = new Color(1f, 0.75f, 0.8f, 1f);
    
    [Tooltip("Голубой цвет")]
    public Color lightBlueColor = new Color(0.5f, 0.8f, 1f, 1f);
    
    [Tooltip("Салатовый цвет")]
    public Color limeColor = new Color(0.5f, 1f, 0f, 1f);

    private LobbyManager lobbyManager;
    private PlayerLobbyItem localPlayerItem;
    private NetworkManager networkManager;

    void Start()
    {
        lobbyManager = FindObjectOfType<LobbyManager>();
        networkManager = MirrorNetworkManager.Instance;
        
        SetupColorButtons();
        
        // Инициализируем цвета по умолчанию
        InitializeDefaultColors();
    }

    void InitializeDefaultColors()
    {
        // Устанавливаем цвета по умолчанию если они не были изменены
        if (redColor == Color.red)
            redColor = Color.red;
        
        if (blueColor == Color.blue)
            blueColor = Color.blue;
        
        if (whiteColor == Color.white)
            whiteColor = Color.white;
        
        if (greenColor == Color.green)
            greenColor = Color.green;
        
        if (violetColor == new Color(0.5f, 0f, 1f, 1f))
            violetColor = new Color(0.5f, 0f, 1f, 1f);
        
        if (pinkColor == new Color(1f, 0.75f, 0.8f, 1f))
            pinkColor = new Color(1f, 0.75f, 0.8f, 1f);
        
        if (lightBlueColor == new Color(0.5f, 0.8f, 1f, 1f))
            lightBlueColor = new Color(0.5f, 0.8f, 1f, 1f);
        
        if (limeColor == new Color(0.5f, 1f, 0f, 1f))
            limeColor = new Color(0.5f, 1f, 0f, 1f);
    }

    void SetupColorButtons()
    {
        if (redColorButton != null)
            redColorButton.onClick.AddListener(() => OnColorSelected(redColor));
        
        if (blueColorButton != null)
            blueColorButton.onClick.AddListener(() => OnColorSelected(blueColor));
        
        if (whiteColorButton != null)
            whiteColorButton.onClick.AddListener(() => OnColorSelected(whiteColor));
        
        if (greenColorButton != null)
            greenColorButton.onClick.AddListener(() => OnColorSelected(greenColor));
        
        if (violetColorButton != null)
            violetColorButton.onClick.AddListener(() => OnColorSelected(violetColor));
        
        if (pinkColorButton != null)
            pinkColorButton.onClick.AddListener(() => OnColorSelected(pinkColor));
        
        if (lightBlueColorButton != null)
            lightBlueColorButton.onClick.AddListener(() => OnColorSelected(lightBlueColor));
        
        if (limeColorButton != null)
            limeColorButton.onClick.AddListener(() => OnColorSelected(limeColor));
    }

    void OnColorSelected(Color color)
    {
        PlayerPrefs.SetFloat("PlayerColor_R", color.r);
        PlayerPrefs.SetFloat("PlayerColor_G", color.g);
        PlayerPrefs.SetFloat("PlayerColor_B", color.b);
        PlayerPrefs.SetFloat("PlayerColor_A", color.a);
        PlayerPrefs.Save();
        
        FindLocalPlayerItem();
        
        if (localPlayerItem != null)
        {
            localPlayerItem.SetPlayerColor(color);
        }
        else
        {
            StartCoroutine(FindAndSetColorDelayed(color));
        }
        
        // Синхронизируем цвет через NetworkPlayer
        NetworkPlayer localPlayer = FindLocalNetworkPlayer();
        if (localPlayer != null && localPlayer.netIdentity != null && localPlayer.netIdentity.netId != 0 && localPlayer.isOwned)
        {
            localPlayer.SetPlayerColorCommand(color);
        }
        
        // Также обновляем в LobbyManager для UI
        LobbyManager lobbyMgr = FindObjectOfType<LobbyManager>();
        if (lobbyMgr != null)
        {
            uint localClientId = 0;
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
            lobbyMgr.UpdatePlayerColorLocally(localClientId, color);
        }
        
        // Скрываем панель после выбора цвета
        HidePanel();
    }
    
    /// <summary>
    /// Находит локального NetworkPlayer
    /// </summary>
    NetworkPlayer FindLocalNetworkPlayer()
    {
        NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
        foreach (NetworkPlayer player in allPlayers)
        {
            if (player != null && player.netIdentity != null && player.netIdentity.netId != 0 && player.isOwned)
            {
                return player;
            }
        }
        return null;
    }

    System.Collections.IEnumerator FindAndSetColorDelayed(Color color)
    {
        float timeout = 2f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            FindLocalPlayerItem();
            
            if (localPlayerItem != null)
            {
                localPlayerItem.SetPlayerColor(color);
                yield break;
            }
            
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }

    void HidePanel()
    {
        // Сначала пытаемся скрыть через LobbyManager
        if (lobbyManager != null)
        {
            lobbyManager.HideColorSelectionPanel();
        }
        
        // Также скрываем саму панель напрямую (на случай если LobbyManager не найден или в билде)
        if (gameObject != null)
        {
            gameObject.SetActive(false);
        }
        
        // Если lobbyManager не был найден, пытаемся найти его снова
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.HideColorSelectionPanel();
            }
        }
    }

    void FindLocalPlayerItem()
    {
        if (networkManager == null)
            return;

        // Находим все элементы игроков в лобби
        PlayerLobbyItem[] playerItems = FindObjectsOfType<PlayerLobbyItem>();
        
        foreach (PlayerLobbyItem item in playerItems)
        {
            // В Mirror для клиента connectionId получаем через рефлексию
            uint localClientId = 0;
            if (NetworkClient.connection != null)
            {
                var connectionIdField = NetworkClient.connection.GetType().GetField("connectionId");
                if (connectionIdField != null)
                {
                    localClientId = (uint)(int)connectionIdField.GetValue(NetworkClient.connection);
                }
            }
            if (item.GetClientId() == localClientId)
            {
                localPlayerItem = item;
                break;
            }
        }
    }
}

