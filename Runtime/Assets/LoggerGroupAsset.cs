using System.Collections.Generic;
using UnityEngine;

namespace ConvenientLogger
{
    /// <summary>
    /// A ScriptableObject that acts as a logger group parent.
    /// Groups can contain other groups (hierarchy) and have loggers assigned to them.
    /// 
    /// Usage:
    /// 1. Create via Assets > Create > ConvenientLogger > Logger Group
    /// 2. Drag child groups into the "Child Groups" list for hierarchy
    /// 3. In any LoggerIconElement context menu, select "Assign to Group..."
    /// 4. Disabling a group disables all child groups and their loggers
    /// </summary>
    [CreateAssetMenu(fileName = "NewLoggerGroup", menuName = "ConvenientLogger/Logger Group", order = 100)]
    public class LoggerGroupAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Parent group asset (if any). This group's logger becomes a child of the parent's logger.")]
        private LoggerGroupAsset _parentGroup;
        
        [SerializeField, Tooltip("Child group assets. These groups' loggers become children of this group's logger.")]
        private List<LoggerGroupAsset> _childGroups = new();
        
        [SerializeField, Tooltip("Logger paths assigned to this group. Persists across domain reloads.")]
        private List<string> _assignedLoggerPaths = new();
        
        [SerializeField, Tooltip("Default enabled state when group is first used")]
        private bool _defaultEnabled = true;
        
        [SerializeField, Tooltip("Default log level mask")]
        private LogLevel _defaultLevelMask = LogLevel.All;
        
        [SerializeField, Tooltip("Default console output setting")]
        private bool _defaultConsoleOutput = true;

        private Logger _logger;
        private bool _isInitializing;

        /// <summary>
        /// The logger instance for this group. Acts as parent for assigned loggers.
        /// </summary>
        public Logger Logger
        {
            get
            {
                if (_logger == null && !_isInitializing)
                {
                    InitializeLogger();
                }
                return _logger;
            }
        }

        /// <summary>
        /// Parent group asset, if any.
        /// </summary>
        public LoggerGroupAsset ParentGroup => _parentGroup;
        
        /// <summary>
        /// Child group assets.
        /// </summary>
        public IReadOnlyList<LoggerGroupAsset> ChildGroups => _childGroups;

        /// <summary>
        /// Shortcut to check if the group is enabled.
        /// </summary>
        public bool Enabled
        {
            get => Logger?.Enabled ?? _defaultEnabled;
            set
            {
                if (_logger != null) _logger.Enabled = value;
                _defaultEnabled = value;
            }
        }
        
        /// <summary>
        /// Whether this group is effectively enabled (considering parent hierarchy).
        /// </summary>
        public bool EffectiveEnabled => Logger?.EffectiveEnabled ?? false;

        /// <summary>
        /// Gets the number of loggers assigned to this group (from serialized paths).
        /// </summary>
        public int AssignedLoggerCount => _assignedLoggerPaths.Count;
        
        /// <summary>
        /// Gets the serialized list of assigned logger paths.
        /// </summary>
        public IReadOnlyList<string> AssignedLoggerPaths => _assignedLoggerPaths;

