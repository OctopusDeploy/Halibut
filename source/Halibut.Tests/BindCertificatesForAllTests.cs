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
#if SUPPORTS_WEB_SOCKET_CLIENT
                WebSocketSslCertificateHelper.AddSslCertToLocalStore();
#endif
            }
        }
    }
}