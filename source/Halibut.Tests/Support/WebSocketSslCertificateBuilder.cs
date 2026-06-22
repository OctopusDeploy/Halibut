#nullable enable
using System;

namespace Halibut.Tests.Support
{
    class WebSocketSslCertificateBuilder
    {
        readonly string bindingAddress;
        ICertAndThumbprint certAndThumbprint = CertAndThumbprint.Ssl;

        public WebSocketSslCertificateBuilder(string bindingAddress)
        {
            this.bindingAddress = bindingAddress;
        }

        public WebSocketSslCertificateBuilder WithCertificate(ICertAndThumbprint certAndThumbprint)
        {
            this.certAndThumbprint = certAndThumbprint;
            return this;
        }

        public WebSocketSslCertificate Build()
        {
            WebSocketSslCertificateHelper.AddSslCertBindingFor(bindingAddress, certAndThumbprint);

            var webSocketSslCertificate = new WebSocketSslCertificate(bindingAddress);

            return webSocketSslCertificate;
        }
    }
}