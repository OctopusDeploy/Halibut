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
using System.Collections;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Halibut.CertificateGenerator
{
    public class CertificateGenerator
    {
        static readonly SecureRandom Random = new SecureRandom(new CryptoApiRandomGenerator());

        public static X509Certificate2 Generate(string fullName)
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    return AttemptToGenerate(fullName);
                }
                catch (CryptographicException ex)
                {
                    if (!ex.Message.StartsWith("Bad data")) throw;
                }
            }

            throw new CryptographicException("Unable to generate a certificate: Bad data.");
        }

        static X509Certificate2 AttemptToGenerate(string fullName)
        {
            var kpgen = new RsaKeyPairGenerator();

            kpgen.Init(new KeyGenerationParameters(Random, 2048));

            var cerKp = kpgen.GenerateKeyPair();

            IDictionary attrs = new Hashtable();
            attrs[X509Name.E] = "";
            attrs[X509Name.CN] = fullName;
            attrs[X509Name.O] = fullName;
            attrs[X509Name.C] = fullName;

            IList ord = new ArrayList();
            ord.Add(X509Name.E);
            ord.Add(X509Name.CN);
            ord.Add(X509Name.O);
            ord.Add(X509Name.C);

            var certGen = new X509V3CertificateGenerator();

            var serial = new byte[32];
            Random.NextBytes(serial);

            certGen.SetSerialNumber(new BigInteger(serial).Abs());
            certGen.SetIssuerDN(new X509Name(ord, attrs));
            certGen.SetNotBefore(DateTime.Today.Subtract(new TimeSpan(1, 0, 0, 0)));
            certGen.SetNotAfter(DateTime.Today.AddYears(100));
            certGen.SetSubjectDN(new X509Name(ord, attrs));
            certGen.SetPublicKey(cerKp.Public);
            certGen.SetSignatureAlgorithm("SHA1WithRSA");
            certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, true, new AuthorityKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(cerKp.Public)));
            var x509 = certGen.Generate(cerKp.Private);

            var x509Certificate = DotNetUtilities.ToX509Certificate(x509);
            return new X509Certificate2(x509Certificate)
                   {
                       PrivateKey = AddPrivateKey(cerKp)
                   };
        }

#if !__MonoCS__
        static RSACryptoServiceProvider AddPrivateKey(AsymmetricCipherKeyPair cerKp)
        {
            var tempRcsp = (RSACryptoServiceProvider) DotNetUtilities.ToRSA((RsaPrivateCrtKeyParameters) cerKp.Private);
            var rcsp = new RSACryptoServiceProvider(new CspParameters(1, "Microsoft Strong Cryptographic Provider",
                                                                      Guid.NewGuid().ToString(),
                                                                      new CryptoKeySecurity(), null));

            rcsp.ImportCspBlob(tempRcsp.ExportCspBlob(true));
            return rcsp;
        }
#else
        private static RSA AddPrivateKey(AsymmetricCipherKeyPair cerKp)
        {
            return DotNetUtilities.ToRSA((RsaPrivateCrtKeyParameters) cerKp.Private);
        }
#endif
    }
}