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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Server.Dispatch;
using Halibut.Server.Security;
using Halibut.Server.ServiceModel;

namespace Halibut.Server
{
    public class HalibutServer : IDisposable
    {
        readonly IPEndPoint endPoint;
        readonly IHalibutServerOptions options = new HalibutServerOptions();
        readonly X509Certificate2 serverCertificate;
        readonly IServiceCatalog services = new ServiceCatalog();
        TcpListener listener;

        public HalibutServer(IPEndPoint endPoint, X509Certificate2 serverCertificate)
        {
            this.endPoint = endPoint;
            this.serverCertificate = serverCertificate;

            EnsureCertificateIsValidForListening(serverCertificate);
        }

        public IHalibutServerOptions Options
        {
            get { return options; }
        }

        public IServiceCatalog Services
        {
            get { return services; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            listener.Stop();
        }

        #endregion

        public void Start()
        {
            listener = new TcpListener(endPoint);
            listener.Start();

            Accept();
        }

        void Accept()
        {
            listener.BeginAcceptTcpClient(r =>
                                              {
                                                  try
                                                  {
                                                      Logs.Server.Info("Accepting TCP client");
                                                      TcpClient client = listener.EndAcceptTcpClient(r);
                                                      var task = new Task(() => ExecuteRequest(client));
                                                      task.Start(options.Scheduler);
                                                  }
                                                  catch (Exception ex)
                                                  {
                                                      Logs.Server.Warn("TCP client error: " + ex.ToString());
                                                  }

                                                  Accept();
                                              }, null);
        }

        void ExecuteRequest(TcpClient client)
        {
            using (NetworkStream stream = client.GetStream())
            using (var ssl = new SslStream(stream, false, ValidateCertificate))
            {
                try
                {
                    ssl.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls, false);

                    IRequestProcessor processor = options.RequestProcessorFactory.CreateProcessor(services, options);
                    processor.Execute(ssl);
                }
                catch (AuthenticationException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        bool ValidateCertificate(object sender, X509Certificate clientCertificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            if (sslpolicyerrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                return false;

            return options.ClientCertificateValidator(new X509Certificate2(clientCertificate)) == CertificateValidationResult.Valid;
        }

        static void EnsureCertificateIsValidForListening(X509Certificate2 certificate)
        {
            if (certificate == null) throw new Exception("No certificate was provided.");

            if (!certificate.HasPrivateKey)
            {
                throw new Exception("The X509 certificate provided does not have a private key, and so it cannot be used for listening.");
            }
        }
    }
}