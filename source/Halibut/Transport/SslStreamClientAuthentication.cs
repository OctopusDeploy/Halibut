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
    // In .NET 8, SslClientAuthenticationOptions gained an SslStreamCertificateContext which deals with client certificates.
    // In .NET 9, we saw signal that these contexts could leak the native OpenSSL context on linux when acting as a server.
    // https://github.com/dotnet/runtime/issues/110803#issuecomment-2553658071
    //
    // It was not mentioned, but logically it holds that if the Server context could leak, so could the Client one.
    // SslClientAuthenticationOptions doesn't even exist in NetFramework, so this class provides a wrapper, enabling
    // other code to use the same API for both .NET 4.8 and .NET 8+.
    class SslStreamClientAuthentication
    {
        readonly string targetHost;
        readonly X509Certificate2Collection clientCertificates;
        readonly SslProtocols enabledSslProtocols;
        
#if !NETFRAMEWORK
        readonly SslClientAuthenticationOptions clientAuthenticationOptions;
#endif

        public SslStreamClientAuthentication(string targetHost, X509Certificate2Collection clientCertificates, SslProtocols enabledSslProtocols)
        {
            this.targetHost = targetHost;
            this.clientCertificates = clientCertificates;
            this.enabledSslProtocols = enabledSslProtocols;
            
#if !NETFRAMEWORK
            clientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                TargetHost = targetHost,
                ClientCertificates = clientCertificates,
                EnabledSslProtocols = SslConfiguration.SupportedProtocols,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            };
#endif
        }

        public async Task AuthenticateAsClientAsync(SslStream ssl, CancellationToken cancellationToken)
        {
#if NETFRAMEWORK
            // AuthenticateAsClientAsync in .NET 4.8 does not support cancellation tokens. So `cancellationToken` is not respected here.
            await ssl.AuthenticateAsClientAsync(
                targetHost,
                clientCertificates,
                enabledSslProtocols,
                false);
#else
            using var timeoutCts = new CancellationTokenSource(ssl.ReadTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            await ssl.AuthenticateAsClientAsync(clientAuthenticationOptions, linkedCts.Token);
#endif
        }
    }
}