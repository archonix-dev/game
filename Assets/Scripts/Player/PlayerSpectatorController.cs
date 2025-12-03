using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// Контроллер для наблюдения за другими игроками после смерти
/// </summary>
public class PlayerSpectatorController : MonoBehaviour
{
    [Header("Spectator Settings")]
    [Tooltip("Скорость поворота камеры при наблюдении")]
    public float lookSpeed = 2f;
    
    [Tooltip("Смещение камеры относительно наблюдаемого игрока")]
    public Vector3 cameraOffset = new Vector3(0, 1.6f, 0);
    
    private Camera spectatorCamera;
    private PlayerController localPlayerController;
    private List<PlayerController> alivePlayers = new List<PlayerController>();
    private int currentSpectatedPlayerIndex = -1;
    private bool isSpectating = false;
    
    void Start()
    {
        // Находим локального игрока
        FindLocalPlayer();
        
        // Находим камеру
        if (localPlayerController != null)
        {
            spectatorCamera = localPlayerController.GetComponentInChildren<Camera>();
        }
        
        if (spectatorCamera == null)
        {
            spectatorCamera = Camera.main;
        }
    }
    
    void Update()
    {
        // Проверяем, должен ли локальный игрок наблюдать
        if (localPlayerController != null)
        {
            // Проверяем, мертв ли локальный игрок (через рефлексию или публичное свойство)
            bool localPlayerDead = IsPlayerDead(localPlayerController);
            
            if (localPlayerDead && !isSpectating)
            {
                StartSpectating();
            }
            else if (!localPlayerDead && isSpectating)
            {
                StopSpectating();
            }
        }
        
        // Если наблюдаем, обрабатываем ввод
        if (isSpectating)
        {
            HandleSpectatorInput();
            UpdateSpectatorCamera();
        }
    }
    
