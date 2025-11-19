using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;

public class VoiceWaveVisualizer : NetworkBehaviour
{
	public AudioSource microphoneSource; // проигрывает голос с микрофона
	
	[Header("Line Points")]
	public Transform startPoint; // первая точка
	public Transform endPoint;   // вторая точка
	
	[Header("Target Renderers")]
	[Tooltip("Необязательно: SpriteRenderer, который нужно окрасить в выбранный игроком цвет")]
	public SpriteRenderer targetSpriteRenderer;
	[Tooltip("Применять ли выбранный цвет игрока при старте")]
	public bool applySelectedColorOnStart = true;
	
	[Header("Hierarchy")]
	[Tooltip("Родитель для LineRenderer. Если указан, линия будет создана дочерним объектом здесь.")]
	public Transform lineParent;
	
	[Header("Wave Settings")]
	[Tooltip("Количество точек LineRenderer")]
	[Min(8)]
	public int linePoints = 64;
	[Tooltip("Минимальная амплитуда волны")]
	[Min(0f)]
	public float minAmplitude = 0f;
	[Tooltip("Максимальная амплитуда волны при громком голосе")]
	[Min(0f)]
	public float maxAmplitude = 0.15f;
	[Tooltip("Минимальное число волн на длине линии")]
	[Min(0f)]
	public int minWaves = 1;
	[Tooltip("Максимальное число волн на длине линии")]
	[Min(1)]
	public int maxWaves = 12;
	[Tooltip("Мин. длина волны (м) — громко => короче")]
	[Min(0.01f)]
	public float minWavelength = 0.3f;
	[Tooltip("Макс. длина волны (м) — тихо => длиннее")]
	[Min(0.05f)]
	public float maxWavelength = 2.0f;
	[Tooltip("Скорость движения волны вдоль линии (м/с)")]
	public float waveSpeed = 1.2f;
	[Tooltip("Толщина линии")]
	public float lineWidth = 0.03f;
	
	[Header("Input")]
	[Tooltip("Кнопка разговора")]
	public KeyCode talkKey = KeyCode.LeftAlt;
	
	[Header("Microphone")]
	[Tooltip("Частота дискретизации для микрофона (Гц)")]
	public int micSampleRate = 44100;
	
	[Header("Loudness Detection")]
	[Tooltip("Размер буфера для оценки громкости")]
	[Min(64)]
	public int amplitudeSampleSize = 1024;
	[Tooltip("Коэффициент чувствительности громкости (увеличьте если слишком тихо)")]
	[Min(0.0001f)]
	public float amplitudeSensitivity = 10f;
	[Tooltip("Сглаживание амплитуды")]
	[Range(0f, 1f)]
	public float amplitudeLerp = 0.2f;
	
	[Header("Voice Networking")]
	[Tooltip("Частота отправки пакетов голоса (секунды)")]
	[Min(0.0001f)]
	public float voiceSendInterval = 0.05f;
	[Tooltip("Максимальное количество сэмплов в одном пакете")]
	[Min(128)]
	public int maxSamplesPerPacket = 2048;
	[Tooltip("Минимальная амплитуда, при которой голос отправляется")]
	[Range(0f, 0.1f)]
	public float minVoiceAmplitude = 0.005f;
	
	[Header("Voice Playback")]
	[Tooltip("Усиление входящего голоса (увеличьте, если собеседника плохо слышно)")]
	[Range(0.1f, 4f)]
	public float remotePlaybackGain = 1.2f;
	[Tooltip("Максимальная длина буфера входящего голоса (сек)")]
	[Range(0.1f, 2f)]
	public float remoteBufferDuration = 0.6f;
	
	[Header("Frequency Classification")]
	[Tooltip("Размер спектра для FFT анализа")]
	[Min(64)]
	public int spectrumSize = 1024;
	[Tooltip("Граница НИЗКИХ частот (Гц)")]
	public float lowFreqMax = 250f;
	[Tooltip("Граница СРЕДНИХ частот (Гц)")]
	public float midFreqMax = 2000f;
	[Tooltip("Скорость поворота объекта в зависимости от частоты")]
	public float rotateLerpSpeed = 6f;
	public GameObject frequencyPivot; // поворачиваемый объект
	
	[Header("Status Display")]
	[Tooltip("TextMeshPro 3D для отображения статуса системы")]
	public TextMeshPro statusText;
	[Tooltip("Интервал обновления статуса (секунды)")]
	public float statusUpdateInterval = 3f;
	[Tooltip("Длительность анимации печати текста (секунды)")]
	public float typingAnimationDuration = 3f;
	
	private LineRenderer lineRenderer;
	private Material lineMaterial;
	private float[] amplitudeSamples;
	private float[] micChunkBuffer;
	private float[] spectrum;
	private float currentAmplitude;
	private float timePhase;
	private float waveScrollDistance; // расстояние прокрутки волны вдоль линии
	private bool micActive;
	private int currentSampleRate;
	private string currentMicrophoneDevice = null; // Текущее устройство микрофона
	private int lastMicSamplePosition = 0;
	private float voiceSendTimer = 0f;
	private byte[] voiceByteBuffer;
	private float lastRemoteSample = 0f;
	
	private Queue<float> remoteSampleQueue = new Queue<float>(8192);
	private readonly object remoteQueueLock = new object();
	private int remoteSampleRate = 0;
	private AudioClip remotePlaybackClip;
	
	// Статус системы
	private float statusUpdateTimer = 0f;
	private string currentStatusText = "";
	private Coroutine typingCoroutine;
	
	private static readonly string PrefR = "PlayerColor_R";
	private static readonly string PrefG = "PlayerColor_G";
	private static readonly string PrefB = "PlayerColor_B";
	private static readonly string PrefA = "PlayerColor_A";
	
	// Сетевая переменная для синхронизации состояния разговора Mirror
	[SyncVar(hook = nameof(OnTalkingStateChanged))]
	private bool isTalking = false;
	
	// Сетевая переменная для синхронизации амплитуды (для визуализации у других игроков) Mirror
	[SyncVar]
	private float networkAmplitude = 0f;
	
	// Цвет визуализатора, синхронизированный через сеть
	[SyncVar(hook = nameof(OnPlayerColorChanged))]
	private Color syncedPlayerColor = new Color(0.05f, 0.82f, 0.27f, 1f);
	
	private string playerName = "";
	
