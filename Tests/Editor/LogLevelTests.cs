using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class LogLevelTests
    {
        [Test]
        public void ToShortString_ReturnsCorrectAbbreviations()
        {
            Assert.AreEqual("TRC", LogLevel.Trace.ToShortString());
            Assert.AreEqual("DBG", LogLevel.Debug.ToShortString());
            Assert.AreEqual("INF", LogLevel.Info.ToShortString());
            Assert.AreEqual("WRN", LogLevel.Warning.ToShortString());
            Assert.AreEqual("ERR", LogLevel.Error.ToShortString());
            Assert.AreEqual("CRT", LogLevel.Critical.ToShortString());
        }

        [Test]
        public void ToLongString_ReturnsFullNames()
        {
            Assert.AreEqual("Trace", LogLevel.Trace.ToLongString());
            Assert.AreEqual("Debug", LogLevel.Debug.ToLongString());
            Assert.AreEqual("Info", LogLevel.Info.ToLongString());
            Assert.AreEqual("Warning", LogLevel.Warning.ToLongString());
            Assert.AreEqual("Error", LogLevel.Error.ToLongString());
            Assert.AreEqual("Critical", LogLevel.Critical.ToLongString());
        }

        [Test]
        public void None_HasValueZero()
        {
            Assert.AreEqual(0, (int)LogLevel.None);
        }

        [Test]
        public void All_IncludesAllLevels()
        {
            var all = LogLevel.All;

            Assert.IsTrue((all & LogLevel.Trace) != 0);
            Assert.IsTrue((all & LogLevel.Debug) != 0);
            Assert.IsTrue((all & LogLevel.Info) != 0);
            Assert.IsTrue((all & LogLevel.Warning) != 0);
            Assert.IsTrue((all & LogLevel.Error) != 0);
            Assert.IsTrue((all & LogLevel.Critical) != 0);
        }

        [Test]
        public void Production_ExcludesTraceAndDebug()
        {
            var prod = LogLevel.Production;

            Assert.IsFalse((prod & LogLevel.Trace) != 0);
            Assert.IsFalse((prod & LogLevel.Debug) != 0);
            Assert.IsTrue((prod & LogLevel.Info) != 0);
            Assert.IsTrue((prod & LogLevel.Warning) != 0);
            Assert.IsTrue((prod & LogLevel.Error) != 0);
            Assert.IsTrue((prod & LogLevel.Critical) != 0);
        }

        [Test]
        public void Development_IncludesDebugButNotTrace()
        {
            var dev = LogLevel.Development;

            Assert.IsFalse((dev & LogLevel.Trace) != 0);
            Assert.IsTrue((dev & LogLevel.Debug) != 0);
            Assert.IsTrue((dev & LogLevel.Info) != 0);
        }

        [Test]
        public void ErrorsOnly_IncludesOnlyErrorAndCritical()
        {
            var errorsOnly = LogLevel.ErrorsOnly;

            Assert.IsFalse((errorsOnly & LogLevel.Trace) != 0);
            Assert.IsFalse((errorsOnly & LogLevel.Debug) != 0);
            Assert.IsFalse((errorsOnly & LogLevel.Info) != 0);
            Assert.IsFalse((errorsOnly & LogLevel.Warning) != 0);
            Assert.IsTrue((errorsOnly & LogLevel.Error) != 0);
            Assert.IsTrue((errorsOnly & LogLevel.Critical) != 0);
        }

        [Test]
        public void Levels_AreBitFlags()
        {
            // Each level should be a power of 2
            Assert.AreEqual(1, (int)LogLevel.Trace);
            Assert.AreEqual(2, (int)LogLevel.Debug);
            Assert.AreEqual(4, (int)LogLevel.Info);
            Assert.AreEqual(8, (int)LogLevel.Warning);
            Assert.AreEqual(16, (int)LogLevel.Error);
            Assert.AreEqual(32, (int)LogLevel.Critical);
        }

        [Test]
        public void CanCombineLevels()
        {
            var combined = LogLevel.Info | LogLevel.Warning | LogLevel.Error;

            Assert.IsFalse((combined & LogLevel.Debug) != 0);
            Assert.IsTrue((combined & LogLevel.Info) != 0);
            Assert.IsTrue((combined & LogLevel.Warning) != 0);
            Assert.IsTrue((combined & LogLevel.Error) != 0);
            Assert.IsFalse((combined & LogLevel.Critical) != 0);
        }

        [Test]
        public void CanToggleLevels()
        {
            var mask = LogLevel.All;

            // Remove Debug
            mask &= ~LogLevel.Debug;
            Assert.IsFalse((mask & LogLevel.Debug) != 0);

            // Add Debug back
            mask |= LogLevel.Debug;
            Assert.IsTrue((mask & LogLevel.Debug) != 0);

            // Toggle with XOR
            mask ^= LogLevel.Debug;
            Assert.IsFalse((mask & LogLevel.Debug) != 0);
        }

        [Test]
        public void ToShortString_UnknownLevel_ReturnsUnknown()
        {
            var unknown = (LogLevel)128; // Not a valid level

            Assert.AreEqual("???", unknown.ToShortString());
        }

        [Test]
        public void ToLongString_UnknownLevel_ReturnsUnknown()
        {
            var unknown = (LogLevel)128;

            Assert.AreEqual("Unknown", unknown.ToLongString());
        }
    }
}
