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
            WebSocketSslCertificateHelper.AddSslCertToLocalStoreAndRegisterFor(bindingAddress);

            var webSocketSslCertificate = new WebSocketSslCertificate(bindingAddress);

            return webSocketSslCertificate;
        }
    }
}