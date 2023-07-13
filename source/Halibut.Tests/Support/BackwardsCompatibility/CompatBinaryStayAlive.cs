using System;
using System.IO;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class CompatBinaryStayAlive : IDisposable
    {

        public static string StayAliveFilePathEnvVarKey = "CompatBinaryStayAliveFilePath";
        
        readonly TmpDirectory tmpDirectory;
        public readonly string lockFile;
        readonly FileStream fileStreamLock;
        public CompatBinaryStayAlive()
        {
            tmpDirectory = new TmpDirectory();
            lockFile = Path.Combine(tmpDirectory.FullPath, "compat-bin-lock");
            
            fileStreamLock = new FileStream(lockFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        }

        public void Dispose()
        {
            try
            {
                fileStreamLock.Dispose();
            }
            catch (Exception e)
            {
                new SerilogLoggerBuilder().Build().ForContext<CompatBinaryStayAlive>().Warning(e, "Could not release lock");
            }

            try
            {
                tmpDirectory.Dispose();
            }
            catch (Exception e)
            {
                new SerilogLoggerBuilder().Build().ForContext<CompatBinaryStayAlive>().Warning(e, "Could not delete directory");
            }
            
        }
    }
}