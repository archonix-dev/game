using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Localization;
using Steamworks;

/// <summary>
/// Система чата. Открывается по нажатию "/" или "T", блокирует движение игрока и камеру.
/// При отправке сообщения проигрывает звуки для каждого символа.
/// </summary>
public class ChatSystem : NetworkBehaviour
{
    [Header("UI References")]
    [Tooltip("Корневой объект чата (GameObject с Canvas или панелью)")]
    public GameObject chatRoot;
    
    [Tooltip("Поле ввода сообщения")]
    public InputField chatInputField;
    
    [Header("Player References")]
    [Tooltip("Контроллер игрока (для отключения движения)")]
    public PlayerController playerController;
    
    [Tooltip("Контроллер камеры (для отключения вращения камеры)")]
    public MouseLook mouseLook;
    
    [Header("Audio Settings")]
    [Tooltip("AudioSource для проигрывания звуков набора текста")]
    public AudioSource audioSource;
    
    [Tooltip("Звуки для букв A-Z (26 звуков, по порядку)")]
    public AudioClip[] letterSounds = new AudioClip[26];
    
    [Tooltip("Звуки для русских букв (31 звук, по порядку: А, Б, В, Г, Д, Е, Ё, Ж, З, И, Й, К, Л, М, Н, О, П, Р, С, Т, У, Ф, Х, Ц, Ч, Ш, Щ, Ы, Э, Ю, Я). Ъ и Ь не озвучиваются.")]
    public AudioClip[] russianLetterSounds = new AudioClip[31];
    
    [Tooltip("Звуки для английских цифр 0-9 (10 звуков, по порядку)")]
    public AudioClip[] numberSounds = new AudioClip[10];
    
    [Tooltip("Звуки для русских цифр 0-9 (10 звуков, по порядку)")]
    public AudioClip[] russianNumberSounds = new AudioClip[10];
    
    [Tooltip("Звук для знаков препинания и других символов (используется, если нет специфичного звука)")]
    public AudioClip defaultSymbolSound;
    [Tooltip("Задержка для пробелов (в секундах)")]
    [Range(0.01f, 0.5f)]
    public float spaceDelay = 0.1f;
    
    [Header("Chat Message Spawn")]
    [Tooltip("Transform для спавна префабов сообщений чата")]
    public Transform messageSpawnParent;
    
    [Tooltip("Префаб сообщения чата (должен иметь NetworkObject и ChatMessageItem компонент)")]
    public GameObject chatMessagePrefab;
    
    
    private bool isChatOpen = false;
    private Coroutine playMessageSoundsCoroutine;
    private Dictionary<char, AudioClip> characterSoundMap;
    private bool wasEnterPressed = false;
    
    // Система команд с автодополнением
    private List<ChatCommand> availableCommands = new List<ChatCommand>();
    private string currentCommandSuggestion = "";
    private int currentSuggestionIndex = -1;
    private List<string> currentSuggestions = new List<string>();
    private bool IsTypingInChat => isChatOpen && chatInputField != null && chatInputField.isActiveAndEnabled && chatInputField.isFocused;
    
    private CorpseGrabSystem corpseGrabSystem;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log($"[ChatSystem] OnStartClient: isServer={isServer}, isOwned={isOwned}, isClient={isClient}, NetworkIdentity={GetComponent<NetworkIdentity>()?.netId ?? 0}");
        
        // Настраиваем AudioSource в зависимости от владельца
        SetupAudioSource();
        
