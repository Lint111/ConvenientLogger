using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace ConvenientLogger
{
    /// <summary>
    /// Represents a single log entry with minimal allocation.
    /// Uses struct to avoid heap allocation for high-frequency logging.
    /// </summary>
    public readonly struct LogEntry
    {
        public readonly DateTime Timestamp;
        public readonly LogLevel Level;
        public readonly string LoggerName;
        public readonly string Message;
        public readonly string FilePath;
        public readonly string MemberName;
        public readonly int LineNumber;

        public LogEntry(
            LogLevel level,
            string loggerName,
            string message,
            string filePath = null,
            string memberName = null,
            int lineNumber = 0)
        {
            Timestamp = DateTime.Now;
            Level = level;
            LoggerName = loggerName;
            Message = message;
            FilePath = filePath;
            MemberName = memberName;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Format: [HH:mm:ss.fff] [LoggerName] [LEVEL] Message
        /// Allocates a new string - use Format(StringBuilder) for pooled formatting.
        /// </summary>
        public string Format(bool includeSource = false)
        {
            var sb = StringBuilderPool.Get();
            try
            {
                Format(sb, includeSource);
                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }

        /// <summary>
        /// Format into an existing StringBuilder - zero allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Format(StringBuilder sb, bool includeSource = false)
        {
            sb.Append('[');
            AppendTimestamp(sb, Timestamp);
            sb.Append("] [");
            sb.Append(LoggerName);
            sb.Append("] [");
            sb.Append(Level.ToShortString());
            sb.Append("] ");
            sb.Append(Message);
            
            if (includeSource && !string.IsNullOrEmpty(FilePath))
            {
                sb.Append(" (");
                AppendFileName(sb, FilePath);
                sb.Append(':');
                sb.Append(LineNumber);
                sb.Append(')');
            }
        }

        /// <summary>
        /// Append timestamp without allocating DateTime.ToString()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendTimestamp(StringBuilder sb, DateTime timestamp)
        {
            Append2Digits(sb, timestamp.Hour);
            sb.Append(':');
            Append2Digits(sb, timestamp.Minute);
            sb.Append(':');
            Append2Digits(sb, timestamp.Second);
            sb.Append('.');
            Append3Digits(sb, timestamp.Millisecond);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Append2Digits(StringBuilder sb, int value)
        {
            sb.Append((char)('0' + value / 10));
            sb.Append((char)('0' + value % 10));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Append3Digits(StringBuilder sb, int value)
        {
            sb.Append((char)('0' + value / 100));
            sb.Append((char)('0' + (value / 10) % 10));
            sb.Append((char)('0' + value % 10));
        }

        /// <summary>
        /// Append just the filename from a path without allocating Path.GetFileName()
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendFileName(StringBuilder sb, string filePath)
        {
            var lastSlash = filePath.LastIndexOfAny(PathSeparators);
            if (lastSlash >= 0)
            {
                sb.Append(filePath, lastSlash + 1, filePath.Length - lastSlash - 1);
            }
            else
            {
                sb.Append(filePath);
            }
        }

        private static readonly char[] PathSeparators = { '/', '\\' };

        public override string ToString() => Format(false);
    }

    /// <summary>
    /// Ring buffer for storing log entries with bounded memory.
    /// Thread-safe for single writer, multiple reader scenarios.
    /// </summary>
    public class LogBuffer
    {
        private readonly LogEntry[] _entries;
        private readonly int _capacity;
        private int _head;
        private int _count;
        private readonly object _lock = new();

        public LogBuffer(int capacity = 1000)
        {
            _capacity = capacity;
            _entries = new LogEntry[capacity];
            _head = 0;
            _count = 0;
        }

        public int Count
        {
            get
            {
                lock (_lock) return _count;
            }
        }

        public int Capacity => _capacity;

        public void Add(in LogEntry entry)
        {
            lock (_lock)
            {
                _entries[_head] = entry;
                _head = (_head + 1) % _capacity;
                if (_count < _capacity) _count++;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
            }
        }

        /// <summary>
        /// Gets entries in chronological order (oldest first).
        /// </summary>
        public LogEntry[] GetEntries()
        {
            lock (_lock)
            {
                var result = new LogEntry[_count];
                if (_count == 0) return result;

                int start = (_count == _capacity) ? _head : 0;
                for (int i = 0; i < _count; i++)
                {
                    result[i] = _entries[(start + i) % _capacity];
                }
                return result;
            }
        }

        /// <summary>
        /// Gets entries filtered by log level and time range.
        /// Single allocation - pre-counts matches before creating array.
        /// </summary>
        public LogEntry[] GetEntries(LogLevel levelMask, DateTime? from = null, DateTime? to = null)
        {
            lock (_lock)
            {
                if (_count == 0) return Array.Empty<LogEntry>();
                
                int start = (_count == _capacity) ? _head : 0;
                
                // First pass: count matching entries
                int matchCount = 0;
                for (int i = 0; i < _count; i++)
                {
                    ref readonly var entry = ref _entries[(start + i) % _capacity];
                    if ((entry.Level & levelMask) == 0) continue;
                    if (from.HasValue && entry.Timestamp < from.Value) continue;
                    if (to.HasValue && entry.Timestamp > to.Value) continue;
                    matchCount++;
                }
                
                if (matchCount == 0) return Array.Empty<LogEntry>();
                
                // Second pass: copy matching entries (single allocation)
                var result = new LogEntry[matchCount];
                int resultIndex = 0;
                for (int i = 0; i < _count && resultIndex < matchCount; i++)
                {
                    ref readonly var entry = ref _entries[(start + i) % _capacity];
                    if ((entry.Level & levelMask) == 0) continue;
                    if (from.HasValue && entry.Timestamp < from.Value) continue;
                    if (to.HasValue && entry.Timestamp > to.Value) continue;
                    result[resultIndex++] = entry;
                }
                
                return result;
            }
        }
    }

    /// <summary>
    /// Simple thread-safe StringBuilder pool to reduce allocations.
    /// </summary>
    public static class StringBuilderPool
    {
        private const int MaxPoolSize = 8;
        private const int DefaultCapacity = 512;
        private const int MaxCapacity = 8192;
        
        private static readonly StringBuilder[] _pool = new StringBuilder[MaxPoolSize];
        private static readonly object _lock = new();

        /// <summary>
        /// Gets a StringBuilder from the pool, or creates a new one if empty.
        /// </summary>
        public static StringBuilder Get()
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxPoolSize; i++)
                {
                    var sb = _pool[i];
                    if (sb != null)
                    {
                        _pool[i] = null;
                        sb.Clear();
                        return sb;
                    }
                }
            }
            return new StringBuilder(DefaultCapacity);
        }

        /// <summary>
        /// Returns a StringBuilder to the pool for reuse.
        /// </summary>
        public static void Return(StringBuilder sb)
        {
            if (sb == null) return;
            
            // Don't pool oversized builders
            if (sb.Capacity > MaxCapacity)
                return;
            
            lock (_lock)
            {
                for (int i = 0; i < MaxPoolSize; i++)
                {
                    if (_pool[i] == null)
                    {
                        _pool[i] = sb;
                        return;
                    }
                }
            }
            // Pool full, let it be GC'd
        }

        /// <summary>
        /// Clears the pool (useful for domain reload).
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxPoolSize; i++)
                {
                    _pool[i] = null;
                }
            }
        }
    }
}
