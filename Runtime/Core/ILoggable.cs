using System.Runtime.CompilerServices;

namespace ConvenientLogger
{
    /// <summary>
    /// Interface for classes that support hierarchical logging.
    /// Inherit from LoggableBase for automatic implementation.
    /// </summary>
    public interface ILoggable
    {
        /// <summary>
        /// The logger instance for this object.
        /// </summary>
        Logger Logger { get; }

        /// <summary>
        /// Whether logging is currently enabled.
        /// </summary>
        bool IsLoggingEnabled { get; }

        /// <summary>
        /// Registers this object's logger as a child of a parent logger.
        /// </summary>
        void RegisterToParentLogger(Logger parentLogger);

        /// <summary>
        /// Deregisters this object's logger from its parent.
        /// </summary>
        void DeregisterFromParentLogger();
    }

    /// <summary>
    /// Base class providing ILoggable implementation with minimal boilerplate.
    /// Derive from this class to get automatic logging support.
    /// </summary>
    public abstract class LoggableBase : ILoggable
    {
        private Logger _logger;

        /// <inheritdoc/>
        public Logger Logger => _logger;

        /// <inheritdoc/>
        public bool IsLoggingEnabled => _logger?.Enabled ?? false;

        /// <summary>
        /// Initializes the logger. Call this in the derived class constructor.
        /// </summary>
        /// <param name="name">Logger name (typically the class name)</param>
        /// <param name="enabled">Initial enabled state</param>
        /// <param name="bufferCapacity">Maximum log entries to retain</param>
        protected void InitializeLogger(string name, bool enabled = false, int bufferCapacity = 500)
        {
            _logger = new Logger(name, bufferCapacity)
            {
                Enabled = enabled
            };
        }

        /// <inheritdoc/>
        public void RegisterToParentLogger(Logger parentLogger)
        {
            if (_logger == null || parentLogger == null) return;
            parentLogger.AddChild(_logger);
        }

        /// <inheritdoc/>
        public void DeregisterFromParentLogger()
        {
            _logger?.Parent?.RemoveChild(_logger);
        }

        /// <summary>
        /// Enables logging for this object.
        /// </summary>
        public void EnableLogging(bool consoleOutput = false)
        {
            if (_logger == null) return;
            _logger.Enabled = true;
            _logger.ConsoleOutput = consoleOutput;
        }

        /// <summary>
        /// Disables logging for this object.
        /// </summary>
        public void DisableLogging()
        {
            if (_logger == null) return;
            _logger.Enabled = false;
        }

        #region Logging Convenience Methods

        /// <summary>
        /// Checks if logging at the given level would be recorded.
        /// Use to avoid expensive string formatting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool ShouldLog(LogLevel level) => _logger?.ShouldLog(level) ?? false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogTrace(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Trace(message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogDebug(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Debug(message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogInfo(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Info(message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogWarning(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Warning(message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogError(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Error(message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void LogCritical(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => _logger?.Critical(message, filePath, memberName, lineNumber);

        #endregion
    }
}
