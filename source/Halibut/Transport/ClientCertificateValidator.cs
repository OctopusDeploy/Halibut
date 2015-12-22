using System;
using System.Net.Security;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class ClientCertificateValidator
    {
        readonly List<string> expectedThumbprints = new List<string>();

        public ClientCertificateValidator(string expectedThumbprint)
        {
            this.expectedThumbprints = new List<string>();
            this.expectedThumbprints.Add(expectedThumbprint);
        }

        public ClientCertificateValidator(IEnumerable<string> expectedThumbprints)
        {
            this.expectedThumbprints = expectedThumbprints.ToList();
        }

        public bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            var provided = new X509Certificate2(certificate).Thumbprint;

            if (expectedThumbprints.Contains(provided))
            {
                return true;
            }

            var msg =
                expectedThumbprints.Count > 1
                    ? string.Format(
                        "The server presented an unexpected security certificate. We expected the server to present a certificate with one of the thumbprints [{0}]. Instead, it presented a certificate with a thumbprint of '{1}'. This usually happens when the client has been configured to expect the server to have the wrong certificate, or when the certificate on the server has been regenerated and the client has not been updated. It may also happen if someone is performing a man-in-the-middle attack on the remote machine, or if a proxy server is intercepting requests. Please check the certificate used on the server, and verify that the client has been configured correctly.",
                        string.Join(", ", expectedThumbprints), provided)
                    : string.Format(
                    "The server presented an unexpected security certificate. We expected the server to present a certificate with the thumbprint '{0}'. Instead, it presented a certificate with a thumbprint of '{1}'. This usually happens when the client has been configured to expect the server to have the wrong certificate, or when the certificate on the server has been regenerated and the client has not been updated. It may also happen if someone is performing a man-in-the-middle attack on the remote machine, or if a proxy server is intercepting requests. Please check the certificate used on the server, and verify that the client has been configured correctly.",
                    expectedThumbprints[0], provided);

            throw new AuthenticationException(msg);
        }
    }
}