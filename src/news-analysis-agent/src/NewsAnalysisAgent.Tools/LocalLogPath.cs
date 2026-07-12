namespace NewsAnalysisAgent.Tools;

internal static class LocalLogPath
{
    public static string For(params string[] segments) => Path.Combine([FindWritableRoot(), .. segments]);

    private static string FindWritableRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        var configured = Environment.GetEnvironmentVariable("NEXUS6_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home, ".nexus6");
        }

        return Directory.GetCurrentDirectory();
    }
}
