#nullable enable
using System;

namespace Halibut.Tests.Support
{
    class WebSocketSslCertificateBuilder
    {
        readonly string bindingAddress;

        public WebSocketSslCertificateBuilder(string bindingAddress)
        {
            this.bindingAddress = bindingAddress;
        }

        public WebSocketSslCertificate Build()
        {
            WebSocketSslCertificateHelper.AddSslCertBindingFor(bindingAddress);

            var webSocketSslCertificate = new WebSocketSslCertificate(bindingAddress);

            return webSocketSslCertificate;
        }
    }
}