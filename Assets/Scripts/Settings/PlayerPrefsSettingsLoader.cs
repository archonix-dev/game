using System.Collections.Generic;
using Game.Localization;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Единая точка инициализации всех сохраненных в PlayerPrefs настроек (звук, графика, чувствительность мыши, локализация).
/// Подвесьте скрипт на объект, который существует во всех сценах, чтобы гарантировать применение настроек до появления UI.
/// </summary>
[DefaultExecutionOrder(-500)]
public class PlayerPrefsSettingsLoader : MonoBehaviour
{
	[Header("Audio")]
	[Tooltip("AudioMixer, в который нужно применить сохраненные уровни громкости.")]
	[SerializeField] private AudioMixer audioMixer;

	[Header("Graphics")]
	[Tooltip("URP Asset, настройки которого нужно обновить из PlayerPrefs (опционально). Если не задан, берется текущий активный.")]
	[SerializeField] private UniversalRenderPipelineAsset overridePipelineAsset;
	[Tooltip("Применять сохраненное разрешение/частоту кадров. Отключите, если управление экраном выполняется где-то еще.")]
	[SerializeField] private bool applyDisplaySettings = true;

	[Header("Input / Controls")]
	[Tooltip("Необязательная ссылка на KeybindScript. Если не указана, будет найден в сцене.")]
	[SerializeField] private KeybindScript keybindScript;

	[Header("Localization")]
	[Tooltip("Явная ссылка на LocalizationManager. Если null, будет найден автоматически.")]
	[SerializeField] private LocalizationManager localizationManager;

	private AudioSettings cachedAudioSettings;
	private GraphicsSettings cachedGraphicsSettings;

	private void Awake()
	{
		LoadAudioPreferences();
		LoadGraphicsPreferences();
		LoadKeybindPreferences();
		LoadLocalizationPreferences();
	}

	private void LoadAudioPreferences()
	{
		if (audioMixer == null)
			return;

		cachedAudioSettings ??= new AudioSettings();
		cachedAudioSettings.LoadSettings();
		cachedAudioSettings.ApplySettings(audioMixer);
	}

	private void LoadGraphicsPreferences()
	{
		cachedGraphicsSettings ??= new GraphicsSettings();
		cachedGraphicsSettings.LoadSettings();

		if (overridePipelineAsset != null)
		{
			cachedGraphicsSettings.urpAsset = overridePipelineAsset;
		}

		cachedGraphicsSettings.ApplyURPSettings();
		cachedGraphicsSettings.ApplyQualitySettings();
		cachedGraphicsSettings.ApplyPostProcessSettings();

		if (!applyDisplaySettings)
			return;

		List<Resolution> availableResolutions = new List<Resolution>();
		if (Screen.resolutions != null && Screen.resolutions.Length > 0)
		{
			availableResolutions.AddRange(Screen.resolutions);
		}
		else
		{
			availableResolutions.Add(Screen.currentResolution);
		}

		cachedGraphicsSettings.ApplyDisplaySettings(availableResolutions);
	}

	private void LoadKeybindPreferences()
	{
		KeybindScript resolvedKeybind = keybindScript;
		if (resolvedKeybind == null)
		{
			resolvedKeybind = KeybindScript.Instance;
		}

		if (resolvedKeybind == null)
		{
			resolvedKeybind = FindObjectOfType<KeybindScript>(true);
		}

		resolvedKeybind?.LoadSettings();
	}

	private void LoadLocalizationPreferences()
	{
		string savedLanguage = PlayerPrefs.GetString("Localization.Language", string.Empty);
		if (string.IsNullOrEmpty(savedLanguage))
			return;

		LocalizationManager resolvedManager = localizationManager;
		if (resolvedManager == null)
		{
			resolvedManager = LocalizationManager.Instance;
		}
		if (resolvedManager == null)
		{
			resolvedManager = FindObjectOfType<LocalizationManager>(true);
		}

		resolvedManager?.SetLanguage(savedLanguage);
	}
}


