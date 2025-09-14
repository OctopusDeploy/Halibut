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

using System.Security.Authentication;

namespace Halibut.Transport.Observability
{
    public struct SecureConnectionInfo
    {
        SecureConnectionInfo(
            SslProtocols sslProtocols,
            ConnectionDirection connectionDirection,
            string thumbprint
        )
        {
            SslProtocols = sslProtocols;
            ConnectionDirection = connectionDirection;
            Thumbprint = thumbprint;
        }

        public SslProtocols SslProtocols { get; }
        public ConnectionDirection ConnectionDirection { get; }
        public string Thumbprint { get; }

        public static SecureConnectionInfo CreateIncoming(
            SslProtocols sslProtocols,
            string thumbprint
        )
        {
            return new SecureConnectionInfo(sslProtocols, ConnectionDirection.Incoming, thumbprint);
        }

        public static SecureConnectionInfo CreateOutgoing(
            SslProtocols sslProtocols,
            string thumbprint
        )
        {
            return new(sslProtocols, ConnectionDirection.Outgoing, thumbprint);
        }
    }
}