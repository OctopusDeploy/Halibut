#if REQUIRES_SERVICE_POINT_MANAGER
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    /// <summary>
    /// .NET Prior to Core (or 5.0) had a static, ugly, process wide way of validating client supplied certificates for WebSockets.
    ///
    /// Since Halibut typically isn't the only one accepting and making requests, it has to play nicely.
    /// 
    /// Halibut does this by wrapping the current global ServerCertificateValidationCallback with some extra logic. 
    /// It then keeps tracks of the connections being made and captures those certificate. Finally the code calls Validate
    /// to check we've captured the appropriate certificate as part of the request.
    ///
    /// This can die in flames when net465 (or similar) support is dropped.
    /// </summary>
    static class ServerCertificateInterceptor
    {
        public const string Header = "X-Octopus-RequestId";

        static readonly Dictionary<string, X509Certificate2> certificates = new Dictionary<string, X509Certificate2>();
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
                            var providedCert = new X509Certificate2(certificate.Export(X509ContentType.Cert)); // Copy the cert so that we can reference it later
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
                if (!certificates.TryGetValue(connectionId, out providedCertificate) || providedCertificate == null)
                    throw new Exception("Did not recieve a certificate from the server");

            if (providedCertificate.Thumbprint != endPoint.RemoteThumbprint)
                throw new UnexpectedCertificateException(providedCertificate, endPoint);
        }
    }
}
#endif