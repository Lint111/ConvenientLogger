# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
