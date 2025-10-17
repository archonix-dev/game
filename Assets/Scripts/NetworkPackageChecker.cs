using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Скрипт для проверки установки Unity Netcode пакета
/// </summary>
public class NetworkPackageChecker : MonoBehaviour
{
    void Start()
    {
        CheckNetcodePackage();
    }
    
    void CheckNetcodePackage()
    {
        Debug.Log("=== ПРОВЕРКА ПАКЕТА UNITY NETCODE ===");
        
        // Проверяем доступность основных классов
        try
        {
            var networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager != null)
            {
                Debug.Log("✓ NetworkManager найден в сцене");
            }
            else
            {
                Debug.Log("⚠ NetworkManager не найден в сцене");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"✗ Ошибка при поиске NetworkManager: {e.Message}");
        }
        
        // Проверяем версию пакета
        try
        {
            var version = typeof(NetworkManager).Assembly.GetName().Version;
            Debug.Log($"✓ Версия Unity Netcode: {version}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"✗ Ошибка при получении версии: {e.Message}");
        }
        
        // Проверяем доступность UnityTransport
        try
        {
            var transportType = typeof(Unity.Netcode.Transports.UTP.UnityTransport);
            Debug.Log("✓ UnityTransport доступен");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"✗ UnityTransport недоступен: {e.Message}");
        }
        
        Debug.Log("=== КОНЕЦ ПРОВЕРКИ ===");
    }
    
    [ContextMenu("Check Netcode Package")]
    void CheckNetcodePackageContextMenu()
    {
        CheckNetcodePackage();
    }
}
