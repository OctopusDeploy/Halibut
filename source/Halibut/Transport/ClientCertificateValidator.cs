using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class ClientCertificateValidator : IClientCertificateValidator
    {
        readonly ServiceEndPoint endPoint;

        public ClientCertificateValidator(ServiceEndPoint endPoint)
        {
            this.endPoint = endPoint;
        }

        public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            var providedCert = new X509Certificate2(certificate.Export(X509ContentType.Cert), (string)null!); // Copy the cert so that we can reference it later
            var providedThumbprint = providedCert.Thumbprint;

            if (providedThumbprint == endPoint.RemoteThumbprint)
            {
                return true;
            }

            throw new UnexpectedCertificateException(providedCert, endPoint);
        }
    }

    class TrustRootCertificateAuthorityValidator : IClientCertificateValidator
    {
        public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
           var result = new X509Certificate2(certificate).Verify();
           return result;
        }
    }

    public interface IClientCertificateValidator
    {
        bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors);
    }
    
    public interface IClientCertificateValidatorFactory
    {
        IClientCertificateValidator Create(ServiceEndPoint serviceEndpoint);
    }
    
    public class ClientCertificateValidatorFactory : IClientCertificateValidatorFactory
    {
        public IClientCertificateValidator Create(ServiceEndPoint serviceEndpoint)
        {
            return new ClientCertificateValidator(serviceEndpoint);
        }
    }
    
    public class TrustRootCertificateAuthorityValidatorFactory : IClientCertificateValidatorFactory
    {
        public IClientCertificateValidator Create(ServiceEndPoint serviceEndpoint)
        {
            return new TrustRootCertificateAuthorityValidator();
        }
    }
}