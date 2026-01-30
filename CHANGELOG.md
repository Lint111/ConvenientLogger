# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-30

### Added

- `LoggerGroupAsset` ScriptableObject for persistent logger groups
- `LoggerGroupRegistry` for editor-time group asset discovery
- `LoggerGroupAssetEditor` with drag-drop group hierarchy management
- `Logger.EffectiveEnabled` cached property with dirty flag
- `Logger.AssignToGroup()` and `RemoveFromGroup()` for runtime group assignment
- `Logger.LogDirect()` internal method for bypassing ShouldLog checks
- `Logger.OnEnabledChanged` and `OnEffectiveEnabledChanged` events
- `StringBuilderPool` for zero-allocation formatting
- Comprehensive test suite (134 EditMode tests)

### Changed

- `Logger.Enable()` and `Disable()` now use property setter (fires events, invalidates cache)
- `LogScope` uses `LogDirect()` to ensure END is logged even if disabled mid-scope
- `LogBuffer.GetEntries()` with filter uses two-pass for single allocation
- `LoggerRegistry.GetOrCreate()` uses span-based path parsing (no string.Split)
- Removed LINQ allocations from Editor hot paths
- Extracted magic numbers to constants in `LoggerGroupAssetEditor`

### Fixed

- `Enable()`/`Disable()` now properly invalidate `EffectiveEnabled` cache
- `LogScope` END message logged even when logger disabled mid-scope
- Cache key consistency in `LoggerRegistry.GetOrCreate()`

## [0.1.0] - 2026-01-29

### Added

- Initial release
- `Logger` class with hierarchical parent-child relationships
- `LogLevel` enum with flags support (Trace, Debug, Info, Warning, Error, Critical)
- `LogEntry` struct for low-allocation log storage
- `LogBuffer` ring buffer for bounded memory usage
- `ILoggable` interface and `LoggableBase` base class
- `LoggerRegistry` for global logger management
- `Log` static class with `[Conditional]` attribute methods
- `LogScope` for timed scoped logging
- `LoggableEditorWindow` base class with logger icon overlay
- `LogExportSettings` for global export configuration
- `LogFilterPopup` for per-logger filtering UI
- Auto-export on play mode exit
- Time range filtering for exports
- Unity Preferences integration
