using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Util
{
    public class WebSocketListeningPrerequisites : IDisposable
    {
        public WebSocketListeningPrerequisites()
        {
            Port = FreeTcpPortFinder.Find();
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                AddSslCertToLocalStoreAndRegisterFor($"0.0.0.0:{Port}");
        }

        public int Port { get;}

        public void Dispose()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                RemoveSslCertBindingFor($"0.0.0.0:{Port}");
        }
        
        static void AddSslCertToLocalStoreAndRegisterFor(string address)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(Certificates.Ssl);
            store.Close();


            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo("netsh", $"http add sslcert ipport={address} certhash={Certificates.SslThumbprint} appid={{2e282bfb-fce9-40fc-a594-2136043e1c8f}}")
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

        static void RemoveSslCertBindingFor(string address)
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo("netsh", $"http delete sslcert ipport={address}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            proc.StartInfo.UseShellExecute = false;
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