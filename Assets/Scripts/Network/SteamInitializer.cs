using UnityEngine;

/// <summary>
/// Автоматически создает и инициализирует SteamManager при старте игры, если его нет в сцене.
/// Этот скрипт должен быть добавлен на GameObject в первой сцене (обычно Menu).
/// </summary>
public class SteamInitializer : MonoBehaviour
{
    [Header("Steam Settings")]
    [Tooltip("Steam App ID (должен совпадать с steam_appid.txt)")]
    public uint steamAppId = 480; // Spacewar - тестовое приложение Steam

    [Tooltip("Автоматически инициализировать Steam при старте")]
    public bool autoInitialize = true;

    [Tooltip("Создавать SteamManager автоматически, если его нет")]
    public bool autoCreateSteamManager = true;

    void Awake()
    {
        // Проверяем, существует ли SteamManager
        if (SteamManager.Instance == null && autoCreateSteamManager)
        {
            Debug.Log("[SteamInitializer] SteamManager не найден, создаем автоматически...");
            CreateSteamManager();
        }
    }

    void Start()
    {
        // Убеждаемся, что SteamManager инициализирован
        if (SteamManager.Instance != null)
        {
            // Обновляем настройки SteamManager, если они были изменены
            if (steamAppId != 0 && SteamManager.Instance.steamAppId != steamAppId)
            {
                SteamManager.Instance.steamAppId = steamAppId;
            }

            if (autoInitialize && !SteamManager.Instance.IsSteamInitialized())
            {
                Debug.Log("[SteamInitializer] Инициализируем Steam...");
                bool success = SteamManager.Instance.InitializeSteam();
                if (success)
                {
                    Debug.Log("[SteamInitializer] ✓ Steam успешно инициализирован!");
                }
                else
                {
                    Debug.LogError("[SteamInitializer] ✗ Не удалось инициализировать Steam. Убедитесь, что Steam клиент запущен.");
                }
            }
        }
        else if (autoCreateSteamManager)
        {
            Debug.LogWarning("[SteamInitializer] SteamManager все еще не найден после попытки создания!");
        }
    }

    /// <summary>
    /// Создает GameObject с компонентом SteamManager
    /// </summary>
    private void CreateSteamManager()
    {
        GameObject steamManagerObj = new GameObject("SteamManager");
        SteamManager steamManager = steamManagerObj.AddComponent<SteamManager>();
        
        // Устанавливаем настройки
        steamManager.steamAppId = steamAppId;
        steamManager.autoInitialize = autoInitialize;

        Debug.Log($"[SteamInitializer] SteamManager создан с App ID: {steamAppId}, Auto Initialize: {autoInitialize}");
    }
}

