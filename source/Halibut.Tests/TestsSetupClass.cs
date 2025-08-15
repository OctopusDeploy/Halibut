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
            var logger = new SerilogLoggerBuilder()
                // Ideally we would write the logs to a static string and have a test print those out.
                // So that we can see what is happening in start up in teamcity.
                // However, the logging is a bit of a mess in halibut currently.
                .Build()
                .ForContext<TestsSetupClass>();
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