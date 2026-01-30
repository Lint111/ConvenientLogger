# Convenient Logger

A low-allocation hierarchical logging system for Unity with per-window filtering, group management, and export capabilities.

## Features

- **Hierarchical Loggers** - Parent-child relationships with automatic state propagation
- **Logger Groups** - ScriptableObject-based groups for organizing loggers across windows
- **Low Allocation** - `[Conditional]` attributes strip logging calls in release builds
- **EffectiveEnabled Caching** - Cached hierarchy state with dirty flag for performance
- **Per-Window Control** - Each EditorWindow has its own logger icon for filtering
- **Scoped Logging** - Automatic BEGIN/END with timing measurements
- **Global Export** - Export all logs at end of play session or on demand

## Installation

Add to your Unity project via Package Manager:

**Option 1: Git URL**
1. Open Package Manager (Window > Package Manager)
2. Click "+" and select "Add package from git URL..."
3. Enter: `https://github.com/Lint111/ConvenientLogger.git`

**Option 2: Local**
```json
{
  "dependencies": {
    "com.llnt.convenientlogger": "file:../ConvenientLogger"
  }
}
```

## Quick Start

### Basic Logging

```csharp
using ConvenientLogger;

// Create a logger
var logger = new Logger("MySystem", bufferCapacity: 500);
logger.Enabled = true;

// Log messages
logger.Debug("Starting operation...");
logger.Info("Processing complete");
logger.Warning("Resource usage high");
logger.Error("Operation failed");
```

### Hierarchical Loggers

```csharp
// Create parent-child hierarchy
var parent = new Logger("AnimationSystem");
var preview = parent.CreateChild("Preview");
var timeline = preview.CreateChild("Timeline");

// timeline.FullPath = "AnimationSystem/Preview/Timeline"

// Disabling parent disables all children
parent.Enabled = false;
Console.WriteLine(timeline.EffectiveEnabled); // false
```

### Using LoggerRegistry

```csharp
// Get or create hierarchical logger (auto-creates intermediates)
var logger = LoggerRegistry.GetOrCreate("System/Subsystem/Component", enabled: true);

// Find existing logger
var existing = LoggerRegistry.Get("System/Subsystem/Component");

// Enable/disable by pattern
LoggerRegistry.EnablePattern("System/**", consoleOutput: true);
LoggerRegistry.DisablePattern("System/Debug/**");

// Export all logs
string allLogs = LoggerRegistry.ExtractAll(LogLevel.All, from: DateTime.Now.AddMinutes(-30));
```

### Conditional Logging (Zero Allocation)

```csharp
// Completely stripped when CONVENIENT_LOGGER_ENABLED is not defined
Log.Debug(logger, "Debug message");
Log.Info(logger, "Info message");
Log.Warning(logger, "Warning message");

// Errors are always logged
Log.Error(logger, "Error message");
Log.Critical(logger, "Critical message");
```

### Lazy Logging (Deferred Allocation)

```csharp
// messageFactory only called if logging is enabled
Log.LazyDebug(logger, () => $"Expensive: {ComputeExpensiveData()}");
Log.LazyInfo(logger, () => $"State: {obj.ToDetailedString()}");
Log.LazyLog(logger, LogLevel.Warning, () => BuildComplexMessage());
```

### Scoped Logging (Timing)

```csharp
using (Log.Scope(logger, "LoadAssets", LogLevel.Info))
{
    // ... do work ...
}
// Logs:
// >>> LoadAssets BEGIN
// <<< LoadAssets END (142.35ms)

// Scope respects logger state at creation time
// If disabled mid-scope, END is still logged
```

### Logger Groups (ScriptableObject)

```csharp
// Create via Assets > Create > ConvenientLogger > Logger Group

// Assign logger to group
var group = Resources.Load<LoggerGroupAsset>("MyGroup");
var logger = LoggerRegistry.GetOrCreate("MyWindow/Preview", enabled: true);
logger.AssignToGroup(group.Logger);

// Disabling group disables all assigned loggers
group.Enabled = false;
Console.WriteLine(logger.EffectiveEnabled); // false
```

### For EditorWindows

```csharp
using ConvenientLogger.Editor;

public class MyWindow : LoggableEditorWindow
{
    [MenuItem("Window/My Window")]
    public static void ShowWindow() => GetWindow<MyWindow>();

    protected override void OnEnable()
    {
        base.OnEnable(); // Initializes logger
        LogInfo("Window opened");
    }

    private void DoSomething()
    {
        LogDebug("Starting...");
        
        // Check before expensive formatting
        if (ShouldLog(LogLevel.Trace))
        {
            LogTrace($"Details: {GetExpensiveData()}");
        }
    }
}
```