        // Используем значения по умолчанию для имени и цвета
    }
    
    private bool TryGetLocalPlayerChatInfo(out string playerName, out Color playerColor, out bool isAdmin, out ulong steamId)
    {
        playerName = "Player";
        playerColor = Color.white;
        isAdmin = false;
        steamId = 0;

        LobbyPlayer[] players = FindObjectsOfType<LobbyPlayer>();
        foreach (var lobbyPlayer in players)
        {
            if (lobbyPlayer != null && lobbyPlayer.isLocalPlayer)
            {
                playerName = string.IsNullOrEmpty(lobbyPlayer.playerName) ? playerName : lobbyPlayer.playerName;
                playerColor = lobbyPlayer.GetPlayerColor();
                isAdmin = lobbyPlayer.isOwner;
                steamId = lobbyPlayer.steamID;
                return true;
            }
        }

        try
        {
            if (SteamAPI.IsSteamRunning())
            {
                steamId = SteamUser.GetSteamID().m_SteamID;
                if (PlayerCustomizationStorage.TryGetBySteamId(steamId, out var dataBySteam))
                {
                    playerName = string.IsNullOrEmpty(dataBySteam.playerName) ? playerName : dataBySteam.playerName;
                    playerColor = dataBySteam.PlayerColor;
                    isAdmin = dataBySteam.isOwner;
                    return true;
                }
            }
        }
        catch
        {
            // Игнорируем ошибки Steam API, используем значения по умолчанию
        }

        return false;
    }

    void Start()
    {
        // Автоматически находим UI-компоненты, если они не назначены в инспекторе
        AutoAssignUIReferences();

        // Инициализация: чат закрыт
        SetChatState(false);
        
        EnsurePlayerReferences();
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                // Создаем AudioSource, если его нет
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
        
        // Если не в сети, настраиваем AudioSource локально
        if (netIdentity == null || netIdentity.netId == 0)
        {
            SetupAudioSource();
        }
        
        // Настройка InputField
        if (chatInputField != null)
        {
            // Подписываемся на событие отправки (Enter)
            chatInputField.onEndEdit.AddListener(OnInputFieldEndEdit);
        }
        
        // Инициализация словаря звуков для символов
        InitializeCharacterSoundMap();
        
        // Инициализация системы команд
        InitializeCommands();
        
        // Используем значения по умолчанию для имени и цвета
    }

    /// <summary>
    /// Автоматически находит UI-элементы чата, если они не заданы в инспекторе.
    /// Это позволяет использовать ChatSystem на Prefab'е, не прописывая ссылки вручную.
    /// </summary>
    private void AutoAssignUIReferences()
    {
        // Находим корневой объект чата
        if (chatRoot == null)
        {
            // Сначала пробуем найти Canvas в дочерних объектах
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                chatRoot = canvas.gameObject;
            }
            else
            {
                // Если Canvas нет, используем сам объект с ChatSystem
                chatRoot = gameObject;
            }
        }

        // Находим поле ввода
        if (chatInputField == null && chatRoot != null)
        {
            chatInputField = chatRoot.GetComponentInChildren<InputField>(true);
        }

        // Находим родителя для сообщений
        if (messageSpawnParent == null && chatRoot != null)
        {
            // Пробуем найти по типичным именам
            Transform found = chatRoot.transform.Find("Messages");
            if (found == null) found = chatRoot.transform.Find("MessageContent");
            if (found == null) found = chatRoot.transform.Find("Scroll View/Viewport/Content");

            // Если ничего не нашли, используем корневой объект
            messageSpawnParent = found != null ? found : chatRoot.transform;
        }
    }
    
    private void SetupAudioSource()
    {
        if (audioSource == null) return;
        
        audioSource.spatialBlend = 0f;
        audioSource.spatialize = false;
        audioSource.dopplerLevel = 0f;

        // ВАЖНО: Для визуализации звуки должны работать, но владелец не должен слышать себя
        // Для владельца используем очень маленький volume (почти неслышимый) вместо 0
        // volume = 0 может блокировать воспроизведение в некоторых случаях
        if (netIdentity != null && netIdentity.netId != 0)
        {
            if (isOwned)
            {
                // Владелец не должен слышать свои звуки через AudioSource
                // Используем очень маленький volume (0.0001f) вместо 0, чтобы звуки воспроизводились
                // Это практически неслышимо, но позволяет другим игрокам слышать через сеть
                audioSource.volume = 0.0001f;
                audioSource.mute = false; // НЕ используем mute, чтобы звуки воспроизводились для других
            }
            else
            {
                // Другие игроки могут слышать звуки
                audioSource.volume = 1f;
                audioSource.mute = false;
            }
        }
        else
        {
            // В одиночной игре можно слышать себя
            audioSource.volume = 1f;
            audioSource.mute = false;
        }
    }
    
    
    /// <summary>
    /// Инициализирует словарь сопоставления символов и звуков
    /// </summary>
    private void InitializeCharacterSoundMap()
    {
        characterSoundMap = new Dictionary<char, AudioClip>();
        
        // Добавляем звуки для букв A-Z (заглавные и строчные)
        for (int i = 0; i < 26; i++)
        {
            char upperChar = (char)('A' + i);
            char lowerChar = (char)('a' + i);
            
            if (i < letterSounds.Length && letterSounds[i] != null)
            {
                characterSoundMap[upperChar] = letterSounds[i];
                characterSoundMap[lowerChar] = letterSounds[i];
            }
        }
        
        // Добавляем звуки для русских букв (31 буква, без Ъ и Ь)
        // Порядок: А, Б, В, Г, Д, Е, Ё, Ж, З, И, Й, К, Л, М, Н, О, П, Р, С, Т, У, Ф, Х, Ц, Ч, Ш, Щ, Ы, Э, Ю, Я
        char[] russianUpper = {
            'А', 'Б', 'В', 'Г', 'Д', 'Е', 'Ё', 'Ж', 'З', 'И', 'Й', 'К', 'Л', 'М', 'Н', 'О', 'П', 
            'Р', 'С', 'Т', 'У', 'Ф', 'Х', 'Ц', 'Ч', 'Ш', 'Щ', 'Ы', 'Э', 'Ю', 'Я'
        };
        char[] russianLower = {
            'а', 'б', 'в', 'г', 'д', 'е', 'ё', 'ж', 'з', 'и', 'й', 'к', 'л', 'м', 'н', 'о', 'п', 
            'р', 'с', 'т', 'у', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ы', 'э', 'ю', 'я'
        };
        
        for (int i = 0; i < russianUpper.Length && i < 31; i++)
        {
            if (i < russianLetterSounds.Length && russianLetterSounds[i] != null)
            {
                characterSoundMap[russianUpper[i]] = russianLetterSounds[i];
                characterSoundMap[russianLower[i]] = russianLetterSounds[i];
            }
        }
        
        // Цифры не добавляем в статический словарь - они будут определяться динамически
        // в зависимости от контекста сообщения (русский или английский)
    }
    
    void Update()
    {
        // Не обрабатываем ввод, если чат уже открыт (чтобы избежать конфликтов)
        if (!isChatOpen)
        {
            // Проверяем нажатие "/" или "T" для открытия чата
            // KeyCode.Slash работает для клавиши "/" на большинстве клавиатур
            if (Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.T))
            {
                ToggleChat();
                return;
            }
        }
        else
        {
            // Пока игрок печатает, игнорируем попытки закрыть чат через горячие клавиши
            if ((Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.T)) && !IsTypingInChat)
            {
                SetChatState(false);
                return;
            }

            // Если чат открыт, отслеживаем нажатие Enter
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                wasEnterPressed = true;
            }
            
            // Обработка Tab для автодополнения
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                HandleTabCompletion();
            }
        }
        
        // Закрытие чата по Escape
        if (isChatOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetChatState(false);
        }
    }
    
    /// <summary>
    /// Переключает состояние чата (открыт/закрыт)
    /// </summary>
    public void ToggleChat()
    {
        SetChatState(!isChatOpen);
    }
    
    /// <summary>
    /// Устанавливает состояние чата (открыт/закрыт)
    /// </summary>
    public void SetChatState(bool open)
    {
        EnsurePlayerReferences();
        
        isChatOpen = open;
        
        // Показываем/скрываем UI чата
        if (chatRoot != null)
        {
            chatRoot.SetActive(open);
        }
        
        SetGameplayInputBlocked(open);
        
        if (open)
        {
            // Фокусируемся на поле ввода и активируем его
            // Используем корутину для задержки, чтобы убедиться, что UI готов
            if (chatInputField != null)
            {
                StartCoroutine(FocusInputFieldDelayed());
            }
        }
        else
        {
            // Очищаем поле ввода
            if (chatInputField != null)
            {
                chatInputField.text = "";
                chatInputField.DeactivateInputField();
            }
            
            // Сбрасываем флаг Enter
            wasEnterPressed = false;
        }
    }
    
    /// <summary>
    /// Включает или отключает управление игроком и дополнительные системы, пока открыт чат
    /// </summary>
    private void SetGameplayInputBlocked(bool blocked)
    {
        EnsurePlayerReferences();
        
        if (playerController != null)
        {
            playerController.enabled = !blocked;
        }
        
        if (mouseLook != null)
        {
            mouseLook.enabled = !blocked;
        }
        
        if (corpseGrabSystem != null)
        {
            corpseGrabSystem.enabled = !blocked;
        }
        
        Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = blocked;
    }
    
    /// <summary>
    /// Обновляет ссылки на компоненты игрока, если они еще не найдены
    /// </summary>
    private void EnsurePlayerReferences()
    {
        if (playerController == null)
        {
            playerController = FindOwnedComponent<PlayerController>();
        }
        
        if (mouseLook == null)
        {
            mouseLook = FindOwnedComponent<MouseLook>();
        }
        
        if (corpseGrabSystem == null)
        {
            corpseGrabSystem = FindOwnedComponent<CorpseGrabSystem>();
        }
    }
    
    private T FindOwnedComponent<T>() where T : NetworkBehaviour
    {
        T[] components = FindObjectsOfType<T>(true);
        T fallback = null;
        foreach (var component in components)
        {
            if (component == null) continue;
            
            if (component.netIdentity == null || component.netIdentity.netId == 0)
            {
                return component;
            }
            
            if (component.isOwned)
            {
                return component;
            }
            
            if (fallback == null)
            {
                fallback = component;
            }
        }
        return fallback;
    }
    
    /// <summary>
    /// Вызывается при потере фокуса InputField или отправке (Enter)
    /// </summary>
    private void OnInputFieldEndEdit(string text)
    {
        // Проверяем, была ли нажата клавиша Enter
        if (wasEnterPressed)
        {
            wasEnterPressed = false; // Сбрасываем флаг
            
            if (string.IsNullOrEmpty(text))
            {
                // Если сообщение пустое, просто закрываем чат
                SetChatState(false);
                return;
            }
            
            // Проверяем команды (начинаются с /)
            if (text.StartsWith("/"))
            {
                HandleCommand(text);
                
                // Очищаем поле ввода и закрываем чат
                if (chatInputField != null)
                {
                    chatInputField.text = "";
                }
                SetChatState(false);
                return; // Выходим, не проигрывая звуки для команд
            }
            
            // Отправляем сообщение в сеть
            bool isSpawned = netIdentity != null && netIdentity.netId != 0;
            if (isSpawned && isClient)
            {
                string playerNameToSend = "Player";
                Color playerColorToSend = Color.white;
                bool isAdminToSend = false;
                ulong steamIdToSend = 0;

                TryGetLocalPlayerChatInfo(out playerNameToSend, out playerColorToSend, out isAdminToSend, out steamIdToSend);

                // Отправляем сообщение на сервер для спавна префаба
                Debug.Log($"[ChatSystem] Отправка Command: text={text}, isSpawned={isSpawned}, isClient={isClient}, playerName={playerNameToSend}");
                SendChatMessageCommand(text, playerNameToSend, playerColorToSend, isAdminToSend, steamIdToSend);
                
            }
            else if (netIdentity == null || netIdentity.netId == 0)
            {
                // В одиночной игре создаем локально
                Debug.Log($"[ChatSystem] Одиночная игра, создание локального сообщения: text={text}");
                
                // Получаем имя и цвет игрока
                string playerName = "Player";
                Color playerColor = Color.white;
                bool isAdmin = false;
                
                // Пытаемся найти LobbyPlayer для локального игрока
                LobbyPlayer[] allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
                if (allLobbyPlayers != null && allLobbyPlayers.Length > 0)
                {
                    // Берем первого найденного LobbyPlayer (в одиночной игре обычно один)
                    LobbyPlayer lobbyPlayer = allLobbyPlayers[0];
                    if (lobbyPlayer != null)
                    {
                        playerName = lobbyPlayer.playerName;
                        playerColor = lobbyPlayer.GetPlayerColor();
                        isAdmin = lobbyPlayer.isOwner;
                        Debug.Log($"[ChatSystem] Найден LobbyPlayer для одиночной игры: {playerName}, цвет: {playerColor}, isOwner: {isAdmin}");
                    }
                }
                
                // Создаем локально
                SpawnChatMessageLocally(text, playerName, playerColor, 0, isAdmin);
                
            }
            else
            {
                bool isSpawnedLocal = netIdentity != null && netIdentity.netId != 0;
                Debug.LogWarning($"[ChatSystem] ChatSystem не заспавнен или не является владельцем! isSpawned={isSpawnedLocal}, isOwned={isOwned}");
            }
            
            // Очищаем поле ввода и закрываем чат
            if (chatInputField != null)
            {
                chatInputField.text = "";
            }
            SetChatState(false);
        }
        // Если просто потеряли фокус без Enter, ничего не делаем (чат остается открытым)
    }
    
    /// <summary>
    /// Инициализирует список доступных команд
    /// </summary>
    private void InitializeCommands()
    {
        availableCommands = new List<ChatCommand>
        {
            new ChatCommand("stamina", "Добавить стамину игроку", new[] { "playerId", "amount" }, ExecuteStaminaCommand),
            new ChatCommand("health", "Добавить здоровье игроку", new[] { "playerId", "amount" }, ExecuteHealthCommand),
            new ChatCommand("kill", "Убить игрока", new[] { "playerId" }, ExecuteKillCommand),
            new ChatCommand("tp", "Телепорт к игроку", new[] { "playerId" }, ExecuteTeleportCommand),
            new ChatCommand("teleport", "Телепорт к игроку", new[] { "playerId" }, ExecuteTeleportCommand),
            new ChatCommand("give", "Дать предмет", new[] { "itemName", "amount" }, ExecuteGiveCommand),
            new ChatCommand("spawn", "Заспавнить объект", new[] { "objectName" }, ExecuteSpawnCommand),
            new ChatCommand("fly", "Включить/выключить полет", new string[0], ExecuteFlyCommand),
            new ChatCommand("god", "Включить/выключить бессмертие", new string[0], ExecuteGodCommand),
            new ChatCommand("speed", "Установить скорость", new[] { "value" }, ExecuteSpeedCommand),
            new ChatCommand("clear", "Очистить инвентарь", new string[0], ExecuteClearCommand),
            new ChatCommand("money", "Дать деньги", new[] { "amount" }, ExecuteMoneyCommand),
            new ChatCommand("kick", "Кикнуть игрока", new[] { "playerId" }, ExecuteKickCommand),
            new ChatCommand("ban", "Забанить игрока", new[] { "playerId" }, ExecuteBanCommand),
            new ChatCommand("heal", "Полностью вылечить игрока", new[] { "playerId" }, ExecuteHealCommand),
            new ChatCommand("maxhealth", "Установить максимальное здоровье", new[] { "playerId", "amount" }, ExecuteMaxHealthCommand),
            new ChatCommand("maxstamina", "Установить максимальную стамину", new[] { "playerId", "amount" }, ExecuteMaxStaminaCommand),
            new ChatCommand("list", "Список игроков", new string[0], ExecuteListCommand),
            new ChatCommand("help", "Список команд", new string[0], ExecuteHelpCommand)
        };
    }
    
    /// <summary>
    /// Обрабатывает команды чата (начинаются с /)
    /// </summary>
    private void HandleCommand(string command)
    {
        // Проверяем, включены ли читы
        if (LobbyManager.Instance == null || !LobbyManager.Instance.cheatsEnabled)
        {
            Debug.LogWarning("[ChatSystem] Читы отключены в настройках лобби!");
            return;
        }
        
        // Проверяем, является ли игрок владельцем лобби или админом
        bool isOwner = false;
        if (LobbyManager.Instance != null)
        {
            isOwner = LobbyManager.Instance.IsLobbyOwner;
        }
        
        // Также проверяем через LobbyPlayer
        LobbyPlayer localPlayer = FindObjectOfType<LobbyPlayer>();
        if (localPlayer != null && localPlayer.isOwner)
        {
            isOwner = true;
        }
        
        if (!isOwner)
        {
            Debug.LogWarning("[ChatSystem] Только владелец лобби может использовать команды!");
            return;
        }
        
        // Разбиваем команду на части
        string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        
        string commandName = parts[0].ToLowerInvariant();
        
        // Ищем команду
        ChatCommand cmd = availableCommands.FirstOrDefault(c => c.name == commandName);
        if (cmd == null)
        {
            Debug.Log($"[ChatSystem] Неизвестная команда: {commandName}. Используйте /help для списка команд.");
            return;
        }
        
        // Выполняем команду
        string[] args = parts.Skip(1).ToArray();
        cmd.Execute(args, this);
    }
    
    /// <summary>
    /// Обрабатывает автодополнение по Tab
    /// </summary>
    private void HandleTabCompletion()
    {
        if (chatInputField == null) return;
        
        string currentText = chatInputField.text;
        if (string.IsNullOrEmpty(currentText) || !currentText.StartsWith("/"))
        {
            return;
        }
        
        // Получаем текущую позицию курсора
        int caretPosition = chatInputField.caretPosition;
        
        // Находим начало команды
        int commandStart = currentText.LastIndexOf('/', caretPosition - 1);
        if (commandStart == -1) return;
        
        // Получаем текст от начала команды до курсора
        string textBeforeCaret = currentText.Substring(commandStart, caretPosition - commandStart);
        
        // Разбиваем на части
        string[] parts = textBeforeCaret.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0) return;
        
        string commandPart = parts[0].Substring(1).ToLowerInvariant(); // Убираем "/"
        
        // Если это первая часть (название команды)
        if (parts.Length == 1)
        {
            // Ищем команды, начинающиеся с этого текста
            var matchingCommands = availableCommands
                .Where(c => c.name.StartsWith(commandPart))
                .Select(c => c.name)
                .ToList();
            
            if (matchingCommands.Count == 0) return;
            
            // Если только одна команда, дополняем её
            if (matchingCommands.Count == 1)
            {
                string fullCommand = "/" + matchingCommands[0];
                string newText = currentText.Substring(0, commandStart) + fullCommand + " " + currentText.Substring(caretPosition);
                chatInputField.text = newText;
                chatInputField.caretPosition = commandStart + fullCommand.Length + 1;
            }
            else
            {
                // Если несколько команд, циклически переключаемся между ними
                if (currentSuggestionIndex == -1 || !matchingCommands.Contains(currentCommandSuggestion))
                {
                    currentSuggestionIndex = 0;
                    currentCommandSuggestion = matchingCommands[0];
                }
                else
                {
                    currentSuggestionIndex = (currentSuggestionIndex + 1) % matchingCommands.Count;
                    currentCommandSuggestion = matchingCommands[currentSuggestionIndex];
                }
                
                string fullCommand = "/" + currentCommandSuggestion;
                string newText = currentText.Substring(0, commandStart) + fullCommand + " " + currentText.Substring(caretPosition);
                chatInputField.text = newText;
                chatInputField.caretPosition = commandStart + fullCommand.Length + 1;
            }
        }
        else
        {
            // Если это параметр команды
            ChatCommand cmd = availableCommands.FirstOrDefault(c => c.name == commandPart);
            if (cmd != null && cmd.parameters.Length > parts.Length - 1)
            {
                string paramName = cmd.parameters[parts.Length - 1];
                
                // Если параметр - playerId, автозаполняем Steam ID
                if (paramName == "playerId")
                {
                    string lastPart = parts[parts.Length - 1];
                    var players = GetOnlinePlayers();
                    
                    // Ищем игроков по ID или имени
                    var matchingPlayers = players
                        .Where(p => 
                            p.steamID.ToString().StartsWith(lastPart) || 
                            p.playerName.ToLowerInvariant().StartsWith(lastPart.ToLowerInvariant()))
                        .ToList();
                    
                    if (matchingPlayers.Count > 0)
                    {
                        // Если несколько совпадений, циклически переключаемся
                        LobbyPlayer selectedPlayer = matchingPlayers[0];
                        if (matchingPlayers.Count > 1)
                        {
                            // Находим текущего выбранного игрока (если есть)
                            int currentIndex = matchingPlayers.FindIndex(p => p.steamID.ToString() == lastPart || p.playerName.ToLowerInvariant() == lastPart.ToLowerInvariant());
                            if (currentIndex >= 0)
                            {
                                currentIndex = (currentIndex + 1) % matchingPlayers.Count;
                            }
                            else
                            {
                                currentIndex = 0;
                            }
                            selectedPlayer = matchingPlayers[currentIndex];
                        }
                        
                        ulong playerId = selectedPlayer.steamID;
                        string newText = currentText.Substring(0, caretPosition - lastPart.Length) + playerId.ToString() + " " + currentText.Substring(caretPosition);
                        chatInputField.text = newText;
                        chatInputField.caretPosition = caretPosition - lastPart.Length + playerId.ToString().Length + 1;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Получает список онлайн игроков
    /// </summary>
    private List<LobbyPlayer> GetOnlinePlayers()
    {
        return FindObjectsOfType<LobbyPlayer>().ToList();
    }
    
    /// <summary>
    /// Проигрывает звуки для каждого символа сообщения
    /// </summary>
    private void PlayMessageSounds(string message)
    {
        // Не проигрываем звуки для команд (начинаются с /)
        if (string.IsNullOrEmpty(message) || message.StartsWith("/"))
        {
            return;
        }
        
        // Останавливаем предыдущую корутину, если она еще выполняется
        if (playMessageSoundsCoroutine != null)
        {
            StopCoroutine(playMessageSoundsCoroutine);
        }
        
        playMessageSoundsCoroutine = StartCoroutine(PlayMessageSoundsCoroutine(message));
    }
    
    /// <summary>
    /// Корутина для проигрывания звуков символов
    /// </summary>
    private IEnumerator PlayMessageSoundsCoroutine(string message)
    {
        if (audioSource == null || characterSoundMap == null)
        {
            yield break;
        }
        
        // Проверяем, содержит ли сообщение только цифры (и пробелы)
        bool isOnlyDigits = ContainsOnlyDigits(message);
        
        // Отслеживаем контекст сообщения (русский или английский)
        // на основе предыдущих букв или локализации (если только цифры)
        bool isRussianContext = false;
        int russianLetterCount = 0;
        int englishLetterCount = 0;
        
        // Если сообщение содержит только цифры, используем язык из LocalizationManager
        if (isOnlyDigits)
        {
            isRussianContext = IsRussianLanguage();
        }
        
        // Разбиваем сообщение на слова и символы
        string[] words = SplitMessageIntoWords(message);
        
        foreach (string word in words)
        {
            if (string.IsNullOrEmpty(word))
                continue;
            
            // Проверяем, является ли это пробелом
            if (word == " ")
            {
                yield return new WaitForSeconds(spaceDelay);
                continue;
            }
            
            // Определяем контекст для слова (если не только цифры)
            if (!isOnlyDigits)
            {
                foreach (char c in word)
                {
                    UpdateContext(c, ref russianLetterCount, ref englishLetterCount, ref isRussianContext);
                }
            }
            
            // Проигрываем по буквам
            foreach (char character in word)
            {
                if (character == 'Ъ' || character == 'ъ' || character == 'Ь' || character == 'ь')
                {
                    // Для Ъ и Ь (твердый и мягкий знаки) пропускаем без звука и без паузы
                    continue;
                }
                
                // Получаем звук для текущего символа с учетом контекста
                AudioClip clipToPlay = GetSoundForCharacter(character, isRussianContext);
                
                if (clipToPlay != null)
                {
                    // Проигрываем звук
                    audioSource.PlayOneShot(clipToPlay);
                    
                    // Ждем полную длину звука перед переходом к следующему символу
                    yield return new WaitForSeconds(clipToPlay.length);
                }
            }
        }
        
        playMessageSoundsCoroutine = null;
    }
    
    // ========== ОБРАБОТЧИКИ КОМАНД ==========
    
    private void ExecuteStaminaCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("[ChatSystem] Использование: /stamina <playerId> <amount>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId) && float.TryParse(args[1], out float amount))
        {
            ExecuteStaminaCommandServer(playerId, amount);
        }
    }
    
    private void ExecuteHealthCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("[ChatSystem] Использование: /health <playerId> <amount>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId) && float.TryParse(args[1], out float amount))
        {
            ExecuteHealthCommandServer(playerId, amount);
        }
    }
    
    private void ExecuteKillCommand(string[] args, ChatSystem chatSystem)
    {
        ulong targetId = 0;
        if (args.Length > 0 && ulong.TryParse(args[0], out targetId))
        {
            ExecuteKillCommandServer(targetId);
        }
        else if (playerController != null)
        {
            // Если не указан ID, убиваем себя
            playerController.KillPlayer();
        }
    }
    
    private void ExecuteTeleportCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /tp <playerId>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong targetId))
        {
            ExecuteTeleportCommandServer(targetId);
        }
    }
    
    private void ExecuteGiveCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /give <itemName> [amount]");
            return;
        }
        
        int amount = 1;
        if (args.Length > 1 && int.TryParse(args[1], out int parsedAmount))
        {
            amount = parsedAmount;
        }
        
        ExecuteGiveCommandServer(args[0], amount);
    }
    
    private void ExecuteSpawnCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /spawn <objectName>");
            return;
        }
        
        ExecuteSpawnCommandServer(args[0]);
    }
    
    private void ExecuteFlyCommand(string[] args, ChatSystem chatSystem)
    {
        ExecuteFlyCommandServer();
    }
    
    private void ExecuteGodCommand(string[] args, ChatSystem chatSystem)
    {
        ExecuteGodCommandServer();
    }
    
    private void ExecuteSpeedCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /speed <value>");
            return;
        }
        
        if (float.TryParse(args[0], out float speed))
        {
            ExecuteSpeedCommandServer(speed);
        }
    }
    
    private void ExecuteClearCommand(string[] args, ChatSystem chatSystem)
    {
        ExecuteClearCommandServer();
    }
    
    private void ExecuteMoneyCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /money <amount>");
            return;
        }
        
        if (int.TryParse(args[0], out int amount))
        {
            ExecuteMoneyCommandServer(amount);
        }
    }
    
    private void ExecuteKickCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /kick <playerId>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId))
        {
            ExecuteKickCommandServer(playerId);
        }
    }
    
    private void ExecuteBanCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 1)
        {
            Debug.LogWarning("[ChatSystem] Использование: /ban <playerId>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId))
        {
            ExecuteBanCommandServer(playerId);
        }
    }
    
    private void ExecuteHealCommand(string[] args, ChatSystem chatSystem)
    {
        ulong targetId = 0;
        if (args.Length > 0 && ulong.TryParse(args[0], out targetId))
        {
            ExecuteHealCommandServer(targetId);
        }
        else if (playerController != null)
        {
            // Если не указан ID, лечим себя
            var healthStamina = playerController.GetComponent<PlayerHealthStamina>();
            if (healthStamina != null)
            {
                float maxHealth = healthStamina.GetMaxHealth();
                healthStamina.Heal(maxHealth);
            }
        }
    }
    
    private void ExecuteMaxHealthCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("[ChatSystem] Использование: /maxhealth <playerId> <amount>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId) && float.TryParse(args[1], out float amount))
        {
            ExecuteMaxHealthCommandServer(playerId, amount);
        }
    }
    
    private void ExecuteMaxStaminaCommand(string[] args, ChatSystem chatSystem)
    {
        if (args.Length < 2)
        {
            Debug.LogWarning("[ChatSystem] Использование: /maxstamina <playerId> <amount>");
            return;
        }
        
        if (ulong.TryParse(args[0], out ulong playerId) && float.TryParse(args[1], out float amount))
        {
            ExecuteMaxStaminaCommandServer(playerId, amount);
        }
    }
    
    private void ExecuteListCommand(string[] args, ChatSystem chatSystem)
    {
        ExecuteListCommandServer();
    }
    
    private void ExecuteHelpCommand(string[] args, ChatSystem chatSystem)
    {
        Debug.Log("[ChatSystem] === Доступные команды ===");
        foreach (var cmd in availableCommands)
        {
            string paramsStr = cmd.parameters.Length > 0 ? string.Join(" ", cmd.parameters.Select(p => $"<{p}>")) : "";
            Debug.Log($"/{cmd.name} {paramsStr} - {cmd.description}");
        }
    }
    
    // ========== СЕРВЕРНЫЕ КОМАНДЫ ==========
    
    [Command]
    private void ExecuteStaminaCommandServer(ulong playerId, float amount)
    {
        // Проверяем права доступа на сервере
        if (!IsCommandAllowed())
        {
            Debug.LogWarning("[ChatSystem] У вас нет прав для выполнения команд!");
            return;
        }
        
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                var healthStamina = playerObj.GetComponent<PlayerHealthStamina>();
                if (healthStamina != null)
                {
                    // Добавляем стамину
                    healthStamina.AddStamina(amount);
                }
            }
        }
    }
    
    /// <summary>
    /// Проверяет, разрешено ли выполнение команд
    /// </summary>
    private bool IsCommandAllowed()
    {
        // Проверяем, включены ли читы
        if (LobbyManager.Instance == null || !LobbyManager.Instance.cheatsEnabled)
        {
            return false;
        }
        
        // Проверяем, является ли игрок владельцем
        LobbyPlayer localPlayer = FindObjectOfType<LobbyPlayer>();
        if (localPlayer != null && localPlayer.isOwner)
        {
            return true;
        }
        
        if (LobbyManager.Instance != null && LobbyManager.Instance.IsLobbyOwner)
        {
            return true;
        }
        
        return false;
    }
    
    [Command]
    private void ExecuteHealthCommandServer(ulong playerId, float amount)
    {
        if (!IsCommandAllowed()) return;
        
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                var healthStamina = playerObj.GetComponent<PlayerHealthStamina>();
                if (healthStamina != null)
                {
                    healthStamina.Heal(amount);
                }
            }
        }
    }
    
    [Command]
    private void ExecuteKillCommandServer(ulong playerId)
    {
        if (!IsCommandAllowed()) return;
        
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                playerObj.KillPlayer();
            }
        }
    }
    
    [Command]
    private void ExecuteTeleportCommandServer(ulong targetId)
    {
        if (!IsCommandAllowed()) return;
        
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(targetId);
        if (targetPlayer != null && playerController != null)
        {
            var targetPlayerObj = targetPlayer.GetComponent<PlayerController>();
            if (targetPlayerObj != null)
            {
                playerController.transform.position = targetPlayerObj.transform.position;
            }
        }
    }
    
    [Command]
    private void ExecuteGiveCommandServer(string itemName, int amount)
    {
        // Реализация выдачи предметов
        Debug.Log($"[ChatSystem] Команда /give {itemName} {amount} выполнена");
    }
    
    [Command]
    private void ExecuteSpawnCommandServer(string objectName)
    {
        // Реализация спавна объектов
        Debug.Log($"[ChatSystem] Команда /spawn {objectName} выполнена");
    }
    
    [Command]
    private void ExecuteFlyCommandServer()
    {
        // Реализация полета
        Debug.Log("[ChatSystem] Команда /fly выполнена");
    }
    
    [Command]
    private void ExecuteGodCommandServer()
    {
        // Реализация бессмертия
        Debug.Log("[ChatSystem] Команда /god выполнена");
    }
    
    [Command]
    private void ExecuteSpeedCommandServer(float speed)
    {
        if (playerController != null)
        {
            // Реализация изменения скорости
            Debug.Log($"[ChatSystem] Команда /speed {speed} выполнена");
        }
    }
    
    [Command]
    private void ExecuteClearCommandServer()
    {
        var inventory = GetComponent<InventorySystem>();
        if (inventory != null)
        {
            // Реализация очистки инвентаря
            Debug.Log("[ChatSystem] Команда /clear выполнена");
        }
    }
    
    [Command]
    private void ExecuteMoneyCommandServer(int amount)
    {
        if (!IsCommandAllowed()) return;
        
        var coinManager = GetComponent<CoinManager>();
        if (coinManager != null)
        {
            coinManager.AddCoins(amount);
        }
    }
    
    [Command]
    private void ExecuteKickCommandServer(ulong playerId)
    {
        if (!IsCommandAllowed()) return;
        
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null && targetPlayer.connectionToClient != null)
        {
            targetPlayer.connectionToClient.Disconnect();
        }
    }
    
    [Command]
    private void ExecuteBanCommandServer(ulong playerId)
    {
        // Реализация бана (требует системы банов)
        Debug.Log($"[ChatSystem] Команда /ban {playerId} выполнена");
    }
    
    [Command]
    private void ExecuteHealCommandServer(ulong playerId)
    {
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                var healthStamina = playerObj.GetComponent<PlayerHealthStamina>();
                if (healthStamina != null)
                {
                    float maxHealth = healthStamina.GetMaxHealth();
                    healthStamina.Heal(maxHealth);
                }
            }
        }
    }
    
    [Command]
    private void ExecuteMaxHealthCommandServer(ulong playerId, float amount)
    {
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                var healthStamina = playerObj.GetComponent<PlayerHealthStamina>();
                if (healthStamina != null)
                {
                    float currentMax = healthStamina.GetMaxHealth();
                    healthStamina.IncreaseMaxHealth(amount - currentMax);
                }
            }
        }
    }
    
    [Command]
    private void ExecuteMaxStaminaCommandServer(ulong playerId, float amount)
    {
        var targetPlayer = LobbyPlayer.GetPlayerBySteamID(playerId);
        if (targetPlayer != null)
        {
            var playerObj = targetPlayer.GetComponent<PlayerController>();
            if (playerObj != null)
            {
                var healthStamina = playerObj.GetComponent<PlayerHealthStamina>();
                if (healthStamina != null)
                {
                    float currentMax = healthStamina.GetMaxStamina();
                    healthStamina.IncreaseMaxStamina(amount - currentMax);
                }
            }
        }
    }
    
    [Command]
    private void ExecuteListCommandServer()
    {
        var players = GetOnlinePlayers();
        Debug.Log($"[ChatSystem] === Онлайн игроки ({players.Count}) ===");
        foreach (var player in players)
        {
            Debug.Log($"- {player.playerName} (Steam ID: {player.steamID})");
        }
    }
    
    /// <summary>
    /// Разбивает сообщение на слова и пробелы
    /// </summary>
    private string[] SplitMessageIntoWords(string message)
    {
        List<string> result = new List<string>();
        System.Text.StringBuilder currentWord = new System.Text.StringBuilder();
        
        foreach (char c in message)
        {
            if (char.IsWhiteSpace(c))
            {
                if (currentWord.Length > 0)
                {
                    result.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                result.Add(" "); // Добавляем пробел как отдельный элемент
            }
            else
            {
                currentWord.Append(c);
            }
        }
        
        if (currentWord.Length > 0)
        {
            result.Add(currentWord.ToString());
        }
        
        return result.ToArray();
    }
    
    /// <summary>
    /// Проверяет, содержит ли сообщение только цифры и пробелы
    /// </summary>
    private bool ContainsOnlyDigits(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;
        
        foreach (char c in message)
        {
            // Разрешаем только цифры и пробелы
            if (c != ' ' && (c < '0' || c > '9'))
            {
                return false;
            }
        }
        
        // Проверяем, что есть хотя бы одна цифра
        foreach (char c in message)
        {
            if (c >= '0' && c <= '9')
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Проверяет, является ли текущий язык игры русским
    /// </summary>
    private bool IsRussianLanguage()
    {
        // Используем LocalizationManager для получения языка
        if (LocalizationManager.Instance == null)
            return false;
        
        string language = LocalizationManager.Instance.GetCurrentLanguage();
        
        if (string.IsNullOrEmpty(language))
            return false;
        
        // Проверяем различные варианты названия русского языка
        // Порядок языков: Русский, Английский, Немецкий, Французский, Испанский, Итальянский, Китайский
        language = language.ToLowerInvariant();
        
        // Проверяем на русский язык
        return language.Contains("русск") || 
               language.Contains("ru") || 
               language == "russian" ||
               language.StartsWith("ru_", System.StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Обновляет контекст сообщения (русский/английский) на основе символа
    /// </summary>
    private void UpdateContext(char character, ref int russianCount, ref int englishCount, ref bool isRussian)
    {
        // Пропускаем Ъ и Ь (твердый и мягкий знаки) - они не влияют на контекст
        if (character == 'Ъ' || character == 'ъ' || character == 'Ь' || character == 'ь')
        {
            return;
        }
        
        // Проверяем, является ли символ русской буквой
        if ((character >= 'А' && character <= 'Я') || (character >= 'а' && character <= 'я') || 
            character == 'Ё' || character == 'ё')
        {
            russianCount++;
        }
        // Проверяем, является ли символ английской буквой
        else if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z'))
        {
            englishCount++;
        }
        
        // Определяем контекст: если русских букв больше или равно английским, используем русский контекст
        // Если еще не было букв, оставляем предыдущий контекст
        if (russianCount > 0 || englishCount > 0)
        {
            isRussian = russianCount >= englishCount;
        }
    }
    
    /// <summary>
    /// Получает звук для указанного символа с учетом контекста
    /// </summary>
    private AudioClip GetSoundForCharacter(char character, bool isRussianContext)
    {
        // Проверяем, является ли символ цифрой
        if (character >= '0' && character <= '9')
        {
            int digitIndex = character - '0';
            
            // Выбираем звук в зависимости от контекста
            if (isRussianContext)
            {
                // Используем русские звуки для цифр
                if (digitIndex < russianNumberSounds.Length && russianNumberSounds[digitIndex] != null)
                {
                    return russianNumberSounds[digitIndex];
                }
            }
            else
            {
                // Используем английские звуки для цифр
                if (digitIndex < numberSounds.Length && numberSounds[digitIndex] != null)
                {
                    return numberSounds[digitIndex];
                }
            }
            
            // Если звука нет в нужном массиве, пробуем другой массив
            if (isRussianContext && digitIndex < numberSounds.Length && numberSounds[digitIndex] != null)
            {
                return numberSounds[digitIndex];
            }
            else if (!isRussianContext && digitIndex < russianNumberSounds.Length && russianNumberSounds[digitIndex] != null)
            {
                return russianNumberSounds[digitIndex];
            }
        }
        
        // Проверяем, есть ли звук для этого символа в словаре (буквы)
        if (characterSoundMap != null && characterSoundMap.ContainsKey(character))
        {
            return characterSoundMap[character];
        }
        
        // Если звука нет, используем звук по умолчанию
        return defaultSymbolSound;
    }
    
    /// <summary>
    /// Корутина для задержки фокусировки InputField (чтобы UI успел обновиться)
    /// </summary>
    private IEnumerator FocusInputFieldDelayed()
    {
        yield return null; // Ждем один кадр
        
        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
            chatInputField.Select();
            // Очищаем текст, если он был введен при открытии (например, "/")
            chatInputField.text = "";
        }
    }
    
    /// <summary>
    /// ServerRpc для отправки сообщения в чат (вызывается любым клиентом, независимо от владения объектом)
    /// </summary>
    [Command(requiresAuthority = false)]
    private void SendChatMessageCommand(string message, string fallbackPlayerName, Color fallbackPlayerColor, bool fallbackIsAdmin, ulong fallbackSteamId, NetworkConnectionToClient sender = null)
    {
        // В Mirror, когда клиент вызывает Command, sender автоматически передается
        // Если sender null, используем connectionToClient из этого NetworkIdentity
        NetworkConnectionToClient actualSender = sender;
        if (actualSender == null && netIdentity != null && netIdentity.connectionToClient != null)
        {
            actualSender = netIdentity.connectionToClient;
        }
        
        int senderConnectionId = actualSender != null ? actualSender.connectionId : -1;
        uint senderId = senderConnectionId >= 0 ? (uint)senderConnectionId : 0;
        
        Debug.Log($"[ChatSystem] Command получен: message={message}, senderId={senderId}, sender={(actualSender != null ? "NOT NULL" : "NULL")}, connectionToClient={(netIdentity?.connectionToClient != null ? "NOT NULL" : "NULL")}");
        
        string playerName = string.IsNullOrWhiteSpace(fallbackPlayerName) ? "Player" : fallbackPlayerName;
        Color playerColor = fallbackPlayerColor;
        bool isAdmin = fallbackIsAdmin;
        ulong steamId = fallbackSteamId;
        
        // Пытаемся найти LobbyPlayer для отправителя
        LobbyPlayer lobbyPlayer = null;
        
        if (actualSender != null)
        {
            if (actualSender.identity != null)
            {
                lobbyPlayer = actualSender.identity.GetComponent<LobbyPlayer>();
            }
            
            if (lobbyPlayer == null)
            {
                LobbyPlayer[] allLobbyPlayers = FindObjectsOfType<LobbyPlayer>();
                foreach (LobbyPlayer lp in allLobbyPlayers)
                {
                    if (lp.connectionToClient != null && lp.connectionToClient.connectionId == actualSender.connectionId)
                    {
                        lobbyPlayer = lp;
                        break;
                    }
                }
            }
        }
        
        if (lobbyPlayer != null)
        {
            playerName = string.IsNullOrEmpty(lobbyPlayer.playerName) ? playerName : lobbyPlayer.playerName;
            playerColor = lobbyPlayer.GetPlayerColor();
            isAdmin = lobbyPlayer.isOwner;
            steamId = lobbyPlayer.steamID;
            Debug.Log($"[ChatSystem] Найден LobbyPlayer: {playerName}, цвет: {playerColor}, isOwner: {isAdmin}");
        }
        else
        {
            bool customizationFound = false;
            if (senderConnectionId >= 0 &&
                PlayerCustomizationStorage.TryGetByConnectionId(senderConnectionId, out PlayerCustomizationStorage.PlayerCustomizationData cachedData))
            {
                customizationFound = true;
                playerName = string.IsNullOrEmpty(cachedData.playerName) ? playerName : cachedData.playerName;
                playerColor = cachedData.PlayerColor;
                isAdmin = cachedData.isOwner;
                steamId = cachedData.steamId;
                Debug.Log($"[ChatSystem] Используем кешированные данные игрока: {playerName}, цвет: {playerColor}, isOwner: {isAdmin}");
            }

            if (!customizationFound)
            {
                if (steamId == 0 && fallbackSteamId != 0)
                {
                    steamId = fallbackSteamId;
                }

                if (steamId != 0 && PlayerCustomizationStorage.TryGetBySteamId(steamId, out var cachedBySteam))
                {
                    playerName = string.IsNullOrEmpty(cachedBySteam.playerName) ? playerName : cachedBySteam.playerName;
                    playerColor = cachedBySteam.PlayerColor;
                    isAdmin = cachedBySteam.isOwner;
                }
                else
                {
                    if (actualSender != null)
                    {
                        isAdmin = NetworkServer.activeHost && actualSender.connectionId == 0;
                    }
                    else if (senderId == 0 && NetworkServer.activeHost)
                    {
                        isAdmin = true;
                    }
                    
                    Debug.LogWarning($"[ChatSystem] Данные игрока не найдены для connectionId {senderId}, используем переданные значения");
                }
            }
        }
        
        Debug.Log($"[ChatSystem] Финальное имя для отправки: {playerName}, цвет: {playerColor}, isAdmin: {isAdmin}");
        
        // Отправляем сообщение всем клиентам через ClientRpc
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        bool isSpawned = networkIdentity != null && networkIdentity.netId != 0;
        Debug.Log($"[ChatSystem] Отправка ClientRpc: message={message}, playerName={playerName}, senderId={senderId}, isSpawned={isSpawned}, netId={networkIdentity?.netId ?? 0}, isServer={isServer}");
        
        if (networkIdentity == null || networkIdentity.netId == 0)
        {
            bool netIsSpawned = networkIdentity != null && networkIdentity.netId != 0;
            Debug.LogError($"[ChatSystem] NetworkIdentity не найден или не заспавнен! networkIdentity={networkIdentity}, isSpawned={netIsSpawned}");
            return;
        }
        
        ReceiveChatMessageClientRpc(playerName, message, playerColor, senderId, isAdmin);
    }
    
    /// <summary>
    /// ClientRpc для получения сообщения чата (вызывается сервером, получают все клиенты)
    /// </summary>
    [ClientRpc]
    public void ReceiveChatMessageClientRpc(string playerName, string message, Color playerColor, uint clientId, bool isAdmin)
    {
        bool isSpawned = netIdentity != null && netIdentity.netId != 0;
        Debug.Log($"[ChatSystem] ReceiveChatMessageClientRpc получен: message={message}, playerName={playerName}, isSpawned={isSpawned}, isOwned={isOwned}, isClient={isClient}, name={gameObject.name}, netId={GetComponent<NetworkIdentity>()?.netId ?? 0}");
        
        // Каждый клиент создает локальный UI элемент
        SpawnChatMessageLocally(message, playerName, playerColor, clientId, isAdmin);
        
        // Проигрываем звуки набора текста для других игроков
        // Отправитель уже проиграл звуки локально в OnInputFieldEndEdit, но не слышит их из-за SetupAudioSource
        // ClientRpc вызывается для всех клиентов, включая отправителя
        // Если это наш ChatSystem (isOwned), мы уже проиграли звуки локально, поэтому не проигрываем их снова
        if (!message.StartsWith("/") && !isOwned)
        {
            // Это сообщение от другого игрока - проигрываем звуки
            PlayMessageSounds(message);
        }
    }
    
    
    /// <summary>
    /// Спавнит префаб сообщения чата локально на каждом клиенте
    /// </summary>
    private void SpawnChatMessageLocally(string message, string playerName, Color playerColor, uint senderId, bool isAdmin)
    {
        Debug.Log($"[ChatSystem] SpawnChatMessageLocally вызван: message={message}, playerName={playerName}, messageSpawnParent={(messageSpawnParent != null ? messageSpawnParent.name : "NULL")}, chatMessagePrefab={(chatMessagePrefab != null ? chatMessagePrefab.name : "NULL")}");
        
        if (chatMessagePrefab == null)
        {
            Debug.LogError("[ChatSystem] chatMessagePrefab не назначен в инспекторе!");
            return;
        }
        
        // Определяем позицию и родителя для спавна
        Transform parentTransform = null;
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;
        
        if (messageSpawnParent != null)
        {
            parentTransform = messageSpawnParent;
            // Используем локальные координаты для UI элементов
            spawnPosition = Vector3.zero; // Локальная позиция (0,0,0) относительно родителя
            spawnRotation = Quaternion.identity; // Локальная ротация
            Debug.Log($"[ChatSystem] messageSpawnParent найден: {parentTransform.name}, активен: {parentTransform.gameObject.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[ChatSystem] messageSpawnParent не назначен в инспекторе! Сообщение будет создано без родителя.");
        }
        
        // Создаем экземпляр префаба с указанием родителя (локальный объект, не NetworkObject)
        GameObject messageObject = Instantiate(chatMessagePrefab, parentTransform);
        
        if (messageObject == null)
        {
            Debug.LogError("[ChatSystem] Не удалось создать экземпляр префаба!");
            return;
        }
        
        Debug.Log($"[ChatSystem] Префаб создан: {messageObject.name}, родитель: {(messageObject.transform.parent != null ? messageObject.transform.parent.name : "NULL")}");
        
        // Устанавливаем локальную позицию и ротацию
        if (parentTransform != null)
        {
            messageObject.transform.localPosition = spawnPosition;
            messageObject.transform.localRotation = spawnRotation;
            Debug.Log($"[ChatSystem] Позиция установлена: localPosition={spawnPosition}, parent={parentTransform.name}");
        }
        else
        {
            // Если родителя нет, используем мировые координаты
            messageObject.transform.position = spawnPosition;
            messageObject.transform.rotation = spawnRotation;
            Debug.Log($"[ChatSystem] Позиция установлена (мировая): position={spawnPosition}");
        }
        
        // Убеждаемся, что объект активен
        messageObject.SetActive(true);
        
        // Получаем ChatMessageItem компонент
        ChatMessageItem messageItem = messageObject.GetComponent<ChatMessageItem>();
        if (messageItem == null)
        {
            Debug.LogError("[ChatSystem] Префаб сообщения чата не имеет ChatMessageItem компонента!");
            Destroy(messageObject);
            return;
        }
        
        // Инициализируем данные сообщения (локально на каждом клиенте)
        messageItem.Initialize(message, playerName, playerColor, senderId, isAdmin);
        
        Debug.Log($"[ChatSystem] ✓ Сообщение от {playerName} создано локально: {message}, родитель: {(messageObject.transform.parent != null ? messageObject.transform.parent.name : "NULL")}");
    }
    
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (chatInputField != null)
        {
            chatInputField.onEndEdit.RemoveListener(OnInputFieldEndEdit);
        }
        
        // Останавливаем корутину, если она выполняется
        if (playMessageSoundsCoroutine != null)
        {
            StopCoroutine(playMessageSoundsCoroutine);
        }
    }
}

/// <summary>
/// Структура команды чата
/// </summary>
public class ChatCommand
{
    public string name;
    public string description;
    public string[] parameters;
    public System.Action<string[], ChatSystem> Execute;
    
    public ChatCommand(string name, string description, string[] parameters, System.Action<string[], ChatSystem> execute)
    {
        this.name = name;
        this.description = description;
        this.parameters = parameters;
        this.Execute = execute;
    }
}
