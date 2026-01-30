using System;

namespace ConvenientLogger
{
    /// <summary>
    /// Log severity levels, ordered from most verbose to most critical.
    /// </summary>
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Trace = 1 << 0,
        Debug = 1 << 1,
        Info = 1 << 2,
        Warning = 1 << 3,
        Error = 1 << 4,
        Critical = 1 << 5,
        
        // Common combinations
        All = Trace | Debug | Info | Warning | Error | Critical,
        Production = Info | Warning | Error | Critical,
        Development = Debug | Info | Warning | Error | Critical,
        ErrorsOnly = Error | Critical
    }

    public static class LogLevelExtensions
    {
        public static string ToShortString(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Info => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???"
            };
        }

        public static string ToLongString(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "Trace",
                LogLevel.Debug => "Debug",
                LogLevel.Info => "Info",
                LogLevel.Warning => "Warning",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Critical",
                _ => "Unknown"
            };
        }

        public static UnityEngine.LogType ToUnityLogType(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => UnityEngine.LogType.Log,
                LogLevel.Debug => UnityEngine.LogType.Log,
                LogLevel.Info => UnityEngine.LogType.Log,
                LogLevel.Warning => UnityEngine.LogType.Warning,
                LogLevel.Error => UnityEngine.LogType.Error,
                LogLevel.Critical => UnityEngine.LogType.Exception,
                _ => UnityEngine.LogType.Log
            };
        }
    }
}
