using UnityEngine;
using UnityEngine.UI;
using Mirror;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

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
    
    [Tooltip("Текст для отображения текущего пароля лобби (только для чтения)")]
    public Text passwordDisplayText;
    
    [Tooltip("Кнопка 'Сбросить'")]
    public Button resetButton;
    
    [Tooltip("Кнопка 'Применить'")]
    public Button applyButton;
    
    [Tooltip("Кнопка закрытия панели (опционально)")]
    public Button closeButton;

    [Header("Настройки")]
    [Tooltip("Максимальное количество игроков по умолчанию")]
    public int defaultMaxPlayers = 8;

    private LobbyManager lobbyManager;

    void Start()
    {
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
        
        // Обновляем отображение пароля при старте
        UpdatePasswordDisplay();
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
    
    /// <summary>
    /// Устанавливает отображение пароля в формате "Пароль : Текущий_пароль"
    /// </summary>
    public void SetPasswordDisplay(string password)
    {
        if (passwordDisplayText != null)
        {
            if (!string.IsNullOrEmpty(password))
            {
                passwordDisplayText.text = $"Пароль : {password}";
            }
            else
            {
                passwordDisplayText.text = "Пароль : не установлен";
            }
        }
    }
    
    /// <summary>
    /// Обновляет отображение пароля из Steam лобби
    /// </summary>
    void UpdatePasswordDisplay()
    {
        #if !DISABLESTEAMWORKS
        SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
        if (steamLobbyManager != null)
        {
            ulong lobbyId = steamLobbyManager.GetCurrentLobbyId();
            if (lobbyId != 0)
            {
                string password = SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), "password");
                SetPasswordDisplay(password);
            }
        }
        #endif
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
        
        // Обновляем отображение пароля
        UpdatePasswordDisplay();
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

        // Применяем настройки лобби (сохраняем в PlayerPrefs или используем напрямую)
        if (lobbyManager != null)
        {
            // Обновляем максимальное количество игроков в LobbyManager
            // Настройки применяются локально для хоста
            Debug.Log($"Настройки лобби применены: Макс игроков={maxPlayers}, Читы={cheatsEnabled}, Пароль={password}");
            
            // Устанавливаем пароль в Steam лобби
            SteamLobbyManager steamLobbyManager = SteamLobbyManager.Instance;
            if (steamLobbyManager != null && !string.IsNullOrEmpty(password))
            {
                steamLobbyManager.SetLobbyData("password", password);
                Debug.Log($"[LobbySettingsPanel] Пароль лобби установлен в Steam: {password}");
                
                // Обновляем отображение пароля
                SetPasswordDisplay(password);
            }
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


