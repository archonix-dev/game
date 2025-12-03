using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Инициализатор сцены Menu - обрабатывает загрузку сцены и настройку UI
/// </summary>
public class MenuSceneInitializer : MonoBehaviour
{
    [Header("Auto-recreate Lobby")]
    [Tooltip("Автоматически пересоздавать лобби после возврата в Menu (например, после смерти всех игроков)")]
    public bool autoRecreateLobby = true;
    
    [Tooltip("Задержка перед пересозданием лобби (секунды)")]
    public float recreateLobbyDelay = 1.5f;
    
    void Start()
    {
        // Разблокируем курсор при загрузке Menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Убеждаемся, что сеть полностью отключена
        var networkManager = Mirror.NetworkManager.singleton;
        if (networkManager != null)
        {
            if (Mirror.NetworkServer.active)
            {
                networkManager.StopHost();
            }
            else if (Mirror.NetworkClient.active)
            {
                networkManager.StopClient();
            }
        }
        
        // Очищаем лобби, если оно еще активно
        bool wasInLobby = false;
        if (LobbyManager.Instance != null)
        {
            // Проверяем, были ли мы в лобби ДО вызова LeaveLobby
            wasInLobby = LobbyManager.Instance.CurrentLobbyID.IsValid();
            
            Debug.Log($"[MenuSceneInitializer] Проверка лобби: wasInLobby={wasInLobby}, CurrentLobbyID.IsValid()={LobbyManager.Instance.CurrentLobbyID.IsValid()}");
            
            // Покидаем лобби, если мы еще в нем
            if (wasInLobby)
            {
                Debug.Log("[MenuSceneInitializer] Покидаем текущее лобби перед пересозданием...");
                LobbyManager.Instance.LeaveLobby();
            }
        }
        else
        {
            Debug.LogWarning("[MenuSceneInitializer] LobbyManager.Instance == null!");
        }
        
        // Обновляем список игроков в лобби с задержкой
        // Это нужно для случая, когда мы вернулись в Menu после смерти всех игроков
        Invoke(nameof(UpdateLobbyPlayerList), 1f);
        
        // Автоматически пересоздаем лобби, если мы были в лобби и включена опция
        if (autoRecreateLobby && wasInLobby)
        {
            Debug.Log($"[MenuSceneInitializer] Запускаем корутину пересоздания лобби (autoRecreateLobby={autoRecreateLobby}, wasInLobby={wasInLobby})...");
            StartCoroutine(RecreateLobbyDelayed());
        }
        else
        {
            Debug.Log($"[MenuSceneInitializer] Не пересоздаем лобби: autoRecreateLobby={autoRecreateLobby}, wasInLobby={wasInLobby}");
        }
    }
    
    /// <summary>
    /// Обновляет список игроков в лобби
    /// </summary>
    void UpdateLobbyPlayerList()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    
    /// <summary>
    /// Пересоздает лобби с задержкой
    /// </summary>
    IEnumerator RecreateLobbyDelayed()
    {
        Debug.Log($"[MenuSceneInitializer] Ожидание {recreateLobbyDelay} секунд перед пересозданием лобби...");
        yield return new WaitForSeconds(recreateLobbyDelay);
        
        // Проверяем, что мы все еще на сцене Menu
        if (SceneManager.GetActiveScene().name != "Menu")
        {
            Debug.Log("[MenuSceneInitializer] Уже не на сцене Menu, не пересоздаем лобби");
            yield break;
        }
        
        // Проверяем, что сеть отключена
        if (Mirror.NetworkServer.active || Mirror.NetworkClient.active)
        {
            Debug.LogWarning("[MenuSceneInitializer] Сеть все еще активна, не пересоздаем лобби");
            yield break;
        }
        
        // Проверяем, что мы не в лобби
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
        {
            Debug.Log("[MenuSceneInitializer] Уже в лобби, не пересоздаем");
            yield break;
        }
        
        Debug.Log("[MenuSceneInitializer] Пересоздаем лобби через Steam...");
        
        // Пересоздаем лобби
        if (LobbyManager.Instance != null)
        {
            // Проверяем, что Steam запущен
            if (!Steamworks.SteamAPI.IsSteamRunning())
            {
                Debug.LogError("[MenuSceneInitializer] Steam не запущен! Не можем пересоздать лобби.");
                yield break;
            }
            
            Debug.Log("[MenuSceneInitializer] Вызываем LobbyManager.Instance.CreateLobby()...");
            LobbyManager.Instance.CreateLobby();
            Debug.Log("[MenuSceneInitializer] CreateLobby() вызван, ждем создания лобби...");
            
            // Ждем немного для создания лобби через Steam (Steam callback может занять время)
            yield return new WaitForSeconds(3f);
            
            // Проверяем, что лобби создано
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyID.IsValid())
            {
                Debug.Log($"[MenuSceneInitializer] Лобби успешно создано! ID: {LobbyManager.Instance.CurrentLobbyID}");
            }
            else
            {
                Debug.LogWarning("[MenuSceneInitializer] Лобби не создано или CurrentLobbyID не валиден!");
            }
            
            // Ждем еще немного для инициализации сети и спавна игроков
            yield return new WaitForSeconds(2f);
            
            // Обновляем список игроков после создания лобби
            if (LobbyManager.Instance != null)
            {
                Debug.Log("[MenuSceneInitializer] Обновляем список игроков после пересоздания лобби...");
                LobbyManager.Instance.UpdatePlayerList();
            }
            
            // Обновляем список лобби (если есть UI для этого)
            var lobbyMenuUI = FindObjectOfType<LobbyMenuUI>();
            if (lobbyMenuUI != null)
            {
                Debug.Log("[MenuSceneInitializer] LobbyMenuUI найден, UI должен обновиться автоматически");
            }
            else
            {
                Debug.LogWarning("[MenuSceneInitializer] LobbyMenuUI не найден на сцене Menu!");
            }
        }
        else
        {
            Debug.LogWarning("[MenuSceneInitializer] LobbyManager не найден, не можем пересоздать лобби");
        }
    }
}

