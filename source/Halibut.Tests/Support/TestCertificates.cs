#if NETFRAMEWORK
using Halibut.Tests.Util;
#endif

namespace Halibut.Tests.Support
{
    /// <summary>
    /// Decides which certificate a test builder should use, based on the target framework.
    ///
    /// On .NET Framework 4.8, <see cref="System.Security.Authentication.SslProtocols.None"/> causes Windows
    /// SChannel to use a per-process TLS session cache keyed on certificate + hostname. Reusing the same static
    /// certificates across tests in one process causes incorrect session reuse when connecting to localhost,
    /// producing AuthenticationExceptions. To avoid this we generate a fresh, unique certificate per test.
    ///
    /// On other frameworks the static certificates are safe to reuse and, crucially, sharing them enables
    /// SChannel TLS session resumption (fast resumed handshakes). Generating unique certs there would defeat
    /// resumption and slow down every handshake, which can break tests that enforce short receive timeouts.
    ///
    /// All of the <c>#if NETFRAMEWORK</c> logic lives here so call sites can route through a single helper.
    /// </summary>
    public static class TestCertificates
    {
        /// <summary>
        /// Returns a new <see cref="TmpDirectory"/> to hold generated certificates on .NET Framework, or
        /// <c>null</c> on other frameworks (where no certificates are generated). The returned directory, when
        /// non-null, must be disposed by the caller.
        /// </summary>
        public static TmpDirectory? NewTmpDirectoryIfNeeded()
        {
#if NETFRAMEWORK
            return new TmpDirectory();
#else
            return null;
#endif
        }

        /// <summary>
        /// On .NET Framework, generates a fresh unique self-signed certificate into <paramref name="tmpDirectory"/>.
        /// On other frameworks, returns the supplied <paramref name="staticCert"/> so static certificates are
        /// shared (enabling TLS session resumption).
        /// </summary>
        public static CertAndThumbprint CertFor(CertAndThumbprint staticCert, TmpDirectory? tmpDirectory)
        {
#if NETFRAMEWORK
            return CertificateGenerator.GenerateSelfSignedCertificate(tmpDirectory!.FullPath);
#else
            return staticCert;
#endif
        }
    }
}
