using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.SampleProgram.Base.Services
{
    public class StayAliveUntilHelper
    {
        public static async Task WaitUntilSignaledToDie()
        {
            var stayAliveFile = SettingsHelper.CompatBinaryStayAliveLockFile();
            while (true)
            {
                try
                {
                    Console.WriteLine("Waiting to lock");
                    using (var fileStreamLock = new FileStream(stayAliveFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        Console.WriteLine("Got a lock, going to die now");
                        fileStreamLock.Dispose();    
                    }

                    try
                    {
                        File.Delete(stayAliveFile);
                    }
                    finally
                    {
                        Environment.Exit(0);
                    }
                }
                catch (Exception e)
                {
                
                }

                if (!File.Exists(stayAliveFile))
                {
                    Environment.Exit(0);
                }
                
                Thread.Sleep(2000);
            }
            
        }
    }
}