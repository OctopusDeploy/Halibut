using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class ClientCertificateValidator
    {
        readonly string expectedThumbprint;

        public ClientCertificateValidator(string expectedThumbprint)
        {
            this.expectedThumbprint = expectedThumbprint;
        }

        public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            var provided = new X509Certificate2(certificate).Thumbprint;

            if (provided == expectedThumbprint)
            {
                return true;
            }

            throw new AuthenticationException(string.Format("The server presented an unexpected security certificate. We expected the server to present a certificate with the thumbprint '{0}'. Instead, it presented a certificate with a thumbprint of '{1}'. This usually happens when the client has been configured to expect the server to have the wrong certificate, or when the certificate on the server has been regenerated and the client has not been updated. It may also happen if someone is performing a man-in-the-middle attack on the remote machine, or if a proxy server is intercepting requests. Please check the certificate used on the server, and verify that the client has been configured correctly.", expectedThumbprint, provided));
        }
    }
}