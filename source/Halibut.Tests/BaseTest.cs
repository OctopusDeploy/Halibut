using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Logging;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using ILog = Halibut.Diagnostics.ILog;
using ILogger = Serilog.ILogger;

namespace Halibut.Tests
{
    public class BaseTest
    {
        CancellationTokenSource? cancellationTokenSource;
        TraceLogFileLogger? traceLogFileLogger;

        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;
        
        public ILog HalibutLog { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            var isRetry = TestContext.CurrentContext.CurrentRepeatCount > 0;
            traceLogFileLogger = new TraceLogFileLogger(SerilogLoggerBuilder.CurrentTestHash(), appendToExisting: isRetry);
            Logger = new SerilogLoggerBuilder()
                .SetTraceLogFileLogger(traceLogFileLogger)
                .Build()
                .ForContext(GetType());
            
            HalibutLog = new TestContextLogCreator("", LogLevel.Trace).CreateNewForPrefix("");
            
            Logger.Information("Trace log file {LogFile}", traceLogFileLogger.logFilePath);

            Logger.Information(isRetry ? $"Test started (retry {TestContext.CurrentContext.CurrentRepeatCount})" : "Test started");
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;
        }

        [TearDown]
        public async Task TearDown()
        {
            Logger.Information("Staring Test Tearing Down");

            Logger.Information("Cancelling CancellationTokenSource");
            
#if NET8_0_OR_GREATER
            if (cancellationTokenSource != null)
            {
                await cancellationTokenSource.CancelAsync();    
            }
#else
            cancellationTokenSource?.Cancel();
#endif
            Logger.Information("Disposing CancellationTokenSource");
            cancellationTokenSource?.Dispose();

            

            Logger.Information("Disposing Trace Log File Logger");
            if (traceLogFileLogger != null)
            {
                await traceLogFileLogger!.DisposeAsync();
            }
            Logger.Information("Finished Test Tearing Down");
        }
    }
}