    /// <summary>
    /// Находит локального игрока
    /// </summary>
    void FindLocalPlayer()
    {
        var allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var player in allPlayers)
        {
            if (player != null && player.isOwned)
            {
                localPlayerController = player;
                break;
            }
        }
    }
    
    /// <summary>
    /// Проверяет, мертв ли игрок
    /// </summary>
    bool IsPlayerDead(PlayerController player)
    {
        if (player == null) return false;
        
        // Используем публичный метод IsDead
        return player.IsDead();
    }
    
    /// <summary>
    /// Начинает наблюдение
    /// </summary>
    void StartSpectating()
    {
        isSpectating = true;
        UpdateAlivePlayersList();
        
        if (alivePlayers.Count > 0)
        {
            currentSpectatedPlayerIndex = 0;
        }
        else
        {
            currentSpectatedPlayerIndex = -1;
            // Если нет живых игроков, проверка будет выполнена в LobbyNetworkManager
        }
        
        // Отключаем камеру локального игрока, если она есть
        if (localPlayerController != null)
        {
            var localCamera = localPlayerController.GetComponentInChildren<Camera>();
            if (localCamera != null)
            {
                localCamera.enabled = false;
            }
        }
        
        // Включаем камеру наблюдателя
        if (spectatorCamera != null)
        {
            spectatorCamera.enabled = true;
        }
    }
    
    /// <summary>
    /// Останавливает наблюдение
    /// </summary>
    void StopSpectating()
    {
        isSpectating = false;
        currentSpectatedPlayerIndex = -1;
        
        // Включаем камеру локального игрока обратно
        if (localPlayerController != null)
        {
            var localCamera = localPlayerController.GetComponentInChildren<Camera>();
            if (localCamera != null)
            {
                localCamera.enabled = true;
            }
        }
    }
    
    /// <summary>
    /// Обновляет список живых игроков
    /// </summary>
    void UpdateAlivePlayersList()
    {
        alivePlayers.Clear();
        var allPlayers = FindObjectsOfType<PlayerController>();
        
        foreach (var player in allPlayers)
        {
            if (player != null && !IsPlayerDead(player))
            {
                alivePlayers.Add(player);
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает ввод наблюдателя
    /// </summary>
    void HandleSpectatorInput()
    {
        // ЛКМ - следующий игрок
        if (Input.GetMouseButtonDown(0))
        {
            SwitchToNextPlayer();
        }
        
        // ПКМ - предыдущий игрок
        if (Input.GetMouseButtonDown(1))
        {
            SwitchToPreviousPlayer();
        }
    }
    
    /// <summary>
    /// Переключается на следующего игрока
    /// </summary>
    void SwitchToNextPlayer()
    {
        UpdateAlivePlayersList();
        
        if (alivePlayers.Count == 0)
        {
            // Нет живых игроков - проверка будет выполнена в LobbyNetworkManager
            return;
        }
        
        currentSpectatedPlayerIndex = (currentSpectatedPlayerIndex + 1) % alivePlayers.Count;
    }
    
    /// <summary>
    /// Переключается на предыдущего игрока
    /// </summary>
    void SwitchToPreviousPlayer()
    {
        UpdateAlivePlayersList();
        
        if (alivePlayers.Count == 0)
        {
            // Нет живых игроков - проверка будет выполнена в LobbyNetworkManager
            return;
        }
        
        currentSpectatedPlayerIndex = (currentSpectatedPlayerIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
    }
    
    /// <summary>
    /// Обновляет позицию камеры наблюдателя
    /// </summary>
    void UpdateSpectatorCamera()
    {
        if (spectatorCamera == null) return;
        
        UpdateAlivePlayersList();
        
        if (alivePlayers.Count == 0)
        {
            // Нет живых игроков - проверка будет выполнена в LobbyNetworkManager
            return;
        }
        
        if (currentSpectatedPlayerIndex < 0 || currentSpectatedPlayerIndex >= alivePlayers.Count)
        {
            currentSpectatedPlayerIndex = 0;
        }
        
        PlayerController targetPlayer = alivePlayers[currentSpectatedPlayerIndex];
        if (targetPlayer == null) return;
        
        // Позиционируем камеру относительно наблюдаемого игрока
        Vector3 targetPosition = targetPlayer.transform.position + cameraOffset;
        spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, targetPosition, Time.deltaTime * 5f);
        
        // Поворачиваем камеру к игроку
        Vector3 lookDirection = (targetPlayer.transform.position - spectatorCamera.transform.position).normalized;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            spectatorCamera.transform.rotation = Quaternion.Slerp(spectatorCamera.transform.rotation, targetRotation, Time.deltaTime * lookSpeed);
        }
    }
    
    /// <summary>
    /// Вызывается когда все игроки мертвы (из PlayerController)
    /// </summary>
    public void OnAllPlayersDead()
    {
        if (!NetworkServer.active) return;
        
        Debug.Log("[PlayerSpectatorController] Получено уведомление о смерти всех игроков! Уведомляем LobbyNetworkManager...");
        
        // Перенаправляем в LobbyNetworkManager, который не удаляется при смерти игрока
        if (LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnAllPlayersDead();
        }
        else
        {
            Debug.LogWarning("[PlayerSpectatorController] LobbyNetworkManager не найден! Не можем вернуться в меню.");
        }
    }
    
    /// <summary>
    /// Возвращается в меню
    /// </summary>
    void ReturnToMenu()
    {
        // Устанавливаем флаг для открытия второго объекта при загрузке Menu
        CameraMovementController.SetShouldOpenSecondObjectOnMenuLoad();
        
        // Покидаем лобби
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LeaveLobby();
        }
        
        // Загружаем сцену Menu
        var networkManager = Mirror.NetworkManager.singleton;
        if (networkManager != null)
        {
            if (NetworkServer.active)
            {
                networkManager.StopHost();
            }
            else if (NetworkClient.active)
            {
                networkManager.StopClient();
            }
        }
        
        // Загружаем сцену Menu
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
}

