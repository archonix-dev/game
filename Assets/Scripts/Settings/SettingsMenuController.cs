using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject menuRoot;
    public PlayerController playerController;
    public MouseLook mouseLook;
	[Tooltip("Камера, которую нужно перемещать/поворачивать при открытии/закрытии меню")]
	public Transform cameraTransform;
	
	[Header("Camera Transition")]
	[Tooltip("Точка, к которой плавно переместится камера при открытии меню")]
	public Transform openCameraPoint;
	[Tooltip("Точка, к которой плавно переместится камера при закрытии меню")]
	public Transform closeCameraPoint;
	[Tooltip("Время анимации перемещения/поворота камеры")]
	public float cameraMoveDuration = 0.6f;
	[Tooltip("Кривая анимации перемещения/поворота камеры")]
	public AnimationCurve cameraMoveCurve;
	[Tooltip("Абсолютный угол Y для камеры при открытии меню")]
	public float openYaw = 183f;
	[Tooltip("Абсолютный угол Y для камеры при закрытии меню")]
	public float closeYaw = 0f;
	[Tooltip("При открытии меню повернуть камеру к самому меню (world-space canvas)")]
	public bool lookAtMenuOnOpen = true;
	[Tooltip("Смещение точки взгляда относительно центра меню")]
	public Vector3 menuLookOffset = Vector3.zero;
	
	[Header("Auto-hide Objects")]
	[Tooltip("Объект, который скрывается при открытом меню и показывается при закрытом")]
	public GameObject hideWhenMenuOpenA;
	[Tooltip("Дополнительный объект, который скрывается при открытом меню и показывается при закрытом")]
	public GameObject hideWhenMenuOpenB;
	
	// Авто-привязываемый LineRenderer из VoiceWaveVisualizer
	private LineRenderer voiceWaveLine;
	private VoiceWaveVisualizer voiceWaveVisualizer;
    
    [Header("Buttons")]
    [Tooltip("Кнопка для закрытия меню (продолжить)")]
    public Button continueButton;
    
    private bool isMenuOpen = false;
	private Coroutine cameraMoveRoutine;
    
    void Start()
    {
        SetMenuState(false);
        
        // Подписываемся на событие нажатия кнопки "Продолжить"
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }
		
		// Если камера не назначена — попытаемся найти камеру у игрока
		if (cameraTransform == null && playerController != null)
		{
			Camera cam = playerController.GetComponentInChildren<Camera>();
			if (cam != null)
			{
				cameraTransform = cam.transform;
			}
		}
		
		
		// Автоматически находим VoiceWaveVisualizer и его LineRenderer
		FindVoiceWaveLineRenderer();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Открываем меню только если оно закрыто и игрок может его открыть (не лежит)
            // Закрываем меню в любом случае
            if (!isMenuOpen)
            {
                // Проверяем, может ли игрок открыть меню (не лежит)
                if (playerController != null && playerController.CanOpenMenu())
                {
                    SetMenuState(true);
                }
            }
            else
            {
                SetMenuState(false);
            }
        }
    }
    
    public void SetMenuState(bool open)
    {
        isMenuOpen = open;
        if (menuRoot != null)
        {
            menuRoot.SetActive(open);
        }
        
        if (playerController != null)
        {
            playerController.enabled = !open;
        }
        
        if (mouseLook != null)
        {
            mouseLook.enabled = !open;
        }
		
		// Скрыть/показать дополнительные объекты
		if (hideWhenMenuOpenA != null) hideWhenMenuOpenA.SetActive(!open);
		if (hideWhenMenuOpenB != null) hideWhenMenuOpenB.SetActive(!open);
		
		// Скрыть/показать линию голоса (если найдена)
		// Пытаемся найти LineRenderer если еще не найден (например, объект появился позже)
		if (voiceWaveLine == null)
		{
			FindVoiceWaveLineRenderer();
		}
		
		if (voiceWaveLine != null)
		{
			voiceWaveLine.enabled = !open;
		}
        
		// Движение/поворот камеры
		StartCameraTransition(open);
		
        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Управление видимостью headObject больше не требуется в локальной игре
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Управление видимостью headObject больше не требуется в локальной игре
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку "Продолжить"
    /// </summary>
    private void OnContinueButtonClicked()
    {
        SetMenuState(false);
    }
    
    void OnDestroy()
    {
        // Отписываемся от события при уничтожении объекта
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonClicked);
        }
    }
	
	private void StartCameraTransition(bool opening)
	{
		if (cameraTransform == null)
			return;
		
		if (cameraMoveRoutine != null)
		{
			StopCoroutine(cameraMoveRoutine);
			cameraMoveRoutine = null;
		}
		
		Transform targetPoint = opening ? openCameraPoint : closeCameraPoint;
		float targetYaw = opening ? openYaw : closeYaw;
		cameraMoveRoutine = StartCoroutine(AnimateCameraTo(opening, targetPoint, targetYaw, cameraMoveDuration));
	}
	
	private IEnumerator AnimateCameraTo(bool opening, Transform targetPoint, float targetYaw, float duration)
	{
		Vector3 startPos = cameraTransform.position;
		Vector3 endPos = targetPoint != null ? targetPoint.position : startPos;
		
		// Ротация: при открытии можно направить камеру на меню, иначе использовать фиксированный yaw
		Quaternion startRot = cameraTransform.rotation;
		Quaternion endRot;
		if (opening && lookAtMenuOnOpen && menuRoot != null)
		{
			Vector3 lookTarget = menuRoot.transform.position + menuLookOffset;
			Vector3 lookDir = (lookTarget - (targetPoint != null ? targetPoint.position : cameraTransform.position));
			if (lookDir.sqrMagnitude < 0.0001f) lookDir = cameraTransform.forward;
			endRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
		}
		else
		{
			// Сохраняем текущие углы; меняем только Y
			Vector3 startEuler = cameraTransform.eulerAngles;
			Vector3 endEuler = new Vector3(startEuler.x, targetYaw, startEuler.z);
			endRot = Quaternion.Euler(endEuler);
		}
		
		float t = 0f;
		float d = Mathf.Max(0.0001f, duration);
		
		while (t < 1f)
		{
			t += Time.unscaledDeltaTime / d;
			float k = cameraMoveCurve != null && cameraMoveCurve.length > 0 ? cameraMoveCurve.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
			
			cameraTransform.position = Vector3.Lerp(startPos, endPos, k);
			cameraTransform.rotation = Quaternion.Slerp(startRot, endRot, k);
			
			yield return null;
		}
		
		cameraTransform.position = endPos;
		cameraTransform.rotation = endRot;
		cameraMoveRoutine = null;
	}
	
	/// <summary>
	/// Находит VoiceWaveVisualizer и его LineRenderer в сцене
	/// </summary>
	private void FindVoiceWaveLineRenderer()
	{
		// Сначала ищем VoiceWaveVisualizer
		if (voiceWaveVisualizer == null)
		{
			voiceWaveVisualizer = FindObjectOfType<VoiceWaveVisualizer>();
		}
		
		// Если нашли VoiceWaveVisualizer, ищем LineRenderer
		if (voiceWaveVisualizer != null && voiceWaveLine == null)
		{
			// LineRenderer может быть на самом объекте, в дочерних объектах, или на lineParent
			voiceWaveLine = voiceWaveVisualizer.GetComponent<LineRenderer>();
			
			if (voiceWaveLine == null)
			{
				// Ищем в дочерних объектах (включая неактивные)
				voiceWaveLine = voiceWaveVisualizer.GetComponentInChildren<LineRenderer>(true);
			}
			
			// Если все еще не найден, пробуем найти через lineParent
			if (voiceWaveLine == null)
			{
				// Используем рефлексию или публичное поле, если оно есть
				// Но проще просто поискать по имени "VoiceWaveLine" который создается в SetupLineRenderer
				Transform lineParent = voiceWaveVisualizer.transform.Find("VoiceWaveLine");
				if (lineParent != null)
				{
					voiceWaveLine = lineParent.GetComponent<LineRenderer>();
				}
			}
		}
	}
}
