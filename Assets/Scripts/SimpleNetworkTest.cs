using UnityEngine;

/// <summary>
/// Простой тест для проверки доступности Unity Netcode
/// </summary>
public class SimpleNetworkTest : MonoBehaviour
{
    void Start()
    {
        TestNetcodeAvailability();
    }
    
    void TestNetcodeAvailability()
    {
        Debug.Log("=== ТЕСТ ДОСТУПНОСТИ UNITY NETCODE ===");
        
        // Проверяем доступность через рефлексию
        System.Type networkManagerType = System.Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
        if (networkManagerType != null)
        {
            Debug.Log("✓ NetworkManager тип найден через рефлексию");
        }
        else
        {
            Debug.LogError("✗ NetworkManager тип НЕ найден через рефлексию");
        }
        
        System.Type networkBehaviourType = System.Type.GetType("Unity.Netcode.NetworkBehaviour, Unity.Netcode.Runtime");
        if (networkBehaviourType != null)
        {
            Debug.Log("✓ NetworkBehaviour тип найден через рефлексию");
        }
        else
        {
            Debug.LogError("✗ NetworkBehaviour тип НЕ найден через рефлексию");
        }
        
        // Проверяем загруженные сборки
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        bool netcodeAssemblyFound = false;
        
        foreach (var assembly in assemblies)
        {
            if (assembly.FullName.Contains("Unity.Netcode"))
            {
                Debug.Log($"✓ Найдена сборка Unity Netcode: {assembly.FullName}");
                netcodeAssemblyFound = true;
            }
        }
        
        if (!netcodeAssemblyFound)
        {
            Debug.LogError("✗ Сборка Unity.Netcode не найдена в загруженных сборках");
        }
        
        Debug.Log("=== КОНЕЦ ТЕСТА ===");
    }
    
    [ContextMenu("Test Netcode")]
    void TestNetcodeContextMenu()
    {
        TestNetcodeAvailability();
    }
}
