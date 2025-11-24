using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;

[System.Serializable]
public class AudioSettings
{
    [Header("Audio Mixer Groups")]
    public AudioMixerGroup masterGroup;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup playersGroup;
    public AudioMixerGroup uiGroup;
    public AudioMixerGroup stepsGroup;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    
    [Range(0f, 1f)]
    public float musicVolume = 1f;
    
    [Range(0f, 1f)]
    public float playersVolume = 1f;
    
    [Range(0f, 1f)]
    public float uiVolume = 1f;
    
    [Range(0f, 1f)]
    public float stepsVolume = 1f;
    
    [Range(0f, 1f)]
    [Tooltip("Громкость окружающих звуков (AudioMixer параметр 'Enivoment')")]
    public float environmentVolume = 1f;

    [Header("Microphone Settings")]
    [Tooltip("Имя устройства микрофона (null = устройство по умолчанию)")]
    public string microphoneDevice = null;
    
    [Range(0.0001f, 100f)]
    [Tooltip("Чувствительность микрофона")]
    public float microphoneSensitivity = 10f;

    // Конвертация линейного значения в децибелы для AudioMixer
    private float LinearToDecibels(float linear)
    {
        return linear > 0 ? 20f * Mathf.Log10(linear) : -80f;
    }

    // Применение настроек к AudioMixer
    public void ApplySettings(AudioMixer audioMixer)
    {
        if (audioMixer == null) 
        {
            return;
        }

        try
        {
            bool success = true;
            
            success &= audioMixer.SetFloat("Master", LinearToDecibels(masterVolume));
            success &= audioMixer.SetFloat("Music", LinearToDecibels(musicVolume));
            success &= audioMixer.SetFloat("Players", LinearToDecibels(playersVolume));
            success &= audioMixer.SetFloat("UI", LinearToDecibels(uiVolume));
            success &= audioMixer.SetFloat("Steps", LinearToDecibels(stepsVolume));
            success &= audioMixer.SetFloat("Enivoment", LinearToDecibels(environmentVolume));

        }
        catch (System.Exception e)
        {
        }
    }

