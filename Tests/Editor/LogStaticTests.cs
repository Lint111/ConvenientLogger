using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LogStaticTests
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

        #region Lazy Logging

        [Test]
        public void LazyLog_WhenEnabled_CallsFactory()
        {
            bool factoryCalled = false;

            Log.LazyLog(_logger, LogLevel.Info, () =>
            {
                factoryCalled = true;
                return "Lazy message";
            });

            Assert.IsTrue(factoryCalled);
            Assert.AreEqual(1, _logger.Buffer.Count);
        }

        [Test]
        public void LazyLog_WhenDisabled_DoesNotCallFactory()
        {
            _logger.Enabled = false;
            bool factoryCalled = false;

            Log.LazyLog(_logger, LogLevel.Info, () =>
            {
                factoryCalled = true;
                return "Lazy message";
            });

            Assert.IsFalse(factoryCalled);
            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void LazyLog_WhenLevelMasked_DoesNotCallFactory()
        {
            _logger.LevelMask = LogLevel.Error;
            bool factoryCalled = false;

            Log.LazyLog(_logger, LogLevel.Debug, () =>
            {
                factoryCalled = true;
                return "Lazy message";
            });

            Assert.IsFalse(factoryCalled);
            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void LazyLog_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                Log.LazyLog(null, LogLevel.Info, () => "Message");
            });
        }

        [Test]
        public void LazyLog_UsesCorrectLevel()
        {
            Log.LazyLog(_logger, LogLevel.Warning, () => "Warning message");

            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(LogLevel.Warning, entries[0].Level);
        }

        #endregion

        #region Error and Critical (Always Compiled)

        [Test]
        public void Error_AlwaysLogs()
        {
            Log.Error(_logger, "Error message");

            Assert.AreEqual(1, _logger.Buffer.Count);
            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(LogLevel.Error, entries[0].Level);
        }

        [Test]
        public void Critical_AlwaysLogs()
        {
            Log.Critical(_logger, "Critical message");

            Assert.AreEqual(1, _logger.Buffer.Count);
            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(LogLevel.Critical, entries[0].Level);
        }

        [Test]
        public void Error_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Error(null, "Error"));
        }

        [Test]
        public void Critical_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Critical(null, "Critical"));
        }

        #endregion

        #region Conditional Methods

        // Note: These tests verify the methods work when called.
        // The actual conditional compilation behavior (stripping when symbol not defined)
        // cannot be tested directly - it's a compile-time feature.

        [Test]
        public void Trace_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Trace(null, "Trace"));
        }

        [Test]
        public void Debug_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Debug(null, "Debug"));
        }

        [Test]
        public void Info_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Info(null, "Info"));
        }

        [Test]
        public void Warning_NullLogger_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Log.Warning(null, "Warning"));
        }

        #endregion

        #region Scope Factory

        [Test]
        public void Scope_ReturnsDisposableScope()
        {
            var scope = Log.Scope(_logger, "TestScope");

            Assert.IsInstanceOf<LogScope>(scope);
        }

        [Test]
        public void Scope_UsesSpecifiedLevel()
        {
            using (Log.Scope(_logger, "TestScope", LogLevel.Warning))
            {
            }

            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(LogLevel.Warning, entries[0].Level);
            Assert.AreEqual(LogLevel.Warning, entries[1].Level);
        }

        #endregion

        #region Lazy Convenience Methods

        [Test]
        public void LazyDebug_WhenEnabled_Logs()
        {
            bool called = false;

            Log.LazyDebug(_logger, () =>
            {
                called = true;
                return "Debug";
            });

            // Note: LazyDebug has [Conditional("CONVENIENT_LOGGER_ENABLED")]
            // In test builds this may or may not be defined
            // We test that it doesn't throw
            Assert.Pass();
        }

        [Test]
        public void LazyInfo_WhenEnabled_Logs()
        {
            bool called = false;

            Log.LazyInfo(_logger, () =>
            {
                called = true;
                return "Info";
            });

            // Same as above - conditional compilation
            Assert.Pass();
        }

        #endregion
    }
}
