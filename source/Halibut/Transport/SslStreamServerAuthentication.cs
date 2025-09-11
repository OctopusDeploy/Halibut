// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    // In .NET 9+, we need to share a SslServerAuthenticationOptions and the underlying SslStreamCertificateContext
    // between connections to resolve a memory leak.
    // https://github.com/dotnet/runtime/issues/110803#issuecomment-2553658071
    // However these types don't exist in NetFramework. This facade
    // allows SecureListener to do the right thing for each platform without a bunch of #if's
    class SslStreamServerAuthentication
    {
        readonly X509Certificate2 certificate;
        readonly bool clientCertificateRequired;
        readonly SslProtocols enabledSslProtocols;
        readonly bool checkCertificateRevocation;

#if !NETFRAMEWORK
        readonly SslServerAuthenticationOptions serverAuthenticationOptions;
#endif

        public SslStreamServerAuthentication(X509Certificate2 certificate, bool clientCertificateRequired, SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
        {
            this.certificate = certificate;
            this.clientCertificateRequired = clientCertificateRequired;
            this.enabledSslProtocols = enabledSslProtocols;
            this.checkCertificateRevocation = checkCertificateRevocation;

#if !NETFRAMEWORK
            serverAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ClientCertificateRequired = clientCertificateRequired,
                EnabledSslProtocols = enabledSslProtocols,
                CertificateRevocationCheckMode = checkCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption
            };
#endif
        }

        public Task AuthenticateAsServerAsync(SslStream ssl, CancellationToken cancellationToken)
        {
#if NETFRAMEWORK
            // NetFramework doesn't support cancellation here
            return ssl.AuthenticateAsServerAsync(certificate, clientCertificateRequired, enabledSslProtocols, checkCertificateRevocation);
#else
            return ssl.AuthenticateAsServerAsync(serverAuthenticationOptions, cancellationToken);
#endif
        }
    }
}