using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ClientNetworkTransform networkTransform;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    
    [Header("Player Settings")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private Color playerColor = Color.white;
    
    // Сетевые переменные
    private NetworkVariable<FixedString64Bytes> networkPlayerName = new NetworkVariable<FixedString64Bytes>(
        "Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(
        Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Инициализируем компоненты
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
            
        if (networkTransform == null)
            networkTransform = GetComponent<ClientNetworkTransform>();
            
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
            
        if (audioListener == null)
            audioListener = GetComponentInChildren<AudioListener>();
        
        // Настраиваем камеру и аудио только для владельца
        if (IsOwner)
        {
            SetupOwnerPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
        
        // Подписываемся на изменения сетевых переменных
        networkPlayerName.OnValueChanged += OnPlayerNameChanged;
        networkPlayerColor.OnValueChanged += OnPlayerColorChanged;
        
        // Устанавливаем начальные значения
        if (IsOwner)
        {
            networkPlayerName.Value = new FixedString64Bytes(playerName);
            networkPlayerColor.Value = playerColor;
        }
    }
    
    void SetupOwnerPlayer()
    {
        // Включаем камеру и аудио только для владельца
        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            playerCamera.tag = "MainCamera";
        }
        
        if (audioListener != null)
        {
            audioListener.enabled = true;
        }
        
        // Включаем управление игроком
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        Debug.Log($"Local player spawned with ID: {OwnerClientId}");
    }
    
    void SetupRemotePlayer()
    {
        // Отключаем камеру и аудио для других игроков
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }
        
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        
        // Отключаем управление для других игроков
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        Debug.Log($"Remote player spawned with ID: {OwnerClientId}");
    }
    
    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        networkPlayerName.OnValueChanged -= OnPlayerNameChanged;
        networkPlayerColor.OnValueChanged -= OnPlayerColorChanged;
        
        base.OnNetworkDespawn();
    }
    
    void OnPlayerNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        playerName = newName.ToString();
        Debug.Log($"Player {OwnerClientId} name changed to: {newName}");
    }
    
    void OnPlayerColorChanged(Color oldColor, Color newColor)
    {
        playerColor = newColor;
        Debug.Log($"Player {OwnerClientId} color changed to: {newColor}");
        
        // Применяем цвет к игроку (например, к материалу)
        ApplyPlayerColor(newColor);
    }
    
    void ApplyPlayerColor(Color color)
    {
        // Находим все рендереры и применяем цвет
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }
    
    // Методы для изменения имени и цвета (только для владельца)
    [ServerRpc(RequireOwnership = true)]
    public void SetPlayerNameServerRpc(FixedString64Bytes newName)
    {
        networkPlayerName.Value = newName;
    }
    
    [ServerRpc(RequireOwnership = true)]
    public void SetPlayerColorServerRpc(Color newColor)
    {
        networkPlayerColor.Value = newColor;
    }
    
    // Публичные свойства для доступа к данным игрока
    public string PlayerName => networkPlayerName.Value.ToString();
    public Color PlayerColor => networkPlayerColor.Value;
    public ulong PlayerId => OwnerClientId;
    
    // Метод для получения информации об игроке
    public string GetPlayerInfo()
    {
        return $"Player {PlayerId}: {PlayerName}";
    }
}
