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

using System;
using System.Security.Authentication;

namespace Halibut.Transport
{
    /// <summary>
    /// An implementation of ISslConfigurationProvider that uses legacy TLS protocols (1.0 and 1.1)
    /// in addition to modern ones. Protocols are explicitly specified rather than using system
    /// defaults.
    /// </summary>
    public class LegacySslConfigurationProvider : ISslConfigurationProvider
    {
#pragma warning disable SYSLIB0039
        // See https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0039
        // TLS 1.0 and 1.1 are obsolete from .NET 7
        public SslProtocols SupportedProtocols => SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
#pragma warning restore SYSLIB0039
    }
}