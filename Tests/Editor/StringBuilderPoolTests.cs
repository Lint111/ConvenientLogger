using System.Text;
using NUnit.Framework;

namespace ConvenientLogger.Tests.Editor
{
    [TestFixture]
    public class StringBuilderPoolTests
    {
        [SetUp]
        public void SetUp()
        {
            StringBuilderPool.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StringBuilderPool.Clear();
        }

        [Test]
        public void Get_ReturnsStringBuilder()
        {
            var sb = StringBuilderPool.Get();

            Assert.IsNotNull(sb);
            Assert.IsInstanceOf<StringBuilder>(sb);
        }

        [Test]
        public void Get_ReturnsEmptyStringBuilder()
        {
            var sb = StringBuilderPool.Get();
            sb.Append("Some content");
            StringBuilderPool.Return(sb);

            var sb2 = StringBuilderPool.Get();

            Assert.AreEqual(0, sb2.Length);
        }

        [Test]
        public void Return_AllowsReuse()
        {
            var sb1 = StringBuilderPool.Get();
            StringBuilderPool.Return(sb1);

            var sb2 = StringBuilderPool.Get();

            // Should be the same instance (reused from pool)
            Assert.AreSame(sb1, sb2);
        }

        [Test]
        public void Get_WhenPoolEmpty_CreatesNew()
        {
            // Get more than pool size (8)
            var builders = new StringBuilder[10];
            for (int i = 0; i < 10; i++)
            {
                builders[i] = StringBuilderPool.Get();
            }

            // All should be valid (some new, some from pool)
            foreach (var sb in builders)
            {
                Assert.IsNotNull(sb);
            }
        }

        [Test]
        public void Return_OversizedBuilder_NotPooled()
        {
            var sb1 = StringBuilderPool.Get();
            sb1.EnsureCapacity(10000); // Exceed MaxCapacity (8192)
            StringBuilderPool.Return(sb1);

            var sb2 = StringBuilderPool.Get();

            // Should be a different instance (oversized not pooled)
            Assert.AreNotSame(sb1, sb2);
        }

        [Test]
        public void Clear_EmptiesPool()
        {
            var sb1 = StringBuilderPool.Get();
            StringBuilderPool.Return(sb1);

            StringBuilderPool.Clear();

            var sb2 = StringBuilderPool.Get();

            // Should be a different instance (pool was cleared)
            Assert.AreNotSame(sb1, sb2);
        }

        [Test]
        public void Return_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => StringBuilderPool.Return(null));
        }

        [Test]
        public void ConcurrentAccess_ThreadSafe()
        {
            // Basic thread safety test
            var tasks = new System.Threading.Tasks.Task[10];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var sb = StringBuilderPool.Get();
                        sb.Append("Test");
                        StringBuilderPool.Return(sb);
                    }
                });
            }

            Assert.DoesNotThrow(() => System.Threading.Tasks.Task.WaitAll(tasks));
        }

        [Test]
        public void TypicalUsagePattern_NoLeaks()
        {
            // Simulate typical usage pattern
            for (int i = 0; i < 100; i++)
            {
                var sb = StringBuilderPool.Get();
                try
                {
                    sb.Append("Line ");
                    sb.Append(i);
                    sb.AppendLine();
                    var result = sb.ToString();
                    Assert.That(result, Does.Contain(i.ToString()));
                }
                finally
                {
                    StringBuilderPool.Return(sb);
                }
            }

            // Pool should be populated but not overflowing
            // (implementation detail: max 8 builders)
        }
    }
}
