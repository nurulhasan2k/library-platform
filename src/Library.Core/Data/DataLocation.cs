namespace Library.Core.Data;

/// <summary>Works out which data file to use, so every tool shares one file wherever it is started from.</summary>
public static class DataLocation
{
    public const string EnvironmentVariable = "LIBRARY_DATA_FILE";

    /// <summary>
    /// Order of precedence: the LIBRARY_DATA_FILE environment variable, then the configured path.
    /// A relative configured path is placed next to LibraryPlatform.sln (found by walking up from the current folder).
    /// </summary>
    public static string Resolve(string configuredPath)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return Path.GetFullPath(fromEnvironment);
        if (Path.IsPathRooted(configuredPath)) return configuredPath;

        var root = FindSolutionRoot(Directory.GetCurrentDirectory())
                   ?? FindSolutionRoot(AppContext.BaseDirectory)
                   ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(root, configuredPath));
    }

    private static string? FindSolutionRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "LibraryPlatform.sln"))) return dir.FullName;
        return null;
    }
}
