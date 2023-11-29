using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class ClientCertificateValidator
    {
        readonly ServiceEndPoint endPoint;

        public ClientCertificateValidator(ServiceEndPoint endPoint)
        {
            this.endPoint = endPoint;
        }

        public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslpolicyerrors)
        {
            var providedCert = new X509Certificate2(certificate!.Export(X509ContentType.Cert), (string)null!); // Copy the cert so that we can reference it later
            var providedThumbprint = providedCert.Thumbprint;

            if (providedThumbprint == endPoint.RemoteThumbprint)
            {
                return true;
            }

            throw new UnexpectedCertificateException(providedCert, endPoint);
        }
    }
}