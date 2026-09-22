namespace GeekFlashToolX.Services;

/// <summary>Filesystem locations shared by the logger, archive, and crash reporter.</summary>
public static class LogPaths
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeekFlashTool-X", "Logs");
}
