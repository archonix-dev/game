using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Скрипт для изменения параметра ReverbSteps (Decay Time) в AudioMixer
/// когда игрок находится в зоне триггера
/// </summary>
[RequireComponent(typeof(Collider))]
public class FootstepReverbZone : MonoBehaviour
{
    [Header("Audio Mixer Settings")]
    [Tooltip("AudioMixer, в котором находится параметр ReverbSteps")]
    [SerializeField] private AudioMixer audioMixer;
    
    [Header("Reverb Settings")]
    [Tooltip("Значение Decay Time для параметра ReverbSteps (от 0.10 до 20)")]
    [SerializeField, Range(0.10f, 20f)] private float decayTime = 1.0f;
    
    [Header("Debug")]
    [Tooltip("Показывать сообщения в консоли при входе/выходе игрока")]
    [SerializeField] private bool debugLog = false;
    
    private const string REVERB_STEPS_PARAMETER = "ReverbSteps";
    private Collider triggerCollider;
    
    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError($"[FootstepReverbZone] На объекте {gameObject.name} не найден Collider!");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Проверяем, что это игрок
        if (!IsPlayer(other))
        {
            return;
        }
        
        if (audioMixer == null)
        {
            Debug.LogWarning($"[FootstepReverbZone] AudioMixer не назначен на объекте {gameObject.name}!");
            return;
        }
        
        // Устанавливаем новое значение
        bool success = audioMixer.SetFloat(REVERB_STEPS_PARAMETER, decayTime);
        
        if (success)
        {
            if (debugLog)
            {
                Debug.Log($"[FootstepReverbZone] Игрок вошел в зону {gameObject.name}. ReverbSteps установлен в {decayTime}");
            }
        }
        else
        {
            Debug.LogError($"[FootstepReverbZone] Не удалось установить параметр {REVERB_STEPS_PARAMETER} в AudioMixer!");
        }
    }
    
    /// <summary>
    /// Проверяет, является ли объект игроком
    /// </summary>
    private bool IsPlayer(Collider other)
    {
        // Проверяем наличие компонента PlayerController
        PlayerController playerController = other.GetComponent<PlayerController>();
        if (playerController != null)
        {
            return true;
        }
        
        // Проверяем наличие компонента в родительских объектах
        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController != null)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Устанавливает значение Decay Time программно
    /// </summary>
    public void SetDecayTime(float newDecayTime)
    {
        decayTime = Mathf.Clamp(newDecayTime, 0.10f, 20f);
    }
    
    void OnValidate()
    {
        // Ограничиваем значения в инспекторе
        decayTime = Mathf.Clamp(decayTime, 0.10f, 20f);
    }
}