	// Цвет по умолчанию и текущий базовый цвет игрока
	private static readonly Color DefaultPlayerColor = new Color(0.05f, 0.82f, 0.27f, 1f);
	private Color playerBaseColor = DefaultPlayerColor;
	
	// Ссылка на PlayerController для определения позиции игрока
	private PlayerController playerController;
	
	public override void OnStartClient()
	{
		base.OnStartClient();
		
		// Настраиваем AudioSource в зависимости от владельца
		SetupAudioSource();
		
		// Применяем уже синхронизированный цвет
		ApplyPlayerColor(syncedPlayerColor, false);
		
		// Загружаем цвет и имя игрока из PlayerCustomizationStorage или LobbyPlayer (локально)
		StartCoroutine(LoadColorAndNameDelayed(false));
		
		// Обновляем видимость LineRenderer для владельца
		UpdateLineRendererVisibility();
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		
		// На сервере сразу применяем цвет из PlayerCustomizationStorage и синхронизируем его
		StartCoroutine(LoadColorAndNameDelayed(true));
	}
	
	/// <summary>
	/// Загружает цвет и имя игрока с небольшой задержкой
	/// </summary>
	private IEnumerator LoadColorAndNameDelayed(bool syncColorToNetwork)
	{
		// Ждем немного, чтобы PlayerCustomizationStorage успел обновиться
		// Для хоста может потребоваться больше времени
		float delay = NetworkServer.activeHost ? 0.3f : 0.1f;
		yield return new WaitForSeconds(delay);
		bool colorLoaded = LoadColorAndNameFromPlayerData(syncColorToNetwork);
		
		// Если цвет не загрузился, пробуем еще раз через некоторое время
		if (!colorLoaded)
		{
			yield return new WaitForSeconds(0.5f);
			LoadColorAndNameFromPlayerData(syncColorToNetwork);
		}
	}
	
	// Hook для изменения состояния разговора
	void OnTalkingStateChanged(bool oldValue, bool newValue)
	{
		// Обновляем визуализацию при изменении состояния разговора
		UpdateLineRendererVisibility();
	}

	// Hook Mirror для обновления цвета на клиентах
	void OnPlayerColorChanged(Color oldColor, Color newColor)
	{
		ApplyPlayerColor(newColor, false);
	}
	
	void Awake()
	{
		SetupLineRenderer();
		
		amplitudeSamples = new float[Mathf.Max(64, amplitudeSampleSize)];
		spectrum = new float[Mathf.Max(64, spectrumSize)];
		
		if (applySelectedColorOnStart)
		{
			ApplySelectedColorIfExists();
		}
		
		// Инициализируем статус
		if (statusText != null)
		{
			currentStatusText = GetStatusText();
			statusText.text = "";
			// Запускаем первую анимацию печати
			typingCoroutine = StartCoroutine(TypeText(currentStatusText));
		}
	}
	
	void Start()
	{
		// Если не в сети, настраиваем AudioSource локально
		if (netIdentity == null || netIdentity.netId == 0)
		{
			SetupAudioSource();
		}
		
		// Загружаем настройки микрофона из PlayerPrefs
		LoadMicrophoneSettings();
		
		// Загружаем цвет и имя игрока (если еще не загружено в OnStartClient)
		if (netIdentity == null || netIdentity.netId == 0)
		{
			LoadColorAndNameFromPlayerData();
		}
		
		// Находим PlayerController
		FindPlayerController();
	}

	private void LoadMicrophoneSettings()
	{
		// Загружаем устройство микрофона из PlayerPrefs
		string savedDevice = PlayerPrefs.GetString("MicrophoneDevice", null);
		if (string.IsNullOrEmpty(savedDevice))
		{
			currentMicrophoneDevice = null; // Используем устройство по умолчанию
		}
		else
		{
			// Проверяем, существует ли это устройство
			string[] devices = Microphone.devices;
			if (devices != null && System.Array.IndexOf(devices, savedDevice) >= 0)
			{
				currentMicrophoneDevice = savedDevice;
			}
			else
			{
				currentMicrophoneDevice = null; // Устройство не найдено, используем по умолчанию
			}
		}

		// Загружаем чувствительность микрофона из PlayerPrefs (если есть)
		float savedSensitivity = PlayerPrefs.GetFloat("MicrophoneSensitivity", -1f);
		if (savedSensitivity >= 0.0001f)
		{
			amplitudeSensitivity = savedSensitivity;
		}
	}
	
	// Загружает цвет и имя игрока только из PlayerCustomizationStorage
	bool LoadColorAndNameFromPlayerData(bool syncColorToNetwork = true)
	{
		Color playerColor = DefaultPlayerColor; // Цвет по умолчанию
		string playerNameToSet = "Player";
		bool colorFound = false;
		
		int connectionId = -1;
		bool isHost = NetworkServer.activeHost;
		
		if (netIdentity != null && netIdentity.netId != 0)
		{
			// Определяем connectionId для этого игрока
			if (netIdentity.connectionToClient != null)
			{
				connectionId = netIdentity.connectionToClient.connectionId;
			}
			else if (isHost && isOwned)
			{
				// Для хоста (когда он владелец объекта) connectionId = LocalConnectionId (0)
				connectionId = NetworkConnection.LocalConnectionId;
			}
			
			// Одинаковая логика для всех игроков: сначала через connectionId, затем через SteamID
			
			// Сначала пробуем найти через PlayerCustomizationStorage по connectionId
			if (!colorFound && connectionId >= 0)
			{
				if (PlayerCustomizationStorage.TryGetByConnectionId(connectionId, out var data))
				{
					playerColor = data.PlayerColor;
					playerNameToSet = string.IsNullOrEmpty(data.playerName) ? "Player" : data.playerName;
					colorFound = true;
					Debug.Log($"[VoiceWaveVisualizer] Найден цвет через connectionId {connectionId}: {playerColor}, имя: {playerNameToSet}");
				}
			}
			
			// Затем пробуем найти через SteamID (если доступен Steam API)
			if (!colorFound)
			{
				try
				{
					if (SteamAPI.IsSteamRunning())
					{
						ulong steamId = SteamUser.GetSteamID().m_SteamID;
						if (PlayerCustomizationStorage.TryGetBySteamId(steamId, out var dataBySteam))
						{
							playerColor = dataBySteam.PlayerColor;
							playerNameToSet = string.IsNullOrEmpty(dataBySteam.playerName) ? "Player" : dataBySteam.playerName;
							colorFound = true;
							Debug.Log($"[VoiceWaveVisualizer] Найден цвет через SteamID {steamId}: {playerColor}, имя: {playerNameToSet}");
						}
					}
				}
				catch
				{
					// Игнорируем ошибки Steam API
				}
			}
		}
		
		// Применяем цвет и имя
		ApplyPlayerColor(playerColor, syncColorToNetwork);
		SetPlayerName(playerNameToSet);
		
		// Отладочная информация (только для владельца)
		if (isOwned)
		{
			Debug.Log($"[VoiceWaveVisualizer] Владелец: Загружен цвет {playerColor}, имя {playerNameToSet}, connectionId={connectionId}, colorFound={colorFound}");
		}

		if (isServer && colorFound)
		{
			// Убеждаемся, что синхронизированное значение соответствует примененному
			syncedPlayerColor = playerColor;
		}

		return colorFound;
	}
	
