using System;
using Halibut.Tests.Support;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class BindCertificatesForAllTests
    {
        public BindCertificatesForAllTests()
        {
#if SUPPORTS_WEB_SOCKET_CLIENT
            WebSocketSslCertificateHelper.AddSslCertToLocalStore();
#endif
        }
        
        [SetUpFixture]
        [Obsolete("Remove when NUnit is fully replaced with xUnit")]
        public class TestsSetupClass
        {
            [OneTimeSetUp]
            [Obsolete("Remove when NUnit is fully replaced with xUnit")]
            public void GlobalSetup()
            {
#if SUPPORTS_WEB_SOCKET_CLIENT
                WebSocketSslCertificateHelper.AddSslCertToLocalStore();
#endif
            }
        }
    }
}