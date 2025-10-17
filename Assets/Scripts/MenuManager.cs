using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private InputField ipInputField;
    [SerializeField] private Text statusText;
    [SerializeField] private Text playerCountText;
    
    [Header("References")]
    [SerializeField] private NetworkManagerUI networkManagerUI;
    [SerializeField] private SceneTransitionManager sceneTransitionManager;
    
    void Start()
    {
        // Получаем компоненты если они не назначены
        if (networkManagerUI == null)
            networkManagerUI = FindObjectOfType<NetworkManagerUI>();
            
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindObjectOfType<SceneTransitionManager>();
        
        // Настраиваем кнопки
        SetupButtons();
        
        // Обновляем UI
        UpdateUI();
    }
    
    void SetupButtons()
    {
        // Кнопка Host
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(OnHostButtonClicked);
        }
        
        // Кнопка Client
        if (clientButton != null)
        {
            clientButton.onClick.RemoveAllListeners();
            clientButton.onClick.AddListener(OnClientButtonClicked);
        }
        
        // Кнопка Start Game
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
        }
        
        // Кнопка Return to Menu
        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(OnReturnToMenuButtonClicked);
        }
        
        // Кнопка Quit
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }
    }
    
    void OnHostButtonClicked()
    {
        Debug.Log("Host button clicked");
        
        if (networkManagerUI != null)
        {
            networkManagerUI.StartHost();
        }
        else
        {
            Debug.LogError("NetworkManagerUI not found!");
        }
    }
    
    void OnClientButtonClicked()
    {
        Debug.Log("Client button clicked");
        
        if (networkManagerUI != null)
        {
            networkManagerUI.StartClient();
        }
        else
        {
            Debug.LogError("NetworkManagerUI not found!");
        }
    }
    
    void OnStartGameButtonClicked()
    {
        Debug.Log("Start Game button clicked");
        
        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.OnStartGameButtonClicked();
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found!");
        }
    }
    
    void OnReturnToMenuButtonClicked()
    {
        Debug.Log("Return to Menu button clicked");
        
        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.OnReturnToMenuButtonClicked();
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found!");
        }
    }
    
    void OnQuitButtonClicked()
    {
        Debug.Log("Quit button clicked");
        
        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.OnQuitGameButtonClicked();
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found!");
        }
    }
    
    void Update()
    {
        // Обновляем UI каждый кадр
        UpdateUI();
    }
    
    void UpdateUI()
    {
        // Обновляем статус
        UpdateStatusText();
        
        // Обновляем количество игроков
        UpdatePlayerCountText();
        
        // Обновляем состояние кнопок
        UpdateButtonStates();
    }
    
    void UpdateStatusText()
    {
        if (statusText == null) return;
        
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            if (Unity.Netcode.NetworkManager.Singleton.IsHost)
            {
                statusText.text = "Host - Waiting for clients...";
            }
            else if (Unity.Netcode.NetworkManager.Singleton.IsClient)
            {
                statusText.text = "Client - Connected to host";
            }
            else
            {
                statusText.text = "Not connected";
            }
        }
        else
        {
            statusText.text = "Network not initialized";
        }
    }
    
    void UpdatePlayerCountText()
    {
        if (playerCountText == null) return;
        
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            int playerCount = Unity.Netcode.NetworkManager.Singleton.ConnectedClients.Count;
            playerCountText.text = $"Players: {playerCount}";
        }
        else
        {
            playerCountText.text = "Players: 0";
        }
    }
    
    void UpdateButtonStates()
    {
        bool isConnected = Unity.Netcode.NetworkManager.Singleton != null && 
                          (Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsClient);
        
        // Кнопки Host и Client доступны только когда не подключены
        if (hostButton != null)
            hostButton.interactable = !isConnected;
            
        if (clientButton != null)
            clientButton.interactable = !isConnected;
        
        // Кнопка Start Game доступна только для хоста
        if (startGameButton != null)
            startGameButton.interactable = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;
        
        // Кнопка Return to Menu доступна только для хоста
        if (returnToMenuButton != null)
            returnToMenuButton.interactable = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;
        
        // Кнопка Quit всегда доступна
        if (quitButton != null)
            quitButton.interactable = true;
    }
    
    // Публичные методы для внешнего вызова
    public void SetStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }
    
    public void SetPlayerCountText(string text)
    {
        if (playerCountText != null)
        {
            playerCountText.text = text;
        }
    }
    
    public void ShowHostButton(bool show)
    {
        if (hostButton != null)
        {
            hostButton.gameObject.SetActive(show);
        }
    }
    
    public void ShowClientButton(bool show)
    {
        if (clientButton != null)
        {
            clientButton.gameObject.SetActive(show);
        }
    }
    
    public void ShowStartGameButton(bool show)
    {
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(show);
        }
    }
    
    public void ShowReturnToMenuButton(bool show)
    {
        if (returnToMenuButton != null)
        {
            returnToMenuButton.gameObject.SetActive(show);
        }
    }
}
