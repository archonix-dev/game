using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
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
        // В Mirror события подключения обрабатываются через MirrorNetworkManager
        // Подписка не требуется, так как MirrorNetworkManager уже обрабатывает эти события
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий
        // В Mirror события подключения обрабатываются через MirrorNetworkManager
        // Отписка не требуется
    }
    
    // Эти методы больше не используются, так как Mirror обрабатывает события через MirrorNetworkManager
    // Если нужна обработка подключений, используйте MirrorNetworkManager.OnServerConnect/OnClientConnect
    
    public void TransitionToGameScene()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            // Хост загружает сцену для всех
            NetworkManager.singleton.ServerChangeScene(gameSceneName);
            
            // Спавним игроков после загрузки сцены
            StartCoroutine(SpawnPlayersAfterSceneLoad());
        }

    }
    
    public void TransitionToMenuScene()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            // Хост загружает сцену для всех
            NetworkManager.singleton.ServerChangeScene(menuSceneName);
        }
        else
        {
            // Если нет активного подключения, загружаем сцену локально
            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
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
        if (NetworkServer.active && NetworkClient.active)
        {
            TransitionToGameScene();
        }
    }
    
    public void OnReturnToMenuButtonClicked()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            TransitionToMenuScene();
        }
    }
    
    public void OnQuitGameButtonClicked()
    {
        var networkManager = MirrorNetworkManager.Instance;
        if (networkManager != null)
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                networkManager.StopHost();
            }
            else if (NetworkClient.active)
            {
                networkManager.StopClient();
            }
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
        return ((NetworkServer.active && NetworkClient.active) || NetworkClient.active);
    }
    
    public bool IsHost()
    {
        return NetworkServer.active && NetworkClient.active;
    }
    
    public bool IsClient()
    {
        return NetworkClient.active;
    }
    
    IEnumerator SpawnPlayersAfterSceneLoad()
    {
        yield return new WaitForSeconds(2f);
        
        
        if (playerPrefab == null)
        {
            yield break;
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        
        // Спавним всех подключенных клиентов
        foreach (var client in NetworkServer.connections.Values)
        {
            
            GameObject playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            NetworkIdentity networkIdentity = playerObject.GetComponent<NetworkIdentity>();
            
            if (networkIdentity != null)
            {
                NetworkServer.AddPlayerForConnection(client, playerObject);
            }
            else
            {
                Destroy(playerObject);
            }
        }
    }
}
