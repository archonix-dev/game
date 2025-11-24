using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Плавно скрывает Image после завершения анимации загрузки в LobbyLoadingController
/// </summary>
public class LoadingScreenFadeOut : MonoBehaviour
{
    [Header("Image Settings")]
    [Tooltip("Image компонент, который нужно скрыть (если не назначен, будет найден автоматически)")]
    [SerializeField] private Image targetImage;
    
    [Header("Fade Settings")]
    [Tooltip("Время плавного исчезновения (в секундах)")]
    [SerializeField] private float fadeDuration = 1f;
    
    [Tooltip("Задержка перед началом исчезновения после завершения анимации (в секундах)")]
    [SerializeField] private float delayBeforeFade = 0.1f;
    
    private bool hasStartedFade = false;
    private Coroutine fadeCoroutine;
    
    void Awake()
    {
        // Находим Image если не назначен
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
            if (targetImage == null)
            {
                targetImage = GetComponentInChildren<Image>();
            }
        }
    }
    
    void Start()
    {
        // Убеждаемся, что Image изначально видим (alpha = 255/255 = 1.0)
        if (targetImage != null)
        {
            Color color = targetImage.color;
            color.a = 1f; // Полностью непрозрачный (255/255)
            targetImage.color = color;
            targetImage.gameObject.SetActive(true);
        }
    }
    
    void Update()
    {
        // Проверяем, не начали ли мы уже процесс затухания
        if (hasStartedFade)
        {
            return;
        }
        
        if (IsAnyLoadingControllerActive())
        {
            return;
        }
        
        // Анимация завершена, начинаем затухание
        if (targetImage != null && targetImage.gameObject.activeSelf)
        {
            hasStartedFade = true;
            fadeCoroutine = StartCoroutine(FadeOutImage());
        }
    }
    
    /// <summary>
    /// Плавно изменяет прозрачность Image от 255 (1.0) до 0 (0.0) и скрывает его
    /// </summary>
    IEnumerator FadeOutImage()
    {
        if (targetImage == null)
        {
            yield break;
        }
        
        // Небольшая задержка перед началом затухания
        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFade);
        }
        
        Color color = targetImage.color;
        float startAlpha = color.a; // Должно быть 1.0 (255/255)
        float targetAlpha = 0f; // 0 (0/255)
        
        float elapsedTime = 0f;
        
        // Плавно изменяем прозрачность
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            
            // Интерполируем от startAlpha (1.0) до targetAlpha (0.0)
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            targetImage.color = color;
            
            yield return null;
        }
        
        // Убеждаемся, что финальное значение установлено
        color.a = targetAlpha; // 0.0 (0/255)
        targetImage.color = color;
        
        // Скрываем Image после завершения затухания
        targetImage.gameObject.SetActive(false);
        
        Debug.Log("[LoadingScreenFadeOut] Image успешно скрыт после плавного затухания");
        
        fadeCoroutine = null;
    }
    
    /// <summary>
    /// Принудительно запускает затухание (можно вызвать вручную)
    /// </summary>
    public void StartFadeOut()
    {
        if (!hasStartedFade && fadeCoroutine == null && targetImage != null)
        {
            hasStartedFade = true;
            fadeCoroutine = StartCoroutine(FadeOutImage());
        }
    }
    
    void OnDestroy()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    bool IsAnyLoadingControllerActive()
    {
        if (IsControllerActive(LobbyLoadingController.Instance))
            return true;

        if (LobbyLoadingController.Instance == null)
        {
            LobbyLoadingController controller = FindObjectOfType<LobbyLoadingController>();
            if (IsControllerActive(controller))
                return true;
        }

        if (IsControllerActive(LobbyMainLoadingController.Instance))
            return true;

        if (LobbyMainLoadingController.Instance == null)
        {
            LobbyMainLoadingController controller = FindObjectOfType<LobbyMainLoadingController>();
            if (IsControllerActive(controller))
                return true;
        }

        return false;
    }

    bool IsControllerActive(MonoBehaviour controller)
    {
        return controller != null && controller.gameObject.activeSelf;
    }
}

