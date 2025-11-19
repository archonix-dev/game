using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Представляет игрока в лобби с синхронизацией данных через Mirror
/// </summary>
public class LobbyPlayer : NetworkBehaviour
{
    [Header("Player Info")]
    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "";
    
    [SyncVar(hook = nameof(OnPlayerColorChanged))]
    public int playerColorIndex = 0; // Индекс цвета из массива цветов
    
    [SyncVar(hook = nameof(OnIsOwnerChanged))]
    public bool isOwner = false;
    
    [SyncVar(hook = nameof(OnSteamIDChanged))]
    public ulong steamID = 0;
    
    [SyncVar(hook = nameof(OnPingChanged))]
    public int ping = 0;
    
    private static Dictionary<ulong, LobbyPlayer> playersBySteamID = new Dictionary<ulong, LobbyPlayer>();
    
    // Цвета для выбора игроков
    public static readonly Color[] PlayerColors = new Color[]
    {
        Color.white,      // 0 - Белый
        Color.red,        // 1 - Красный
        new Color(1f, 0.4f, 0.8f), // 2 - Розовый
        new Color(0.6f, 0.2f, 1f), // 3 - Фиолетовый
        Color.blue,       // 4 - Синий
        Color.cyan,       // 5 - Голубой
        Color.green,      // 6 - Зеленый
        new Color(0.5f, 1f, 0.5f)  // 7 - Салатовый
    };

    void CacheCustomizationData()
    {
        if (!isServer)
        {
            return;
        }

        PlayerCustomizationStorage.SaveFromLobbyPlayer(this);
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log($"[LobbyPlayer] OnStartServer вызван для netId: {netId}");
        
        // Получаем Steam ID и имя игрока из подключения
        // Для FizzySteam: address содержит Steam ID клиента в виде строки
        ulong clientSteamID = 0;
        string clientName = "";
        
        try
        {
            if (connectionToClient != null && SteamAPI.IsSteamRunning())
            {
                // В FizzySteam address содержит Steam ID клиента
                string addressStr = connectionToClient.address;
                if (!string.IsNullOrEmpty(addressStr) && ulong.TryParse(addressStr, out clientSteamID))
                {
                    steamID = clientSteamID;
                    
                    // Получаем имя игрока по его Steam ID
                    CSteamID steamIDObj = new CSteamID(clientSteamID);
                    clientName = SteamFriends.GetFriendPersonaName(steamIDObj);
                    
                    // Если имя пустое или это мы сами, используем GetPersonaName
                    if (string.IsNullOrEmpty(clientName) || clientSteamID == SteamUser.GetSteamID().m_SteamID)
                    {
                        clientName = SteamFriends.GetPersonaName();
                    }
                    
                    playerName = clientName;
                    Debug.Log($"[LobbyPlayer] Получен Steam ID клиента из address: {clientSteamID}, имя: {clientName}");
                }
                else
                {
                    // Если не удалось получить Steam ID из address, пробуем для локального подключения
                    if (connectionToClient.connectionId == NetworkConnection.LocalConnectionId)
                    {
                        steamID = SteamUser.GetSteamID().m_SteamID;
                        playerName = SteamFriends.GetPersonaName();
                        Debug.Log($"[LobbyPlayer] Локальное подключение, используем свой Steam ID: {steamID}, имя: {playerName}");
                    }
                    else
                    {
                        // Запасной вариант
                        playerName = "Player " + netId;
                        Debug.LogWarning($"[LobbyPlayer] Не удалось получить Steam ID из address: '{addressStr}', используем запасное имя: {playerName}");
                    }
                }
            }
            else
            {
                playerName = "Player " + netId;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyPlayer] Ошибка получения Steam данных: {e.Message}");
            playerName = "Player " + netId;
        }
        
        // Проверяем, является ли игрок владельцем лобби
        if (connectionToClient != null && connectionToClient.connectionId == NetworkConnection.LocalConnectionId)
        {
            isOwner = true;
        }
        
        Debug.Log($"[LobbyPlayer] Игрок создан на сервере: {playerName} (Steam ID: {steamID}, Owner: {isOwner}, ConnectionId: {connectionToClient?.connectionId})");

        CacheCustomizationData();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log($"[LobbyPlayer] OnStartClient вызван для: {playerName} (isLocalPlayer: {isLocalPlayer})");
        
        if (steamID != 0)
        {
            playersBySteamID[steamID] = this;
        }
        
        // Обновляем UI при подключении с небольшой задержкой
        if (LobbyManager.Instance != null)
        {
            Invoke(nameof(DelayedUpdatePlayerList), 0.2f);
        }
    }
    
    /// <summary>
    /// Обновляет список игроков с задержкой
    /// </summary>
    void DelayedUpdatePlayerList()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        if (steamID != 0 && playersBySteamID.ContainsKey(steamID))
        {
            playersBySteamID.Remove(steamID);
        }
        
        // Обновляем UI при отключении
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    
    /// <summary>
    /// Обновляет пинг игрока (вызывается на сервере)
    /// </summary>
    [Server]
    public void UpdatePing()
    {
        if (isServer && connectionToClient != null)
        {
            // Получаем RTT (Round Trip Time) из Mirror
            ping = (int)(connectionToClient.rtt * 1000); // Конвертируем в миллисекунды
        }
    }
    
    /// <summary>
    /// Устанавливает цвет игрока (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    public void CmdSetPlayerColor(int colorIndex)
    {
        if (colorIndex >= 0 && colorIndex < PlayerColors.Length)
        {
            playerColorIndex = colorIndex;
            Debug.Log($"[LobbyPlayer] Игрок {playerName} выбрал цвет {colorIndex}");

            CacheCustomizationData();
        }
    }
    
    /// <summary>
    /// Получает цвет игрока
    /// </summary>
    public Color GetPlayerColor()
    {
        if (playerColorIndex >= 0 && playerColorIndex < PlayerColors.Length)
        {
            return PlayerColors[playerColorIndex];
        }
        return Color.white;
    }
    
    // Хуки для синхронизации
    private void OnPlayerNameChanged(string oldName, string newName)
    {
        playerName = newName;
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }

        CacheCustomizationData();
    }
    
    private void OnPlayerColorChanged(int oldColor, int newColor)
    {
        playerColorIndex = newColor;
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }

        CacheCustomizationData();
    }
    
    private void OnIsOwnerChanged(bool oldValue, bool newValue)
    {
        isOwner = newValue;
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }

        CacheCustomizationData();
    }
    
    private void OnSteamIDChanged(ulong oldID, ulong newID)
    {
        steamID = newID;
        if (newID != 0)
        {
            playersBySteamID[newID] = this;
        }

        CacheCustomizationData();
    }
    
    private void OnPingChanged(int oldPing, int newPing)
    {
        ping = newPing;
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }

    void Update()
    {
        // Обновляем пинг каждую секунду на сервере
        if (isServer && Time.frameCount % 60 == 0) // Примерно раз в секунду при 60 FPS
        {
            UpdatePing();
        }
    }
    
    public static LobbyPlayer GetPlayerBySteamID(ulong steamID)
    {
        if (playersBySteamID.ContainsKey(steamID))
        {
            return playersBySteamID[steamID];
        }
        return null;
    }

    [TargetRpc]
    public void TargetShowLoadingScreen(NetworkConnection target)
    {
        var controller = LobbyLoadingController.Instance;
        if (controller != null)
        {
            controller.StartClientLoadingSequence();
        }
    }
}

