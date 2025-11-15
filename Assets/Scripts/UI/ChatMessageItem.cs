using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Компонент для отображения сообщения в чате. Показывает имя игрока, цвет и текст сообщения.
/// Локальный UI элемент (не NetworkObject), создается каждым клиентом отдельно.
/// </summary>
public class ChatMessageItem : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Текст с именем игрока")]
    public Text playerNameText;
    
    [Tooltip("Текст с сообщением игрока")]
    public Text messageText;
    
    [Tooltip("Массив Image компонентов для отображения цвета игрока (image_color_player)")]
    public Image[] colorPlayerImages;
    
    [Tooltip("GameObject для отображения статуса админа")]
    public GameObject adminIndicator;

    private ulong clientId;
    private bool isAdmin;
    private Color playerColor = Color.white;
    private string playerName = "";
    private string message = "";
    private bool isInitialized = false;

    /// <summary>
    /// Инициализирует элемент сообщения чата (вызывается локально на каждом клиенте)
    /// </summary>
    public void Initialize(string message, string playerName, Color playerColor, ulong clientId, bool isAdmin = false)
    {
        this.clientId = clientId;
        this.message = message;
        this.playerName = playerName;
        this.playerColor = playerColor;
        this.isAdmin = isAdmin;
        
        isInitialized = true;
        UpdateUI();
    }

    void UpdateUI()
    {
        // Обновляем имя игрока
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        // Обновляем текст сообщения
        if (messageText != null)
        {
            messageText.text = message;
        }

        // Обновляем цвет игрока
        UpdatePlayerColor();

        // Показываем/скрываем индикатор админа
        if (adminIndicator != null)
        {
            adminIndicator.SetActive(isAdmin);
        }
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

