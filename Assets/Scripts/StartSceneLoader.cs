using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class StartSceneLoader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string menuSceneName = "Menu";

    [Header("Progress UI")]
    [Tooltip("Заполняемый Image с Fill Method = Horizontal, отображающий прогресс загрузки.")]
    [SerializeField] private Image progressImage;

    [Tooltip("Текстовое поле статуса (например: \"загрузка текстур...\", \"загрузка игры...\", \"синхронизация с Steam...\").")]
    [SerializeField] private Text statusText;

    [Tooltip("Цвет текста в обычном состоянии.")]
    [SerializeField] private Color normalTextColor = Color.white;

    [Tooltip("Цвет текста при ошибке.")]
    [SerializeField] private Color errorTextColor = Color.red;

    [Header("Loading Messages")]
    [Tooltip("Сообщения, которые будут отображаться по мере загрузки.")]
    [SerializeField] private string[] loadingMessages =
    {
        "загрузка текстур...",
        "загрузка игры...",
        "синхронизация с Steam..."
    };

    [Header("Fade Out")]
    [Tooltip("Изображения (в том числе логотип/фон/прогрессбар), которые будут плавно скрываться после загрузки.")]
    [SerializeField] private Image[] fadeImages;

    [Tooltip("Длительность плавного скрытия (в секундах).")]
    [SerializeField] private float fadeDuration = 3f;

    [Tooltip("Минимальное время показа экрана загрузки (в секундах), чтобы успела отыграть анимация.")]
    [SerializeField] private float minDisplayTime = 3f;

    private bool loadCompleted = false;
    private bool steamErrorDisplayed = false;

    private void Start()
    {
        // Не уничтожаем объект при смене сцены, чтобы он дожил до fade-out поверх Menu
        DontDestroyOnLoad(gameObject);
        StartCoroutine(LoadMenuAsync());
    }

    private IEnumerator LoadMenuAsync()
    {
        float startTime = Time.time;

        // Если при инициализации Steam уже произошла ошибка,
        // не пытаемся загружать сцену Menu и просто показываем ошибку.
        if (SteamInitializer.HasSteamError)
        {
            string msg = string.IsNullOrEmpty(SteamInitializer.SteamErrorMessage)
                ? "Ошибка инициализации Steam. Игра не может быть запущена."
                : SteamInitializer.SteamErrorMessage;

            ShowError(msg);
            yield break;
        }

        AsyncOperation op;
        try
        {
            op = SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Single);
        }
        catch (System.SystemException e)
        {
            ShowError($"Ошибка загрузки сцены \"{menuSceneName}\": {e.Message}");
            yield break;
        }

        if (op == null)
        {
            ShowError($"Не удалось начать загрузку сцены \"{menuSceneName}\".");
            yield break;
        }

        // Сцена активируется сразу после завершения загрузки,
        // а этот объект останется и выполнит fade-out.
        op.allowSceneActivation = true;

        // Основной цикл загрузки
        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f); // op.progress ∈ [0,0.9], нормализуем до [0,1]
            UpdateProgressUI(progress);
            CheckSteamError();
            yield return null;
        }

        loadCompleted = true;

        // Гарантируем минимальное время отображения экрана загрузки
        float elapsed = Time.time - startTime;
        if (elapsed < minDisplayTime)
        {
            float extra = minDisplayTime - elapsed;
            float t = 0f;
            while (t < extra)
            {
                t += Time.deltaTime;
                // Плавно дотягиваем прогресс до 1
                UpdateProgressUI(1f);
                yield return null;
            }
        }

        // Завершаем прогресс и запускаем плавное скрытие
        UpdateProgressUI(1f);
        CheckSteamError();
        StartCoroutine(FadeOutAndDestroy());
    }

    private void UpdateProgressUI(float progress)
    {
        if (progressImage != null)
        {
            progressImage.fillAmount = Mathf.Clamp01(progress);
        }

        if (statusText != null)
        {
            statusText.color = normalTextColor;

            if (loadingMessages != null && loadingMessages.Length > 0)
            {
                float clamped = Mathf.Clamp01(progress);
                int index = Mathf.FloorToInt(clamped * loadingMessages.Length);
                if (index >= loadingMessages.Length)
                    index = loadingMessages.Length - 1;

                statusText.text = loadingMessages[index];
            }
        }
    }

    private void ShowError(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = errorTextColor;
        }

        if (progressImage != null)
        {
            progressImage.fillAmount = 0f;
        }
    }

    private void CheckSteamError()
    {
        if (steamErrorDisplayed)
            return;

        // Если SteamInitializer зафиксировал ошибку — показываем её в статусе
        if (SteamInitializer.HasSteamError && !string.IsNullOrEmpty(SteamInitializer.SteamErrorMessage))
        {
            ShowError(SteamInitializer.SteamErrorMessage);
            steamErrorDisplayed = true;
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        if (fadeImages != null && fadeImages.Length > 0 && fadeDuration > 0f)
        {
            float t = 0f;

            // Сохраняем исходные цвета
            Color[] initialColors = new Color[fadeImages.Length];
            for (int i = 0; i < fadeImages.Length; i++)
            {
                if (fadeImages[i] != null)
                {
                    initialColors[i] = fadeImages[i].color;
                }
            }

            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeDuration);
                float alpha = Mathf.Lerp(1f, 0f, k);

                for (int i = 0; i < fadeImages.Length; i++)
                {
                    Image img = fadeImages[i];
                    if (img == null)
                        continue;

                    Color c = initialColors[i];
                    c.a = alpha * c.a;
                    img.color = c;
                }

                yield return null;
            }
        }

        // Удаляем объект после завершения fade-out
        Destroy(gameObject);
    }
}