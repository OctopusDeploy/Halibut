using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class UnexpectedCertificateException : AuthenticationException
    {
        public X509Certificate2 ProvidedCert { get; }
        public string ExpectedThumbprint { get; }
        public Uri ServerUrl { get; }

        const string Text = "The server at {0} presented an unexpected security certificate. We expected the server to " +
                            "present a certificate with the thumbprint '{1}'. Instead, it " +
                            "presented a certificate with a thumbprint of '{2}' and subject " +
                            "'{3}'. This usually happens when the client has been configured " +
                            "to expect the server to have the wrong certificate, or when the certificate on the " +
                            "server has been regenerated and the client has not been updated. It may also happen " +
                            "if someone is performing a man-in-the-middle attack on the remote machine, or if a " +
                            "proxy server is intercepting requests. Please check the certificate used on the server, " +
                            "and verify that the client has been configured correctly.";

        public UnexpectedCertificateException(X509Certificate2 providedCert, ServiceEndPoint endPoint)
            : base(string.Format(Text, endPoint.BaseUri, endPoint.RemoteThumbprint, providedCert.Thumbprint, providedCert.Subject))
        {
            ServerUrl = endPoint.BaseUri;
            ProvidedCert = providedCert;
            ExpectedThumbprint = endPoint.RemoteThumbprint;
        }

        public override string ToString()
        {
            //We dont want to log the stack trace here
            return Message;
        }
    }
}