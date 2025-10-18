using UnityEngine;

/// <summary>
/// Компонент для осколков разрушенного объекта
/// Управляет затуханием и поведением осколка
/// </summary>
public class SimpleFragment : MonoBehaviour
{
    private Rigidbody rb;
    private Renderer fragmentRenderer;
    private Material fragmentMaterial;
    
    [Header("Настройки затухания")]
    [Tooltip("Начать затухание за N секунд до уничтожения")]
    [SerializeField] private float fadeStartBeforeDestroy = 0.5f;
    
    [Tooltip("Включить постепенное затухание (fade out)")]
    [SerializeField] private bool enableFadeOut = true;
    
    private float creationTime;
    private float lifetime = 3f;
    private bool isFading = false;
    private Color originalColor;
    private bool hasAlpha = false;

    /// <summary>
    /// Инициализация осколка
    /// </summary>
    public void Initialize(Rigidbody rigidbody, float fragmentLifetime = 3f)
    {
        rb = rigidbody;
        lifetime = fragmentLifetime;
        creationTime = Time.time;
        
        fragmentRenderer = GetComponent<Renderer>();
        if (fragmentRenderer != null && enableFadeOut)
        {
            // Создаем копию материала для независимого изменения
            fragmentMaterial = fragmentRenderer.material;
            originalColor = fragmentMaterial.color;
            
            // Проверяем поддержку прозрачности
            if (fragmentMaterial.HasProperty("_Color"))
            {
                hasAlpha = true;
                
                // Если материал не поддерживает прозрачность, меняем режим
                if (fragmentMaterial.renderQueue < 3000)
                {
                    fragmentMaterial.SetFloat("_Mode", 3); // Transparent mode
                    fragmentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fragmentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fragmentMaterial.SetInt("_ZWrite", 0);
                    fragmentMaterial.DisableKeyword("_ALPHATEST_ON");
                    fragmentMaterial.EnableKeyword("_ALPHABLEND_ON");
                    fragmentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fragmentMaterial.renderQueue = 3000;
                }
            }
        }
    }

    void Update()
    {
        if (!enableFadeOut || !hasAlpha) return;
        
        float timeSinceCreation = Time.time - creationTime;
        float timeUntilDestroy = lifetime - timeSinceCreation;
        
        // Начинаем затухание
        if (timeUntilDestroy <= fadeStartBeforeDestroy && !isFading)
        {
            isFading = true;
        }
        
        // Постепенное затухание
        if (isFading && fragmentMaterial != null)
        {
            float fadeProgress = timeUntilDestroy / fadeStartBeforeDestroy;
            fadeProgress = Mathf.Clamp01(fadeProgress);
            
            Color newColor = originalColor;
            newColor.a = fadeProgress;
            fragmentMaterial.color = newColor;
        }
    }

    /// <summary>
    /// Применить дополнительную силу к осколку
    /// </summary>
    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (rb != null)
        {
            rb.AddForce(force, mode);
        }
    }

    /// <summary>
    /// Применить вращение к осколку
    /// </summary>
    public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Impulse)
    {
        if (rb != null)
        {
            rb.AddTorque(torque, mode);
        }
    }

    /// <summary>
    /// Получить скорость осколка
    /// </summary>
    public Vector3 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }

    /// <summary>
    /// Установить скорость осколка
    /// </summary>
    public void SetVelocity(Vector3 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    void OnDestroy()
    {
        // Очищаем материал при уничтожении
        if (fragmentMaterial != null)
        {
            Destroy(fragmentMaterial);
        }
    }

    [Header("Эффекты столкновения")]
    [Tooltip("Минимальная сила столкновения для эффектов")]
    [SerializeField] private float minImpactForceForEffects = 2f;
    
    [Tooltip("Звук столкновения")]
    [SerializeField] private AudioClip impactSound;
    
    [Tooltip("Эффект частиц при столкновении")]
    [SerializeField] private GameObject impactEffectPrefab;
    
    private AudioSource audioSource;
    private int collisionCount = 0;
    private const int maxCollisionEffects = 3;

    void OnCollisionEnter(Collision collision)
    {
        if (collisionCount >= maxCollisionEffects) return;
        
        float impactForce = collision.relativeVelocity.magnitude;
        
        if (impactForce >= minImpactForceForEffects)
        {
            collisionCount++;
            
            if (impactSound != null)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f;
                    audioSource.playOnAwake = false;
                }
                
                audioSource.volume = Mathf.Clamp01(impactForce / 10f);
                audioSource.PlayOneShot(impactSound);
            }
            
            if (impactEffectPrefab != null && collision.contacts.Length > 0)
            {
                GameObject effect = Instantiate(
                    impactEffectPrefab, 
                    collision.contacts[0].point, 
                    Quaternion.LookRotation(collision.contacts[0].normal)
                );
                Destroy(effect, 2f);
            }
        }
    }
    
    /// <summary>
    /// Установить звук столкновения
    /// </summary>
    public void SetImpactSound(AudioClip clip, float minForce = 2f)
    {
        impactSound = clip;
        minImpactForceForEffects = minForce;
    }
    
    /// <summary>
    /// Установить эффект столкновения
    /// </summary>
    public void SetImpactEffect(GameObject effectPrefab)
    {
        impactEffectPrefab = effectPrefab;
    }
}

