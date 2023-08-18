using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.SampleProgram.Base.Services
{
    public class StayAliveUntilHelper
    {
        public static async Task WaitUntilSignaledToDie(CancellationToken cancellationToken)
        {
            var stayAliveFile = SettingsHelper.CompatBinaryStayAliveLockFile();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
#if !NETFRAMEWORK
                    await
#endif
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

                await Task.Delay(2000, cancellationToken);
            }
            
        }
    }
}