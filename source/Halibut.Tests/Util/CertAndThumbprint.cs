using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Util;

public class CertAndThumbprint
{

    public static CertAndThumbprint TentacleListening = new CertAndThumbprint(Tests.Certificates.TentacleListening, Tests.Certificates.TentacleListeningPublicThumbprint);
    public static CertAndThumbprint TentaclePolling = new CertAndThumbprint(Tests.Certificates.TentaclePolling, Tests.Certificates.TentaclePollingPublicThumbprint);
    public static CertAndThumbprint Octopus = new CertAndThumbprint(Tests.Certificates.Octopus, Tests.Certificates.OctopusPublicThumbprint);
    
    public CertAndThumbprint(X509Certificate2 certificate2, string thumbprint)
    {
        Certificate2 = certificate2;
        Thumbprint = thumbprint;
    }

    public X509Certificate2 Certificate2 { get; }
    public string Thumbprint { get; }
    
}