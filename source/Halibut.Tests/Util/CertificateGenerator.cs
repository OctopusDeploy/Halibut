using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Halibut.Tests.Support;

namespace Halibut.Tests.Util
{
    public static class CertificateGenerator
    {
        public static CertAndThumbprint GenerateSelfSignedCertificate(string folderPath)
        {
            var name = Guid.NewGuid().ToString();
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            // request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            // request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
            var bytes = certificate.Export(X509ContentType.Pfx);
            var filePath = Path.Combine(folderPath, $"{name}.pfx");
            File.WriteAllBytes(filePath, bytes);
            return new CertAndThumbprint(filePath, new X509Certificate2(bytes));
        }
    }
}