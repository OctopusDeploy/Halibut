using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Support
{
    static class WebSocketSslCertificateHelper
    {
        internal static void AddSslCertToLocalStore()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            store.Add(CertAndThumbprint.Ssl.Certificate2);

            store.Close();
        }

        internal static void AddSslCertBindingFor(string address, CertAndThumbprint certAndThumbprint)
        {
            if (certAndThumbprint.Thumbprint != CertAndThumbprint.Ssl.Thumbprint)
            {
                throw new Exception("Only the SSL certificate can be used in websockets see AddSslCertToLocalStore()");
            }
            
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"http add sslcert ipport={address} certhash={certAndThumbprint.Thumbprint} appid={{2e282bfb-fce9-40fc-a594-2136043e1c8f}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            proc.WaitForExit();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0 && !output.Contains("Cannot create a file when that file already exists"))
            {
                Console.WriteLine(output);
                Console.WriteLine(proc.StandardError.ReadToEnd());
                throw new Exception("Could not bind cert to port");
            }
        }

        internal static void RemoveSslCertBindingFor(string address)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("netsh", $"http delete sslcert ipport={address}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            proc.WaitForExit();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine(output);
                Console.WriteLine(proc.StandardError.ReadToEnd());
                throw new Exception("The system cannot find the file specified");
            }
        }
    }
}
