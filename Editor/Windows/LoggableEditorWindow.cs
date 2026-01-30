#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Base class for EditorWindows with integrated logging support.
    /// Provides a logger icon in the top-right corner for log filtering and export.
    /// </summary>
    public abstract class LoggableEditorWindow : EditorWindow, ILoggable
    {
        #region Fields

        private Logger _logger;
        private Button _loggerButton;
        private VisualElement _loggerIconContainer;
        
        // Persisted settings per window type
        private bool _loggingEnabled;
        private LogLevel _levelMask = LogLevel.All;
        private bool _consoleOutput;

        #endregion

        #region ILoggable Implementation

        public Logger Logger => _logger;
        public bool IsLoggingEnabled => _logger?.Enabled ?? false;

        public void RegisterToParentLogger(Logger parentLogger)
        {
            parentLogger?.AddChild(_logger);
        }

        public void DeregisterFromParentLogger()
        {
            _logger?.Parent?.RemoveChild(_logger);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Override to customize the logger name. Defaults to the window type name.
        /// </summary>
        protected virtual string LoggerName => GetType().Name;

        /// <summary>
        /// Override to set initial buffer capacity.
        /// </summary>
        protected virtual int LoggerBufferCapacity => 500;

        /// <summary>
        /// Override to set initial logging state.
        /// </summary>
        protected virtual bool InitialLoggingEnabled => false;

        /// <summary>
        /// Whether to show the logger icon in the toolbar. Override to hide.
        /// </summary>
        protected virtual bool ShowLoggerIcon => true;

        #endregion

        #region Unity Lifecycle

        protected virtual void OnEnable()
        {
            InitializeLogger();
            LoadLoggerSettings();
        }

        protected virtual void OnDisable()
        {
            SaveLoggerSettings();
        }

        protected virtual void CreateGUI()
        {
            if (ShowLoggerIcon)
            {
                CreateLoggerIcon();
            }
        }

        #endregion

        #region Logger Initialization

        private void InitializeLogger()
        {
            // Use GetOrCreate to avoid duplicate loggers
            _logger = LoggerRegistry.GetOrCreate(LoggerName, InitialLoggingEnabled);
        }

        private void LoadLoggerSettings()
        {
            var key = $"ConvenientLogger_{GetType().FullName}";
            _loggingEnabled = EditorPrefs.GetBool($"{key}_Enabled", InitialLoggingEnabled);
            _levelMask = (LogLevel)EditorPrefs.GetInt($"{key}_LevelMask", (int)LogLevel.All);
            _consoleOutput = EditorPrefs.GetBool($"{key}_ConsoleOutput", false);

            if (_logger != null)
            {
                _logger.Enabled = _loggingEnabled;
                _logger.LevelMask = _levelMask;
                _logger.ConsoleOutput = _consoleOutput;
            }
        }

        private void SaveLoggerSettings()
        {
            var key = $"ConvenientLogger_{GetType().FullName}";
            EditorPrefs.SetBool($"{key}_Enabled", _loggingEnabled);
            EditorPrefs.SetInt($"{key}_LevelMask", (int)_levelMask);
            EditorPrefs.SetBool($"{key}_ConsoleOutput", _consoleOutput);
        }

        #endregion

        #region Logger Icon UI

        private void CreateLoggerIcon()
        {
            // Create container for icon in top-right
            _loggerIconContainer = new VisualElement
            {
                name = "logger-icon-container",
                style =
                {
                    position = Position.Absolute,
                    top = 2,
                    right = 4,
                    width = 20,
                    height = 20,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            _loggerButton = new Button(OnLoggerIconClicked)
            {
                name = "logger-button",
                tooltip = "Logger Settings",
                style =
                {
                    width = 18,
                    height = 18,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    backgroundColor = Color.clear
                }
            };

            UpdateLoggerIcon();
            
            _loggerIconContainer.Add(_loggerButton);
            rootVisualElement.Add(_loggerIconContainer);
        }

        private void UpdateLoggerIcon()
        {
            if (_loggerButton == null) return;

            // Use Unicode symbols for the icon
            // Enabled: filled circle, Disabled: empty circle
            _loggerButton.text = _loggingEnabled ? "\u25CF" : "\u25CB";
            
            // Color based on state
            _loggerButton.style.color = _loggingEnabled 
                ? new Color(0.3f, 0.8f, 0.3f) // Green when enabled
                : new Color(0.5f, 0.5f, 0.5f); // Gray when disabled
            
            _loggerButton.tooltip = _loggingEnabled 
                ? $"Logger: ON ({_levelMask})\nClick to configure"
                : "Logger: OFF\nClick to enable";
        }

        private void OnLoggerIconClicked()
        {
            var menu = new GenericMenu();
            
            // Enable/Disable toggle
            menu.AddItem(
                new GUIContent("Logging Enabled"),
                _loggingEnabled,
                () => ToggleLogging());

            menu.AddSeparator("");

            // Log level toggles
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                if (level == LogLevel.None || level == LogLevel.All ||
                    level == LogLevel.Production || level == LogLevel.Development ||
                    level == LogLevel.ErrorsOnly)
                    continue;

                var levelCopy = level;
                var isEnabled = (_levelMask & level) != 0;
                menu.AddItem(
                    new GUIContent($"Levels/{level.ToLongString()}"),
                    isEnabled,
                    () => ToggleLogLevel(levelCopy));
            }

            menu.AddSeparator("");

            // Console output toggle
            menu.AddItem(
                new GUIContent("Output to Console"),
                _consoleOutput,
                () => ToggleConsoleOutput());

            menu.AddSeparator("");

            // Quick presets
            menu.AddItem(new GUIContent("Presets/All Levels"), _levelMask == LogLevel.All, () => SetLevelMask(LogLevel.All));
            menu.AddItem(new GUIContent("Presets/Development"), _levelMask == LogLevel.Development, () => SetLevelMask(LogLevel.Development));
            menu.AddItem(new GUIContent("Presets/Errors Only"), _levelMask == LogLevel.ErrorsOnly, () => SetLevelMask(LogLevel.ErrorsOnly));

            menu.AddSeparator("");

            // Actions
            menu.AddItem(new GUIContent("Clear Logs"), false, ClearLogs);
            menu.AddItem(new GUIContent("Export Logs..."), false, ExportLogs);
            menu.AddItem(new GUIContent("Copy Logs to Clipboard"), false, CopyLogsToClipboard);

            menu.ShowAsContext();
        }

        #endregion

        #region Logger Controls

        private void ToggleLogging()
        {
            _loggingEnabled = !_loggingEnabled;
            if (_logger != null)
            {
                _logger.Enabled = _loggingEnabled;
            }
            UpdateLoggerIcon();
            SaveLoggerSettings();
        }

        private void ToggleLogLevel(LogLevel level)
        {
            _levelMask ^= level;
            if (_logger != null)
            {
                _logger.LevelMask = _levelMask;
            }
            SaveLoggerSettings();
        }

        private void ToggleConsoleOutput()
        {
            _consoleOutput = !_consoleOutput;
            if (_logger != null)
            {
                _logger.ConsoleOutput = _consoleOutput;
            }
            SaveLoggerSettings();
        }

        private void SetLevelMask(LogLevel mask)
        {
            _levelMask = mask;
            if (_logger != null)
            {
                _logger.LevelMask = _levelMask;
            }
            SaveLoggerSettings();
        }

        private void ClearLogs()
        {
            _logger?.ClearAll();
            LogInfo("Logs cleared");
        }

        private void ExportLogs()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Logs",
                "",
                $"{LoggerName}_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt");

            if (string.IsNullOrEmpty(path)) return;

            var logs = _logger?.ExtractLogs() ?? "No logs available";
            System.IO.File.WriteAllText(path, logs);
            LogInfo($"Logs exported to: {path}");
        }

        private void CopyLogsToClipboard()
        {
            var logs = _logger?.ExtractLogs() ?? "No logs available";
            EditorGUIUtility.systemCopyBuffer = logs;
            LogInfo("Logs copied to clipboard");
        }

        #endregion

        #region Logging Convenience Methods

        protected void LogTrace(string message) => _logger?.Trace(message);
        protected void LogDebug(string message) => _logger?.Debug(message);
        protected void LogInfo(string message) => _logger?.Info(message);
        protected void LogWarning(string message) => _logger?.Warning(message);
        protected void LogError(string message) => _logger?.Error(message);
        protected void LogCritical(string message) => _logger?.Critical(message);

        /// <summary>
        /// Check before expensive string operations.
        /// </summary>
        protected bool ShouldLog(LogLevel level) => _logger?.ShouldLog(level) ?? false;

        #endregion
    }
}
#endif
