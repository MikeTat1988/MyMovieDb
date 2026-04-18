namespace MyMovieDB.Tray;

internal static class TrayLog
{
    private static readonly object Sync = new();
    private static string? _path;

    public static void Configure(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--log-file", StringComparison.OrdinalIgnoreCase))
            {
                _path = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(_path))
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyMovieDB");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "tray.log");
        }
    }

    public static void Write(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_path)) return;
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(_path!, line);
            }
        }
        catch
        {
        }
    }
}
