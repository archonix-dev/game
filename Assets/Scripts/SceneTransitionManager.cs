using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private string gameSceneName = "Test";
    
    [Header("Transition Settings")]
    [SerializeField] private float transitionDelay = 1f;
    
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;
    
    private static SceneTransitionManager instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SceneTransitionManager>();
            }
            return instance;
        }
    }
    
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
        // Подписываемся на события NetworkManager
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
    
    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        
        // Если это хост, переходим в игровую сцену через 2 секунды
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Host started, transitioning to game scene in 2 seconds");
            Invoke(nameof(TransitionToGameScene), 2f);
        }
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        
        // Если это хост и отключился клиент, возвращаемся в меню
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClients.Count <= 1)
        {
            Debug.Log("All clients disconnected, returning to menu");
            TransitionToMenuScene();
        }
    }
    
    public void TransitionToGameScene()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            // Хост загружает сцену для всех
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            
            // Спавним игроков после загрузки сцены
            StartCoroutine(SpawnPlayersAfterSceneLoad());
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // Клиент ждет загрузки сцены от хоста
            Debug.Log("Waiting for host to load game scene...");
        }
    }
    
    public void TransitionToMenuScene()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            // Хост загружает сцену для всех
            NetworkManager.Singleton.SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // Клиент ждет загрузки сцены от хоста
            Debug.Log("Waiting for host to load menu scene...");
        }
        else
        {
            // Если нет активного подключения, загружаем сцену локально
            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
            else
            {
                Debug.LogError("Menu scene name is not set!");
            }
        }
    }
    
    public void LoadGameSceneDirectly()
    {
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void LoadMenuSceneDirectly()
    {
        SceneManager.LoadScene(menuSceneName);
    }
    
    // Методы для кнопок UI
    public void OnStartGameButtonClicked()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            TransitionToGameScene();
        }
        else
        {
            Debug.LogWarning("Only host can start the game!");
        }
    }
    
    public void OnReturnToMenuButtonClicked()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            TransitionToMenuScene();
        }
        else
        {
            Debug.LogWarning("Only host can return to menu!");
        }
    }
    
    public void OnQuitGameButtonClicked()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        LoadMenuSceneDirectly();
    }
    
    // Методы для получения информации о текущей сцене
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
    
    public bool IsInMenuScene()
    {
        return GetCurrentSceneName() == menuSceneName;
    }
    
    public bool IsInGameScene()
    {
        return GetCurrentSceneName() == gameSceneName;
    }
    
    // Методы для проверки состояния сети
    public bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && 
               (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);
    }
    
    public bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
    
    public bool IsClient()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
    }
    
    IEnumerator SpawnPlayersAfterSceneLoad()
    {
        Debug.Log("Waiting for scene to load...");
        yield return new WaitForSeconds(2f);
        
        Debug.Log("Spawning players...");
        
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in SceneTransitionManager!");
            yield break;
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        
        // Спавним всех подключенных клиентов
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            Debug.Log($"Spawning player for client {client.Key}");
            
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(client.Key);
                Debug.Log($"Player spawned for client {client.Key} at position {spawnPosition}");
            }
            else
            {
                Debug.LogError("Player prefab doesn't have NetworkObject component!");
                Destroy(playerObject);
            }
        }
    }
}
