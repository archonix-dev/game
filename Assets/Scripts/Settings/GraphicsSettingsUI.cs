using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Enum для MSAA Quality
public enum MsaaQuality
{
    Disabled = 1,
    _2x = 2,
    _4x = 4,
    _8x = 8
}

// Enum для Upscaling Filter
public enum UpscalingFilterSelection
{
    Linear = 0,
    Point = 1,
    FSR = 2
}

[System.Serializable]
public class GraphicsSettings
{
    [Header("URP Asset Settings")]
    public UniversalRenderPipelineAsset urpAsset;
    
    [Header("MSAA Settings")]
    public MsaaQuality msaaQuality = MsaaQuality.Disabled;
    
    [Header("Upscaling Filter")]
    public UpscalingFilterSelection upscalingFilter = UpscalingFilterSelection.Linear;
    
    [Header("Main Light Shadows")]
    public bool mainLightShadowsSupported = true;
    public UnityEngine.Rendering.Universal.ShadowResolution mainLightShadowmapResolution = UnityEngine.Rendering.Universal.ShadowResolution._2048;
    
    [Header("HDR Support")]
    public bool supportsHDR = true;
    
    [Header("Render Scale")]
    [Range(0.1f, 2f)]
    public float renderScale = 1f;
    
    [Header("Quality Settings")]
    public bool vsyncEnabled = true;
    public int globalTextureMipmapLimit = 0;

	[Header("Display Settings")]
	public int resolutionIndex = 0; // index in filtered resolutions list
	public int targetFrameRate = 60;
	public bool fpsMatchMonitor = false;
	public FullScreenMode screenMode = FullScreenMode.Windowed;
    
    [Header("Post Processing")]
    public bool postProcessDataEnabled = true;

    private UniversalRenderPipelineAsset ResolveActiveUrpAsset()
    {
        if (urpAsset != null) return urpAsset;
        var current = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (current != null) urpAsset = current;
        return urpAsset;
    }

    private void ApplyMainLightShadowToggle()
    {
        var sun = RenderSettings.sun;
        if (sun == null)
        {
            // Fallback: try find any directional light
            var anyDir = GameObject.FindObjectsOfType<Light>();
            for (int i = 0; i < anyDir.Length; i++)
            {
                if (anyDir[i].type == LightType.Directional) { sun = anyDir[i]; break; }
            }
        }
        if (sun == null) return;

        bool assetAllows = urpAsset != null && urpAsset.supportsMainLightShadows;
        if (mainLightShadowsSupported && assetAllows)
        {
            sun.shadows = LightShadows.Soft;
        }
        else
        {
            sun.shadows = LightShadows.None;
        }
    }

    // Применение настроек к URP Asset
    public void ApplyURPSettings()
    {
        urpAsset = ResolveActiveUrpAsset();
        if (urpAsset == null) 
        {
            return;
        }

        try
        {
            // Публичные свойства URP Asset
            urpAsset.msaaSampleCount = Mathf.Max(1, (int)msaaQuality);
            urpAsset.upscalingFilter = (UnityEngine.Rendering.Universal.UpscalingFilterSelection)upscalingFilter;
            // Нельзя менять supportsMainLightShadows в рантайме (read-only); вместо этого переключаем тени на основном Directional Light
            urpAsset.mainLightShadowmapResolution = (int)mainLightShadowmapResolution;
            urpAsset.supportsHDR = supportsHDR;
            urpAsset.renderScale = Mathf.Clamp(renderScale, 0.1f, 2f);

            // На случай если quality уровни используют разные RP ассеты, переприсваиваем текущий, чтобы форсировать обновление
            QualitySettings.renderPipeline = urpAsset;

            // Применяем тени на уровне источника света
            ApplyMainLightShadowToggle();
        }
        catch (System.Exception e)
        {
        }
    }

    // Применение настроек качества
    public void ApplyQualitySettings()
    {
        // VSync
        QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;
        
        // Texture Mipmap Limit
        QualitySettings.globalTextureMipmapLimit = globalTextureMipmapLimit;
    }

