using System;
using System.IO;

namespace Halibut.Tests
{
    public class TraceLogFileLogger : IDisposable
    {
        string tempFilePath = Path.GetTempFileName();
        string testHash;

        public void SetTestHash(string testHash)
        {
            this.testHash = testHash;
        }
        
        public void WriteLine(string logMessage)
        {
            File.AppendAllLines(tempFilePath, new[] { logMessage });
        }

        public bool CopyLogFileToArtifacts()
        {
            // The current directory is expected to have the following structure
            // (w/ variance depending on Debug/Release and dotnet framework used (net6.0, net48 etc):
            //
            // <REPO ROOT>\source\Halibut.Tests\bin\Debug\net6.0
            //
            // Therefore we go up 5 levels to get to the <REPO ROOT> directory,
            // from which point we can navigate to the artifacts directory.
            var currentDirectory = Directory.GetCurrentDirectory();
            var rootDirectory = new DirectoryInfo(currentDirectory).Parent.Parent.Parent.Parent.Parent;
            
            var traceLogsDirectory = rootDirectory.CreateSubdirectory("artifacts").CreateSubdirectory("trace-logs");
            var fileName = $"{testHash}.tracelog";

            try
            {
                File.Copy(tempFilePath, Path.Combine(traceLogsDirectory.ToString(), fileName), true);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public void Dispose()
        {
            try
            {
                File.Delete(tempFilePath);
            }
            catch
            {
                // Best effort clean-up, but we don't want to
                // fail the test because we couldn't delete this file
            }
        }
    }
}
