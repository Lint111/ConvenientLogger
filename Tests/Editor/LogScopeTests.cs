using System.Threading;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LogScopeTests
    {
        private Logger _logger;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger("TestLogger", 100);
            _logger.Enabled = true;
            _logger.LevelMask = LogLevel.All;
        }

        [TearDown]
        public void TearDown()
        {
            _logger = null;
        }

        [Test]
        public void Scope_WhenEnabled_LogsBeginAndEnd()
        {
            using (Log.Scope(_logger, "TestScope", LogLevel.Debug))
            {
                // Do something
            }

            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(2, entries.Length);
            Assert.That(entries[0].Message, Does.Contain("BEGIN"));
            Assert.That(entries[0].Message, Does.Contain("TestScope"));
            Assert.That(entries[1].Message, Does.Contain("END"));
            Assert.That(entries[1].Message, Does.Contain("TestScope"));
        }

        [Test]
        public void Scope_WhenDisabled_DoesNotLog()
        {
            _logger.Enabled = false;

            using (Log.Scope(_logger, "TestScope"))
            {
                // Do something
            }

            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void Scope_WhenLevelMasked_DoesNotLog()
        {
            _logger.LevelMask = LogLevel.Error; // Only errors

            using (Log.Scope(_logger, "TestScope", LogLevel.Debug))
            {
                // Do something
            }

            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void Scope_IncludesElapsedTime()
        {
            using (Log.Scope(_logger, "TimedScope"))
            {
                Thread.Sleep(50); // Ensure measurable time
            }

            var entries = _logger.Buffer.GetEntries();
            var endEntry = entries[1];

            Assert.That(endEntry.Message, Does.Contain("ms"));
            // Should contain a number (the elapsed time)
            Assert.That(endEntry.Message, Does.Match(@"\d+\.\d+ms"));
        }

        [Test]
        public void Scope_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                using (Log.Scope(null, "TestScope"))
                {
                    // Do something
                }
            });
        }

        [Test]
        public void Scope_NestedScopes_LogsCorrectly()
        {
            using (Log.Scope(_logger, "OuterScope"))
            {
                using (Log.Scope(_logger, "InnerScope"))
                {
                    // Do something
                }
            }

            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(4, entries.Length);

            // Order: Outer BEGIN, Inner BEGIN, Inner END, Outer END
            Assert.That(entries[0].Message, Does.Contain("OuterScope").And.Contain("BEGIN"));
            Assert.That(entries[1].Message, Does.Contain("InnerScope").And.Contain("BEGIN"));
            Assert.That(entries[2].Message, Does.Contain("InnerScope").And.Contain("END"));
            Assert.That(entries[3].Message, Does.Contain("OuterScope").And.Contain("END"));
        }

        [Test]
        public void Scope_RespectsLogLevel()
        {
            using (Log.Scope(_logger, "InfoScope", LogLevel.Info))
            {
            }
            using (Log.Scope(_logger, "WarningScope", LogLevel.Warning))
            {
            }

            var entries = _logger.Buffer.GetEntries();

            Assert.AreEqual(LogLevel.Info, entries[0].Level);
            Assert.AreEqual(LogLevel.Info, entries[1].Level);
            Assert.AreEqual(LogLevel.Warning, entries[2].Level);
            Assert.AreEqual(LogLevel.Warning, entries[3].Level);
        }

        [Test]
        public void Scope_DisabledMidScope_StillLogsEnd()
        {
            // Start with enabled
            using (Log.Scope(_logger, "TestScope"))
            {
                // Disable mid-scope - but shouldLog was captured at start
                _logger.Enabled = false;
            }

            // Should still have logged both BEGIN and END
            // because the decision was made at scope creation
            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(2, entries.Length);
        }
    }
}
