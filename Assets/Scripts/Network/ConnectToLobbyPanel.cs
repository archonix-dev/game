using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель подключения к другому лобби. Позволяет ввести IP адрес и пароль для подключения.
/// </summary>
public class ConnectToLobbyPanel : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Поле ввода IP адреса лобби")]
    public InputField ipAddressInput;
    
    [Tooltip("Поле ввода пароля лобби")]
    public InputField passwordInput;
    
    [Tooltip("Кнопка 'Войти' (подключиться)")]
    public Button connectButton;
    
    [Tooltip("Кнопка 'Назад' (закрыть панель)")]
    public Button backButton;

    private LobbyManager lobbyManager;

    void Start()
    {
        lobbyManager = FindObjectOfType<LobbyManager>();

        SetupButtons();
    }

    void SetupButtons()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);
    }

    void OnConnectButtonClicked()
    {
        // Получаем IP адрес и пароль
        string ipAddress = "";
        string password = "";

        if (ipAddressInput != null)
        {
            ipAddress = ipAddressInput.text.Trim();
        }

        if (passwordInput != null)
        {
            password = passwordInput.text.Trim();
        }

        // Проверяем, что IP адрес введен
        if (string.IsNullOrEmpty(ipAddress))
        {
            Debug.LogWarning("Введите IP адрес лобби!");
            return;
        }

        // Подключаемся к лобби
        if (lobbyManager != null)
        {
            lobbyManager.ConnectToLobby(ipAddress, password);
            
            // Закрываем панель после попытки подключения
            gameObject.SetActive(false);
        }
    }

    void OnBackButtonClicked()
    {
        // Закрываем панель
        gameObject.SetActive(false);
    }
}

