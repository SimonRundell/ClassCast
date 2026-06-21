namespace ClassCast.Common.Logging;

/// <summary>
/// Severity levels emitted by <see cref="Logger"/>.
/// </summary>
public enum LogLevel
{
    /// <summary>Diagnostic detail useful during development.</summary>
    Debug,
    /// <summary>Normal operational events.</summary>
    Info,
    /// <summary>Recoverable problems worth noting.</summary>
    Warn,
    /// <summary>Failures that prevented an operation from completing.</summary>
    Error
}

/// <summary>
/// A minimal thread-safe, dated log-file writer shared by both ClassCast
/// applications. One file is created per calendar day in a <c>logs</c> folder
/// beside the executable, named <c>ClassCast-yyyy-MM-dd.log</c>. Messages are
/// also echoed to the debug output window.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static string _component = "App";

    /// <summary>
    /// Configures the logger. Call once at application startup.
    /// </summary>
    /// <param name="component">Short tag identifying the application (e.g. "Teacher", "Student").</param>
    /// <param name="logDirectory">Optional override for the log directory. Defaults to <c>&lt;exe&gt;\logs</c>.</param>
    public static void Configure(string component, string? logDirectory = null)
    {
        lock (Gate)
        {
            _component = string.IsNullOrWhiteSpace(component) ? "App" : component;
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                _logDirectory = logDirectory!;
            }
        }
    }

    /// <summary>Writes an informational message.</summary>
    /// <param name="message">The text to log.</param>
    public static void Info(string message) => Write(LogLevel.Info, message, null);

    /// <summary>Writes a debug message.</summary>
    /// <param name="message">The text to log.</param>
    public static void Debug(string message) => Write(LogLevel.Debug, message, null);

    /// <summary>Writes a warning message.</summary>
    /// <param name="message">The text to log.</param>
    public static void Warn(string message) => Write(LogLevel.Warn, message, null);

    /// <summary>Writes an error message, optionally with an exception.</summary>
    /// <param name="message">The text to log.</param>
    /// <param name="ex">An optional exception whose details are appended.</param>
    public static void Error(string message, Exception? ex = null) => Write(LogLevel.Error, message, ex);

    /// <summary>
    /// Core write routine: formats the entry, appends it to today's log file, and
    /// echoes it to the debugger. All file access is serialised behind a lock.
    /// </summary>
    private static void Write(LogLevel level, string message, Exception? ex)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] [{_component}] {message}";
        if (ex is not null)
        {
            line += Environment.NewLine + ex;
        }

        System.Diagnostics.Debug.WriteLine(line);

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                string file = Path.Combine(_logDirectory, $"ClassCast-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never throw into application code; swallow IO errors.
            }
        }
    }
}
