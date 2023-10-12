using System;
using System.IO;
using System.Threading;
using Halibut.Tests.Support;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Serilog;
using Xunit;

namespace Halibut.Tests
{
    public class BaseTest
    {
        CancellationTokenSource? cancellationTokenSource;
        string? traceLogFilePath;
        
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;
        

        [SetUp]
        public void SetUp()
        {
            traceLogFilePath = Path.GetTempFileName();
            Logger = new SerilogLoggerBuilder()
                .SetTraceLogFilePath(traceLogFilePath)
                .Build()
                .ForContext(GetType());
            
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }
        
        [TearDown]
        public void TearDown()
        {
            Logger.Information("Staring Test Tearing Down");
            
            Logger.Information("Cancelling CancellationTokenSource");
            cancellationTokenSource?.Cancel();
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();

            if (TestContext.CurrentContext.Result.Outcome == ResultState.Success)
            {
                Logger.Information("Deleting trace log file for successful test");
                try
                {
                    File.Delete(traceLogFilePath!);
                }
                catch
                {
                    Logger.Information("Couldn't delete trace log file /shrug");
                }
            }
            else
            {
                // TODO: Copy file to TeamCity artifacts
                var dir = Directory.GetCurrentDirectory();
                Logger.Information($"Current directory is: {dir}");
                TestContext.AddTestAttachment(traceLogFilePath!, "Trace logs");
            }
            
            Logger.Information("Finished Test Tearing Down");
        }
    }

    // TODO: @server-at-scale - Merge with above when tests are completely cut over to xUnit.
    public class BaseTestXUnit : 
        IClassFixture<BindCertificatesForAllTests>,
        IClassFixture<BumpThreadPoolForAllTests>,
        IClassFixture<LowerHalibutLimitsForAllTests>,
        IDisposable
    {
        readonly CancellationTokenSource? cancellationTokenSource;
        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; }

        public BaseTestXUnit()
        {
            Logger = new SerilogLoggerBuilder().Build().ForContext(GetType());
            Logger.Information("Test started");
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }

        public void Dispose()
        {
            Logger.Information("Staring Test Tearing Down");
            Logger.Information("Cancelling CancellationTokenSource");
            cancellationTokenSource?.Cancel();
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();
            Logger.Information("Finished Test Tearing Down");
        }
    }
}