## Log Levels

| Level | Description | Conditional Symbol |
|-------|-------------|-------------------|
| Trace | Verbose debugging | `CONVENIENT_LOGGER_VERBOSE` |
| Debug | Development info | `CONVENIENT_LOGGER_ENABLED` |
| Info | Notable events | `CONVENIENT_LOGGER_ENABLED` |
| Warning | Potential issues | `CONVENIENT_LOGGER_ENABLED` |
| Error | Failures | Always compiled |
| Critical | Fatal errors | Always compiled |

### Level Presets

```csharp
LogLevel.All         // All levels
LogLevel.Development // Debug | Info | Warning | Error | Critical
LogLevel.Production  // Warning | Error | Critical
LogLevel.ErrorsOnly  // Error | Critical
```

## API Reference

### Logger

```csharp
public class Logger
{
    // Construction
    Logger(string name, int bufferCapacity = 500)
    Logger CreateChild(string name, int bufferCapacity = 500)
    
    // Properties
    string Name { get; }
    string FullPath { get; }
    Logger Parent { get; }
    IReadOnlyList<Logger> Children { get; }
    LogBuffer Buffer { get; }
    
    // State
    bool Enabled { get; set; }
    bool EffectiveEnabled { get; }  // Considers parent hierarchy, cached
    LogLevel LevelMask { get; set; }
    bool ConsoleOutput { get; set; }
    bool IncludeSourceInfo { get; set; }
    
    // Group Assignment
    Logger GroupParent { get; }      // Current group assignment
    void AssignToGroup(Logger groupLogger)
    void RemoveFromGroup()
    
    // Hierarchy
    void AddChild(Logger child)
    bool RemoveChild(Logger child)
    void ClearChildren()
    
    // Logging
    void Log(LogLevel level, string message, ...)
    void Trace(string message, ...)
    void Debug(string message, ...)
    void Info(string message, ...)
    void Warning(string message, ...)
    void Error(string message, ...)
    void Critical(string message, ...)
    bool ShouldLog(LogLevel level)
    
    // Bulk Operations
    void Enable(bool recursive = false)
    void Disable(bool recursive = false)
    void SetConsoleOutput(bool enabled, bool recursive = false)
    
    // Export
    string ExtractLogs(int indentLevel = 0, LogLevel levelMask = LogLevel.All, 
                       DateTime? from = null, DateTime? to = null)
    void Clear()
    void ClearAll()  // Includes children
    
    // Events
    event Action<Logger, bool> OnEnabledChanged
    event Action<Logger> OnEffectiveEnabledChanged
}
```

### ILoggable & LoggableBase

```csharp
public interface ILoggable
{
    Logger Logger { get; }
    bool IsLoggingEnabled { get; }
    void RegisterToParentLogger(Logger parentLogger)
    void DeregisterFromParentLogger()
}

public abstract class LoggableBase : ILoggable
{
    // Properties
    Logger Logger { get; }
    bool IsLoggingEnabled { get; }
    
    // Setup
    protected void InitializeLogger(string name, bool enabled = false, int bufferCapacity = 500)
    
    // Parent Registration
    void RegisterToParentLogger(Logger parentLogger)
    void DeregisterFromParentLogger()
    
    // Control
    void EnableLogging(bool consoleOutput = false)
    void DisableLogging()
    
    // Logging (protected)
    protected bool ShouldLog(LogLevel level)
    protected void LogTrace(string message, ...)
    protected void LogDebug(string message, ...)
    protected void LogInfo(string message, ...)
    protected void LogWarning(string message, ...)
    protected void LogError(string message, ...)
    protected void LogCritical(string message, ...)
}
```

### LoggerRegistry

```csharp
public static class LoggerRegistry
{
    // Access
    static Logger Root { get; }
    static IReadOnlyDictionary<string, Logger> All { get; }
    
    // Get/Create
    static Logger Get(string path)
    static Logger GetOrCreate(string path, bool startEnabled = false)
    
    // Registration
    static void Register(Logger logger)
    static void Unregister(Logger logger)
    
    // Pattern Operations
    static void EnablePattern(string pattern, bool consoleOutput = false)
    static void DisablePattern(string pattern)
    // Patterns: "*" (single level), "**" (any depth), "Root/System/**"
    
    // Export
    static string ExtractAll(LogLevel levelMask = LogLevel.All, 
                             DateTime? from = null, DateTime? to = null)
    static void ClearAll()
    static void Reset()
    
    // Debug
    static void DebugDumpLoggers()
    
    // Events
    static event Action<Logger> OnLoggerRegistered
    static event Action<Logger> OnLoggerUnregistered
}
```

