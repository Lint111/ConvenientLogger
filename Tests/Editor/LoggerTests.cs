using System;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LoggerTests
    {
        private Logger _logger;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger("TestLogger", 100);
        }

        [TearDown]
        public void TearDown()
        {
            _logger = null;
        }

        #region Construction

        [Test]
        public void Constructor_SetsNameAndPath()
        {
            var logger = new Logger("MyLogger");

            Assert.AreEqual("MyLogger", logger.Name);
            Assert.AreEqual("MyLogger", logger.FullPath);
        }

        [Test]
        public void Constructor_DefaultsToEnabled()
        {
            var logger = new Logger("Test");

            Assert.IsTrue(logger.Enabled);
            Assert.IsTrue(logger.EffectiveEnabled);
        }

        [Test]
        public void Constructor_DefaultsToAllLevels()
        {
            var logger = new Logger("Test");

            Assert.AreEqual(LogLevel.All, logger.LevelMask);
        }

        #endregion

        #region Hierarchy

        [Test]
        public void CreateChild_AddsToChildren()
        {
            var child = _logger.CreateChild("Child");

            Assert.AreEqual(1, _logger.Children.Count);
            Assert.AreEqual(child, _logger.Children[0]);
        }

        [Test]
        public void CreateChild_SetsParent()
        {
            var child = _logger.CreateChild("Child");

            Assert.AreEqual(_logger, child.Parent);
        }

        [Test]
        public void CreateChild_BuildsFullPath()
        {
            var child = _logger.CreateChild("Child");
            var grandchild = child.CreateChild("Grandchild");

            Assert.AreEqual("TestLogger/Child", child.FullPath);
            Assert.AreEqual("TestLogger/Child/Grandchild", grandchild.FullPath);
        }

        [Test]
        public void CreateChild_IsCodeChild()
        {
            var child = _logger.CreateChild("Child");

            Assert.IsTrue(child.IsCodeChild);
            Assert.IsFalse(child.CanAssignToGroup);
        }

        [Test]
        public void RemoveChild_RemovesFromChildren()
        {
            var child = _logger.CreateChild("Child");

            var result = _logger.RemoveChild(child);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _logger.Children.Count);
        }

        [Test]
        public void ClearChildren_RemovesAll()
        {
            _logger.CreateChild("Child1");
            _logger.CreateChild("Child2");
            _logger.CreateChild("Child3");

            _logger.ClearChildren();

            Assert.AreEqual(0, _logger.Children.Count);
        }

        #endregion

        #region Enabled State

        [Test]
        public void Enabled_Set_FiresEvent()
        {
            bool eventFired = false;
            _logger.OnEnabledChanged += (logger, enabled) => eventFired = true;

            _logger.Enabled = false;

            Assert.IsTrue(eventFired);
        }

        [Test]
        public void Enabled_SetSameValue_DoesNotFireEvent()
        {
            bool eventFired = false;
            _logger.Enabled = true; // Already true
            _logger.OnEnabledChanged += (logger, enabled) => eventFired = true;

            _logger.Enabled = true; // Same value

            Assert.IsFalse(eventFired);
        }

        [Test]
        public void EffectiveEnabled_WhenDisabled_ReturnsFalse()
        {
            _logger.Enabled = false;

            Assert.IsFalse(_logger.EffectiveEnabled);
        }

        [Test]
        public void EffectiveEnabled_WhenParentDisabled_ReturnsFalse()
        {
            var child = _logger.CreateChild("Child");
            child.Enabled = true;
            _logger.Enabled = false;

            Assert.IsFalse(child.EffectiveEnabled);
        }

        [Test]
        public void EffectiveEnabled_CachingWorks()
        {
            var child = _logger.CreateChild("Child");

            // First access computes and caches
            Assert.IsTrue(child.EffectiveEnabled);

            // Disable parent - should invalidate cache
            _logger.Enabled = false;

            // Should now return false (cache was invalidated)
            Assert.IsFalse(child.EffectiveEnabled);
        }

        [Test]
        public void EffectiveEnabled_DeepHierarchy_PropagatesCorrectly()
        {
            var child1 = _logger.CreateChild("Child1");
            var child2 = child1.CreateChild("Child2");
            var child3 = child2.CreateChild("Child3");

            Assert.IsTrue(child3.EffectiveEnabled);

            // Disable middle of chain
            child1.Enabled = false;

            Assert.IsFalse(child2.EffectiveEnabled);
            Assert.IsFalse(child3.EffectiveEnabled);
        }

        [Test]
        public void OnEffectiveEnabledChanged_FiresWhenParentChanges()
        {
            var child = _logger.CreateChild("Child");
            bool eventFired = false;
            child.OnEffectiveEnabledChanged += (logger) => eventFired = true;

            _logger.Enabled = false;

            Assert.IsTrue(eventFired);
        }

        #endregion

        #region Logging

        [Test]
        public void Log_WhenEnabled_AddsToBuffer()
        {
            _logger.Info("Test message");

            Assert.AreEqual(1, _logger.Buffer.Count);
        }

        [Test]
        public void Log_WhenDisabled_DoesNotAddToBuffer()
        {
            _logger.Enabled = false;

            _logger.Info("Test message");

            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void Log_WhenLevelMasked_DoesNotAddToBuffer()
        {
            _logger.LevelMask = LogLevel.Error; // Only errors

            _logger.Info("Test message");

            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void ShouldLog_RespectsEnabledState()
        {
            Assert.IsTrue(_logger.ShouldLog(LogLevel.Info));

            _logger.Enabled = false;

            Assert.IsFalse(_logger.ShouldLog(LogLevel.Info));
        }

        [Test]
        public void ShouldLog_RespectsLevelMask()
        {
            _logger.LevelMask = LogLevel.Warning | LogLevel.Error;

            Assert.IsFalse(_logger.ShouldLog(LogLevel.Info));
            Assert.IsTrue(_logger.ShouldLog(LogLevel.Warning));
            Assert.IsTrue(_logger.ShouldLog(LogLevel.Error));
        }

        [Test]
        public void ShouldLog_RespectsParentState()
        {
            var child = _logger.CreateChild("Child");
            _logger.Enabled = false;

            Assert.IsFalse(child.ShouldLog(LogLevel.Info));
        }

        [Test]
        public void AllLogLevels_AddCorrectEntry()
        {
            _logger.Trace("Trace");
            _logger.Debug("Debug");
            _logger.Info("Info");
            _logger.Warning("Warning");
            _logger.Error("Error");
            _logger.Critical("Critical");

            var entries = _logger.Buffer.GetEntries();
            Assert.AreEqual(6, entries.Length);

            Assert.AreEqual(LogLevel.Trace, entries[0].Level);
            Assert.AreEqual(LogLevel.Debug, entries[1].Level);
            Assert.AreEqual(LogLevel.Info, entries[2].Level);
            Assert.AreEqual(LogLevel.Warning, entries[3].Level);
            Assert.AreEqual(LogLevel.Error, entries[4].Level);
            Assert.AreEqual(LogLevel.Critical, entries[5].Level);
        }

        #endregion

        #region Extraction

        [Test]
        public void ExtractLogs_IncludesLoggerHeader()
        {
            _logger.Info("Test");

            var logs = _logger.ExtractLogs();

            Assert.That(logs, Does.Contain("=== Logger: TestLogger ==="));
        }

        [Test]
        public void ExtractLogs_IncludesMessages()
        {
            _logger.Info("Message1");
            _logger.Warning("Message2");

            var logs = _logger.ExtractLogs();

            Assert.That(logs, Does.Contain("Message1"));
            Assert.That(logs, Does.Contain("Message2"));
        }

        [Test]
        public void ExtractLogs_IncludesChildren()
        {
            var child = _logger.CreateChild("Child");
            child.Info("Child message");

            var logs = _logger.ExtractLogs();

            Assert.That(logs, Does.Contain("Child"));
            Assert.That(logs, Does.Contain("Child message"));
        }

        [Test]
        public void ExtractLogs_RespectsLevelFilter()
        {
            _logger.Info("Info message");
            _logger.Error("Error message");

            var logs = _logger.ExtractLogs(levelMask: LogLevel.Error);

            Assert.That(logs, Does.Not.Contain("Info message"));
            Assert.That(logs, Does.Contain("Error message"));
        }

        [Test]
        public void Clear_RemovesOwnEntries()
        {
            _logger.Info("Test");
            Assert.AreEqual(1, _logger.Buffer.Count);

            _logger.Clear();

            Assert.AreEqual(0, _logger.Buffer.Count);
        }

        [Test]
        public void ClearAll_RemovesChildEntries()
        {
            var child = _logger.CreateChild("Child");
            _logger.Info("Parent");
            child.Info("Child");

            _logger.ClearAll();

            Assert.AreEqual(0, _logger.Buffer.Count);
            Assert.AreEqual(0, child.Buffer.Count);
        }

        #endregion

        #region Enable/Disable Hierarchy

        [Test]
        public void Enable_Recursive_EnablesChildren()
        {
            var child = _logger.CreateChild("Child");
            child.Enabled = false;

            _logger.Enable(recursive: true);

            Assert.IsTrue(child.Enabled);
        }

        [Test]
        public void Disable_Recursive_DisablesChildren()
        {
            var child = _logger.CreateChild("Child");
            var grandchild = child.CreateChild("Grandchild");

            _logger.Disable(recursive: true);

            Assert.IsFalse(_logger.Enabled);
            Assert.IsFalse(child.Enabled);
            Assert.IsFalse(grandchild.Enabled);
        }

        [Test]
        public void SetConsoleOutput_Recursive_SetsChildren()
        {
            var child = _logger.CreateChild("Child");
            _logger.ConsoleOutput = false;
            child.ConsoleOutput = false;

            _logger.SetConsoleOutput(true, recursive: true);

            Assert.IsTrue(_logger.ConsoleOutput);
            Assert.IsTrue(child.ConsoleOutput);
        }

        #endregion
    }
}
