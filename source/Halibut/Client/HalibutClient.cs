// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Client
{
    public class HalibutClient : IHalibutClient
    {
        static readonly ILog Log = Logs.Client;
        readonly X509Certificate2 clientCertificate;
        readonly JsonSerializer serializer;

        public HalibutClient(X509Certificate2 clientCertificate) : this(null, clientCertificate)
        {
        }

        public HalibutClient(JsonSerializer serializer, X509Certificate2 clientCertificate)
        {
            this.serializer = serializer ?? DefaultJsonSerializer.Factory();
            this.clientCertificate = clientCertificate;

            SendTimeout = ReceiveTimeout = 1000*60*10;
        }

        public int SendTimeout { get; set; }
        public int ReceiveTimeout { get; set; }

        public TService Create<TService>(ServiceEndPoint endPoint)
        {
            return (TService) new HalibutProxy(this, typeof (TService), endPoint).GetTransparentProxy();
        }

        public JsonRpcResponse Post(ServiceEndPoint serviceEndpoint, JsonRpcRequest request)
        {
            using (Log.BeginActivity(request.Id, request.ActivityId))
            {
                try
                {
                    var uri = serviceEndpoint.BaseUri;

                    Log.InfoFormat("Sending request: {0}.{1} to {2}", request.Service, request.Method, uri);

                    var client = new TcpClient();
                    client.Connect(uri.Host, uri.Port);
                    client.SendTimeout = SendTimeout;
                    client.ReceiveTimeout = ReceiveTimeout;

                    var certificateValidator = new ClientCertificateValidator(serviceEndpoint.RemoteThumbprint);

                    using (var stream = client.GetStream())
                    {
                        Log.Info("TCP stream established");

                        using (var ssl = new SslStream(stream, false, certificateValidator.Validate, UserCertificateSelectionCallback))
                        {
                            Log.Info("SSL stream established");
                            Log.InfoFormat("Authenticating as client for TLS. Client certificate thumbprint: {0}", clientCertificate.Thumbprint);

                            ssl.AuthenticateAsClient(uri.Host, new X509Certificate2Collection(clientCertificate), SslProtocols.Tls, false);

                            Log.Info("SSL stream successfully authenticated as a client");
                            Log.InfoFormat("SSL stream cipher: {0} strength {1}", ssl.CipherAlgorithm, ssl.CipherStrength);
                            Log.InfoFormat("SSL stream hash: {0} strength {1}", ssl.HashAlgorithm, ssl.HashStrength);
                            Log.InfoFormat("SSL stream key exchange: {0} strength {1}", ssl.KeyExchangeAlgorithm, ssl.KeyExchangeStrength);

                            using (var writer = new BsonWriter(ssl))
                            using (var reader = new BsonReader(ssl))
                            {
                                Log.Info("Serializing request as BSON");

                                serializer.Serialize(writer, request);
                                writer.Flush();

                                Log.Info("BSON request serialized and sent");
                                Log.Info("Deserializing BSON response");

                                var response = serializer.Deserialize<JsonRpcResponse>(reader);

                                Log.Info("BSON response deserialized");

                                return response;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    Log.Error(ex.ToString());
                    throw;
                }
            }
        }

        X509Certificate UserCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return clientCertificate;
        }

        #region Nested type: ClientCertificateValidator

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

                Log.InfoFormat("We expect the server to provide a certificate with thumbprint: {0}", expectedThumbprint);
                Log.InfoFormat("The server actually provided a certificate with thumbprint: {0}", provided);

                if (provided == expectedThumbprint)
                {
                    Log.InfoFormat("Server certificate is valid.");
                    return true;
                }

                Log.Error("Server certificate was invalid");
                return false;
            }
        }

        #endregion
    }
}