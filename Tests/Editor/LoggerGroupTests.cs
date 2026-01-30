using System;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LoggerGroupTests
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

        #region Group Assignment

        [Test]
        public void AssignToGroup_SetsGroupParent()
        {
            var groupLogger = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Test");

            logger.AssignToGroup(groupLogger);

            Assert.AreSame(groupLogger, logger.GroupParent);
        }

        [Test]
        public void AssignToGroup_GroupParentTakesPrecedence()
        {
            var groupLogger = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Test");

            logger.AssignToGroup(groupLogger);

            // Parent property should return group parent, not code parent
            Assert.AreSame(groupLogger, logger.Parent);
        }

        [Test]
        public void AssignToGroup_CodeChild_ThrowsException()
        {
            var parent = new Logger("Parent");
            var child = parent.CreateChild("Child"); // Code child
            var group = new Logger("Group");

            Assert.Throws<InvalidOperationException>(() =>
            {
                child.AssignToGroup(group);
            });
        }

        [Test]
        public void AssignToGroup_AddsToGroupChildren()
        {
            var groupLogger = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Test");

            logger.AssignToGroup(groupLogger);

            Assert.Contains(logger, (System.Collections.ICollection)groupLogger.Children);
        }

        [Test]
        public void AssignToGroup_RemovesFromPreviousGroup()
        {
            var group1 = new Logger("Group1");
            var group2 = new Logger("Group2");
            var logger = LoggerRegistry.GetOrCreate("Test");

            logger.AssignToGroup(group1);
            Assert.AreEqual(1, group1.Children.Count);

            logger.AssignToGroup(group2);

            Assert.AreEqual(0, group1.Children.Count);
            Assert.AreEqual(1, group2.Children.Count);
        }

        [Test]
        public void RemoveFromGroup_ClearsGroupParent()
        {
            var group = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Test");
            logger.AssignToGroup(group);

            logger.RemoveFromGroup();

            Assert.IsNull(logger.GroupParent);
        }

        [Test]
        public void RemoveFromGroup_RemovesFromGroupChildren()
        {
            var group = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Test");
            logger.AssignToGroup(group);

            logger.RemoveFromGroup();

            Assert.AreEqual(0, group.Children.Count);
        }

        #endregion

        #region Effective Enabled with Groups

        [Test]
        public void EffectiveEnabled_GroupDisabled_ReturnsFalse()
        {
            var group = new Logger("Group");
            group.Enabled = false;

            var logger = LoggerRegistry.GetOrCreate("Test");
            logger.Enabled = true;
            logger.AssignToGroup(group);

            Assert.IsFalse(logger.EffectiveEnabled);
        }

        [Test]
        public void EffectiveEnabled_GroupEnabled_ReturnsTrue()
        {
            var group = new Logger("Group");
            group.Enabled = true;

            var logger = LoggerRegistry.GetOrCreate("Test");
            logger.Enabled = true;
            logger.AssignToGroup(group);

            Assert.IsTrue(logger.EffectiveEnabled);
        }

        [Test]
        public void EffectiveEnabled_AssignToGroup_InvalidatesCache()
        {
            var enabledGroup = new Logger("EnabledGroup");
            enabledGroup.Enabled = true;

            var disabledGroup = new Logger("DisabledGroup");
            disabledGroup.Enabled = false;

            var logger = LoggerRegistry.GetOrCreate("Test", startEnabled: true);
            logger.AssignToGroup(enabledGroup);
            Assert.IsTrue(logger.EffectiveEnabled);

            // Change group to disabled
            logger.AssignToGroup(disabledGroup);

            // Cache should be invalidated
            Assert.IsFalse(logger.EffectiveEnabled);
        }

        [Test]
        public void OnEffectiveEnabledChanged_FiredOnGroupAssignment()
        {
            var group = new Logger("Group");
            group.Enabled = false;

            var logger = LoggerRegistry.GetOrCreate("Test");
            bool eventFired = false;
            logger.OnEffectiveEnabledChanged += (l) => eventFired = true;

            logger.AssignToGroup(group);

            Assert.IsTrue(eventFired);
        }

        #endregion

        #region Hierarchy with Groups

        [Test]
        public void GroupHierarchy_PropagatesEffectiveEnabled()
        {
            var topGroup = new Logger("TopGroup");
            var subGroup = new Logger("SubGroup");
            var logger = LoggerRegistry.GetOrCreate("Test", startEnabled: true);

            // Set up hierarchy: topGroup -> subGroup -> logger
            subGroup.AssignToGroup(topGroup);
            logger.AssignToGroup(subGroup);

            // Everything enabled
            Assert.IsTrue(logger.EffectiveEnabled);

            // Disable top group
            topGroup.Enabled = false;

            Assert.IsFalse(subGroup.EffectiveEnabled);
            Assert.IsFalse(logger.EffectiveEnabled);
        }

        [Test]
        public void CanAssignToGroup_RegistryLogger_True()
        {
            var logger = LoggerRegistry.GetOrCreate("Test");

            Assert.IsTrue(logger.CanAssignToGroup);
        }

        [Test]
        public void CanAssignToGroup_CodeChild_False()
        {
            var parent = new Logger("Parent");
            var child = parent.CreateChild("Child");

            Assert.IsFalse(child.CanAssignToGroup);
        }

        [Test]
        public void IsCodeChild_CodeChild_True()
        {
            var parent = new Logger("Parent");
            var child = parent.CreateChild("Child");

            Assert.IsTrue(child.IsCodeChild);
        }

        [Test]
        public void IsCodeChild_RegistryLogger_False()
        {
            var logger = LoggerRegistry.GetOrCreate("Test");

            Assert.IsFalse(logger.IsCodeChild);
        }

        #endregion

        #region Mixed Hierarchies

        [Test]
        public void MixedHierarchy_CodeChildWithGroupParent()
        {
            var group = new Logger("Group");
            group.Enabled = true;

            var logger = LoggerRegistry.GetOrCreate("Parent", startEnabled: true);
            var codeChild = logger.CreateChild("Child");

            logger.AssignToGroup(group);

            // Code child should follow its code parent's effective state
            // which is affected by the group
            Assert.IsTrue(codeChild.EffectiveEnabled);

            group.Enabled = false;
            Assert.IsFalse(logger.EffectiveEnabled);
            Assert.IsFalse(codeChild.EffectiveEnabled);
        }

        [Test]
        public void ChildrenNotification_PropagatesOnGroupChange()
        {
            var group = new Logger("Group");
            var logger = LoggerRegistry.GetOrCreate("Parent");
            var child1 = logger.CreateChild("Child1");
            var child2 = child1.CreateChild("Child2");

            int notificationCount = 0;
            child2.OnEffectiveEnabledChanged += (l) => notificationCount++;

            logger.AssignToGroup(group);

            // Child should be notified when parent's group changes
            Assert.IsTrue(notificationCount > 0);
        }

        #endregion
    }
}
