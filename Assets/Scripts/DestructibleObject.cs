using UnityEngine;
using TMPro;

/// <summary>
/// Компонент для объектов которые могут быть захвачены и разрушены после определенного количества ударов
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    [Header("Настройки разрушения")]
    [SerializeField] private DestructibleObjectData objectData;
    
    [Header("Свойства захвата")]
    [Tooltip("Вес объекта в килограммах")]
    [SerializeField] public float objectWeight = 5f;
    
    [Tooltip("Насколько крепко можно держать объект (0-1)")]
    [SerializeField] private float gripStrength = 1f;
    
    [Tooltip("Центр масс объекта для реалистичной физики")]
    [SerializeField] private Vector3 centerOfMass = Vector3.zero;
    
    [Header("Хрупкость")]
    [SerializeField] private bool isFragile = false;
    [SerializeField] private float breakForceThreshold = 100f;
    
    [Header("Звуки захвата (опционально)")]
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip releaseSound;
    
    [Header("Отображение награды")]
    [SerializeField] private bool showRewardDisplay = true;
    [SerializeField] private float displayDistance = 5f;
    [SerializeField] private TextMeshPro rewardText;
    [SerializeField] private GameObject rewardDisplayObject;
    
    [Header("Настройки масштабирования")]
    [SerializeField] private float minScale = 0.01f;  // Минимальный размер (близко к объекту)
    [SerializeField] private float maxScale = 0.05f;  // Максимальный размер (далеко от объекта)
    [SerializeField] private float scaleSmoothTime = 0.1f; // Время плавного изменения размера
    
    [Header("Настройки фрагментов")]
    [Tooltip("Цвет фрагментов при разрушении")]
    [SerializeField] private Color fragmentColor = Color.white;
    
    [Header("Материалы outline")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Material normalMaterial;
    
    private int currentHits = 0;
    private Rigidbody rb;
    private AudioSource audioSource;
    private MeshFilter meshFilter;
    private Renderer objectRenderer;
    private Material[] originalMaterials;
    
    // Для предотвращения множественных ударов в один кадр
    private float lastHitTime = 0f;
    private float hitCooldown = 0.1f;
    
    // Для захвата объектов
    private bool isGrabbed = false;
    private float originalMass;
    private float originalDrag;
    private float originalAngularDrag;
    
    // Для отслеживания столкновений
    private Vector3 lastVelocity;
    
    // Для отображения награды
    private Transform playerTransform;
    private bool isPlayerInRange = false;
    private bool isPlayerLookingAt = false;
    private float currentScale = 0.01f;
    private float scaleVelocity = 0f;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        meshFilter = GetComponent<MeshFilter>();
        objectRenderer = GetComponent<Renderer>();
        
        if (objectRenderer != null)
        {
            originalMaterials = objectRenderer.materials;
        }
        
        // Настраиваем Rigidbody на основе веса
        if (rb != null)
        {
            rb.mass = objectWeight;
            rb.centerOfMass = centerOfMass;
            originalMass = rb.mass;
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
        }
        
        // Настраиваем аудио компонент
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D звук
            audioSource.playOnAwake = false;
            audioSource.volume = 0.5f;
        }
    }
    
    void Start()
    {
        if (objectData == null)
        {
            Debug.LogWarning($"DestructibleObject на {gameObject.name} не имеет назначенного DestructibleObjectData!");
        }
        
        // Автоматически назначаем тег если не назначен
        if (gameObject.tag == "Untagged")
        {
            gameObject.tag = "Grabbable";
        }
        
        // Проверяем наличие коллайдера
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"DestructibleObject на {gameObject.name} не имеет Collider! Добавьте коллайдер для корректной работы.");
        }
        
        // Инициализируем систему отображения награды
        InitializeRewardDisplay();
    }
    
    void Update()
    {
        // Обновляем отображение награды
        UpdateRewardDisplay();
    }
    
    void FixedUpdate()
    {
        if (rb != null)
        {
            lastVelocity = rb.linearVelocity;
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Вычисляем силу удара с учетом относительной скорости
        Vector3 relativeVelocity = lastVelocity;
        
        // Если у другого объекта есть Rigidbody, учитываем его скорость
        Rigidbody otherRb = collision.rigidbody;
        if (otherRb != null)
        {
            relativeVelocity -= otherRb.linearVelocity;
        }
        
        float impactForce = relativeVelocity.magnitude * rb.mass;
        
        // Получаем информацию о точке и направлении удара
        Vector3 impactPoint = collision.contacts[0].point;
        Vector3 impactDirection = collision.contacts[0].normal;
        
        // Проверяем столкновение с другим разрушаемым объектом
        DestructibleObject otherDestructible = collision.gameObject.GetComponent<DestructibleObject>();
        if (otherDestructible != null)
        {
            // Передаем информацию об ударе другому разрушаемому объекту
            otherDestructible.TakeHit(impactForce, impactPoint, impactDirection);
            
            Debug.Log($"{gameObject.name} ударил {collision.gameObject.name} с силой {impactForce}");
        }
        
        // ВАЖНО: Этот объект тоже получает урон от столкновения с любыми объектами
        // (включая обычные объекты без DestructibleObject компонента)
        if (objectData != null && impactForce > 0)
        {
            // Проверяем минимальную силу удара для получения урона
            if (impactForce >= objectData.MinimumImpactForce)
            {
                // Дополнительная проверка: не получаем урон если объект захвачен и удар очень слабый
                if (!isGrabbed || impactForce >= objectData.MinimumImpactForce * 2f)
                {
                    TakeHit(impactForce, impactPoint, impactDirection);
                    Debug.Log($"{gameObject.name} получил урон от столкновения с {collision.gameObject.name} (сила: {impactForce})");
                }
                else
                {
                    Debug.Log($"{gameObject.name} не получил урон - объект захвачен и удар слишком слабый (сила: {impactForce})");
                }
            }
            else
            {
                Debug.Log($"{gameObject.name} не получил урон - удар слишком слабый (сила: {impactForce} < {objectData.MinimumImpactForce})");
            }
        }
        
        // Если этот объект хрупкий и сила удара велика - ломаем его
        if (!isGrabbed && isFragile && impactForce > breakForceThreshold)
        {
            BreakObject(impactPoint);
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
    /// Вызывается когда объект захватывается
    /// </summary>
    public void OnGrabbed()
    {
        isGrabbed = true;
        
        // Скрываем отображение награды при захвате
        HideRewardDisplay();
        
        // Воспроизводим звук захвата
        if (audioSource != null && grabSound != null)
        {
            audioSource.PlayOneShot(grabSound);
        }
        
        // Можно добавить визуальные эффекты
        // Например, изменить цвет или включить частицы
    }
    
    /// <summary>
    /// Вызывается когда объект отпускается
    /// </summary>
    public void OnReleased()
    {
        isGrabbed = false;
        
        // Воспроизводим звук отпускания
        if (audioSource != null && releaseSound != null)
        {
            audioSource.PlayOneShot(releaseSound);
        }
        
        // Восстанавливаем оригинальные физические параметры
        if (rb != null)
        {
            rb.linearDamping = originalDrag;
            rb.angularDamping = originalAngularDrag;
        }
    }
    
    /// <summary>
    /// Разрушает хрупкий объект при сильном ударе
    /// </summary>
    void BreakObject(Vector3 impactPoint)
    {
        Debug.Log($"{gameObject.name} разбился!");
        
        // Отключаем визуальное представление сразу
        DisableObjectVisually();
        
        // Воспроизводим звук разрушения
        if (audioSource != null && objectData != null && objectData.DestroySound != null)
        {
            // Создаем временный объект для звука, чтобы он доиграл после уничтожения объекта
            GameObject soundObject = new GameObject("BreakSound");
            soundObject.transform.position = transform.position;
            AudioSource tempAudio = soundObject.AddComponent<AudioSource>();
            tempAudio.clip = objectData.DestroySound;
            tempAudio.spatialBlend = 1f;
            tempAudio.Play();
            Destroy(soundObject, objectData.DestroySound.length);
        }
        
        // Пытаемся использовать SimpleDestructionManager для реалистичного разрушения
        if (TryUseMeshCutter(impactPoint))
        {
            // SimpleDestructionManager успешно применен, уничтожаем объект с задержкой
            Destroy(gameObject, 0.1f);
        }
        else
        {
            // Используем простое разрушение
            CreateBreakEffect(impactPoint);
            Destroy(gameObject);
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
        int coinsToGive = objectData.GetCoinAmount();
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(coinsToGive);
        }
        
        // Реалистичное разрушение
        if (objectData.UseRealisticDestruction && meshFilter != null && meshFilter.mesh != null)
        {
            if (SimpleDestructionManager.Instance != null)
            {
                DestroyWithSimpleDestruction(destructionPoint, force);
            }
            else
            {
                Debug.LogWarning($"SimpleDestructionManager не найден! Используем базовое разрушение для {gameObject.name}");
                CreateSimpleFragments(destructionPoint, direction);
                Destroy(gameObject, 3f);
            }
        }
        else
        {
            // Простое разрушение - создаем осколки как дочерние объекты
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
    /// Использует SimpleDestructionManager для разрушения (РЕКОМЕНДУЕТСЯ!)
    /// </summary>
    private void DestroyWithSimpleDestruction(Vector3 destructionPoint, float impactForce)
    {
        // Вычисляем количество осколков из настроек ScriptableObject
        int fragmentCount = objectData.CalculateShatterAmount(impactForce);
        
        // Используем SimpleDestructionManager с цветом фрагментов
        SimpleDestructionManager.Instance.DestroyObjectWithColor(
            gameObject,
            fragmentCount,
            objectData.FragmentExplosionForce,
            destructionPoint,
            transform,
            fragmentColor,
            3f
        );
        
        Debug.Log($"[РАЗРУШЕНИЕ] SimpleDestruction применен к {gameObject.name} после {currentHits} ударов. " +
                 $"Количество осколков: {fragmentCount}, сила разлета: {objectData.FragmentExplosionForce}. " +
                 $"Осколки будут дочерними и удалятся через 3 секунды.");
        
        // Уничтожаем оригинальный объект (с дочерними осколками) через 3 секунды
        Destroy(gameObject, 3f);
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
            Vector3 explosionDir = (fragment.transform.position - position).normalized;
            explosionDir += direction.normalized;
            fragmentRb.AddForce(explosionDir * objectData.FragmentExplosionForce, ForceMode.Impulse);
            fragmentRb.AddTorque(Random.insideUnitSphere * objectData.FragmentExplosionForce, ForceMode.Impulse);
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
    /// Пытается использовать SimpleDestructionManager для реалистичного разрушения хрупких объектов
    /// </summary>
    bool TryUseMeshCutter(Vector3 impactPoint)
    {
        // Проверяем наличие необходимых компонентов
        if (meshFilter == null || meshFilter.mesh == null)
        {
            return false;
        }
        
        // Проверяем наличие SimpleDestructionManager
        if (SimpleDestructionManager.Instance == null)
        {
            return false;
        }
        
        try
        {
            // Вычисляем силу удара
            float impactForce = lastVelocity.magnitude * rb.mass;
            
            // Используем SimpleDestructionManager
            int fragmentCount = Mathf.RoundToInt(impactForce * 0.5f);
            fragmentCount = Mathf.Clamp(fragmentCount, 5, 15);
            
            SimpleDestructionManager.Instance.DestroyObjectWithColor(
                gameObject,
                fragmentCount,
                impactForce * 0.5f,
                transform.position,
                null,
                fragmentColor,
                3f
            );
            Debug.Log($"SimpleDestruction успешно применен к {gameObject.name} (сила: {impactForce}, осколков: {fragmentCount})");
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Не удалось применить разрушение к {gameObject.name}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Создает простые осколки (fallback если SimpleDestructionManager недоступен)
    /// </summary>
    void CreateBreakEffect(Vector3 position)
    {
        // Простой эффект разрушения - создаем несколько маленьких кубиков
        int fragmentCount = Random.Range(5, 10);
        
        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.transform.position = position + Random.insideUnitSphere * 0.5f;
            fragment.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);
            fragment.transform.rotation = Random.rotation;
            
            // Устанавливаем цвет фрагмента
            Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
            if (fragmentRenderer != null)
            {
                // Создаем простой материал с цветом для URP
                Material fragmentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fragmentMaterial.color = fragmentColor;
                fragmentRenderer.material = fragmentMaterial;
            }
            
            // Добавляем физику
            Rigidbody fragmentRb = fragment.AddComponent<Rigidbody>();
            fragmentRb.mass = objectWeight / fragmentCount;
            
            // Добавляем силу от точки удара
            Vector3 explosionDir = (fragment.transform.position - position).normalized;
            fragmentRb.AddForce(explosionDir * 5f, ForceMode.Impulse);
            fragmentRb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
            
            // Уничтожаем осколки через 3 секунды
            Destroy(fragment, 3f);
        }
    }
    
    // Методы для настройки объекта извне
    /// <summary>
    /// Установить вес объекта
    /// </summary>
    public void SetWeight(float weight)
    {
        objectWeight = weight;
        if (rb != null)
        {
            rb.mass = weight;
            originalMass = weight;
        }
    }
    
    /// <summary>
    /// Получить силу захвата
    /// </summary>
    public float GetGripStrength()
    {
        return gripStrength;
    }
    
    /// <summary>
    /// Проверить, захвачен ли объект в данный момент
    /// </summary>
    public bool IsCurrentlyGrabbed()
    {
        return isGrabbed;
    }
    
    /// <summary>
    /// Сбросить счетчик ударов
    /// </summary>
    public void ResetHits()
    {
        currentHits = 0;
    }
    
    /// <summary>
    /// Устанавливает состояние наведения игрока на объект
    /// </summary>
    public void SetPlayerLookingAt(bool looking)
    {
        isPlayerLookingAt = looking;
        UpdateOutlineMaterial();
    }
    
    /// <summary>
    /// Проверяет, смотрит ли игрок на объект
    /// </summary>
    public bool IsPlayerLookingAt()
    {
        return isPlayerLookingAt;
    }
    
    /// <summary>
    /// Принудительно обновляет текст награды
    /// </summary>
    public void RefreshRewardText()
    {
        UpdateRewardText();
    }
    
    /// <summary>
    /// Получить цвет фрагментов
    /// </summary>
    public Color GetFragmentColor()
    {
        return fragmentColor;
    }
    
    /// <summary>
    /// Инициализирует систему отображения награды
    /// </summary>
    void InitializeRewardDisplay()
    {
        if (!showRewardDisplay) 
        {
            Debug.Log($"Reward display disabled for {gameObject.name}");
            return;
        }
        
        if (objectData == null) 
        {
            Debug.LogWarning($"objectData is null for {gameObject.name} - reward display will not work!");
            return;
        }
        
        Debug.Log($"Initializing reward display for {gameObject.name} with {objectData.CoinAmount} coins");
        
        // Ищем игрока
        FindPlayer();
        
        // Создаем отображение награды если не назначено
        if (rewardDisplayObject == null)
        {
            CreateRewardDisplay();
        }
        
        // Скрываем отображение по умолчанию
        HideRewardDisplay();
    }
    
    /// <summary>
    /// Ищет игрока в сцене
    /// </summary>
    void FindPlayer()
    {
        // Ищем игрока по тегу
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            // Ищем камеру как альтернативу
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                playerTransform = mainCamera.transform;
            }
        }
    }
    
    /// <summary>
    /// Создает отображение награды
    /// </summary>
    void CreateRewardDisplay()
    {
        // Создаем GameObject для отображения награды
        rewardDisplayObject = new GameObject("RewardDisplay");
        rewardDisplayObject.transform.SetParent(transform);
        rewardDisplayObject.transform.localPosition = Vector3.up * 2f; // Над объектом
        rewardDisplayObject.transform.localScale = Vector3.one * minScale; // Устанавливаем начальный размер
        
        // Добавляем TextMeshPro
        rewardText = rewardDisplayObject.AddComponent<TextMeshPro>();
        rewardText.fontSize = 2f;
        rewardText.color = Color.yellow;
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.sortingOrder = 10;
        
        // Устанавливаем текст награды
        rewardText.text = GetRewardText();
        
        // Настраиваем шрифт
        rewardText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (rewardText.font == null)
        {
            // Используем стандартный шрифт если не найден
            rewardText.font = Resources.GetBuiltinResource<TMP_FontAsset>("Legacy Runtime/TextMeshPro/Fonts & Materials/LiberationSans SDF");
        }
        
        Debug.Log($"Создано отображение награды для {gameObject.name}");
    }
    
    /// <summary>
    /// Обновляет отображение награды
    /// </summary>
    void UpdateRewardDisplay()
    {
        if (!showRewardDisplay || objectData == null || isGrabbed) return;
        
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }
        
        // Проверяем расстояние до игрока
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = distanceToPlayer <= displayDistance;
        
        // Показываем только если игрок в радиусе И смотрит на объект
        bool shouldShow = inRange && isPlayerLookingAt;
        
        if (shouldShow != isPlayerInRange)
        {
            isPlayerInRange = shouldShow;
            
            if (isPlayerInRange)
            {
                ShowRewardDisplay();
            }
            else
            {
                HideRewardDisplay();
            }
        }
        
        // Поворачиваем текст к игроку и обновляем размер
        if (isPlayerInRange && rewardDisplayObject != null)
        {
            rewardDisplayObject.transform.LookAt(playerTransform);
            // Поворачиваем на 180 градусов чтобы текст был читаемым
            rewardDisplayObject.transform.Rotate(0, 180, 0);
            
            // Обновляем размер в зависимости от расстояния
            UpdateRewardScale(distanceToPlayer);
            
            // Обновляем текст награды
            UpdateRewardText();
        }
    }
    
    /// <summary>
    /// Обновляет размер отображения награды в зависимости от расстояния до игрока
    /// </summary>
    void UpdateRewardScale(float distanceToPlayer)
    {
        if (rewardDisplayObject == null) return;
        
        // Вычисляем целевой размер на основе расстояния
        // Чем дальше игрок, тем больше размер (но в пределах displayDistance)
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / displayDistance);
        
        // Инвертируем: далеко = большой размер, близко = маленький размер
        float targetScale = Mathf.Lerp(minScale, maxScale, normalizedDistance);
        
        // Плавно изменяем размер
        currentScale = Mathf.SmoothDamp(currentScale, targetScale, ref scaleVelocity, scaleSmoothTime);
        
        // Применяем размер
        rewardDisplayObject.transform.localScale = Vector3.one * currentScale;
    }
    
    /// <summary>
    /// Обновляет текст награды
    /// </summary>
    void UpdateRewardText()
    {
        if (rewardText != null)
        {
            string newText = GetRewardText();
            if (rewardText.text != newText)
            {
                rewardText.text = newText;
                Debug.Log($"Updated reward text for {gameObject.name}: {newText}");
            }
        }
    }
    
    /// <summary>
    /// Показывает отображение награды
    /// </summary>
    void ShowRewardDisplay()
    {
        if (rewardDisplayObject != null)
        {
            rewardDisplayObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Скрывает отображение награды
    /// </summary>
    void HideRewardDisplay()
    {
        if (rewardDisplayObject != null)
        {
            rewardDisplayObject.SetActive(false);
            // Сбрасываем размер к минимальному при скрытии
            rewardDisplayObject.transform.localScale = Vector3.one * minScale;
            currentScale = minScale;
            scaleVelocity = 0f;
        }
        isPlayerInRange = false;
    }
    
    /// <summary>
    /// Получает текст награды
    /// </summary>
    string GetRewardText()
    {
        if (objectData == null) 
        {
            Debug.LogWarning($"objectData is null for {gameObject.name}");
            return "0";
        }
        
        int coins = objectData.CoinAmount;
        Debug.Log($"Getting reward text for {gameObject.name}: {coins} coins");
        return $"{coins}";
    }
    
    void UpdateOutlineMaterial()
    {
        if (objectRenderer == null || originalMaterials == null) return;
        
        Material[] materials = new Material[originalMaterials.Length];
        originalMaterials.CopyTo(materials, 0);
        
        if (isPlayerLookingAt && outlineMaterial != null)
        {
            if (materials.Length > 1)
            {
                materials[1] = outlineMaterial;
            }
            else
            {
                materials = new Material[] { materials[0], outlineMaterial };
            }
        }
        else if (normalMaterial != null)
        {
            if (materials.Length > 1)
            {
                materials[1] = normalMaterial;
            }
            else
            {
                materials = new Material[] { materials[0], normalMaterial };
            }
        }
        else
        {
            materials = originalMaterials;
        }
        
        objectRenderer.materials = materials;
    }
    
    void OnDrawGizmosSelected()
    {
        // Отображаем центр масс
        Gizmos.color = Color.red;
        Vector3 worldCenterOfMass = transform.TransformPoint(centerOfMass);
        Gizmos.DrawWireSphere(worldCenterOfMass, 0.1f);
        Gizmos.DrawLine(transform.position, worldCenterOfMass);
        
        // Отображаем радиус отображения награды
        if (showRewardDisplay)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, displayDistance);
        }
        
        // Отображаем информацию об объекте
        #if UNITY_EDITOR
        string info = $"Вес: {objectWeight}kg\nЗахвачен: {isGrabbed}";
        if (objectData != null)
        {
            info += $"\nПрочность: {currentHits}/{objectData.HitsToDestroy}\nМонеты: {objectData.CoinAmount}";
        }
        if (showRewardDisplay)
        {
            info += $"\nРадиус награды: {displayDistance}m\nРазмер: {minScale}-{maxScale}";
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, info);
        #endif
    }
}

