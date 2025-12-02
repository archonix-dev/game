using UnityEngine;
using Steamworks;

/// <summary>
/// Инициализирует Steam при старте сцены Menu
/// </summary>
public class SteamInitializer : MonoBehaviour
{
    private static bool steamInitialized = false;
    private static bool steamInitError = false;
    private static string steamErrorMessage = null;

    public static bool IsSteamInitialized => steamInitialized;
    public static bool HasSteamError => steamInitError;
    public static string SteamErrorMessage => steamErrorMessage;
    
    void Awake()
    {
        // Сохраняем объект между сценами, чтобы Steam не выключался
        DontDestroyOnLoad(gameObject);
        
        // Сбрасываем флаги ошибки перед новой попыткой
        steamInitError = false;
        steamErrorMessage = null;
        
        if (steamInitialized)
        {
            Debug.Log("[SteamInitializer] Steam уже инициализирован");
            return;
        }
        
        try
        {
            // Инициализируем Steam
            if (SteamAPI.Init())
            {
                steamInitialized = true;
                steamInitError = false;
                steamErrorMessage = null;
                Debug.Log("[SteamInitializer] Steam успешно инициализирован");
                Debug.Log($"[SteamInitializer] Steam ID: {SteamUser.GetSteamID()}");
                Debug.Log($"[SteamInitializer] Имя пользователя: {SteamFriends.GetPersonaName()}");
            }
            else
            {
                steamInitError = true;
                steamErrorMessage = "Не удалось инициализировать Steam. Убедитесь, что Steam запущен.";
                Debug.LogError($"[SteamInitializer] {steamErrorMessage}");
            }
        }
        catch (System.Exception e)
        {
            steamInitError = true;
            steamErrorMessage = $"Ошибка инициализации Steam: {e.Message}";
            Debug.LogError($"[SteamInitializer] {steamErrorMessage}");
        }
    }
    
    void Update()
    {
        // Необходимо вызывать SteamAPI.RunCallbacks() каждый кадр
        if (steamInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }
    
    void OnDestroy()
    {
        // НЕ выключаем Steam при уничтожении объекта при смене сцены
        // Выключаем только при выходе из приложения
        // Steam будет выключен в OnApplicationQuit
    }
    
    void OnApplicationQuit()
    {
        if (steamInitialized)
        {
            SteamAPI.Shutdown();
            steamInitialized = false;
            steamInitError = false;
            steamErrorMessage = null;
        }
    }
}

