using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// Структура данных об игроке для синхронизации
/// </summary>
[System.Serializable]
public struct PlayerLobbyData
{
    public uint connectionId;
    public string playerName;
    public Color playerColor;
    public bool isAdmin;
}

/// <summary>
/// NetworkBehaviour компонент для синхронизации списка игроков в лобби
/// Должен быть на том же GameObject что и LobbyManager
/// </summary>
public class LobbyPlayerSync : NetworkBehaviour
{
    private LobbyManager lobbyManager;

    void Start()
    {
        lobbyManager = GetComponent<LobbyManager>();
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
        }
        
        // Убеждаемся, что у GameObject есть NetworkIdentity для работы NetworkBehaviour
        NetworkIdentity netId = GetComponent<NetworkIdentity>();
        if (netId == null)
        {
            netId = gameObject.AddComponent<NetworkIdentity>();
            netId.serverOnly = false; // Должен быть доступен на клиенте
            Debug.Log("[LobbyPlayerSync] Добавлен NetworkIdentity для синхронизации");
        }
        
        // КРИТИЧЕСКИ ВАЖНО: Если мы на сервере и объект еще не заспавнен, заспавним его
        if (NetworkServer.active && netId != null && netId.netId == 0)
        {
            // Ждем немного, чтобы NetworkManager успел инициализироваться
            StartCoroutine(EnsureSpawnedOnServer());
        }
    }
    
    /// <summary>
    /// Убеждается, что объект заспавнен на сервере
    /// </summary>
    System.Collections.IEnumerator EnsureSpawnedOnServer()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (!NetworkServer.active) yield break;
        
        NetworkIdentity netId = GetComponent<NetworkIdentity>();
        if (netId == null || netId.netId != 0) yield break;
        
        // Проверяем, что объект действительно должен быть заспавнен
        // Если это LobbyManager, он должен быть заспавнен через MirrorNetworkManager
        if (lobbyManager != null && lobbyManager.gameObject == gameObject)
        {
            // Пытаемся заспавнить объект
            try
            {
                NetworkServer.Spawn(gameObject);
                Debug.Log("[LobbyPlayerSync] Объект заспавнен на сервере через EnsureSpawnedOnServer");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyPlayerSync] Не удалось заспавнить объект на сервере: {e.Message}");
            }
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[LobbyPlayerSync] LobbyPlayerSync запущен на сервере");
        
        // КРИТИЧЕСКИ ВАЖНО: Если объект еще не заспавнен на сервере, заспавним его
        if (netIdentity != null && netIdentity.netId == 0)
        {
            Debug.Log("[LobbyPlayerSync] Объект еще не заспавнен на сервере, заспавниваем...");
            try
            {
                NetworkServer.Spawn(gameObject);
                Debug.Log($"[LobbyPlayerSync] Объект заспавнен на сервере (netId={netIdentity?.netId ?? 0})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyPlayerSync] Не удалось заспавнить объект на сервере: {e.Message}. Попытка повторного спавна...");
                // Попытка повторного спавна через корутину
                StartCoroutine(RetrySpawnOnServer());
            }
        }
        else if (netIdentity != null && netIdentity.netId != 0)
        {
            Debug.Log($"[LobbyPlayerSync] Объект уже заспавнен на сервере (netId={netIdentity.netId})");
        }
    }
    
    /// <summary>
    /// Повторная попытка заспавнить объект на сервере
    /// </summary>
    System.Collections.IEnumerator RetrySpawnOnServer()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (!NetworkServer.active) yield break;
        
        if (netIdentity != null && netIdentity.netId == 0)
        {
            try
            {
                NetworkServer.Spawn(gameObject);
                Debug.Log($"[LobbyPlayerSync] Объект успешно заспавнен на сервере после повторной попытки (netId={netIdentity?.netId ?? 0})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyPlayerSync] Не удалось заспавнить объект на сервере после повторной попытки: {e.Message}");
            }
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[LobbyPlayerSync] LobbyPlayerSync запущен на клиенте (netId={netIdentity?.netId ?? 0})");
        
        // На клиенте запрашиваем список игроков у сервера, если мы подключились
        if (NetworkClient.active && !NetworkServer.active)
        {
            // Ждем немного, чтобы подключение установилось
            StartCoroutine(RequestPlayersListDelayed());
        }
    }
    
    /// <summary>
    /// Запрашивает список игроков у сервера с задержкой
    /// </summary>
    System.Collections.IEnumerator RequestPlayersListDelayed()
    {
        yield return new WaitForSeconds(1.0f);
        
        // Проверяем, что мы все еще клиент
        if (NetworkClient.active && !NetworkServer.active && lobbyManager != null)
        {
            Debug.Log("[LobbyPlayerSync] Клиент готов к получению списка игроков от сервера");
            
            // Если мы клиент и не получили список игроков, пытаемся запросить его у сервера
            // Но для этого нужен Command, который может вызвать только сервер
            // Поэтому просто ждем, пока сервер отправит список через ClientRpc
            Debug.Log("[LobbyPlayerSync] Ожидание списка игроков от сервера...");
        }
    }

    /// <summary>
    /// Отправляет список всех игроков клиенту при подключении
    /// </summary>
    public void SendPlayersListToClient(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || conn == null || lobbyManager == null) return;

        List<PlayerLobbyData> playersData = new List<PlayerLobbyData>();

        // Собираем данные о всех игроках
        var allPlayersData = lobbyManager.GetAllPlayersData();
        if (allPlayersData != null)
        {
            foreach (var kvp in allPlayersData)
            {
                PlayerLobbyData data = new PlayerLobbyData
                {
                    connectionId = kvp.Key,
                    playerName = kvp.Value.playerName,
                    playerColor = kvp.Value.playerColor,
                    isAdmin = kvp.Value.isAdmin
                };
                playersData.Add(data);
            }
        }

        Debug.Log($"[LobbyPlayerSync] Отправка списка игроков клиенту {conn.connectionId}. Игроков: {playersData.Count}");
        if (playersData.Count > 0)
        {
            // Для хоста (connectionId = 0) используем ClientRpc, для остальных - TargetRpc
            if (conn.connectionId == 0)
            {
                // Хост - используем ClientRpc для всех (включая хост)
                RpcReceivePlayersListForAll(playersData.ToArray());
            }
            else
            {
                // Обычный клиент - используем TargetRpc
                TargetReceivePlayersList(conn, playersData.ToArray());
            }
        }
    }

    /// <summary>
    /// TargetRpc для получения списка игроков от сервера (для конкретного клиента)
    /// </summary>
    [TargetRpc]
    void TargetReceivePlayersList(NetworkConnection conn, PlayerLobbyData[] playersData)
    {
        // Получаем connectionId через рефлексию, так как NetworkConnection не имеет прямого свойства connectionId
        int connectionId = -1;
        if (conn != null)
        {
            var connectionIdField = conn.GetType().GetField("connectionId");
            if (connectionIdField != null)
            {
                connectionId = (int)connectionIdField.GetValue(conn);
            }
        }
        
        Debug.Log($"[LobbyPlayerSync] TargetReceivePlayersList получен для connectionId={connectionId}. Игроков: {playersData?.Length ?? 0}");
        ReceivePlayersList(playersData);
    }
    
    /// <summary>
    /// ClientRpc для получения списка игроков от сервера (для всех, включая хост)
    /// </summary>
    [ClientRpc]
    void RpcReceivePlayersListForAll(PlayerLobbyData[] playersData)
    {
        Debug.Log($"[LobbyPlayerSync] RpcReceivePlayersListForAll получен. Игроков: {playersData?.Length ?? 0}");
        ReceivePlayersList(playersData);
    }
    
    /// <summary>
    /// Общий метод для обработки полученного списка игроков
    /// </summary>
    void ReceivePlayersList(PlayerLobbyData[] playersData)
    {
        Debug.Log($"[LobbyPlayerSync] ReceivePlayersList вызван. Игроков: {playersData?.Length ?? 0}, NetworkClient.active={NetworkClient.active}, NetworkServer.active={NetworkServer.active}");
        
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager == null)
            {
                Debug.LogError("[LobbyPlayerSync] LobbyManager не найден!");
                return;
            }
        }

        if (playersData == null || playersData.Length == 0)
        {
            Debug.LogWarning("[LobbyPlayerSync] Получен пустой список игроков!");
            return;
        }

        Debug.Log($"[LobbyPlayerSync] Получен список игроков от сервера. Игроков: {playersData.Length}");

        // Создаем PlayerLobbyItem для каждого игрока
        foreach (var playerData in playersData)
        {
            Debug.Log($"[LobbyPlayerSync] Обработка игрока: connectionId={playerData.connectionId}, name={playerData.playerName}, isAdmin={playerData.isAdmin}");
            
            // Пропускаем, если PlayerLobbyItem уже существует (чтобы избежать дублирования)
            if (!lobbyManager.playerLobbyItems.ContainsKey(playerData.connectionId))
            {
                Debug.Log($"[LobbyPlayerSync] Создание нового PlayerLobbyItem для connectionId={playerData.connectionId}");
                lobbyManager.CreatePlayerLobbyItemLocally(
                    playerData.connectionId,
                    playerData.isAdmin,
                    playerData.playerName,
                    playerData.playerColor
                );
            }
            else
            {
                Debug.Log($"[LobbyPlayerSync] Обновление существующего PlayerLobbyItem для connectionId={playerData.connectionId}");
                // Обновляем существующий
                lobbyManager.UpdatePlayerDataAndSync(
                    playerData.connectionId,
                    playerData.isAdmin,
                    playerData.playerName,
                    playerData.playerColor
                );
            }
        }

        Debug.Log($"[LobbyPlayerSync] ✓ Синхронизация списка игроков завершена. Создано/обновлено PlayerLobbyItem: {playersData.Length}");
    }

    /// <summary>
    /// Отправляет обновление данных одного игрока всем клиентам
    /// </summary>
    public void BroadcastPlayerUpdate(uint connectionId, string playerName, Color playerColor, bool isAdmin)
    {
        if (!NetworkServer.active) return;

        PlayerLobbyData data = new PlayerLobbyData
        {
            connectionId = connectionId,
            playerName = playerName,
            playerColor = playerColor,
            isAdmin = isAdmin
        };

        RpcUpdatePlayerData(data);
    }

    /// <summary>
    /// ClientRpc для обновления данных игрока
    /// </summary>
    [ClientRpc]
    void RpcUpdatePlayerData(PlayerLobbyData playerData)
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager == null) return;
        }

        lobbyManager.UpdatePlayerDataAndSync(
            playerData.connectionId,
            playerData.isAdmin,
            playerData.playerName,
            playerData.playerColor
        );
    }

    /// <summary>
    /// Отправляет уведомление об удалении игрока всем клиентам
    /// </summary>
    public void BroadcastPlayerRemoved(uint connectionId)
    {
        if (!NetworkServer.active) return;

        RpcRemovePlayer(connectionId);
    }

    /// <summary>
    /// ClientRpc для удаления игрока из списка
    /// </summary>
    [ClientRpc]
    void RpcRemovePlayer(uint connectionId)
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager == null) return;
        }

        if (lobbyManager.playerLobbyItems.ContainsKey(connectionId))
        {
            GameObject playerItem = lobbyManager.playerLobbyItems[connectionId];
            if (playerItem != null)
            {
                Destroy(playerItem);
            }
            lobbyManager.playerLobbyItems.Remove(connectionId);
        }
    }
}

