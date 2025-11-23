using UnityEngine;
using Mirror;

/// <summary>
/// Обрабатывает столкновение игрока с объектами с тегом "Over"
/// Переносит игрока в точку спавна и отнимает здоровье
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerHealthStamina))]
public class OverZoneHandler : NetworkBehaviour
{
    [Header("Settings")]
    [Tooltip("Урон при соприкосновении с зоной Over")]
    [SerializeField] private float damageAmount = 10f;
    
    [Tooltip("Задержка между обработками столкновений (в секундах)")]
    [SerializeField] private float collisionCooldown = 1f;
    
    private PlayerController playerController;
    private PlayerHealthStamina playerHealthStamina;
    private float lastCollisionTime = -1f;
    
    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerHealthStamina = GetComponent<PlayerHealthStamina>();
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Проверяем, что это объект с тегом "Over"
        if (other.CompareTag("Over"))
        {
            HandleOverCollision();
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Проверяем, что это объект с тегом "Over"
        if (collision.gameObject.CompareTag("Over"))
        {
            HandleOverCollision();
        }
    }
    
    /// <summary>
    /// Обрабатывает столкновение с зоной Over
    /// </summary>
    void HandleOverCollision()
    {
        // Проверяем кулдаун
        if (Time.time - lastCollisionTime < collisionCooldown)
        {
            return;
        }
        
        lastCollisionTime = Time.time;
        
        // Получаем LobbyPlayerSpawner
        LobbyPlayerSpawner spawner = LobbyPlayerSpawner.Instance;
        if (spawner == null)
        {
            Debug.LogWarning("[OverZoneHandler] LobbyPlayerSpawner не найден!");
            return;
        }
        
        // Получаем точки спавна
        Transform[] spawnPoints = spawner.spawnPoints;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[OverZoneHandler] Точки спавна не найдены!");
            return;
        }
        
        // Выбираем случайную точку спавна
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform selectedSpawnPoint = spawnPoints[randomIndex];
        
        if (selectedSpawnPoint == null)
        {
            Debug.LogWarning($"[OverZoneHandler] Точка спавна {randomIndex} не назначена!");
            return;
        }
        
        Vector3 spawnPosition = selectedSpawnPoint.position;
        Quaternion spawnRotation = selectedSpawnPoint.rotation;
        
        // Проверяем текущее здоровье перед нанесением урона
        float currentHealth = playerHealthStamina.GetCurrentHealth();
        bool willDie = (currentHealth <= damageAmount);
        
        // Если игрок умрет, устанавливаем кастомную позицию для спавна трупа
        if (willDie)
        {
            if (isServer)
            {
                playerController.SetCustomCorpseSpawnPosition(spawnPosition);
            }
            else if (isOwned)
            {
                // Вызываем команду для установки позиции спавна трупа
                SetCorpseSpawnPositionCommand(spawnPosition);
            }
            Debug.Log($"[OverZoneHandler] Игрок умрет от урона. Позиция для спавна трупа установлена: {spawnPosition}");
        }
        else
        {
            // Переносим игрока только если он не умрет
            // Переносим игрока на сервере
            if (isServer)
            {
                TeleportPlayer(spawnPosition, spawnRotation);
            }
            else if (isOwned)
            {
                // Вызываем команду для телепортации
                TeleportPlayerCommand(spawnPosition, spawnRotation);
            }
        }
        
        // Наносим урон (UseHealth уже имеет [Command], так что вызываем его напрямую)
        // Команда будет выполнена на сервере автоматически
        if (isOwned || isServer)
        {
            playerHealthStamina.UseHealth(damageAmount);
        }
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию
    /// </summary>
    [Server]
    void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        if (playerController == null)
        {
            Debug.LogWarning("[OverZoneHandler] PlayerController не найден!");
            return;
        }
        
        // Отключаем CharacterController временно для телепортации
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        // Устанавливаем позицию и ротацию
        transform.position = position;
        transform.rotation = rotation;
        
        // Включаем CharacterController обратно
        if (controller != null)
        {
            controller.enabled = true;
        }
        
        Debug.Log($"[OverZoneHandler] Игрок телепортирован в позицию {position}");
        
        // Синхронизируем телепортацию с клиентами
        RpcTeleportPlayer(position, rotation);
    }
    
    /// <summary>
    /// Command для телепортации игрока (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    void TeleportPlayerCommand(Vector3 position, Quaternion rotation)
    {
        TeleportPlayer(position, rotation);
    }
    
    /// <summary>
    /// RPC для синхронизации телепортации с клиентами
    /// </summary>
    [ClientRpc]
    void RpcTeleportPlayer(Vector3 position, Quaternion rotation)
    {
        if (isServer) return; // Сервер уже установил позицию
        
        // Отключаем CharacterController временно для телепортации
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        // Устанавливаем позицию и ротацию
        transform.position = position;
        transform.rotation = rotation;
        
        // Включаем CharacterController обратно
        if (controller != null)
        {
            controller.enabled = true;
        }
    }
    
    /// <summary>
    /// Command для установки позиции спавна трупа (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command]
    void SetCorpseSpawnPositionCommand(Vector3 position)
    {
        if (playerController != null)
        {
            playerController.SetCustomCorpseSpawnPosition(position);
        }
    }
}

