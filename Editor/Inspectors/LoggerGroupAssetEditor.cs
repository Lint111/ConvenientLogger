#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ConvenientLogger.Editor
{
    [CustomEditor(typeof(LoggerGroupAsset))]
    public class LoggerGroupAssetEditor : UnityEditor.Editor
    {
        private const float RepaintIntervalSeconds = 0.5f;
        private const int ScrollThreshold = 10;
        
        private SerializedProperty _parentGroup;
        private SerializedProperty _childGroups;
        private SerializedProperty _defaultEnabled;
        private SerializedProperty _defaultLevelMask;
        private SerializedProperty _defaultConsoleOutput;
        
        private bool _showChildGroups = true;
        private bool _showAssignedLoggers = true;
        private Vector2 _loggersScrollPos;
        
        // Cached dictionary for active loggers lookup - reused across frames
        private readonly Dictionary<string, Logger> _cachedActiveLoggers = new();
        
        private static GUIStyle _indicatorStyle;
        private static GUIStyle _removeButtonStyle;
        private static GUIStyle _dropZoneStyle;

        private void OnEnable()
        {
            _parentGroup = serializedObject.FindProperty("_parentGroup");
            _childGroups = serializedObject.FindProperty("_childGroups");
            _defaultEnabled = serializedObject.FindProperty("_defaultEnabled");
            _defaultLevelMask = serializedObject.FindProperty("_defaultLevelMask");
            _defaultConsoleOutput = serializedObject.FindProperty("_defaultConsoleOutput");
            
            // Subscribe to EditorApplication update to refresh when logger states change externally
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }
        
        private float _lastRepaintTime;
        private void OnEditorUpdate()
        {
            // Repaint periodically to catch external state changes
            // More efficient than RequiresConstantRepaint() which repaints every frame
            if (EditorApplication.timeSinceStartup - _lastRepaintTime > RepaintIntervalSeconds)
            {
                _lastRepaintTime = (float)EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
        
        private void InitStyles()
        {
            if (_indicatorStyle == null)
            {
                _indicatorStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 2),
                    margin = new RectOffset(0, 4, 2, 2),
                    fixedWidth = 24,
                    fixedHeight = 20
                };
            }
            
            if (_removeButtonStyle == null)
            {
                _removeButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 2),
                    fixedWidth = 28,
                    fixedHeight = 20
                };
            }
            
            if (_dropZoneStyle == null)
            {
                _dropZoneStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Italic
                };
            }
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            var group = (LoggerGroupAsset)target;

            // Header
            EditorGUILayout.LabelField("Logger Group Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Parent Group (read-only display with navigation)
            DrawParentSection(group);
            
            EditorGUILayout.Space();

            // Default settings
            EditorGUILayout.LabelField("Default Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_defaultEnabled, new GUIContent("Enabled", "Whether loggers in this group are enabled by default"));
            
            _defaultLevelMask.intValue = (int)(LogLevel)EditorGUILayout.EnumFlagsField(
                new GUIContent("Level Mask", "Which log levels are enabled by default"),
                (LogLevel)_defaultLevelMask.intValue);
            
            EditorGUILayout.PropertyField(_defaultConsoleOutput, new GUIContent("Console Output", "Whether to output logs to Unity Console"));

            EditorGUILayout.Space();
            
            // Child Groups section
            DrawChildGroupsSection(group);
            
            EditorGUILayout.Space();
            
            // Assigned Loggers section
            DrawAssignedLoggersSection(group);

            EditorGUILayout.Space();
            
            // Runtime Status
            DrawRuntimeStatus(group);

            EditorGUILayout.Space();
            
            // Quick Actions
            DrawQuickActions(group);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawParentSection(LoggerGroupAsset group)
        {
            EditorGUILayout.LabelField("Hierarchy", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Parent Group");
            
            if (group.ParentGroup != null)
            {
                if (GUILayout.Button(group.ParentGroup.name, EditorStyles.linkLabel))
                {
                    Selection.activeObject = group.ParentGroup;
                }
                if (GUILayout.Button("×", _removeButtonStyle))
                {
                    Undo.RecordObject(group, "Remove Parent Group");
                    Undo.RecordObject(group.ParentGroup, "Remove Child Group");
                    group.ParentGroup.RemoveChildGroup(group);
                    EditorUtility.SetDirty(group);
                    EditorUtility.SetDirty(group.ParentGroup);
                }
            }
            else
            {
                // Drop zone for parent
                var dropRect = EditorGUILayout.GetControlRect();
                var newParent = (LoggerGroupAsset)EditorGUI.ObjectField(dropRect, null, typeof(LoggerGroupAsset), false);
                
                if (newParent != null && newParent != group)
                {
                    if (newParent.WouldCreateCircularReference(group))
                    {
                        EditorUtility.DisplayDialog("Circular Reference", 
                            $"Cannot set '{newParent.name}' as parent - this would create a circular reference.", "OK");
                    }
                    else
                    {
                        Undo.RecordObject(group, "Set Parent Group");
                        Undo.RecordObject(newParent, "Add Child Group");
                        newParent.AddChildGroup(group);
                        EditorUtility.SetDirty(group);
                        EditorUtility.SetDirty(newParent);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChildGroupsSection(LoggerGroupAsset group)
        {
            _showChildGroups = EditorGUILayout.Foldout(_showChildGroups, $"Child Groups ({group.ChildGroups.Count})", true);
            
            if (!_showChildGroups) return;
            
            EditorGUI.indentLevel++;
            
            // List existing children
            var childrenToRemove = new List<LoggerGroupAsset>();
            
            foreach (var child in group.ChildGroups)
            {
                if (child == null) continue;
                
                EditorGUILayout.BeginHorizontal();
                
                // Clickable status indicator - left click toggles
                if (DrawClickableIndicator(child.EffectiveEnabled, child.Enabled && !child.EffectiveEnabled))
                {
                    child.Enabled = !child.Enabled;
                    EditorUtility.SetDirty(child);
                }
                
                // Check for right-click on indicator
                var indicatorRect = GUILayoutUtility.GetLastRect();
                if (HandleRightClick(indicatorRect))
                {
                    ShowChildGroupContextMenu(child, group);
                }
                
                // Clickable name
                if (GUILayout.Button(child.name, EditorStyles.linkLabel))
                {
                    Selection.activeObject = child;
                }
                
                GUILayout.FlexibleSpace();
                
                // Logger count
                EditorGUILayout.LabelField($"({child.AssignedLoggerCount})", EditorStyles.miniLabel, GUILayout.Width(40));
                
                // Remove button
                if (GUILayout.Button("×", _removeButtonStyle))
                {
                    childrenToRemove.Add(child);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Process removals
            foreach (var child in childrenToRemove)
            {
                Undo.RecordObject(group, "Remove Child Group");
                Undo.RecordObject(child, "Remove from Parent");
                group.RemoveChildGroup(child);
                EditorUtility.SetDirty(group);
                EditorUtility.SetDirty(child);
            }
            
            // Drop zone for adding children
            EditorGUILayout.Space(4);
            var dropRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            
            // Draw drop zone using cached style
            GUI.Box(dropRect, "Drop Logger Group here to add as child", _dropZoneStyle);
            
            // Handle drag & drop
            HandleDragAndDrop(dropRect, group);
            
            EditorGUI.indentLevel--;
        }
        
        private bool DrawClickableIndicator(bool effectiveEnabled, bool blockedByParent, string tooltip = null)
        {
            // Determine color
            Color textColor;
            string defaultTooltip;
            
            if (effectiveEnabled)
            {
                textColor = new Color(0.3f, 0.9f, 0.3f); // Bright green
                defaultTooltip = "Enabled\nClick to toggle | Right-click for options";
            }
            else if (blockedByParent)
            {
                textColor = new Color(1f, 0.7f, 0.2f); // Orange  
                defaultTooltip = "Enabled (blocked by parent)\nClick to toggle | Right-click for options";
            }
            else
            {
                textColor = new Color(0.5f, 0.5f, 0.5f); // Gray
                defaultTooltip = "Disabled\nClick to toggle | Right-click for options";
            }
            
            var oldColor = GUI.contentColor;
            GUI.contentColor = textColor;
            
            // Use a button - returns true on left click
            var content = new GUIContent("●", tooltip ?? defaultTooltip);
            var clicked = GUILayout.Button(content, _indicatorStyle);
            
            GUI.contentColor = oldColor;
            
            // Get the last rect for right-click detection
            var rect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            
            return clicked;
        }
        
        private bool HandleRightClick(Rect rect)
        {
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 1 && rect.Contains(evt.mousePosition))
            {
                evt.Use();
                return true;
            }
            return false;
        }
        
        private void ShowChildGroupContextMenu(LoggerGroupAsset child, LoggerGroupAsset parent)
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent(child.Enabled ? "Disable" : "Enable"), false, () =>
            {
                child.Enabled = !child.Enabled;
                EditorUtility.SetDirty(child);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Select in Project"), false, () =>
            {
                Selection.activeObject = child;
                EditorGUIUtility.PingObject(child);
            });
            
            menu.AddItem(new GUIContent("Remove from Group"), false, () =>
            {
                Undo.RecordObject(parent, "Remove Child Group");
                Undo.RecordObject(child, "Remove from Parent");
                parent.RemoveChildGroup(child);
                EditorUtility.SetDirty(parent);
                EditorUtility.SetDirty(child);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Enable All Children"), false, () =>
            {
                SetEnabledRecursive(child, true);
            });
            
            menu.AddItem(new GUIContent("Disable All Children"), false, () =>
            {
                SetEnabledRecursive(child, false);
            });
            
            menu.ShowAsContext();
        }

        private void HandleDragAndDrop(Rect dropRect, LoggerGroupAsset group)
        {
            var evt = Event.current;
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropRect.Contains(evt.mousePosition)) return;
                    
                    var validDrag = false;
                    LoggerGroupAsset draggedGroup = null;
                    
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is LoggerGroupAsset potentialChild && potentialChild != group)
                        {
                            if (!group.WouldCreateCircularReference(potentialChild) && 
                                !group.ChildGroups.Contains(potentialChild))
                            {
                                validDrag = true;
                                draggedGroup = potentialChild;
                                break;
                            }
                        }
                    }
                    
                    if (validDrag)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        
                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            
                            Undo.RecordObject(group, "Add Child Group");
                            if (draggedGroup.ParentGroup != null)
                            {
                                Undo.RecordObject(draggedGroup.ParentGroup, "Remove Child Group");
                            }
                            Undo.RecordObject(draggedGroup, "Set Parent Group");
                            
                            group.AddChildGroup(draggedGroup);
                            
                            EditorUtility.SetDirty(group);
                            EditorUtility.SetDirty(draggedGroup);
                            if (draggedGroup.ParentGroup != null)
                            {
                                EditorUtility.SetDirty(draggedGroup.ParentGroup);
                            }
                        }
                    }
                    else
                    {
                        // Check if any dragged object is a LoggerGroupAsset (without LINQ allocation)
                        bool hasGroupAsset = false;
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is LoggerGroupAsset)
                            {
                                hasGroupAsset = true;
                                break;
                            }
                        }
                        if (hasGroupAsset)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                        }
                    }
                    
                    evt.Use();
                    break;
            }
        }

        private void DrawAssignedLoggersSection(LoggerGroupAsset group)
        {
            // Use serialized paths - these persist across domain reloads
            var paths = group.AssignedLoggerPaths;
            
            // Reuse cached dictionary to avoid allocation every frame
            _cachedActiveLoggers.Clear();
            foreach (var logger in group.GetActiveLoggers())
            {
                _cachedActiveLoggers[logger.FullPath] = logger;
            }
            var activeLoggers = _cachedActiveLoggers;
            
            _showAssignedLoggers = EditorGUILayout.Foldout(_showAssignedLoggers, $"Assigned Loggers ({paths.Count})", true);
            
            if (!_showAssignedLoggers) return;
            
            EditorGUI.indentLevel++;
            
            if (paths.Count == 0)
            {
                EditorGUILayout.HelpBox("No loggers assigned to this group.\n\nTo assign loggers:\n1. Click the logger icon in any editor window\n2. Select 'Assign to Group' from the context menu", MessageType.Info);
            }
            else
            {
                // Scrollable list if many loggers
                if (paths.Count > ScrollThreshold)
                {
                    _loggersScrollPos = EditorGUILayout.BeginScrollView(_loggersScrollPos, GUILayout.Height(200));
                }
                
                var pathsToRemove = new List<string>();
                
                foreach (var path in paths)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Check if logger is currently active (exists at runtime)
                    var isActive = activeLoggers.TryGetValue(path, out var logger);
                    
                    if (isActive)
                    {
                        // Active logger - clickable indicator
                        if (DrawClickableIndicator(logger.EffectiveEnabled, logger.Enabled && !logger.EffectiveEnabled))
                        {
                            logger.Enabled = !logger.Enabled;
                        }
                        
                        // Check for right-click on indicator  
                        var indicatorRect = GUILayoutUtility.GetLastRect();
                        if (HandleRightClick(indicatorRect))
                        {
                            ShowLoggerContextMenu(logger, group, path, pathsToRemove);
                        }
                    }
                    else
                    {
                        // Inactive logger - show dimmed indicator
                        var oldColor = GUI.contentColor;
                        GUI.contentColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                        GUILayout.Label("○", _indicatorStyle, GUILayout.Width(24)); // Empty circle for inactive
                        GUI.contentColor = oldColor;
                    }
                    
                    // Logger path - dimmed if inactive
                    var pathStyle = isActive ? EditorStyles.label : EditorStyles.miniLabel;
                    var displayPath = isActive ? path : $"{path} (inactive)";
                    EditorGUILayout.LabelField(displayPath, pathStyle);
                    
                    GUILayout.FlexibleSpace();
                    
                    // Remove button
                    if (GUILayout.Button("×", _removeButtonStyle))
                    {
                        pathsToRemove.Add(path);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (paths.Count > ScrollThreshold)
                {
                    EditorGUILayout.EndScrollView();
                }
                
                // Process removals
                foreach (var path in pathsToRemove)
                {
                    group.RemoveLoggerByPath(path);
                    
                    // Also clear the EditorPrefs for the group assignment
                    var prefsKey = $"ConvenientLogger_{path.Replace("/", "_")}_Group";
                    EditorPrefs.DeleteKey(prefsKey);
                }
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void ShowLoggerContextMenu(Logger logger, LoggerGroupAsset group, string loggerPath, List<string> pathRemovalList)
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent(logger.Enabled ? "Disable" : "Enable"), false, () =>
            {
                logger.Enabled = !logger.Enabled;
            });
            
            menu.AddSeparator("");
            
            // Level mask submenu
            foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            {
                var lvl = level;
                var isOn = (logger.LevelMask & lvl) != 0;
                menu.AddItem(new GUIContent($"Levels/{lvl}"), isOn, () =>
                {
                    logger.LevelMask ^= lvl;
                });
            }
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Console Output"), logger.ConsoleOutput, () =>
            {
                logger.ConsoleOutput = !logger.ConsoleOutput;
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Remove from Group"), false, () =>
            {
                pathRemovalList.Add(loggerPath);
                // Force repaint
                EditorUtility.SetDirty(group);
            });
            
            menu.AddItem(new GUIContent("Clear Logs"), false, () =>
            {
                logger.ClearAll();
            });
            
            menu.ShowAsContext();
        }

        private void DrawRuntimeStatus(LoggerGroupAsset group)
        {
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Currently Enabled", group.Enabled);
            EditorGUILayout.Toggle("Effectively Enabled", group.EffectiveEnabled);
            EditorGUI.EndDisabledGroup();
            
            // Show hierarchy path
            var path = GetHierarchyPath(group);
            if (path.Count > 1)
            {
                EditorGUILayout.LabelField("Hierarchy Path:", EditorStyles.miniLabel);
                // Build path string without LINQ allocation
                var pathStr = new System.Text.StringBuilder("  ");
                for (int i = 0; i < path.Count; i++)
                {
                    if (i > 0) pathStr.Append(" → ");
                    pathStr.Append(path[i].name);
                }
                EditorGUILayout.LabelField(pathStr.ToString(), EditorStyles.miniLabel);
            }
        }

        private void DrawQuickActions(LoggerGroupAsset group)
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Enable All"))
            {
                SetEnabledRecursive(group, true);
            }
            
            if (GUILayout.Button("Disable All"))
            {
                SetEnabledRecursive(group, false);
            }
            
            if (GUILayout.Button("Clear All Loggers"))
            {
                if (EditorUtility.DisplayDialog("Clear All Loggers", 
                    "Remove all assigned loggers from this group and its children?", "Yes", "Cancel"))
                {
                    ClearLoggersRecursive(group);
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private List<LoggerGroupAsset> GetHierarchyPath(LoggerGroupAsset group)
        {
            var path = new List<LoggerGroupAsset>();
            var current = group;
            
            while (current != null)
            {
                path.Insert(0, current);
                current = current.ParentGroup;
            }
            
            return path;
        }

        private void SetEnabledRecursive(LoggerGroupAsset group, bool enabled)
        {
            Undo.RecordObject(group, enabled ? "Enable Group" : "Disable Group");
            group.Enabled = enabled;
            EditorUtility.SetDirty(group);
            
            foreach (var child in group.ChildGroups)
            {
                if (child != null)
                {
                    SetEnabledRecursive(child, enabled);
                }
            }
        }

        private void ClearLoggersRecursive(LoggerGroupAsset group)
        {
            // Use paths instead of active loggers to clear all (including inactive)
            // Copy to array to avoid modifying collection during iteration
            var paths = group.AssignedLoggerPaths;
            var pathCount = paths.Count;
            var pathsCopy = new string[pathCount];
            paths.CopyTo(pathsCopy, 0);
            
            foreach (var path in pathsCopy)
            {
                group.RemoveLoggerByPath(path);
                var prefsKey = $"ConvenientLogger_{path.Replace("/", "_")}_Group";
                EditorPrefs.DeleteKey(prefsKey);
            }
            
            foreach (var child in group.ChildGroups)
            {
                if (child != null)
                {
                    ClearLoggersRecursive(child);
                }
            }
        }
    }
}
#endif