        private void InitializeLogger()
        {
            _isInitializing = true;
            try
            {
                var safeName = string.IsNullOrEmpty(name) ? "UnnamedGroup" : name;
                _logger = new Logger($"[Group:{safeName}]");
                _logger.Enabled = _defaultEnabled;
                _logger.LevelMask = _defaultLevelMask;
                _logger.ConsoleOutput = _defaultConsoleOutput;
                
                // If we have a parent group, assign to it
                if (_parentGroup != null && _parentGroup != this)
                {
                    var parentLogger = _parentGroup.Logger;
                    if (parentLogger != null)
                    {
                        parentLogger.AddChild(_logger);
                    }
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void OnValidate()
        {
            // Only apply if logger was already initialized
            if (_logger == null) return;
            
            _logger.Enabled = _defaultEnabled;
            _logger.LevelMask = _defaultLevelMask;
            _logger.ConsoleOutput = _defaultConsoleOutput;
            
            // Rebuild hierarchy if needed
            RebuildHierarchy();
        }
        
        /// <summary>
        /// Rebuilds the logger hierarchy based on current parent/child relationships.
        /// </summary>
        public void RebuildHierarchy()
        {
            if (_logger == null) return;
            
            // Update parent relationship
            var currentParent = _logger.Parent;
            var expectedParent = _parentGroup?.Logger;
            
            if (currentParent != expectedParent)
            {
                currentParent?.RemoveChild(_logger);
                expectedParent?.AddChild(_logger);
            }
        }

        /// <summary>
        /// Checks if adding the specified group as a child would create a circular reference.
        /// </summary>
        public bool WouldCreateCircularReference(LoggerGroupAsset potentialChild)
        {
            if (potentialChild == null) return false;
            if (potentialChild == this) return true;
            
            // Check if potentialChild is an ancestor of this group
            var current = _parentGroup;
            while (current != null)
            {
                if (current == potentialChild) return true;
                current = current._parentGroup;
            }
            
            // Check if this group appears anywhere in potentialChild's descendant tree
            return IsDescendantOf(potentialChild, this);
        }
        
        private static bool IsDescendantOf(LoggerGroupAsset group, LoggerGroupAsset potentialAncestor)
        {
            if (group == null) return false;
            
            foreach (var child in group._childGroups)
            {
                if (child == potentialAncestor) return true;
                if (IsDescendantOf(child, potentialAncestor)) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a child group. Returns false if it would create a circular reference.
        /// </summary>
        public bool AddChildGroup(LoggerGroupAsset child)
        {
            if (child == null || child == this) return false;
            if (_childGroups.Contains(child)) return false;
            if (WouldCreateCircularReference(child)) return false;
            
            // Remove from old parent
            child._parentGroup?._childGroups.Remove(child);
            child._parentGroup?.Logger?.RemoveChild(child._logger);
            
            // Add to this group
            child._parentGroup = this;
            _childGroups.Add(child);
            
            // Update logger hierarchy
            if (child._logger != null && _logger != null)
            {
                Logger.AddChild(child._logger);
            }
            
            return true;
        }

        /// <summary>
        /// Removes a child group from this group.
        /// </summary>
        public bool RemoveChildGroup(LoggerGroupAsset child)
        {
            if (child == null) return false;
            if (!_childGroups.Remove(child)) return false;
            
            // Update references
            if (child._parentGroup == this)
            {
                child._parentGroup = null;
            }
            
            // Update logger hierarchy
            _logger?.RemoveChild(child._logger);
            
            return true;
        }

        /// <summary>
        /// Registers a logger path as assigned to this group.
        /// Call this when assigning a logger to persist the assignment.
        /// </summary>
        public void RegisterLoggerPath(string loggerPath)
        {
            if (string.IsNullOrEmpty(loggerPath)) return;
            if (!_assignedLoggerPaths.Contains(loggerPath))
            {
                _assignedLoggerPaths.Add(loggerPath);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
        
        /// <summary>
        /// Unregisters a logger path from this group.
        /// </summary>
        public void UnregisterLoggerPath(string loggerPath)
        {
            if (_assignedLoggerPaths.Remove(loggerPath))
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        /// Removes a logger from this group (both runtime and serialized).
        /// </summary>
        public bool RemoveLogger(Logger logger)
        {
            if (logger == null) return false;
            
            // Remove from serialized paths
            UnregisterLoggerPath(logger.FullPath);
            
            // Remove from runtime logger children
            if (_logger != null)
            {
                _logger.RemoveChild(logger);
                
                // If the logger supports group assignment, clear its group parent
                if (logger.CanAssignToGroup)
                {
                    logger.RemoveFromGroup();
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Removes a logger by path (for when the Logger object doesn't exist).
        /// </summary>
        public bool RemoveLoggerByPath(string loggerPath)
        {
            if (string.IsNullOrEmpty(loggerPath)) return false;
            
            // Remove from serialized paths
            var removed = _assignedLoggerPaths.Remove(loggerPath);
            
            if (removed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            
            // Also try to remove from runtime if logger exists
            if (_logger != null)
            {
                foreach (var child in _logger.Children)
                {
                    if (child.FullPath == loggerPath)
                    {
                        _logger.RemoveChild(child);
                        if (child.CanAssignToGroup)
                        {
                            child.RemoveFromGroup();
                        }
                        break;
                    }
                }
            }
            
            return removed;
        }

        /// <summary>
        /// Gets all active loggers currently assigned to this group.
        /// Only returns loggers that exist at runtime.
        /// </summary>
        public IEnumerable<Logger> GetActiveLoggers()
        {
            if (_logger == null) yield break;
            
            foreach (var child in _logger.Children)
            {
                // Skip group loggers
                if (!child.Name.StartsWith("[Group:"))
                {
                    yield return child;
                }
            }
        }
        
        /// <summary>
        /// Checks if a logger path is registered with this group.
        /// </summary>
        public bool HasLoggerPath(string loggerPath)
        {
            return _assignedLoggerPaths.Contains(loggerPath);
        }
    }
}
