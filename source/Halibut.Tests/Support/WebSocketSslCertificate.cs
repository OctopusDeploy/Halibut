#nullable enable
using System;

namespace Halibut.Tests.Support
{
    class WebSocketSslCertificate : IDisposable
    {
        readonly string bindingAddress;

        internal WebSocketSslCertificate(string bindingAddress)
        {
            this.bindingAddress = bindingAddress;
        }

        public void Dispose()
        {
            WebSocketSslCertificateHelper.RemoveSslCertBindingFor(bindingAddress);
        }
    }
}