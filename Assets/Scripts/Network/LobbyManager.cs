using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Mirror.Examples.NetworkRoom;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Управляет лобби: создание, присоединение, настройки, список игроков
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Lobby Settings")]
    [Tooltip("Максимальное количество игроков по умолчанию")]
    public int maxPlayers = 4;
    
    [Tooltip("Пароль лобби")]
    public string lobbyPassword = "";
    
    [Tooltip("Включены ли читы")]
    public bool cheatsEnabled = false;
    
    [Header("UI References")]
    [Tooltip("Transform для спавна префабов списка игроков")]
    public Transform playerListParent;
    
    [Tooltip("Префаб для элемента списка игроков")]
    public GameObject playerListPrefab;
    
    [Tooltip("Transform для спавна префабов списка лобби")]
    public Transform lobbyListParent;
    
    [Tooltip("Префаб для элемента списка лобби")]
    public GameObject lobbyListPrefab;

    [Header("Scene Loading")]
    [Tooltip("Тайм-аут ожидания загрузки сцены и готовности игроков (сек)")]
    [SerializeField] float clientSceneTimeout = 45f;
    
    [Tooltip("Тайм-аут ожидания спавнера (сек)")]
    [SerializeField] float spawnerWaitTimeout = 20f;
    
    [Tooltip("Дополнительная задержка перед началом игры (сек) для стабилизации сцены")]
    [SerializeField] float postSceneWarmupDelay = 0.5f;
    
    private static LobbyManager instance;
    private List<GameObject> spawnedPlayerListItems = new List<GameObject>();
    private List<GameObject> spawnedLobbyListItems = new List<GameObject>();
    private CSteamID currentLobbyID;
    private bool isLobbyOwner = false;
    private Callback<LobbyEnter_t> lobbyEnterCallback;
    private Coroutine lobbySceneLoadRoutine;
    private bool isGameStarting;
    
    public static LobbyManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyManager>();
            }
            return instance;
        }
    }
    
    public bool IsLobbyOwner => isLobbyOwner;
    public CSteamID CurrentLobbyID => currentLobbyID;
    
    void Awake()
    {
        instance = this;
    }
    
    void Start()
    {
        Debug.Log("[LobbyManager] Start() вызван");
        
        // Генерируем пароль при создании лобби
        if (string.IsNullOrEmpty(lobbyPassword))
        {
            GenerateLobbyPassword();
        }
        
        // Периодически проверяем и обновляем список игроков
        InvokeRepeating(nameof(CheckAndUpdatePlayerList), 1f, 0.5f);
        
        // Проверяем, что Steam запущен
        try
        {
            if (SteamAPI.IsSteamRunning())
            {
                Debug.Log("[LobbyManager] Steam запущен, настраиваем callbacks");
            }
            else
            {
                Debug.LogWarning("[LobbyManager] Steam не запущен! Callbacks могут не работать.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Ошибка проверки Steam: {e.Message}");
        }
        
        // Создаем callback для входа в лобби (создается один раз)
        SetupLobbyEnterCallback();
    }
    
    /// <summary>
    /// Настраивает callback для входа в лобби (создается один раз)
    /// </summary>
    void SetupLobbyEnterCallback()
    {
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogWarning("[LobbyManager] Steam не запущен, callback для входа в лобби не создан");
                return;
            }
            
            // Создаем callback один раз при старте
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            Debug.Log("[LobbyManager] Callback для входа в лобби создан");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Ошибка создания callback для входа в лобби: {e.Message}");
        }
    }
    
    /// <summary>
    /// Проверяет количество игроков и обновляет список при изменении
    /// </summary>
    void CheckAndUpdatePlayerList()
    {
        if (!NetworkClient.active && !NetworkServer.active) return;
        
        // Получаем текущее количество игроков
        LobbyPlayer[] currentPlayers = FindObjectsOfType<LobbyPlayer>();
        int currentCount = currentPlayers.Length;
        
        // Если количество изменилось, обновляем список
        if (currentCount != spawnedPlayerListItems.Count)
        {
            Debug.Log($"[LobbyManager] Количество игроков изменилось: {spawnedPlayerListItems.Count} -> {currentCount}");
            UpdatePlayerList();
        }
    }
    
    /// <summary>
    /// Генерирует 6-значный пароль для лобби
    /// </summary>
    public void GenerateLobbyPassword()
    {
        System.Random random = new System.Random();
        lobbyPassword = random.Next(100000, 999999).ToString();
        Debug.Log($"[LobbyManager] Сгенерирован пароль лобби: {lobbyPassword}");
    }
    
    /// <summary>
    /// Создает лобби через Steam
    /// </summary>
    public void CreateLobby()
    {
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogError("[LobbyManager] Steam не запущен!");
                return;
            }
            
            Debug.Log("[LobbyManager] Создание лобби через Steam...");
            
            // Создаем лобби через Steam Matchmaking
            SteamAPICall_t apiCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
            
            // Подписываемся на события Steam
            Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Ошибка создания лобби: {e.Message}");
        }
    }
    
    /// <summary>
    /// Обработчик создания лобби
    /// </summary>
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult == EResult.k_EResultOK)
        {
            currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            isLobbyOwner = true;
            
            Debug.Log($"[LobbyManager] Лобби создано успешно! ID: {currentLobbyID}");
            
            // Устанавливаем данные лобби
            SteamMatchmaking.SetLobbyData(currentLobbyID, "name", SteamFriends.GetPersonaName());
            SteamMatchmaking.SetLobbyData(currentLobbyID, "password", lobbyPassword);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "maxPlayers", maxPlayers.ToString());
            SteamMatchmaking.SetLobbyData(currentLobbyID, "cheats", cheatsEnabled.ToString());
            
            // Синхронизируем лобби с SteamLobbyManager
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.SetCurrentLobby(currentLobbyID);
            }
            
            // Запускаем хост через NetworkManager
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.CreateLobby();
                // Обновляем список игроков после создания лобби (с задержкой для спавна LobbyPlayer)
                Invoke(nameof(UpdatePlayerList), 0.5f);
            }
        }
        else
        {
            Debug.LogError($"[LobbyManager] Ошибка создания лобби: {callback.m_eResult}");
        }
    }
    
    /// <summary>
    /// Обновляет настройки лобби
    /// </summary>
    public void UpdateLobbySettings(int newMaxPlayers, string newPassword, bool newCheatsEnabled)
    {
        if (!isLobbyOwner) return;
        
        maxPlayers = newMaxPlayers;
        lobbyPassword = newPassword;
        cheatsEnabled = newCheatsEnabled;
        
        if (currentLobbyID.IsValid())
        {
            SteamMatchmaking.SetLobbyData(currentLobbyID, "password", lobbyPassword);
            SteamMatchmaking.SetLobbyData(currentLobbyID, "maxPlayers", maxPlayers.ToString());
            SteamMatchmaking.SetLobbyData(currentLobbyID, "cheats", cheatsEnabled.ToString());
            
            Debug.Log($"[LobbyManager] Настройки лобби обновлены: MaxPlayers={maxPlayers}, Password={lobbyPassword}, Cheats={cheatsEnabled}");
        }
    }
    
    /// <summary>
    /// Обновляет список игроков в UI
    /// </summary>
    public void UpdatePlayerList()
    {
        if (playerListParent == null || playerListPrefab == null)
        {
            Debug.LogWarning("[LobbyManager] playerListParent или playerListPrefab не назначены!");
            return;
        }
        
        // Удаляем старые элементы
        foreach (GameObject item in spawnedPlayerListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedPlayerListItems.Clear();
        
        // Получаем всех игроков из сети
        LobbyPlayer[] players = FindObjectsOfType<LobbyPlayer>();
        
        Debug.Log($"[LobbyManager] Найдено LobbyPlayer: {players.Length}");
        
        if (players.Length == 0)
        {
            Debug.LogWarning("[LobbyManager] LobbyPlayer не найдены! Убедитесь, что autoCreatePlayer = true в NetworkManager и playerPrefab назначен.");
        }
        
        foreach (LobbyPlayer player in players)
        {
            Debug.Log($"[LobbyManager] Добавляем игрока в список: {player.playerName} (Owner: {player.isOwner}, Local: {player.isLocalPlayer})");
            
            GameObject playerListItem = Instantiate(playerListPrefab, playerListParent);
            PlayerListUI playerListUI = playerListItem.GetComponent<PlayerListUI>();
            
            if (playerListUI != null)
            {
                playerListUI.SetupPlayer(player);
            }
            else
            {
                Debug.LogError("[LobbyManager] PlayerListUI не найден на префабе playerListPrefab!");
            }
            
            spawnedPlayerListItems.Add(playerListItem);
        }
    }
    
    /// <summary>
    /// Обновляет список лобби друзей
    /// </summary>
    public void UpdateLobbyList(string searchFilter = "")
    {
        if (lobbyListParent == null || lobbyListPrefab == null) return;
        
        // Удаляем старые элементы
        foreach (GameObject item in spawnedLobbyListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spawnedLobbyListItems.Clear();
        
        try
        {
            if (!SteamAPI.IsSteamRunning()) return;
        }
        catch
        {
            return;
        }
        
        // Получаем список друзей
        int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagAll);
        List<CSteamID> friendLobbies = new List<CSteamID>();
        
        for (int i = 0; i < friendCount; i++)
        {
            CSteamID friendID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagAll);
            
            // Проверяем, есть ли у друга активное лобби
            FriendGameInfo_t gameInfo;
            if (SteamFriends.GetFriendGamePlayed(friendID, out gameInfo) && gameInfo.m_steamIDLobby.IsValid())
            {
                string lobbyName = SteamMatchmaking.GetLobbyData(gameInfo.m_steamIDLobby, "name");
                
                // Фильтруем по имени, если указан фильтр
                if (string.IsNullOrEmpty(searchFilter) || lobbyName.ToLower().Contains(searchFilter.ToLower()))
                {
                    friendLobbies.Add(gameInfo.m_steamIDLobby);
                }
            }
        }
        
        // Создаем UI элементы для каждого лобби
        foreach (CSteamID lobbyID in friendLobbies)
        {
            GameObject lobbyListItem = Instantiate(lobbyListPrefab, lobbyListParent);
            LobbyListUI lobbyListUI = lobbyListItem.GetComponent<LobbyListUI>();
            
            if (lobbyListUI != null)
            {
                lobbyListUI.SetupLobby(lobbyID);
            }
            
            spawnedLobbyListItems.Add(lobbyListItem);
        }
    }
    
    /// <summary>
    /// Присоединяется к лобби по Steam ID
    /// </summary>
    public void JoinLobby(CSteamID lobbyID, string password)
    {
        Debug.Log($"[LobbyManager] JoinLobby вызван для лобби: {lobbyID}, пароль: {(string.IsNullOrEmpty(password) ? "пустой" : "указан")}");
        
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogError("[LobbyManager] Steam не запущен! Невозможно присоединиться к лобби.");
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Ошибка проверки Steam: {e.Message}");
            return;
        }
        
        // Проверяем, что лобби валидно
        if (!lobbyID.IsValid())
        {
            Debug.LogError("[LobbyManager] Невалидный ID лобби!");
            return;
        }
        
        Debug.Log($"[LobbyManager] Проверяем пароль лобби...");
        
        // Проверяем пароль
        string lobbyPassword = SteamMatchmaking.GetLobbyData(lobbyID, "password");
        Debug.Log($"[LobbyManager] Пароль лобби из Steam: {(string.IsNullOrEmpty(lobbyPassword) ? "не установлен" : "установлен")}, введенный пароль: {(string.IsNullOrEmpty(password) ? "пустой" : "указан")}");
        
        // Если пароль не пустой и не совпадает, выдаем ошибку
        if (!string.IsNullOrEmpty(lobbyPassword) && lobbyPassword != password)
        {
            Debug.LogError($"[LobbyManager] Неверный пароль лобби! Ожидалось: {lobbyPassword}, получено: {password}");
            return;
        }
        
        Debug.Log($"[LobbyManager] Пароль проверен успешно, присоединяемся к лобби {lobbyID}...");
        
        // Присоединяемся к лобби
        // Callback уже создан в SetupLobbyEnterCallback, не создаем его снова
        SteamMatchmaking.JoinLobby(lobbyID);
        Debug.Log($"[LobbyManager] Вызван SteamMatchmaking.JoinLobby({lobbyID}), ожидаем callback OnLobbyEntered");
    }
    
    /// <summary>
    /// Обработчик входа в лобби
    /// </summary>
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        Debug.Log($"[LobbyManager] OnLobbyEntered вызван! Response: {callback.m_EChatRoomEnterResponse}, LobbyID: {callback.m_ulSteamIDLobby}");
        
        if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            isLobbyOwner = false;
            
            Debug.Log($"[LobbyManager] Успешно вошли в лобби: {currentLobbyID}");
            
            // Проверяем, являемся ли мы владельцем
            CSteamID ownerID = SteamMatchmaking.GetLobbyOwner(currentLobbyID);
            if (ownerID == SteamUser.GetSteamID())
            {
                isLobbyOwner = true;
                Debug.Log($"[LobbyManager] Мы являемся владельцем лобби");
            }
            else
            {
                Debug.Log($"[LobbyManager] Владелец лобби: {ownerID}");
            }
            
            // Синхронизируем лобби с SteamLobbyManager
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.SetCurrentLobby(currentLobbyID);
            }
            
            // Подключаемся через NetworkManager
            if (LobbyNetworkManager.Instance != null)
            {
                Debug.Log($"[LobbyManager] Подключаемся через LobbyNetworkManager к лобби {currentLobbyID.m_SteamID}");
                LobbyNetworkManager.Instance.JoinLobby(currentLobbyID.m_SteamID);
            }
            else
            {
                Debug.LogError("[LobbyManager] LobbyNetworkManager.Instance == null! Невозможно подключиться.");
            }
        }
        else
        {
            string errorMessage = GetLobbyEnterErrorString(callback.m_EChatRoomEnterResponse);
            Debug.LogError($"[LobbyManager] Ошибка входа в лобби: {callback.m_EChatRoomEnterResponse} ({errorMessage})");
        }
    }
    
    /// <summary>
    /// Получает текстовое описание ошибки входа в лобби
    /// </summary>
    private string GetLobbyEnterErrorString(uint response)
    {
        switch (response)
        {
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess:
                return "Успех";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                return "Лобби не существует";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                return "Не разрешено";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                return "Лобби заполнено";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseError:
                return "Ошибка";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                return "Забанен";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                return "Ограничено";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseClanDisabled:
                return "Клан отключен";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan:
                return "Бан сообщества";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou:
                return "Участник заблокировал вас";
            case (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember:
                return "Вы заблокировали участника";
            default:
                return "Неизвестная ошибка";
        }
    }
    
    /// <summary>
    /// Начинает игру (переход на сцену Lobby) с улучшенной асинхронной загрузкой
    /// </summary>
    public void StartGame()
    {
        if (!isLobbyOwner)
        {
            Debug.LogWarning("[LobbyManager] Только создатель лобби может начать игру!");
            return;
        }
        
        if (isGameStarting || lobbySceneLoadRoutine != null)
        {
            Debug.LogWarning("[LobbyManager] Запуск игры уже выполняется, повторный вызов проигнорирован.");
            return;
        }
        
        if (LobbyNetworkManager.Instance == null)
        {
            Debug.LogError("[LobbyManager] Невозможно начать игру: LobbyNetworkManager.Instance == null");
            return;
        }
        
        lobbySceneLoadRoutine = StartCoroutine(StartLobbySceneAsync());
    }
    
    IEnumerator StartLobbySceneAsync()
    {
        isGameStarting = true;
        ThreadPriority originalPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;
        
        try
        {
            Debug.Log("[LobbyManager] Стартуем улучшенную последовательность загрузки Lobby...");
            
            NotifyPlayersLoadingScreen();
            
            yield return PrepareSceneLoadAsync();
            
            // Уничтожаем все LobbyPlayer перед переходом на сцену Lobby
            // Они нужны только на сцене Menu
            DestroyAllLobbyPlayers();
            
            yield return null; // даем кадр на очистку и отображение загрузочных экранов
            
            LobbyNetworkManager.Instance.LoadLobbyScene();
            
            string lobbySceneName = LobbyNetworkManager.Instance.lobbySceneName;
            
            yield return WaitForServerSceneActivation(lobbySceneName, clientSceneTimeout);
            yield return WaitForAllClientsReady(clientSceneTimeout);
            yield return WaitForRewardSpawner(spawnerWaitTimeout);
            
            if (postSceneWarmupDelay > 0f)
            {
                yield return new WaitForSeconds(postSceneWarmupDelay);
            }
            
            Debug.Log("[LobbyManager] ✓ Сцена Lobby полностью загружена, все игроки синхронизированы.");
        }
        finally
        {
            Application.backgroundLoadingPriority = originalPriority;
            isGameStarting = false;
            lobbySceneLoadRoutine = null;
        }
    }
    
    IEnumerator PrepareSceneLoadAsync()
    {
        Debug.Log("[LobbyManager] Оптимизируем память перед загрузкой сцены (GC + UnloadUnusedAssets)...");
        
        // Выгружаем неиспользуемые ресурсы и очищаем память, чтобы уменьшить лаги при загрузке
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();
        
        // Плавное распределение нагрузки
        yield return null;
    }
    
    IEnumerator WaitForServerSceneActivation(string sceneName, float timeout)
    {
        if (string.IsNullOrEmpty(sceneName))
            yield break;
        
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                Debug.Log($"[LobbyManager] Сервер перешел на сцену {sceneName}");
                yield break;
            }
            
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        Debug.LogWarning($"[LobbyManager] Тайм-аут ожидания загрузки сцены {sceneName} на сервере ({timeout} сек)");
    }
    
    IEnumerator WaitForAllClientsReady(float timeout)
    {
        if (!NetworkServer.active)
            yield break;
        
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (AreAllConnectionsReady())
            {
                Debug.Log("[LobbyManager] Все клиенты подтвердили загрузку сцены.");
                yield break;
            }
            
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        Debug.LogWarning($"[LobbyManager] Тайм-аут ожидания готовности всех клиентов ({timeout} сек)");
    }
    
    IEnumerator WaitForRewardSpawner(float timeout)
    {
        if (!Spawner.HasActivePool || Spawner.IsInitialSpawnComplete)
            yield break;
        
        Debug.Log("[LobbyManager] Ожидаем завершения спавнера объектов (Spawner.cs)...");
        
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (Spawner.IsInitialSpawnComplete)
            {
                Debug.Log("[LobbyManager] Спавнер завершил создание стартовых объектов.");
                yield break;
            }
            
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        Debug.LogWarning("[LobbyManager] Тайм-аут ожидания завершения работы спавнера. Продолжаем загрузку.");
    }
    
    bool AreAllConnectionsReady()
    {
        if (!NetworkServer.active)
            return true;
        
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null)
                continue;
            
            if (!conn.isAuthenticated || !conn.isReady)
                return false;
        }
        
        return true;
    }

    void NotifyPlayersLoadingScreen()
    {
        LobbyPlayer[] lobbyPlayers = FindObjectsOfType<LobbyPlayer>();
        foreach (LobbyPlayer player in lobbyPlayers)
        {
            if (player != null && player.connectionToClient != null)
            {
                player.TargetShowLoadingScreen(player.connectionToClient);
            }
        }
    }
    
    /// <summary>
    /// Уничтожает все LobbyPlayer на сцене (вызывается перед переходом на Lobby)
    /// </summary>
    void DestroyAllLobbyPlayers()
    {
        LobbyPlayer[] lobbyPlayers = FindObjectsOfType<LobbyPlayer>();
        Debug.Log($"[LobbyManager] Уничтожаем {lobbyPlayers.Length} LobbyPlayer перед переходом на сцену Lobby");
        
        foreach (LobbyPlayer player in lobbyPlayers)
        {
            if (player != null)
            {
                Debug.Log($"[LobbyManager] Уничтожаем LobbyPlayer: {player.name} (netId: {player.netId})");
                
                // Уничтожаем через NetworkServer если это сервер
                if (NetworkServer.active && player.isServer)
                {
                    NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
                    if (identity != null && identity.connectionToClient != null)
                    {
                        Debug.Log($"[LobbyManager] Уничтожаем LobbyPlayer для подключения {identity.connectionToClient.connectionId}");
                        // Используем RemovePlayerForConnection для правильного удаления player object
                        NetworkServer.RemovePlayerForConnection(identity.connectionToClient, RemovePlayerOptions.Destroy);
                        Debug.Log($"[LobbyManager] LobbyPlayer удален из подключения и уничтожен");
                    }
                    else
                    {
                        // Если нет connectionToClient, просто уничтожаем
                        NetworkServer.Destroy(player.gameObject);
                        Debug.Log($"[LobbyManager] LobbyPlayer уничтожен через NetworkServer (без connectionToClient)");
                    }
                }
                else
                {
                    Destroy(player.gameObject);
                    Debug.Log($"[LobbyManager] LobbyPlayer уничтожен через Destroy");
                }
            }
        }
        
        // Проверяем, что все LobbyPlayer уничтожены
        LobbyPlayer[] remainingPlayers = FindObjectsOfType<LobbyPlayer>();
        if (remainingPlayers.Length > 0)
        {
            Debug.LogWarning($"[LobbyManager] ⚠️ После уничтожения осталось {remainingPlayers.Length} LobbyPlayer!");
        }
        else
        {
            Debug.Log($"[LobbyManager] ✓ Все LobbyPlayer успешно уничтожены");
        }
    }
    
    /// <summary>
    /// Покидает лобби
    /// </summary>
    public void LeaveLobby()
    {
        if (currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyID);
        }
        
        if (NetworkServer.active)
        {
            LobbyNetworkManager.Instance.StopHost();
        }
        else if (NetworkClient.active)
        {
            LobbyNetworkManager.Instance.StopClient();
        }
        
        currentLobbyID = CSteamID.Nil;
        isLobbyOwner = false;
        
        // Синхронизируем выход из лобби с SteamLobbyManager
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }
    }
    
}


