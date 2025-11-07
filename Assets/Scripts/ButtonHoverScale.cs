using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Скрипт для плавного изменения Scale Image при наведении мыши
/// </summary>
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Настройки")]
    [Tooltip("Image компонент, который будет масштабироваться")]
    public Image targetImage;
    
    [Tooltip("Скорость анимации (чем больше, тем быстрее)")]
    public float animationSpeed = 5f;
    
    [Header("Настройки клика")]
    [Tooltip("GameObject, который появляется при нажатии на эту кнопку")]
    public GameObject targetGameObject;
    
    // Начальный и конечный Scale
    private Vector3 normalScale = new Vector3(1f, 0.01f, 1f);
    public Vector3 hoverScale = new Vector3(4.31f, 0.01f, 1f);
    
    private Coroutine scaleCoroutine;
    private Button button;
    
    // Статический список всех экземпляров для управления состоянием
    private static List<ButtonHoverScale> allInstances = new List<ButtonHoverScale>();
    
    void Start()
    {
        // Если Image не назначен, пытаемся найти его на этом же объекте
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
        
        // Получаем компонент Button
        button = GetComponent<Button>();
        
        // Устанавливаем начальный Scale
        if (targetImage != null)
        {
            targetImage.rectTransform.localScale = normalScale;
        }
        
        // Скрываем GameObject при старте, если он назначен
        if (targetGameObject != null)
        {
            targetGameObject.SetActive(false);
        }
        
        // Добавляем этот экземпляр в статический список
        if (!allInstances.Contains(this))
        {
            allInstances.Add(this);
        }
    }
    
    /// <summary>
    /// Вызывается при наведении мыши на кнопку
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage == null) return;
        
        // Останавливаем предыдущую корутину, если она запущена
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        
        // Запускаем анимацию увеличения
        scaleCoroutine = StartCoroutine(ScaleTo(hoverScale));
    }
    
    /// <summary>
    /// Вызывается при убирании мыши с кнопки
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetImage == null) return;
        
        // Останавливаем предыдущую корутину, если она запущена
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        
        // Запускаем анимацию уменьшения
        scaleCoroutine = StartCoroutine(ScaleTo(normalScale));
    }
    
    /// <summary>
    /// Плавно изменяет Scale к целевому значению
    /// </summary>
    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = targetImage.rectTransform.localScale;
        
        while (Vector3.Distance(targetImage.rectTransform.localScale, targetScale) > 0.01f)
        {
            targetImage.rectTransform.localScale = Vector3.Lerp(
                targetImage.rectTransform.localScale,
                targetScale,
                animationSpeed * Time.deltaTime
            );
            
            yield return null;
        }
        
        // Убеждаемся, что достигли точного значения
        targetImage.rectTransform.localScale = targetScale;
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // Проверяем, что кнопка не заблокирована
        if (button != null && !button.interactable)
        {
            return;
        }
        
        // Скрываем GameObject и показываем targetImage у всех других кнопок
        foreach (var instance in allInstances)
        {
            if (instance != this && instance != null)
            {
                // Скрываем GameObject у других кнопок
                if (instance.targetGameObject != null)
                {
                    instance.targetGameObject.SetActive(false);
                }
                
                // Показываем targetImage у других кнопок
                if (instance.targetImage != null)
                {
                    instance.targetImage.gameObject.SetActive(true);
                }
            }
        }
        
        // Показываем GameObject и скрываем targetImage у этой кнопки
        if (targetGameObject != null)
        {
            targetGameObject.SetActive(true);
        }
        
        if (targetImage != null)
        {
            targetImage.gameObject.SetActive(false);
        }
    }
    
    void OnDestroy()
    {
        // Удаляем этот экземпляр из статического списка
        if (allInstances.Contains(this))
        {
            allInstances.Remove(this);
        }
    }
}

