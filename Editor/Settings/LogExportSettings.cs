#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Global settings for log export behavior.
    /// </summary>
    [FilePath("ConvenientLogger/ExportSettings.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class LogExportSettings : ScriptableSingleton<LogExportSettings>
    {
        #region Export Settings

        [Header("Auto-Export Settings")]
        [Tooltip("Automatically export logs when exiting Play Mode")]
        public bool autoExportOnPlayModeExit = false;

        [Tooltip("Directory for auto-exported logs (relative to project)")]
        public string autoExportDirectory = "Logs";

        [Tooltip("Log levels to include in auto-export")]
        public LogLevel autoExportLevelMask = LogLevel.All;

        [Tooltip("Maximum log files to keep in auto-export directory")]
        public int maxAutoExportFiles = 10;

        [Header("Manual Export Settings")]
        [Tooltip("Default directory for manual exports")]
        public string manualExportDirectory = "";

        [Tooltip("Include source file/line info in exports")]
        public bool includeSourceInfo = false;

        [Header("Time Range Export")]
        [Tooltip("Enable time range filtering for exports")]
        public bool useTimeRange = false;

        [Tooltip("Export logs from the last N minutes (0 = all)")]
        public int timeRangeMinutes = 30;

        #endregion

        #region Runtime State

        [NonSerialized] private DateTime? _playModeStartTime;

        #endregion

        #region Unity Callbacks

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            var settings = instance;

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    settings._playModeStartTime = DateTime.Now;
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    if (settings.autoExportOnPlayModeExit)
                    {
                        settings.AutoExportLogs();
                    }
                    break;
            }
        }

        #endregion

        #region Export Methods

        public void AutoExportLogs()
        {
            try
            {
                // Ensure directory exists
                var fullPath = Path.Combine(Application.dataPath, "..", autoExportDirectory);
                Directory.CreateDirectory(fullPath);

                // Generate filename
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"session_{timestamp}.log";
                var filePath = Path.Combine(fullPath, filename);

                // Determine time range
                DateTime? from = null;
                if (useTimeRange && timeRangeMinutes > 0)
                {
                    from = DateTime.Now.AddMinutes(-timeRangeMinutes);
                }
                else if (_playModeStartTime.HasValue)
                {
                    from = _playModeStartTime;
                }

                // Extract and write logs
                var logs = LoggerRegistry.ExtractAll(autoExportLevelMask, from, null);
                File.WriteAllText(filePath, logs);

                Debug.Log($"[ConvenientLogger] Auto-exported logs to: {filePath}");

                // Cleanup old files
                CleanupOldExports(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConvenientLogger] Failed to auto-export logs: {ex.Message}");
            }
        }

        public void ExportLogsManual(string path, LogLevel levelMask, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var logs = LoggerRegistry.ExtractAll(levelMask, from, to);
                File.WriteAllText(path, logs);
                Debug.Log($"[ConvenientLogger] Exported logs to: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConvenientLogger] Failed to export logs: {ex.Message}");
            }
        }

        private void CleanupOldExports(string directory)
        {
            if (maxAutoExportFiles <= 0) return;

            try
            {
                var files = Directory.GetFiles(directory, "session_*.log");
                if (files.Length <= maxAutoExportFiles) return;

                // Sort by creation time, delete oldest
                Array.Sort(files, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));
                
                int toDelete = files.Length - maxAutoExportFiles;
                for (int i = 0; i < toDelete; i++)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvenientLogger] Failed to cleanup old exports: {ex.Message}");
            }
        }

        #endregion

        #region Settings Window

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Preferences/Convenient Logger", SettingsScope.User)
            {
                label = "Convenient Logger",
                guiHandler = searchContext =>
                {
                    var settings = instance;
                    var serializedObject = new SerializedObject(settings);

                    EditorGUILayout.LabelField("Auto-Export Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("autoExportOnPlayModeExit"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("autoExportDirectory"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("autoExportLevelMask"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAutoExportFiles"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Manual Export Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("manualExportDirectory"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("includeSourceInfo"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Time Range Export", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("useTimeRange"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("timeRangeMinutes"));

                    if (serializedObject.hasModifiedProperties)
                    {
                        serializedObject.ApplyModifiedProperties();
                        settings.Save(true);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                    
                    if (GUILayout.Button("Export All Logs Now"))
                    {
                        var path = EditorUtility.SaveFilePanel(
                            "Export Logs",
                            settings.manualExportDirectory,
                            $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                            "txt");

                        if (!string.IsNullOrEmpty(path))
                        {
                            settings.ExportLogsManual(path, LogLevel.All);
                        }
                    }

                    if (GUILayout.Button("Clear All Logger Buffers"))
                    {
                        LoggerRegistry.ClearAll();
                        Debug.Log("[ConvenientLogger] All logger buffers cleared");
                    }
                },
                keywords = new[] { "logger", "logging", "export", "debug" }
            };
        }

        #endregion
    }
}
#endif
