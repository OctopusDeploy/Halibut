using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Transport
{
    class ServerCertificateInterceptor
    {
        public const string Header = "X-Octopus-RequestId";

        readonly Dictionary<string, X509Certificate2> certificates = new();
        bool initialized;


        public void Expect(string connectionId, ClientWebSocket clientWebSocket)
        {
            lock (certificates)
            {
                if (!initialized)
                    Initialize(clientWebSocket, connectionId);

                certificates[connectionId] = null;
            }
        }

        void Initialize(ClientWebSocket clientWebSocket, string connectionId)
        {
#if SUPPORTS_WEB_SOCKET_CERTIFICATE_VALIDATION_CALLBACK
            clientWebSocket.Options.RemoteCertificateValidationCallback = WebSocketServerCertificateValidationCallback(clientWebSocket.Options.RemoteCertificateValidationCallback, connectionId);
#endif
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback(ServicePointManager.ServerCertificateValidationCallback);

            initialized = true;

            RemoteCertificateValidationCallback ServerCertificateValidationCallback(RemoteCertificateValidationCallback previous)
            {
                return (sender, certificate, chain, errors) =>
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

                    return previous?.Invoke(sender, certificate, chain, errors) ?? true;
                };
            }

#if SUPPORTS_WEB_SOCKET_CERTIFICATE_VALIDATION_CALLBACK
            RemoteCertificateValidationCallback WebSocketServerCertificateValidationCallback(RemoteCertificateValidationCallback previous, string connectionId)
            {
                return (sender, certificate, chain, errors) =>
                {
                    lock (certificates)
                    {
                        if (certificates.ContainsKey(connectionId))
                        {
                            var providedCert = new X509Certificate2(certificate.Export(X509ContentType.Cert), (string)null!); // Copy the cert so that we can reference it later
                            certificates[connectionId] = providedCert;
                            return true;
                        }
                    }

                    return previous?.Invoke(sender, certificate, chain, errors) ?? true;
                };
            }
#endif
        }

        public void Remove(string connectionId)
        {
            lock (certificates)
            {
                certificates.Remove(connectionId);
            }
        }

        public void Validate(string connectionId, ServiceEndPoint endPoint)
        {
            X509Certificate2 providedCertificate;

            lock (certificates)
            {
                if (!certificates.TryGetValue(connectionId, out providedCertificate))
                {
                    throw new Exception("Did not receive a certificate from the server");
                }
            }

            if (providedCertificate.Thumbprint != endPoint.RemoteThumbprint)
            {
                throw new UnexpectedCertificateException(providedCertificate, endPoint);
            }
        }
    }
}