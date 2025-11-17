using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using Game.Localization;

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
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log($"[ChatSystem] OnStartClient: isServer={isServer}, isOwned={isOwned}, isClient={isClient}, NetworkIdentity={GetComponent<NetworkIdentity>()?.netId ?? 0}");
        
        // Настраиваем AudioSource в зависимости от владельца
        SetupAudioSource();
        
        // Используем значения по умолчанию для имени и цвета
    }
    
    void Start()
    {
        // Инициализация: чат закрыт
        SetChatState(false);
        
        // Автоматический поиск компонентов, если не назначены
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }
        
        if (mouseLook == null)
        {
            mouseLook = FindObjectOfType<MouseLook>();
        }
        
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
        
        // Используем значения по умолчанию для имени и цвета
    }
    
    private void SetupAudioSource()
    {
        if (audioSource == null) return;
        
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
            // Если чат открыт, отслеживаем нажатие Enter
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                wasEnterPressed = true;
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
        isChatOpen = open;
        
        // Показываем/скрываем UI чата
        if (chatRoot != null)
        {
            chatRoot.SetActive(open);
        }
        
        // Отключаем/включаем движение игрока
        if (playerController != null)
        {
            playerController.enabled = !open;
        }
        
        // Отключаем/включаем вращение камеры
        if (mouseLook != null)
        {
            mouseLook.enabled = !open;
        }
        
        // Управление курсором
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Фокусируемся на поле ввода и активируем его
            // Используем корутину для задержки, чтобы убедиться, что UI готов
            if (chatInputField != null)
            {
                StartCoroutine(FocusInputFieldDelayed());
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
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
                return;
            }
            
            // Проигрываем звуки для каждого символа
            PlayMessageSounds(text);
            
            // Отправляем сообщение в сеть
            bool isSpawned = netIdentity != null && netIdentity.netId != 0;
            if (isSpawned && isOwned)
            {
                // Отправляем сообщение на сервер для спавна префаба
                Debug.Log($"[ChatSystem] Отправка Command: text={text}, isSpawned={isSpawned}, isOwned={isOwned}");
                SendChatMessageCommand(text);
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
    /// Обрабатывает команды чата (начинаются с /)
    /// </summary>
    private void HandleCommand(string command)
    {
        // Убираем пробелы и приводим к нижнему регистру
        command = command.Trim().ToLowerInvariant();
        
        // Команда /kill - убивает игрока
        if (command == "/kill")
        {
            if (playerController != null)
            {
                playerController.KillPlayer();
                Debug.Log("[ChatSystem] Команда /kill выполнена - игрок убит");
            }
            else
            {
                Debug.LogWarning("[ChatSystem] PlayerController не найден для выполнения команды /kill");
            }
        }
        else
        {
            Debug.Log($"[ChatSystem] Неизвестная команда: {command}");
        }
    }
    
    /// <summary>
    /// Проигрывает звуки для каждого символа сообщения
    /// </summary>
    private void PlayMessageSounds(string message)
    {
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
    /// ServerRpc для отправки сообщения в чат (вызывается владельцем)
    /// </summary>
    [Command]
    private void SendChatMessageCommand(string message, NetworkConnectionToClient sender = null)
    {
        // В Mirror, когда клиент вызывает Command, sender автоматически передается
        // Если sender null, используем connectionToClient из этого NetworkIdentity
        NetworkConnectionToClient actualSender = sender;
        if (actualSender == null && netIdentity != null && netIdentity.connectionToClient != null)
        {
            actualSender = netIdentity.connectionToClient;
        }
        
        uint senderId = 0;
        if (actualSender != null)
        {
            senderId = (uint)actualSender.connectionId;
        }
        
        Debug.Log($"[ChatSystem] Command получен: message={message}, senderId={senderId}, sender={(actualSender != null ? "NOT NULL" : "NULL")}, connectionToClient={(netIdentity?.connectionToClient != null ? "NOT NULL" : "NULL")}");
        
        // Получаем имя и цвет игрока из LobbyPlayer
        string playerName = "Player";
        Color playerColor = Color.white;
        bool isAdmin = false;
        
        // Пытаемся найти LobbyPlayer для отправителя
        LobbyPlayer lobbyPlayer = null;
        
        if (actualSender != null)
        {
            // Вариант 1: Проверяем, есть ли LobbyPlayer как player object у этого подключения
            if (actualSender.identity != null)
            {
                lobbyPlayer = actualSender.identity.GetComponent<LobbyPlayer>();
            }
            
            // Вариант 2: Если не нашли, ищем по всем LobbyPlayer и проверяем connectionToClient
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
        
        // Если нашли LobbyPlayer, получаем данные из него
        if (lobbyPlayer != null)
        {
            playerName = lobbyPlayer.playerName;
            playerColor = lobbyPlayer.GetPlayerColor();
            isAdmin = lobbyPlayer.isOwner;
            Debug.Log($"[ChatSystem] Найден LobbyPlayer: {playerName}, цвет: {playerColor}, isOwner: {isAdmin}");
        }
        else
        {
            // Если не нашли LobbyPlayer, используем значения по умолчанию
            // и проверяем, является ли отправитель админом (хостом)
            if (actualSender != null)
            {
                // Проверяем, является ли этот connection хостом
                // Хост - это когда NetworkServer.activeHost == true
                // И connectionId хоста обычно 0, но нужно проверить через NetworkServer.connections
                isAdmin = NetworkServer.activeHost && actualSender.connectionId == 0;
            }
            else if (senderId == 0 && NetworkServer.activeHost)
            {
                // Если senderId == 0 и мы хост, то это хост
                isAdmin = true;
            }
            
            Debug.LogWarning($"[ChatSystem] LobbyPlayer не найден для connectionId {senderId}, используем значения по умолчанию");
        }
        
        Debug.Log($"[ChatSystem] Финальное имя для отправки: {playerName}, цвет: {playerColor}, isAdmin: {isAdmin}");
        
        // Отправляем сообщение всем клиентам через ClientRpc
        // ClientRpc автоматически отправляется всем клиентам, у которых есть этот NetworkIdentity
        NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
        bool isSpawned = networkIdentity != null && networkIdentity.netId != 0;
        Debug.Log($"[ChatSystem] Отправка ClientRpc: message={message}, playerName={playerName}, senderId={senderId}, isSpawned={isSpawned}, netId={networkIdentity?.netId ?? 0}, isServer={isServer}");
        
        if (networkIdentity == null || networkIdentity.netId == 0)
        {
            bool netIsSpawned = networkIdentity != null && networkIdentity.netId != 0;
            Debug.LogError($"[ChatSystem] NetworkIdentity не найден или не заспавнен! networkIdentity={networkIdentity}, isSpawned={netIsSpawned}");
            return;
        }
        
        // Вызываем ClientRpc - он автоматически отправится всем клиентам
        Debug.Log($"[ChatSystem] Отправка ClientRpc на клиентов");
        
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

