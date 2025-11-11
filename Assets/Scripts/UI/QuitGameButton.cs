using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

public class QuitGameButton : MonoBehaviour
{
    public Button button;
	
	[Header("UI")]
	public GameObject shutdownPanel; // Появляется при нажатии
	public Text line1Text; // "$ sudo shutdown"
	public Text line2Text; // "[system] killing all process ..."
	public Text line3Text; // "shutdown localhost"
	public Text line4Text; // "bye..."
	[Header("Progress UI")]
	public Image progressImage; // Image с Image Type = Filled для прогресса
    public Image progressImage2;
	[Tooltip("Скорость заполнения индикатора (ед/сек)")]
	[Min(0f)]
	public float progressFillSpeed = 1.5f;
	
	[Header("Typing Settings")]
	[Tooltip("Задержка между символами, сек")]
	[Min(0f)]
	public float charDelay = 0.03f;
	[Tooltip("Задержка между строками, сек")]
	[Min(0f)]
	public float linePause = 0.35f;
	
	[Header("Optional Saves shown on line 2")]
	public bool savePlayerPrefs = true;
	public bool saveModConfiguration = false;
	[Tooltip("Вызывается, если требуется сохранить конфигурацию модов")]
	public UnityEvent onSaveMods;
	
	private Coroutine progressRoutine;
    
    void Start()
    {
        if (button != null)
        {
			button.onClick.AddListener(OnQuitClicked);
        }
    }
    
	private void OnQuitClicked()
    {
		if (shutdownPanel != null)
		{
			shutdownPanel.SetActive(true);
		}

		// Очистить тексты перед началом
		if (line1Text) line1Text.text = string.Empty;
		if (line2Text) line2Text.text = string.Empty;
		if (line3Text) line3Text.text = string.Empty;
		if (line4Text) line4Text.text = string.Empty;
		
		// Подготовить прогресс-бар
		if (progressImage)
		{
			progressImage.type = Image.Type.Filled;
			progressImage.fillAmount = 0f;
			progressImage.gameObject.SetActive(false);
            progressImage2.gameObject.SetActive(false);
		}

		StartCoroutine(StartShutdownSequence());
    }
    
	private IEnumerator StartShutdownSequence()
	{
		// 1) "$ sudo shutdown"
		yield return StartCoroutine(TypeLine(line1Text, "$ sudo shutdown"));
		
		// Показать индикатор прогресса сразу после первой строки
		if (progressImage)
		{
			progressImage.gameObject.SetActive(true);
            progressImage2.gameObject.SetActive(true);
			StartFillTo(0.4f); // начинаем заполняться во время строки 2
		}
		
		yield return new WaitForSeconds(linePause);

		// 2) "[system] killing all process" (+ optional saves on same line)
		yield return StartCoroutine(TypeLine(line2Text, "[system] killing all process"));

		// Optional: append saves on same line with typing effect
		if (savePlayerPrefs)
		{
			yield return new WaitForSeconds(0.25f);
			yield return StartCoroutine(AppendText(line2Text, "  - saving prefs..."));
			yield return new WaitForSeconds(0.2f);
			PlayerPrefs.Save();
			yield return StartCoroutine(AppendText(line2Text, " done"));
			StartFillTo(0.7f);
		}

		if (saveModConfiguration)
		{
			yield return new WaitForSeconds(0.25f);
			yield return StartCoroutine(AppendText(line2Text, "  - saving mods..."));
			yield return new WaitForSeconds(0.2f);
			onSaveMods?.Invoke();
			yield return StartCoroutine(AppendText(line2Text, " done"));
			StartFillTo(1f);
		}
		
		// Если сохранений нет — все равно доведем индикатор до 1
		if (!savePlayerPrefs && !saveModConfiguration)
			StartFillTo(1f);

		yield return new WaitForSeconds(linePause);

		// 3) "shutdown localhost"
		yield return StartCoroutine(TypeLine(line3Text, "shutdown localhost"));
		yield return new WaitForSeconds(linePause);

		// Hide 1-3 lines
		if (line1Text) line1Text.text = string.Empty;
		if (line2Text) line2Text.text = string.Empty;
		if (line3Text) line3Text.text = string.Empty;
		
		// Скрыть индикатор перед началом линии 4
		if (progressImage)
			progressImage.gameObject.SetActive(false);
		if (progressImage2)
			progressImage2.gameObject.SetActive(false);
		
		yield return new WaitForSeconds(0.2f);

		// 4) "bye..." then quit
		yield return StartCoroutine(TypeLine(line4Text, "bye..."));
		yield return new WaitForSeconds(0.45f);

		QuitGameImmediate();
	}

	private void StartFillTo(float targetFill)
	{
		if (!progressImage)
			return;
		
		targetFill = Mathf.Clamp01(targetFill);
		if (progressRoutine != null)
			StopCoroutine(progressRoutine);
		progressRoutine = StartCoroutine(AnimateFill(targetFill));
	}

	private IEnumerator AnimateFill(float targetFill)
	{
		if (!progressImage)
			yield break;
		
		while (!Mathf.Approximately(progressImage.fillAmount, targetFill))
		{
			progressImage.fillAmount = Mathf.MoveTowards(
				progressImage.fillAmount,
				targetFill,
				progressFillSpeed * Time.deltaTime
			);
			yield return null;
		}
	}

	private IEnumerator TypeLine(Text target, string text)
	{
		if (target == null)
			yield break;

		target.text = string.Empty;
		for (int i = 0; i < text.Length; i++)
		{
			target.text += text[i];
			if (charDelay > 0f)
				yield return new WaitForSeconds(charDelay);
			else
				yield return null;
		}
	}

	private IEnumerator AppendText(Text target, string textToAppend)
	{
		if (target == null || string.IsNullOrEmpty(textToAppend))
			yield break;

		for (int i = 0; i < textToAppend.Length; i++)
		{
			target.text += textToAppend[i];
			if (charDelay > 0f)
				yield return new WaitForSeconds(charDelay);
			else
				yield return null;
		}
	}

	private void QuitGameImmediate()
	{
		Application.Quit();

		#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
		#endif
	}
	
    void OnDestroy()
    {
        if (button != null)
        {
			button.onClick.RemoveListener(OnQuitClicked);
        }
    }
}

