using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// UI контроллер для меню лобби
/// </summary>
public class LobbyMenuUI : MonoBehaviour
{
    [Header("Player List")]
    [Tooltip("Transform для спавна префабов списка игроков")]
    public Transform playerListParent;
    
    [Header("Buttons")]
    [Tooltip("Кнопка 'Начать игру' (видна только создателю лобби)")]
    public Button startGameButton;
    
    [Tooltip("Кнопка 'Выбрать цвет'")]
    public Button chooseColorButton;
    
    [Tooltip("Кнопка 'Настройки лобби' (видна только создателю лобби)")]
    public Button lobbySettingsButton;
    
    [Tooltip("Кнопка 'Присоединиться к другому лобби'")]
    public Button joinOtherLobbyButton;
    
    [Header("Color Selection")]
    [Tooltip("GameObject с кнопками выбора цвета")]
    public GameObject colorSelectionPanel;
    
    [Tooltip("Кнопки цветов (Белый, Красный, Розовый, Фиолетовый, Синий, Голубой, Зеленый, Салатовый)")]
    public Button[] colorButtons;
    
    [Header("Lobby Settings")]
    [Tooltip("GameObject с настройками лобби")]
    public GameObject lobbySettingsPanel;
    
    [Tooltip("InputField для максимального количества игроков")]
    public InputField maxPlayersInput;
    
    [Tooltip("InputField для пароля лобби")]
    public InputField passwordInput;
    
    [Tooltip("Toggle для включения читов")]
    public Toggle cheatsToggle;
    
    [Tooltip("Кнопка 'Назад' в настройках")]
    public Button settingsBackButton;
    
    [Tooltip("Кнопка 'Применить' в настройках")]
    public Button settingsApplyButton;
    
    [Header("Join Lobby")]
    [Tooltip("GameObject с панелью присоединения к лобби")]
    public GameObject joinLobbyPanel;
    
    [Tooltip("InputField для поиска лобби по имени")]
    public InputField lobbySearchInput;
    
    [Tooltip("Transform для спавна префабов списка лобби")]
    public Transform lobbyListParent;
    
    [Header("Lobby Info")]
    [Tooltip("Text для отображения пароля лобби (виден только создателю)")]
    public Text lobbyPasswordText;
    
    [Tooltip("Text для отображения статуса соединения")]
    public Text connectionStatusText;
    
    [Header("Scene Loading")]
    [Tooltip("GameObject с аниматором, который будет показываться во время загрузки")]
    public GameObject loadingObject;
    
    [Tooltip("Аниматор для анимации загрузки")]
    public Animator loadingAnimator;
    
    [Tooltip("Время (в секундах) до начала загрузки сцены")]
    public float loadStartTime = 3f;
    
    [Tooltip("Общее время (в секундах) до скрытия объекта")]
    public float hideTime = 8f;
    
    private const string LOADING_ANIMATION_NAME = "loadingmainscene";
    
    private bool isLoading = false;
    private Coroutine loadingCoroutine;
    private bool isLoadingObjectInitialized = false;
    
    private LobbyManager lobbyManager;
    private LobbyPlayer localPlayer;
    
    void Start()
    {
        lobbyManager = LobbyManager.Instance;
        
        // Настраиваем кнопки
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        
        if (chooseColorButton != null)
        {
            chooseColorButton.onClick.AddListener(OnChooseColorClicked);
        }
        
        if (lobbySettingsButton != null)
        {
            lobbySettingsButton.onClick.AddListener(OnLobbySettingsClicked);
        }
        
        if (joinOtherLobbyButton != null)
        {
            joinOtherLobbyButton.onClick.AddListener(OnJoinOtherLobbyClicked);
        }
        
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
        }
        
        if (settingsApplyButton != null)
        {
            settingsApplyButton.onClick.AddListener(OnSettingsApplyClicked);
        }
        
        // Настраиваем кнопки цветов
        SetupColorButtons();
        
        // Настраиваем поиск лобби
        if (lobbySearchInput != null)
        {
            lobbySearchInput.onValueChanged.AddListener(OnLobbySearchChanged);
        }
        
