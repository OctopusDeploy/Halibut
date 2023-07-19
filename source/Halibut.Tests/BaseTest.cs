using System;
using System.Threading;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;
using Serilog;

namespace Halibut.Tests
{
    public class BaseTest
    {
        CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource(TestTimeoutAttribute.TestTimeout() - (int) TimeSpan.FromSeconds(5).TotalMilliseconds);
            CancellationToken = cancellationTokenSource.Token;
            CancellationToken.Register(() =>
            {
                Logger.Error("The test timed out.");
                Assert.Fail("The test timed out.");
            });
        }
        
        [TearDown]
        public void TearDown()
        {
            Logger.Information("Tearing down");
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }
}