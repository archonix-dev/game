using UnityEngine;

/// <summary>
/// Тестовый компонент для проверки SimpleDestruction системы
/// </summary>
public class SimpleDestructionTester : MonoBehaviour
{
    [Header("Настройки теста")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private KeyCode destroyKey = KeyCode.Space;
    
    [Header("Параметры разрушения")]
    [SerializeField] private int fragmentCount = 10;
    [SerializeField] private float explosionForce = 7f;
    [SerializeField] private bool useSimpleMode = false;

    void Update()
    {
        if (Input.GetKeyDown(destroyKey))
        {
            if (targetObject != null)
            {
                TestDestruction();
            }
            else
            {
                Debug.LogWarning("Target Object не назначен!");
            }
        }
    }

    private void TestDestruction()
    {
        if (SimpleDestructionManager.Instance == null)
        {
            Debug.LogError("SimpleDestructionManager не найден в сцене!");
            return;
        }

        Vector3 explosionCenter = targetObject.transform.position;

        if (useSimpleMode)
        {
            SimpleDestructionManager.Instance.DestroyObjectSimple(
                targetObject,
                fragmentCount,
                explosionForce,
                explosionCenter,
                null,
                3f
            );
        }
        else
        {
            SimpleDestructionManager.Instance.DestroyObject(
                targetObject,
                fragmentCount,
                explosionForce,
                explosionCenter,
                null,
                3f
            );
        }

        targetObject = null;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label($"Нажмите [{destroyKey}] для разрушения");
        GUILayout.Label($"Режим: {(useSimpleMode ? "Простые кубы" : "Оригинальный меш")}");
        GUILayout.Label($"Осколков: {fragmentCount}, Сила: {explosionForce}");
        GUILayout.EndArea();
    }
}

