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
    
    [Tooltip("Массив Image для отображения выбранного цвета игрока")]
    public Image[] colorImages;
    
    private LobbyPlayer player;
    
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

