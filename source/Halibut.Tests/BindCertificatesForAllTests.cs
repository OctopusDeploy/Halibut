using Halibut.Tests.Support;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BindCertificatesForAllTests
    {
        [SetUpFixture]
        public class TestsSetupClass
        {
            [OneTimeSetUp]
            public void GlobalSetup()
            {
                WebSocketSslCertificateHelper.AddSslCertToLocalStore();
            }
        }
    }
}