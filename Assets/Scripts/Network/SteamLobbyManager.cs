using System;
using System.Collections;
using UnityEngine;
using Steamworks;
using Mirror;

/// <summary>
/// Управляет Steam лобби: создание, приглашения, взаимодействие с Steam оверлеем
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    [Header("Steam Lobby Settings")]
    [Tooltip("Тип лобби Steam")]
    public ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
    
    [Header("Input Settings")]
    [Tooltip("Клавиша для открытия Steam оверлея приглашений")]
    public KeyCode inviteKey = KeyCode.I;
    
    private CSteamID currentLobbyID;
    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private Callback<LobbyEnter_t> lobbyEnterCallback;

    public static SteamLobbyManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Инициализация колбэков Steamworks
        if (SteamAPI.IsSteamRunning())
        {
            lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            
            Debug.Log("[SteamLobbyManager] Steam callbacks инициализированы");
        }
        else
        {
            Debug.LogWarning("[SteamLobbyManager] Steam не запущен!");
        }
    }

    private void Update()
    {
        // Обработка нажатия клавиши для открытия Steam оверлея приглашений
        if (Input.GetKeyDown(inviteKey))
        {
            InviteFriendToLobby();
        }
        
        // Обработка нажатия ESC для выхода из лобби
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Проверяем, находимся ли мы в активном лобби
            if (IsInActiveLobby())
            {
                Debug.Log("[SteamLobbyManager] Нажата клавиша ESC - выход из лобби");
                LeaveLobbyCompletely();
            }
        }
    }

    /// <summary>
    /// Создание Steam лобби
    /// </summary>
    public void CreateSteamLobby()
    {
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogError("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }

        SteamMatchmaking.CreateLobby(lobbyType, NetworkManager.singleton.maxConnections);
        Debug.Log("[SteamLobbyManager] Создание Steam лобби...");
    }

    /// <summary>
    /// Колбэк создания лобби
    /// </summary>
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobbyManager] Ошибка создания лобби: {callback.m_eResult}");
            return;
        }

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        
        // Устанавливаем данные лобби
        SteamMatchmaking.SetLobbyData(currentLobbyID, "name", 
            $"{SteamFriends.GetPersonaName()}'s Lobby");
        SteamMatchmaking.SetLobbyData(currentLobbyID, "game", Application.productName);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "version", Application.version);

        // Запускаем хост Mirror
        NetworkManager.singleton.StartHost();
        
        Debug.Log($"[SteamLobbyManager] Лобби создано! ID: {currentLobbyID}");
    }

    /// <summary>
    /// Колбэк запроса на присоединение к лобби (через оверлей Steam)
    /// </summary>
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("[SteamLobbyManager] Получен запрос на присоединение к лобби через Steam оверлей");
        
        // Присоединяемся к лобби
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// Колбэк входа в лобби
    /// </summary>
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        
        // Если мы не хост, присоединяемся как клиент
        if (!NetworkServer.active)
        {
            string hostAddress = GetLobbyHostAddress();
            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }
        
        Debug.Log($"[SteamLobbyManager] Вошли в лобби: {currentLobbyID}");
    }

    /// <summary>
    /// Получение адреса хоста лобби
    /// </summary>
    private string GetLobbyHostAddress()
    {
        CSteamID hostID = SteamMatchmaking.GetLobbyOwner(currentLobbyID);
        return hostID.m_SteamID.ToString();
    }

    /// <summary>
    /// Отправка приглашения другу через Steam оверлей
    /// Вызывается при нажатии клавиши I
    /// </summary>
    public void InviteFriendToLobby()
    {
        // Проверяем, что Steam запущен
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogWarning("[SteamLobbyManager] Steam не запущен! Невозможно открыть оверлей.");
            return;
        }

        // Проверяем, есть ли активное лобби
        // Пытаемся получить лобби из LobbyManager
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            currentLobbyID = LobbyManager.Instance.CurrentLobbyID;
        }

        if (currentLobbyID.IsValid())
        {
            // Открываем диалог приглашения Steam
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyID);
            Debug.Log($"[SteamLobbyManager] Открытие диалога приглашения через Steam оверлей для лобби {currentLobbyID}");
        }
        else
        {
            Debug.LogWarning("[SteamLobbyManager] Нет активного лобби для отправки приглашения. Сначала создайте или присоединитесь к лобби.");
        }
    }

    /// <summary>
    /// Альтернативный метод приглашения конкретного друга
    /// </summary>
    /// <param name="friendID">Steam ID друга</param>
    public void InviteSpecificFriend(CSteamID friendID)
    {
        // Получаем активное лобби из LobbyManager
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            currentLobbyID = LobbyManager.Instance.CurrentLobbyID;
        }

        if (currentLobbyID.IsValid())
        {
            SteamMatchmaking.InviteUserToLobby(currentLobbyID, friendID);
            Debug.Log($"[SteamLobbyManager] Приглашение отправлено другу: {friendID}");
        }
        else
        {
            Debug.LogWarning("[SteamLobbyManager] Нет активного лобби для отправки приглашения");
        }
    }

    /// <summary>
    /// Получение списка игроков в лобби
    /// </summary>
    public int GetLobbyPlayerCount()
    {
        // Получаем активное лобби из LobbyManager
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            currentLobbyID = LobbyManager.Instance.CurrentLobbyID;
        }

        if (currentLobbyID.IsValid())
        {
            return SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
        }
        return 0;
    }

    /// <summary>
    /// Выход из лобби (только Steam)
    /// </summary>
    public void LeaveLobby()
    {
        if (currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
    }
    
    /// <summary>
    /// Полный выход из лобби (Steam + Network)
    /// </summary>
    public void LeaveLobbyCompletely()
    {
        // Выходим из Steam лобби
        LeaveLobby();
        
        // Выходим из сетевого подключения через LobbyManager
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LeaveLobby();
        }
    }
    
    /// <summary>
    /// Проверяет, находимся ли мы в активном лобби
    /// </summary>
    private bool IsInActiveLobby()
    {
        // Проверяем через LobbyManager
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            return true;
        }
        
        // Проверяем через текущий ID
        if (currentLobbyID.IsValid())
        {
            return true;
        }
        
        // Проверяем через сетевые подключения
        if (NetworkClient.active || NetworkServer.active)
        {
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Устанавливает текущее активное лобби (вызывается из LobbyManager)
    /// </summary>
    /// <param name="lobbyID">ID активного лобби</param>
    public void SetCurrentLobby(CSteamID lobbyID)
    {
        currentLobbyID = lobbyID;
        Debug.Log($"[SteamLobbyManager] Установлено текущее лобби: {currentLobbyID}");
    }

    /// <summary>
    /// Получает текущее активное лобби
    /// </summary>
    public CSteamID GetCurrentLobby()
    {
        return currentLobbyID;
    }

    /// <summary>
    /// Вызывается при выходе из приложения (Alt+F4, закрытие окна и т.д.)
    /// </summary>
    private void OnApplicationQuit()
    {
        Debug.Log("[SteamLobbyManager] OnApplicationQuit вызван - удаление активного лобби");
        
        // Проверяем, есть ли активное лобби
        if (IsInActiveLobby())
        {
            // Пытаемся выйти из лобби перед закрытием
            try
            {
                if (currentLobbyID.IsValid())
                {
                    SteamMatchmaking.LeaveLobby(currentLobbyID);
                    Debug.Log($"[SteamLobbyManager] Лобби {currentLobbyID} удалено при выходе из игры");
                }
                
                // Также пытаемся выйти через LobbyManager
                if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
                {
                    // Останавливаем сетевые подключения
                    if (NetworkServer.active && LobbyNetworkManager.Instance != null)
                    {
                        LobbyNetworkManager.Instance.StopHost();
                    }
                    else if (NetworkClient.active && LobbyNetworkManager.Instance != null)
                    {
                        LobbyNetworkManager.Instance.StopClient();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamLobbyManager] Ошибка при выходе из лобби: {e.Message}");
            }
        }
    }
    
    private void OnDestroy()
    {
        // При уничтожении объекта также выходим из лобби
        if (IsInActiveLobby())
        {
            LeaveLobby();
        }
    }
}

