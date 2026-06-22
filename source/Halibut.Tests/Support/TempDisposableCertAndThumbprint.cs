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
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Support
{
    public sealed class TempDisposableCertAndThumbprint : IDisposableCertAndThumbprint
    {
        readonly TemporaryDirectory tempDirectory;
        TempDisposableCertAndThumbprint(TemporaryDirectory directory, string certificatePfxPath, X509Certificate2 certificate2)
        {
            tempDirectory = directory;
            Certificate2 = certificate2;
            CertificatePfxPath = certificatePfxPath;
        }

        public X509Certificate2 Certificate2 { get; }
        public string CertificatePfxPath { get; }
        public string Thumbprint => Certificate2.Thumbprint;

        void IDisposable.Dispose()
        {
            Certificate2.Dispose();
            tempDirectory.Dispose();
        }

        public static ICertAndThumbprint CreateSelfSigned(DisposableCollection disposedBy)
        {
            var tempDir = new TemporaryDirectory();
            var name = Guid.NewGuid().ToString();
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
            var bytes = certificate.Export(X509ContentType.Pfx);
            var filePath = Path.Combine(tempDir.DirectoryPath, $"{name}.pfx");
            File.WriteAllBytes(filePath, bytes);
            var disposableCert = new TempDisposableCertAndThumbprint(tempDir, filePath, new X509Certificate2(bytes));
            disposedBy.Add(disposableCert);
            return disposableCert;
        }
    }
}