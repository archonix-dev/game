using System;
using UnityEngine;

/// <summary>
/// Класс для хранения данных мода
/// </summary>
[System.Serializable]
public class ModData
{
    public string modFilePath;          // Путь к zip файлу мода
    public string modName;              // Название мода (Name_mod)
    public string modVersion;           // Версия мода (Version_mod)
    public string gameVersion;          // Версия игры, для которой мод (Version_mod_game)
    public Sprite modLogo;              // Логотип мода (logo_mod.png)
    public VersionCompatibility compatibility; // Совместимость версии
    
    public ModData(string filePath, string name, string version, string gameVer, Sprite logo)
    {
        modFilePath = filePath;
        modName = name;
        modVersion = version;
        gameVersion = gameVer;
        modLogo = logo;
        compatibility = VersionCompatibility.Unknown;
    }
}

/// <summary>
/// Совместимость версии мода с игрой
/// </summary>
public enum VersionCompatibility
{
    Unknown,         // Неизвестно (не удалось определить)
    Compatible,      // Совместим
    Warning,         // Предупреждение (незначительное отличие)
    Incompatible     // Несовместим (кардинальное отличие)
}

