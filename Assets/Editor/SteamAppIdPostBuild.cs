using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

public static class SteamAppIdPostBuild
{
    // Подставьте ваш настоящий AppID
    private const string SteamAppId = "480";
    private const string FileName = "steam_appid.txt";

    [PostProcessBuild]
    public static void WriteSteamAppId(BuildTarget target, string pathToBuiltProject)
    {
        // pathToBuiltProject указывает на exe/апк; нам нужна директория рядом с ним
        string buildDirectory = Path.GetDirectoryName(pathToBuiltProject);
        if (string.IsNullOrEmpty(buildDirectory)) return;

        string targetPath = Path.Combine(buildDirectory, FileName);
        File.WriteAllText(targetPath, SteamAppId);
    }
}