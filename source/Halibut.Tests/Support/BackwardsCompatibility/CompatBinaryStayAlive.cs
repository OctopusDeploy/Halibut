using System;
using System.IO;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class CompatBinaryStayAlive : IDisposable
    {

        public static string StayAliveFilePathEnvVarKey = "CompatBinaryStayAliveFilePath";
        
        readonly TmpDirectory tmpDirectory;
        public string LockFile { get; }
        readonly FileStream fileStreamLock;
        public CompatBinaryStayAlive()
        {
            tmpDirectory = new TmpDirectory();
            LockFile = Path.Combine(tmpDirectory.FullPath, "compat-bin-lock");
            
            fileStreamLock = new FileStream(LockFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        }

        public void Dispose()
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<CompatBinaryStayAlive>();
            try
            {
                fileStreamLock.Dispose();
            }
            catch (Exception e)
            {
                logger.Warning(e, "Could not release lock");
            }

            try
            {
                tmpDirectory.Dispose();
            }
            catch (Exception e)
            {
                logger.Warning(e, "Could not delete directory");
            }
        }
    }
}