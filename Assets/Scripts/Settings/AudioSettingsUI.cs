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
    public Slider microphoneSensitivitySlider;
    public Text microphoneSensitivityLabel;

    [Header("Dropdowns")]
    public Dropdown microphoneDropdown;

    [Header("Buttons")]
    public Button resetButton;
    public Button applyButton;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    private AudioSettings currentSettings;

    private void Start()
    {
        InitializeSettings();
        InitializeUI();
        LoadCurrentSettings();
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

    private void OnMicrophoneSensitivityChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.microphoneSensitivity = Mathf.Clamp(value, 0.0001f, 100f);
            UpdateMicrophoneSensitivityLabel();
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
    #endregion
}
