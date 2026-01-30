#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Extension methods for adding logger functionality to any VisualElement.
    /// 
    /// Usage:
    ///   // Simple - creates logger at path
    ///   toolbar.AddLogger("DMotion/Preview");
    ///   
    ///   // With logger reference
    ///   _logger = toolbar.AddLogger("DMotion/Preview");
    ///   
    ///   // With existing logger
    ///   toolbar.AddLogger(existingLogger);
    ///   
    ///   // As child of parent logger
    ///   header.AddLogger(parentLogger, "SubSystem");
    /// </summary>
    public static class LoggerUIExtensions
    {
        private const int IconSize = 24;
        private const int IconFontSize = 14;
        
        /// <summary>
        /// Adds a logger icon to this element. Creates or gets logger at the specified path.
        /// Left-click toggles, right-click opens options menu.
        /// </summary>
        /// <param name="container">Element to add the icon to</param>
        /// <param name="loggerPath">Hierarchical path (e.g., "DMotion/Preview")</param>
        /// <param name="defaultEnabled">Initial enabled state if no saved preference</param>
        /// <returns>The logger instance for use in code</returns>
        public static Logger AddLogger(this VisualElement container, string loggerPath, bool defaultEnabled = false)
        {
            var prefsKey = $"ConvenientLogger_{loggerPath.Replace("/", "_")}";
            var logger = LoggerRegistry.GetOrCreate(loggerPath, defaultEnabled);
            
            // Load persisted settings
            logger.Enabled = EditorPrefs.GetBool($"{prefsKey}_Enabled", defaultEnabled);
            logger.ConsoleOutput = EditorPrefs.GetBool($"{prefsKey}_Console", true);
            logger.LevelMask = (LogLevel)EditorPrefs.GetInt($"{prefsKey}_Levels", (int)LogLevel.All);
            
            // Load group assignment
            LoadGroupAssignment(logger, prefsKey);
            
            // Create and add the icon
            var icon = CreateLoggerIcon(logger, prefsKey);
            container.Add(icon);
            
            return logger;
        }
        
        /// <summary>
        /// Adds a logger icon for an existing logger instance.
        /// </summary>
        public static VisualElement AddLogger(this VisualElement container, Logger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            
            var prefsKey = $"ConvenientLogger_{logger.FullPath.Replace("/", "_")}";
            
            // Load group assignment if applicable
            if (logger.CanAssignToGroup)
            {
                LoadGroupAssignment(logger, prefsKey);
            }
            
            var icon = CreateLoggerIcon(logger, prefsKey);
            container.Add(icon);
            
            return icon;
        }
        
        /// <summary>
        /// Adds a child logger icon. Creates a code-child of the parent logger.
        /// </summary>
        public static Logger AddLogger(this VisualElement container, Logger parent, string childName, bool defaultEnabled = true)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            
            var logger = parent.CreateChild(childName);
            var prefsKey = $"ConvenientLogger_{logger.FullPath.Replace("/", "_")}";
            
            // Load persisted settings
            logger.Enabled = EditorPrefs.GetBool($"{prefsKey}_Enabled", defaultEnabled);
            logger.ConsoleOutput = EditorPrefs.GetBool($"{prefsKey}_Console", parent.ConsoleOutput);
            logger.LevelMask = (LogLevel)EditorPrefs.GetInt($"{prefsKey}_Levels", (int)parent.LevelMask);
            
            var icon = CreateLoggerIcon(logger, prefsKey);
            container.Add(icon);
            
            return logger;
        }
        
        #region Icon Creation
        
        private static VisualElement CreateLoggerIcon(Logger logger, string prefsKey)
        {
            var icon = new VisualElement { name = "logger-icon" };
            icon.style.flexShrink = 0;
            
            var button = new Button { name = "logger-button" };
            button.style.width = IconSize;
            button.style.height = IconSize - 4;
            button.style.marginLeft = 4;
            button.style.marginRight = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.backgroundColor = Color.clear;
            button.style.fontSize = IconFontSize;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            
            // Store data for callbacks (including event handlers to prevent GC)
            var iconData = new LoggerIconData 
            { 
                Logger = logger, 
                PrefsKey = prefsKey,
                Button = button
            };
            button.userData = iconData;
            
            // Create event handlers that we can unsubscribe later
            iconData.OnEnabledChanged = (_, __) => ScheduleVisualUpdate(button);
            iconData.OnEffectiveChanged = _ => ScheduleVisualUpdate(button);
            
            // Subscribe to logger's own changes
            logger.OnEnabledChanged += iconData.OnEnabledChanged;
            logger.OnEffectiveEnabledChanged += iconData.OnEffectiveChanged;
            
            // Left-click toggles
            button.clicked += () => OnIconLeftClick(button);
            
            // Right-click opens menu
            button.RegisterCallback<MouseDownEvent>(evt => OnIconMouseDown(evt, button), TrickleDown.TrickleDown);
            
            // Cleanup when removed from hierarchy
            button.RegisterCallback<DetachFromPanelEvent>(_ => OnIconDetached(iconData));
            
            // Initial visual update
            UpdateIconVisual(button);
            
            icon.Add(button);
            return icon;
        }
        
        private static void OnIconDetached(LoggerIconData data)
        {
            // Unsubscribe to prevent memory leaks
            if (data.Logger != null)
            {
                data.Logger.OnEnabledChanged -= data.OnEnabledChanged;
                data.Logger.OnEffectiveEnabledChanged -= data.OnEffectiveChanged;
            }
        }
        
        private static void ScheduleVisualUpdate(Button button)
        {
            if (button == null) return;
            
            // Use EditorApplication.delayCall for reliable cross-window updates
            EditorApplication.delayCall += () =>
            {
                if (button != null && button.panel != null)
                {
                    UpdateIconVisual(button);
                }
            };
        }
        
        private class LoggerIconData
        {
            public Logger Logger;
            public string PrefsKey;
            public Button Button;
            public Action<Logger, bool> OnEnabledChanged;
            public Action<Logger> OnEffectiveChanged;
        }
        
        #endregion
        
        #region Event Handlers
        
        private static void OnIconLeftClick(Button button)
        {
            var data = (LoggerIconData)button.userData;
            data.Logger.Enabled = !data.Logger.Enabled;
            SaveSettings(data.Logger, data.PrefsKey);
            UpdateIconVisual(button);
        }
        
        private static void OnIconMouseDown(MouseDownEvent evt, Button button)
        {
            if (evt.button == 1) // Right-click
            {
                var data = (LoggerIconData)button.userData;
                ShowContextMenu(data.Logger, data.PrefsKey, button);
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }
        
        #endregion
        
        #region Visual Updates
        
        private static void UpdateIconVisual(Button button)
        {
            if (button?.userData == null) return;
            
            var data = (LoggerIconData)button.userData;
            var logger = data.Logger;
            if (logger == null) return;
            
            bool effectiveEnabled = logger.EffectiveEnabled;
            bool blockedByParent = logger.Enabled && !effectiveEnabled;
            
            Color color;
            string tooltip;
            
            if (effectiveEnabled)
            {
                color = new Color(0.3f, 0.9f, 0.3f); // Green
                tooltip = $"Logger: ON | {logger.LevelMask}\nClick: Toggle | Right-click: Options";
            }
            else if (blockedByParent)
            {
                color = new Color(1f, 0.7f, 0.2f); // Orange
                tooltip = "Logger: ON (blocked by parent)\nClick: Toggle | Right-click: Options";
            }
            else
            {
                color = new Color(0.5f, 0.5f, 0.5f); // Gray
                tooltip = "Logger: OFF\nClick: Toggle | Right-click: Options";
            }
            
            button.text = "●";
            button.style.color = color;
            button.tooltip = tooltip;
            
            // Force UI repaint
            button.MarkDirtyRepaint();
        }
        
        #endregion
        
        #region Settings Persistence
        
        private static void SaveSettings(Logger logger, string prefsKey)
        {
            EditorPrefs.SetBool($"{prefsKey}_Enabled", logger.Enabled);
            EditorPrefs.SetBool($"{prefsKey}_Console", logger.ConsoleOutput);
            EditorPrefs.SetInt($"{prefsKey}_Levels", (int)logger.LevelMask);
        }
        
        private static void LoadGroupAssignment(Logger logger, string prefsKey)
        {
            if (!logger.CanAssignToGroup) return;
            
            var groupGuid = EditorPrefs.GetString($"{prefsKey}_Group", "");
            if (string.IsNullOrEmpty(groupGuid)) return;
            
            var group = LoggerGroupRegistry.GetByGuid(groupGuid);
            if (group != null)
            {
                logger.AssignToGroup(group.Logger);
                group.RegisterLoggerPath(logger.FullPath);
            }
            else
            {
                EditorPrefs.DeleteKey($"{prefsKey}_Group");
            }
        }
        
        private static void SaveGroupAssignment(Logger logger, string prefsKey, LoggerGroupAsset newGroup, LoggerGroupAsset oldGroup)
        {
            oldGroup?.UnregisterLoggerPath(logger.FullPath);
            
            if (newGroup == null)
            {
                EditorPrefs.DeleteKey($"{prefsKey}_Group");
            }
            else
            {
                var guid = LoggerGroupRegistry.GetGuid(newGroup);
                if (!string.IsNullOrEmpty(guid))
                {
                    EditorPrefs.SetString($"{prefsKey}_Group", guid);
                    newGroup.RegisterLoggerPath(logger.FullPath);
                }
            }
        }
        
        private static LoggerGroupAsset GetCurrentGroupAsset(string prefsKey)
        {
            var groupGuid = EditorPrefs.GetString($"{prefsKey}_Group", "");
            return string.IsNullOrEmpty(groupGuid) ? null : LoggerGroupRegistry.GetByGuid(groupGuid);
        }
        
        #endregion
        
        #region Context Menu
        
        private static void ShowContextMenu(Logger logger, string prefsKey, Button button)
        {
            var menu = new GenericMenu();
            bool disabledByParent = logger.Enabled && !logger.EffectiveEnabled;
            
            // Show parent/group status
            if (logger.GroupParent != null)
            {
                string groupStatus = logger.GroupParent.EffectiveEnabled ? "ON" : "OFF";
                menu.AddDisabledItem(new GUIContent($"Group: {logger.GroupParent.Name} ({groupStatus})"));
                if (disabledByParent)
                {
                    menu.AddDisabledItem(new GUIContent("(Blocked by group)"));
                }
            }
            else if (logger.Parent != null)
            {
                string parentStatus = logger.Parent.EffectiveEnabled ? "ON" : "OFF";
                menu.AddDisabledItem(new GUIContent($"Parent: {logger.Parent.Name} ({parentStatus})"));
                if (disabledByParent)
                {
                    menu.AddDisabledItem(new GUIContent("(Blocked by parent)"));
                    menu.AddItem(new GUIContent("Enable Parent Chain"), false, () =>
                    {
                        EnableParentChain(logger);
                        UpdateIconVisual(button);
                    });
                }
            }
            
            // Group assignment (only for non-code-children)
            if (logger.CanAssignToGroup)
            {
                if (logger.GroupParent != null)
                {
                    menu.AddItem(new GUIContent("Remove from Group"), false, () =>
                    {
                        var oldGroup = GetCurrentGroupAsset(prefsKey);
                        logger.RemoveFromGroup();
                        SaveGroupAssignment(logger, prefsKey, null, oldGroup);
                        UpdateIconVisual(button);
                    });
                }
                
                // Iterate directly without ToList() allocation
                bool hasGroups = false;
                foreach (var group in LoggerGroupRegistry.GetAll())
                {
                    hasGroups = true;
                    var g = group;
                    bool isCurrentGroup = logger.GroupParent == g.Logger;
                    string status = g.Enabled ? "ON" : "OFF";
                    menu.AddItem(new GUIContent($"Assign to Group/{g.name} ({status})"), isCurrentGroup, () =>
                    {
                        var oldGroup = GetCurrentGroupAsset(prefsKey);
                        logger.AssignToGroup(g.Logger);
                        SaveGroupAssignment(logger, prefsKey, g, oldGroup);
                        UpdateIconVisual(button);
                    });
                }
                
                if (!hasGroups)
                {
                    menu.AddDisabledItem(new GUIContent("Assign to Group/(No groups - Create via Assets menu)"));
                }
                menu.AddSeparator("");
            }
            else if (logger.Parent != null || logger.GroupParent != null)
            {
                menu.AddSeparator("");
            }

            // Enable toggle
            menu.AddItem(new GUIContent("Logging Enabled"), logger.Enabled, () =>
            {
                logger.Enabled = !logger.Enabled;
                SaveSettings(logger, prefsKey);
                UpdateIconVisual(button);
            });

            // Console output toggle
            menu.AddItem(new GUIContent("Console Output"), logger.ConsoleOutput, () =>
            {
                logger.ConsoleOutput = !logger.ConsoleOutput;
                SaveSettings(logger, prefsKey);
            });

            menu.AddSeparator("");

            // Level toggles
            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            {
                var lvl = level;
                var isOn = (logger.LevelMask & lvl) != 0;
                menu.AddItem(new GUIContent($"Levels/{lvl.ToLongString()}"), isOn, () =>
                {
                    logger.LevelMask ^= lvl;
                    SaveSettings(logger, prefsKey);
                    UpdateIconVisual(button);
                });
            }

            menu.AddSeparator("");

            // Presets
            menu.AddItem(new GUIContent("Presets/All"), logger.LevelMask == LogLevel.All, () =>
            {
                logger.LevelMask = LogLevel.All;
                SaveSettings(logger, prefsKey);
            });
            menu.AddItem(new GUIContent("Presets/Debug+"), logger.LevelMask == LogLevel.Development, () =>
            {
                logger.LevelMask = LogLevel.Development;
                SaveSettings(logger, prefsKey);
            });
            menu.AddItem(new GUIContent("Presets/Errors Only"), logger.LevelMask == LogLevel.ErrorsOnly, () =>
            {
                logger.LevelMask = LogLevel.ErrorsOnly;
                SaveSettings(logger, prefsKey);
            });

            menu.AddSeparator("");

            // Actions
            menu.AddItem(new GUIContent("Clear Logs"), false, () => logger.ClearAll());
            menu.AddItem(new GUIContent("Copy to Clipboard"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = logger.ExtractLogs();
            });
            menu.AddItem(new GUIContent("Export to File..."), false, () => ExportToFile(logger));

            // Child loggers info
            if (logger.Children.Count > 0)
            {
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent($"Children: {logger.Children.Count}"));
                menu.AddItem(new GUIContent("Enable All Children"), false, () =>
                {
                    logger.Enable(recursive: true);
                    SaveSettings(logger, prefsKey);
                    UpdateIconVisual(button);
                });
                menu.AddItem(new GUIContent("Disable All Children"), false, () =>
                {
                    logger.Disable(recursive: true);
                    SaveSettings(logger, prefsKey);
                    UpdateIconVisual(button);
                });
            }

            menu.ShowAsContext();
        }
        
        private static void EnableParentChain(Logger logger)
        {
            var current = logger.Parent;
            while (current != null)
            {
                current.Enabled = true;
                current = current.Parent;
            }
        }
        
        private static void ExportToFile(Logger logger)
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Logs",
                "",
                $"{logger.Name}_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, logger.ExtractLogs());
            }
        }
        
        #endregion
    }
}
#endif
