using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Структура для хранения данных о скрытии/показе объектов
/// </summary>
[System.Serializable]
public class VisibilityAction
{
    [Tooltip("Идентификатор действия (строка для вызова метода)")]
    public string actionName;
    
    [Tooltip("Объекты, которые будут скрыты при вызове этого действия")]
    public GameObject[] objectsToHide;
    
    [Tooltip("Объекты, которые будут показаны при вызове этого действия")]
    public GameObject[] objectsToShow;
}

/// <summary>
/// Скрипт для управления видимостью объектов через массив действий
/// </summary>
public class ObjectVisibilityController : MonoBehaviour
{
    [Header("Массив действий")]
    [Tooltip("Массив действий для скрытия/показа объектов")]
    public VisibilityAction[] visibilityActions;
    
    /// <summary>
    /// Выполняет действие по имени (скрывает и показывает соответствующие объекты)
    /// </summary>
    /// <param name="actionName">Имя действия из массива visibilityActions</param>
    public void ExecuteAction(string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
        {
            Debug.LogWarning("Имя действия не указано!");
            return;
        }
        
        // Ищем действие с указанным именем
        VisibilityAction action = null;
        foreach (var act in visibilityActions)
        {
            if (act != null && act.actionName == actionName)
            {
                action = act;
                break;
            }
        }
        
        if (action == null)
        {
            Debug.LogWarning($"Действие с именем '{actionName}' не найдено в массиве!");
            return;
        }
        
        // Скрываем указанные объекты
        if (action.objectsToHide != null)
        {
            foreach (var obj in action.objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        // Показываем указанные объекты
        if (action.objectsToShow != null)
        {
            foreach (var obj in action.objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
    }
    
    /// <summary>
    /// Публичный метод для вызова из кнопки через Inspector (для UnityEvent)
    /// </summary>
    /// <param name="actionName">Имя действия из массива visibilityActions</param>
    public void OnButtonClick(string actionName)
    {
        ExecuteAction(actionName);
    }
}