    // Загрузка настроек из PlayerPrefs
    public void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        playersVolume = PlayerPrefs.GetFloat("PlayersVolume", 1f);
        uiVolume = PlayerPrefs.GetFloat("UIVolume", 1f);
        stepsVolume = PlayerPrefs.GetFloat("StepsVolume", 1f);
        environmentVolume = PlayerPrefs.GetFloat("EnvironmentVolume", 1f);
        microphoneDevice = PlayerPrefs.GetString("MicrophoneDevice", null);
        if (string.IsNullOrEmpty(microphoneDevice)) microphoneDevice = null;
        microphoneSensitivity = PlayerPrefs.GetFloat("MicrophoneSensitivity", 10f);
    }

    // Сохранение настроек в PlayerPrefs
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("PlayersVolume", playersVolume);
        PlayerPrefs.SetFloat("UIVolume", uiVolume);
        PlayerPrefs.SetFloat("StepsVolume", stepsVolume);
        PlayerPrefs.SetFloat("EnvironmentVolume", environmentVolume);
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            PlayerPrefs.DeleteKey("MicrophoneDevice");
        }
        else
        {
            PlayerPrefs.SetString("MicrophoneDevice", microphoneDevice);
        }
        PlayerPrefs.SetFloat("MicrophoneSensitivity", microphoneSensitivity);
        PlayerPrefs.Save();
    }
}

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Audio Sliders")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider playersVolumeSlider;
    public Slider uiVolumeSlider;
    public Slider stepsVolumeSlider;
    public Slider environmentVolumeSlider;
    public Slider microphoneSensitivitySlider;
    public Text microphoneSensitivityLabel;

    [Header("Dropdowns")]
    public Dropdown microphoneDropdown;

    [Header("Buttons")]
    public Button resetButton;
    public Button applyButton;
    [Tooltip("Кнопка для прослушивания микрофона")]
    public Button testMicrophoneButton;

    [Header("Microphone Monitoring")]
    [Tooltip("Image для визуальной индикации прослушивания микрофона")]
    public UnityEngine.UI.Image microphoneIndicatorImage;
    [Tooltip("Скорость изменения прозрачности (секунды на цикл)")]
    public float indicatorFadeSpeed = 1f;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    private AudioSettings currentSettings;
    
    // Переменные для прослушивания микрофона
    private AudioSource microphoneAudioSource;
    private AudioClip microphoneClip;
    private bool isMonitoringMicrophone = false;
    private float savedMusicVolume = 1f; // Сохраненное значение громкости Music перед уменьшением
    private Coroutine musicVolumeCoroutine;
    private Coroutine indicatorFadeCoroutine; // Корутина для анимации индикатора

    private void Start()
    {
        InitializeSettings();
        InitializeUI();
        LoadCurrentSettings();
        InitializeMicrophoneAudioSource();
        InitializeMicrophoneIndicator();
    }
    
    private void InitializeMicrophoneIndicator()
    {
        // Скрываем индикатор по умолчанию
        if (microphoneIndicatorImage != null)
        {
            microphoneIndicatorImage.gameObject.SetActive(false);
            // Устанавливаем начальную прозрачность
            Color color = microphoneIndicatorImage.color;
            color.a = 0f;
            microphoneIndicatorImage.color = color;
        }
    }
    
    private void InitializeMicrophoneAudioSource()
    {
        // Создаем AudioSource для воспроизведения микрофона
        microphoneAudioSource = gameObject.AddComponent<AudioSource>();
        microphoneAudioSource.loop = true;
        microphoneAudioSource.playOnAwake = false;
        
        // Настраиваем AudioSource для использования AudioMixer
        // Используем playersGroup, если он доступен, иначе будет использован Master
        if (currentSettings != null && currentSettings.playersGroup != null)
        {
            microphoneAudioSource.outputAudioMixerGroup = currentSettings.playersGroup;
        }
    }
    
    private void OnDestroy()
    {
        // Останавливаем микрофон при уничтожении объекта
        StopMicrophoneMonitoring();
    }

    private void InitializeUI()
    {
        // Настройка слайдеров
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (playersVolumeSlider != null)
        {
            playersVolumeSlider.minValue = 0f;
            playersVolumeSlider.maxValue = 1f;
            playersVolumeSlider.onValueChanged.AddListener(OnPlayersVolumeChanged);
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.minValue = 0f;
            uiVolumeSlider.maxValue = 1f;
            uiVolumeSlider.onValueChanged.AddListener(OnUIVolumeChanged);
        }

        if (stepsVolumeSlider != null)
        {
            stepsVolumeSlider.minValue = 0f;
            stepsVolumeSlider.maxValue = 1f;
            stepsVolumeSlider.onValueChanged.AddListener(OnStepsVolumeChanged);
        }
        
        if (environmentVolumeSlider != null)
        {
            environmentVolumeSlider.minValue = 0f;
            environmentVolumeSlider.maxValue = 1f;
            environmentVolumeSlider.onValueChanged.AddListener(OnEnvironmentVolumeChanged);
        }

        // Настройка Microphone Sensitivity Slider
        if (microphoneSensitivitySlider != null)
        {
            microphoneSensitivitySlider.minValue = 0.0001f;
            microphoneSensitivitySlider.maxValue = 100f;
            microphoneSensitivitySlider.onValueChanged.AddListener(OnMicrophoneSensitivityChanged);
        }

        // Настройка Microphone Dropdown
        if (microphoneDropdown != null)
        {
            BuildMicrophoneOptions();
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneDeviceChanged);
        }

        // Настройка кнопок
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClicked);

        if (applyButton != null)
            applyButton.onClick.AddListener(OnApplyButtonClicked);
            
        if (testMicrophoneButton != null)
            testMicrophoneButton.onClick.AddListener(OnTestMicrophoneButtonClicked);
    }

    private void InitializeSettings()
    {
        if (currentSettings == null)
        {
            currentSettings = new AudioSettings();
        }
        
        // Загружаем сохраненные настройки
        currentSettings.LoadSettings();
        
        // Применяем настройки
        ApplyAllSettings();
    }

    public void LoadCurrentSettings()
    {
        if (currentSettings != null)
        {
            currentSettings.LoadSettings();
            UpdateUI();
        }
    }

    private void ApplyAllSettings()
    {
        if (currentSettings != null && audioMixer != null)
        {
            currentSettings.ApplySettings(audioMixer);
        }
    }

    private void UpdateUI()
    {
        if (currentSettings == null) return;

        // Обновляем слайдеры
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = currentSettings.masterVolume;

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = currentSettings.musicVolume;

        if (playersVolumeSlider != null)
            playersVolumeSlider.value = currentSettings.playersVolume;

        if (uiVolumeSlider != null)
            uiVolumeSlider.value = currentSettings.uiVolume;

        if (stepsVolumeSlider != null)
            stepsVolumeSlider.value = currentSettings.stepsVolume;
        
        if (environmentVolumeSlider != null)
            environmentVolumeSlider.value = currentSettings.environmentVolume;

        if (microphoneSensitivitySlider != null)
            microphoneSensitivitySlider.value = currentSettings.microphoneSensitivity;

        if (microphoneDropdown != null)
        {
            UpdateMicrophoneDropdown();
        }

        UpdateMicrophoneSensitivityLabel();
    }

    private void BuildMicrophoneOptions()
    {
        if (microphoneDropdown == null) return;

        var options = new System.Collections.Generic.List<string>();
        options.Add("По умолчанию"); // Индекс 0 - устройство по умолчанию (null)

        string[] devices = Microphone.devices;
        if (devices != null && devices.Length > 0)
        {
            for (int i = 0; i < devices.Length; i++)
            {
                options.Add(devices[i]);
            }
        }
        else
        {
            options.Add("Микрофоны не найдены");
        }

        microphoneDropdown.ClearOptions();
        microphoneDropdown.AddOptions(options);
    }

    private void UpdateMicrophoneDropdown()
    {
        if (microphoneDropdown == null || currentSettings == null) return;

        if (string.IsNullOrEmpty(currentSettings.microphoneDevice))
        {
            microphoneDropdown.value = 0; // "По умолчанию"
        }
        else
        {
            string[] devices = Microphone.devices;
            if (devices != null)
            {
                int index = System.Array.IndexOf(devices, currentSettings.microphoneDevice);
                if (index >= 0)
                {
                    microphoneDropdown.value = index + 1; // +1 потому что индекс 0 - "По умолчанию"
                }
                else
                {
                    microphoneDropdown.value = 0; // Если устройство не найдено, используем по умолчанию
                }
            }
            else
            {
                microphoneDropdown.value = 0;
            }
        }
    }

    private void UpdateMicrophoneSensitivityLabel()
    {
        if (microphoneSensitivityLabel != null && currentSettings != null)
        {
            microphoneSensitivityLabel.text = $"{currentSettings.microphoneSensitivity:F1}";
        }
    }

    #region Event Handlers
    private void OnMasterVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.masterVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.musicVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }

    private void OnPlayersVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.playersVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }

    private void OnUIVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.uiVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }

    private void OnStepsVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.stepsVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }
    
    private void OnEnvironmentVolumeChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.environmentVolume = Mathf.Clamp01(value);
            if (audioMixer != null)
            {
                currentSettings.ApplySettings(audioMixer);
            }
        }
    }

    private void OnMicrophoneSensitivityChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.microphoneSensitivity = Mathf.Clamp(value, 0.0001f, 100f);
            UpdateMicrophoneSensitivityLabel();
            
            // Обновляем громкость микрофона во время прослушивания
            if (isMonitoringMicrophone && microphoneAudioSource != null)
            {
                ApplyMicrophoneSensitivity();
            }
        }
    }

    private void OnMicrophoneDeviceChanged(int value)
    {
        if (currentSettings == null) return;

        if (value == 0)
        {
            // "По умолчанию"
            currentSettings.microphoneDevice = null;
        }
        else
        {
            string[] devices = Microphone.devices;
            if (devices != null && devices.Length > 0)
            {
                int deviceIndex = value - 1; // -1 потому что индекс 0 - "По умолчанию"
                if (deviceIndex >= 0 && deviceIndex < devices.Length)
                {
                    currentSettings.microphoneDevice = devices[deviceIndex];
                }
                else
                {
                    currentSettings.microphoneDevice = null;
                }
            }
            else
            {
                currentSettings.microphoneDevice = null;
            }
        }
    }

    private void OnResetButtonClicked()
    {
        if (currentSettings != null)
        {
            // Сброс аудио настроек
            currentSettings.masterVolume = 1f;
            currentSettings.musicVolume = 1f;
            currentSettings.playersVolume = 1f;
            currentSettings.uiVolume = 1f;
            currentSettings.stepsVolume = 1f;
            currentSettings.environmentVolume = 1f;
            currentSettings.microphoneDevice = null;
            currentSettings.microphoneSensitivity = 10f;

            ApplyAllSettings();
            UpdateUI();
        }
    }

    private void OnApplyButtonClicked()
    {
        if (currentSettings != null)
        {
            currentSettings.SaveSettings();
        }
    }
    
    private void OnTestMicrophoneButtonClicked()
    {
        if (isMonitoringMicrophone)
        {
            StopMicrophoneMonitoring();
        }
        else
        {
            StartMicrophoneMonitoring();
        }
    }
    #endregion
    
    #region Microphone Monitoring
    /// <summary>
    /// Начинает прослушивание микрофона
    /// </summary>
    private void StartMicrophoneMonitoring()
    {
        if (isMonitoringMicrophone) return;
        
        if (currentSettings == null || audioMixer == null)
        {
            Debug.LogWarning("[AudioSettingsUI] Не удалось начать прослушивание микрофона: настройки или микшер не инициализированы");
            return;
        }
        
        // Получаем текущее значение громкости Music из микшера
        if (audioMixer.GetFloat("Music", out float currentMusicDb))
        {
            // Конвертируем из децибел в линейное значение
            savedMusicVolume = DecibelsToLinear(currentMusicDb);
        }
        else
        {
            // Если не удалось получить, используем значение из настроек
            savedMusicVolume = currentSettings.musicVolume;
        }
        
        // Получаем устройство микрофона
        string deviceName = currentSettings.microphoneDevice;
        if (string.IsNullOrEmpty(deviceName))
        {
            deviceName = null; // Используем устройство по умолчанию
        }
        
        // Проверяем доступность микрофона
        if (deviceName != null && System.Array.IndexOf(Microphone.devices, deviceName) < 0)
        {
            Debug.LogWarning($"[AudioSettingsUI] Микрофон '{deviceName}' не найден, используется устройство по умолчанию");
            deviceName = null;
        }
        
        // Начинаем запись с микрофона
        int frequency = 44100; // Частота дискретизации
        microphoneClip = Microphone.Start(deviceName, true, 10, frequency);
        
        if (microphoneClip == null)
        {
            Debug.LogError("[AudioSettingsUI] Не удалось начать запись с микрофона");
            return;
        }
        
        isMonitoringMicrophone = true;
        
        // Показываем индикатор и запускаем анимацию
        if (microphoneIndicatorImage != null)
        {
            microphoneIndicatorImage.gameObject.SetActive(true);
            // Устанавливаем начальную прозрачность
            Color color = microphoneIndicatorImage.color;
            color.a = 0f;
            microphoneIndicatorImage.color = color;
            
            // Запускаем анимацию прозрачности
            if (indicatorFadeCoroutine != null)
            {
                StopCoroutine(indicatorFadeCoroutine);
            }
            indicatorFadeCoroutine = StartCoroutine(AnimateMicrophoneIndicator());
        }
        
        // Плавно уменьшаем громкость Music до 0
        if (musicVolumeCoroutine != null)
        {
            StopCoroutine(musicVolumeCoroutine);
        }
        musicVolumeCoroutine = StartCoroutine(FadeMusicVolume(savedMusicVolume, 0f, 0.5f));
        
        // Запускаем корутину для ожидания начала записи и воспроизведения
        StartCoroutine(WaitForMicrophoneAndPlay(deviceName));
        
        Debug.Log("[AudioSettingsUI] Начато прослушивание микрофона");
    }
    
    /// <summary>
    /// Останавливает прослушивание микрофона
    /// </summary>
    private void StopMicrophoneMonitoring()
    {
        if (!isMonitoringMicrophone) return;
        
        // Останавливаем воспроизведение
        if (microphoneAudioSource != null && microphoneAudioSource.isPlaying)
        {
            microphoneAudioSource.Stop();
        }
        
        // Останавливаем запись с микрофона
        string deviceName = currentSettings != null ? currentSettings.microphoneDevice : null;
        if (string.IsNullOrEmpty(deviceName))
        {
            deviceName = null;
        }
        
        if (Microphone.IsRecording(deviceName))
        {
            Microphone.End(deviceName);
        }
        
        // Удаляем AudioClip
        if (microphoneClip != null)
        {
            Destroy(microphoneClip);
            microphoneClip = null;
        }
        
        isMonitoringMicrophone = false;
        
        // Останавливаем анимацию индикатора и скрываем его
        if (indicatorFadeCoroutine != null)
        {
            StopCoroutine(indicatorFadeCoroutine);
            indicatorFadeCoroutine = null;
        }
        
        if (microphoneIndicatorImage != null)
        {
            // Плавно скрываем индикатор
            StartCoroutine(FadeOutIndicator());
        }
        
        // Восстанавливаем громкость Music из PlayerPrefs
        float targetMusicVolume = PlayerPrefs.GetFloat("MusicVolume", savedMusicVolume);
        
        // Плавно увеличиваем громкость Music до сохраненного значения
        if (musicVolumeCoroutine != null)
        {
            StopCoroutine(musicVolumeCoroutine);
        }
        musicVolumeCoroutine = StartCoroutine(FadeMusicVolume(0f, targetMusicVolume, 0.5f));
        
        Debug.Log("[AudioSettingsUI] Остановлено прослушивание микрофона");
    }
    
    /// <summary>
    /// Плавно изменяет громкость Music в микшере
    /// </summary>
    private System.Collections.IEnumerator FadeMusicVolume(float fromVolume, float toVolume, float duration)
    {
        if (audioMixer == null) yield break;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float currentVolume = Mathf.Lerp(fromVolume, toVolume, t);
            
            // Конвертируем в децибелы и применяем
            float db = currentVolume > 0 ? 20f * Mathf.Log10(currentVolume) : -80f;
            audioMixer.SetFloat("Music", db);
            
            yield return null;
        }
        
        // Убеждаемся, что финальное значение установлено
        float finalDb = toVolume > 0 ? 20f * Mathf.Log10(toVolume) : -80f;
        audioMixer.SetFloat("Music", finalDb);
        
        musicVolumeCoroutine = null;
    }
    
    /// <summary>
    /// Ожидает начала записи микрофона и начинает воспроизведение
    /// </summary>
    private System.Collections.IEnumerator WaitForMicrophoneAndPlay(string deviceName)
    {
        // Ждем, пока микрофон начнет запись (максимум 1 секунда)
        float timeout = 1f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            if (Microphone.GetPosition(deviceName) > 0)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Воспроизводим запись
        if (microphoneAudioSource != null && microphoneClip != null && isMonitoringMicrophone)
        {
            microphoneAudioSource.clip = microphoneClip;
            ApplyMicrophoneSensitivity(); // Применяем чувствительность перед воспроизведением
            microphoneAudioSource.Play();
        }
    }
    
    /// <summary>
    /// Применяет чувствительность микрофона к громкости AudioSource
    /// </summary>
    private void ApplyMicrophoneSensitivity()
    {
        if (microphoneAudioSource == null || currentSettings == null) return;
        
        // Нормализуем чувствительность (0.0001-100) в диапазон 0-1 для volume
        // Минимальное значение 0.0001 соответствует почти нулевой громкости
        // Максимальное значение 100 соответствует максимальной громкости (1.0)
        float sensitivity = currentSettings.microphoneSensitivity;
        float normalizedSensitivity = Mathf.Clamp01(sensitivity / 100f);
        
        // Применяем к громкости AudioSource
        microphoneAudioSource.volume = normalizedSensitivity;
        
        Debug.Log($"[AudioSettingsUI] Применена чувствительность микрофона: {sensitivity:F2} -> volume: {normalizedSensitivity:F4}");
    }
    
    /// <summary>
    /// Анимирует индикатор микрофона (плавное изменение прозрачности)
    /// </summary>
    private System.Collections.IEnumerator AnimateMicrophoneIndicator()
    {
        if (microphoneIndicatorImage == null) yield break;
        
        float fadeSpeed = indicatorFadeSpeed > 0 ? indicatorFadeSpeed : 1f;
        
        while (isMonitoringMicrophone)
        {
            // Плавное появление (fade in)
            float elapsed = 0f;
            Color color = microphoneIndicatorImage.color;
            float startAlpha = color.a;
            
            while (elapsed < fadeSpeed && isMonitoringMicrophone)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeSpeed;
                color.a = Mathf.Lerp(startAlpha, 1f, t);
                microphoneIndicatorImage.color = color;
                yield return null;
            }
            
            if (!isMonitoringMicrophone) break;
            
            // Плавное затухание (fade out)
            elapsed = 0f;
            startAlpha = color.a;
            
            while (elapsed < fadeSpeed && isMonitoringMicrophone)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeSpeed;
                color.a = Mathf.Lerp(startAlpha, 0f, t);
                microphoneIndicatorImage.color = color;
                yield return null;
            }
        }
        
        indicatorFadeCoroutine = null;
    }
    
    /// <summary>
    /// Плавно скрывает индикатор микрофона
    /// </summary>
    private System.Collections.IEnumerator FadeOutIndicator()
    {
        if (microphoneIndicatorImage == null) yield break;
        
        Color color = microphoneIndicatorImage.color;
        float startAlpha = color.a;
        float fadeSpeed = indicatorFadeSpeed > 0 ? indicatorFadeSpeed : 1f;
        float elapsed = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeSpeed;
            color.a = Mathf.Lerp(startAlpha, 0f, t);
            microphoneIndicatorImage.color = color;
            yield return null;
        }
        
        // Скрываем Image после завершения анимации
        microphoneIndicatorImage.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Конвертирует децибелы в линейное значение
    /// </summary>
    private float DecibelsToLinear(float db)
    {
        return db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
    }
    #endregion
}
