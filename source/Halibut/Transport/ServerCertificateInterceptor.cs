#if SUPPORTS_WEB_SOCKET_CLIENT
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    static class ServerCertificateInterceptor
    {
        public const string Header = "X-Octopus-RequestId";

        static readonly Dictionary<string, X509Certificate2> certificates = new();
        static bool initialized;


        public static void Expect(string connectionId)
        {
            lock (certificates)
            {
                if (!initialized)
                    Initialize();

                certificates[connectionId] = null;
            }
        }

        static void Initialize()
        {
            var previous = ServicePointManager.ServerCertificateValidationCallback;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                var request = sender as HttpWebRequest;
                var clientId = request?.Headers[Header];
                if (clientId != null)
                {
                    lock (certificates)
                    {
                        if (certificates.ContainsKey(clientId))
                        {
                            var providedCert = new X509Certificate2(certificate.Export(X509ContentType.Cert), (string)null!); // Copy the cert so that we can reference it later
                            certificates[clientId] = providedCert;
                            return true;
                        }
                    }
                }

                return previous == null ? true : previous(sender, certificate, chain, errors);
            };
            initialized = true;
        }

        public static void Remove(string connectionId)
        {
            lock (certificates)
                certificates.Remove(connectionId);
        }

        public static void Validate(string connectionId, ServiceEndPoint endPoint)
        {
            X509Certificate2 providedCertificate;

            lock (certificates)
                if (!certificates.TryGetValue(connectionId, out providedCertificate))
                    throw new Exception("Did not recieve a certificate from the server");

            if (providedCertificate.Thumbprint != endPoint.RemoteThumbprint)
                throw new UnexpectedCertificateException(providedCertificate, endPoint);
        }
    }
}
#endif