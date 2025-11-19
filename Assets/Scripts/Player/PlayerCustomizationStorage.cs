using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Буфер для сохранения выбранных игроком настроек (ник, цвет, статус) между сценами.
/// Нужен для случаев, когда LobbyPlayer уничтожается при переходе на сцену Lobby,
/// но другим системам (например, чату) все еще требуются эти данные.
/// </summary>
public static class PlayerCustomizationStorage
{
    public class PlayerCustomizationData
    {
        public string playerName;
        public int colorIndex;
        public bool isOwner;
        public ulong steamId;

        public Color PlayerColor
        {
            get
            {
                if (LobbyPlayer.PlayerColors != null &&
                    colorIndex >= 0 &&
                    colorIndex < LobbyPlayer.PlayerColors.Length)
                {
                    return LobbyPlayer.PlayerColors[colorIndex];
                }

                return Color.white;
            }
        }
    }

    private static readonly Dictionary<int, PlayerCustomizationData> dataByConnectionId =
        new Dictionary<int, PlayerCustomizationData>();

    private static readonly Dictionary<ulong, PlayerCustomizationData> dataBySteamId =
        new Dictionary<ulong, PlayerCustomizationData>();

    /// <summary>
    /// Обновляет или добавляет данные игрока на основании LobbyPlayer.
    /// </summary>
    public static void SaveFromLobbyPlayer(LobbyPlayer lobbyPlayer)
    {
        if (lobbyPlayer == null)
        {
            return;
        }

        PlayerCustomizationData data = new PlayerCustomizationData
        {
            playerName = lobbyPlayer.playerName,
            colorIndex = lobbyPlayer.playerColorIndex,
            isOwner = lobbyPlayer.isOwner,
            steamId = lobbyPlayer.steamID
        };

        int connectionId = lobbyPlayer.connectionToClient != null
            ? lobbyPlayer.connectionToClient.connectionId
            : -1;

        if (connectionId >= 0)
        {
            dataByConnectionId[connectionId] = data;
        }

        if (data.steamId != 0)
        {
            dataBySteamId[data.steamId] = data;
        }
    }

    /// <summary>
    /// Пытается получить данные игрока по connectionId.
    /// </summary>
    public static bool TryGetByConnectionId(int connectionId, out PlayerCustomizationData data)
    {
        return dataByConnectionId.TryGetValue(connectionId, out data);
    }

    /// <summary>
    /// Пытается получить данные игрока по SteamID.
    /// </summary>
    public static bool TryGetBySteamId(ulong steamId, out PlayerCustomizationData data)
    {
        data = null;
        if (steamId == 0)
        {
            return false;
        }

        return dataBySteamId.TryGetValue(steamId, out data);
    }

    /// <summary>
    /// Удаляет данные игрока по connectionId.
    /// </summary>
    public static void RemoveByConnectionId(int connectionId)
    {
        if (connectionId < 0)
        {
            return;
        }

        if (dataByConnectionId.TryGetValue(connectionId, out PlayerCustomizationData data))
        {
            if (data.steamId != 0)
            {
                dataBySteamId.Remove(data.steamId);
            }
        }

        dataByConnectionId.Remove(connectionId);
    }

    /// <summary>
    /// Удаляет данные по SteamID.
    /// </summary>
    public static void RemoveBySteamId(ulong steamId)
    {
        if (steamId == 0)
        {
            return;
        }

        if (dataBySteamId.TryGetValue(steamId, out PlayerCustomizationData data))
        {
            // Найдем запись по connectionId и удалим её тоже
            List<int> connectionIdsToRemove = null;
            foreach (KeyValuePair<int, PlayerCustomizationData> pair in dataByConnectionId)
            {
                if (ReferenceEquals(pair.Value, data))
                {
                    if (connectionIdsToRemove == null)
                    {
                        connectionIdsToRemove = new List<int>();
                    }

                    connectionIdsToRemove.Add(pair.Key);
                }
            }

            if (connectionIdsToRemove != null)
            {
                foreach (int id in connectionIdsToRemove)
                {
                    dataByConnectionId.Remove(id);
                }
            }
        }

        dataBySteamId.Remove(steamId);
    }

    /// <summary>
    /// Полностью очищает кеш (например, при перезапуске сессии).
    /// </summary>
    public static void Clear()
    {
        dataByConnectionId.Clear();
        dataBySteamId.Clear();
    }
}