### Log (Static Utilities)

```csharp
public static class Log
{
    // Conditional Logging (stripped when symbol not defined)
    [Conditional("CONVENIENT_LOGGER_VERBOSE")]
    static void Trace(Logger logger, string message, ...)
    
    [Conditional("CONVENIENT_LOGGER_ENABLED")]
    static void Debug(Logger logger, string message, ...)
    static void Info(Logger logger, string message, ...)
    static void Warning(Logger logger, string message, ...)
    
    // Always compiled
    static void Error(Logger logger, string message, ...)
    static void Critical(Logger logger, string message, ...)
    
    // Lazy Logging (deferred message construction)
    static void LazyLog(Logger logger, LogLevel level, Func<string> messageFactory, ...)
    static void LazyDebug(Logger logger, Func<string> messageFactory, ...)
    static void LazyInfo(Logger logger, Func<string> messageFactory, ...)
    
    // Scoped Logging
    static LogScope Scope(Logger logger, string scopeName, LogLevel level = LogLevel.Debug, ...)
}

public readonly struct LogScope : IDisposable
{
    // Logs BEGIN on construction, END with timing on Dispose
    // Zero allocation when logging is disabled
    // Captures ShouldLog at construction - END logged even if disabled mid-scope
}
```

### LoggerGroupAsset

```csharp
[CreateAssetMenu(menuName = "ConvenientLogger/Logger Group")]
public class LoggerGroupAsset : ScriptableObject
{
    // Properties
    Logger Logger { get; }
    LoggerGroupAsset ParentGroup { get; }
    IReadOnlyList<LoggerGroupAsset> ChildGroups { get; }
    IReadOnlyList<string> AssignedLoggerPaths { get; }
    int AssignedLoggerCount { get; }
    
    // State
    bool Enabled { get; set; }
    bool EffectiveEnabled { get; }
    
    // Group Hierarchy
    bool AddChildGroup(LoggerGroupAsset child)
    bool RemoveChildGroup(LoggerGroupAsset child)
    bool WouldCreateCircularReference(LoggerGroupAsset potentialChild)
    void RebuildHierarchy()
    
    // Logger Assignment
    void RegisterLoggerPath(string loggerPath)
    void UnregisterLoggerPath(string loggerPath)
    bool RemoveLogger(Logger logger)
    bool RemoveLoggerByPath(string loggerPath)
    bool HasLoggerPath(string loggerPath)
    IEnumerable<Logger> GetActiveLoggers()
}
```

### LogBuffer

```csharp
public class LogBuffer
{
    LogBuffer(int capacity = 1000)
    
    int Count { get; }
    int Capacity { get; }
    
    void Add(in LogEntry entry)
    void Clear()
    
    LogEntry[] GetEntries()
    LogEntry[] GetEntries(LogLevel levelMask, DateTime? from = null, DateTime? to = null)
}
```

### LogEntry

```csharp
public readonly struct LogEntry
{
    DateTime Timestamp { get; }
    LogLevel Level { get; }
    string LoggerName { get; }
    string Message { get; }
    string FilePath { get; }
    string MemberName { get; }
    int LineNumber { get; }
    
    string Format(bool includeSource = false)
    void Format(StringBuilder sb, bool includeSource = false)  // Zero allocation
}
```

### StringBuilderPool

```csharp
public static class StringBuilderPool
{
    static StringBuilder Get()
    static void Return(StringBuilder sb)
    static void Clear()  // Clear pool (for domain reload)
}
```

## Define Symbols

Add to Player Settings > Scripting Define Symbols:

| Symbol | Effect |
|--------|--------|
| `CONVENIENT_LOGGER_ENABLED` | Enable Debug/Info/Warning logs |
| `CONVENIENT_LOGGER_VERBOSE` | Enable Trace logs (implies ENABLED) |

## Performance Notes

- **EffectiveEnabled** is cached with dirty flag - parent walks only on state change
- **LogBuffer** uses ring buffer with bounded memory
- **StringBuilderPool** reduces allocations for formatting
- **GetOrCreate** uses span-based path parsing (no string.Split allocation)
- **GetEntries** with filter uses two-pass (count then allocate) for single allocation
- **LogScope** captures ShouldLog at construction - zero allocation when disabled

## License

MIT License
