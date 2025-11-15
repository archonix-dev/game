using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// NetworkBehaviour для синхронизации данных лобби между клиентами и сервером.
/// </summary>
public class LobbyNetworkManager : NetworkBehaviour
{
    [Header("Настройки лобби")]
    [Tooltip("Максимальное количество игроков")]
    public NetworkVariable<int> maxPlayers = new NetworkVariable<int>(
        8, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Tooltip("Пароль лобби")]
    public NetworkVariable<FixedString32Bytes> lobbyPassword = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Tooltip("Включены ли читы")]
    public NetworkVariable<bool> cheatsEnabled = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static LobbyNetworkManager instance;
    public static LobbyNetworkManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyNetworkManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Подписываемся на изменения сетевых переменных
        maxPlayers.OnValueChanged += OnMaxPlayersChanged;
        lobbyPassword.OnValueChanged += OnLobbyPasswordChanged;
        cheatsEnabled.OnValueChanged += OnCheatsEnabledChanged;
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        maxPlayers.OnValueChanged -= OnMaxPlayersChanged;
        lobbyPassword.OnValueChanged -= OnLobbyPasswordChanged;
        cheatsEnabled.OnValueChanged -= OnCheatsEnabledChanged;
        
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Инициализирует настройки лобби (вызывается только на сервере)
    /// </summary>
    public void InitializeLobby(int maxPlayersValue, string password)
    {
        if (!IsServer)
        {
            Debug.LogWarning("InitializeLobby может быть вызван только на сервере!");
            return;
        }

        maxPlayers.Value = maxPlayersValue;
        lobbyPassword.Value = new FixedString32Bytes(password);
        cheatsEnabled.Value = false;
    }

    /// <summary>
    /// Устанавливает настройки лобби (вызывается только на сервере)
    /// </summary>
    public void SetLobbySettings(int maxPlayersValue, bool cheatsEnabledValue, string password)
    {
        if (!IsServer)
        {
            Debug.LogWarning("SetLobbySettings может быть вызван только на сервере!");
            return;
        }

        maxPlayers.Value = maxPlayersValue;
        cheatsEnabled.Value = cheatsEnabledValue;
        lobbyPassword.Value = new FixedString32Bytes(password);
        
        Debug.Log($"Настройки лобби обновлены: Макс игроков={maxPlayersValue}, Читы={cheatsEnabledValue}, Пароль={password}");
    }

    void OnMaxPlayersChanged(int oldValue, int newValue)
    {
        Debug.Log($"Максимальное количество игроков изменено: {oldValue} -> {newValue}");
    }

    void OnLobbyPasswordChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        Debug.Log($"Пароль лобби изменен: {oldValue} -> {newValue}");
    }

    void OnCheatsEnabledChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"Читы {(newValue ? "включены" : "выключены")}");
    }

    /// <summary>
    /// Проверяет пароль лобби
    /// </summary>
    public bool CheckPassword(string password)
    {
        if (string.IsNullOrEmpty(lobbyPassword.Value.ToString()))
        {
            return true; // Если пароль не установлен, доступ открыт
        }
        
        return lobbyPassword.Value.ToString() == password;
    }

    /// <summary>
    /// Получает максимальное количество игроков
    /// </summary>
    public int GetMaxPlayers()
    {
        return maxPlayers.Value;
    }

    /// <summary>
    /// Получает пароль лобби
    /// </summary>
    public string GetLobbyPassword()
    {
        return lobbyPassword.Value.ToString();
    }

    /// <summary>
    /// Проверяет, включены ли читы
    /// </summary>
    public bool AreCheatsEnabled()
    {
        return cheatsEnabled.Value;
    }
    
    /// <summary>
    /// Отправляет данные игрока всем клиентам для создания PlayerLobbyItem
    /// </summary>
    public void BroadcastPlayerLobbyItem(ulong clientId, bool isAdmin, string playerName, Color playerColor)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[LobbyNetworkManager] BroadcastPlayerLobbyItem может быть вызван только на сервере!");
            return;
        }
        
        // Отправляем всем клиентам через ClientRpc (включая сервер, если это хост)
        ClientRpcParams clientRpcParams = default;
        var targetClientIds = new List<ulong>();
        
        // Добавляем всех подключенных клиентов
        if (NetworkManager.Singleton != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                targetClientIds.Add(client.Key);
            }
        }
        
        clientRpcParams.Send.TargetClientIds = targetClientIds.ToArray();
        
        CreatePlayerLobbyItemClientRpc(clientId, isAdmin, new FixedString64Bytes(playerName), playerColor, clientRpcParams);
    }
    
    /// <summary>
    /// ClientRpc для создания PlayerLobbyItem на всех клиентах
    /// </summary>
    [ClientRpc]
    private void CreatePlayerLobbyItemClientRpc(ulong clientId, bool isAdmin, FixedString64Bytes playerName, Color playerColor, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[LobbyNetworkManager] CreatePlayerLobbyItemClientRpc получен: clientId={clientId}, isAdmin={isAdmin}, playerName={playerName}");
        
        // Находим LobbyManager и создаем локальный UI элемент
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.CreatePlayerLobbyItemLocally(clientId, isAdmin, playerName.ToString(), playerColor);
        }
        else
        {
            Debug.LogError("[LobbyNetworkManager] LobbyManager не найден при получении CreatePlayerLobbyItemClientRpc!");
        }
    }
    
    /// <summary>
    /// Отправляет обновление цвета игрока всем клиентам
    /// </summary>
    public void BroadcastPlayerColorUpdate(ulong clientId, Color playerColor)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[LobbyNetworkManager] BroadcastPlayerColorUpdate может быть вызван только на сервере!");
            return;
        }
        
        // Отправляем всем клиентам через ClientRpc
        UpdatePlayerColorClientRpc(clientId, playerColor);
    }
    
    /// <summary>
    /// ClientRpc для обновления цвета игрока на всех клиентах
    /// </summary>
    [ClientRpc]
    private void UpdatePlayerColorClientRpc(ulong clientId, Color playerColor, ClientRpcParams rpcParams = default)
    {
        // Находим LobbyManager и обновляем цвет локально
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.UpdatePlayerColorLocally(clientId, playerColor);
        }
    }
    
    /// <summary>
    /// ServerRpc для запроса списка всех игроков (вызывается клиентом)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestAllPlayersServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[LobbyNetworkManager] Клиент {requesterId} запросил список всех игроков");
        
        // Находим LobbyManager и отправляем данные всех игроков запросившему клиенту
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null && NetworkManager.Singleton != null)
        {
            // Получаем данные всех игроков из LobbyManager
            var playersData = lobbyManager.GetAllPlayersData();
            
            foreach (var playerData in playersData)
            {
                ulong playerClientId = playerData.Key;
                bool isAdmin = playerData.Value.isAdmin;
                string playerName = playerData.Value.playerName;
                Color playerColor = playerData.Value.playerColor;
                
                // Отправляем данные игрока запросившему клиенту
                SendPlayerLobbyItemToClient(requesterId, playerClientId, isAdmin, playerName, playerColor);
            }
        }
    }
    
    /// <summary>
    /// Отправляет данные игрока конкретному клиенту (для синхронизации при подключении)
    /// </summary>
    public void SendPlayerLobbyItemToClient(ulong targetClientId, ulong playerClientId, bool isAdmin, string playerName, Color playerColor)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[LobbyNetworkManager] SendPlayerLobbyItemToClient может быть вызван только на сервере!");
            return;
        }
        
        // Отправляем конкретному клиенту через ClientRpc
        ClientRpcParams clientRpcParams = default;
        clientRpcParams.Send.TargetClientIds = new ulong[] { targetClientId };
        
        CreatePlayerLobbyItemClientRpc(playerClientId, isAdmin, new FixedString64Bytes(playerName), playerColor, clientRpcParams);
    }
    
    /// <summary>
    /// ServerRpc для проверки пароля при подключении клиента
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CheckPasswordServerRpc(FixedString32Bytes password, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string passwordString = password.ToString();
        
        // Проверяем, установлен ли пароль на сервере
        string serverPassword = lobbyPassword.Value.ToString();
        bool isValid = false;
        
        // Если пароль не установлен на сервере, доступ открыт
        if (string.IsNullOrEmpty(serverPassword))
        {
            isValid = true;
            Debug.Log($"Клиент {clientId} подключен: пароль не установлен на сервере");
        }
        else
        {
            // Проверяем пароль
            isValid = CheckPassword(passwordString);
            
            if (!isValid)
            {
                // Отключаем клиента с неверным паролем
                Debug.LogWarning($"[LobbyNetworkManager] Клиент {clientId} отключен: неверный пароль (ожидался: '{serverPassword}', получен: '{passwordString}')");
                
                // Используем корутину для отключения, чтобы дать время ClientRpc отправиться
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(DisconnectClientAfterDelay(clientId));
                }
                else
                {
                    NetworkManager.Singleton.DisconnectClient(clientId);
                }
            }
            else
            {
                Debug.Log($"[LobbyNetworkManager] Клиент {clientId} успешно подключен с правильным паролем");
            }
        }
        
        // Отправляем результат клиенту
        CheckPasswordResultClientRpc(isValid, clientId);
    }
    
    System.Collections.IEnumerator DisconnectClientAfterDelay(ulong clientId)
    {
        // Небольшая задержка, чтобы ClientRpc успел отправиться
        yield return new WaitForSeconds(0.1f);
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
        }
    }
    
    /// <summary>
    /// ClientRpc для уведомления клиента о результате проверки пароля
    /// </summary>
    [ClientRpc]
    void CheckPasswordResultClientRpc(bool isValid, ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (!isValid)
            {
                Debug.LogError("[LobbyNetworkManager] Неверный пароль! Подключение отклонено.");
                
                // Уведомляем ConnectToLobbyPanel об ошибке пароля, если он существует
                ConnectToLobbyPanel connectPanel = FindObjectOfType<ConnectToLobbyPanel>();
                if (connectPanel != null)
                {
                    connectPanel.OnPasswordError();
                }
            }
            else
            {
                Debug.Log("[LobbyNetworkManager] Пароль подтвержден! Подключение успешно.");
            }
        }
    }
}

