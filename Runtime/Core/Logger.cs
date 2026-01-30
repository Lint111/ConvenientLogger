using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace ConvenientLogger
{
    /// <summary>
    /// Hierarchical logger with per-instance filtering and optional Unity console output.
    /// Designed for minimal allocation - messages are only formatted when logging is enabled.
    /// </summary>
    public class Logger
    {
        #region Fields

        private readonly string _name;
        private readonly string _fullPath;
        private readonly LogBuffer _buffer;
        private readonly List<Logger> _children = new();
        private readonly bool _isCodeChild;  // True if created via CreateChild() - cannot be re-parented
        
        private Logger _codeParent;   // Parent set at construction (immutable for code children)
        private Logger _groupParent;  // Parent set via AssignToGroup (mutable for non-code-children)
        
        private bool _enabled = true;
        private LogLevel _levelMask = LogLevel.All;
        private bool _consoleOutput = false;
        private bool _includeSourceInfo = false;
        
        // Cached effective enabled state to avoid recursive parent walks
        private bool _cachedEffectiveEnabled = true;
        private bool _effectiveEnabledDirty = true;

        #endregion

        #region Properties

        /// <summary>
        /// Logger name (e.g., "Preview", "Timeline").
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Full hierarchical path (e.g., "AnimationPreview/Preview/Timeline").
        /// </summary>
        public string FullPath => _fullPath;

        /// <summary>
        /// Parent logger in the hierarchy. 
        /// For code children, this is the immutable code parent.
        /// For registry loggers, this returns the group parent if assigned, otherwise the code parent.
        /// </summary>
        public Logger Parent => _groupParent ?? _codeParent;
        
        /// <summary>
        /// Whether this logger was created via CreateChild() and cannot be re-parented.
        /// Code children have an immutable parent relationship set at construction.
        /// This is primarily used internally - most users don't need to check this.
        /// </summary>
        public bool IsCodeChild => _isCodeChild;
        
        /// <summary>
        /// Whether this logger can be assigned to a group via AssignToGroup().
        /// Returns false for loggers created via CreateChild() (code children).
        /// Returns true for loggers created via LoggerRegistry.GetOrCreate().
        /// </summary>
        public bool CanAssignToGroup => !_isCodeChild;
        
        /// <summary>
        /// The currently assigned group parent, if any.
        /// </summary>
        public Logger GroupParent => _groupParent;

        /// <summary>
        /// Child loggers.
        /// </summary>
        public IReadOnlyList<Logger> Children => _children;

        /// <summary>
        /// Whether this logger is enabled by the user. 
        /// Note: Use EffectiveEnabled to check if logging will actually occur (respects parent state).
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                InvalidateEffectiveEnabled();
                OnEnabledChanged?.Invoke(this, value);
                // Notify children that effective state may have changed
                NotifyChildrenOfParentStateChange();
            }
        }
        
        /// <summary>
        /// Whether logging is effectively enabled, considering parent hierarchy.
        /// Returns false if this logger OR any ancestor is disabled.
        /// Cached for performance - invalidated when this or any ancestor's state changes.
        /// </summary>
        public bool EffectiveEnabled
        {
            get
            {
                if (_effectiveEnabledDirty)
                {
                    _cachedEffectiveEnabled = ComputeEffectiveEnabled();
                    _effectiveEnabledDirty = false;
                }
                return _cachedEffectiveEnabled;
            }
        }
        
        /// <summary>
        /// Computes effective enabled by walking up the parent chain.
        /// </summary>
        private bool ComputeEffectiveEnabled()
        {
            if (!_enabled) return false;
            var parent = Parent;
            return parent?.EffectiveEnabled ?? true;
        }
        
        /// <summary>
        /// Invalidates the cached EffectiveEnabled value for this logger and all descendants.
        /// </summary>
        private void InvalidateEffectiveEnabled()
        {
            _effectiveEnabledDirty = true;
            foreach (var child in _children)
            {
                child.InvalidateEffectiveEnabled();
            }
        }
        
        /// <summary>
        /// Fired when Enabled state changes. Parameters: (logger, newEnabledValue)
        /// </summary>
        public event Action<Logger, bool> OnEnabledChanged;
        
        /// <summary>
        /// Fired when effective enabled state changes due to parent state change.
        /// </summary>
        public event Action<Logger> OnEffectiveEnabledChanged;

        /// <summary>
        /// Bitmask of log levels that will be recorded.
        /// </summary>
        public LogLevel LevelMask
        {
            get => _levelMask;
            set => _levelMask = value;
        }

        /// <summary>
        /// Whether to also output to Unity console.
        /// </summary>
        public bool ConsoleOutput
        {
            get => _consoleOutput;
            set => _consoleOutput = value;
        }

        /// <summary>
        /// Whether to include file/line info in log entries.
        /// </summary>
        public bool IncludeSourceInfo
        {
            get => _includeSourceInfo;
            set => _includeSourceInfo = value;
        }

        /// <summary>
        /// Internal buffer for this logger's entries.
        /// </summary>
        public LogBuffer Buffer => _buffer;

        #endregion

        #region Construction

        /// <summary>
        /// Creates a root logger with no parent.
        /// </summary>
        public Logger(string name, int bufferCapacity = 500)
        {
            _name = name;
            _fullPath = name;
            _buffer = new LogBuffer(bufferCapacity);
            _isCodeChild = false;
        }

        /// <summary>
        /// Internal constructor for child loggers.
        /// </summary>
        /// <param name="name">Logger name</param>
        /// <param name="parent">Parent logger</param>
        /// <param name="isCodeChild">If true, this logger cannot be re-parented to a group</param>
        /// <param name="bufferCapacity">Ring buffer capacity</param>
        internal Logger(string name, Logger parent, bool isCodeChild, int bufferCapacity = 500)
        {
            _name = name;
            _codeParent = parent;
            _isCodeChild = isCodeChild;
            _fullPath = parent != null ? $"{parent.FullPath}/{name}" : name;
            _buffer = new LogBuffer(bufferCapacity);
        }

        #endregion

        #region Hierarchy

        /// <summary>
        /// Creates and adds a code child logger. Code children cannot be re-parented to groups.
        /// </summary>
        public Logger CreateChild(string name, int bufferCapacity = 500)
        {
            var child = new Logger(name, this, isCodeChild: true, bufferCapacity);
            _children.Add(child);
            return child;
        }
        
        /// <summary>
        /// Creates a registry child logger. Registry children CAN be re-parented to groups.
        /// Used internally by LoggerRegistry.
        /// </summary>
        internal Logger CreateRegistryChild(string name, int bufferCapacity = 500)
        {
            var child = new Logger(name, this, isCodeChild: false, bufferCapacity);
            _children.Add(child);
            return child;
        }

        /// <summary>
        /// Adds an existing logger as a child. The child becomes a code-child and cannot be re-parented to groups.
        /// </summary>
        public void AddChild(Logger child)
        {
            if (child == null || child._codeParent != null) return;
            child._codeParent = this;
            _children.Add(child);
        }

        /// <summary>
        /// Removes a child logger from this logger's children list.
        /// </summary>
        public bool RemoveChild(Logger child)
        {
            if (child == null) return false;
            if (_children.Remove(child))
            {
                // Only clear the code parent if this is actually the parent
                if (child._codeParent == this)
                {
                    child._codeParent = null;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all children.
        /// </summary>
        public void ClearChildren()
        {
            foreach (var child in _children)
            {
                if (!child._isCodeChild)
                {
                    // Non-code children can be detached
                    child._codeParent = null;
                }
            }
            _children.Clear();
        }
        
        /// <summary>
        /// Assigns this logger to a group parent. Only valid for non-code-children.
        /// The group parent takes precedence over the code parent for hierarchy purposes.
        /// </summary>
        /// <param name="groupLogger">The group's logger, or null to remove from group</param>
        /// <exception cref="InvalidOperationException">If this is a code child</exception>
        public void AssignToGroup(Logger groupLogger)
        {
            if (_isCodeChild)
            {
                throw new InvalidOperationException(
                    $"Cannot assign code-parented logger '{_fullPath}' to a group. " +
                    "Only loggers created via LoggerRegistry can be assigned to groups.");
            }
            
            // Remove from old group parent's children
            _groupParent?.RemoveChild(this);
            
            _groupParent = groupLogger;
            
            // Add to new group parent's children
            _groupParent?._children.Add(this);
            
            // Invalidate cache since parent changed
            InvalidateEffectiveEnabled();
            
            // Notify that effective state may have changed
            OnEffectiveEnabledChanged?.Invoke(this);
            NotifyChildrenOfParentStateChange();
        }
        
        /// <summary>
        /// Removes this logger from its current group (if any).
        /// </summary>
        public void RemoveFromGroup()
        {
            if (_groupParent == null) return;
            AssignToGroup(null);
        }

        #endregion

        #region Logging - Core

        /// <summary>
        /// Core logging method. Use the level-specific methods for cleaner code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Log(
            LogLevel level,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!ShouldLog(level)) return;

            var entry = new LogEntry(
                level,
                _fullPath,
                message,
                _includeSourceInfo ? filePath : null,
                _includeSourceInfo ? memberName : null,
                _includeSourceInfo ? lineNumber : 0);

            _buffer.Add(entry);

            if (_consoleOutput)
            {
                OutputToConsole(entry);
            }
        }

        /// <summary>
        /// Checks if a log at the given level would be recorded.
        /// Use this to avoid expensive string formatting when logging is disabled.
        /// Respects parent hierarchy - returns false if any ancestor is disabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldLog(LogLevel level)
        {
            return EffectiveEnabled && (_levelMask & level) != 0;
        }
        
        /// <summary>
        /// Logs directly without checking ShouldLog. Used internally by LogScope
        /// to ensure END is logged even if logger state changed mid-scope.
        /// </summary>
        internal void LogDirect(
            LogLevel level,
            string message,
            string filePath,
            string memberName,
            int lineNumber)
        {
            var entry = new LogEntry(
                level,
                _fullPath,
                message,
                _includeSourceInfo ? filePath : null,
                _includeSourceInfo ? memberName : null,
                _includeSourceInfo ? lineNumber : 0);

            _buffer.Add(entry);

            if (_consoleOutput)
            {
                OutputToConsole(entry);
            }
        }
        
        /// <summary>
        /// Notifies all children that the parent's effective state may have changed.
        /// Also invalidates the cached EffectiveEnabled values.
        /// </summary>
        private void NotifyChildrenOfParentStateChange()
        {
            foreach (var child in _children)
            {
                child._effectiveEnabledDirty = true;
                child.OnEffectiveEnabledChanged?.Invoke(child);
                // Recursively notify grandchildren
                child.NotifyChildrenOfParentStateChange();
            }
        }

        private void OutputToConsole(in LogEntry entry)
        {
            var message = entry.Format(_includeSourceInfo);
            
            switch (entry.Level)
            {
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }

        #endregion

        #region Logging - Convenience Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trace(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Trace, message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Debug(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Debug, message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Info, message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Warning, message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Error, message, filePath, memberName, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Critical(string message,
            [CallerFilePath] string filePath = "",
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
            => Log(LogLevel.Critical, message, filePath, memberName, lineNumber);

        #endregion

        #region Extraction

        /// <summary>
        /// Extracts all logs from this logger and its children, formatted hierarchically.
        /// Uses pooled StringBuilder to minimize allocations.
        /// </summary>
        public string ExtractLogs(int indentLevel = 0, LogLevel levelMask = LogLevel.All, DateTime? from = null, DateTime? to = null)
        {
            var sb = StringBuilderPool.Get();
            try
            {
                ExtractLogsInternal(sb, indentLevel, levelMask, from, to);
                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }
        
        /// <summary>
        /// Internal extraction that writes directly to StringBuilder.
        /// </summary>
        private void ExtractLogsInternal(StringBuilder sb, int indentLevel, LogLevel levelMask, DateTime? from, DateTime? to)
        {
            // Append indent without allocating new string
            AppendIndent(sb, indentLevel);
            sb.Append("=== Logger: ");
            sb.Append(_fullPath);
            sb.AppendLine(" ===");

            var entries = _buffer.GetEntries(levelMask, from, to);
            foreach (var entry in entries)
            {
                AppendIndent(sb, indentLevel);
                entry.Format(sb, _includeSourceInfo);
                sb.AppendLine();
            }

            foreach (var child in _children)
            {
                sb.AppendLine();
                child.ExtractLogsInternal(sb, indentLevel + 1, levelMask, from, to);
            }
        }
        
        /// <summary>
        /// Appends indentation without allocating a new string.
        /// </summary>
        private static void AppendIndent(StringBuilder sb, int indentLevel)
        {
            for (int i = 0; i < indentLevel * 2; i++)
            {
                sb.Append(' ');
            }
        }

        /// <summary>
        /// Clears this logger's buffer.
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }

        /// <summary>
        /// Clears this logger and all children recursively.
        /// </summary>
        public void ClearAll()
        {
            Clear();
            foreach (var child in _children)
            {
                child.ClearAll();
            }
        }

        #endregion

        #region Enable/Disable Hierarchy

        /// <summary>
        /// Enables this logger and optionally all children.
        /// </summary>
        public void Enable(bool recursive = false)
        {
            Enabled = true;  // Use property to fire events and invalidate cache
            if (recursive)
            {
                foreach (var child in _children)
                    child.Enable(true);
            }
        }

        /// <summary>
        /// Disables this logger and optionally all children.
        /// </summary>
        public void Disable(bool recursive = false)
        {
            Enabled = false;  // Use property to fire events and invalidate cache
            if (recursive)
            {
                foreach (var child in _children)
                    child.Disable(true);
            }
        }

        /// <summary>
        /// Sets console output for this logger and optionally all children.
        /// </summary>
        public void SetConsoleOutput(bool enabled, bool recursive = false)
        {
            _consoleOutput = enabled;
            if (recursive)
            {
                foreach (var child in _children)
                    child.SetConsoleOutput(enabled, true);
            }
        }

        #endregion
    }
}
