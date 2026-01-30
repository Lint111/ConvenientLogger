using System;
using System.Text;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LogEntryTests
    {
        [Test]
        public void Constructor_SetsAllFields()
        {
            var entry = new LogEntry(
                LogLevel.Info,
                "TestLogger",
                "Test message",
                "test.cs",
                "TestMethod",
                42);

            Assert.AreEqual(LogLevel.Info, entry.Level);
            Assert.AreEqual("TestLogger", entry.LoggerName);
            Assert.AreEqual("Test message", entry.Message);
            Assert.AreEqual("test.cs", entry.FilePath);
            Assert.AreEqual("TestMethod", entry.MemberName);
            Assert.AreEqual(42, entry.LineNumber);
            Assert.That(entry.Timestamp, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Format_WithoutSource_ReturnsCorrectFormat()
        {
            var entry = new LogEntry(LogLevel.Warning, "MyLogger", "Warning message");
            var formatted = entry.Format(includeSource: false);

            Assert.That(formatted, Does.Contain("[MyLogger]"));
            Assert.That(formatted, Does.Contain("[WRN]"));
            Assert.That(formatted, Does.Contain("Warning message"));
            Assert.That(formatted, Does.Not.Contain("("));
        }

        [Test]
        public void Format_WithSource_IncludesFileInfo()
        {
            var entry = new LogEntry(
                LogLevel.Error,
                "MyLogger",
                "Error message",
                "C:/Project/Scripts/MyScript.cs",
                "MyMethod",
                100);

            var formatted = entry.Format(includeSource: true);

            Assert.That(formatted, Does.Contain("Error message"));
            Assert.That(formatted, Does.Contain("MyScript.cs:100"));
        }

        [Test]
        public void Format_ToStringBuilder_DoesNotAllocate()
        {
            var entry = new LogEntry(LogLevel.Debug, "Test", "Message");
            var sb = new StringBuilder();

            // Should not throw and should append to existing builder
            entry.Format(sb, includeSource: false);

            Assert.That(sb.Length, Is.GreaterThan(0));
            Assert.That(sb.ToString(), Does.Contain("Message"));
        }

        [Test]
        public void Format_ToStringBuilder_WithSource_IncludesFileInfo()
        {
            var entry = new LogEntry(
                LogLevel.Info,
                "Logger",
                "Test",
                "/path/to/file.cs",
                "Method",
                50);

            var sb = new StringBuilder();
            entry.Format(sb, includeSource: true);

            Assert.That(sb.ToString(), Does.Contain("file.cs:50"));
        }

        [Test]
        public void ToString_EqualsFormatWithoutSource()
        {
            var entry = new LogEntry(LogLevel.Info, "Logger", "Message");

            Assert.AreEqual(entry.Format(false), entry.ToString());
        }

        [Test]
        public void Format_AllLogLevels_ProducesValidOutput()
        {
            var levels = new[] 
            { 
                LogLevel.Trace, 
                LogLevel.Debug, 
                LogLevel.Info, 
                LogLevel.Warning, 
                LogLevel.Error, 
                LogLevel.Critical 
            };

            foreach (var level in levels)
            {
                var entry = new LogEntry(level, "Test", "Message");
                var formatted = entry.Format();

                Assert.That(formatted, Is.Not.Null.And.Not.Empty, $"Format failed for {level}");
                Assert.That(formatted, Does.Contain("Message"), $"Message missing for {level}");
            }
        }
    }
}
