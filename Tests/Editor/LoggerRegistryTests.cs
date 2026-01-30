using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LoggerRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            LoggerRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            LoggerRegistry.Reset();
        }

        #region Root Logger

        [Test]
        public void Root_ReturnsRootLogger()
        {
            var root = LoggerRegistry.Root;

            Assert.IsNotNull(root);
            Assert.AreEqual("Root", root.Name);
            Assert.AreEqual("Root", root.FullPath);
        }

        [Test]
        public void Root_IsSingleton()
        {
            var root1 = LoggerRegistry.Root;
            var root2 = LoggerRegistry.Root;

            Assert.AreSame(root1, root2);
        }

        #endregion

        #region Registration

        [Test]
        public void Register_AddsToAll()
        {
            var logger = new Logger("Test");

            LoggerRegistry.Register(logger);

            Assert.IsTrue(LoggerRegistry.All.ContainsKey("Test"));
        }

        [Test]
        public void Register_FiresEvent()
        {
            Logger registeredLogger = null;
            LoggerRegistry.OnLoggerRegistered += (l) => registeredLogger = l;

            var logger = new Logger("Test");
            LoggerRegistry.Register(logger);

            Assert.AreSame(logger, registeredLogger);
        }

        [Test]
        public void Unregister_RemovesFromAll()
        {
            var logger = new Logger("Test");
            LoggerRegistry.Register(logger);

            LoggerRegistry.Unregister(logger);

            Assert.IsFalse(LoggerRegistry.All.ContainsKey("Test"));
        }

        [Test]
        public void Unregister_FiresEvent()
        {
            Logger unregisteredLogger = null;
            LoggerRegistry.OnLoggerUnregistered += (l) => unregisteredLogger = l;

            var logger = new Logger("Test");
            LoggerRegistry.Register(logger);
            LoggerRegistry.Unregister(logger);

            Assert.AreSame(logger, unregisteredLogger);
        }

        #endregion

        #region Get

        [Test]
        public void Get_ExistingPath_ReturnsLogger()
        {
            var logger = LoggerRegistry.GetOrCreate("MyLogger");

            var result = LoggerRegistry.Get("Root/MyLogger");

            Assert.AreSame(logger, result);
        }

        [Test]
        public void Get_WithoutRootPrefix_ReturnsLogger()
        {
            var logger = LoggerRegistry.GetOrCreate("MyLogger");

            var result = LoggerRegistry.Get("MyLogger");

            Assert.AreSame(logger, result);
        }

        [Test]
        public void Get_NonExistent_ReturnsNull()
        {
            var result = LoggerRegistry.Get("NonExistent");

            Assert.IsNull(result);
        }

        #endregion

        #region GetOrCreate

        [Test]
        public void GetOrCreate_NewPath_CreatesLogger()
        {
            var logger = LoggerRegistry.GetOrCreate("NewLogger");

            Assert.IsNotNull(logger);
            Assert.AreEqual("NewLogger", logger.Name);
        }

        [Test]
        public void GetOrCreate_ExistingPath_ReturnsSame()
        {
            var logger1 = LoggerRegistry.GetOrCreate("MyLogger");
            var logger2 = LoggerRegistry.GetOrCreate("MyLogger");

            Assert.AreSame(logger1, logger2);
        }

        [Test]
        public void GetOrCreate_NestedPath_CreatesHierarchy()
        {
            var logger = LoggerRegistry.GetOrCreate("Level1/Level2/Level3");

            Assert.AreEqual("Level3", logger.Name);
            Assert.AreEqual("Root/Level1/Level2/Level3", logger.FullPath);
            Assert.IsNotNull(logger.Parent);
            Assert.AreEqual("Level2", logger.Parent.Name);
        }

        [Test]
        public void GetOrCreate_NestedPath_IntermediateLoggersEnabled()
        {
            var logger = LoggerRegistry.GetOrCreate("A/B/C", enabled: false);

            // Final logger should be disabled (as requested)
            Assert.IsFalse(logger.Enabled);

            // Intermediate loggers should be enabled (structural)
            var levelB = LoggerRegistry.Get("Root/A/B");
            var levelA = LoggerRegistry.Get("Root/A");
            Assert.IsTrue(levelB.Enabled);
            Assert.IsTrue(levelA.Enabled);
        }

        [Test]
        public void GetOrCreate_RegistryChildren_CanAssignToGroup()
        {
            var logger = LoggerRegistry.GetOrCreate("Test");

            Assert.IsTrue(logger.CanAssignToGroup);
            Assert.IsFalse(logger.IsCodeChild);
        }

        [Test]
        public void GetOrCreate_SetsParentCorrectly()
        {
            var logger = LoggerRegistry.GetOrCreate("Parent/Child");

            Assert.AreEqual(LoggerRegistry.Get("Root/Parent"), logger.Parent);
        }

        [Test]
        public void GetOrCreate_CacheKeyConsistency()
        {
            // This tests the critical fix for cache key mismatch
            var logger1 = LoggerRegistry.GetOrCreate("DMotion/AnimationPreview");
            var logger2 = LoggerRegistry.GetOrCreate("DMotion/AnimationPreview");

            // Should be the same instance (no duplicate creation)
            Assert.AreSame(logger1, logger2);

            // Should be findable by both paths
            Assert.AreSame(logger1, LoggerRegistry.Get("Root/DMotion/AnimationPreview"));
            Assert.AreSame(logger1, LoggerRegistry.Get("DMotion/AnimationPreview"));
        }

        #endregion

        #region Pattern Matching

        [Test]
        public void EnablePattern_WildcardAll_EnablesAll()
        {
            var logger1 = LoggerRegistry.GetOrCreate("A", enabled: false);
            var logger2 = LoggerRegistry.GetOrCreate("B", enabled: false);

            LoggerRegistry.EnablePattern("**");

            Assert.IsTrue(logger1.Enabled);
            Assert.IsTrue(logger2.Enabled);
        }

        [Test]
        public void EnablePattern_PrefixWildcard_EnablesMatching()
        {
            var logger1 = LoggerRegistry.GetOrCreate("DMotion/Preview", enabled: false);
            var logger2 = LoggerRegistry.GetOrCreate("DMotion/Editor", enabled: false);
            var logger3 = LoggerRegistry.GetOrCreate("Other/Stuff", enabled: false);

            LoggerRegistry.EnablePattern("Root/DMotion/**");

            Assert.IsTrue(logger1.Enabled);
            Assert.IsTrue(logger2.Enabled);
            Assert.IsFalse(logger3.Enabled);
        }

        [Test]
        public void DisablePattern_DisablesMatching()
        {
            var logger1 = LoggerRegistry.GetOrCreate("Test/A", enabled: true);
            var logger2 = LoggerRegistry.GetOrCreate("Test/B", enabled: true);

            LoggerRegistry.DisablePattern("Root/Test/**");

            Assert.IsFalse(logger1.Enabled);
            Assert.IsFalse(logger2.Enabled);
        }

        #endregion

        #region ClearAll

        [Test]
        public void ClearAll_ClearsAllBuffers()
        {
            var logger1 = LoggerRegistry.GetOrCreate("A", enabled: true);
            var logger2 = LoggerRegistry.GetOrCreate("B", enabled: true);
            logger1.Info("Message1");
            logger2.Info("Message2");

            LoggerRegistry.ClearAll();

            Assert.AreEqual(0, logger1.Buffer.Count);
            Assert.AreEqual(0, logger2.Buffer.Count);
        }

        #endregion

        #region Reset

        [Test]
        public void Reset_ClearsAllLoggers()
        {
            LoggerRegistry.GetOrCreate("A");
            LoggerRegistry.GetOrCreate("B");

            LoggerRegistry.Reset();

            // Access Root first to trigger lazy initialization
            var root = LoggerRegistry.Root;
            Assert.IsNotNull(root);
            
            // Now only Root should exist
            Assert.AreEqual(1, LoggerRegistry.All.Count);
            Assert.IsTrue(LoggerRegistry.All.ContainsKey("Root"));
        }

        #endregion

        #region ExtractAll

        [Test]
        public void ExtractAll_IncludesAllLoggers()
        {
            var logger1 = LoggerRegistry.GetOrCreate("Logger1", enabled: true);
            var logger2 = LoggerRegistry.GetOrCreate("Logger2", enabled: true);
            logger1.Info("Message1");
            logger2.Info("Message2");

            var logs = LoggerRegistry.ExtractAll();

            Assert.That(logs, Does.Contain("Message1"));
            Assert.That(logs, Does.Contain("Message2"));
        }

        #endregion
    }
}
