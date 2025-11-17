using UnityEngine;
using Steamworks;

/// <summary>
/// Инициализирует Steam при старте сцены Menu
/// </summary>
public class SteamInitializer : MonoBehaviour
{
    private static bool steamInitialized = false;
    
    void Awake()
    {
        // Сохраняем объект между сценами, чтобы Steam не выключался
        DontDestroyOnLoad(gameObject);
        
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
                Debug.Log("[SteamInitializer] Steam успешно инициализирован");
                Debug.Log($"[SteamInitializer] Steam ID: {SteamUser.GetSteamID()}");
                Debug.Log($"[SteamInitializer] Имя пользователя: {SteamFriends.GetPersonaName()}");
            }
            else
            {
                Debug.LogError("[SteamInitializer] Не удалось инициализировать Steam. Убедитесь, что Steam запущен.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamInitializer] Ошибка инициализации Steam: {e.Message}");
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
        }
    }
}

