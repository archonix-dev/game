using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

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
        networkManager = NetworkManager.Singleton;
        
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
        Debug.Log($"Выбран цвет: {color}");
        
        // Сохраняем выбранный цвет для применения в другой сцене
        PlayerPrefs.SetFloat("PlayerColor_R", color.r);
        PlayerPrefs.SetFloat("PlayerColor_G", color.g);
        PlayerPrefs.SetFloat("PlayerColor_B", color.b);
        PlayerPrefs.SetFloat("PlayerColor_A", color.a);
        PlayerPrefs.Save();
        
        // Находим локального игрока в лобби
        FindLocalPlayerItem();
        
        // Применяем цвет к локальному игроку
        if (localPlayerItem != null)
        {
            localPlayerItem.SetPlayerColor(color);
            Debug.Log($"Цвет игрока изменен на: {color} (локально)");
        }
        else
        {
            Debug.LogWarning("Локальный игрок не найден в лобби! Попытка найти через корутину...");
            // Пытаемся найти игрока через корутину (на случай, если он еще не создан)
            StartCoroutine(FindAndSetColorDelayed(color));
        }
        
        // Синхронизируем цвет через сеть (отправляем всем клиентам через LobbyNetworkManager)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            LobbyNetworkManager lobbyNetManager = FindObjectOfType<LobbyNetworkManager>();
            if (lobbyNetManager != null && lobbyNetManager.IsSpawned)
            {
                ulong localClientId = NetworkManager.Singleton.LocalClientId;
                // Отправляем обновление цвета всем клиентам
                lobbyNetManager.BroadcastPlayerColorUpdate(localClientId, color);
            }
        }

        // Скрываем панель выбора цвета в любом случае
        HidePanel();
    }

    System.Collections.IEnumerator FindAndSetColorDelayed(Color color)
    {
        // Ждем до 2 секунд, пока PlayerLobbyItem не синхронизируется
        float timeout = 2f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            FindLocalPlayerItem();
            
            if (localPlayerItem != null)
            {
                localPlayerItem.SetPlayerColor(color);
                Debug.Log($"Цвет игрока изменен на: {color} (найден после задержки)");
                yield break;
            }
            
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.LogWarning("Локальный игрок не найден после ожидания! Цвет будет применен при создании игрока.");
    }

    void HidePanel()
    {
        // Скрываем панель через LobbyManager, если он доступен
        if (lobbyManager != null)
        {
            lobbyManager.HideColorSelectionPanel();
        }
        else
        {
            // Если LobbyManager не найден, скрываем напрямую
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                Debug.Log("Панель выбора цвета скрыта.");
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
            if (item.GetClientId() == networkManager.LocalClientId)
            {
                localPlayerItem = item;
                break;
            }
        }
    }
}

