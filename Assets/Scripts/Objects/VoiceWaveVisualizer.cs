using UnityEngine;
using TMPro;
using System.Collections;

public class VoiceWaveVisualizer : MonoBehaviour
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
	private float[] spectrum;
	private float currentAmplitude;
	private float timePhase;
	private float waveScrollDistance; // расстояние прокрутки волны вдоль линии
	private bool micActive;
	private int currentSampleRate;
	
	// Статус системы
	private float statusUpdateTimer = 0f;
	private string currentStatusText = "";
	private Coroutine typingCoroutine;
	
	private static readonly string PrefR = "PlayerColor_R";
	private static readonly string PrefG = "PlayerColor_G";
	private static readonly string PrefB = "PlayerColor_B";
	private static readonly string PrefA = "PlayerColor_A";
	
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
	
	void Update()
	{
		HandleTalkInput();
		UpdateAmplitudeAndWave();
		UpdateLineRendererWave();
		UpdateFrequencyRotation();
		UpdateStatus();
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
		if (!PlayerPrefs.HasKey(PrefR) || !PlayerPrefs.HasKey(PrefG) || !PlayerPrefs.HasKey(PrefB) || !PlayerPrefs.HasKey(PrefA))
			return;
		
		Color c = new Color(
			PlayerPrefs.GetFloat(PrefR, 0.05f),
			PlayerPrefs.GetFloat(PrefG, 0.82f),
			PlayerPrefs.GetFloat(PrefB, 0.27f),
			PlayerPrefs.GetFloat(PrefA, 1f)
		);
		
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
		if (microphoneSource == null) return;
		if (Microphone.devices == null || Microphone.devices.Length == 0) return;
		
		int sampleRate = micSampleRate > 0 ? micSampleRate : 44100;
		AudioClip micClip = Microphone.Start(null, true, 1, sampleRate);
		if (micClip == null) return;
		
		microphoneSource.loop = true;
		microphoneSource.clip = micClip;
		// Ждем, пока микрофон начнет писать хотя бы 1 сэмпл
		while (Microphone.GetPosition(null) <= 0) { }
		microphoneSource.Play();
		
		// Фактическая частота дискретизации клипа (если доступна)
		currentSampleRate = micClip.frequency > 0 ? micClip.frequency : sampleRate;
		micActive = true;
	}
	
	private void StopMicrophone()
	{
		if (microphoneSource != null && microphoneSource.isPlaying)
		{
			microphoneSource.Stop();
		}
		if (Microphone.IsRecording(null))
		{
			Microphone.End(null);
		}
		micActive = false;
	}
	
	private void UpdateAmplitudeAndWave()
	{
		float targetAmp = 0f;
		
		if (microphoneSource != null && microphoneSource.isPlaying)
		{
			// Получаем амплитуду
			microphoneSource.GetOutputData(amplitudeSamples, 0);
			float sum = 0f;
			for (int i = 0; i < amplitudeSamples.Length; i++)
			{
				float v = amplitudeSamples[i];
				sum += v * v;
			}
			float rms = Mathf.Sqrt(sum / amplitudeSamples.Length);
			float loudness = Mathf.Clamp01(rms * amplitudeSensitivity);
			
			targetAmp = Mathf.Lerp(minAmplitude, maxAmplitude, loudness);
		}
		
		currentAmplitude = Mathf.Lerp(currentAmplitude, targetAmp, amplitudeLerp);
		// Непрерывное движение волны вдоль линии в метрах/сек
		waveScrollDistance += Time.deltaTime * waveSpeed;
	}
	
	private void UpdateLineRendererWave()
	{
		if (lineRenderer == null || startPoint == null || endPoint == null) return;
		
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
	
	private void UpdateFrequencyRotation()
	{
		if (frequencyPivot == null || microphoneSource == null) return;
		
		Vector3 targetEuler = frequencyPivot.transform.localEulerAngles;
		
		// Снимаем спектр
		if (microphoneSource.isPlaying)
		{
			microphoneSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
			
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
			// По документации: бин частоты ~ (i * sampleRate / 2) / spectrumSize
			float dominantFreq = (maxIndex * sampleRate * 0.5f) / spectrum.Length;
			
			// Классифицируем
			if (dominantFreq >= midFreqMax)
			{
				// высокие
				targetEuler = new Vector3(90f, 0f, 0f);
			}
			else if (dominantFreq >= lowFreqMax)
			{
				// средние
				targetEuler = new Vector3(90f, 0f, -45f);
			}
			else
			{
				// низкие
				targetEuler = new Vector3(90f, 0f, 47f);
			}
		}
		
		Quaternion current = frequencyPivot.transform.localRotation;
		Quaternion target = Quaternion.Euler(targetEuler);
		frequencyPivot.transform.localRotation = Quaternion.Lerp(current, target, rotateLerpSpeed * Time.deltaTime);
	}
	
	private void UpdateStatus()
	{
		if (statusText == null) return;
		
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
	
	private string GetStatusText()
	{
		if (microphoneSource == null || !microphoneSource.isPlaying)
		{
			return "$ system status : normal";
		}
		
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
			return "$ system status : happy :)";
		}
		else if (dominantFreq >= lowFreqMax)
		{
			// средние
			return "$ system status : normal";
		}
		else
		{
			// низкие
			return "$ system status : angry >:(";
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
}


