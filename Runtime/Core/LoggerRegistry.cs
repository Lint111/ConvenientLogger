using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConvenientLogger
{
    /// <summary>
    /// Global registry for all loggers in the application.
    /// Provides centralized access for export, filtering, and management.
    /// </summary>
    public static class LoggerRegistry
    {
        private static readonly Dictionary<string, Logger> _loggers = new();
        private static Logger _rootLogger;

        /// <summary>
        /// The root logger that all other loggers can be children of.
        /// </summary>
        public static Logger Root
        {
            get
            {
                if (_rootLogger == null)
                {
                    _rootLogger = new Logger("Root", 1000);
                    _loggers["Root"] = _rootLogger;
                }
                return _rootLogger;
            }
        }

        /// <summary>
        /// All registered loggers (flat view).
        /// </summary>
        public static IReadOnlyDictionary<string, Logger> All => _loggers;

        /// <summary>
        /// Event fired when a logger is registered.
        /// </summary>
        public static event Action<Logger> OnLoggerRegistered;

        /// <summary>
        /// Event fired when a logger is unregistered.
        /// </summary>
        public static event Action<Logger> OnLoggerUnregistered;

        /// <summary>
        /// Registers a logger with the registry.
        /// </summary>
        public static void Register(Logger logger)
        {
            if (logger == null) return;
            
            if (_loggers.TryAdd(logger.FullPath, logger))
            {
                OnLoggerRegistered?.Invoke(logger);
            }
        }

        /// <summary>
        /// Unregisters a logger from the registry.
        /// </summary>
        public static void Unregister(Logger logger)
        {
            if (logger == null) return;
            
            if (_loggers.Remove(logger.FullPath))
            {
                OnLoggerUnregistered?.Invoke(logger);
            }
        }

        /// <summary>
        /// Gets a logger by its full path. Handles both "Root/Path" and "Path" formats.
        /// </summary>
        public static Logger Get(string path)
        {
            if (_loggers.TryGetValue(path, out var logger))
                return logger;
            
            // Try with Root prefix if not found
            if (!path.StartsWith("Root/"))
            {
                if (_loggers.TryGetValue($"Root/{path}", out logger))
                    return logger;
            }
            
            return null;
        }

        /// <summary>
        /// Gets or creates a logger at the specified path.
        /// Creates parent loggers as needed.
        /// Intermediate path segments are enabled by default (they're structural, not explicit loggers).
        /// Only the final logger uses the 'enabled' parameter.
        /// Uses span-based path iteration to minimize allocations.
        /// </summary>
        public static Logger GetOrCreate(string path, bool enabled = false)
        {
            // Build the full path including Root prefix for cache lookup
            var fullPathWithRoot = string.Concat("Root/", path);
            
            if (_loggers.TryGetValue(fullPathWithRoot, out var existing))
                return existing;

            // Use span-based iteration to avoid allocating string[] from Split
            var current = Root;
            var pathSpan = path.AsSpan();
            var sb = StringBuilderPool.Get();
            
            try
            {
                sb.Append(current.FullPath);
                
                while (pathSpan.Length > 0)
                {
                    // Find next separator
                    var sepIndex = pathSpan.IndexOf('/');
                    ReadOnlySpan<char> partSpan;
                    bool isLastPart;
                    
                    if (sepIndex < 0)
                    {
                        // Last segment
                        partSpan = pathSpan;
                        pathSpan = ReadOnlySpan<char>.Empty;
                        isLastPart = true;
                    }
                    else
                    {
                        partSpan = pathSpan.Slice(0, sepIndex);
                        pathSpan = pathSpan.Slice(sepIndex + 1);
                        isLastPart = pathSpan.Length == 0;
                    }
                    
                    // Skip empty segments (e.g., "a//b")
                    if (partSpan.Length == 0) continue;
                    
                    // Build the full path for lookup
                    sb.Append('/');
                    var partStartIndex = sb.Length;
                    foreach (var c in partSpan)
                        sb.Append(c);
                    
                    var actualFullPath = sb.ToString();
                    var part = actualFullPath.Substring(partStartIndex);

                    if (_loggers.TryGetValue(actualFullPath, out var child))
                    {
                        current = child;
                    }
                    else
                    {
                        // Use CreateRegistryChild so these loggers can be assigned to groups
                        var newLogger = current.CreateRegistryChild(part);
                        // Intermediate loggers are enabled by default (structural only)
                        // Final logger uses the requested 'enabled' state
                        newLogger.Enabled = isLastPart ? enabled : true;
                        Register(newLogger);
                        current = newLogger;
                    }
                }
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }

            return current;
        }

        /// <summary>
        /// Enables all loggers matching a path pattern.
        /// Supports wildcards: * (single level), ** (any depth)
        /// </summary>
        public static void EnablePattern(string pattern, bool consoleOutput = false)
        {
            foreach (var kvp in _loggers)
            {
                if (MatchesPattern(kvp.Key, pattern))
                {
                    kvp.Value.Enabled = true;
                    kvp.Value.ConsoleOutput = consoleOutput;
                }
            }
        }

        /// <summary>
        /// Disables all loggers matching a path pattern.
        /// </summary>
        public static void DisablePattern(string pattern)
        {
            foreach (var kvp in _loggers)
            {
                if (MatchesPattern(kvp.Key, pattern))
                {
                    kvp.Value.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Clears all logger buffers.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var logger in _loggers.Values)
            {
                logger.Clear();
            }
        }

        /// <summary>
        /// Extracts all logs from all loggers.
        /// </summary>
        public static string ExtractAll(LogLevel levelMask = LogLevel.All, DateTime? from = null, DateTime? to = null)
        {
            return Root.ExtractLogs(0, levelMask, from, to);
        }

        /// <summary>
        /// Resets the registry, clearing all loggers.
        /// </summary>
        public static void Reset()
        {
            _loggers.Clear();
            _rootLogger = null;
        }
        
        /// <summary>
        /// Debug: Dumps all registered loggers to the console.
        /// </summary>
        public static void DebugDumpLoggers()
        {
            UnityEngine.Debug.Log($"=== LoggerRegistry: {_loggers.Count} loggers ===");
            foreach (var kvp in _loggers.OrderBy(x => x.Key))
            {
                var logger = kvp.Value;
                UnityEngine.Debug.Log($"  [{kvp.Key}] Enabled={logger.Enabled}, Effective={logger.EffectiveEnabled}, Parent={logger.Parent?.Name ?? "null"}");
            }
        }

        private static bool MatchesPattern(string path, string pattern)
        {
            // Simple pattern matching
            if (pattern == "*" || pattern == "**")
                return true;

            if (pattern.EndsWith("/**"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 3);
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.EndsWith("/*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 2);
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                       !path.Substring(prefix.Length + 1).Contains('/');
            }

            return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);
        }

        #region Unity Lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnDomainReload()
        {
            // Reset static state on domain reload
            _loggers.Clear();
            _rootLogger = null;
        }

        #endregion
    }
}
