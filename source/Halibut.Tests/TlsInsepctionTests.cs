using System;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.TestUtils.Contracts;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TlsInspectionTests : BaseTest
    {
        
        [Test]
        public void TrustAnyCertificateIssuedByTrustedCertificateRootAuthority()
        {
            var services = GetDelegateServiceFactory();
            var clientTrustProvider = new DefaultTrustProvider();
            using (var octopusServer = new HalibutRuntimeBuilder()
                       .WithServerCertificate(CertAndThumbprint.Octopus.Certificate2)
                       .Build())
            using (var tentaclePolling = new HalibutRuntimeBuilder()
                       .WithServiceFactory(services)
                       .WithServerCertificate(CertAndThumbprint.TentaclePolling.Certificate2)
                       .WithTrustProvider(clientTrustProvider)
                       .Build())
            {
                octopusServer.Trust(CertAndThumbprint.TentaclePolling.Thumbprint);
                
                var port = octopusServer.Listen();
                
                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + port), CertAndThumbprint.Octopus.Thumbprint));

                var echo = octopusServer.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", CertAndThumbprint.TentaclePolling.Thumbprint);

                var result = echo.SayHello("World");
                result.Should().Be("World...");
            }
        }
        
        
        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }
        
    }
}