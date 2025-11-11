using UnityEngine;

public class ObjectHoverEffect : MonoBehaviour
{
    [Header("Textures")]
    [Tooltip("Текстура для обычного состояния (когда мышь не наведена)")]
    public Texture2D normalTexture;
    
    [Tooltip("Текстура для состояния наведения (когда мышь наведена)")]
    public Texture2D hoverTexture;
    
    [Header("Settings")]
    [Tooltip("Индекс материала (если объект имеет несколько материалов)")]
    public int materialIndex = 0;
    
    [Tooltip("Слой для Raycast (оставьте пустым для всех слоев)")]
    public LayerMask raycastLayer = -1;
    
    private Material objectMaterial;
    private bool isHovered = false;
    private Renderer objectRenderer;
    private Collider objectCollider;
    
    void Start()
    {
        // Получаем Renderer объекта
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogWarning($"ObjectHoverEffect: Renderer не найден на объекте {gameObject.name}. Добавьте компонент Renderer (MeshRenderer, SkinnedMeshRenderer и т.д.)");
            return;
        }
        
        // Получаем Collider для Raycast
        objectCollider = GetComponent<Collider>();
        if (objectCollider == null)
        {
            Debug.LogWarning($"ObjectHoverEffect: Collider не найден на объекте {gameObject.name}. Добавьте Collider для работы эффекта наведения.");
        }
        
        // Получаем материал
        if (objectRenderer.materials != null && objectRenderer.materials.Length > 0)
        {
            if (materialIndex < objectRenderer.materials.Length)
            {
                objectMaterial = objectRenderer.materials[materialIndex];
            }
            else
            {
                objectMaterial = objectRenderer.material;
            }
            
            // Устанавливаем начальную текстуру
            if (normalTexture != null && objectMaterial != null)
            {
                SetTexture(normalTexture);
            }
        }
    }
    
    /// <summary>
    /// Вызывается из менеджера для проверки наведения через Raycast
    /// </summary>
    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered) return;
        
        isHovered = hovered;
        
        if (hovered && hoverTexture != null && objectMaterial != null)
        {
            SetTexture(hoverTexture);
        }
        else if (!hovered && normalTexture != null && objectMaterial != null)
        {
            SetTexture(normalTexture);
        }
    }
    
    /// <summary>
    /// Устанавливает текстуру в материал (поддерживает разные шейдеры)
    /// </summary>
    private void SetTexture(Texture2D texture)
    {
        if (objectMaterial == null || texture == null) return;
        
        // Пробуем разные имена свойств в зависимости от шейдера
        if (objectMaterial.HasProperty("_BaseMap"))
        {
            // URP/Lit шейдер
            objectMaterial.SetTexture("_BaseMap", texture);
        }
        else if (objectMaterial.HasProperty("_MainTex"))
        {
            // Стандартный шейдер
            objectMaterial.SetTexture("_MainTex", texture);
        }
        else if (objectMaterial.HasProperty("_BaseColorMap"))
        {
            // HDRP шейдер
            objectMaterial.SetTexture("_BaseColorMap", texture);
        }
        else
        {
            Debug.LogWarning($"ObjectHoverEffect: Не удалось найти свойство текстуры в материале объекта {gameObject.name}. Убедитесь, что используется поддерживаемый шейдер.");
        }
    }
    
    void OnDestroy()
    {
        // Восстанавливаем исходную текстуру при уничтожении
        if (normalTexture != null && objectMaterial != null)
        {
            SetTexture(normalTexture);
        }
    }
}
