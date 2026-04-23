using System.Text;

namespace LocalMovieVault.Web.Services;

public sealed class SizeLimitedFileLogWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly string _activePath;
    private readonly string _archivePath;
    private readonly long _maxBytes;

    public SizeLimitedFileLogWriter(string activePath, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(activePath))
        {
            throw new ArgumentException("Log path is required.", nameof(activePath));
        }

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max log size must be positive.");
        }

        _activePath = activePath;
        _archivePath = activePath + ".previous";
        _maxBytes = maxBytes;
    }

    public void WriteLine(string message)
    {
        var line = message + Environment.NewLine;
        var lineBytes = Utf8NoBom.GetByteCount(line);

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_activePath)!);

            var currentLength = File.Exists(_activePath) ? new FileInfo(_activePath).Length : 0L;
            if (currentLength > 0 && currentLength + lineBytes > _maxBytes)
            {
                RotateUnsafe();
            }

            File.AppendAllText(_activePath, line, Utf8NoBom);
        }
    }

    private void RotateUnsafe()
    {
        if (File.Exists(_archivePath))
        {
            File.Delete(_archivePath);
        }

        if (File.Exists(_activePath))
        {
            File.Move(_activePath, _archivePath);
        }
    }
}
