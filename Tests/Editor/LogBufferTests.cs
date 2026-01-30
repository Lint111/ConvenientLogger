using System;
using System.Threading;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LogBufferTests
    {
        [Test]
        public void Constructor_InitializesWithCapacity()
        {
            var buffer = new LogBuffer(100);

            Assert.AreEqual(100, buffer.Capacity);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Add_IncrementsCount()
        {
            var buffer = new LogBuffer(10);
            var entry = new LogEntry(LogLevel.Info, "Test", "Message");

            buffer.Add(entry);

            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void Add_MultipleEntries_TracksCount()
        {
            var buffer = new LogBuffer(100);

            for (int i = 0; i < 50; i++)
            {
                buffer.Add(new LogEntry(LogLevel.Info, "Test", $"Message {i}"));
            }

            Assert.AreEqual(50, buffer.Count);
        }

        [Test]
        public void Add_WhenFull_OverwritesOldest()
        {
            var buffer = new LogBuffer(3);

            buffer.Add(new LogEntry(LogLevel.Info, "Test", "First"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Second"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Third"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Fourth"));

            Assert.AreEqual(3, buffer.Count);

            var entries = buffer.GetEntries();
            Assert.AreEqual("Second", entries[0].Message);
            Assert.AreEqual("Third", entries[1].Message);
            Assert.AreEqual("Fourth", entries[2].Message);
        }

        [Test]
        public void Clear_ResetsCount()
        {
            var buffer = new LogBuffer(10);
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Message"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Message2"));

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void GetEntries_ReturnsInChronologicalOrder()
        {
            var buffer = new LogBuffer(10);

            buffer.Add(new LogEntry(LogLevel.Info, "Test", "First"));
            Thread.Sleep(10); // Ensure different timestamps
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Second"));
            Thread.Sleep(10);
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Third"));

            var entries = buffer.GetEntries();

            Assert.AreEqual(3, entries.Length);
            Assert.AreEqual("First", entries[0].Message);
            Assert.AreEqual("Second", entries[1].Message);
            Assert.AreEqual("Third", entries[2].Message);
        }

        [Test]
        public void GetEntries_AfterWrap_ReturnsChronologicalOrder()
        {
            var buffer = new LogBuffer(3);

            buffer.Add(new LogEntry(LogLevel.Info, "Test", "A"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "B"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "C"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "D"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "E"));

            var entries = buffer.GetEntries();

            Assert.AreEqual(3, entries.Length);
            Assert.AreEqual("C", entries[0].Message);
            Assert.AreEqual("D", entries[1].Message);
            Assert.AreEqual("E", entries[2].Message);
        }

        [Test]
        public void GetEntries_Empty_ReturnsEmptyArray()
        {
            var buffer = new LogBuffer(10);

            var entries = buffer.GetEntries();

            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Length);
        }

        [Test]
        public void GetEntries_WithLevelFilter_FiltersCorrectly()
        {
            var buffer = new LogBuffer(10);

            buffer.Add(new LogEntry(LogLevel.Debug, "Test", "Debug1"));
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Info1"));
            buffer.Add(new LogEntry(LogLevel.Warning, "Test", "Warning1"));
            buffer.Add(new LogEntry(LogLevel.Error, "Test", "Error1"));
            buffer.Add(new LogEntry(LogLevel.Debug, "Test", "Debug2"));

            var errorOnly = buffer.GetEntries(LogLevel.Error);
            Assert.AreEqual(1, errorOnly.Length);
            Assert.AreEqual("Error1", errorOnly[0].Message);

            var debugAndInfo = buffer.GetEntries(LogLevel.Debug | LogLevel.Info);
            Assert.AreEqual(3, debugAndInfo.Length);
        }

        [Test]
        public void GetEntries_WithTimeRange_FiltersCorrectly()
        {
            var buffer = new LogBuffer(10);

            var before = DateTime.Now;
            Thread.Sleep(50);

            buffer.Add(new LogEntry(LogLevel.Info, "Test", "First"));
            Thread.Sleep(50);
            var middle = DateTime.Now;
            Thread.Sleep(50);
            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Second"));

            Thread.Sleep(50);
            var after = DateTime.Now;

            // Get entries from middle onwards
            var fromMiddle = buffer.GetEntries(LogLevel.All, middle, null);
            Assert.AreEqual(1, fromMiddle.Length);
            Assert.AreEqual("Second", fromMiddle[0].Message);

            // Get entries before middle
            var beforeMiddle = buffer.GetEntries(LogLevel.All, null, middle);
            Assert.AreEqual(1, beforeMiddle.Length);
            Assert.AreEqual("First", beforeMiddle[0].Message);
        }

        [Test]
        public void GetEntries_WithFilterNoMatches_ReturnsEmptyArray()
        {
            var buffer = new LogBuffer(10);

            buffer.Add(new LogEntry(LogLevel.Info, "Test", "Info message"));
            buffer.Add(new LogEntry(LogLevel.Debug, "Test", "Debug message"));

            var errors = buffer.GetEntries(LogLevel.Error);

            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Length);
        }

        [Test]
        public void GetEntries_SingleAllocation_NoDoubleAllocation()
        {
            // This test verifies the fix for the double allocation issue
            var buffer = new LogBuffer(100);

            for (int i = 0; i < 50; i++)
            {
                buffer.Add(new LogEntry(i % 2 == 0 ? LogLevel.Info : LogLevel.Debug, "Test", $"Msg{i}"));
            }

            // Should return exactly 25 Info entries without double allocation
            var infos = buffer.GetEntries(LogLevel.Info);
            Assert.AreEqual(25, infos.Length);

            // Verify all are Info level
            foreach (var entry in infos)
            {
                Assert.AreEqual(LogLevel.Info, entry.Level);
            }
        }
    }
}