	// Применение настроек экрана (разрешение и FPS)
	public void ApplyDisplaySettings(System.Collections.Generic.List<Resolution> filteredResolutions)
	{
		if (filteredResolutions != null && filteredResolutions.Count > 0)
		{
			int clampedIndex = Mathf.Clamp(resolutionIndex, 0, filteredResolutions.Count - 1);
			var res = filteredResolutions[clampedIndex];
			Screen.SetResolution(res.width, res.height, screenMode, 0);
		}

		if (fpsMatchMonitor)
		{
			int monitorHz = 0;
#if UNITY_2022_2_OR_NEWER
			monitorHz = (int)Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
#else
			monitorHz = Screen.currentResolution.refreshRate;
#endif
			if (monitorHz <= 0) monitorHz = 60;
			Application.targetFrameRate = monitorHz;
		}
		else
		{
			Application.targetFrameRate = Mathf.Max(0, targetFrameRate);
		}
	}

    // Применение настроек Post Processing
    public void ApplyPostProcessSettings()
    {
        // Управляем пост-обработкой и HDR на всех активных камерах
        var cameras = GameObject.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderPostProcessing = postProcessDataEnabled;
            }
            cam.allowHDR = supportsHDR;
        }
    }

    // Загрузка настроек из PlayerPrefs
    public void LoadSettings()
    {
        msaaQuality = (MsaaQuality)Mathf.Clamp(PlayerPrefs.GetInt("MSAAQuality", (int)MsaaQuality.Disabled), (int)MsaaQuality.Disabled, (int)MsaaQuality._8x);
        upscalingFilter = (UpscalingFilterSelection)Mathf.Clamp(PlayerPrefs.GetInt("UpscalingFilter", (int)UpscalingFilterSelection.Linear), 0, 2);
        mainLightShadowsSupported = PlayerPrefs.GetInt("MainLightShadowsSupported", 1) == 1;
        mainLightShadowmapResolution = (UnityEngine.Rendering.Universal.ShadowResolution)Mathf.Clamp(PlayerPrefs.GetInt("MainLightShadowmapResolution", (int)UnityEngine.Rendering.Universal.ShadowResolution._2048), (int)UnityEngine.Rendering.Universal.ShadowResolution._256, (int)UnityEngine.Rendering.Universal.ShadowResolution._4096);
        supportsHDR = PlayerPrefs.GetInt("SupportsHDR", 1) == 1;
        renderScale = Mathf.Clamp(PlayerPrefs.GetFloat("RenderScale", 1f), 0.1f, 2f);
        vsyncEnabled = PlayerPrefs.GetInt("VSyncEnabled", 1) == 1;
        globalTextureMipmapLimit = Mathf.Clamp(PlayerPrefs.GetInt("GlobalTextureMipmapLimit", 0), 0, 3);
        postProcessDataEnabled = PlayerPrefs.GetInt("PostProcessDataEnabled", 1) == 1;
        resolutionIndex = Mathf.Max(0, PlayerPrefs.GetInt("ResolutionIndex", 0));
        targetFrameRate = Mathf.Max(0, PlayerPrefs.GetInt("TargetFrameRate", 60));
		fpsMatchMonitor = PlayerPrefs.GetInt("FPSMatchMonitor", 0) == 1;
		// При первом запуске (если нет сохраненного значения) устанавливаем полноэкранный режим по умолчанию
		int defaultScreenMode = (int)FullScreenMode.ExclusiveFullScreen;
		if (PlayerPrefs.HasKey("ScreenMode"))
		{
			screenMode = (FullScreenMode)Mathf.Clamp(PlayerPrefs.GetInt("ScreenMode"), (int)FullScreenMode.Windowed, (int)FullScreenMode.FullScreenWindow);
		}
		else
		{
			screenMode = (FullScreenMode)defaultScreenMode;
		}
    }

    // Сохранение настроек в PlayerPrefs
    public void SaveSettings()
    {
        PlayerPrefs.SetInt("MSAAQuality", (int)msaaQuality);
        PlayerPrefs.SetInt("UpscalingFilter", (int)upscalingFilter);
        PlayerPrefs.SetInt("MainLightShadowsSupported", mainLightShadowsSupported ? 1 : 0);
        PlayerPrefs.SetInt("MainLightShadowmapResolution", (int)mainLightShadowmapResolution);
        PlayerPrefs.SetInt("SupportsHDR", supportsHDR ? 1 : 0);
        PlayerPrefs.SetFloat("RenderScale", renderScale);
        PlayerPrefs.SetInt("VSyncEnabled", vsyncEnabled ? 1 : 0);
        PlayerPrefs.SetInt("GlobalTextureMipmapLimit", globalTextureMipmapLimit);
        PlayerPrefs.SetInt("PostProcessDataEnabled", postProcessDataEnabled ? 1 : 0);
		PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
		PlayerPrefs.SetInt("TargetFrameRate", targetFrameRate);
		PlayerPrefs.SetInt("FPSMatchMonitor", fpsMatchMonitor ? 1 : 0);
		PlayerPrefs.SetInt("ScreenMode", (int)screenMode);
        PlayerPrefs.Save();
    }
}

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("URP References")]
    public UniversalRenderPipelineAsset urpPipelineAsset; // assign in Inspector
    public UniversalRendererData universalRendererData;   // assign in Inspector (kept for future needs)

    [Header("Dropdowns")]
    public Dropdown msaaDropdown;
    public Dropdown upscalingFilterDropdown;
    public Dropdown shadowResolutionDropdown;
    public Dropdown textureMipmapLimitDropdown;
	public Dropdown resolutionDropdown;
	public Dropdown screenModeDropdown;

    [Header("Toggles")]
    public Toggle mainLightShadowsToggle;
    public Toggle hdrToggle;
    public Toggle vsyncToggle;
    public Toggle postProcessToggle;
	public Toggle fpsUnderMonitorToggle;

    [Header("Sliders")]
    public Slider renderScaleSlider;
    public Text renderScaleLabel;

    [Header("Buttons")]
    public Button resetButton;
    public Button applyButton;

	[Header("Inputs")]
	public InputField fpsInputField;

    private GraphicsSettings currentSettings;
	private System.Collections.Generic.List<Resolution> filteredResolutions = new System.Collections.Generic.List<Resolution>();

    private void Start()
    {
        InitializeSettings();
        InitializeUI();
        LoadCurrentSettings();
    }

    private void InitializeUI()
    {
        // Настройка MSAA Dropdown
        if (msaaDropdown != null)
        {
            msaaDropdown.ClearOptions();
            msaaDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Disabled (1x)",
                "2x MSAA",
                "4x MSAA",
                "8x MSAA"
            });
            msaaDropdown.onValueChanged.AddListener(OnMSAAChanged);
        }

        // Настройка Upscaling Filter Dropdown
        if (upscalingFilterDropdown != null)
        {
            upscalingFilterDropdown.ClearOptions();
            upscalingFilterDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Linear",
                "Point",
                "FSR"
            });
            upscalingFilterDropdown.onValueChanged.AddListener(OnUpscalingFilterChanged);
        }

        // Настройка Shadow Resolution Dropdown
        if (shadowResolutionDropdown != null)
        {
            shadowResolutionDropdown.ClearOptions();
            shadowResolutionDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "256",
                "512",
                "1024",
                "2048",
                "4096"
            });
            shadowResolutionDropdown.onValueChanged.AddListener(OnShadowResolutionChanged);
        }

        // Настройка Texture Mipmap Limit Dropdown
        if (textureMipmapLimitDropdown != null)
        {
            textureMipmapLimitDropdown.ClearOptions();
            textureMipmapLimitDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "No Limit",
                "Limit to 1",
                "Limit to 2",
                "Limit to 3"
            });
            textureMipmapLimitDropdown.onValueChanged.AddListener(OnTextureMipmapLimitChanged);
        }

		// Настройка Resolution Dropdown
		if (resolutionDropdown != null)
		{
			BuildResolutionOptions();
			resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
		}

		// Настройка Screen Mode Dropdown
		if (screenModeDropdown != null)
		{
			screenModeDropdown.ClearOptions();
			screenModeDropdown.AddOptions(new System.Collections.Generic.List<string>
			{
				"В окне",
				"Полноэкранный",
				"В окне без рамки"
			});
			screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
		}

        // Настройка Toggles
        if (mainLightShadowsToggle != null)
            mainLightShadowsToggle.onValueChanged.AddListener(OnMainLightShadowsChanged);

        if (hdrToggle != null)
            hdrToggle.onValueChanged.AddListener(OnHDRChanged);

        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);

        if (postProcessToggle != null)
            postProcessToggle.onValueChanged.AddListener(OnPostProcessChanged);

		if (fpsUnderMonitorToggle != null)
			fpsUnderMonitorToggle.onValueChanged.AddListener(OnFpsUnderMonitorChanged);

        // Настройка Render Scale Slider
        if (renderScaleSlider != null)
        {
            renderScaleSlider.minValue = 0.1f;
            renderScaleSlider.maxValue = 2f;
            renderScaleSlider.onValueChanged.AddListener(OnRenderScaleChanged);
        }

		// Настройка FPS InputField
		if (fpsInputField != null)
		{
			fpsInputField.onEndEdit.AddListener(OnFpsInputEndEdit);
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
            currentSettings = new GraphicsSettings();
        }
        
        // Загружаем сохраненные настройки
        currentSettings.LoadSettings();

        // Если URP Asset назначен в инспекторе UI — используем его явно
        if (urpPipelineAsset != null)
        {
            currentSettings.urpAsset = urpPipelineAsset;
        }
        
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
        if (currentSettings != null)
        {
            currentSettings.ApplyURPSettings();
            currentSettings.ApplyQualitySettings();
			currentSettings.ApplyPostProcessSettings();
			currentSettings.ApplyDisplaySettings(filteredResolutions);
        }
    }

    private void UpdateUI()
    {
        if (currentSettings == null) return;

        // Обновляем Dropdowns
        if (msaaDropdown != null)
        {
            // Маппинг enum значений на индексы dropdown
            int msaaIndex = currentSettings.msaaQuality switch
            {
                MsaaQuality.Disabled => 0,  // 1 -> 0
                MsaaQuality._2x => 1,       // 2 -> 1
                MsaaQuality._4x => 2,       // 4 -> 2
                MsaaQuality._8x => 3,       // 8 -> 3
                _ => 0
            };
            msaaDropdown.value = msaaIndex;
        }

        if (upscalingFilterDropdown != null)
            upscalingFilterDropdown.value = (int)currentSettings.upscalingFilter;

        if (shadowResolutionDropdown != null)
        {
            // Маппинг enum значений на индексы dropdown
            int shadowIndex = currentSettings.mainLightShadowmapResolution switch
            {
                UnityEngine.Rendering.Universal.ShadowResolution._256 => 0,
                UnityEngine.Rendering.Universal.ShadowResolution._512 => 1,
                UnityEngine.Rendering.Universal.ShadowResolution._1024 => 2,
                UnityEngine.Rendering.Universal.ShadowResolution._2048 => 3,
                UnityEngine.Rendering.Universal.ShadowResolution._4096 => 4,
                _ => 3
            };
            shadowResolutionDropdown.value = shadowIndex;
        }

        if (textureMipmapLimitDropdown != null)
            textureMipmapLimitDropdown.value = currentSettings.globalTextureMipmapLimit;

		if (resolutionDropdown != null)
		{
			if (filteredResolutions == null || filteredResolutions.Count == 0)
			{
				BuildResolutionOptions();
			}
			resolutionDropdown.value = Mathf.Clamp(currentSettings.resolutionIndex, 0, Mathf.Max(0, resolutionDropdown.options.Count - 1));
		}

		if (screenModeDropdown != null)
		{
			// Маппинг FullScreenMode на индексы dropdown
			int screenModeIndex = currentSettings.screenMode switch
			{
				FullScreenMode.Windowed => 0,
				FullScreenMode.ExclusiveFullScreen => 1,
				FullScreenMode.FullScreenWindow => 2,
				_ => 0
			};
			screenModeDropdown.value = screenModeIndex;
		}

        // Обновляем Toggles
        if (mainLightShadowsToggle != null)
            mainLightShadowsToggle.isOn = currentSettings.mainLightShadowsSupported;

        if (hdrToggle != null)
            hdrToggle.isOn = currentSettings.supportsHDR;

        if (vsyncToggle != null)
            vsyncToggle.isOn = currentSettings.vsyncEnabled;

        if (postProcessToggle != null)
            postProcessToggle.isOn = currentSettings.postProcessDataEnabled;

		if (fpsUnderMonitorToggle != null)
			fpsUnderMonitorToggle.isOn = currentSettings.fpsMatchMonitor;

        // Обновляем Slider
        if (renderScaleSlider != null)
            renderScaleSlider.value = currentSettings.renderScale;

		if (fpsInputField != null)
		{
			fpsInputField.text = Mathf.Max(0, currentSettings.targetFrameRate).ToString();
			fpsInputField.interactable = !currentSettings.fpsMatchMonitor;
		}

        // Обновляем лейбл
        UpdateRenderScaleLabel();
    }

    private void UpdateRenderScaleLabel()
    {
        if (renderScaleLabel != null && currentSettings != null)
        {
            renderScaleLabel.text = $"{currentSettings.renderScale:F1}x";
        }
    }

	private void BuildResolutionOptions()
	{
		filteredResolutions.Clear();
		var all = Screen.resolutions;
		// Фильтруем по уникальной паре width x height, оставляя наибольшую частоту обновления для каждой
		for (int i = 0; i < all.Length; i++)
		{
			bool exists = false;
			for (int j = 0; j < filteredResolutions.Count; j++)
			{
				if (filteredResolutions[j].width == all[i].width && filteredResolutions[j].height == all[i].height)
				{
					exists = true;
					// заменяем, если у текущей запись частота ниже (на старых Unity только refreshRate)
#if UNITY_2022_2_OR_NEWER
					float curHz = (float)filteredResolutions[j].refreshRateRatio.value;
					float newHz = (float)all[i].refreshRateRatio.value;
					if (newHz > curHz) filteredResolutions[j] = all[i];
#else
					if (all[i].refreshRate > filteredResolutions[j].refreshRate) filteredResolutions[j] = all[i];
#endif
					break;
				}
			}
			if (!exists) filteredResolutions.Add(all[i]);
		}

		// Сортируем от меньшего к большему
		filteredResolutions.Sort((a, b) =>
		{
			int byW = a.width.CompareTo(b.width);
			if (byW != 0) return byW;
			return a.height.CompareTo(b.height);
		});

		var options = new System.Collections.Generic.List<string>();
		for (int i = 0; i < filteredResolutions.Count; i++)
		{
#if UNITY_2022_2_OR_NEWER
			int hz = (int)Mathf.Round((float)filteredResolutions[i].refreshRateRatio.value);
#else
			int hz = filteredResolutions[i].refreshRate;
#endif
			options.Add($"{filteredResolutions[i].width}x{filteredResolutions[i].height} @{hz}Hz");
		}
		resolutionDropdown.ClearOptions();
		resolutionDropdown.AddOptions(options);
	}

    #region Event Handlers
    private void OnMSAAChanged(int value)
    {
        if (currentSettings != null)
        {
            // Маппинг индексов dropdown на enum значения
            MsaaQuality msaaQuality = value switch
            {
                0 => MsaaQuality.Disabled,  // 0 -> 1
                1 => MsaaQuality._2x,       // 1 -> 2
                2 => MsaaQuality._4x,       // 2 -> 4
                3 => MsaaQuality._8x,       // 3 -> 8
                _ => MsaaQuality.Disabled
            };
            currentSettings.msaaQuality = msaaQuality;
            currentSettings.ApplyURPSettings();
        }
    }

    private void OnUpscalingFilterChanged(int value)
    {
        if (currentSettings != null)
        {
            currentSettings.upscalingFilter = (UpscalingFilterSelection)value;
            currentSettings.ApplyURPSettings();
        }
    }

    private void OnShadowResolutionChanged(int value)
    {
        if (currentSettings != null)
        {
            // Маппинг индексов dropdown на enum значения
            UnityEngine.Rendering.Universal.ShadowResolution shadowResolution = value switch
            {
                0 => UnityEngine.Rendering.Universal.ShadowResolution._256,
                1 => UnityEngine.Rendering.Universal.ShadowResolution._512,
                2 => UnityEngine.Rendering.Universal.ShadowResolution._1024,
                3 => UnityEngine.Rendering.Universal.ShadowResolution._2048,
                4 => UnityEngine.Rendering.Universal.ShadowResolution._4096,
                _ => UnityEngine.Rendering.Universal.ShadowResolution._2048
            };
            currentSettings.mainLightShadowmapResolution = shadowResolution;
            currentSettings.ApplyURPSettings();
        }
    }

    private void OnTextureMipmapLimitChanged(int value)
    {
        if (currentSettings != null)
        {
            currentSettings.globalTextureMipmapLimit = Mathf.Clamp(value, 0, 3);
            currentSettings.ApplyQualitySettings();
        }
    }

    private void OnMainLightShadowsChanged(bool value)
    {
        if (currentSettings != null)
        {
            currentSettings.mainLightShadowsSupported = value;
            currentSettings.ApplyURPSettings();
        }
    }

    private void OnHDRChanged(bool value)
    {
        if (currentSettings != null)
        {
            currentSettings.supportsHDR = value;
            currentSettings.ApplyURPSettings();
        }
    }

    private void OnVSyncChanged(bool value)
    {
        if (currentSettings != null)
        {
            currentSettings.vsyncEnabled = value;
            currentSettings.ApplyQualitySettings();
        }
    }

    private void OnPostProcessChanged(bool value)
    {
        if (currentSettings != null)
        {
            currentSettings.postProcessDataEnabled = value;
            currentSettings.ApplyPostProcessSettings();
        }
    }

    private void OnRenderScaleChanged(float value)
    {
        if (currentSettings != null)
        {
            currentSettings.renderScale = Mathf.Clamp(value, 0.1f, 2f);
            currentSettings.ApplyURPSettings();
        }
        UpdateRenderScaleLabel();
    }

	private void OnResolutionChanged(int value)
	{
		if (currentSettings != null)
		{
			currentSettings.resolutionIndex = Mathf.Clamp(value, 0, Mathf.Max(0, (filteredResolutions?.Count ?? 1) - 1));
			currentSettings.ApplyDisplaySettings(filteredResolutions);
		}
	}

	private void OnFpsInputEndEdit(string value)
	{
		if (currentSettings == null) return;
		int parsed;
		if (!int.TryParse(value, out parsed))
		{
			parsed = currentSettings.targetFrameRate;
		}
		parsed = Mathf.Max(0, parsed);
		currentSettings.targetFrameRate = parsed;
		if (fpsInputField != null) fpsInputField.text = parsed.ToString();
		currentSettings.ApplyDisplaySettings(filteredResolutions);
	}

	private void OnFpsUnderMonitorChanged(bool value)
	{
		if (currentSettings == null) return;
		currentSettings.fpsMatchMonitor = value;
		if (fpsInputField != null) fpsInputField.interactable = !value;
		currentSettings.ApplyDisplaySettings(filteredResolutions);
	}

	private void OnScreenModeChanged(int value)
	{
		if (currentSettings == null) return;
		// Маппинг индексов dropdown на FullScreenMode
		FullScreenMode mode = value switch
		{
			0 => FullScreenMode.Windowed,
			1 => FullScreenMode.ExclusiveFullScreen,
			2 => FullScreenMode.FullScreenWindow,
			_ => FullScreenMode.Windowed
		};
		currentSettings.screenMode = mode;
		currentSettings.ApplyDisplaySettings(filteredResolutions);
		// Немедленно сохраняем изменение режима экрана в PlayerPrefs
		PlayerPrefs.SetInt("ScreenMode", (int)mode);
		PlayerPrefs.Save();
	}

    private void OnResetButtonClicked()
    {
        if (currentSettings != null)
        {
            // Сброс графических настроек
            currentSettings.msaaQuality = MsaaQuality.Disabled;
            currentSettings.upscalingFilter = UpscalingFilterSelection.Linear;
            currentSettings.mainLightShadowsSupported = true;
            currentSettings.mainLightShadowmapResolution = UnityEngine.Rendering.Universal.ShadowResolution._2048;
            currentSettings.supportsHDR = true;
            currentSettings.renderScale = 1f;
            currentSettings.vsyncEnabled = true;
            currentSettings.globalTextureMipmapLimit = 0;
            currentSettings.postProcessDataEnabled = true;

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
