using UnityEngine;
using UnityEngine.Audio;

public class FootstepController : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("AudioSource для проигрывания звуков шагов")]
    [SerializeField] public AudioSource audioSource;
    
    [Tooltip("Массив звуков шагов для случайного выбора")]
    [SerializeField] public AudioClip[] footstepSounds;
    
    [Tooltip("AudioMixerGroup для звуков шагов (Steps)")]
    [SerializeField] private AudioMixerGroup audioMixerGroup;
    
    [Tooltip("Громкость звуков шагов")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    
    // Отслеживание предыдущих фаз ног для определения касания земли
    private float[] previousPhases;
    private bool[] hasPlayedStepForPhase;
    public int legCount = 0;
    
    void Awake()
    {
        // Находим AudioSource если не назначен
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = volume;
            
            // Устанавливаем AudioMixerGroup если назначен и еще не установлен
            if (audioMixerGroup != null && audioSource.outputAudioMixerGroup == null)
            {
                audioSource.outputAudioMixerGroup = audioMixerGroup;
            }
            // Если AudioMixerGroup уже установлен в инспекторе, используем его
            else if (audioSource.outputAudioMixerGroup != null && audioMixerGroup == null)
            {
                audioMixerGroup = audioSource.outputAudioMixerGroup;
            }
        }
    }
    
    /// <summary>
    /// Инициализирует отслеживание фаз для указанного количества ног
    /// </summary>
    public void InitializeLegTracking(int numberOfLegs)
    {
        legCount = numberOfLegs;
        previousPhases = new float[legCount];
        hasPlayedStepForPhase = new bool[legCount];
        
        // Инициализируем все фазы большим значением, чтобы не проигрывать звук сразу
        for (int i = 0; i < legCount; i++)
        {
            previousPhases[i] = 999f;
            hasPlayedStepForPhase[i] = false;
        }
    }
    
    /// <summary>
    /// Проверяет фазу ноги и проигрывает звук шага когда нога касается земли
    /// </summary>
    /// <param name="legIndex">Индекс ноги (0, 1, 2, 3...)</param>
    /// <param name="phase">Текущая фаза ноги</param>
    /// <param name="isMoving">Движется ли игрок</param>
    public void CheckLegPhase(int legIndex, float phase, bool isMoving)
    {
        if (audioSource == null || !isMoving)
        {
            return;
        }
        
        if (legIndex < 0 || legIndex >= legCount)
        {
            return;
        }
        
        // Нормализуем фазу в диапазон [0, 2π]
        float normalizedPhase = phase % (2f * Mathf.PI);
        if (normalizedPhase < 0) normalizedPhase += 2f * Mathf.PI;
        
        float previousPhase = previousPhases[legIndex];
        if (previousPhase > 10f) // Первая инициализация
        {
            previousPhases[legIndex] = normalizedPhase;
            return;
        }
        
        // Нормализуем предыдущую фазу
        float normalizedPreviousPhase = previousPhase % (2f * Mathf.PI);
        if (normalizedPreviousPhase < 0) normalizedPreviousPhase += 2f * Mathf.PI;
        
        // Определяем момент касания земли: когда sin(phase) переходит через 0 снизу вверх
        // Нога касается земли когда sin(phase) близок к 0 и движется вверх
        float sinPhase = Mathf.Sin(normalizedPhase);
        float sinPreviousPhase = Mathf.Sin(normalizedPreviousPhase);
        
        // Касание земли: sin переходит от отрицательного к положительному через 0
        // Или когда sin близок к 0 и увеличивается
        bool footTouchingGround = Mathf.Abs(sinPhase) < 0.3f && sinPreviousPhase < sinPhase && sinPhase > -0.1f;
        
        // Альтернативная проверка: переход через 0 снизу вверх (когда phase проходит через 0 или 2π)
        bool crossedZeroUpward = false;
        if (normalizedPreviousPhase > Mathf.PI * 1.5f && normalizedPhase < Mathf.PI * 0.5f)
        {
            // Переход через 2π -> 0 (снизу вверх)
            crossedZeroUpward = true;
        }
        else if (normalizedPreviousPhase > normalizedPhase && normalizedPreviousPhase - normalizedPhase > Mathf.PI)
        {
            // Переход через 0 (когда фаза уменьшается и переходит границу снизу вверх)
            crossedZeroUpward = true;
        }
        
        if ((footTouchingGround || crossedZeroUpward) && !hasPlayedStepForPhase[legIndex])
        {
            PlayFootstep();
            hasPlayedStepForPhase[legIndex] = true;
        }
        
        // Сбрасываем флаг когда нога поднимается (sin далеко от 0)
        if (Mathf.Abs(sinPhase) > 0.5f)
        {
            hasPlayedStepForPhase[legIndex] = false;
        }
        
        previousPhases[legIndex] = normalizedPhase;
    }
    
    /// <summary>
    /// Проигрывает случайный звук шага
    /// </summary>
    private void PlayFootstep()
    {
        if (audioSource == null)
        {
            return;
        }
        
        AudioClip clipToPlay = null;
        
        // Используем массив звуков если он заполнен
        if (footstepSounds != null && footstepSounds.Length > 0)
        {
            // Выбираем случайный звук из массива
            int randomIndex = Random.Range(0, footstepSounds.Length);
            clipToPlay = footstepSounds[randomIndex];
        }
        // Если массив пуст, используем клип из AudioSource
        else if (audioSource.clip != null)
        {
            clipToPlay = audioSource.clip;
        }
        
        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, volume);
        }
    }
    
    /// <summary>
    /// Устанавливает громкость звуков шагов
    /// </summary>
    /// <param name="newVolume">Новая громкость (0-1)</param>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }
    
    /// <summary>
    /// Устанавливает массив звуков шагов
    /// </summary>
    /// <param name="sounds">Массив AudioClip</param>
    public void SetFootstepSounds(AudioClip[] sounds)
    {
        footstepSounds = sounds;
    }
    
    /// <summary>
    /// Устанавливает AudioMixerGroup для звуков шагов
    /// </summary>
    /// <param name="mixerGroup">AudioMixerGroup для Steps</param>
    public void SetAudioMixerGroup(AudioMixerGroup mixerGroup)
    {
        audioMixerGroup = mixerGroup;
        if (audioSource != null && audioMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = mixerGroup;
        }
    }
}
