using Microsoft.Extensions.Logging;

namespace LocalMovieVault.Web.Services;

public sealed class SizeLimitedFileLoggerProvider : ILoggerProvider
{
    private readonly SizeLimitedFileLogWriter _writer;

    public SizeLimitedFileLoggerProvider(string path, long maxBytes)
    {
        _writer = new SizeLimitedFileLogWriter(path, maxBytes);
    }

    public ILogger CreateLogger(string categoryName)
        => new SizeLimitedFileLogger(categoryName, _writer);

    public void Dispose()
    {
    }

    private sealed class SizeLimitedFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly SizeLimitedFileLogWriter _writer;

        public SizeLimitedFileLogger(string categoryName, SizeLimitedFileLogWriter writer)
        {
            _categoryName = categoryName;
            _writer = writer;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var rendered = $"[{DateTimeOffset.UtcNow:O}] {logLevel,-11} {_categoryName}";
            if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
            {
                rendered += $" ({eventId.Id}:{eventId.Name})";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                rendered += " " + message;
            }

            if (exception is not null)
            {
                rendered += Environment.NewLine + exception;
            }

            try
            {
                _writer.WriteLine(rendered);
            }
            catch
            {
                // File logging should never prevent the app from continuing.
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
