#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Popup window for configuring logger filters.
    /// Can be shown from any LoggableEditorWindow.
    /// </summary>
    public class LogFilterPopup : PopupWindowContent
    {
        private readonly Logger _logger;
        private readonly Action _onSettingsChanged;

        private LogLevel _levelMask;
        private bool _consoleOutput;
        private bool _includeChildren;

        public LogFilterPopup(Logger logger, Action onSettingsChanged = null)
        {
            _logger = logger;
            _onSettingsChanged = onSettingsChanged;
            _levelMask = logger?.LevelMask ?? LogLevel.All;
            _consoleOutput = logger?.ConsoleOutput ?? false;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(280, 320);
        }

        public override void OnGUI(Rect rect)
        {
            if (_logger == null)
            {
                EditorGUILayout.LabelField("No logger available");
                return;
            }

            EditorGUILayout.LabelField($"Logger: {_logger.Name}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Enable toggle
            var enabled = EditorGUILayout.Toggle("Enabled", _logger.Enabled);
            if (enabled != _logger.Enabled)
            {
                _logger.Enabled = enabled;
                _onSettingsChanged?.Invoke();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log Levels", EditorStyles.boldLabel);

            // Level toggles
            foreach (LogLevel level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            {
                var isEnabled = (_levelMask & level) != 0;
                var newEnabled = EditorGUILayout.Toggle(level.ToLongString(), isEnabled);
                
                if (newEnabled != isEnabled)
                {
                    if (newEnabled)
                        _levelMask |= level;
                    else
                        _levelMask &= ~level;

                    _logger.LevelMask = _levelMask;
                    _onSettingsChanged?.Invoke();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            // Console output
            var newConsoleOutput = EditorGUILayout.Toggle("Unity Console", _consoleOutput);
            if (newConsoleOutput != _consoleOutput)
            {
                _consoleOutput = newConsoleOutput;
                _logger.ConsoleOutput = _consoleOutput;
                _onSettingsChanged?.Invoke();
            }

            // Include source info
            var includeSource = EditorGUILayout.Toggle("Include Source Info", _logger.IncludeSourceInfo);
            if (includeSource != _logger.IncludeSourceInfo)
            {
                _logger.IncludeSourceInfo = includeSource;
                _onSettingsChanged?.Invoke();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                _logger.Clear();
            }
            if (GUILayout.Button("Clear All"))
            {
                _logger.ClearAll();
            }
            EditorGUILayout.EndHorizontal();

            // Child loggers
            if (_logger.Children.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField($"Children ({_logger.Children.Count})", EditorStyles.boldLabel);

                _includeChildren = EditorGUILayout.Toggle("Apply to Children", _includeChildren);

                if (_includeChildren)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Enable All"))
                    {
                        _logger.Enable(true);
                        _onSettingsChanged?.Invoke();
                    }
                    if (GUILayout.Button("Disable All"))
                    {
                        _logger.Disable(true);
                        _onSettingsChanged?.Invoke();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            // Log count
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Buffer: {_logger.Buffer.Count}/{_logger.Buffer.Capacity} entries", EditorStyles.miniLabel);
        }
    }
}
#endif
