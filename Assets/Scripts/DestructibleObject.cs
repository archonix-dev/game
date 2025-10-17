using UnityEngine;

/// <summary>
/// Компонент для объектов которые могут быть разрушены после определенного количества ударов
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private DestructibleObjectData objectData;
    
    private int currentHits = 0;
    private Rigidbody rb;
    private AudioSource audioSource;
    private MeshFilter meshFilter;
    private Renderer objectRenderer;
    
    // Для предотвращения множественных ударов в один кадр
    private float lastHitTime = 0f;
    private float hitCooldown = 0.1f;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        meshFilter = GetComponent<MeshFilter>();
        objectRenderer = GetComponent<Renderer>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D звук
            audioSource.playOnAwake = false;
        }
        
        // ВАЖНО: Удаляем ObjectMaterial если он есть!
        // ObjectMaterial вызывает DamageMesh при КАЖДОМ ударе, а нам нужно только при полном разрушении
        // Вместо этого мы добавим ObjectMaterial только при полном разрушении в DestroyWithMeshSlicer()
        ObjectMaterial existingMaterial = GetComponent<ObjectMaterial>();
        if (existingMaterial != null)
        {
            Debug.LogWarning($"DestructibleObject: Удаляем ObjectMaterial с {gameObject.name}. ObjectMaterial несовместим с системой счетчика ударов!");
            Destroy(existingMaterial);
        }
    }
    
    void Start()
    {
        if (objectData == null)
        {
            Debug.LogWarning($"DestructibleObject на {gameObject.name} не имеет назначенного DestructibleObjectData!");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект получает удар
    /// </summary>
    public void TakeHit(float impactForce, Vector3 impactPoint, Vector3 impactDirection)
    {
        if (objectData == null) return;
        
        // Проверка cooldown
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;
        
        // Проверяем минимальную силу удара
        if (impactForce < objectData.MinimumImpactForce)
        {
            Debug.Log($"Удар слишком слабый: {impactForce} < {objectData.MinimumImpactForce}");
            return;
        }
        
        currentHits++;
        Debug.Log($"{gameObject.name} получил удар! {currentHits}/{objectData.HitsToDestroy} (сила: {impactForce})");
        
        // Визуальные эффекты
        PlayHitEffect(impactPoint);
        
        // Звук удара
        if (objectData.HitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(objectData.HitSound);
        }
        
        // Проверка на разрушение
        if (currentHits >= objectData.HitsToDestroy)
        {
            DestroyObject(impactPoint, impactDirection, impactForce);
        }
    }
    
    /// <summary>
    /// Разрушает объект и выдает награды
    /// </summary>
    private void DestroyObject(Vector3 destructionPoint, Vector3 direction, float force)
    {
        Debug.Log($"{gameObject.name} разрушен!");
        
        // ВАЖНО: Сразу отключаем визуальное представление и коллайдеры
        // чтобы объект исчез визуально даже если уничтожение займет время
        DisableObjectVisually();
        
        // Воспроизводим звук разрушения
        if (objectData.DestroySound != null)
        {
            // Создаем временный объект для звука
            GameObject soundObject = new GameObject("DestroySound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = objectData.DestroySound;
            tempAudio.spatialBlend = 1f;
            tempAudio.Play();
            Destroy(soundObject, objectData.DestroySound.length);
        }
        
        // Визуальный эффект разрушения
        if (objectData.DestroyEffectPrefab != null)
        {
            GameObject effect = Instantiate(objectData.DestroyEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        // Выдаем монеты
        int coinsToGive = objectData.GetRandomCoinAmount();
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(coinsToGive);
        }
        
        // Реалистичное разрушение через MeshSlicer
        if (objectData.UseRealisticDestruction && meshFilter != null && meshFilter.mesh != null && MeshCutterManager.Instance != null)
        {
            DestroyWithMeshSlicer(force);
        }
        else
        {
            // Простое разрушение - создаем осколки как дочерние объекты
            if (objectData.UseRealisticDestruction && MeshCutterManager.Instance == null)
            {
                Debug.LogWarning($"MeshCutterManager не найден! Используем простое разрушение для {gameObject.name}");
            }
            CreateSimpleFragments(destructionPoint, direction);
            
            // Удаляем весь родительский объект вместе со всеми дочерними осколками через 3 секунды
            Destroy(gameObject, 3f);
        }
    }
    
    /// <summary>
    /// Отключает визуальное представление объекта (рендерер, коллайдеры)
    /// </summary>
    private void DisableObjectVisually()
    {
        // Отключаем рендерер
        if (objectRenderer != null)
        {
            objectRenderer.enabled = false;
        }
        
        // Отключаем все коллайдеры
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }
    
    /// <summary>
    /// Использует MeshSlicer для реалистичного разрушения (вызывается ТОЛЬКО при полном разрушении!)
    /// </summary>
    private void DestroyWithMeshSlicer(float impactForce)
    {
        bool meshSlicerSuccess = false;
        
        if (MeshCutterManager.Instance != null)
        {
            try
            {
                // Добавляем ObjectMaterial ТОЛЬКО для MeshSlicer разрушения
                ObjectMaterial objMat = gameObject.AddComponent<ObjectMaterial>();
                objMat.MaterialType = objectData.MaterialType;
                
                // Вычисляем количество разрезов из настроек ScriptableObject
                int shatterAmount = objectData.CalculateShatterAmount(impactForce);
                
                // Используем КАСТОМНЫЙ метод MeshSlicer с настройками из DestructibleObjectData
                // Передаем transform родителя чтобы осколки стали дочерними объектами
                // И время удаления через 3 секунды
                MeshCutterManager.Instance.DamageMeshCustom(gameObject, shatterAmount, transform, 3f);
                meshSlicerSuccess = true;
                
                Debug.Log($"[РАЗРУШЕНИЕ] MeshSlicer применен к {gameObject.name} после {currentHits} ударов. " +
                         $"Количество разрезов: {shatterAmount} (базовое: {objectData.ShatterAmount}, " +
                         $"с учетом силы: {objectData.UseImpactForceMultiplier}). " +
                         $"Осколки будут дочерними и удалятся через 3 секунды.");
                
                // ВАЖНО: Уничтожаем оригинальный объект (с дочерними осколками) через 3 секунды
                Destroy(gameObject, 3f);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка MeshSlicer для {gameObject.name}: {e.Message}. Используем простое разрушение.");
                meshSlicerSuccess = false;
            }
        }
        else
        {
            Debug.LogWarning("MeshCutterManager не найден в сцене! Используем простое разрушение.");
        }
        
        // Если MeshSlicer не сработал, используем простое разрушение
        if (!meshSlicerSuccess)
        {
            CreateSimpleFragments(transform.position, Vector3.down);
            // Удаляем весь объект через 3 секунды вместе с осколками
            Destroy(gameObject, 3f);
        }
    }
    
    /// <summary>
    /// Создает простые осколки если MeshSlicer не используется
    /// ОСКОЛКИ СОЗДАЮТСЯ КАК ДОЧЕРНИЕ ОБЪЕКТЫ, затем отсоединяются для физики
    /// </summary>
    private void CreateSimpleFragments(Vector3 position, Vector3 direction)
    {
        int fragmentCount = Random.Range(5, 10);
        Debug.Log($"[РАЗРУШЕНИЕ] Создаем {fragmentCount} осколков как дочерние объекты {gameObject.name}");
        
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = $"{gameObject.name}_Fragment_{i}";
            
            // Сначала делаем дочерним для организации иерархии
            fragment.transform.SetParent(transform);
            
            fragment.transform.position = position + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);
            fragment.transform.rotation = Random.rotation;
            
            // Копируем материал
            if (objectRenderer != null)
            {
                Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
                fragmentRenderer.material = objectRenderer.material;
            }
            
            // Добавляем физику
            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.mass = rb.mass / fragmentCount;
            fragmentRb.useGravity = true;
            
            // ВАЖНО: Отсоединяем от родителя для корректной работы физики
            // Но сохраняем мировые координаты
            fragment.transform.SetParent(transform, true);
            
            // Добавляем силу от точки удара
            Vector3 explosionForce = (fragment.transform.position - position).normalized;
            explosionForce += direction.normalized;
            fragmentRb.AddForce(explosionForce * 3f, ForceMode.Impulse);
            fragmentRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
        }
        
        Debug.Log($"[РАЗРУШЕНИЕ] Создано {fragmentCount} осколков. Весь объект {gameObject.name} (включая осколки) будет удален через 3 секунды");
    }
    
    /// <summary>
    /// Воспроизводит эффект удара
    /// </summary>
    private void PlayHitEffect(Vector3 position)
    {
        if (objectData.HitEffectPrefab != null)
        {
            GameObject effect = Instantiate(objectData.HitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    /// <summary>
    /// Получить текущее количество ударов
    /// </summary>
    public int GetCurrentHits() => currentHits;
    
    /// <summary>
    /// Получить оставшееся количество ударов до разрушения
    /// </summary>
    public int GetRemainingHits()
    {
        if (objectData == null) return 0;
        return Mathf.Max(0, objectData.HitsToDestroy - currentHits);
    }
    
    /// <summary>
    /// Сбросить счетчик ударов
    /// </summary>
    public void ResetHits()
    {
        currentHits = 0;
    }
    
    void OnDrawGizmosSelected()
    {
        if (objectData != null)
        {
            // Отображаем информацию об объекте
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
                $"Прочность: {currentHits}/{objectData.HitsToDestroy}\nМонеты: {objectData.MinCoins}-{objectData.MaxCoins}");
            #endif
        }
    }
}

