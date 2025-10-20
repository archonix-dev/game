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
            Debug.LogWarning("AudioMixer is not assigned in AudioSettings!");
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
            
            if (success)
            {
                Debug.Log($"Applied audio settings - Master: {masterVolume:F2}, Music: {musicVolume:F2}, Players: {playersVolume:F2}, UI: {uiVolume:F2}, Steps: {stepsVolume:F2}");
            }
            else
            {
                Debug.LogWarning("Some audio mixer parameters could not be set. Check if the parameter names match in the AudioMixer.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying audio settings: {e.Message}");
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
    }

    // Сохранение настроек в PlayerPrefs
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("PlayersVolume", playersVolume);
        PlayerPrefs.SetFloat("UIVolume", uiVolume);
        PlayerPrefs.SetFloat("StepsVolume", stepsVolume);
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
            Debug.Log("Audio settings applied successfully!");
        }
        else if (audioMixer == null)
        {
            Debug.LogWarning("AudioMixer is not assigned in AudioSettingsUI!");
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

            ApplyAllSettings();
            UpdateUI();
            Debug.Log("Audio settings reset to defaults!");
        }
    }

    private void OnApplyButtonClicked()
    {
        if (currentSettings != null)
        {
            currentSettings.SaveSettings();
            Debug.Log("Audio settings saved successfully!");
        }
    }
    #endregion
}