	// Используем значения по умолчанию для цвета и имени (резервный метод)
	void LoadColorAndNameFromDefaults()
	{
		// Используем значения по умолчанию
		Color defaultColor = new Color(0.05f, 0.82f, 0.27f, 1f);
		ApplyPlayerColor(defaultColor);
		SetPlayerName("Player");
	}
	
	private void SetupAudioSource()
	{
		if (microphoneSource == null) return;
		
		// ВАЖНО: Для визуализации микрофон должен работать, но мы не должны слышать себя
		// Для владельца используем очень маленький volume (почти неслышимый) вместо 0
		// volume = 0 может блокировать GetOutputData в некоторых случаях
		if (netIdentity != null && netIdentity.netId != 0)
		{
			if (isOwned)
			{
				// Владелец не должен слышать свой голос через AudioSource
				// Используем очень маленький volume (0.0001f) вместо 0, чтобы GetOutputData работал
				// Это практически неслышимо, но позволяет GetOutputData получать данные
				microphoneSource.volume = 0.0001f;
				microphoneSource.mute = false; // НЕ используем mute, чтобы GetOutputData работал
			}
			else
			{
				// Другие игроки могут слышать (если будет реализована синхронизация аудио)
				// Пока оставляем выключенным, так как полная синхронизация аудио требует отдельной реализации
				microphoneSource.mute = false;
			}
		}
		else
		{
			// В одиночной игре можно слышать себя
			microphoneSource.mute = false;
		}
	}
	
	// Command для синхронизации состояния разговора
	[Command]
	void SetTalkingCommand(bool talking)
	{
		isTalking = talking;
	}
	
	// Command для синхронизации амплитуды
	[Command]
	void SetAmplitudeCommand(float amplitude)
	{
		networkAmplitude = amplitude;
	}
	
	// Command для синхронизации цвета игрока
	[Command]
	void CmdSetPlayerColor(Color color)
	{
		syncedPlayerColor = color;
	}
	
	private void FindPlayerController()
	{
		// Ищем PlayerController на этом объекте или в родительских объектах
		playerController = GetComponentInParent<PlayerController>();
		if (playerController == null)
		{
			playerController = GetComponent<PlayerController>();
		}
		if (playerController == null)
		{
			// Пытаемся найти в дочерних объектах
			playerController = GetComponentInChildren<PlayerController>();
		}
	}
	
	void Update()
	{
		// Обрабатываем ввод только для владельца
		if (netIdentity != null && netIdentity.netId != 0 && !isOwned)
		{
			// Для других игроков обновляем визуализацию на основе сетевых переменных
			UpdateVisualizationForRemotePlayers();
			UpdateStatus();
			return;
		}
		
		// Для владельца обрабатываем ввод и микрофон
		HandleTalkInput();
		UpdateAmplitudeAndWave();
		UpdateLineRendererWave();
		UpdateFrequencyRotation();
		UpdateStatus();
		
		// Обновляем цвет спрайта на основе амплитуды (визуальный эффект)
		UpdateSpriteColorBasedOnAmplitude();
	}
	
	private void UpdateVisualizationForRemotePlayers()
	{
		// Используем синхронизированную амплитуду для визуализации
		if (isTalking)
		{
			// Используем синхронизированную амплитуду
			currentAmplitude = Mathf.Lerp(currentAmplitude, networkAmplitude, amplitudeLerp);
			waveScrollDistance += Time.deltaTime * waveSpeed;
		}
		else
		{
			// Плавно уменьшаем амплитуду когда не говорим
			currentAmplitude = Mathf.Lerp(currentAmplitude, 0f, amplitudeLerp);
		}
		
		// Обновляем видимость LineRenderer (для других игроков всегда видна)
		UpdateLineRendererVisibility();
		
		// Обновляем визуализацию линии и спрайта
		UpdateLineRendererWave();
		UpdateFrequencyRotation();
		
		// Обновляем цвет спрайта на основе амплитуды (визуальный эффект)
		UpdateSpriteColorBasedOnAmplitude();
	}
	
