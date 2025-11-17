using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для синхронизации разрушения объектов в мультиплеере
/// Должен быть добавлен на объекты с DestructibleObject
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkDestructibleObject : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnIsDestroyedChanged))]
    private bool isDestroyed = false;
    
    [SyncVar(hook = nameof(OnCurrentHitsChanged))]
    private int currentHits = 0;
    
    private DestructibleObject destructibleObject;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        destructibleObject = GetComponent<DestructibleObject>();
        
        if (destructibleObject == null)
        {
            Debug.LogWarning($"[NetworkDestructibleObject] На объекте {gameObject.name} нет компонента DestructibleObject!");
        }
        
        // Убеждаемся что объект заспавнен (если еще не заспавнен)
        // Это обработается автоматически через LobbyNetworkManager.RegisterDestructibleObjects()
        // но на всякий случай проверяем здесь тоже
        if (netIdentity.netId == 0 && NetworkServer.active)
        {
            if (netIdentity.sceneId != 0 || netIdentity.assetId != 0)
            {
                NetworkServer.Spawn(gameObject);
            }
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        destructibleObject = GetComponent<DestructibleObject>();
    }
    
    /// <summary>
    /// Вызывается на сервере когда объект получает удар
    /// </summary>
    [Server]
    public void ServerTakeHit(float impactForce, Vector3 impactPoint, Vector3 impactDirection, GameObject sourceObject = null)
    {
        if (isDestroyed) return;
        
        if (destructibleObject != null && destructibleObject.objectData != null)
        {
            // Получаем текущее количество ударов напрямую из DestructibleObject
            // Используем рефлексию для доступа к приватному полю currentHits
            var currentHitsField = typeof(DestructibleObject).GetField("currentHits", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (currentHitsField != null)
            {
                int hits = (int)currentHitsField.GetValue(destructibleObject);
                hits++;
                currentHitsField.SetValue(destructibleObject, hits);
                currentHits = hits; // Синхронизируем с клиентами
                
                // Визуальные эффекты и звуки синхронизируются через RPC для всех клиентов
                RpcPlayHitEffects(impactPoint);
                
                // Проверка на разрушение
                if (hits >= destructibleObject.objectData.HitsToDestroy)
                {
                    ServerDestroyObject(impactPoint, impactDirection, impactForce);
                }
            }
        }
    }
    
    /// <summary>
    /// Вызывается на сервере для разрушения объекта
    /// </summary>
    [Server]
    public void ServerDestroyObject(Vector3 destructionPoint, Vector3 direction, float force)
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        if (destructibleObject != null)
        {
            // Вызываем метод разрушения (теперь он публичный)
            destructibleObject.DestroyObject(destructionPoint, direction, force);
            
            // Уничтожаем через NetworkServer после небольшой задержки
            // (чтобы эффекты и звуки успели проиграться)
            Invoke(nameof(DestroyNetworked), 3f);
        }
        else
        {
            // Если нет DestructibleObject, просто уничтожаем
            NetworkServer.Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Уничтожает объект через NetworkServer
    /// </summary>
    [Server]
    void DestroyNetworked()
    {
        if (gameObject != null)
        {
            NetworkServer.Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Хук для синхронизации состояния разрушения
    /// </summary>
    void OnIsDestroyedChanged(bool oldValue, bool newValue)
    {
        if (newValue && !oldValue)
        {
            // Объект был разрушен на сервере, применяем визуально на клиенте
            if (destructibleObject != null)
            {
                var disableMethod = typeof(DestructibleObject).GetMethod("DisableObjectVisually", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (disableMethod != null)
                {
                    disableMethod.Invoke(destructibleObject, null);
                }
            }
        }
    }
    
    /// <summary>
    /// Хук для синхронизации количества ударов
    /// </summary>
    void OnCurrentHitsChanged(int oldValue, int newValue)
    {
        // Можно добавить визуальную обратную связь на клиенте
        // Например, показывать трещины или эффекты
    }
    
    /// <summary>
    /// Публичный метод для получения удара (вызывается из DestructibleObject)
    /// </summary>
    public void TakeHitNetworked(float impactForce, Vector3 impactPoint, Vector3 impactDirection, GameObject sourceObject = null)
    {
        if (isServer)
        {
            ServerTakeHit(impactForce, impactPoint, impactDirection, sourceObject);
        }
        else if (isClient)
        {
            // Клиент отправляет команду на сервер
            CmdTakeHit(impactForce, impactPoint, impactDirection, sourceObject != null ? sourceObject.GetComponent<NetworkIdentity>()?.netId ?? 0 : 0);
        }
    }
    
    /// <summary>
    /// Команда клиента для отправки удара на сервер
    /// </summary>
    [Command(requiresAuthority = false)]
    void CmdTakeHit(float impactForce, Vector3 impactPoint, Vector3 impactDirection, uint sourceNetId)
    {
        GameObject sourceObject = null;
        if (sourceNetId != 0)
        {
            if (NetworkServer.spawned.TryGetValue(sourceNetId, out NetworkIdentity sourceIdentity))
            {
                sourceObject = sourceIdentity.gameObject;
            }
        }
        
        ServerTakeHit(impactForce, impactPoint, impactDirection, sourceObject);
    }
    
    /// <summary>
    /// RPC для синхронизации визуальных эффектов и звуков удара на всех клиентах
    /// </summary>
    [ClientRpc]
    void RpcPlayHitEffects(Vector3 impactPoint)
    {
        if (destructibleObject != null)
        {
            // Визуальные эффекты
            var playHitEffectMethod = typeof(DestructibleObject).GetMethod("PlayHitEffect", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (playHitEffectMethod != null)
            {
                playHitEffectMethod.Invoke(destructibleObject, new object[] { impactPoint });
            }
            
            // Звук удара
            var audioSourceField = typeof(DestructibleObject).GetField("audioSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (audioSourceField != null)
            {
                AudioSource audioSource = audioSourceField.GetValue(destructibleObject) as AudioSource;
                if (audioSource != null && destructibleObject.objectData != null && destructibleObject.objectData.HitSound != null)
                {
                    audioSource.PlayOneShot(destructibleObject.objectData.HitSound);
                }
            }
        }
    }
}

