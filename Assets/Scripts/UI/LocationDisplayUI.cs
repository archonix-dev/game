using System.Collections;
using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// Управляет отображением текущей локации игрока в UI с эффектом печати.
/// Работает локально на каждом клиенте — скрипт подписывается на события зон.
/// </summary>
public class LocationDisplayUI : MonoBehaviour
{
    private static LocationDisplayUI instance;

    [Header("UI")]
    [SerializeField] private Text locationLabel;

    [Header("Typewriter")]
    [SerializeField, Min(0f)] private float characterDelay = 0.035f;
    [SerializeField] private string prefix = "> ";

    private Coroutine typewriterRoutine;
    private string currentLocation = string.Empty;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[LocationDisplayUI] Второй экземпляр уничтожен.");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void OnEnable()
    {
        LocationZone.OnLocalPlayerEnterZone += HandleZoneChanged;
    }

    void OnDisable()
    {
        LocationZone.OnLocalPlayerEnterZone -= HandleZoneChanged;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Показывает локацию из другого скрипта напрямую.
    /// </summary>
    public static void ShowLocation(string locationName)
    {
        if (instance == null)
        {
            Debug.LogWarning("[LocationDisplayUI] Экземпляр не найден в сцене.");
            return;
        }

        instance.HandleZoneChanged(locationName);
    }

    void HandleZoneChanged(string locationName)
    {
        if (locationLabel == null)
        {
            Debug.LogWarning("[LocationDisplayUI] TMP_Text не назначен.");
            return;
        }

        if (string.IsNullOrEmpty(locationName) || currentLocation == locationName)
        {
            return;
        }

        currentLocation = locationName;

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
        }

        typewriterRoutine = StartCoroutine(TypewriterRoutine($"{prefix}{locationName}"));
    }

    IEnumerator TypewriterRoutine(string message)
    {
        locationLabel.text = string.Empty;

        foreach (char c in message)
        {
            locationLabel.text += c;

            if (characterDelay > 0f)
            {
                yield return new WaitForSeconds(characterDelay);
            }
            else
            {
                yield return null;
            }
        }
    }
}