	private void UpdateSpriteColorBasedOnAmplitude()
	{
		// Обновляем цвет targetSpriteRenderer на основе амплитуды для визуального эффекта
		if (targetSpriteRenderer != null)
		{
			// Используем сохраненный цвет игрока
			Color baseColor = playerBaseColor;
			
			// Убеждаемся, что спрайт активен и виден
			if (!targetSpriteRenderer.gameObject.activeSelf)
			{
				targetSpriteRenderer.gameObject.SetActive(true);
			}
			
			// Добавляем небольшое изменение яркости на основе амплитуды
			// Для владельца используем локальную амплитуду, для других - синхронизированную
			bool isCurrentlyTalking = (netIdentity != null && netIdentity.netId != 0 && isOwned) ? (micActive && microphoneSource != null && microphoneSource.isPlaying) : isTalking;
			
			if (isCurrentlyTalking && currentAmplitude > 0.01f)
			{
				float brightness = Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(0f, maxAmplitude, currentAmplitude));
				Color adjustedColor = new Color(
					Mathf.Clamp01(baseColor.r * brightness),
					Mathf.Clamp01(baseColor.g * brightness),
					Mathf.Clamp01(baseColor.b * brightness),
					baseColor.a
				);
				targetSpriteRenderer.color = adjustedColor;
			}
			else
			{
				// Возвращаем базовый цвет когда не говорим
				targetSpriteRenderer.color = baseColor;
			}
		}
		else if (targetSpriteRenderer != null)
		{
			// Используем цвет по умолчанию
			ApplySelectedColorIfExists();
		}
	}
	
	void OnDisable()
	{
		StopMicrophone();
		
		// Останавливаем корутину печати
		if (typingCoroutine != null)
		{
			StopCoroutine(typingCoroutine);
			typingCoroutine = null;
		}
		
		lock (remoteQueueLock)
		{
			remoteSampleQueue.Clear();
			lastRemoteSample = 0f;
		}
		
		if (!micActive && microphoneSource != null && microphoneSource.isPlaying)
		{
			microphoneSource.Stop();
		}
		remotePlaybackClip = null;
	}
	
	private void SetupLineRenderer()
	{
		// Если указан родитель — пробуем найти линию среди его потомков или создать новую как дочернюю
		if (lineParent != null)
		{
			lineRenderer = lineParent.GetComponentInChildren<LineRenderer>(true);
			if (lineRenderer == null)
			{
				GameObject lineGO = new GameObject("VoiceWaveLine");
				lineGO.transform.SetParent(lineParent, false);
				lineGO.transform.localPosition = Vector3.zero;
				lineGO.transform.localRotation = Quaternion.identity;
				lineGO.transform.localScale = Vector3.one;
				lineRenderer = lineGO.AddComponent<LineRenderer>();
			}
		}
		else
		{
			// Иначе используем этот объект
			lineRenderer = GetComponent<LineRenderer>();
			if (lineRenderer == null)
			{
				lineRenderer = gameObject.AddComponent<LineRenderer>();
			}
		}
		
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null) shader = Shader.Find("Unlit/Color");
		if (shader != null)
		{
			lineMaterial = new Material(shader);
		}
		
		if (lineMaterial != null)
		{
			lineRenderer.material = lineMaterial;
			lineRenderer.material.color = new Color32(0x0D, 0xD2, 0x44, 0xFF); // #0DD244 (по умолчанию)
		}
		lineRenderer.startColor = new Color32(0x0D, 0xD2, 0x44, 0xFF);
		lineRenderer.endColor = new Color32(0x0D, 0xD2, 0x44, 0xFF);
		
		lineRenderer.useWorldSpace = true;
		lineRenderer.positionCount = Mathf.Max(8, linePoints);
		lineRenderer.widthMultiplier = Mathf.Max(0.001f, lineWidth);
		lineRenderer.startWidth = lineRenderer.widthMultiplier;
		lineRenderer.endWidth = lineRenderer.widthMultiplier;
	}
	
	private void ApplySelectedColorIfExists()
	{
		// Используем сохраненный цвет игрока, если он установлен, иначе цвет по умолчанию
		Color c = playerBaseColor;
		
		// Применить к линии
		if (lineRenderer != null)
		{
			if (lineMaterial != null)
			{
				lineMaterial.color = c;
				lineRenderer.material = lineMaterial;
			}
			lineRenderer.startColor = c;
			lineRenderer.endColor = c;
		}
		
		// Применить к спрайту
		if (targetSpriteRenderer != null)
		{
			targetSpriteRenderer.color = c;
		}
		
		// Применить к тексту статуса
		if (statusText != null)
		{
			statusText.color = c;
		}
	}
	
	private void HandleTalkInput()
	{
		bool wantMic = Input.GetKey(talkKey);
		
		// Обновляем сетевое состояние разговора
		if (netIdentity != null && netIdentity.netId != 0 && isOwned)
		{
			if (wantMic != isTalking)
			{
				if (isServer)
					isTalking = wantMic;
				else
					SetTalkingCommand(wantMic);
			}
		}
		
		if (wantMic && !micActive)
		{
			StartMicrophone();
		}
		else if (!wantMic && micActive)
		{
			StopMicrophone();
		}
	}
	
	private void StartMicrophone()
	{
		// Микрофон работает только для владельца
		if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
		
		if (microphoneSource == null)
		{
			Debug.LogError("[VoiceWaveVisualizer] microphoneSource не назначен!");
			return;
		}
		
		if (Microphone.devices == null || Microphone.devices.Length == 0)
		{
			Debug.LogWarning("[VoiceWaveVisualizer] Микрофон не найден!");
			return;
		}
		
		// Используем настройку микрофона из PlayerPrefs или устройство по умолчанию
		string deviceToUse = currentMicrophoneDevice;
		if (string.IsNullOrEmpty(deviceToUse))
		{
			// Если настройка не загружена, загружаем её сейчас
			string savedDevice = PlayerPrefs.GetString("MicrophoneDevice", null);
			if (!string.IsNullOrEmpty(savedDevice))
			{
				string[] devices = Microphone.devices;
				if (devices != null && System.Array.IndexOf(devices, savedDevice) >= 0)
				{
					deviceToUse = savedDevice;
					currentMicrophoneDevice = savedDevice;
				}
			}
		}

		int sampleRate = micSampleRate > 0 ? micSampleRate : 44100;
		AudioClip micClip = Microphone.Start(deviceToUse, true, 1, sampleRate);
		if (micClip == null)
		{
			Debug.LogError("[VoiceWaveVisualizer] Не удалось запустить микрофон!");
			return;
		}
		
		microphoneSource.loop = true;
		microphoneSource.clip = micClip;
		lastMicSamplePosition = 0;
		
		// Ждем, пока микрофон начнет писать хотя бы 1 сэмпл
		int waitFrames = 0;
		while (Microphone.GetPosition(deviceToUse) <= 0 && waitFrames < 100)
		{
			waitFrames++;
			System.Threading.Thread.Sleep(10);
		}
		
		if (waitFrames >= 100)
		{
			Debug.LogWarning("[VoiceWaveVisualizer] Микрофон не начал запись за отведенное время!");
		}
		
		// Воспроизведение только для визуализации (mute уже установлен в SetupAudioSource)
		microphoneSource.Play();
		
		// Фактическая частота дискретизации клипа (если доступна)
		currentSampleRate = micClip.frequency > 0 ? micClip.frequency : sampleRate;
		micActive = true;
		
		string deviceName = deviceToUse ?? (Microphone.devices != null && Microphone.devices.Length > 0 ? Microphone.devices[0] : "По умолчанию");
		Debug.Log($"[VoiceWaveVisualizer] ✓ Микрофон запущен! SampleRate: {currentSampleRate}, Device: {deviceName}");
	}
	
	private void StopMicrophone()
	{
		// Останавливаем микрофон только для владельца
		if (netIdentity != null && netIdentity.netId != 0 && !isOwned) return;
		
		if (microphoneSource != null && microphoneSource.isPlaying)
		{
			microphoneSource.Stop();
		}
		string deviceToStop = currentMicrophoneDevice;
		if (Microphone.IsRecording(deviceToStop))
		{
			Microphone.End(deviceToStop);
		}
		micActive = false;
		lastMicSamplePosition = 0;
	}
	
	private void UpdateAmplitudeAndWave()
	{
		float targetAmp = 0f;
		
		// ВАЖНО: GetOutputData НЕ работает с микрофоном в Unity!
		// Нужно использовать прямой доступ к AudioClip через GetData()
		string deviceToCheck = currentMicrophoneDevice;
		float[] clipData = null;
		int capturedSamples = 0;
		
		if (microphoneSource != null && microphoneSource.clip != null && micActive && Microphone.IsRecording(deviceToCheck))
		{
			try
			{
				clipData = CaptureMicSamples(microphoneSource.clip, deviceToCheck, out capturedSamples);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[VoiceWaveVisualizer] Ошибка получения данных микрофона: {e.Message}\n{e.StackTrace}");
			}
		}
		else if (micActive && Time.frameCount % 300 == 0)
		{
			string issue = "";
			if (microphoneSource == null) issue += "microphoneSource=null; ";
			else if (microphoneSource.clip == null) issue += "clip=null; ";
			else if (!microphoneSource.isPlaying) issue += "not playing; ";
			if (!Microphone.IsRecording(deviceToCheck)) issue += "not recording; ";
			Debug.LogWarning($"[VoiceWaveVisualizer] Микрофон активен, но данные не получаются! {issue}");
		}
		
		if (clipData != null && capturedSamples > 0)
		{
			int copyCount = Mathf.Min(capturedSamples, amplitudeSamples.Length);
			for (int i = 0; i < copyCount; i++)
			{
				amplitudeSamples[i] = clipData[i];
			}
			
			float sum = 0f;
			float maxSample = 0f;
			for (int i = 0; i < capturedSamples; i++)
			{
				float v = clipData[i];
				float absV = Mathf.Abs(v);
				if (absV > maxSample) maxSample = absV;
				sum += v * v;
			}
			
			float rms = Mathf.Sqrt(sum / capturedSamples);
			float sensitivity = PlayerPrefs.GetFloat("MicrophoneSensitivity", amplitudeSensitivity);
			if (sensitivity < 0.0001f) sensitivity = amplitudeSensitivity;
			float loudness = Mathf.Clamp01(rms * sensitivity);
			
			targetAmp = Mathf.Lerp(minAmplitude, maxAmplitude, loudness);
			TrySendVoicePacket(clipData, microphoneSource.clip.frequency, targetAmp);
			
			if (netIdentity != null && netIdentity.netId != 0 && isOwned && Time.frameCount % 300 == 0)
			{
				Debug.Log($"[VoiceWaveVisualizer] Mic samples: {capturedSamples}, RMS: {rms:F6}, Loudness: {loudness:F4}, TargetAmp: {targetAmp:F4}");
			}
		}
		else
		{
			targetAmp = Mathf.Lerp(currentAmplitude, minAmplitude, amplitudeLerp);
		}
		
		currentAmplitude = Mathf.Lerp(currentAmplitude, targetAmp, amplitudeLerp);
		
		// Синхронизируем амплитуду через сеть для других игроков
		if (netIdentity != null && netIdentity.netId != 0 && isOwned)
		{
			if (isServer)
				networkAmplitude = currentAmplitude;
			else
				SetAmplitudeCommand(currentAmplitude);
		}
		
		// Непрерывное движение волны вдоль линии в метрах/сек (даже когда не говорим, для плавности)
		waveScrollDistance += Time.deltaTime * waveSpeed;
	}
	
	private void UpdateLineRendererWave()
	{
		if (lineRenderer == null || startPoint == null || endPoint == null)
		{
			// Дебаг: проверяем наличие компонентов
			if (lineRenderer == null)
				Debug.LogWarning("[VoiceWaveVisualizer] lineRenderer не найден!");
			if (startPoint == null)
				Debug.LogWarning("[VoiceWaveVisualizer] startPoint не назначен!");
			if (endPoint == null)
				Debug.LogWarning("[VoiceWaveVisualizer] endPoint не назначен!");
			return;
		}
		
		// Обновляем видимость LineRenderer (скрываем для владельца)
		UpdateLineRendererVisibility();
		
		int count = Mathf.Max(8, linePoints);
		if (lineRenderer.positionCount != count) lineRenderer.positionCount = count;
		
		Vector3 a = startPoint.position;
		Vector3 b = endPoint.position;
		Vector3 dir = (b - a);
		float len = dir.magnitude;
		if (len <= 0.0001f)
		{
			// Деградация: рисуем одну точку
			for (int i = 0; i < count; i++)
			{
				lineRenderer.SetPosition(i, a);
			}
			return;
		}
		dir /= len;
		
		// Выбираем перпендикуляр
		Vector3 up = Vector3.up;
		if (Mathf.Abs(Vector3.Dot(up, dir)) > 0.95f) up = Vector3.right;
		Vector3 perp = Vector3.Cross(dir, up).normalized;
		
		// Длина волны зависит от громкости: громче -> короче (больше волн)
		float loudT = Mathf.InverseLerp(minAmplitude, maxAmplitude, currentAmplitude);
		float wavelength = Mathf.Lerp(maxWavelength, minWavelength, loudT);
		wavelength = Mathf.Max(0.01f, wavelength);

		for (int i = 0; i < count; i++)
		{
			float t = i / (float)(count - 1);
			Vector3 pos = Vector3.Lerp(a, b, t);

			float s = t * len; // расстояние вдоль линии для текущей точки

			// Непрерывная «бегущая» волна на всем протяжении
			float phase = (s - waveScrollDistance) * (Mathf.PI * 2f / wavelength);
			float offset = Mathf.Sin(phase) * currentAmplitude;

			lineRenderer.SetPosition(i, pos + perp * offset);
		}
		
		// Гарантируем точные концы
		lineRenderer.SetPosition(0, a);
		lineRenderer.SetPosition(count - 1, b);
		
		// Применяем толщину
		float w = Mathf.Max(0.001f, lineWidth);
		lineRenderer.widthMultiplier = w;
		lineRenderer.startWidth = w;
		lineRenderer.endWidth = w;
	}
	
	private void TrySendVoicePacket(float[] samples, int sampleRate, float amplitude)
	{
		if (!isOwned || !NetworkClient.active || !micActive)
			return;
		
		if (samples == null || samples.Length == 0)
			return;
		
		if (amplitude < minVoiceAmplitude)
			return;
		
		voiceSendTimer += Time.deltaTime;
		if (voiceSendTimer < voiceSendInterval)
			return;
		
		voiceSendTimer = 0f;
		
		int sampleCount = Mathf.Min(samples.Length, Mathf.Max(128, maxSamplesPerPacket));
		int byteCount = sampleCount * 2;
		if (voiceByteBuffer == null || voiceByteBuffer.Length != byteCount)
		{
			voiceByteBuffer = new byte[byteCount];
		}
		
		for (int i = 0; i < sampleCount; i++)
		{
			float clamped = Mathf.Clamp(samples[i], -1f, 1f);
			short value = (short)Mathf.RoundToInt(clamped * 32767f);
			int byteIndex = i * 2;
			voiceByteBuffer[byteIndex] = (byte)(value & 0xFF);
			voiceByteBuffer[byteIndex + 1] = (byte)((value >> 8) & 0xFF);
		}
		
		byte[] packet = new byte[byteCount];
		System.Buffer.BlockCopy(voiceByteBuffer, 0, packet, 0, byteCount);
		CmdSendVoiceData(packet, sampleCount, sampleRate, amplitude);
	}
	
	[Command(channel = Channels.Unreliable)]
	void CmdSendVoiceData(byte[] encodedSamples, int sampleCount, int sampleRate, float amplitude)
	{
		if (encodedSamples == null || sampleCount <= 0 || sampleRate <= 0)
			return;
		
		RpcReceiveVoiceData(encodedSamples, sampleCount, sampleRate, amplitude);
	}
	
	[ClientRpc(includeOwner = false, channel = Channels.Unreliable)]
	void RpcReceiveVoiceData(byte[] encodedSamples, int sampleCount, int sampleRate, float amplitude)
	{
		if (encodedSamples == null || sampleCount <= 0 || sampleRate <= 0)
			return;
		
		PlayRemoteVoice(encodedSamples, sampleCount, sampleRate);
		networkAmplitude = amplitude;
		isTalking = amplitude > minVoiceAmplitude * 0.5f;
	}
	
	private void PlayRemoteVoice(byte[] encodedSamples, int sampleCount, int sampleRate)
	{
		if (microphoneSource == null)
			return;
		
		int expectedLength = sampleCount * 2;
		if (encodedSamples.Length < expectedLength)
			return;
		
		float[] floatSamples = new float[sampleCount];
		for (int i = 0; i < sampleCount; i++)
		{
			int byteIndex = i * 2;
			short sampleValue = (short)(encodedSamples[byteIndex] | (encodedSamples[byteIndex + 1] << 8));
			floatSamples[i] = sampleValue / 32767f;
		}
		
		QueueRemoteSamples(floatSamples, sampleRate);
	}
	
	private void QueueRemoteSamples(float[] samples, int sampleRate)
	{
		if (samples == null || samples.Length == 0)
			return;
		
		EnsureRemoteAudioPlayback(sampleRate);
		
		lock (remoteQueueLock)
		{
			int maxSamples = Mathf.Max(sampleRate, Mathf.CeilToInt(sampleRate * Mathf.Clamp(remoteBufferDuration, 0.1f, 2f)));
			int targetCount = remoteSampleQueue.Count + samples.Length;
			
			if (targetCount > maxSamples)
			{
				int toDrop = targetCount - maxSamples;
				for (int i = 0; i < toDrop && remoteSampleQueue.Count > 0; i++)
				{
					remoteSampleQueue.Dequeue();
				}
			}
			
			for (int i = 0; i < samples.Length; i++)
			{
				remoteSampleQueue.Enqueue(samples[i]);
			}
		}
	}
	
	private void EnsureRemoteAudioPlayback(int sampleRate)
	{
		if (microphoneSource == null)
			return;
		
		if (remoteSampleRate != sampleRate)
		{
			remoteSampleRate = sampleRate;
			remotePlaybackClip = null;
		}
		
		if (remotePlaybackClip == null)
		{
			int clipSamples = Mathf.Max(sampleRate, 1024);
			remotePlaybackClip = AudioClip.Create("RemoteVoiceStream", clipSamples, 1, sampleRate, false);
			microphoneSource.clip = remotePlaybackClip;
		}
		
		if (!microphoneSource.isPlaying)
		{
			microphoneSource.loop = true;
			microphoneSource.Play();
		}
	}
	
	private float[] CaptureMicSamples(AudioClip micClip, string deviceName, out int capturedSamples)
	{
		capturedSamples = 0;
		if (micClip == null)
			return null;
		
		int clipSamples = micClip.samples;
		if (clipSamples <= 0)
			return null;
		
		int micPosition = Microphone.GetPosition(deviceName);
		if (micPosition < 0)
			return null;
		
		int samplesAvailable = micPosition - lastMicSamplePosition;
		if (samplesAvailable < 0)
		{
			samplesAvailable += clipSamples;
		}
		
		int samplesToRead = Mathf.Min(samplesAvailable, maxSamplesPerPacket);
		if (samplesToRead < 128)
			return null;
		if (samplesToRead <= 0)
			return null;
		
		if (micChunkBuffer == null || micChunkBuffer.Length != samplesToRead)
		{
			micChunkBuffer = new float[samplesToRead];
		}
		
		int startPos = lastMicSamplePosition;
		if (startPos + samplesToRead <= clipSamples)
		{
			micClip.GetData(micChunkBuffer, startPos);
		}
		else
		{
			int tailLength = clipSamples - startPos;
			float[] tailBuffer = new float[tailLength];
			micClip.GetData(tailBuffer, startPos);
			System.Array.Copy(tailBuffer, 0, micChunkBuffer, 0, tailLength);
			
			int headLength = samplesToRead - tailLength;
			float[] headBuffer = new float[headLength];
			micClip.GetData(headBuffer, 0);
			System.Array.Copy(headBuffer, 0, micChunkBuffer, tailLength, headLength);
		}
		
		lastMicSamplePosition = (lastMicSamplePosition + samplesToRead) % clipSamples;
		capturedSamples = samplesToRead;
		return micChunkBuffer;
	}
	
	private void UpdateFrequencyRotation()
	{
		Vector3 targetEuler = Vector3.zero;
		bool hasTargetRotation = false;
		
		// ВАЖНО: GetSpectrumData может не работать с микрофоном так же, как GetOutputData
		// Поэтому используем амплитуду для определения поворота (она уже работает!)
		if (netIdentity != null && netIdentity.netId != 0 && isOwned && micActive && currentAmplitude > 0.01f)
		{
			// Используем амплитуду для определения поворота
			// Высокая амплитуда обычно соответствует более высоким частотам
			float ampNormalized = Mathf.InverseLerp(0f, maxAmplitude, currentAmplitude);
			
			// Пробуем получить спектр для более точного определения частоты
			bool spectrumValid = false;
			if (microphoneSource != null && microphoneSource.isPlaying)
			{
				try
				{
					microphoneSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
					
					// Проверяем, есть ли данные в спектре (не все нули)
					float spectrumSum = 0f;
					for (int i = 0; i < spectrum.Length; i++)
					{
						spectrumSum += spectrum[i];
					}
					
					if (spectrumSum > 0.0001f) // Спектр содержит данные
					{
						// Находим "доминирующую" частоту (самую сильную)
						int maxIndex = 0;
						float maxValue = 0f;
						for (int i = 1; i < spectrum.Length; i++)
						{
							float v = spectrum[i];
							if (v > maxValue)
							{
								maxValue = v;
								maxIndex = i;
							}
						}
						
						// Переводим индекс в частоту
						float sampleRate = currentSampleRate > 0 ? currentSampleRate : (micSampleRate > 0 ? micSampleRate : 44100);
						float dominantFreq = (maxIndex * sampleRate * 0.5f) / spectrum.Length;
						
						// Классифицируем на основе частоты
						if (dominantFreq >= midFreqMax)
						{
							targetEuler = new Vector3(90f, 0f, 0f);
						}
						else if (dominantFreq >= lowFreqMax)
						{
							targetEuler = new Vector3(90f, 0f, -45f);
						}
						else
						{
							targetEuler = new Vector3(90f, 0f, 47f);
						}
						spectrumValid = true;
						hasTargetRotation = true;
					}
				}
				catch (System.Exception e)
				{
					// Спектр не работает, используем амплитуду
				}
			}
			
			// Если спектр не работает, используем амплитуду
			if (!spectrumValid)
			{
				// Используем амплитуду для приблизительного определения частоты
				// Высокая амплитуда + быстрые изменения = высокие частоты
				// Низкая амплитуда = низкие частоты
				if (ampNormalized > 0.7f)
				{
					// Высокая амплитуда - вероятно высокие частоты
					targetEuler = new Vector3(90f, 0f, 47f);
				}
				else if (ampNormalized > 0.3f)
				{
					// Средняя амплитуда - средние частоты
					targetEuler = new Vector3(90f, 0f, -45f);
				}
				else
				{
					// Низкая амплитуда - низкие частоты
					targetEuler = new Vector3(90f, 0f, 0f);
				}
				hasTargetRotation = true;
			}
		}
		else if (netIdentity != null && netIdentity.netId != 0 && !isOwned && isTalking)
		{
			// Для других игроков используем упрощенную логику на основе амплитуды
			float ampNormalized = Mathf.InverseLerp(0f, maxAmplitude, currentAmplitude);
			
			if (ampNormalized > 0.7f)
			{
				targetEuler = UnityEngine.Random.value > 0.5f ? new Vector3(90f, 0f, 0f) : new Vector3(90f, 0f, -45f);
			}
			else if (ampNormalized > 0.3f)
			{
				targetEuler = new Vector3(90f, 0f, -45f);
			}
			else
			{
				targetEuler = new Vector3(90f, 0f, 47f);
			}
			hasTargetRotation = true;
		}
		
		// Применяем поворот к frequencyPivot, если он назначен
		if (frequencyPivot != null && hasTargetRotation)
		{
			Quaternion current = frequencyPivot.transform.localRotation;
			Quaternion target = Quaternion.Euler(targetEuler);
			frequencyPivot.transform.localRotation = Quaternion.Lerp(current, target, rotateLerpSpeed * Time.deltaTime);
		}
		
		// Применяем поворот к targetSpriteRenderer, если он назначен
		if (targetSpriteRenderer != null && hasTargetRotation)
		{
			// Проверяем, не является ли targetSpriteRenderer дочерним объектом frequencyPivot
			bool isChildOfPivot = frequencyPivot != null && targetSpriteRenderer.transform.IsChildOf(frequencyPivot.transform);
			
			// Если не дочерний объект, применяем поворот напрямую
			if (!isChildOfPivot)
			{
				Quaternion current = targetSpriteRenderer.transform.localRotation;
				Quaternion target = Quaternion.Euler(targetEuler);
				targetSpriteRenderer.transform.localRotation = Quaternion.Lerp(current, target, rotateLerpSpeed * Time.deltaTime);
				
				// Дебаг лог (только иногда, чтобы не засорять консоль)
				if (netIdentity != null && netIdentity.netId != 0 && isOwned && Time.frameCount % 300 == 0)
				{
					Debug.Log($"[VoiceWaveVisualizer] Поворот targetSpriteRenderer: {targetEuler}, AmpNormalized: {Mathf.InverseLerp(0f, maxAmplitude, currentAmplitude):F2}");
				}
			}
		}
		else if (targetSpriteRenderer == null && netIdentity != null && netIdentity.netId != 0 && isOwned && Time.frameCount % 300 == 0)
		{
			Debug.LogWarning("[VoiceWaveVisualizer] targetSpriteRenderer не назначен в инспекторе!");
		}
	}
	
	private void OnAudioFilterRead(float[] data, int channels)
	{
		if (microphoneSource == null || data == null || channels <= 0)
			return;
		
		if (netIdentity == null || netIdentity.netId == 0 || isOwned)
			return;
		
		lock (remoteQueueLock)
		{
			for (int i = 0; i < data.Length; i += channels)
			{
				float sample = remoteSampleQueue.Count > 0 ? remoteSampleQueue.Dequeue() : lastRemoteSample * 0.95f;
				lastRemoteSample = sample;
				sample = Mathf.Clamp(sample * remotePlaybackGain, -1f, 1f);
				
				for (int ch = 0; ch < channels; ch++)
				{
					data[i + ch] = sample;
				}
			}
		}
	}
	
	private void UpdateStatus()
	{
		if (statusText == null) return;
		
		// Определяем, в какой позиции находится игрок
		bool isProne = false;
		if (playerController != null)
		{
			isProne = playerController.IsProne();
		}
		
		// Если игрок лежит, показываем statusText с системным статусом
		// Если игрок стоит, statusText скрыт (показывается nameTagText из PlayerController)
		if (isProne)
		{
			// Показываем statusText когда лежим
			if (!statusText.gameObject.activeSelf)
			{
				statusText.gameObject.SetActive(true);
			}
			
			statusUpdateTimer += Time.deltaTime;
			
			if (statusUpdateTimer >= statusUpdateInterval)
			{
				statusUpdateTimer = 0f;
				
				// Определяем категорию частоты
				string newStatus = GetStatusText();
				
				// Если статус изменился, запускаем анимацию печати
				if (newStatus != currentStatusText)
				{
					currentStatusText = newStatus;
					
					// Останавливаем предыдущую корутину, если она запущена
					if (typingCoroutine != null)
					{
						StopCoroutine(typingCoroutine);
					}
					
					// Запускаем новую анимацию печати
					typingCoroutine = StartCoroutine(TypeText(newStatus));
				}
			}
		}
		else
		{
			// Скрываем statusText когда стоим (показывается nameTagText)
			if (statusText.gameObject.activeSelf)
			{
				statusText.gameObject.SetActive(false);
			}
		}
	}
	
	private string GetStatusText()
	{
		// Если есть имя игрока, добавляем его в начало
		string namePrefix = "";
		if (!string.IsNullOrEmpty(playerName))
		{
			namePrefix = $"{playerName} - ";
		}
		
		string status;
		
		// Для владельца используем микрофон, для других игроков - упрощенную логику
		if (netIdentity != null && netIdentity.netId != 0 && isOwned && microphoneSource != null && microphoneSource.isPlaying)
		{
			// Получаем спектр
			microphoneSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
			
			// Находим доминирующую частоту
			int maxIndex = 0;
			float maxValue = 0f;
			for (int i = 1; i < spectrum.Length; i++)
			{
				float v = spectrum[i];
				if (v > maxValue)
				{
					maxValue = v;
					maxIndex = i;
				}
			}
			
			// Переводим индекс в частоту
			float sampleRate = currentSampleRate > 0 ? currentSampleRate : (micSampleRate > 0 ? micSampleRate : 44100);
			float dominantFreq = (maxIndex * sampleRate * 0.5f) / spectrum.Length;
			
			// Классифицируем и возвращаем соответствующий текст
			if (dominantFreq >= midFreqMax)
			{
				// высокие
				status = "$ system status : happy :)";
			}
			else if (dominantFreq >= lowFreqMax)
			{
				// средние
				status = "$ system status : normal";
			}
			else
			{
				// низкие
				status = "$ system status : angry >:(";
			}
		}
		else if (netIdentity != null && netIdentity.netId != 0 && !isOwned && isTalking)
		{
			// Для других игроков используем упрощенную логику на основе амплитуды
			float ampNormalized = Mathf.InverseLerp(0f, maxAmplitude, currentAmplitude);
			
			if (ampNormalized > 0.7f)
			{
				// высокие
				status = "$ system status : happy :)";
			}
			else if (ampNormalized > 0.3f)
			{
				// средние
				status = "$ system status : normal";
			}
			else
			{
				// низкие
				status = "$ system status : angry >:(";
			}
		}
		else
		{
			// Микрофон не активен или не доступен
			status = "$ system status : normal";
		}
		
		return $"{namePrefix}{status}";
	}
	
	/// <summary>
	/// Применяет цвет игрока к визуализатору
	/// </summary>
	public void ApplyPlayerColor(Color color, bool syncToNetwork = true)
	{
		// Сохраняем базовый цвет игрока
		playerBaseColor = color;
		
		// Применяем к линии
		if (lineRenderer != null)
		{
			if (lineMaterial != null)
			{
				lineMaterial.color = color;
				lineRenderer.material = lineMaterial;
			}
			lineRenderer.startColor = color;
			lineRenderer.endColor = color;
		}
		
		// Применяем к спрайту
		if (targetSpriteRenderer != null)
		{
			targetSpriteRenderer.color = color;
		}
		
		// Применяем к тексту статуса
		if (statusText != null)
		{
			statusText.color = color;
		}

		if (syncToNetwork)
		{
			TrySyncPlayerColor(color);
		}
	}
	
	private void TrySyncPlayerColor(Color color)
	{
		if (netIdentity == null || netIdentity.netId == 0)
			return;
		
		if (isServer)
		{
			syncedPlayerColor = color;
		}
		else if (isOwned)
		{
			CmdSetPlayerColor(color);
		}
	}
	
	/// <summary>
	/// Устанавливает имя игрока
	/// </summary>
	public void SetPlayerName(string name)
	{
		if (string.IsNullOrEmpty(name) || name == playerName) return;
		
		playerName = name;
		
		// Обновляем статус текст
		if (statusText != null)
		{
			// Обновляем текущий статус с новым именем
			string newStatus = GetStatusText();
			currentStatusText = newStatus;
			
			// Останавливаем предыдущую корутину
			if (typingCoroutine != null)
			{
				StopCoroutine(typingCoroutine);
			}
			
			// Запускаем новую анимацию печати
			typingCoroutine = StartCoroutine(TypeText(newStatus));
		}
	}
	
	private IEnumerator TypeText(string fullText)
	{
		if (statusText == null) yield break;
		
		float duration = typingAnimationDuration;
		int totalChars = fullText.Length;
		float charDelay = duration / totalChars;
		
		// Печатаем текст посимвольно
		for (int i = 0; i <= totalChars; i++)
		{
			statusText.text = fullText.Substring(0, i);
			yield return new WaitForSeconds(charDelay);
		}
		
		// Убеждаемся, что весь текст отображен
		statusText.text = fullText;
		typingCoroutine = null;
	}
	
	/// <summary>
	/// Обновляет видимость LineRenderer (скрывает для владельца, показывает для других игроков)
	/// </summary>
	private void UpdateLineRendererVisibility()
	{
		if (lineRenderer == null)
			return;
		
		// Если заспавнен в сети, скрываем для владельца, показываем для других
		if (netIdentity != null && netIdentity.netId != 0)
		{
			// Владелец не видит свою линию голоса
			lineRenderer.enabled = !isOwned;
		}
		else
		{
			// В одиночной игре показываем линию
			lineRenderer.enabled = true;
		}
	}
}


