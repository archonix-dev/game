using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// NetworkBehaviour для синхронизации данных лобби между клиентами и сервером.
/// </summary>
public class LobbyNetworkManager : NetworkBehaviour
{
    [Header("Настройки лобби")]
    [Tooltip("Максимальное количество игроков")]
    public NetworkVariable<int> maxPlayers = new NetworkVariable<int>(
        8, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Tooltip("Пароль лобби")]
    public NetworkVariable<FixedString32Bytes> lobbyPassword = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Tooltip("Включены ли читы")]
    public NetworkVariable<bool> cheatsEnabled = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static LobbyNetworkManager instance;
    public static LobbyNetworkManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LobbyNetworkManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Подписываемся на изменения сетевых переменных
        maxPlayers.OnValueChanged += OnMaxPlayersChanged;
        lobbyPassword.OnValueChanged += OnLobbyPasswordChanged;
        cheatsEnabled.OnValueChanged += OnCheatsEnabledChanged;
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        maxPlayers.OnValueChanged -= OnMaxPlayersChanged;
        lobbyPassword.OnValueChanged -= OnLobbyPasswordChanged;
        cheatsEnabled.OnValueChanged -= OnCheatsEnabledChanged;
        
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Инициализирует настройки лобби (вызывается только на сервере)
    /// </summary>
    public void InitializeLobby(int maxPlayersValue, string password)
    {
        if (!IsServer)
        {
            Debug.LogWarning("InitializeLobby может быть вызван только на сервере!");
            return;
        }

        maxPlayers.Value = maxPlayersValue;
        lobbyPassword.Value = new FixedString32Bytes(password);
        cheatsEnabled.Value = false;
    }

    /// <summary>
    /// Устанавливает настройки лобби (вызывается только на сервере)
    /// </summary>
    public void SetLobbySettings(int maxPlayersValue, bool cheatsEnabledValue, string password)
    {
        if (!IsServer)
        {
            Debug.LogWarning("SetLobbySettings может быть вызван только на сервере!");
            return;
        }

        maxPlayers.Value = maxPlayersValue;
        cheatsEnabled.Value = cheatsEnabledValue;
        lobbyPassword.Value = new FixedString32Bytes(password);
        
        Debug.Log($"Настройки лобби обновлены: Макс игроков={maxPlayersValue}, Читы={cheatsEnabledValue}, Пароль={password}");
    }

    void OnMaxPlayersChanged(int oldValue, int newValue)
    {
        Debug.Log($"Максимальное количество игроков изменено: {oldValue} -> {newValue}");
    }

    void OnLobbyPasswordChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        Debug.Log($"Пароль лобби изменен: {oldValue} -> {newValue}");
    }

    void OnCheatsEnabledChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"Читы {(newValue ? "включены" : "выключены")}");
    }

    /// <summary>
    /// Проверяет пароль лобби
    /// </summary>
    public bool CheckPassword(string password)
    {
        if (string.IsNullOrEmpty(lobbyPassword.Value.ToString()))
        {
            return true; // Если пароль не установлен, доступ открыт
        }
        
        return lobbyPassword.Value.ToString() == password;
    }

    /// <summary>
    /// Получает максимальное количество игроков
    /// </summary>
    public int GetMaxPlayers()
    {
        return maxPlayers.Value;
    }

    /// <summary>
    /// Получает пароль лобби
    /// </summary>
    public string GetLobbyPassword()
    {
        return lobbyPassword.Value.ToString();
    }

    /// <summary>
    /// Проверяет, включены ли читы
    /// </summary>
    public bool AreCheatsEnabled()
    {
        return cheatsEnabled.Value;
    }
}

