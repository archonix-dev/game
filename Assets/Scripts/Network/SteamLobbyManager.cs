using UnityEngine;
using Mirror;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Менеджер для работы со Steam лобби через встроенный Steam оверлей
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    private static SteamLobbyManager instance;
    public static SteamLobbyManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SteamLobbyManager>();
            }
            return instance;
        }
    }

    private CSteamID currentLobbyId;
    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private Callback<LobbyEnter_t> lobbyEnteredCallback;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<LobbyMatchList_t> lobbyMatchListCallback;

    private MirrorNetworkManager networkManager;
    private LobbyManager lobbyManager;
    
    // События для уведомления других компонентов
    public System.Action<ulong> OnLobbyJoined;
    public System.Action OnLobbyLeft;

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        networkManager = MirrorNetworkManager.Instance;
        lobbyManager = LobbyManager.Instance;

        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogWarning("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }

        // Подписываемся на события Steam
        if (lobbyCreatedCallback == null)
        {
            lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        }
        
        if (gameLobbyJoinRequestedCallback == null)
        {
            gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        }
        
        if (lobbyEnteredCallback == null)
        {
            lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }
        
        if (lobbyChatUpdateCallback == null)
        {
            lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        }
        
        if (lobbyMatchListCallback == null)
        {
            lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }
        #endif
    }

    /// <summary>
    /// Создает Steam лобби
    /// </summary>
    public void CreateLobby()
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }

        if (networkManager == null)
        {
            networkManager = MirrorNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[SteamLobbyManager] MirrorNetworkManager не найден!");
                return;
            }
        }

        // Создаем Steam лобби (ELobbyType.k_ELobbyTypeFriendsOnly - только для друзей)
        // ВАЖНО: k_ELobbyTypeFriendsOnly означает, что лобби видно только друзьям, но это не мешает поиску
        // Для публичных лобби используйте k_ELobbyTypePublic, но тогда лобби будет видно всем
        SteamAPICall_t handle = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkManager.maxConnections);
        
        if (handle == SteamAPICall_t.Invalid)
        {
            Debug.LogError("[SteamLobbyManager] Не удалось создать Steam лобби!");
        }
        else
        {
        }
        #else
        Debug.LogWarning("[SteamLobbyManager] Steam не доступен, создание лобби пропущено");
        #endif
    }

    /// <summary>
    /// Открывает Steam оверлей для присоединения к лобби (устаревший метод, оставлен для совместимости)
    /// </summary>
    public void OpenSteamOverlayForJoin()
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }

        // Открываем Steam оверлей с друзьями
        SteamFriends.ActivateGameOverlay("friends");
        #else
        Debug.LogWarning("[SteamLobbyManager] Steam не доступен");
        #endif
    }
    
    /// <summary>
    /// Ищет лобби по никнейму хоста
    /// </summary>
    public void SearchLobbiesByHostName(string hostName)
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }
        
        // Steam API не поддерживает поиск по строковому полю напрямую
        // Запрашиваем все доступные лобби (только для друзей)
        // Фильтрация по имени хоста будет выполнена в OnLobbyMatchList
        storedSearchFilter = hostName.ToLower(); // Сохраняем фильтр для использования в OnLobbyMatchList
        
        // Запрашиваем список лобби
        SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        if (handle == SteamAPICall_t.Invalid)
        {
            Debug.LogError("[SteamLobbyManager] Не удалось запросить список лобби!");
        }
        else
        {
        }
        #else
        Debug.LogWarning("[SteamLobbyManager] Steam не доступен");
        #endif
    }
    
    /// <summary>
    /// Запрашивает все доступные лобби (для обновления списка)
    /// </summary>
    public void SearchAllLobbies()
    {
        #if !DISABLESTEAMWORKS
        if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamInitialized())
        {
            Debug.LogError("[SteamLobbyManager] Steam не инициализирован!");
            return;
        }
        
        // Очищаем фильтр поиска по имени
        storedSearchFilter = "";
        
        // Запрашиваем список всех доступных лобби
        SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        if (handle == SteamAPICall_t.Invalid)
        {
            Debug.LogError("[SteamLobbyManager] Не удалось запросить список лобби!");
        }
        else
        {
        }
        #else
        Debug.LogWarning("[SteamLobbyManager] Steam не доступен");
        #endif
    }
    
    private string storedSearchFilter = ""; // Временное хранилище для фильтра поиска
    
    /// <summary>
    /// Получает данные лобби (пароль, никнейм хоста и т.д.)
    /// </summary>
    public string GetLobbyData(ulong lobbyId, string key)
    {
        #if !DISABLESTEAMWORKS
        if (lobbyId != 0)
        {
            CSteamID lobby = new CSteamID(lobbyId);
            return SteamMatchmaking.GetLobbyData(lobby, key);
        }
        #endif
        return "";
    }
    
    /// <summary>
    /// Устанавливает данные лобби (пароль, никнейм хоста и т.д.)
    /// </summary>
    public void SetLobbyData(string key, string value)
    {
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            SteamMatchmaking.SetLobbyData(currentLobbyId, key, value);
        }
        #endif
    }
    
    /// <summary>
    /// Получает количество найденных лобби
    /// </summary>
    public int GetLobbyCount()
    {
        #if !DISABLESTEAMWORKS
        return SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        #endif
        return 0;
    }
    
    /// <summary>
    /// Получает Steam ID лобби по индексу из результатов поиска
    /// </summary>
    public ulong GetLobbyByIndex(int index)
    {
        #if !DISABLESTEAMWORKS
        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(index);
        if (lobbyId.IsValid())
        {
            return lobbyId.m_SteamID;
        }
        #endif
        return 0;
    }
    
    // Событие для уведомления о найденных лобби
    public System.Action<System.Collections.Generic.List<LobbySearchResult>> OnLobbiesFound;
    
    /// <summary>
    /// Структура для хранения информации о найденном лобби
    /// </summary>
    public struct LobbySearchResult
    {
        public ulong lobbyId;
        public string hostName;
        public int currentPlayers;
        public int maxPlayers;
        public string password;
        public ulong hostSteamId;
    }

    /// <summary>
    /// Получает Steam ID владельца лобби
    /// </summary>
    public ulong GetLobbyOwnerId()
    {
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
            return ownerId.m_SteamID;
        }
        #endif
        return 0;
    }
    
    /// <summary>
    /// Получает ID текущего Steam лобби
    /// </summary>
    public ulong GetCurrentLobbyId()
    {
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            return currentLobbyId.m_SteamID;
        }
        #endif
        return 0;
    }
    
    /// <summary>
    /// Получает количество игроков в текущем лобби
    /// </summary>
    public int GetLobbyMemberCount()
    {
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            return SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        }
        #endif
        return 0;
    }
    
    /// <summary>
    /// Получает Steam ID игрока по индексу в лобби
    /// </summary>
    public ulong GetLobbyMemberByIndex(int index)
    {
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, index);
            return memberId.m_SteamID;
        }
        #endif
        return 0;
    }
    
    /// <summary>
    /// Получает имя игрока по Steam ID
    /// </summary>
    public string GetPlayerName(ulong steamId)
    {
        #if !DISABLESTEAMWORKS
        if (steamId != 0)
        {
            CSteamID playerId = new CSteamID(steamId);
            return SteamFriends.GetFriendPersonaName(playerId);
        }
        #endif
        return "";
    }
    
    /// <summary>
    /// Получает список всех игроков в текущем лобби (Steam ID и имя)
    /// </summary>
    public System.Collections.Generic.List<System.Tuple<ulong, string>> GetLobbyMembers()
    {
        var members = new System.Collections.Generic.List<System.Tuple<ulong, string>>();
        
        #if !DISABLESTEAMWORKS
        if (currentLobbyId.IsValid())
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i);
                string memberName = SteamFriends.GetFriendPersonaName(memberId);
                members.Add(new System.Tuple<ulong, string>(memberId.m_SteamID, memberName));
            }
        }
        #endif
        
        return members;
    }

    #if !DISABLESTEAMWORKS
    /// <summary>
    /// Обработчик создания лобби
    /// </summary>
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult == EResult.k_EResultOK)
        {
            currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Устанавливаем данные лобби (никнейм хоста и Steam ID)
            // ВАЖНО: Эти данные должны быть установлены ДО того, как другие игроки начнут искать лобби
            #if !DISABLESTEAMWORKS
            string hostName = SteamFriends.GetPersonaName();
            if (!string.IsNullOrEmpty(hostName))
            {
                SteamMatchmaking.SetLobbyData(currentLobbyId, "host_name", hostName);
                Debug.Log($"[SteamLobbyManager] Установлено host_name для лобби: {hostName}");
            }
            else
            {
                Debug.LogWarning("[SteamLobbyManager] Не удалось получить имя хоста из Steam!");
            }
            
            // Сохраняем Steam ID хоста в данных лобби для надежности
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
            SteamMatchmaking.SetLobbyData(currentLobbyId, "host_steam_id", mySteamId.ToString());
            Debug.Log($"[SteamLobbyManager] Установлено host_steam_id для лобби: {mySteamId}");
            
            // Генерируем и устанавливаем пароль лобби, если он еще не установлен
            string currentPassword = SteamMatchmaking.GetLobbyData(currentLobbyId, "password");
            if (string.IsNullOrEmpty(currentPassword))
            {
                // Генерируем случайный 6-значный цифровой пароль
                string newPassword = GenerateRandomPassword();
                SteamMatchmaking.SetLobbyData(currentLobbyId, "password", newPassword);
                Debug.Log($"[SteamLobbyManager] Сгенерирован и установлен пароль лобби: {newPassword}");
            }
            else
            {
                Debug.Log($"[SteamLobbyManager] Пароль лобби уже установлен: {currentPassword}");
            }
            
            // Устанавливаем дополнительные данные для поиска
            // Убеждаемся, что данные установлены правильно
            // ВАЖНО: Steam API может требовать небольшую задержку для синхронизации данных
            StartCoroutine(VerifyLobbyDataDelayed(currentLobbyId, hostName, mySteamId));
            #endif

            // Запускаем Mirror хост
            if (networkManager != null)
            {
                networkManager.StartHostGame();
            }

            // Уведомляем LobbyManager о создании лобби
            if (lobbyManager != null)
            {
                // Лобби создано, Mirror хост запустится автоматически
            }
        }
        else
        {
            string errorMessage = "Не удалось создать лобби";
            switch (callback.m_eResult)
            {
                case EResult.k_EResultLimitExceeded:
                    errorMessage = "Достигнут лимит созданных лобби. Попробуйте позже";
                    break;
                case EResult.k_EResultAccessDenied:
                    errorMessage = "Нет доступа для создания лобби";
                    break;
                case EResult.k_EResultTimeout:
                    errorMessage = "Превышено время ожидания. Проверьте подключение";
                    break;
                case EResult.k_EResultNoConnection:
                    errorMessage = "Нет подключения к серверам. Проверьте интернет";
                    break;
            }
            
            if (lobbyManager != null)
            {
                lobbyManager.UpdateStatusText(errorMessage, false);
            }
            
            Debug.LogError($"[SteamLobbyManager] Ошибка создания Steam лобби: {callback.m_eResult}");
        }
    }

    /// <summary>
    /// Обработчик запроса на присоединение к лобби (приглашение друга)
    /// </summary>
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        if (!callback.m_steamIDLobby.IsValid())
        {
            Debug.LogWarning("[SteamLobbyManager] Получен невалидный Steam ID лобби в OnGameLobbyJoinRequested");
            return;
        }
        
        Debug.Log($"[SteamLobbyManager] Получен запрос на присоединение к лобби: {callback.m_steamIDLobby.m_SteamID}");
        
        // Проверяем, что мы не уже в лобби или не пытаемся подключиться
        if (currentLobbyId.IsValid() && currentLobbyId == callback.m_steamIDLobby)
        {
            Debug.Log("[SteamLobbyManager] Уже находимся в этом лобби");
            return;
        }
        
        // Если мы уже подключены, сначала отключаемся
        if (networkManager != null && (NetworkServer.active || NetworkClient.active))
        {
            Debug.Log("[SteamLobbyManager] Уже подключены к сети. Отключаемся перед присоединением к новому лобби...");
            if (NetworkServer.active && NetworkClient.active)
            {
                networkManager.StopHostSafe();
            }
            else if (NetworkClient.active)
            {
                networkManager.StopClientSafe();
            }
            
            // Ждем немного перед присоединением к новому лобби
            StartCoroutine(JoinLobbyAfterDisconnect(callback.m_steamIDLobby));
            return;
        }
        
        // Присоединяемся к лобби
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }
    
    /// <summary>
    /// Присоединяется к лобби после отключения
    /// </summary>
    private System.Collections.IEnumerator JoinLobbyAfterDisconnect(CSteamID lobbyId)
    {
        // Ждем отключения
        int attempts = 0;
        while (networkManager != null && (NetworkServer.active || NetworkClient.active) && attempts < 30)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (networkManager != null && (NetworkServer.active || NetworkClient.active))
        {
            Debug.LogWarning("[SteamLobbyManager] Не удалось отключиться перед присоединением к лобби");
            yield break;
        }
        
        // Ждем еще немного для полного закрытия соединения
        yield return new WaitForSeconds(0.5f);
        
        // Присоединяемся к лобби
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    /// <summary>
    /// Обработчик входа в лобби
    /// </summary>
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        EChatRoomEnterResponse response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;
        
        if (response == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            // Уведомляем о присоединении к лобби (проверяем, что делегат не null)
            if (OnLobbyJoined != null)
            {
                try
                {
                    OnLobbyJoined.Invoke(currentLobbyId.m_SteamID);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SteamLobbyManager] Ошибка при вызове OnLobbyJoined: {e.Message}");
                }
            }

            // Получаем Steam ID владельца лобби
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
            ulong ownerSteamId = 0;
            
            if (ownerId.IsValid())
            {
                ownerSteamId = ownerId.m_SteamID;
            }
            else
            {
                // Если владелец невалиден, пытаемся получить из данных лобби
                string hostSteamIdStr = SteamMatchmaking.GetLobbyData(currentLobbyId, "host_steam_id");
                if (!string.IsNullOrEmpty(hostSteamIdStr) && ulong.TryParse(hostSteamIdStr, out ulong parsedSteamId))
                {
                    ownerSteamId = parsedSteamId;
                }
                else
                {
                    // Пытаемся получить первого участника
                    int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
                    if (memberCount > 0)
                    {
                        CSteamID firstMember = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, 0);
                        if (firstMember.IsValid())
                        {
                            ownerSteamId = firstMember.m_SteamID;
                        }
                    }
                }
            }
            
            // Получаем наш собственный Steam ID
            ulong mySteamId = SteamUser.GetSteamID().m_SteamID;
            
            if (ownerSteamId == 0)
            {
                if (lobbyManager != null)
                {
                    lobbyManager.UpdateStatusText("Ошибка: Не удалось найти хост лобби", false);
                }
                Debug.LogError("[SteamLobbyManager] Не удалось получить Steam ID владельца лобби!");
                return;
            }

            // Проверяем, что мы не пытаемся подключиться к своему собственному серверу
            if (ownerSteamId == mySteamId)
            {
                Debug.LogWarning("[SteamLobbyManager] Мы являемся владельцем лобби! Не подключаемся к серверу (мы уже хост).");
                return;
            }

            // Подключаемся к Mirror серверу через Steam ID владельца
            if (networkManager != null)
            {
                Debug.Log($"[SteamLobbyManager] Подключаемся к серверу через Steam ID: {ownerSteamId}");
                networkManager.ConnectToSteamId(ownerSteamId);
            }
            else
            {
                if (lobbyManager != null)
                {
                    lobbyManager.UpdateStatusText("Ошибка: Не удалось установить соединение с сервером", false);
                }
                Debug.LogError("[SteamLobbyManager] NetworkManager не найден!");
            }
        }
        else
        {
            // Детальная обработка ошибок
            string errorMessage = GetLobbyEnterErrorString(response);
            Debug.LogError($"[SteamLobbyManager] Ошибка входа в лобби: {errorMessage} (код: {callback.m_EChatRoomEnterResponse})");
            
            // Обновляем статус в LobbyManager
            if (lobbyManager != null)
            {
                lobbyManager.UpdateStatusText($"Ошибка: {errorMessage}", false);
            }
            
            // Дополнительная информация для отладки
            if (callback.m_ulSteamIDLobby != 0)
            {
                CSteamID failedLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                int memberCount = SteamMatchmaking.GetNumLobbyMembers(failedLobbyId);
                int maxMembers = SteamMatchmaking.GetLobbyMemberLimit(failedLobbyId);
                string lobbyData = SteamMatchmaking.GetLobbyData(failedLobbyId, "host_steam_id");
                
                Debug.LogError($"[SteamLobbyManager] Детали лобби: LobbyID={failedLobbyId.m_SteamID}, Members={memberCount}/{maxMembers}, HostSteamID={lobbyData}");
            }
        }
    }
    
    /// <summary>
    /// Получает текстовое описание ошибки входа в лобби
    /// </summary>
    private string GetLobbyEnterErrorString(EChatRoomEnterResponse response)
    {
        switch (response)
        {
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                return "Лобби не найдено или было закрыто";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                return "Нет доступа. Проверьте пароль или попробуйте позже";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                return "Лобби заполнено. Максимальное количество игроков достигнуто";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseError:
                return "Ошибка подключения. Попробуйте еще раз";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                return "Доступ запрещен администратором лобби";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                return "Ограниченный доступ к лобби";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseClanDisabled:
                return "Лобби недоступно";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan:
                return "Доступ ограничен";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou:
                return "Хост лобби заблокировал вас";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember:
                return "Вы заблокировали хост лобби";
            default:
                return "Не удалось подключиться к лобби";
        }
    }
    
    /// <summary>
    /// Обработчик обновления лобби (вход/выход игроков)
    /// </summary>
    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        
        // Проверяем, был ли это выход из лобби
        if (callback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft ||
            callback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected)
        {
            // Проверяем, не мы ли вышли из лобби
            CSteamID localSteamId = SteamUser.GetSteamID();
            if (callback.m_ulSteamIDUserChanged == localSteamId.m_SteamID)
            {
                // Мы вышли из лобби
                currentLobbyId = CSteamID.Nil;
                if (OnLobbyLeft != null)
                {
                    try
                    {
                        OnLobbyLeft.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SteamLobbyManager] Ошибка при вызове OnLobbyLeft: {e.Message}");
                    }
                }
            }
            else
            {
                // Кто-то другой вышел из лобби
            }
        }
        else if (callback.m_rgfChatMemberStateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered)
        {
            // Кто-то вошел в лобби
        }
        
        // Уведомляем LobbyManager об обновлении (для синхронизации списка игроков)
        if (lobbyManager != null)
        {
            // LobbyManager будет синхронизировать список через периодическую проверку
        }
    }
    
    /// <summary>
    /// Обработчик получения списка найденных лобби
    /// </summary>
    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        var foundLobbies = new System.Collections.Generic.List<LobbySearchResult>();
        
        #if !DISABLESTEAMWORKS
        Debug.Log($"[SteamLobbyManager] Получен список лобби: найдено {callback.m_nLobbiesMatching} лобби");
        
        for (int i = 0; i < callback.m_nLobbiesMatching && i < 50; i++) // Ограничиваем до 50 лобби
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            if (!lobbyId.IsValid()) continue;
            
            // Получаем данные лобби
            // ВАЖНО: Сначала пытаемся получить из данных лобби (самый надежный способ)
            string hostName = SteamMatchmaking.GetLobbyData(lobbyId, "host_name");
            
            // Получаем количество участников заранее (нужно для проверок)
            int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            
            // Если имя хоста не получено из данных, пытаемся получить через другие методы
            if (string.IsNullOrEmpty(hostName))
            {
                // Метод 1: Получаем из Steam ID владельца
                try
                {
                    CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
                    if (ownerId.IsValid() && ownerId.m_SteamID != 0)
                    {
                        hostName = SteamFriends.GetFriendPersonaName(ownerId);
                        if (!string.IsNullOrEmpty(hostName))
                        {
                            Debug.Log($"[SteamLobbyManager] Имя хоста получено из Steam ID владельца: {hostName}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamLobbyManager] Ошибка при получении имени хоста через GetLobbyOwner: {e.Message}");
                }
                
                // Метод 2: Если все еще не получили, пытаемся получить из первого участника
                if (string.IsNullOrEmpty(hostName) && currentPlayers > 0)
                {
                    try
                    {
                        CSteamID firstMember = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, 0);
                        if (firstMember.IsValid() && firstMember.m_SteamID != 0)
                        {
                            hostName = SteamFriends.GetFriendPersonaName(firstMember);
                            if (!string.IsNullOrEmpty(hostName))
                            {
                                Debug.Log($"[SteamLobbyManager] Имя хоста получено из первого участника: {hostName}");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[SteamLobbyManager] Ошибка при получении имени хоста через первого участника: {e.Message}");
                    }
                }
                
                // Если все еще не получили, используем "Unknown Host"
                if (string.IsNullOrEmpty(hostName))
                {
                    Debug.LogWarning($"[SteamLobbyManager] Не удалось получить имя хоста для лобби {lobbyId.m_SteamID}");
                    hostName = "Unknown Host";
                }
            }
            
            // Фильтруем по имени хоста, если задан фильтр
            if (!string.IsNullOrEmpty(storedSearchFilter))
            {
                if (!hostName.ToLower().Contains(storedSearchFilter))
                {
                    continue; // Пропускаем лобби, которое не соответствует фильтру
                }
            }
            
            string password = SteamMatchmaking.GetLobbyData(lobbyId, "password");
            // currentPlayers уже объявлен выше
            int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
            
            // Получаем Steam ID владельца лобби (хоста)
            // Упрощенная и оптимизированная логика получения hostSteamId
            ulong hostSteamId = 0;
            
            // Метод 1: Получаем из данных лобби (самый надежный способ)
            string hostSteamIdStr = SteamMatchmaking.GetLobbyData(lobbyId, "host_steam_id");
            if (!string.IsNullOrEmpty(hostSteamIdStr) && ulong.TryParse(hostSteamIdStr, out ulong parsedSteamId) && parsedSteamId != 0)
            {
                hostSteamId = parsedSteamId;
                Debug.Log($"[SteamLobbyManager] HostSteamID получен из данных лобби: {hostSteamId}");
            }
            
            // Метод 2: Получаем через GetLobbyOwner (работает для доступных лобби)
            if (hostSteamId == 0)
            {
                try
                {
                    CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
                    if (ownerId.IsValid() && ownerId.m_SteamID != 0)
                    {
                        hostSteamId = ownerId.m_SteamID;
                        Debug.Log($"[SteamLobbyManager] HostSteamID получен из GetLobbyOwner: {hostSteamId}");
                        
                        // Обновляем имя хоста, если оно пустое
                        if (string.IsNullOrEmpty(hostName) || hostName == "Unknown Host")
                        {
                            string ownerName = SteamFriends.GetFriendPersonaName(ownerId);
                            if (!string.IsNullOrEmpty(ownerName))
                            {
                                hostName = ownerName;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamLobbyManager] Ошибка при получении владельца лобби: {e.Message}");
                }
            }
            
            // Метод 3: Fallback - используем первого участника (если есть)
            if (hostSteamId == 0 && currentPlayers > 0)
            {
                try
                {
                    CSteamID firstMember = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, 0);
                    if (firstMember.IsValid() && firstMember.m_SteamID != 0)
                    {
                        hostSteamId = firstMember.m_SteamID;
                        Debug.LogWarning($"[SteamLobbyManager] HostSteamID получен из первого участника (fallback): {hostSteamId}");
                        
                        // Обновляем имя хоста
                        if (string.IsNullOrEmpty(hostName) || hostName == "Unknown Host")
                        {
                            string memberName = SteamFriends.GetFriendPersonaName(firstMember);
                            if (!string.IsNullOrEmpty(memberName))
                            {
                                hostName = memberName;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamLobbyManager] Ошибка при получении первого участника: {e.Message}");
                }
            }
            
            // Если все методы не сработали, пропускаем это лобби
            if (hostSteamId == 0)
            {
                Debug.LogWarning($"[SteamLobbyManager] Не удалось получить hostSteamId для лобби {lobbyId.m_SteamID}. " +
                    $"Данные: host_steam_id='{hostSteamIdStr}', currentPlayers={currentPlayers}, hostName='{hostName}'. " +
                    $"Возможно, данные лобби еще не синхронизированы. Лобби будет пропущено.");
                continue;
            }
            else
            {
                // Отладочная информация для успешно найденных лобби (первые 5)
                if (foundLobbies.Count < 5)
                {
                    Debug.Log($"[SteamLobbyManager] Лобби {foundLobbies.Count + 1}: LobbyID={lobbyId.m_SteamID}, HostSteamID={hostSteamId}, HostName='{hostName}', Players={currentPlayers}/{maxPlayers}");
                }
            }
            
            foundLobbies.Add(new LobbySearchResult
            {
                lobbyId = lobbyId.m_SteamID,
                hostName = hostName,
                currentPlayers = currentPlayers,
                maxPlayers = maxPlayers,
                password = password,
                hostSteamId = hostSteamId
            });
        }
        
        // Очищаем фильтр после использования
        storedSearchFilter = "";
        #endif
        
        Debug.Log($"[SteamLobbyManager] Обработано {foundLobbies.Count} лобби из {callback.m_nLobbiesMatching} найденных");
        
        // Уведомляем о найденных лобби (проверяем, что делегат не null)
        if (OnLobbiesFound != null)
        {
            try
            {
                OnLobbiesFound.Invoke(foundLobbies);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SteamLobbyManager] Ошибка при вызове OnLobbiesFound: {e.Message}");
            }
        }
    }
    #endif

    /// <summary>
    /// Генерирует случайный 6-значный цифровой пароль
    /// </summary>
    private string GenerateRandomPassword()
    {
        System.Random random = new System.Random();
        return random.Next(100000, 999999).ToString();
    }
    
    /// <summary>
    /// Проверяет данные лобби с задержкой (для синхронизации Steam API)
    /// </summary>
    private System.Collections.IEnumerator VerifyLobbyDataDelayed(CSteamID lobbyId, string expectedHostName, ulong expectedHostSteamId)
    {
        // Ждем немного для синхронизации данных Steam API
        yield return new WaitForSeconds(0.5f);
        
        #if !DISABLESTEAMWORKS
        if (!lobbyId.IsValid()) yield break;
        
        string verifyHostName = SteamMatchmaking.GetLobbyData(lobbyId, "host_name");
        string verifyHostSteamId = SteamMatchmaking.GetLobbyData(lobbyId, "host_steam_id");
        
        Debug.Log($"[SteamLobbyManager] Проверка данных лобби (после задержки): host_name='{verifyHostName}', host_steam_id='{verifyHostSteamId}'");
        
        // Если данные все еще не установлены, пытаемся установить их снова
        if (string.IsNullOrEmpty(verifyHostName) || string.IsNullOrEmpty(verifyHostSteamId))
        {
            Debug.LogWarning("[SteamLobbyManager] Данные лобби не синхронизированы, пытаемся установить снова...");
            
            // Повторная установка данных
            if (string.IsNullOrEmpty(verifyHostName) && !string.IsNullOrEmpty(expectedHostName))
            {
                SteamMatchmaking.SetLobbyData(lobbyId, "host_name", expectedHostName);
                Debug.Log($"[SteamLobbyManager] Повторно установлено host_name: {expectedHostName}");
            }
            
            if (string.IsNullOrEmpty(verifyHostSteamId) && expectedHostSteamId != 0)
            {
                SteamMatchmaking.SetLobbyData(lobbyId, "host_steam_id", expectedHostSteamId.ToString());
                Debug.Log($"[SteamLobbyManager] Повторно установлено host_steam_id: {expectedHostSteamId}");
            }
            
            // Проверяем еще раз после повторной установки
            yield return new WaitForSeconds(0.3f);
            
            verifyHostName = SteamMatchmaking.GetLobbyData(lobbyId, "host_name");
            verifyHostSteamId = SteamMatchmaking.GetLobbyData(lobbyId, "host_steam_id");
            
            if (string.IsNullOrEmpty(verifyHostName) || string.IsNullOrEmpty(verifyHostSteamId))
            {
                Debug.LogError("[SteamLobbyManager] КРИТИЧЕСКАЯ ОШИБКА: Данные лобби не установлены даже после повторной попытки! " +
                    $"host_name='{verifyHostName}', host_steam_id='{verifyHostSteamId}'. " +
                    $"Лобби может быть не найдено при поиске!");
            }
            else
            {
                Debug.Log($"[SteamLobbyManager] ✓ Данные лобби успешно установлены после повторной попытки");
            }
        }
        else
        {
            Debug.Log($"[SteamLobbyManager] ✓ Данные лобби успешно установлены и синхронизированы");
        }
        #endif
    }
    
    void OnDestroy()
    {
        // ВАЖНО: Отписываемся от событий Steam перед уничтожением
        // Callbacks должны быть отписаны явно, иначе могут остаться в памяти
        #if !DISABLESTEAMWORKS
        try
        {
            // Disposing callbacks может вызвать ошибку, если Steam уже закрыт
            // Поэтому оборачиваем в try-catch
            if (lobbyCreatedCallback != null)
            {
                lobbyCreatedCallback.Dispose();
                lobbyCreatedCallback = null;
            }
            
            if (gameLobbyJoinRequestedCallback != null)
            {
                gameLobbyJoinRequestedCallback.Dispose();
                gameLobbyJoinRequestedCallback = null;
            }
            
            if (lobbyEnteredCallback != null)
            {
                lobbyEnteredCallback.Dispose();
                lobbyEnteredCallback = null;
            }
            
            if (lobbyChatUpdateCallback != null)
            {
                lobbyChatUpdateCallback.Dispose();
                lobbyChatUpdateCallback = null;
            }
            
            if (lobbyMatchListCallback != null)
            {
                lobbyMatchListCallback.Dispose();
                lobbyMatchListCallback = null;
            }
        }
        catch (System.Exception e)
        {
            // Если Steam уже закрыт или callback уже уничтожен, это нормально
            Debug.LogWarning($"[SteamLobbyManager] Ошибка при освобождении callbacks: {e.Message}");
        }
        #endif
        
        // Очищаем события
        OnLobbyJoined = null;
        OnLobbyLeft = null;
        OnLobbiesFound = null;
    }
}

