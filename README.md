# Convenient Logger

A low-allocation hierarchical logging system for Unity with per-window filtering and export capabilities.

## Features

- **Hierarchical Loggers** - Parent-child relationships for organized logging
- **Low Allocation** - `[Conditional]` attributes strip logging calls in release builds
- **Per-Window Control** - Each EditorWindow has its own logger icon for filtering
- **Global Export** - Export all logs at end of play session or on demand
- **Time Range Filtering** - Export logs from specific time periods

## Installation

Add to your Unity project via Package Manager:
1. Open Package Manager (Window > Package Manager)
2. Click "+" and select "Add package from disk..."
3. Navigate to `package.json` in this folder

Or add to `manifest.json`:
```json
{
  "dependencies": {
    "com.vixen.convenientlogger": "file:../ConvenientLogger"
  }
}
```

## Quick Start

### For EditorWindows

Inherit from `LoggableEditorWindow` instead of `EditorWindow`:

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
        LogDebug("Starting operation...");
        
        // Avoid allocation when logging is disabled
        if (ShouldLog(LogLevel.Trace))
        {
            LogTrace($"Expensive debug info: {GetExpensiveData()}");
        }
        
        LogInfo("Operation complete");
    }
}
```

A logger icon appears in the top-right corner. Click to:
- Enable/disable logging
- Filter by log level
- Toggle console output
- Export or clear logs

### For Regular Classes

Inherit from `LoggableBase`:

```csharp
using ConvenientLogger;

public class MyService : LoggableBase
{
    public MyService()
    {
        InitializeLogger("MyService", enabled: true);
    }

    public void Process()
    {
        LogInfo("Processing started");
        // ...
        LogDebug("Details...");
    }
}
```

### Hierarchical Logging

Create parent-child relationships:

```csharp
var parentLogger = new Logger("AnimationSystem");
var childLogger = parentLogger.CreateChild("Preview");
var grandchildLogger = childLogger.CreateChild("Timeline");

// grandchildLogger.FullPath = "AnimationSystem/Preview/Timeline"
```

### Conditional Logging (Zero Allocation)

Use static `Log` methods with `[Conditional]` attributes:

```csharp
// These are completely stripped when CONVENIENT_LOGGER_ENABLED is not defined
Log.Debug(logger, "This won't allocate in release");
Log.Info(logger, "Neither will this");

// Errors are always logged (no conditional)
Log.Error(logger, "This is always logged");
```

### Lazy Logging (Deferred Allocation)

For expensive string operations:

```csharp
// messageFactory is only called if logging is enabled
Log.LazyDebug(logger, () => $"Object state: {expensiveObject.ToDetailedString()}");
```

### Scoped Logging (Timing)

```csharp
using (Log.Scope(logger, "ExpensiveOperation"))
{
    // ... do work ...
}
// Logs: ">>> ExpensiveOperation BEGIN" and "<<< ExpensiveOperation END (42.50ms)"
```

## Global Settings

Open **Edit > Preferences > Convenient Logger** to configure:

- **Auto-Export on Play Mode Exit** - Automatically save logs when exiting play mode
- **Export Directory** - Where to save auto-exported logs
- **Max Export Files** - Limit number of saved log files
- **Time Range** - Export only recent logs

## Log Levels

| Level | Description | Conditional |
|-------|-------------|-------------|
| Trace | Verbose debugging | CONVENIENT_LOGGER_VERBOSE |
| Debug | Development info | CONVENIENT_LOGGER_ENABLED |
| Info | Notable events | CONVENIENT_LOGGER_ENABLED |
| Warning | Potential issues | CONVENIENT_LOGGER_ENABLED |
| Error | Failures | Always |
| Critical | Fatal errors | Always |

## Define Symbols

Add these to Player Settings > Scripting Define Symbols:

- `CONVENIENT_LOGGER_ENABLED` - Enable Debug/Info/Warning logs
- `CONVENIENT_LOGGER_VERBOSE` - Enable Trace logs (implies ENABLED)

## API Reference

### Logger

```csharp
// Create
var logger = new Logger("MyLogger", bufferCapacity: 500);

// Configure
logger.Enabled = true;
logger.LevelMask = LogLevel.Development;
logger.ConsoleOutput = true;
logger.IncludeSourceInfo = true;

// Log
logger.Trace("...");
logger.Debug("...");
logger.Info("...");
logger.Warning("...");
logger.Error("...");
logger.Critical("...");

// Hierarchy
var child = logger.CreateChild("Child");
logger.Enable(recursive: true);

// Export
string logs = logger.ExtractLogs(levelMask: LogLevel.All);
logger.Clear();
logger.ClearAll();
```

### LoggerRegistry

```csharp
// Get or create hierarchical logger
var logger = LoggerRegistry.GetOrCreate("System/Subsystem/Component");

// Enable by pattern
LoggerRegistry.EnablePattern("System/**", consoleOutput: true);

// Export all
string allLogs = LoggerRegistry.ExtractAll(LogLevel.All, from: DateTime.Now.AddMinutes(-30));
```

## License

MIT License
