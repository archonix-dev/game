using UnityEngine;

/// <summary>
/// Менеджер для управления эффектом наведения на 3D объекты через Raycast
/// </summary>
public class ObjectHoverManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Камера для Raycast (если не указана, используется Camera.main)")]
    public Camera raycastCamera;
    
    [Tooltip("Максимальная дистанция Raycast")]
    public float maxRaycastDistance = 100f;
    
    [Tooltip("Слой для Raycast (оставьте пустым для всех слоев)")]
    public LayerMask raycastLayer = -1;
    
    private ObjectHoverEffect currentHoveredObject;
    private Camera cam;
    
    /// <summary>
    /// Получить текущий объект, на который наведена мышь
    /// </summary>
    public ObjectHoverEffect GetCurrentHoveredObject()
    {
        return currentHoveredObject;
    }
    
    void Start()
    {
        // Получаем камеру
        if (raycastCamera != null)
        {
            cam = raycastCamera;
        }
        else
        {
            cam = Camera.main;
        }
        
        if (cam == null)
        {
            Debug.LogError("ObjectHoverManager: Камера не найдена!");
            enabled = false;
        }
    }
    
    void Update()
    {
        if (cam == null) return;
        
        // Создаем луч из камеры через позицию мыши
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Проверяем, попал ли луч в какой-либо объект
        if (Physics.Raycast(ray, out hit, maxRaycastDistance, raycastLayer))
        {
            // Проверяем, есть ли у объекта компонент ObjectHoverEffect
            ObjectHoverEffect hoverEffect = hit.collider.GetComponent<ObjectHoverEffect>();
            
            if (hoverEffect != null)
            {
                // Если это новый объект, снимаем наведение со старого
                if (currentHoveredObject != null && currentHoveredObject != hoverEffect)
                {
                    currentHoveredObject.SetHovered(false);
                }
                
                // Устанавливаем наведение на новый объект
                if (currentHoveredObject != hoverEffect)
                {
                    currentHoveredObject = hoverEffect;
                    hoverEffect.SetHovered(true);
                }
            }
            else
            {
                // Если луч попал в объект без ObjectHoverEffect, снимаем наведение
                if (currentHoveredObject != null)
                {
                    currentHoveredObject.SetHovered(false);
                    currentHoveredObject = null;
                }
            }
        }
        else
        {
            // Если луч ни во что не попал, снимаем наведение
            if (currentHoveredObject != null)
            {
                currentHoveredObject.SetHovered(false);
                currentHoveredObject = null;
            }
        }
    }
}

