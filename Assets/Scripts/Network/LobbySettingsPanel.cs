using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Панель настроек лобби. Позволяет администратору настраивать параметры лобби.
/// </summary>
public class LobbySettingsPanel : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Поле ввода максимального количества игроков")]
    public InputField maxPlayersInput;
    
    [Tooltip("Чекбокс для включения/выключения читов")]
    public Toggle cheatsToggle;
    
    [Tooltip("Поле ввода пароля лобби")]
    public InputField passwordInput;
    
    [Tooltip("Кнопка 'Сбросить'")]
    public Button resetButton;
    
    [Tooltip("Кнопка 'Применить'")]
    public Button applyButton;
    
    [Tooltip("Кнопка закрытия панели (опционально)")]
    public Button closeButton;

    [Header("Настройки")]
    [Tooltip("Максимальное количество игроков по умолчанию")]
    public int defaultMaxPlayers = 8;

    private LobbyNetworkManager lobbyNetworkManager;
    private LobbyManager lobbyManager;

    void Start()
    {
        lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
        lobbyManager = FindObjectOfType<LobbyManager>();

        SetupButtons();
        
        // Устанавливаем значения по умолчанию
        if (maxPlayersInput != null)
        {
            maxPlayersInput.text = defaultMaxPlayers.ToString();
        }

        if (cheatsToggle != null)
        {
            cheatsToggle.isOn = false;
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
        }
    }

    void SetupButtons()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);
        
        if (applyButton != null)
            applyButton.onClick.AddListener(OnApplyButtonClicked);
        
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    void OnResetButtonClicked()
    {
        // Сбрасываем все значения к значениям по умолчанию
        if (maxPlayersInput != null)
        {
            maxPlayersInput.text = defaultMaxPlayers.ToString();
        }

        if (cheatsToggle != null)
        {
            cheatsToggle.isOn = false;
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
        }
    }

    void OnApplyButtonClicked()
    {
        // Применяем настройки лобби
        int maxPlayers = defaultMaxPlayers;
        
        if (maxPlayersInput != null && !string.IsNullOrEmpty(maxPlayersInput.text))
        {
            if (int.TryParse(maxPlayersInput.text, out int parsedMaxPlayers))
            {
                maxPlayers = parsedMaxPlayers;
                if (maxPlayers < 2)
                {
                    maxPlayers = 2;
                    maxPlayersInput.text = "2";
                }
                else if (maxPlayers > 32)
                {
                    maxPlayers = 32;
                    maxPlayersInput.text = "32";
                }
            }
        }

        bool cheatsEnabled = false;
        if (cheatsToggle != null)
        {
            cheatsEnabled = cheatsToggle.isOn;
        }

        string password = "";
        if (passwordInput != null)
        {
            password = passwordInput.text.Trim();
            
            // Если пароль пустой, генерируем случайный 6-значный числовой код
            if (string.IsNullOrEmpty(password))
            {
                password = GenerateRandomPassword();
                passwordInput.text = password;
            }
        }

        // Применяем настройки через LobbyNetworkManager
        // Если LobbyNetworkManager не найден, пытаемся найти его снова
        if (lobbyNetworkManager == null)
        {
            lobbyNetworkManager = FindObjectOfType<LobbyNetworkManager>();
        }

        if (lobbyNetworkManager != null)
        {
            lobbyNetworkManager.SetLobbySettings(maxPlayers, cheatsEnabled, password);
            Debug.Log($"Настройки лобби применены: Макс игроков={maxPlayers}, Читы={cheatsEnabled}, Пароль={password}");
        }
        else
        {
            Debug.LogWarning("LobbyNetworkManager не найден! Настройки не могут быть применены.");
        }
    }

    void OnCloseButtonClicked()
    {
        // Закрываем панель
        gameObject.SetActive(false);
    }

    string GenerateRandomPassword()
    {
        // Генерируем случайный 6-значный числовой код
        System.Random random = new System.Random();
        return random.Next(100000, 999999).ToString();
    }
}

