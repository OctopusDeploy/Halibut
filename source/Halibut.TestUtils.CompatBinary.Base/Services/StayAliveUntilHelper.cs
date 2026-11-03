using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;

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
#if !NETFRAMEWORK
                    await
#endif
                    using (var fileStreamLock = new FileStream(stayAliveFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        NonBlockingConsole.WriteLine("Got a lock, going to die now");
                        fileStreamLock.Dispose();    
                    }

                    try
                    {
                        File.Delete(stayAliveFile);
                    }
                    finally
                    {
                        NonBlockingConsole.FlushBeforeExit();
                        Environment.Exit(0);
                    }
                }
                catch (Exception)
                {
                
                }

                if (!File.Exists(stayAliveFile))
                {
                    Environment.Exit(0);
                }

                await Task.Delay(2000);
            }
        }
    }
}