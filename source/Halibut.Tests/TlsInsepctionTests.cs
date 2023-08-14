using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.TestUtils.Contracts;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class TlsInspectionTests : BaseTest
    {
        
        [Test]
        public void TrustExplicitlySpecifiedCerts()
        {
            var services = GetDelegateServiceFactory();
            using (var octopusServer = new HalibutRuntimeBuilder()
                       .WithServerCertificate(CertAndThumbprint.Octopus.Certificate2)
                       .Build())
            using (var tentaclePolling = new HalibutRuntimeBuilder()
                       .WithServiceFactory(services)
                       .WithServerCertificate(CertAndThumbprint.TentaclePolling.Certificate2)
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
        
        [Test]
        public void TrustAnyCertificateIssuedByTrustedCertificateRootAuthority()
        {
            var store = new X509Store(StoreName.My);
            store.Open(OpenFlags.ReadOnly);

            var trustedCert = FindTrustedCert(store.Certificates);
            var serverCert = trustedCert;
            
            var services = GetDelegateServiceFactory();
            using (var octopusServer = new HalibutRuntimeBuilder()
                       .WithServerCertificate(serverCert)
                       .Build())
            using (var tentaclePolling = new HalibutRuntimeBuilder()
                       .WithServiceFactory(services)
                       .WithClientCertificateValidatorFactory(new TrustRootCertificateAuthorityValidatorFactory())
                       .WithServerCertificate(CertAndThumbprint.TentaclePolling.Certificate2)
                       .Build())
            {
                octopusServer.Trust(CertAndThumbprint.TentaclePolling.Thumbprint);
                
                var port = octopusServer.Listen();
                
                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + port), null));

                var echo = octopusServer.CreateClient<IEchoService>("poll://SQ-TENTAPOLL", CertAndThumbprint.TentaclePolling.Thumbprint);

                var result = echo.SayHello("World");
                result.Should().Be("World...");
            }
        }

        X509Certificate2 FindTrustedCert(X509Certificate2Collection storeCertificates)
        {
            foreach (var storeCertificate in storeCertificates)
            {
                if (storeCertificate.Verify() && storeCertificate.HasPrivateKey)
                {
                    return storeCertificate;
                }
            }

            return null;
        }

        static DelegateServiceFactory GetDelegateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            return services;
        }
        
    }
}