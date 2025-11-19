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
    [Tooltip("Контроллер экрана загрузки (DontDestroy)")]
    public LobbyLoadingController loadingController;
    
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

        HandleExistingLobbyState();
        // Обновляем UI
        InvokeRepeating(nameof(UpdateUI), 0.5f, 0.5f);
        
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
            // Загружаем сцену с анимацией загрузки, если есть контроллер
            if (loadingController != null)
            {
                loadingController.StartHostLoadingSequence(lobbyManager);
            }
            else
            {
                // Если контроллер не назначен, используем стандартный метод LobbyManager
                lobbyManager.StartGame();
            }
        }
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

    void HandleExistingLobbyState()
    {
        if (lobbyManager == null)
            return;

        bool hasLobbyPlayers = FindObjectsOfType<LobbyPlayer>().Length > 0;
        bool isNetworkActive = NetworkClient.active || NetworkServer.active;

        if (!lobbyManager.IsLobbyOwner && (hasLobbyPlayers || isNetworkActive))
        {
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(false);
            }

            if (lobbySettingsButton != null)
            {
                lobbySettingsButton.gameObject.SetActive(false);
            }
        }

        if (hasLobbyPlayers)
        {
            if (playerListParent != null && lobbyManager.playerListParent != playerListParent)
            {
                lobbyManager.playerListParent = playerListParent;
            }

            lobbyManager.UpdatePlayerList();
        }
    }
}

