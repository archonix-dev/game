using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Collections;

/// <summary>
/// Компонент для визуализации тревоги на игроке
/// Показывает Image с filled (от 1 до 0 за 60 секунд) и проигрывает звук тревоги
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerAlarmVisualizer : NetworkBehaviour
{
    [Header("Alarm UI")]
    [Tooltip("Image с компонентом Image (filled), который будет показывать прогресс тревоги")]
    [SerializeField] private Image alarmFillImage;
    [SerializeField] private GameObject alarmUI;
    
    [Header("Alarm Text")]
    [Tooltip("Text (TMP_Text) для отображения сообщений тревоги")]
    [SerializeField] private Text alarmText;
    [Tooltip("Массив сообщений, которые будут печататься во время тревоги")]
    [SerializeField] private string[] alarmMessages = new string[] { "ALERT", "INTRUDER DETECTED", "SYSTEM ALERT" };
    [Tooltip("Интервал между символами при печати (секунды)")]
    [SerializeField] private float typewriterCharInterval = 0.05f;
    [Tooltip("Скорость мигания текста (секунды на цикл)")]
    [SerializeField] private float blinkSpeed = 0.5f;
    [Tooltip("Красный цвет для текста тревоги")]
    [SerializeField] private Color alarmTextColor = Color.red;
    
    [Header("Alarm Audio")]
    [Tooltip("AudioSource для звука тревоги (уже должен быть в префабе)")]
    [SerializeField] private AudioSource alarmAudioSource;
    
    [Header("Alarm Fill Color")]
    [Tooltip("Цвет заполнения в начале тревоги (красный)")]
    [SerializeField] private Color alarmStartColor = Color.red;
    [Tooltip("Цвет заполнения в конце тревоги (белый)")]
    [SerializeField] private Color alarmEndColor = Color.white;


    
    private bool isAlarmActive = false;
    private float alarmStartTime = 0f;
    private float alarmDuration = 60f;
    private Coroutine typewriterCoroutine;
    private Coroutine blinkCoroutine;
    
    void Update()
    {
        // Обновляем визуализацию только для локального игрока
        if (!isOwned || !isAlarmActive)
        {
            if (alarmFillImage != null)
            {
                alarmFillImage.gameObject.SetActive(false);
            }
            if (alarmUI != null)
            {
                alarmUI.SetActive(false);
            }
            if (alarmText != null)
            {
                alarmText.gameObject.SetActive(false);
            }
            return;
        }
        
        // Обновляем заполнение Image
        if (alarmFillImage != null)
        {
            float elapsed = Time.time - alarmStartTime;
            float progress = Mathf.Clamp01(1f - (elapsed / alarmDuration));
            
            alarmFillImage.fillAmount = progress;
            
            // Изменяем цвет от красного к белому
            alarmFillImage.color = Color.Lerp(alarmEndColor, alarmStartColor, progress);
            
            // Скрываем Image когда тревога закончилась
            if (progress <= 0f)
            {
                alarmFillImage.gameObject.SetActive(false);
                if (alarmUI != null)
                {
                    alarmUI.SetActive(false);
                }
                isAlarmActive = false;
            }
        }
    }
    
    /// <summary>
    /// Запускает тревогу (вызывается с сервера)
    /// </summary>
    public void StartAlarm(float duration)
    {
        if (!isServer) return;
        
        RpcStartAlarm(duration);
    }
    
    /// <summary>
    /// Останавливает тревогу (вызывается с сервера)
    /// </summary>
    public void StopAlarm()
    {
        if (!isServer) return;
        
        RpcStopAlarm();
    }
    
    [ClientRpc]
    void RpcStartAlarm(float duration)
    {
        isAlarmActive = true;
        alarmStartTime = Time.time;
        alarmDuration = duration;
        
        // Показываем Image только для локального игрока
        if (isOwned)
        {
            if (alarmFillImage != null)
            {
                alarmFillImage.gameObject.SetActive(true);
                alarmFillImage.fillAmount = 1f;
                alarmFillImage.color = alarmStartColor; // Начинаем с красного
            }
            
            if (alarmUI != null)
            {
                alarmUI.SetActive(true);
            }
            
            // Запускаем typewriter эффект и мигание текста
            if (alarmText != null && alarmMessages != null && alarmMessages.Length > 0)
            {
                alarmText.gameObject.SetActive(true);
                StartTypewriterEffect();
                StartBlinkEffect();
            }
        }
        
        // Проигрываем звук тревоги
        if (alarmAudioSource != null && !alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Play();
        }
    }
    
    [ClientRpc]
    void RpcStopAlarm()
    {
        isAlarmActive = false;
        
        // Останавливаем корутины
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        
        // Скрываем Image
        if (alarmFillImage != null)
        {
            alarmFillImage.gameObject.SetActive(false);
        }
        
        if (alarmUI != null)
        {
            alarmUI.SetActive(false);
        }
        
        // Скрываем текст
        if (alarmText != null)
        {
            alarmText.gameObject.SetActive(false);
            alarmText.text = string.Empty;
        }
        
        // Останавливаем звук тревоги
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
        }
    }
    
    /// <summary>
    /// Запускает эффект печати текста (typewriter)
    /// </summary>
    void StartTypewriterEffect()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }
        
        typewriterCoroutine = StartCoroutine(TypewriterRoutine());
    }
    
    /// <summary>
    /// Корутина для печати текста посимвольно
    /// </summary>
    IEnumerator TypewriterRoutine()
    {
        if (alarmText == null || alarmMessages == null || alarmMessages.Length == 0)
            yield break;
        
        int messageIndex = 0;
        float delay = Mathf.Max(typewriterCharInterval, 0.01f);
        
        while (isAlarmActive)
        {
            string currentMessage = alarmMessages[messageIndex];
            alarmText.text = string.Empty;
            
            // Печатаем сообщение посимвольно
            foreach (char c in currentMessage)
            {
                if (!isAlarmActive)
                    yield break;
                
                alarmText.text += c;
                yield return new WaitForSeconds(delay);
            }
            
            // Ждем немного перед следующим сообщением
            yield return new WaitForSeconds(0.5f);
            
            // Переходим к следующему сообщению
            messageIndex = (messageIndex + 1) % alarmMessages.Length;
        }
    }
    
    /// <summary>
    /// Запускает эффект мигания текста красным цветом
    /// </summary>
    void StartBlinkEffect()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
        
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }
    
    /// <summary>
    /// Корутина для мигания текста красным цветом
    /// </summary>
    IEnumerator BlinkRoutine()
    {
        if (alarmText == null)
            yield break;
        
        Color originalColor = alarmTextColor;
        Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        
        float halfBlinkTime = blinkSpeed * 0.5f;
        
        while (isAlarmActive)
        {
            // Плавно появляемся
            float elapsed = 0f;
            while (elapsed < halfBlinkTime && isAlarmActive)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfBlinkTime;
                alarmText.color = Color.Lerp(transparentColor, originalColor, t);
                yield return null;
            }
            
            if (!isAlarmActive)
                yield break;
            
            // Плавно исчезаем
            elapsed = 0f;
            while (elapsed < halfBlinkTime && isAlarmActive)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfBlinkTime;
                alarmText.color = Color.Lerp(originalColor, transparentColor, t);
                yield return null;
            }
        }
    }
}

