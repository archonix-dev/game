using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;

/// <summary>
/// UI элемент для отображения лобби в списке
/// </summary>
public class LobbyListUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("Image который следует за курсором")]
    public Image cursorFollowImage;
    
    [Tooltip("Text для отображения имени создателя лобби")]
    public Text playerNameText;
    
    [Tooltip("Text для отображения количества игроков")]
    public Text playerCountText;
    
    [Tooltip("InputField для ввода пароля")]
    public InputField passwordInput;
    
    [Tooltip("Кнопка 'Войти'")]
    public Button joinButton;
    
    private CSteamID lobbyID;
    private RectTransform rectTransform;
    private Camera uiCamera;
    private LobbyManager lobbyManager;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        lobbyManager = LobbyManager.Instance;
        
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
        
        // Настраиваем кнопку входа
        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinClicked);
        }
    }
    
    void Update()
    {
        // Обновляем позицию изображения курсора
        if (cursorFollowImage != null && rectTransform != null)
        {
            UpdateCursorFollowImage();
        }
        
        // Обновляем информацию о лобби
        if (lobbyID.IsValid() && Time.frameCount % 60 == 0)
        {
            UpdateLobbyInfo();
        }
    }
    
    /// <summary>
    /// Настраивает UI для лобби
    /// </summary>
    public void SetupLobby(CSteamID steamLobbyID)
    {
        lobbyID = steamLobbyID;
        UpdateLobbyInfo();
    }
    
    void UpdateLobbyInfo()
    {
        try
        {
            if (!lobbyID.IsValid() || !SteamAPI.IsSteamRunning()) return;
        }
        catch
        {
            return;
        }
        
        // Получаем имя владельца лобби через Steam
        string ownerName = "";
        try
        {
            CSteamID ownerID = SteamMatchmaking.GetLobbyOwner(lobbyID);
            if (ownerID.IsValid())
            {
                ownerName = SteamFriends.GetFriendPersonaName(ownerID);
            }
        }
        catch
        {
            // Если не удалось получить имя владельца, используем данные лобби
            ownerName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        }
        
        // Если имя пустое, используем данные лобби как запасной вариант
        if (string.IsNullOrEmpty(ownerName))
        {
            ownerName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
        }
        
        if (playerNameText != null)
        {
            playerNameText.text = ownerName;
        }
        
        // Получаем количество игроков
        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
        string maxPlayersStr = SteamMatchmaking.GetLobbyData(lobbyID, "maxPlayers");
        int maxPlayers = 4;
        if (!string.IsNullOrEmpty(maxPlayersStr))
        {
            int.TryParse(maxPlayersStr, out maxPlayers);
        }
        
        if (playerCountText != null)
        {
            playerCountText.text = $"{currentPlayers} / {maxPlayers}";
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
    
    void OnJoinClicked()
    {
        if (!lobbyID.IsValid() || lobbyManager == null) return;
        
        string password = "";
        if (passwordInput != null)
        {
            password = passwordInput.text;
        }
        
        lobbyManager.JoinLobby(lobbyID, password);
        
        // Закрываем панель присоединения к лобби
        if (lobbyManager != null)
        {
            // Находим LobbyMenuUI и закрываем панель
            LobbyMenuUI menuUI = FindObjectOfType<LobbyMenuUI>();
            if (menuUI != null && menuUI.joinLobbyPanel != null)
            {
                menuUI.joinLobbyPanel.SetActive(false);
            }
        }
    }
}

