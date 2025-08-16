using System;
using System.Text;
using Halibut.Tests.Support;
using Halibut.Tests.TestSetup;
using Halibut.Tests.TestSetup.Redis;
using NUnit.Framework;

namespace Halibut.Tests
{
    /// <summary>
    /// We will have only one of these for Nunit, since multiple of these can
    /// result in pain e.g. test runners being confused, no logs showing.
    /// </summary>
    [SetUpFixture]
    public class TestsSetupClass
    {
        private ISetupFixture[] setupFixtures = new ISetupFixture[]
        {
            new EnsureRedisIsAvailableSetupFixture(),
            new BumpThreadPoolForAllTests()
        };
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var sb = new StringBuilder();
            var traceLogFileLogger = new TraceLogFileLogger("TestsSetupClass");

            var logger = new SerilogLoggerBuilder()
                .SetTraceLogFileLogger(traceLogFileLogger)
                .Build()
                .ForContext<TestsSetupClass>();
            logger.Information("Trace log file {LogFile}", traceLogFileLogger.logFilePath);
            foreach (var setupFixture in setupFixtures)
            {
                setupFixture.OneTimeSetUp(logger.ForContext(setupFixture.GetType()));
            }
            
#if SUPPORTS_WEB_SOCKET_CLIENT
            WebSocketSslCertificateHelper.AddSslCertToLocalStore();
#endif
        }
        
    }
    
}