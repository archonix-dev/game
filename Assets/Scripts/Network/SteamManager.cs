using UnityEngine;
using System.Collections;
using Mirror;

#if !DISABLESTEAMWORKS
using Steamworks;
#else
// Steamworks отключен - используем заглушки
#endif

/// <summary>
/// Менеджер для инициализации и управления Steam API.
/// Должен быть единственным экземпляром в сцене (Singleton).
/// </summary>
public class SteamManager : MonoBehaviour
{
    private static SteamManager instance;
    public static SteamManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SteamManager>();
            }
            return instance;
        }
    }

    [Header("Steam Settings")]
    [Tooltip("Steam App ID (должен совпадать с steam_appid.txt)")]
    public uint steamAppId = 480; // Spacewar - тестовое приложение Steam

    [Tooltip("Автоматически инициализировать Steam при старте")]
    public bool autoInitialize = true;

    private bool isSteamInitialized = false;
    private bool isSteamRunning = false;

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
        StartCoroutine(InitializeSteamDelayed());
    }

    private System.Collections.IEnumerator InitializeSteamDelayed()
    {
        // Ждем один кадр, чтобы все системы успели инициализироваться
        yield return null;
        
        if (!isSteamInitialized)
        {
            InitializeSteam();
        }
    }

    void Update()
    {
        // Steam API требует периодического вызова RunCallbacks
        if (isSteamInitialized)
        {
            #if !DISABLESTEAMWORKS
            SteamAPI.RunCallbacks();
            #endif
        }
    }

    /// <summary>
    /// Инициализирует Steam API
    /// </summary>
    public bool InitializeSteam()
    {
        if (isSteamInitialized)
        {
            Debug.Log("[SteamManager] Steam уже инициализирован");
            return true;
        }

        #if !DISABLESTEAMWORKS
        try
        {
            // Проверяем наличие steam_appid.txt файла
            string appIdPath = System.IO.Path.Combine(Application.dataPath, "..", "steam_appid.txt");
            if (!System.IO.File.Exists(appIdPath))
            {
                Debug.LogWarning($"[SteamManager] Файл steam_appid.txt не найден по пути: {appIdPath}");
                Debug.LogWarning($"[SteamManager] Создаю файл steam_appid.txt с App ID: {steamAppId}");
                try
                {
                    System.IO.File.WriteAllText(appIdPath, steamAppId.ToString());
                    Debug.Log($"[SteamManager] ✓ Файл steam_appid.txt создан");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SteamManager] ✗ Не удалось создать steam_appid.txt: {e.Message}");
                }
            }
            else
            {
                // Проверяем, совпадает ли App ID в файле
                string fileContent = System.IO.File.ReadAllText(appIdPath).Trim();
                if (fileContent != steamAppId.ToString())
                {
                    Debug.LogWarning($"[SteamManager] App ID в steam_appid.txt ({fileContent}) не совпадает с настройками ({steamAppId})");
                    Debug.LogWarning($"[SteamManager] Обновляю файл steam_appid.txt...");
                    try
                    {
                        System.IO.File.WriteAllText(appIdPath, steamAppId.ToString());
                        Debug.Log($"[SteamManager] ✓ Файл steam_appid.txt обновлен");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SteamManager] ✗ Не удалось обновить steam_appid.txt: {e.Message}");
                    }
                }
            }

            // Проверяем, запущен ли Steam
            Debug.Log("[SteamManager] Инициализация Steam API...");
            isSteamRunning = SteamAPI.Init();
            
            if (!isSteamRunning)
            {
                Debug.LogError("[SteamManager] ✗ Steam не запущен! Убедитесь, что Steam клиент запущен.");
                Debug.LogError("[SteamManager] Убедитесь, что:");
                Debug.LogError("[SteamManager] 1. Steam клиент запущен");
                Debug.LogError("[SteamManager] 2. Вы вошли в аккаунт Steam");
                Debug.LogError("[SteamManager] 3. Файл steam_appid.txt существует и содержит правильный App ID");
                return false;
            }

            // Получаем Steam ID пользователя
            ulong steamId = SteamUser.GetSteamID().m_SteamID;
            string steamName = SteamFriends.GetPersonaName();
            
            isSteamInitialized = true;
            
            Debug.Log($"[SteamManager] ✓ Steam инициализирован успешно!");
            Debug.Log($"[SteamManager] Steam ID: {steamId}");
            Debug.Log($"[SteamManager] Steam Name: {steamName}");
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamManager] ✗ Ошибка инициализации Steam: {e.Message}");
            Debug.LogError($"[SteamManager] Stack trace: {e.StackTrace}");
            return false;
        }
        #else
        Debug.LogWarning("[SteamManager] Steamworks отключен (DISABLESTEAMWORKS определен)");
        return false;
        #endif
    }

    /// <summary>
    /// Проверяет, инициализирован ли Steam
    /// </summary>
    public bool IsSteamInitialized()
    {
        return isSteamInitialized;
    }

    /// <summary>
    /// Проверяет, запущен ли Steam клиент
    /// </summary>
    public bool IsSteamRunning()
    {
        return isSteamRunning;
    }

    /// <summary>
    /// Получает Steam ID текущего пользователя
    /// </summary>
    public ulong GetSteamId()
    {
        #if !DISABLESTEAMWORKS
        if (isSteamInitialized)
        {
            return SteamUser.GetSteamID().m_SteamID;
        }
        #endif
        return 0;
    }

    /// <summary>
    /// Получает имя текущего пользователя Steam
    /// </summary>
    public string GetSteamName()
    {
        #if !DISABLESTEAMWORKS
        if (isSteamInitialized)
        {
            return SteamFriends.GetPersonaName();
        }
        #endif
        return "Unknown";
    }

    void OnDestroy()
    {
        #if !DISABLESTEAMWORKS
        // КРИТИЧЕСКИ ВАЖНО: OnDestroy() вызывается при смене сцен, а не только при закрытии приложения
        // НЕ останавливаем сеть здесь, так как это может прервать загрузку сцены
        // Остановка сети происходит только в OnApplicationQuit()
        
        // Проверяем, действительно ли приложение закрывается
        // Если сеть активна, значит мы просто меняем сцену - не останавливаем сеть
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.Log("[SteamManager] OnDestroy вызван, но сеть активна - это смена сцены, не закрытие приложения. Пропускаем остановку сети.");
            // Не останавливаем сеть и не завершаем Steam - это сделается в OnApplicationQuit()
            return;
        }
        
        // Если сеть не активна, значит приложение действительно закрывается
        // Но Steam все равно должен завершаться только в OnApplicationQuit()
        // Здесь просто логируем
        if (isSteamInitialized)
        {
            Debug.Log("[SteamManager] OnDestroy вызван, но Steam будет завершен в OnApplicationQuit()");
        }
        #endif
    }

    void OnApplicationQuit()
    {
        #if !DISABLESTEAMWORKS
        // КРИТИЧЕСКИ ВАЖНО: Сначала останавливаем сеть, затем завершаем Steam
        // Порядок важен для предотвращения race condition
        if (isSteamInitialized)
        {
            // Шаг 1: Останавливаем сеть перед завершением Steam
            var networkManager = MirrorNetworkManager.Instance;
            if (networkManager != null)
            {
                try
                {
                    // Останавливаем в правильном порядке: сначала клиент, потом сервер
                    if (NetworkClient.active)
                    {
                        networkManager.StopClient();
                    }
                    if (NetworkServer.active)
                    {
                        networkManager.StopServer();
                    }
                    
                    // Даем время транспорту закрыть сокеты и очистить ресурсы
                    // Используем короткую задержку, так как в OnApplicationQuit нельзя использовать корутины
                    System.Threading.Thread.Sleep(100);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamManager] Ошибка при остановке сети в OnApplicationQuit: {e.Message}");
                }
            }
            
            // Шаг 2: Завершаем Steam API только после остановки сети
            try
            {
                // Проверяем, что сеть действительно остановлена
                if (!NetworkServer.active && !NetworkClient.active)
                {
                    SteamAPI.Shutdown();
                    isSteamInitialized = false;
                    Debug.Log("[SteamManager] Steam API успешно завершен");
                }
                else
                {
                    Debug.LogWarning("[SteamManager] Сеть все еще активна при завершении Steam API");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamManager] Ошибка при завершении Steam API в OnApplicationQuit: {e.Message}");
            }
        }
        #endif
    }
}

