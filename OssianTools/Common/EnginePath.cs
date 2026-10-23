namespace OssianForge.Tools;

internal static class EnginePath
{
    /// <summary>The engine's assembly name: its apphost is OssianForge.exe and it loads OssianForge.dll.</summary>
    public const string EngineName = "OssianForge";

    /// <summary>Nearest folder above the exe (then above the cwd) that contains OssianForge.dll.</summary>
    public static string? Find()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, EngineName + ".dll"))) return dir.FullName;
        return null;
    }
}
