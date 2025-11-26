using UnityEngine;

public class ObjectHoverEffect : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("Материал, на котором будет управляться эмиссия (опционально)")]
    public Material sourceMaterial;
    
    [ColorUsage(true, true)]
    [Tooltip("Цвет эмиссии, который будет применен при наведении")]
    public Color hoverEmissionColor = Color.white;
    
    [Header("Settings")]
    [Tooltip("Индекс материала (если объект имеет несколько материалов)")]
    public int materialIndex = 0;
    
    [Tooltip("Слой для Raycast (оставьте пустым для всех слоев)")]
    public LayerMask raycastLayer = -1;
    
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    
    private Material runtimeMaterial;
    private Color initialEmissionColor = Color.black;
    private bool emissionSupported = false;
    
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
        
        InitializeMaterial();
    }
    
    /// <summary>
    /// Вызывается из менеджера для проверки наведения через Raycast
    /// </summary>
    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered) return;
        
        isHovered = hovered;
        UpdateEmission();
    }
    
    private void InitializeMaterial()
    {
        if (objectRenderer == null) return;
        
        // Создаем копию материала, чтобы не изменять оригинальный ассет
        if (sourceMaterial != null)
        {
            runtimeMaterial = Instantiate(sourceMaterial);
            
            if (objectRenderer.materials != null && objectRenderer.materials.Length > 0)
            {
                var materials = objectRenderer.materials;
                var index = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
                materials[index] = runtimeMaterial;
                objectRenderer.materials = materials;
            }
            else
            {
                objectRenderer.material = runtimeMaterial;
            }
        }
        else if (objectRenderer.materials != null && objectRenderer.materials.Length > 0)
        {
            var materials = objectRenderer.materials;
            var index = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
            runtimeMaterial = materials[index];
        }
        
        if (runtimeMaterial == null)
        {
            Debug.LogWarning($"ObjectHoverEffect: Материал не найден на объекте {gameObject.name}. Задайте материал в инспекторе или убедитесь, что Renderer содержит материалы.");
            return;
        }
        
        if (!runtimeMaterial.HasProperty(EmissionColorProperty))
        {
            Debug.LogWarning($"ObjectHoverEffect: Материал {runtimeMaterial.name} не поддерживает эмиссию. Добавьте свойство _EmissionColor.");
            return;
        }
        
        emissionSupported = true;
        initialEmissionColor = runtimeMaterial.GetColor(EmissionColorProperty);
        DisableEmission();
    }
    
    private void UpdateEmission()
    {
        if (!emissionSupported || runtimeMaterial == null) return;
        
        if (isHovered)
        {
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor(EmissionColorProperty, hoverEmissionColor);
        }
        else
        {
            DisableEmission();
        }
    }
    
    private void DisableEmission()
    {
        if (!emissionSupported || runtimeMaterial == null) return;
        
        runtimeMaterial.SetColor(EmissionColorProperty, initialEmissionColor);
        runtimeMaterial.DisableKeyword("_EMISSION");
    }
    
    void OnDestroy()
    {
        DisableEmission();
    }
}