        // Скрываем панели при старте
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(false);
        }
        
        if (lobbySettingsPanel != null)
        {
            lobbySettingsPanel.SetActive(false);
        }
        
        if (joinLobbyPanel != null)
        {
            joinLobbyPanel.SetActive(false);
        }
        
        // Инициализируем объект загрузки
        InitializeLoadingObject();
        
        // Обновляем UI
        InvokeRepeating(nameof(UpdateUI), 0.5f, 0.5f);
        
        // Периодически обновляем список игроков
        InvokeRepeating(nameof(UpdatePlayerListPeriodically), 1f, 1f);
    }
    
    /// <summary>
    /// Периодически обновляет список игроков
    /// </summary>
    void UpdatePlayerListPeriodically()
    {
        if (lobbyManager != null)
        {
            lobbyManager.UpdatePlayerList();
        }
    }
    
    void UpdateUI()
    {
        // Находим локального игрока
        if (localPlayer == null)
        {
            LobbyPlayer[] players = FindObjectsOfType<LobbyPlayer>();
            foreach (LobbyPlayer player in players)
            {
                if (player.isLocalPlayer)
                {
                    localPlayer = player;
                    break;
                }
            }
        }
        
        // Обновляем видимость кнопок
        // Проверяем, что мы действительно владелец лобби
        bool isOwner = false;
        if (lobbyManager != null)
        {
            isOwner = lobbyManager.IsLobbyOwner;
            
            // Дополнительная проверка через NetworkServer (если мы хост, то мы владелец)
            if (!isOwner && NetworkServer.active)
            {
                isOwner = true;
            }
        }
        
        // Кнопки и текст должны быть видны ТОЛЬКО у владельца лобби
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isOwner);
        }
        
        if (lobbySettingsButton != null)
        {
            lobbySettingsButton.gameObject.SetActive(isOwner);
        }
        
        if (lobbyPasswordText != null)
        {
            lobbyPasswordText.gameObject.SetActive(isOwner);
            if (isOwner && lobbyManager != null)
            {
                lobbyPasswordText.text = $"Пароль лобби: {lobbyManager.lobbyPassword}";
            }
        }
        
        // Обновляем статус соединения
        UpdateConnectionStatus();
    }
    
    void UpdateConnectionStatus()
    {
        if (connectionStatusText == null) return;
        
        if (NetworkClient.isConnected && NetworkServer.active)
        {
            connectionStatusText.text = "Хост активен";
            connectionStatusText.color = Color.green;
        }
        else if (NetworkClient.isConnected)
        {
            connectionStatusText.text = "Подключено к серверу";
            connectionStatusText.color = Color.green;
        }
        else if (NetworkClient.isConnecting)
        {
            connectionStatusText.text = "Подключение...";
            connectionStatusText.color = Color.yellow;
        }
        else
        {
            connectionStatusText.text = "Не подключено";
            connectionStatusText.color = Color.red;
        }
    }
    
    void SetupColorButtons()
    {
        if (colorButtons == null || colorButtons.Length != 8) return;
        
        for (int i = 0; i < colorButtons.Length; i++)
        {
            int colorIndex = i;
            if (colorButtons[i] != null)
            {
                colorButtons[i].onClick.AddListener(() => OnColorSelected(colorIndex));
            }
        }
    }
    
    void OnStartGameClicked()
    {
        if (lobbyManager != null)
        {
            // Загружаем сцену с анимацией загрузки, если есть объект загрузки
            if (loadingObject != null)
            {
                StartLoadingScene();
            }
            else
            {
                // Если объекта загрузки нет, используем стандартный метод LobbyManager
                // (который загружает сцену Lobby через NetworkManager)
                lobbyManager.StartGame();
            }
        }
    }
    
    /// <summary>
    /// Инициализирует объект загрузки и делает его постоянным между сценами
    /// </summary>
    private void InitializeLoadingObject()
    {
        if (loadingObject != null && !isLoadingObjectInitialized)
        {
            // Делаем объект загрузки постоянным между сценами
            DontDestroyOnLoad(loadingObject);
            isLoadingObjectInitialized = true;
            
            // Скрываем объект загрузки при старте (он будет показан только при вызове StartLoadingScene)
            loadingObject.SetActive(false);
            
            Debug.Log("[LobbyMenuUI] Объект загрузки настроен как постоянный между сценами");
        }
    }
    
    /// <summary>
    /// Запускает процесс загрузки сцены с анимацией
    /// </summary>
    private void StartLoadingScene()
    {
        if (isLoading)
        {
            Debug.LogWarning("[LobbyMenuUI] Загрузка уже идет!");
            return;
        }
        
        if (loadingObject == null)
        {
            Debug.LogError("[LobbyMenuUI] Объект загрузки не назначен!");
            return;
        }
        
        // Инициализируем объект загрузки, если еще не инициализирован
        InitializeLoadingObject();
        
        if (loadingAnimator == null)
        {
            loadingAnimator = loadingObject.GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[LobbyMenuUI] Аниматор не найден на объекте загрузки!");
                return;
            }
        }
        
        // Убеждаемся, что GameObject активен перед запуском корутины
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("[LobbyMenuUI] GameObject неактивен! Невозможно запустить корутину.");
            isLoading = false;
            return;
        }
        
        isLoading = true;
        loadingCoroutine = StartCoroutine(LoadingCoroutine());
    }
    
    /// <summary>
    /// Корутина для управления процессом загрузки
    /// </summary>
    private IEnumerator LoadingCoroutine()
    {
        // Показываем объект загрузки
        loadingObject.SetActive(true);
        
        // Получаем аниматор, если не назначен
        if (loadingAnimator == null)
        {
            loadingAnimator = loadingObject.GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[LobbyMenuUI] Аниматор не найден на объекте загрузки!");
                yield break;
            }
        }
        
        // Запускаем анимацию
        loadingAnimator.Play(LOADING_ANIMATION_NAME);
        
        // Отслеживаем общее время с начала
        float totalElapsedTime = 0f;
        
        // Ждем до момента начала загрузки (3 секунды)
        while (totalElapsedTime < loadStartTime)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Приостанавливаем анимацию на 3 секунде
        AnimatorStateInfo stateInfo = loadingAnimator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = stateInfo.normalizedTime;
        loadingAnimator.speed = 0f; // Останавливаем анимацию
        
        // Запускаем загрузку сцены через LobbyManager (для мультиплеера)
        if (lobbyManager != null)
        {
            lobbyManager.StartGame();
        }
        
        // Ждем, пока сцена загрузится и активируется
        // Проверяем изменение сцены
        string initialScene = SceneManager.GetActiveScene().name;
        while (SceneManager.GetActiveScene().name == initialScene)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Сцена изменилась, ждем еще немного для полной загрузки
        yield return new WaitForSeconds(0.5f);
        
        // Проверяем, что объект загрузки все еще существует (он должен быть постоянным)
        if (loadingObject == null || loadingAnimator == null)
        {
            Debug.LogWarning("[LobbyMenuUI] Объект загрузки или аниматор уничтожены после загрузки сцены!");
            isLoading = false;
            yield break;
        }
        
        // Возобновляем анимацию с того же места (3 секунды)
        loadingAnimator.speed = 1f;
        loadingAnimator.Play(LOADING_ANIMATION_NAME, 0, normalizedTime);
        
        // Ждем до момента скрытия объекта (8 секунд от начала)
        // Продолжаем отслеживать время, пока не пройдет 8 секунд
        while (totalElapsedTime < hideTime)
        {
            totalElapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Скрываем объект загрузки
        if (loadingObject != null)
        {
            loadingObject.SetActive(false);
        }
        
        isLoading = false;
    }
    
    /// <summary>
    /// Останавливает процесс загрузки
    /// </summary>
    private void StopLoading()
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        
        if (loadingAnimator != null)
        {
            loadingAnimator.speed = 1f;
        }
        
        if (loadingObject != null)
        {
            loadingObject.SetActive(false);
        }
        
        isLoading = false;
    }
    
    void OnDestroy()
    {
        StopLoading();
    }
    
    void OnChooseColorClicked()
    {
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(!colorSelectionPanel.activeSelf);
        }
    }
    
    void OnColorSelected(int colorIndex)
    {
        if (localPlayer != null)
        {
            localPlayer.CmdSetPlayerColor(colorIndex);
        }
        
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(false);
        }
    }
    
    void OnLobbySettingsClicked()
    {
        if (lobbySettingsPanel != null)
        {
            bool isActive = !lobbySettingsPanel.activeSelf;
            lobbySettingsPanel.SetActive(isActive);
            
            if (isActive && lobbyManager != null)
            {
                // Заполняем поля текущими значениями
                if (maxPlayersInput != null)
                {
                    maxPlayersInput.text = lobbyManager.maxPlayers.ToString();
                }
                
                if (passwordInput != null)
                {
                    passwordInput.text = lobbyManager.lobbyPassword;
                }
                
                if (cheatsToggle != null)
                {
                    cheatsToggle.isOn = lobbyManager.cheatsEnabled;
                }
            }
        }
    }
    
    void OnSettingsBackClicked()
    {
        if (lobbySettingsPanel != null)
        {
            lobbySettingsPanel.SetActive(false);
        }
    }
    
    void OnSettingsApplyClicked()
    {
        if (lobbyManager == null) return;
        
        int maxPlayers = 4;
        if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int parsedMax))
        {
            maxPlayers = parsedMax;
        }
        
        string password = lobbyManager.lobbyPassword;
        if (passwordInput != null)
        {
            password = passwordInput.text;
        }
        
        bool cheats = false;
        if (cheatsToggle != null)
        {
            cheats = cheatsToggle.isOn;
        }
        
        lobbyManager.UpdateLobbySettings(maxPlayers, password, cheats);
        
        if (lobbySettingsPanel != null)
        {
            lobbySettingsPanel.SetActive(false);
        }
    }
    
    void OnJoinOtherLobbyClicked()
    {
        if (joinLobbyPanel != null)
        {
            bool isActive = !joinLobbyPanel.activeSelf;
            joinLobbyPanel.SetActive(isActive);
            
            if (isActive && lobbyManager != null)
            {
                lobbyManager.UpdateLobbyList();
            }
        }
    }
    
    void OnLobbySearchChanged(string searchText)
    {
        if (lobbyManager != null)
        {
            lobbyManager.UpdateLobbyList(searchText);
        }
    }
}

