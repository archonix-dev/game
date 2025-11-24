using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет экраном загрузки и сохраняется между сценами.
/// </summary>
public class LobbyLoadingController : MonoBehaviour
{
    private static LobbyLoadingController instance;

    [Header("Animation Settings")]
    [Tooltip("Аниматор для проигрывания загрузочной анимации")]
    [SerializeField] private Animator loadingAnimator;

    [Tooltip("Название клипа анимации загрузки")]
    [SerializeField] private string loadingAnimationName = "loadingmainscene";

    [Tooltip("Время (в секундах) перед запуском загрузки сцены")]
    [SerializeField] private float loadStartTime = 3f;

    [Tooltip("Общее время (в секундах) до скрытия объекта")]
    [SerializeField] private float hideTime = 8f;

    private bool isLoading = false;
    private Coroutine loadingCoroutine;
    private AnimatorStateInfo cachedState;

    public static LobbyLoadingController Instance => instance;

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
    }

    /// <summary>
    /// Запускает загрузку для хоста (создателя лобби).
    /// </summary>
    public void StartHostLoadingSequence(LobbyManager lobbyManager)
    {
        StartLoadingSequenceInternal(lobbyManager, true, null);
    }

    /// <summary>
    /// Запускает загрузку для хоста с произвольным действием при готовности.
    /// </summary>
    public void StartHostLoadingSequence(Action hostAction)
    {
        StartLoadingSequenceInternal(null, true, hostAction);
    }

    /// <summary>
    /// Запускает загрузку для клиента, присоединившегося к лобби.
    /// </summary>
    public void StartClientLoadingSequence()
    {
        StartLoadingSequenceInternal(null, false, null);
    }
    
    private void StartLoadingSequenceInternal(LobbyManager lobbyManager, bool isHost, Action hostActionOverride)
    {
        if (isLoading)
        {
            Debug.Log("[LobbyLoadingController] Загрузка уже запущена, повторный запуск пропущен.");
            return;
        }

        if (isHost && lobbyManager == null)
        {
            Debug.LogError("[LobbyLoadingController] LobbyManager не назначен для хоста!");
            return;
        }

        if (loadingAnimator == null)
        {
            loadingAnimator = GetComponent<Animator>();
            if (loadingAnimator == null)
            {
                Debug.LogError("[LobbyLoadingController] Аниматор не найден!");
                return;
            }
        }

        gameObject.SetActive(true);
        loadingCoroutine = StartCoroutine(LoadingRoutine(lobbyManager, isHost, hostActionOverride));
    }

    IEnumerator LoadingRoutine(LobbyManager lobbyManager, bool isHost, Action hostActionOverride)
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

        if (isHost)
        {
            if (hostActionOverride != null)
            {
                hostActionOverride.Invoke();
            }
            else if (lobbyManager != null)
            {
                lobbyManager.StartGame();
            }
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
        loadingCoroutine = null;
        isLoading = false;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}

