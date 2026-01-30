using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ConvenientLogger
{
    /// <summary>
    /// Static logging utilities with conditional compilation support.
    /// Methods with [Conditional] attribute are completely stripped when the symbol is not defined.
    /// 
    /// Define CONVENIENT_LOGGER_ENABLED to enable logging.
    /// Define CONVENIENT_LOGGER_VERBOSE for trace-level logging.
    /// </summary>
    public static class Log
    {
        #region Conditional Logging (Zero-Allocation when disabled)

        /// <summary>
        /// Logs a trace message. Only compiled when CONVENIENT_LOGGER_VERBOSE is defined.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_VERBOSE")]
        public static void Trace(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Trace(message, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Logs a debug message. Only compiled when CONVENIENT_LOGGER_ENABLED is defined.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_ENABLED")]
        public static void Debug(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Debug(message, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Logs an info message. Only compiled when CONVENIENT_LOGGER_ENABLED is defined.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_ENABLED")]
        public static void Info(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Info(message, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Logs a warning message. Only compiled when CONVENIENT_LOGGER_ENABLED is defined.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_ENABLED")]
        public static void Warning(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Warning(message, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Logs an error message. Always compiled (errors should always be logged).
        /// </summary>
        public static void Error(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Error(message, filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Logs a critical message. Always compiled.
        /// </summary>
        public static void Critical(
            Logger logger,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            logger?.Critical(message, filePath, memberName, lineNumber);
        }

        #endregion

        #region Lazy Logging (Deferred message construction)

        /// <summary>
        /// Logs with deferred message construction - the messageFactory is only called if logging is enabled.
        /// Use this for expensive string operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LazyLog(
            Logger logger,
            LogLevel level,
            Func<string> messageFactory,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (logger == null || !logger.ShouldLog(level)) return;
            logger.Log(level, messageFactory(), filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Lazy debug logging - message is only constructed if debug logging is enabled.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_ENABLED")]
        public static void LazyDebug(
            Logger logger,
            Func<string> messageFactory,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (logger == null || !logger.ShouldLog(LogLevel.Debug)) return;
            logger.Debug(messageFactory(), filePath, memberName, lineNumber);
        }

        /// <summary>
        /// Lazy info logging - message is only constructed if info logging is enabled.
        /// </summary>
        [Conditional("CONVENIENT_LOGGER_ENABLED")]
        public static void LazyInfo(
            Logger logger,
            Func<string> messageFactory,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (logger == null || !logger.ShouldLog(LogLevel.Info)) return;
            logger.Info(messageFactory(), filePath, memberName, lineNumber);
        }

        #endregion

        #region Scoped Logging

        /// <summary>
        /// Creates a scope that logs entry and exit. Useful for timing operations.
        /// Usage: using (Log.Scope(logger, "MethodName")) { ... }
        /// </summary>
        public static LogScope Scope(
            Logger logger,
            string scopeName,
            LogLevel level = LogLevel.Debug,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            return new LogScope(logger, scopeName, level, filePath, memberName, lineNumber);
        }

        #endregion
    }

    /// <summary>
    /// Disposable scope that logs entry and exit with timing.
    /// Zero allocation when logging is disabled.
    /// </summary>
    public readonly struct LogScope : IDisposable
    {
        private readonly Logger _logger;
        private readonly string _scopeName;
        private readonly LogLevel _level;
        private readonly long _startTicks;
        private readonly string _filePath;
        private readonly string _memberName;
        private readonly int _lineNumber;
        private readonly bool _shouldLog;

        public LogScope(
            Logger logger,
            string scopeName,
            LogLevel level,
            string filePath,
            string memberName,
            int lineNumber)
        {
            _logger = logger;
            _scopeName = scopeName;
            _level = level;
            _filePath = filePath;
            _memberName = memberName;
            _lineNumber = lineNumber;
            
            // Check upfront if we should log - avoids allocation if disabled
            _shouldLog = logger?.ShouldLog(level) ?? false;
            
            if (_shouldLog)
            {
                _startTicks = Stopwatch.GetTimestamp();
                // Only allocate the interpolated string if we're actually logging
                // Use LogDirect to bypass ShouldLog check (we already checked)
                _logger.LogDirect(level, string.Concat(">>> ", scopeName, " BEGIN"), filePath, memberName, lineNumber);
            }
            else
            {
                _startTicks = 0;
            }
        }

        public void Dispose()
        {
            if (!_shouldLog) return;
            
            var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
            var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            
            // Use StringBuilderPool to avoid allocation
            var sb = StringBuilderPool.Get();
            try
            {
                sb.Append("<<< ");
                sb.Append(_scopeName);
                sb.Append(" END (");
                sb.Append(elapsedMs.ToString("F2"));
                sb.Append("ms)");
                // Use LogDirect to ensure END is logged even if logger was disabled mid-scope
                // The decision to log was made at scope creation time
                _logger.LogDirect(_level, sb.ToString(), _filePath, _memberName, _lineNumber);
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }
    }
}
