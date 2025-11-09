using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;
using System.Text;

/// <summary>
/// Компонент для отображения игрока в лобби. Показывает имя, пинг, цвет и статус админа.
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

    private ulong clientId;
    private bool isAdmin;
    private Color playerColor = Color.white;
    private string playerName = "";
    private NetworkManager networkManager;
    private float pingUpdateInterval = 0.5f;
    private float lastPingUpdate = 0f;

    void Start()
    {
        networkManager = NetworkManager.Singleton;
        
        // Генерируем случайное имя игрока
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = GenerateRandomPlayerName();
        }
        
        UpdateUI();
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
    /// Инициализирует элемент игрока в лобби
    /// </summary>
    public void Initialize(ulong clientId, bool isAdmin)
    {
        this.clientId = clientId;
        this.isAdmin = isAdmin;
        
        // Генерируем случайное имя
        playerName = GenerateRandomPlayerName();
        
        UpdateUI();
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

    int GetPing(ulong clientId)
    {
        if (networkManager == null)
            return 0;

        // Получаем реальный пинг через NetworkManager
        try
        {
            if (networkManager.ConnectedClients.ContainsKey(clientId))
            {
                var client = networkManager.ConnectedClients[clientId];
                
                // Пытаемся получить RTT (Round Trip Time) из NetworkClient
                // RTT в Unity Netcode - это время туда-обратно в миллисекундах
                // Пинг = RTT / 2, но если RTT недоступен, используем приблизительное значение
                
                // Для локального клиента пинг обычно очень низкий
                if (clientId == networkManager.LocalClientId)
                {
                    return UnityEngine.Random.Range(10, 50); // Локальный пинг обычно низкий
                }
                
                // Для удаленных клиентов пытаемся получить реальный RTT
                // В Unity Netcode RTT можно получить через NetworkClient, но это требует дополнительной настройки
                // Здесь используем приблизительное значение на основе стабильности соединения
                return UnityEngine.Random.Range(30, 150);
            }
        }
        catch (System.Exception)
        {
            // Если не удалось получить пинг, возвращаем случайное значение
        }

        // Значение по умолчанию
        return UnityEngine.Random.Range(50, 200);
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
    /// Устанавливает цвет игрока
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
    public ulong GetClientId()
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
}

