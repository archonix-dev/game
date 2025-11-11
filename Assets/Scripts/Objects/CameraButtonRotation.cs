using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Структура для хранения пары кнопка-точка поворота
/// </summary>
[System.Serializable]
public class ButtonTargetPair
{
    [Tooltip("Кнопка, при нажатии на которую камера повернется")]
    public Button button;
    
    [Tooltip("Точка, к которой камера будет поворачиваться")]
    public Transform targetPoint;
}

/// <summary>
/// Скрипт для плавного поворота камеры к указанным точкам при нажатии на кнопки
/// </summary>
public class CameraButtonRotation : MonoBehaviour
{
    [Header("Настройки камеры")]
    [Tooltip("Камера, которая будет поворачиваться (если не указана, используется Camera.main)")]
    public Camera targetCamera;
    
    [Header("Массив кнопок и точек")]
    [Tooltip("Массив пар кнопка-точка для поворота камеры")]
    public ButtonTargetPair[] buttonTargets;
    
    [Header("Настройки анимации")]
    [Tooltip("Скорость поворота камеры (чем больше, тем быстрее)")]
    public float rotationSpeed = 2f;
    
    [Tooltip("Включить ли временно MouseLook во время поворота (чтобы игрок не мог управлять камерой)")]
    public bool disableMouseLookDuringRotation = true;
    
    private bool isRotating = false;
    private MouseLook mouseLook;
    private Coroutine rotationCoroutine;
    
    void Start()
    {
        // Если камера не указана, используем Camera.main
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        
        // Пытаемся найти MouseLook на камере или на родительском объекте
        if (disableMouseLookDuringRotation)
        {
            if (targetCamera != null)
            {
                mouseLook = targetCamera.GetComponent<MouseLook>();
                if (mouseLook == null && targetCamera.transform.parent != null)
                {
                    mouseLook = targetCamera.transform.parent.GetComponent<MouseLook>();
                }
            }
        }
        
        // Настраиваем обработчики нажатий для всех кнопок
        SetupButtonListeners();
        
        // Поворачиваем камеру к первой точке в массиве при старте
        RotateToFirstTarget();
    }
    
    /// <summary>
    /// Поворачивает камеру к первой точке в массиве
    /// </summary>
    private void RotateToFirstTarget()
    {
        // Проверяем, что массив не пустой
        if (buttonTargets == null || buttonTargets.Length == 0)
        {
            return;
        }
        
        // Проверяем, что первая точка назначена
        if (buttonTargets[0].targetPoint == null)
        {
            Debug.LogWarning("Первая точка поворота в массиве не назначена");
            return;
        }
        
        if (targetCamera == null)
        {
            return;
        }
        
        // Запускаем поворот к первой точке
        rotationCoroutine = StartCoroutine(RotateCameraToTarget(buttonTargets[0].targetPoint));
    }
    
    /// <summary>
    /// Настраивает обработчики нажатий для всех кнопок в массиве
    /// </summary>
    private void SetupButtonListeners()
    {
        for (int i = 0; i < buttonTargets.Length; i++)
        {
            int index = i; // Замыкание для лямбда-выражения
            
            if (buttonTargets[i].button != null)
            {
                buttonTargets[i].button.onClick.AddListener(() => OnButtonClicked(index));
            }
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку
    /// </summary>
    private void OnButtonClicked(int index)
    {
        // Проверяем валидность индекса
        if (index < 0 || index >= buttonTargets.Length)
        {
            Debug.LogWarning($"Неверный индекс кнопки: {index}");
            return;
        }
        
        // Проверяем, что кнопка и точка назначены
        if (buttonTargets[index].button == null)
        {
            Debug.LogWarning($"Кнопка с индексом {index} не назначена");
            return;
        }
        
        if (buttonTargets[index].targetPoint == null)
        {
            Debug.LogWarning($"Точка поворота с индексом {index} не назначена");
            return;
        }
        
        if (targetCamera == null)
        {
            Debug.LogWarning("Камера не назначена");
            return;
        }
        
        // Если уже идет поворот, останавливаем его
        if (rotationCoroutine != null)
        {
            StopCoroutine(rotationCoroutine);
        }
        
        // Запускаем поворот к указанной точке
        rotationCoroutine = StartCoroutine(RotateCameraToTarget(buttonTargets[index].targetPoint));
    }
    
    /// <summary>
    /// Плавно поворачивает камеру к указанной точке
    /// </summary>
    private IEnumerator RotateCameraToTarget(Transform targetPoint)
    {
        isRotating = true;
        
        // Отключаем MouseLook, если нужно
        bool mouseLookWasEnabled = false;
        if (mouseLook != null && disableMouseLookDuringRotation)
        {
            mouseLookWasEnabled = mouseLook.enabled;
            mouseLook.enabled = false;
        }
        
        // Вычисляем направление от камеры к целевой точке
        Vector3 directionToTarget = (targetPoint.position - targetCamera.transform.position).normalized;
        
        // Вычисляем целевой поворот
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        
        // Получаем начальный поворот камеры
        Quaternion startRotation = targetCamera.transform.rotation;
        
        // Плавно поворачиваем камеру
        float elapsedTime = 0f;
        float journeyLength = Quaternion.Angle(startRotation, targetRotation);
        float speed = rotationSpeed * (journeyLength / 90f); // Нормализуем скорость относительно угла поворота
        
        while (Quaternion.Angle(targetCamera.transform.rotation, targetRotation) > 0.1f)
        {
            elapsedTime += Time.deltaTime * speed;
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime);
            yield return null;
        }
        
        // Убеждаемся, что достигли точного поворота
        targetCamera.transform.rotation = targetRotation;
        
        // Включаем MouseLook обратно, если он был включен
        if (mouseLook != null && disableMouseLookDuringRotation && mouseLookWasEnabled)
        {
            mouseLook.enabled = true;
        }
        
        isRotating = false;
    }
    
    /// <summary>
    /// Проверяет, идет ли сейчас поворот камеры
    /// </summary>
    public bool IsRotating()
    {
        return isRotating;
    }
    
    void OnDestroy()
    {
        // Отписываемся от всех событий кнопок
        foreach (var pair in buttonTargets)
        {
            if (pair.button != null)
            {
                pair.button.onClick.RemoveAllListeners();
            }
        }
    }
}

