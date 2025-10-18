using UnityEngine;

/// <summary>
/// ScriptableObject содержащий параметры для разрушаемого объекта
/// </summary>
[CreateAssetMenu(fileName = "New Destructible Object", menuName = "Game/Destructible Object Data")]
public class DestructibleObjectData : ScriptableObject
{
    [Header("Настройки разрушения")]
    [Tooltip("Количество ударов необходимое для разрушения объекта")]
    [SerializeField] private int hitsToDestroy = 3;
    
    [Tooltip("Минимальная сила удара для засчитывания (0-100)")]
    [SerializeField] private float minimumImpactForce = 10f;
    
    [Tooltip("Использовать систему MeshSlicer для реалистичного разрушения")]
    [SerializeField] private bool useRealisticDestruction = true;
    
    [Header("Параметры MeshSlicer разрушения")]
    [Tooltip("Базовое количество разрезов меша (чем больше - тем больше осколков)\nРекомендуется: 3-10 для оптимального разрушения всей модели")]
    [Range(1, 20)]
    [SerializeField] private int shatterAmount = 5;
    
    [Tooltip("Использовать силу удара для увеличения количества разрезов")]
    [SerializeField] private bool useImpactForceMultiplier = false;
    
    [Tooltip("Множитель силы удара (влияет на количество осколков при сильном ударе)")]
    [Range(0.01f, 2f)]
    [SerializeField] private float impactForceMultiplier = 0.1f;
    
    [Tooltip("Сила разлета осколков при разрушении")]
    [Range(0f, 20f)]
    [SerializeField] private float fragmentExplosionForce = 5f;
    
    [Header("Награды")]
    [Tooltip("Фиксированное количество монет при разрушении")]
    [SerializeField] private int coinAmount = 3;
    
    [Header("Визуальная обратная связь")]
    [Tooltip("Визуальный эффект при ударе")]
    [SerializeField] private GameObject hitEffectPrefab;
    
    [Tooltip("Визуальный эффект при разрушении")]
    [SerializeField] private GameObject destroyEffectPrefab;
    
    [Header("Звуки")]
    [Tooltip("Звук при ударе")]
    [SerializeField] private AudioClip hitSound;
    
    [Tooltip("Звук при разрушении")]
    [SerializeField] private AudioClip destroySound;
    
    // Публичные свойства для доступа
    public int HitsToDestroy => hitsToDestroy;
    public float MinimumImpactForce => minimumImpactForce;
    public bool UseRealisticDestruction => useRealisticDestruction;
    public int ShatterAmount => shatterAmount;
    public bool UseImpactForceMultiplier => useImpactForceMultiplier;
    public float ImpactForceMultiplier => impactForceMultiplier;
    public float FragmentExplosionForce => fragmentExplosionForce;
    public int CoinAmount => coinAmount;
    public GameObject HitEffectPrefab => hitEffectPrefab;
    public GameObject DestroyEffectPrefab => destroyEffectPrefab;
    public AudioClip HitSound => hitSound;
    public AudioClip DestroySound => destroySound;
    
    /// <summary>
    /// Получить фиксированное количество монет
    /// </summary>
    public int GetCoinAmount()
    {
        return coinAmount;
    }
    
    /// <summary>
    /// Вычислить количество разрезов с учетом силы удара
    /// </summary>
    public int CalculateShatterAmount(float impactForce)
    {
        int finalAmount = shatterAmount;
        
        if (useImpactForceMultiplier)
        {
            // Базовое количество + множитель от силы удара
            finalAmount = shatterAmount + Mathf.RoundToInt(impactForce * impactForceMultiplier);
            
            // Ограничиваем максимум чтобы не создать слишком много осколков
            finalAmount = Mathf.Clamp(finalAmount, shatterAmount, 30);
        }
        
        // ВАЖНО: Минимум 3 разреза для корректного разрушения всей модели
        // При 1-2 разрезах может разрушиться только часть модели
        if (finalAmount < 3)
        {
            Debug.LogWarning($"ShatterAmount слишком мало ({finalAmount}). Рекомендуется минимум 3 для полного разрушения модели.");
        }
        
        return Mathf.Max(finalAmount, 1); // Минимум 1 разрез
    }
}

