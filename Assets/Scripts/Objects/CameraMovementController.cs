using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Структура для хранения пары объект-точка подлета
/// </summary>
[System.Serializable]
public class ObjectTargetPair
{
    [Tooltip("Объект с ObjectHoverEffect")]
    public GameObject hoverObject;
    
    [Tooltip("Точка Transform для подлета камеры (позиция и поворот)")]
    public Transform targetPoint;
    
    [Tooltip("3D Canvas (World Space), который открывается при клике на объект")]
    public Canvas objectCanvas;
    
    [Tooltip("Смещение Canvas относительно объекта (если Canvas не является дочерним объектом)")]
    public Vector3 canvasOffset = Vector3.zero;
    
    [Tooltip("Объект, который скрывается при клике ЛКМ и показывается при нажатии ESC")]
    public GameObject objectToHide;
}

/// <summary>
/// Контроллер для плавного движения камеры к объектам
/// </summary>
public class CameraMovementController : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Камера для управления (если не указана, используется Camera.main)")]
    public Camera targetCamera;
    
    [Tooltip("Начальная позиция камеры (также используется для возврата по ESC)")]
    public Transform initialCameraPosition;
    
    [Header("Object Targets")]
    [Tooltip("Массив пар: объект с ObjectHoverEffect -> точка подлета")]
    public ObjectTargetPair[] objectTargets;
    
    [Header("Movement Settings")]
    [Tooltip("Скорость движения камеры к цели")]
    public float movementSpeed = 2f;
    
    [Tooltip("Скорость поворота камеры к цели")]
    public float rotationSpeed = 2f;
    
    [Header("Network Settings")]
    [Tooltip("Ссылка на LobbyManager для создания/закрытия лобби")]
    public LobbyManager lobbyManager;
    
    [Tooltip("Индекс элемента массива для создания лобби (по умолчанию 1 - второй элемент)")]
    public int lobbyCreationIndex = 1;
    
    [Header("UI Buttons")]
    [Tooltip("Массив кнопок, при нажатии на которые выполняется логика ESC (возврат камеры, закрытие лобби и т.д.)")]
    public Button[] escapeButtons;
    
    private bool isMoving = false;
    private CameraMouseFollow mouseFollowScript;
    private ObjectHoverManager hoverManager;
    private GameObject currentActiveObject = null; // Текущий объект с отключенным коллайдером
    private Canvas currentActiveCanvas = null; // Текущий активный Canvas
    private GameObject currentHiddenObject = null; // Текущий скрытый объект
    
    void Start()
    {
        // Получаем камеру
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        
        if (targetCamera == null)
        {
            Debug.LogError("CameraMovementController: Камера не найдена!");
            enabled = false;
            return;
        }
        
        // Получаем компоненты
        mouseFollowScript = targetCamera.GetComponent<CameraMouseFollow>();
        hoverManager = FindObjectOfType<ObjectHoverManager>();
        
        // Устанавливаем начальную позицию камеры, если указана
        if (initialCameraPosition != null)
        {
            targetCamera.transform.position = initialCameraPosition.position;
            targetCamera.transform.rotation = initialCameraPosition.rotation;
        }
        
        // Деактивируем все Canvas при старте
        if (objectTargets != null)
        {
            foreach (ObjectTargetPair pair in objectTargets)
            {
                if (pair.objectCanvas != null)
                {
                    pair.objectCanvas.gameObject.SetActive(false);
                }
            }
        }
        
        // Настраиваем кнопки ESC
        SetupEscapeButtons();
    }
    
    /// <summary>
    /// Настраивает обработчики для кнопок ESC
    /// </summary>
    private void SetupEscapeButtons()
    {
        if (escapeButtons != null)
        {
            foreach (Button button in escapeButtons)
            {
                if (button != null)
                {
                    button.onClick.AddListener(OnEscapeButtonClicked);
                }
            }
        }
    }
    
    void Update()
    {
        // Обработка ESC - возврат к начальной позиции
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PerformEscapeAction();
        }
        
        // Обработка ЛКМ - подлет к объекту
        if (Input.GetMouseButtonDown(0) && !isMoving && hoverManager != null)
        {
            ObjectHoverEffect hoveredObject = hoverManager.GetCurrentHoveredObject();
            if (hoveredObject != null)
            {
                // Ищем соответствующую точку для этого объекта
                ObjectTargetPair pair = FindPairForObject(hoveredObject.gameObject);
                if (pair != null && pair.targetPoint != null)
                {
                    // Проверяем, является ли это вторым элементом массива (для создания лобби)
                    int pairIndex = GetPairIndex(pair);
                    if (pairIndex == lobbyCreationIndex && lobbyManager != null)
                    {
                        // Создаем лобби
                        lobbyManager.CreateLobby();
                    }
                    
                    // Отключаем коллайдер у объекта
                    DisableColliderForObject(hoveredObject.gameObject);
                    // Скрываем объект, если указан
                    HideObjectForPair(pair);
                    // Открываем Canvas для объекта
                    OpenCanvasForObject(pair);
                    StartCoroutine(MoveCameraToTarget(pair.targetPoint));
                }
            }
        }
    }
    
    /// <summary>
    /// Находит пару для указанного объекта
    /// </summary>
    private ObjectTargetPair FindPairForObject(GameObject obj)
    {
        if (objectTargets == null) return null;
        
        foreach (ObjectTargetPair pair in objectTargets)
        {
            if (pair.hoverObject != null && pair.hoverObject == obj)
            {
                return pair;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Получает индекс пары в массиве
    /// </summary>
    private int GetPairIndex(ObjectTargetPair targetPair)
    {
        if (objectTargets == null || targetPair == null) return -1;
        
        for (int i = 0; i < objectTargets.Length; i++)
        {
            if (objectTargets[i] == targetPair)
            {
                return i;
            }
        }
        
        return -1;
    }
    
    /// <summary>
    /// Плавно перемещает камеру к указанной точке
    /// </summary>
    private IEnumerator MoveCameraToTarget(Transform target)
    {
        if (target == null || targetCamera == null) yield break;
        
        isMoving = true;
        
        // Отключаем следование за мышью во время движения
        if (mouseFollowScript != null)
        {
            mouseFollowScript.enabled = false;
        }
        
        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        
        Vector3 targetPosition = target.position;
        Quaternion targetRotation = target.rotation;
        
        float elapsedTime = 0f;
        float journeyLength = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = journeyLength / movementSpeed;
        
        while (elapsedTime < journeyTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / journeyTime);
            
            // Плавная интерполяция позиции
            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            
            // Плавная интерполяция поворота
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            
            yield return null;
        }
        
        // Убеждаемся, что достигли точной позиции и поворота
        targetCamera.transform.position = targetPosition;
        targetCamera.transform.rotation = targetRotation;
        
        // Включаем следование за мышью обратно
        if (mouseFollowScript != null)
        {
            mouseFollowScript.enabled = true;
            // Обновляем начальный поворот для следования за мышью
            mouseFollowScript.ResetRotation();
        }
        
        isMoving = false;
    }
    
    /// <summary>
    /// Публичный метод для программного перемещения камеры
    /// </summary>
    public void MoveToTarget(Transform target)
    {
        if (!isMoving && target != null)
        {
            StartCoroutine(MoveCameraToTarget(target));
        }
    }
    
    /// <summary>
    /// Публичный метод для возврата к начальной позиции
    /// </summary>
    public void ReturnToInitial()
    {
        if (initialCameraPosition != null && !isMoving)
        {
            // Включаем коллайдер обратно, если был отключен
            EnableColliderForCurrentObject();
            StartCoroutine(MoveCameraToTarget(initialCameraPosition));
        }
    }
    
    /// <summary>
    /// Отключает коллайдер у указанного объекта
    /// </summary>
    private void DisableColliderForObject(GameObject obj)
    {
        if (obj == null) return;
        
        // Включаем коллайдер у предыдущего объекта, если был
        EnableColliderForCurrentObject();
        
        // Отключаем коллайдер у нового объекта
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            currentActiveObject = obj;
        }
    }
    
    /// <summary>
    /// Включает коллайдер у текущего активного объекта
    /// </summary>
    private void EnableColliderForCurrentObject()
    {
        if (currentActiveObject != null)
        {
            Collider col = currentActiveObject.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }
            currentActiveObject = null;
        }
    }
    
    /// <summary>
    /// Открывает Canvas для указанного объекта
    /// </summary>
    private void OpenCanvasForObject(ObjectTargetPair pair)
    {
        if (pair == null || pair.objectCanvas == null) return;
        
        // Закрываем предыдущий Canvas, если был открыт
        CloseCurrentCanvas();
        
        Canvas canvas = pair.objectCanvas;
        
        // Убеждаемся, что Canvas настроен как World Space
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            Debug.LogWarning($"Canvas '{canvas.name}' был переключен в режим World Space");
        }
        
        // Устанавливаем камеру для Canvas, если не установлена
        if (canvas.worldCamera == null && targetCamera != null)
        {
            canvas.worldCamera = targetCamera;
        }
        
        // Позиционируем Canvas относительно объекта
        if (pair.hoverObject != null)
        {
            // Если Canvas не является дочерним объектом, позиционируем его
            if (canvas.transform.parent != pair.hoverObject.transform)
            {
                canvas.transform.position = pair.hoverObject.transform.position + pair.canvasOffset;
                // Поворачиваем Canvas к камере
                if (targetCamera != null)
                {
                    canvas.transform.LookAt(targetCamera.transform);
                    canvas.transform.Rotate(0, 180, 0); // Разворачиваем, чтобы текст был читаемым
                }
            }
        }
        
        // Активируем Canvas
        canvas.gameObject.SetActive(true);
        currentActiveCanvas = canvas;
    }
    
    /// <summary>
    /// Закрывает текущий активный Canvas
    /// </summary>
    private void CloseCurrentCanvas()
    {
        if (currentActiveCanvas != null)
        {
            currentActiveCanvas.gameObject.SetActive(false);
            currentActiveCanvas = null;
        }
    }
    
    /// <summary>
    /// Скрывает объект для указанной пары
    /// </summary>
    private void HideObjectForPair(ObjectTargetPair pair)
    {
        if (pair == null || pair.objectToHide == null) return;
        
        // Показываем предыдущий скрытый объект, если был
        ShowHiddenObject();
        
        // Скрываем новый объект
        pair.objectToHide.SetActive(false);
        currentHiddenObject = pair.objectToHide;
    }
    
    /// <summary>
    /// Показывает скрытый объект
    /// </summary>
    private void ShowHiddenObject()
    {
        if (currentHiddenObject != null)
        {
            currentHiddenObject.SetActive(true);
            currentHiddenObject = null;
        }
    }
    
    /// <summary>
    /// Выполняет действие ESC (возврат камеры, закрытие лобби и т.д.)
    /// </summary>
    private void PerformEscapeAction()
    {
        if (initialCameraPosition != null && !isMoving)
        {
            // Закрываем и удаляем лобби, если оно было создано
            if (lobbyManager != null)
            {
                lobbyManager.CloseAndDestroyLobby();
            }
            
            // Закрываем Canvas
            CloseCurrentCanvas();
            // Показываем скрытый объект
            ShowHiddenObject();
            // Включаем коллайдер обратно, если был отключен
            EnableColliderForCurrentObject();
            StartCoroutine(MoveCameraToTarget(initialCameraPosition));
        }
    }
    
    /// <summary>
    /// Обработчик нажатия на кнопку ESC
    /// </summary>
    private void OnEscapeButtonClicked()
    {
        PerformEscapeAction();
    }
    
    void OnDestroy()
    {
        // Отписываемся от всех кнопок ESC
        if (escapeButtons != null)
        {
            foreach (Button button in escapeButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(OnEscapeButtonClicked);
                }
            }
        }
    }
}

