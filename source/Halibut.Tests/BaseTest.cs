using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Support;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using ILogger = Serilog.ILogger;

namespace Halibut.Tests
{
    public class BaseTest
    {
        CancellationTokenSource? cancellationTokenSource;
        TraceLogFileLogger? traceLogFileLogger;

        public CancellationToken CancellationToken { get; private set; }
        public ILogger Logger { get; private set; } = null!;

        [SetUp]
        public void SetUp()
        {
            traceLogFileLogger = new TraceLogFileLogger(SerilogLoggerBuilder.CurrentTestHash());
            Logger = new SerilogLoggerBuilder()
                .SetTraceLogFileLogger(traceLogFileLogger)
                .Build()
                .ForContext(GetType());
            
            Logger.Information("Trace log file {LogFile}", traceLogFileLogger.logFilePath);

            Logger.Information("Test started");
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
