#if UNITY_EDITOR
using System;
using UnityEngine.UIElements;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Self-contained logger icon element that can be added to any VisualElement.
    /// 
    /// PREFERRED: Use the extension methods instead:
    ///   var logger = toolbar.AddLogger("MySystem/MyWindow");
    ///   var logger = toolbar.AddLogger(parentLogger, "ChildName");
    /// </summary>
    [Obsolete("Use LoggerUIExtensions.AddLogger() extension method instead")]
    public class LoggerIconElement : VisualElement
    {
        private readonly Logger _logger;

        /// <summary>
        /// The logger instance managed by this icon.
        /// </summary>
        public Logger Logger => _logger;

        /// <summary>
        /// Creates a logger icon for the specified logger path.
        /// </summary>
        public LoggerIconElement(string loggerPath, bool defaultEnabled = false, bool defaultConsoleOutput = true)
        {
            _logger = this.AddLogger(loggerPath, defaultEnabled);
            _logger.ConsoleOutput = defaultConsoleOutput;
        }

        /// <summary>
        /// Creates a logger icon using an existing logger instance.
        /// </summary>
        public LoggerIconElement(Logger logger, string prefsKey = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.AddLogger(logger);
        }
    }
    
    /// <summary>
    /// Legacy extension methods. Use LoggerUIExtensions instead.
    /// </summary>
    [Obsolete("Use LoggerUIExtensions.AddLogger() instead")]
    public static class LegacyLoggerUIExtensions
    {
        /// <summary>
        /// Use LoggerUIExtensions.AddLogger() instead.
        /// </summary>
        [Obsolete("Use AddLogger() instead")]
        public static Logger AddLoggerIcon(this VisualElement container, string loggerPath, bool defaultEnabled = false, bool defaultConsoleOutput = true)
        {
            var logger = container.AddLogger(loggerPath, defaultEnabled);
            logger.ConsoleOutput = defaultConsoleOutput;
            return logger;
        }

        /// <summary>
        /// Use LoggerUIExtensions.AddLogger() instead.
        /// </summary>
        [Obsolete("Use AddLogger() instead")]
        public static VisualElement AddLoggerIcon(this VisualElement container, Logger logger)
        {
            return container.AddLogger(logger);
        }

        /// <summary>
        /// Use LoggerUIExtensions.AddLogger(parent, childName) instead.
        /// </summary>
        [Obsolete("Use AddLogger(parent, childName) instead")]
        public static Logger AddChildLoggerIcon(this VisualElement container, Logger parent, string childName, bool defaultEnabled = false)
        {
            return container.AddLogger(parent, childName, defaultEnabled);
        }
    }
}
#endif
