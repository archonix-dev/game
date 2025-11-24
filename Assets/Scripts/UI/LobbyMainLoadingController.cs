using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Отдельный контроллер загрузки для перехода со сцены Lobby на сцену Main.
/// </summary>
public class LobbyMainLoadingController : MonoBehaviour
{
    private static LobbyMainLoadingController instance;

    [Header("Animation Settings")]
    [Tooltip("Аниматор для проигрывания загрузочной анимации (Lobby -> Main)")]
    [SerializeField] private Animator loadingAnimator;

    [Tooltip("Название клипа анимации загрузки")]
    [SerializeField] private string loadingAnimationName = "loadingmaintransition";

    [Tooltip("Время (в секундах) перед выполнением действия хоста")]
    [SerializeField] private float loadStartTime = 2f;

    [Tooltip("Общее время (в секундах) до скрытия объекта")]
    [SerializeField] private float hideTime = 8f;
    
    [Header("Visuals")]
    [Tooltip("Дополнительный объект, который показывается во время загрузки (например, текст 'Игра начинается')")]
    [SerializeField] private GameObject loadingDisplayRoot;

    private bool isLoading;
    private Coroutine loadingCoroutine;
    private AnimatorStateInfo cachedState;

    public static LobbyMainLoadingController Instance => instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingAnimator == null)
        {
            loadingAnimator = GetComponent<Animator>();
        }

        gameObject.SetActive(false);
        SetDisplayActive(false);
    }

    /// <summary>
    /// Запускает загрузку для хоста (создателя лобби), выполняя указанное действие.
    /// </summary>
    public void StartHostLoadingSequence(Action hostAction)
    {
        StartLoadingSequenceInternal(true, hostAction);
    }

    /// <summary>
    /// Запускает загрузку для клиента, присоединившегося к лобби.
    /// </summary>
    public void StartClientLoadingSequence()
    {
        StartLoadingSequenceInternal(false, null);
    }

    void StartLoadingSequenceInternal(bool isHost, Action hostActionOverride)
    {
        if (isLoading)
        {
            Debug.Log("[LobbyMainLoadingController] Загрузка уже запущена, повторный запуск пропущен.");
            return;
        }

        if (loadingAnimator == null)
        {
            loadingAnimator = GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[LobbyMainLoadingController] Аниматор не найден!");
                return;
            }
        }

        gameObject.SetActive(true);
        SetDisplayActive(true);
        loadingCoroutine = StartCoroutine(LoadingRoutine(isHost, hostActionOverride));
    }

    IEnumerator LoadingRoutine(bool isHost, Action hostActionOverride)
    {
        isLoading = true;
        string initialScene = SceneManager.GetActiveScene().name;
        float elapsedTime = 0f;

        loadingAnimator.speed = 1f;
        loadingAnimator.Play(loadingAnimationName, 0, 0f);

        while (elapsedTime < loadStartTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cachedState = loadingAnimator.GetCurrentAnimatorStateInfo(0);
        loadingAnimator.speed = 0f;

        if (isHost && hostActionOverride != null)
        {
            hostActionOverride.Invoke();
        }

        while (SceneManager.GetActiveScene().name == initialScene)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        loadingAnimator.speed = 1f;
        loadingAnimator.Play(loadingAnimationName, 0, cachedState.normalizedTime);

        while (elapsedTime < hideTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
        SetDisplayActive(false);
        loadingCoroutine = null;
        isLoading = false;
    }
    
    void SetDisplayActive(bool value)
    {
        if (loadingDisplayRoot != null)
        {
            loadingDisplayRoot.SetActive(value);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}

