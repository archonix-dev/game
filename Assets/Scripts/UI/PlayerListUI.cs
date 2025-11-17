using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// UI элемент для отображения игрока в списке лобби
/// </summary>
public class PlayerListUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Image для отображения, что игрок - создатель лобби")]
    public Image isOwnerImage;
    
    [Tooltip("Text для отображения имени игрока")]
    public Text playerNameText;
    
    [Tooltip("Text для отображения пинга")]
    public Text pingText;
    
    [Tooltip("Image который следует за курсором")]
    public Image cursorFollowImage;
    
    [Tooltip("Массив Image для отображения выбранного цвета игрока")]
    public Image[] colorImages;
    
    private LobbyPlayer player;
    private RectTransform rectTransform;
    private Camera uiCamera;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Находим камеру UI
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            uiCamera = canvas.worldCamera;
        }
        
        // Скрываем изображение курсора по умолчанию
        if (cursorFollowImage != null)
        {
            cursorFollowImage.gameObject.SetActive(false);
        }
    }
    
    void Update()
    {
        // Обновляем позицию изображения курсора
        if (cursorFollowImage != null && rectTransform != null)
        {
            UpdateCursorFollowImage();
        }
    }
    
    /// <summary>
    /// Настраивает UI для игрока
    /// </summary>
    public void SetupPlayer(LobbyPlayer lobbyPlayer)
    {
        player = lobbyPlayer;
        
        if (player == null) return;
        
        // Обновляем имя
        if (playerNameText != null)
        {
            playerNameText.text = player.playerName;
        }
        
        // Обновляем статус владельца
        if (isOwnerImage != null)
        {
            isOwnerImage.gameObject.SetActive(player.isOwner);
        }
        
        // Обновляем пинг
        UpdatePing();
        
        // Обновляем цвет
        UpdateColor();
    }
    
    void UpdatePing()
    {
        if (pingText == null || player == null) return;
        
        int ping = player.isOwner ? 0 : player.ping;
        pingText.text = ping.ToString() + " ms";
        
        // Меняем цвет в зависимости от пинга (0-50 белый, 200+ красный)
        float pingNormalized = Mathf.Clamp01(ping / 200f);
        Color pingColor = Color.Lerp(Color.white, Color.red, pingNormalized);
        pingText.color = pingColor;
    }
    
    void UpdateColor()
    {
        if (player == null || colorImages == null) return;
        
        Color playerColor = player.GetPlayerColor();
        
        foreach (Image colorImage in colorImages)
        {
            if (colorImage != null)
            {
                colorImage.color = playerColor;
            }
        }
    }
    
    void UpdateCursorFollowImage()
    {
        if (cursorFollowImage == null || rectTransform == null) return;
        
        // Проверяем, находится ли курсор над элементом
        Vector2 mousePosition = Input.mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, mousePosition, uiCamera, out Vector2 localPoint);
        
        Rect rect = rectTransform.rect;
        bool isHovered = rect.Contains(localPoint);
        
        if (isHovered)
        {
            cursorFollowImage.gameObject.SetActive(true);
            
            // Определяем позицию курсора относительно элемента
            Vector2 normalizedPoint = new Vector2(
                (localPoint.x + rect.width / 2) / rect.width,
                (localPoint.y + rect.height / 2) / rect.height
            );
            
            // Определяем, с какой стороны находится курсор
            Vector2 offset = Vector2.zero;
            
            if (normalizedPoint.y < 0.25f) // Снизу
            {
                offset = new Vector2(-25.9f, -21.9f);
            }
            else if (normalizedPoint.x < 0.25f) // Слева
            {
                offset = new Vector2(-40f, 3.100023f);
            }
            else if (normalizedPoint.x > 0.75f) // Справа
            {
                offset = new Vector2(-14f, 3.100023f);
            }
            else if (normalizedPoint.y > 0.75f) // Сверху
            {
                offset = new Vector2(-25.9f, 22.1f);
            }
            
            // Плавно перемещаем изображение
            RectTransform cursorRect = cursorFollowImage.GetComponent<RectTransform>();
            if (cursorRect != null)
            {
                Vector2 targetPosition = offset;
                cursorRect.anchoredPosition = Vector2.Lerp(cursorRect.anchoredPosition, targetPosition, Time.deltaTime * 10f);
            }
        }
        else
        {
            cursorFollowImage.gameObject.SetActive(false);
        }
    }
    
    void LateUpdate()
    {
        // Обновляем пинг и цвет периодически
        if (player != null && Time.frameCount % 30 == 0)
        {
            UpdatePing();
            UpdateColor();
        }
    }
}

