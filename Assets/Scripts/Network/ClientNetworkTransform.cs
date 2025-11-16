using UnityEngine;
using Mirror;

/// <summary>
/// Client-authoritative NetworkTransform для Mirror.
/// Позволяет клиенту управлять своей позицией и синхронизировать её с сервером.
/// Альтернатива NetworkTransform, если он не доступен в вашей версии Mirror.
/// </summary>
public class ClientNetworkTransform : NetworkBehaviour
{
    [Header("Sync Settings")]
    [Tooltip("Синхронизировать позицию")]
    public bool syncPosition = true;
    
    [Tooltip("Синхронизировать поворот")]
    public bool syncRotation = true;
    
    [Tooltip("Синхронизировать масштаб")]
    public bool syncScale = false;
    
    [Tooltip("Интервал синхронизации (секунды)")]
    [SerializeField]
    private float clientSyncInterval = 0.1f;
    
    /// <summary>
    /// Публичное свойство для доступа к интервалу синхронизации
    /// </summary>
    public float SyncInterval
    {
        get { return clientSyncInterval; }
        set { clientSyncInterval = value; }
    }
    
    // Синхронизированные значения (только для чтения на клиентах)
    [SyncVar(hook = nameof(OnPositionChanged))]
    private Vector3 networkPosition;
    
    [SyncVar(hook = nameof(OnRotationChanged))]
    private Quaternion networkRotation;
    
    [SyncVar(hook = nameof(OnScaleChanged))]
    private Vector3 networkScale;
    
    [System.NonSerialized]
    private float clientLastSyncTime = 0f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
    
    void Update()
    {
        if (netIdentity != null && isOwned)
        {
            // Владелец отправляет свою позицию на сервер
            if (Time.time - clientLastSyncTime >= clientSyncInterval)
            {
                bool positionChanged = syncPosition && Vector3.Distance(transform.position, lastPosition) > 0.01f;
                bool rotationChanged = syncRotation && Quaternion.Angle(transform.rotation, lastRotation) > 1f;
                bool scaleChanged = syncScale && Vector3.Distance(transform.localScale, lastScale) > 0.01f;
                
                if (positionChanged || rotationChanged || scaleChanged)
                {
                    if (isServer)
                    {
                        // На сервере обновляем напрямую
                        if (positionChanged) networkPosition = transform.position;
                        if (rotationChanged) networkRotation = transform.rotation;
                        if (scaleChanged) networkScale = transform.localScale;
                    }
                    else
                    {
                        // Отправляем на сервер через Command
                        UpdateTransformCommand(transform.position, transform.rotation, transform.localScale);
                    }
                    
                    lastPosition = transform.position;
                    lastRotation = transform.rotation;
                    lastScale = transform.localScale;
                    clientLastSyncTime = Time.time;
                }
            }
        }
        else
        {
            // Другие клиенты интерполируют позицию
            if (syncPosition)
            {
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            }
            
            if (syncRotation)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
            }
            
            if (syncScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, networkScale, Time.deltaTime * 10f);
            }
        }
    }
    
    [Command]
    void UpdateTransformCommand(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (syncPosition) networkPosition = position;
        if (syncRotation) networkRotation = rotation;
        if (syncScale) networkScale = scale;
    }
    
    void OnPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        // Hook вызывается автоматически при изменении SyncVar
        if (netIdentity != null && !isOwned && syncPosition)
        {
            networkPosition = newPos;
        }
    }
    
    void OnRotationChanged(Quaternion oldRot, Quaternion newRot)
    {
        if (netIdentity != null && !isOwned && syncRotation)
        {
            networkRotation = newRot;
        }
    }
    
    void OnScaleChanged(Vector3 oldScale, Vector3 newScale)
    {
        if (netIdentity != null && !isOwned && syncScale)
        {
            networkScale = newScale;
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Инициализируем значения при старте
        if (syncPosition) networkPosition = transform.position;
        if (syncRotation) networkRotation = transform.rotation;
        if (syncScale) networkScale = transform.localScale;
        
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.localScale;
    }
}
